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

            var scaleX = MathF.Abs(UiDrawing.GetScaleX(spriteBatch));
            var scaleY = MathF.Abs(UiDrawing.GetScaleY(spriteBatch));
            if (scaleX <= 0f)
            {
                scaleX = 1f;
            }

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
            var typeface = ResolveTypefaceCached(effectiveTypography);
            var pixelSize = Math.Max(1, (int)MathF.Round(effectiveTypography.Size * scaleY));
            var scaledTypography = effectiveTypography with { Size = effectiveTypography.Size * scaleY };

            var currentX = transformedPosition.X;
            var baselineY = transformedPosition.Y + GetLineHeight(scaledTypography);
            uint previousGlyphIndex = 0;
            foreach (var rune in text.EnumerateRunes())
            {
                var glyph = ResolveGlyph(spriteBatch.GraphicsDevice, typeface, pixelSize, rune.Value, mode);
                DrawGlyph(spriteBatch, glyph, currentX, baselineY, color);
                currentX += glyph.AdvanceX;
                _ = previousGlyphIndex;
            }
        }
        finally
        {
            _drawStringElapsedTicks += Stopwatch.GetTimestamp() - start;
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
            _measureWidthElapsedTicks += Stopwatch.GetTimestamp() - start;
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
            _getLineHeightElapsedTicks += Stopwatch.GetTimestamp() - start;
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
            _lineHeightCacheMissCount);
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

    private static void DrawGlyph(SpriteBatch spriteBatch, UiGlyphEntry glyph, float penX, float baselineY, Color color)
    {
        var destination = new Rectangle(
            (int)MathF.Round(penX + glyph.BearingX),
            (int)MathF.Round(baselineY - glyph.BearingY),
            glyph.SourceRect.Width,
            glyph.SourceRect.Height);
        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        spriteBatch.Draw(glyph.Texture, destination, glyph.SourceRect, color);
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
    int LineHeightCacheMissCount);

internal readonly record struct UiTextMetricsCacheKey(
    UiTypography Typography,
    string Text);

internal readonly record struct UiLineHeightCacheKey(
    UiTypography Typography);
