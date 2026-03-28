using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal static class UiTextRenderer
{
    private const int MetricsCacheCapacity = 4096;
    private const int LineHeightCacheCapacity = 256;
    private const int ShapedTextLayoutCacheCapacity = 2048;
    private static readonly object SyncRoot = new();
    private static readonly HashSet<string> PrewarmedGlyphBuckets = new(StringComparer.Ordinal);
    private static readonly Dictionary<GraphicsDevice, UiGlyphAtlas> GrayscaleAtlases = new();
    private static readonly Dictionary<GraphicsDevice, UiGlyphAtlas> LcdAtlases = new();
    private static readonly Dictionary<UiGlyphKey, UiGlyphEntry> GlyphCache = new();
    private static readonly Dictionary<UiTypography, UiResolvedTypeface> TypefaceCache = new();
    private static readonly Dictionary<UiTextMetricsCacheKey, UiTextMetrics> MetricsCache = new();
    private static readonly Queue<UiTextMetricsCacheKey> MetricsCacheOrder = new();
    private static readonly Dictionary<UiLineHeightCacheKey, float> LineHeightCache = new();
    private static readonly Queue<UiLineHeightCacheKey> LineHeightCacheOrder = new();
    private static readonly Dictionary<UiShapedTextLayoutCacheKey, UiShapedTextLayout> ShapedTextLayoutCache = new();
    private static readonly Queue<UiShapedTextLayoutCacheKey> ShapedTextLayoutCacheOrder = new();
    private static IUiFontCatalog _fontCatalog = CreateDefaultFontCatalog();
    private static IUiFontRasterizer _rasterizer = CreateDefaultRasterizer();
    private static UiTypography _defaultTypography = new("Segoe UI", 12f, "Normal", "Normal");
    private static long _measureWidthElapsedTicks;
    private static long _getLineHeightElapsedTicks;
    private static long _drawStringElapsedTicks;
    private static int _measureWidthCallCount;
    private static int _getLineHeightCallCount;
    private static int _drawStringCallCount;
    private static int _typefaceCacheHitCount;
    private static int _typefaceCacheMissCount;
    private static int _metricsCacheHitCount;
    private static int _metricsCacheMissCount;
    private static int _lineHeightCacheHitCount;
    private static int _lineHeightCacheMissCount;
    private static long _hottestDrawStringElapsedTicks;
    private static string _hottestDrawStringText = "none";
    private static string _hottestDrawStringTypography = "none";
    private static long _hottestMeasureWidthElapsedTicks;
    private static string _hottestMeasureWidthText = "none";
    private static string _hottestMeasureWidthTypography = "none";
    private static long _hottestLineHeightElapsedTicks;
    private static string _hottestLineHeightTypography = "none";

    public static void SetDefaultTypography(string family, float size = 12f, string weight = "Normal", string style = "Normal")
    {
        _defaultTypography = new UiTypography(
            UiTypography.NormalizeFamily(family),
            MathF.Max(1f, size),
            UiTypography.NormalizeWeight(weight),
            UiTypography.NormalizeStyle(style));
    }

    internal static void ConfigureRuntimeServicesForTests(IUiFontCatalog? fontCatalog = null, IUiFontRasterizer? rasterizer = null)
    {
        lock (SyncRoot)
        {
            DisposeAtlasesNoLock();
            GlyphCache.Clear();
            TypefaceCache.Clear();
            MetricsCache.Clear();
            MetricsCacheOrder.Clear();
            LineHeightCache.Clear();
            LineHeightCacheOrder.Clear();
            ShapedTextLayoutCache.Clear();
            ShapedTextLayoutCacheOrder.Clear();
            PrewarmedGlyphBuckets.Clear();

            var nextCatalog = fontCatalog ?? CreateDefaultFontCatalog();
            var nextRasterizer = rasterizer ?? CreateDefaultRasterizer();
            if (!ReferenceEquals(_rasterizer, nextRasterizer))
            {
                _rasterizer.Dispose();
            }

            _fontCatalog = nextCatalog;
            _rasterizer = nextRasterizer;
        }
    }

    internal static void PrewarmDefaultGlyphs(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        var presets = new[]
        {
            _defaultTypography with { Size = 12f, Weight = "Normal", Style = "Normal" },
            _defaultTypography with { Size = 15f, Weight = "SemiBold", Style = "Normal" },
            _defaultTypography with { Size = 16f, Weight = "SemiBold", Style = "Normal" }
        };

        foreach (var typography in presets)
        {
            PrewarmTypographyBucket(graphicsDevice, typography, UiTextAntialiasMode.Grayscale);
        }
    }

    public static UiTypography ResolveTypography(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var typography = UiTypography.FromElement(element);
        if (string.IsNullOrWhiteSpace(element.FontFamily))
        {
            typography = typography with { Family = _defaultTypography.Family };
        }

        return typography;
    }

    public static UiTypography ResolveTypography(FrameworkElement element, float fontSize, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        return (ResolveTypography(element) with { Size = MathF.Max(1f, fontSize) }).Apply(styleOverride);
    }

    public static void DrawString(
        SpriteBatch spriteBatch,
        FrameworkElement element,
        string text,
        Vector2 position,
        Color color,
        bool opaqueBackground = false,
        UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        DrawString(spriteBatch, ResolveTypography(element), text, position, color, opaqueBackground, styleOverride);
    }

    public static void DrawString(
        SpriteBatch spriteBatch,
        FrameworkElement element,
        string text,
        Vector2 position,
        Color color,
        float fontSize,
        bool opaqueBackground = false,
        UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        DrawString(spriteBatch, ResolveTypography(element, fontSize, styleOverride), text, position, color, opaqueBackground);
    }

    public static void DrawString(
        SpriteBatch spriteBatch,
        UiTypography typography,
        string text,
        Vector2 position,
        Color color,
        bool opaqueBackground = false,
        UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var start = Stopwatch.GetTimestamp();
        try
        {
            _drawStringCallCount++;

            var scaleY = MathF.Abs(UiDrawing.GetScaleY(spriteBatch));
            if (scaleY <= 0f)
            {
                scaleY = 1f;
            }

            var transformedPosition = UiDrawing.TransformPoint(spriteBatch, position);
            // LCD/subpixel glyph masks are invalid in this pipeline because text is rendered into
            // an intermediate alpha-blended render target instead of directly onto the final surface.
            // Grayscale masks are the only correct mode until there is a real final-surface compositor.
            _ = opaqueBackground;
            var mode = UiTextAntialiasMode.Grayscale;
            var effectiveTypography = typography.Apply(styleOverride);
            var scaledTypography = effectiveTypography with { Size = effectiveTypography.Size * scaleY };
            var typeface = ResolveTypefaceCached(scaledTypography);
            var pixelSize = Math.Max(1, (int)MathF.Round(scaledTypography.Size));
            var shapedLayout = ResolveShapedTextLayout(scaledTypography, text);
            for (var i = 0; i < shapedLayout.CodePoints.Length; i++)
            {
                var glyph = ResolveGlyph(spriteBatch.GraphicsDevice, typeface, pixelSize, shapedLayout.CodePoints[i], mode);
                DrawGlyphAtPosition(spriteBatch, glyph, transformedPosition + shapedLayout.DrawPositions[i], color);
            }
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - start;
            _drawStringElapsedTicks += elapsedTicks;
            RecordHottestDrawString(elapsedTicks, text, typography.Apply(styleOverride));
        }
    }

    public static float MeasureWidth(FrameworkElement element, string text, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        return MeasureWidth(ResolveTypography(element), text, styleOverride);
    }

    public static float MeasureWidth(FrameworkElement element, string text, float fontSize, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        return MeasureWidth(ResolveTypography(element, fontSize), text, styleOverride);
    }

    public static float MeasureWidth(UiTypography typography, string text, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        var start = Stopwatch.GetTimestamp();
        _measureWidthCallCount++;
        try
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            var effectiveTypography = typography.Apply(styleOverride);
            return GetTextMetrics(effectiveTypography, text, styleOverride).Width;
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - start;
            _measureWidthElapsedTicks += elapsedTicks;
            RecordHottestMeasureWidth(elapsedTicks, text, typography.Apply(styleOverride));
        }
    }

    public static float MeasureWidth(string text, float fontSize)
    {
        return MeasureWidth(_defaultTypography with { Size = fontSize }, text);
    }

    public static float MeasureHeight(UiTypography typography, string text, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        var effectiveTypography = typography.Apply(styleOverride);
        return GetTextMetrics(effectiveTypography, text, styleOverride).Height;
    }

    public static float MeasureHeight(FrameworkElement element, string text, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        return MeasureHeight(ResolveTypography(element), text, styleOverride);
    }

    public static float MeasureHeight(FrameworkElement element, string text, float fontSize, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        return MeasureHeight(ResolveTypography(element, fontSize), text, styleOverride);
    }

    public static float MeasureHeight(string text, float fontSize, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        return MeasureHeight(_defaultTypography with { Size = fontSize }, text, styleOverride);
    }

    public static float GetLineHeight(FrameworkElement element, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        return GetLineHeight(ResolveTypography(element), styleOverride);
    }

    public static float GetLineHeight(FrameworkElement element, float fontSize, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        return GetLineHeight(ResolveTypography(element, fontSize), styleOverride);
    }

    public static float GetLineHeight(UiTypography typography, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        var start = Stopwatch.GetTimestamp();
        _getLineHeightCallCount++;
        try
        {
            var effectiveTypography = typography.Apply(styleOverride);
            var cacheKey = new UiLineHeightCacheKey(effectiveTypography);
            lock (SyncRoot)
            {
                if (LineHeightCache.TryGetValue(cacheKey, out var cached))
                {
                    _lineHeightCacheHitCount++;
                    return cached;
                }
            }

            _lineHeightCacheMissCount++;
            var lineHeight = GetTextMetrics(effectiveTypography, "Ag", styleOverride).LineHeight;
            lock (SyncRoot)
            {
                AddLineHeightCacheEntryNoLock(cacheKey, lineHeight);
            }

            return lineHeight;
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - start;
            _getLineHeightElapsedTicks += elapsedTicks;
            RecordHottestLineHeight(elapsedTicks, typography.Apply(styleOverride));
        }
    }

    internal static UiTextRendererTimingSnapshot GetTimingSnapshotForTests()
    {
        return new UiTextRendererTimingSnapshot(
            _measureWidthElapsedTicks,
            _measureWidthCallCount,
            _getLineHeightElapsedTicks,
            _getLineHeightCallCount,
            _drawStringElapsedTicks,
            _drawStringCallCount,
            _typefaceCacheHitCount,
            _typefaceCacheMissCount,
            _metricsCacheHitCount,
            _metricsCacheMissCount,
            _lineHeightCacheHitCount,
            _lineHeightCacheMissCount,
            _hottestDrawStringText,
            _hottestDrawStringTypography,
            TicksToMilliseconds(_hottestDrawStringElapsedTicks),
            _hottestMeasureWidthText,
            _hottestMeasureWidthTypography,
            TicksToMilliseconds(_hottestMeasureWidthElapsedTicks),
            _hottestLineHeightTypography,
            TicksToMilliseconds(_hottestLineHeightElapsedTicks));
    }

    internal static void ResetTimingForTests()
    {
        _measureWidthElapsedTicks = 0;
        _measureWidthCallCount = 0;
        _getLineHeightElapsedTicks = 0;
        _getLineHeightCallCount = 0;
        _drawStringElapsedTicks = 0;
        _drawStringCallCount = 0;
        _typefaceCacheHitCount = 0;
        _typefaceCacheMissCount = 0;
        _metricsCacheHitCount = 0;
        _metricsCacheMissCount = 0;
        _lineHeightCacheHitCount = 0;
        _lineHeightCacheMissCount = 0;
        _hottestDrawStringElapsedTicks = 0;
        _hottestDrawStringText = "none";
        _hottestDrawStringTypography = "none";
        _hottestMeasureWidthElapsedTicks = 0;
        _hottestMeasureWidthText = "none";
        _hottestMeasureWidthTypography = "none";
        _hottestLineHeightElapsedTicks = 0;
        _hottestLineHeightTypography = "none";
    }

    private static void RecordHottestDrawString(long elapsedTicks, string text, UiTypography typography)
    {
        if (elapsedTicks <= _hottestDrawStringElapsedTicks)
        {
            return;
        }

        _hottestDrawStringElapsedTicks = elapsedTicks;
        _hottestDrawStringText = SummarizeText(text);
        _hottestDrawStringTypography = SummarizeTypography(typography);
    }

    private static void RecordHottestMeasureWidth(long elapsedTicks, string text, UiTypography typography)
    {
        if (elapsedTicks <= _hottestMeasureWidthElapsedTicks)
        {
            return;
        }

        _hottestMeasureWidthElapsedTicks = elapsedTicks;
        _hottestMeasureWidthText = SummarizeText(text);
        _hottestMeasureWidthTypography = SummarizeTypography(typography);
    }

    private static void RecordHottestLineHeight(long elapsedTicks, UiTypography typography)
    {
        if (elapsedTicks <= _hottestLineHeightElapsedTicks)
        {
            return;
        }

        _hottestLineHeightElapsedTicks = elapsedTicks;
        _hottestLineHeightTypography = SummarizeTypography(typography);
    }

    private static string SummarizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "empty";
        }

        var normalized = text.Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
        return normalized.Length <= 32
            ? normalized
            : normalized[..32] + "...";
    }

    private static string SummarizeTypography(UiTypography typography)
    {
        return $"{typography.Family}|{typography.Size:0.###}|{typography.Weight}|{typography.Style}";
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    internal static float GetBaselineOffsetForTests(UiTypography typography, string text, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        return GetTextMetrics(typography.Apply(styleOverride), text, styleOverride).Ascent;
    }

    internal static float GetDrawWidthForTests(UiTypography typography, string text, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        var effectiveTypography = typography.Apply(styleOverride);
        return ResolveShapedTextLayout(effectiveTypography, text).Width;
    }

    internal static IReadOnlyList<Vector2> GetGlyphDrawPositionsForTests(UiTypography typography, string text, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<Vector2>();
        }

        var effectiveTypography = typography.Apply(styleOverride);
        return ResolveShapedTextLayout(effectiveTypography, text).DrawPositions;
    }

    internal static LayoutRect GetInkBoundsForTests(UiTypography typography, string text, UiTextStyleOverride styleOverride = UiTextStyleOverride.None)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new LayoutRect(0f, 0f, 0f, 0f);
        }

        var effectiveTypography = typography.Apply(styleOverride);
        var typeface = ResolveTypefaceCached(effectiveTypography);
        var pixelSize = Math.Max(1, (int)MathF.Round(effectiveTypography.Size));
        var shapedLayout = ResolveShapedTextLayout(effectiveTypography, text);
        var hasBounds = false;
        var left = 0f;
        var top = 0f;
        var right = 0f;
        var bottom = 0f;

        for (var i = 0; i < shapedLayout.CodePoints.Length; i++)
        {
            var glyph = _rasterizer.Rasterize(typeface, pixelSize, shapedLayout.CodePoints[i], UiTextAntialiasMode.Grayscale);
            if (glyph.Width <= 0 || glyph.Height <= 0)
            {
                continue;
            }

            var position = shapedLayout.DrawPositions[i];
            var glyphLeft = position.X;
            var glyphTop = position.Y;
            var glyphRight = glyphLeft + glyph.Width;
            var glyphBottom = glyphTop + glyph.Height;

            if (!hasBounds)
            {
                left = glyphLeft;
                top = glyphTop;
                right = glyphRight;
                bottom = glyphBottom;
                hasBounds = true;
                continue;
            }

            left = MathF.Min(left, glyphLeft);
            top = MathF.Min(top, glyphTop);
            right = MathF.Max(right, glyphRight);
            bottom = MathF.Max(bottom, glyphBottom);
        }

        return hasBounds
            ? new LayoutRect(left, top, right - left, bottom - top)
            : new LayoutRect(0f, 0f, 0f, 0f);
    }

    private static UiShapedTextLayout ResolveShapedTextLayout(UiTypography typography, string text)
    {
        var cacheKey = new UiShapedTextLayoutCacheKey(typography, text);
        lock (SyncRoot)
        {
            if (ShapedTextLayoutCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }
        }

        var typeface = ResolveTypefaceCached(typography);
        var metrics = GetTextMetrics(typography, text, UiTextStyleOverride.None);
        var lineHeight = MathF.Max(1f, metrics.LineHeight);
        var pixelSize = Math.Max(1, (int)MathF.Round(typography.Size));
        var codePoints = new List<int>(text.Length);
        var positions = new List<Vector2>(text.Length);
        var penX = 0f;
        var baselineY = metrics.Ascent;
        var maxWidth = 0f;
        uint previousGlyphIndex = 0;

        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\r')
            {
                continue;
            }

            if (rune.Value == '\n')
            {
                maxWidth = MathF.Max(maxWidth, penX);
                penX = 0f;
                baselineY += lineHeight;
                previousGlyphIndex = 0;
                continue;
            }

            var glyph = _rasterizer.Rasterize(typeface, pixelSize, rune.Value, UiTextAntialiasMode.Grayscale);
            if (previousGlyphIndex != 0 && glyph.GlyphIndex != 0)
            {
                penX += _rasterizer.GetKerning(typeface, pixelSize, previousGlyphIndex, glyph.GlyphIndex);
            }

            codePoints.Add(rune.Value);
            positions.Add(GetGlyphDrawPosition(penX, baselineY, glyph.BearingX, glyph.BearingY));
            penX += glyph.AdvanceX;
            previousGlyphIndex = glyph.GlyphIndex;
        }

        var layout = new UiShapedTextLayout(codePoints.ToArray(), positions.ToArray(), MathF.Max(maxWidth, penX));
        lock (SyncRoot)
        {
            AddShapedTextLayoutEntryNoLock(cacheKey, layout);
        }

        return layout;
    }

    private static UiResolvedTypeface ResolveTypefaceCached(UiTypography typography)
    {
        lock (SyncRoot)
        {
            if (TypefaceCache.TryGetValue(typography, out var cached))
            {
                _typefaceCacheHitCount++;
                return cached;
            }
        }

        _typefaceCacheMissCount++;
        var resolved = _fontCatalog.Resolve(typography);
        lock (SyncRoot)
        {
            TypefaceCache[typography] = resolved;
        }

        return resolved;
    }

    private static UiTextMetrics GetTextMetrics(UiTypography typography, string text, UiTextStyleOverride styleOverride)
    {
        var cacheKey = new UiTextMetricsCacheKey(typography, text);
        lock (SyncRoot)
        {
            if (MetricsCache.TryGetValue(cacheKey, out var cached))
            {
                _metricsCacheHitCount++;
                return cached;
            }
        }

        _metricsCacheMissCount++;
        var typeface = ResolveTypefaceCached(typography);
        var measured = _rasterizer.Measure(typeface, typography.Size, text, styleOverride);
        lock (SyncRoot)
        {
            AddMetricsCacheEntryNoLock(cacheKey, measured);
        }

        return measured;
    }

    private static void AddMetricsCacheEntryNoLock(UiTextMetricsCacheKey key, UiTextMetrics metrics)
    {
        if (MetricsCache.ContainsKey(key))
        {
            return;
        }

        if (MetricsCache.Count >= MetricsCacheCapacity)
        {
            var evicted = MetricsCacheOrder.Dequeue();
            MetricsCache.Remove(evicted);
        }

        MetricsCache[key] = metrics;
        MetricsCacheOrder.Enqueue(key);
    }

    private static void AddLineHeightCacheEntryNoLock(UiLineHeightCacheKey key, float lineHeight)
    {
        if (LineHeightCache.ContainsKey(key))
        {
            return;
        }

        if (LineHeightCache.Count >= LineHeightCacheCapacity)
        {
            var evicted = LineHeightCacheOrder.Dequeue();
            LineHeightCache.Remove(evicted);
        }

        LineHeightCache[key] = lineHeight;
        LineHeightCacheOrder.Enqueue(key);
    }

    private static void AddShapedTextLayoutEntryNoLock(UiShapedTextLayoutCacheKey key, UiShapedTextLayout layout)
    {
        if (ShapedTextLayoutCache.ContainsKey(key))
        {
            return;
        }

        if (ShapedTextLayoutCache.Count >= ShapedTextLayoutCacheCapacity)
        {
            var evicted = ShapedTextLayoutCacheOrder.Dequeue();
            ShapedTextLayoutCache.Remove(evicted);
        }

        ShapedTextLayoutCache[key] = layout;
        ShapedTextLayoutCacheOrder.Enqueue(key);
    }

    private static IUiFontCatalog CreateDefaultFontCatalog()
    {
        return new WindowsInstalledFontCatalog();
    }

    private static IUiFontRasterizer CreateDefaultRasterizer()
    {
        return new FreeTypeFontRasterizer();
    }

    private static void PrewarmTypographyBucket(GraphicsDevice graphicsDevice, UiTypography typography, UiTextAntialiasMode mode)
    {
        var key = $"{RuntimeHelpers.GetHashCode(graphicsDevice)}|{typography.Family}|{typography.Size:0.###}|{typography.Weight}|{typography.Style}|{mode}";

        lock (SyncRoot)
        {
            if (!PrewarmedGlyphBuckets.Add(key))
            {
                return;
            }
        }

        var typeface = _fontCatalog.Resolve(typography);
        var pixelSize = Math.Max(1, (int)MathF.Round(typography.Size));
        for (var codePoint = 0x20; codePoint <= 0x7E; codePoint++)
        {
            _ = ResolveGlyph(graphicsDevice, typeface, pixelSize, codePoint, mode);
        }
    }

    private static UiGlyphEntry ResolveGlyph(
        GraphicsDevice graphicsDevice,
        UiResolvedTypeface typeface,
        int pixelSize,
        int codePoint,
        UiTextAntialiasMode mode)
    {
        var key = new UiGlyphKey(typeface.FontPath, pixelSize, codePoint, mode);
        lock (SyncRoot)
        {
            if (GlyphCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var rasterized = _rasterizer.Rasterize(typeface, pixelSize, codePoint, mode);
            var atlas = GetAtlas(graphicsDevice, mode);
            var created = atlas.AddGlyph(rasterized);
            GlyphCache[key] = created;
            return created;
        }
    }

    private static UiGlyphAtlas GetAtlas(GraphicsDevice graphicsDevice, UiTextAntialiasMode mode)
    {
        var store = mode == UiTextAntialiasMode.Lcd ? LcdAtlases : GrayscaleAtlases;
        if (store.TryGetValue(graphicsDevice, out var atlas))
        {
            return atlas;
        }

        atlas = new UiGlyphAtlas(graphicsDevice);
        store.Add(graphicsDevice, atlas);
        return atlas;
    }

    private static void DisposeAtlasesNoLock()
    {
        foreach (var atlas in GrayscaleAtlases.Values)
        {
            atlas.Dispose();
        }

        foreach (var atlas in LcdAtlases.Values)
        {
            atlas.Dispose();
        }

        GrayscaleAtlases.Clear();
        LcdAtlases.Clear();
    }

    private static Vector2 GetGlyphDrawPosition(float penX, float baselineY, float bearingX, float bearingY)
    {
        return new Vector2(penX + bearingX, baselineY - bearingY);
    }

    private static void DrawGlyph(SpriteBatch spriteBatch, UiGlyphEntry glyph, float penX, float baselineY, Color color)
    {
        if (glyph.SourceRect.Width <= 0 || glyph.SourceRect.Height <= 0)
        {
            return;
        }

        spriteBatch.Draw(
            glyph.Texture,
            GetGlyphDrawPosition(penX, baselineY, glyph.BearingX, glyph.BearingY),
            glyph.SourceRect,
            color);
    }

    private static void DrawGlyphAtPosition(SpriteBatch spriteBatch, UiGlyphEntry glyph, Vector2 position, Color color)
    {
        if (glyph.SourceRect.Width <= 0 || glyph.SourceRect.Height <= 0)
        {
            return;
        }

        spriteBatch.Draw(
            glyph.Texture,
            position,
            glyph.SourceRect,
            color);
    }
}

internal readonly record struct UiTextRendererTimingSnapshot(
    long MeasureWidthElapsedTicks,
    int MeasureWidthCallCount,
    long GetLineHeightElapsedTicks,
    int GetLineHeightCallCount,
    long DrawStringElapsedTicks,
    int DrawStringCallCount,
    int TypefaceCacheHitCount,
    int TypefaceCacheMissCount,
    int MetricsCacheHitCount,
    int MetricsCacheMissCount,
    int LineHeightCacheHitCount,
    int LineHeightCacheMissCount,
    string HottestDrawStringText,
    string HottestDrawStringTypography,
    double HottestDrawStringMilliseconds,
    string HottestMeasureWidthText,
    string HottestMeasureWidthTypography,
    double HottestMeasureWidthMilliseconds,
    string HottestLineHeightTypography,
    double HottestLineHeightMilliseconds);

internal readonly record struct UiTextMetricsCacheKey(
    UiTypography Typography,
    string Text);

internal readonly record struct UiLineHeightCacheKey(
    UiTypography Typography);

internal readonly record struct UiShapedTextLayoutCacheKey(
    UiTypography Typography,
    string Text);

internal readonly record struct UiShapedTextLayout(
    int[] CodePoints,
    Vector2[] DrawPositions,
    float Width);
