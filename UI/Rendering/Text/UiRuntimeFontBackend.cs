using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using FreeTypeSharp;
using Microsoft.Win32;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static FreeTypeSharp.FT;
using static FreeTypeSharp.FT_Kerning_Mode_;
using static FreeTypeSharp.FT_LOAD;
using static FreeTypeSharp.FT_Render_Mode_;

namespace InkkSlinger;

internal interface IUiFontCatalog
{
    UiResolvedTypeface Resolve(UiTypography typography);
}

internal interface IUiFontRasterizer : IDisposable
{
    UiTextMetrics Measure(UiResolvedTypeface typeface, float fontSize, string text, UiTextStyleOverride styleOverride);

    UiGlyphRasterized Rasterize(UiResolvedTypeface typeface, float fontSize, int codePoint, UiTextAntialiasMode antialiasMode);

    float GetKerning(UiResolvedTypeface typeface, float fontSize, uint leftGlyphIndex, uint rightGlyphIndex);
}

internal sealed class WindowsInstalledFontCatalog : IUiFontCatalog
{
    private const string WindowsFontsRegistryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
    private readonly object _syncRoot = new();
    private Dictionary<string, List<UiResolvedTypeface>>? _facesByFamily;

    public UiResolvedTypeface Resolve(UiTypography typography)
    {
        EnsureLoaded();

        var requestedFamily = UiTypography.NormalizeFamily(typography.Family);
        if (TryResolveFromFamily(requestedFamily, typography.Weight, typography.Style, out var resolved))
        {
            return resolved;
        }

        if (!requestedFamily.Equals("Segoe UI", StringComparison.OrdinalIgnoreCase) &&
            TryResolveFromFamily("Segoe UI", typography.Weight, typography.Style, out resolved))
        {
            return resolved;
        }

        if (!requestedFamily.Equals("Segoe UI Symbol", StringComparison.OrdinalIgnoreCase) &&
            TryResolveFromFamily("Segoe UI Symbol", typography.Weight, typography.Style, out resolved))
        {
            return resolved;
        }

        if (_facesByFamily is { Count: > 0 })
        {
            return _facesByFamily.Values.SelectMany(static faces => faces).First();
        }

        throw new InvalidOperationException("No Windows fonts were discovered.");
    }

    private bool TryResolveFromFamily(string family, string weight, string style, out UiResolvedTypeface resolved)
    {
        EnsureLoaded();
        if (_facesByFamily == null ||
            !_facesByFamily.TryGetValue(family, out var faces) ||
            faces.Count == 0)
        {
            resolved = default;
            return false;
        }

        var requestedWeight = ParseWeight(weight);
        var requestedItalic = IsItalic(style);
        resolved = faces
            .OrderBy(face => Math.Abs(face.WeightValue - requestedWeight))
            .ThenBy(face => string.Equals(face.Style, requestedItalic ? "Italic" : "Normal", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(face => face.FontPath, StringComparer.OrdinalIgnoreCase)
            .First();
        return true;
    }

    private void EnsureLoaded()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The Windows installed font catalog is only supported on Windows.");
        }

        if (_facesByFamily != null)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_facesByFamily != null)
            {
                return;
            }

            var catalog = new Dictionary<string, List<UiResolvedTypeface>>(StringComparer.OrdinalIgnoreCase);
            using var fontsKey = Registry.LocalMachine.OpenSubKey(WindowsFontsRegistryPath);
            if (fontsKey == null)
            {
                throw new PlatformNotSupportedException("Windows fonts registry key was not found.");
            }

            var fontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            foreach (var valueName in fontsKey.GetValueNames())
            {
                if (fontsKey.GetValue(valueName) is not string fileName ||
                    string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                var fontPath = Path.IsPathRooted(fileName)
                    ? fileName
                    : Path.Combine(fontsDirectory, fileName);
                if (!File.Exists(fontPath))
                {
                    continue;
                }

                var descriptor = ParseDescriptor(valueName, fontPath);
                if (!catalog.TryGetValue(descriptor.Family, out var faces))
                {
                    faces = [];
                    catalog.Add(descriptor.Family, faces);
                }

                if (faces.Any(existing => string.Equals(existing.FontPath, descriptor.FontPath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                faces.Add(descriptor);
            }

            _facesByFamily = catalog;
        }
    }

    private static UiResolvedTypeface ParseDescriptor(string registryName, string fontPath)
    {
        var normalizedName = registryName;
        var trueTypeMarker = normalizedName.IndexOf("(", StringComparison.Ordinal);
        if (trueTypeMarker >= 0)
        {
            normalizedName = normalizedName[..trueTypeMarker];
        }

        normalizedName = normalizedName.Trim();
        var style = "Normal";
        var weight = "Normal";

        if (normalizedName.Contains("Italic", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Contains("Oblique", StringComparison.OrdinalIgnoreCase))
        {
            style = "Italic";
            normalizedName = normalizedName.Replace("Italic", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("Oblique", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();
        }

        foreach (var candidate in WeightTokens)
        {
            if (!normalizedName.Contains(candidate.Key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            weight = candidate.Key;
            normalizedName = normalizedName.Replace(candidate.Key, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            break;
        }

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            normalizedName = "Segoe UI";
        }

        weight = UiTypography.NormalizeWeight(weight);
        style = UiTypography.NormalizeStyle(style);
        return new UiResolvedTypeface(
            normalizedName,
            weight,
            style,
            fontPath,
            ParseWeight(weight));
    }

    private static bool IsItalic(string style)
    {
        return UiTypography.NormalizeStyle(style).Equals("Italic", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseWeight(string weight)
    {
        var normalized = UiTypography.NormalizeWeight(weight);
        foreach (var candidate in WeightTokens)
        {
            if (normalized.Equals(candidate.Key, StringComparison.OrdinalIgnoreCase))
            {
                return candidate.Value;
            }
        }

        return 400;
    }

    private static readonly IReadOnlyList<KeyValuePair<string, int>> WeightTokens =
    [
        new("Black", 900),
        new("ExtraBold", 800),
        new("UltraBold", 800),
        new("Bold", 700),
        new("SemiBold", 600),
        new("DemiBold", 600),
        new("Medium", 500),
        new("Normal", 400),
        new("Regular", 400),
        new("Book", 400),
        new("Light", 300),
        new("Thin", 200)
    ];
}

internal sealed unsafe class UiGlyphAtlas : IDisposable
{
    private const int PageSize = 2048;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly List<UiGlyphAtlasPage> _pages = [];

    public UiGlyphAtlas(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public UiGlyphEntry AddGlyph(UiGlyphRasterized rasterized)
    {
        foreach (var page in _pages)
        {
            if (page.TryAllocate(rasterized.Width, rasterized.Height, out var sourceRect))
            {
                page.Texture.SetData(0, sourceRect, rasterized.PixelData, 0, rasterized.PixelData.Length);
                return new UiGlyphEntry(
                    page.Texture,
                    sourceRect,
                    rasterized.GlyphIndex,
                    rasterized.BearingX,
                    rasterized.BearingY,
                    rasterized.AdvanceX,
                    rasterized.AntialiasMode);
            }
        }

        var createdPage = new UiGlyphAtlasPage(_graphicsDevice, PageSize, PageSize);
        _pages.Add(createdPage);
        if (!createdPage.TryAllocate(rasterized.Width, rasterized.Height, out var rect))
        {
            throw new InvalidOperationException("Failed to allocate glyph in a fresh atlas page.");
        }

        createdPage.Texture.SetData(0, rect, rasterized.PixelData, 0, rasterized.PixelData.Length);
        return new UiGlyphEntry(
            createdPage.Texture,
            rect,
            rasterized.GlyphIndex,
            rasterized.BearingX,
            rasterized.BearingY,
            rasterized.AdvanceX,
            rasterized.AntialiasMode);
    }

    public void Dispose()
    {
        for (var i = 0; i < _pages.Count; i++)
        {
            _pages[i].Dispose();
        }

        _pages.Clear();
    }

    private sealed class UiGlyphAtlasPage : IDisposable
    {
        private readonly int _width;
        private readonly int _height;
        private int _cursorX = 1;
        private int _cursorY = 1;
        private int _rowHeight;

        public UiGlyphAtlasPage(GraphicsDevice graphicsDevice, int width, int height)
        {
            _width = width;
            _height = height;
            Texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
            var clear = new Color[width * height];
            Texture.SetData(clear);
        }

        public Texture2D Texture { get; }

        public bool TryAllocate(int width, int height, out Rectangle rect)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);

            if (_cursorX + width + 1 > _width)
            {
                _cursorX = 1;
                _cursorY += _rowHeight + 1;
                _rowHeight = 0;
            }

            if (_cursorY + height + 1 > _height)
            {
                rect = Rectangle.Empty;
                return false;
            }

            rect = new Rectangle(_cursorX, _cursorY, width, height);
            _cursorX += width + 1;
            _rowHeight = Math.Max(_rowHeight, height);
            return true;
        }

        public void Dispose()
        {
            Texture.Dispose();
        }
    }
}

internal sealed unsafe class FreeTypeFontRasterizer : IUiFontRasterizer
{
    private static long _measureElapsedTicks;
    private static int _measureCallCount;
    private static long _rasterizeElapsedTicks;
    private static int _rasterizeCallCount;
    private static int _faceCacheHitCount;
    private static int _faceCacheMissCount;
    private static int _faceSizeReuseHitCount;
    private static int _faceSizeChangeCount;
    private static int _glyphAdvanceCacheHitCount;
    private static int _glyphAdvanceCacheMissCount;
    private static int _kerningCacheHitCount;
    private static int _kerningCacheMissCount;
    private static int _verticalMetricsCacheHitCount;
    private static int _verticalMetricsCacheMissCount;
    private readonly object _syncRoot = new();
    private FT_LibraryRec_* _library;
    private readonly Dictionary<string, nint> _facesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _activeFacePixelSizes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<UiGlyphAdvanceCacheKey, UiGlyphAdvanceEntry> _glyphAdvanceCache = new();
    private readonly Dictionary<UiKerningCacheKey, float> _kerningCache = new();
    private readonly Dictionary<UiFaceSizeCacheKey, UiFaceVerticalMetrics> _verticalMetricsCache = new();
    private bool _disposed;

    public FreeTypeFontRasterizer()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The FreeType text renderer is Windows-only in this wave.");
        }

        FT_LibraryRec_* library = null;
        var error = FT_Init_FreeType(&library);
        if (error != 0)
        {
            throw new InvalidOperationException($"FT_Init_FreeType failed with error {error}.");
        }

        _library = library;

    }

    public UiTextMetrics Measure(UiResolvedTypeface typeface, float fontSize, string text, UiTextStyleOverride styleOverride)
    {
        var start = Stopwatch.GetTimestamp();
        _measureCallCount++;
        try
        {
            _ = styleOverride;
            lock (_syncRoot)
            {
                var face = GetFace(typeface.FontPath);
                var pixelSize = ResolvePixelSize(fontSize);
                EnsureFaceSize(face, typeface.FontPath, pixelSize);

                float width = 0f;
                var previousGlyphIndex = 0u;
                foreach (var rune in text.EnumerateRunes())
                {
                    var glyph = ResolveGlyphAdvance(face, typeface.FontPath, pixelSize, rune.Value, ResolveLoadFlags(UiTextAntialiasMode.Grayscale));
                    if (previousGlyphIndex != 0 && glyph.GlyphIndex != 0)
                    {
                        width += ResolveKerning(face, typeface.FontPath, pixelSize, previousGlyphIndex, glyph.GlyphIndex);
                    }

                    width += glyph.AdvanceX;
                    previousGlyphIndex = glyph.GlyphIndex;
                }

                var verticalMetrics = ResolveVerticalMetrics(face, typeface.FontPath, pixelSize);
                return new UiTextMetrics(
                    width,
                    verticalMetrics.Height,
                    verticalMetrics.LineHeight,
                    verticalMetrics.Ascent,
                    verticalMetrics.Descent);
            }
        }
        finally
        {
            _measureElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    public UiGlyphRasterized Rasterize(UiResolvedTypeface typeface, float fontSize, int codePoint, UiTextAntialiasMode antialiasMode)
    {
        var start = Stopwatch.GetTimestamp();
        _rasterizeCallCount++;
        try
        {
            lock (_syncRoot)
            {
                var face = GetFace(typeface.FontPath);
                var pixelSize = ResolvePixelSize(fontSize);
                EnsureFaceSize(face, typeface.FontPath, pixelSize);

                var glyphIndex = ResolveGlyphIndex(face, codePoint);
                var loadFlags = ResolveLoadFlags(antialiasMode);
                var loadError = FT_Load_Glyph(face, glyphIndex, loadFlags);
                if (loadError != 0)
                {
                    throw new InvalidOperationException($"FT_Load_Glyph failed with error {loadError}.");
                }

                var renderMode = antialiasMode == UiTextAntialiasMode.Lcd ? FT_RENDER_MODE_LCD : FT_RENDER_MODE_NORMAL;
                var renderError = FT_Render_Glyph(face->glyph, renderMode);
                if (renderError != 0)
                {
                    throw new InvalidOperationException($"FT_Render_Glyph failed with error {renderError}.");
                }

                var bitmap = face->glyph->bitmap;
                var pixelData = ConvertBitmap(bitmap, antialiasMode);
                return new UiGlyphRasterized(
                    pixelData,
                    (int)(antialiasMode == UiTextAntialiasMode.Lcd ? bitmap.width / 3 : bitmap.width),
                    (int)bitmap.rows,
                    glyphIndex,
                    face->glyph->bitmap_left,
                    face->glyph->bitmap_top,
                    face->glyph->advance.x / 64f,
                    antialiasMode);
            }
        }
        finally
        {
            _rasterizeElapsedTicks += Stopwatch.GetTimestamp() - start;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var pair in _facesByPath)
        {
            FT_Done_Face((FT_FaceRec_*)pair.Value);
        }

        _facesByPath.Clear();
        _activeFacePixelSizes.Clear();
        _glyphAdvanceCache.Clear();
        _kerningCache.Clear();
        _verticalMetricsCache.Clear();
        if (_library != null)
        {
            FT_Done_FreeType(_library);
            _library = null;
        }
    }

    public float GetKerning(UiResolvedTypeface typeface, float fontSize, uint leftGlyphIndex, uint rightGlyphIndex)
    {
        if (leftGlyphIndex == 0 || rightGlyphIndex == 0)
        {
            return 0f;
        }

        lock (_syncRoot)
        {
            var face = GetFace(typeface.FontPath);
            var pixelSize = ResolvePixelSize(fontSize);
            EnsureFaceSize(face, typeface.FontPath, pixelSize);
            return ResolveKerning(face, typeface.FontPath, pixelSize, leftGlyphIndex, rightGlyphIndex);
        }
    }

    private FT_FaceRec_* GetFace(string fontPath)
    {
        lock (_syncRoot)
        {
            if (_facesByPath.TryGetValue(fontPath, out var cached))
            {
                _faceCacheHitCount++;
                return (FT_FaceRec_*)cached;
            }

            _faceCacheMissCount++;
            var ansi = Marshal.StringToHGlobalAnsi(fontPath);
            try
            {
                FT_FaceRec_* face;
                var error = FT_New_Face(_library, (byte*)ansi, 0, &face);
                if (error != 0)
                {
                    throw new InvalidOperationException($"FT_New_Face failed for '{fontPath}' with error {error}.");
                }

                _facesByPath.Add(fontPath, (nint)face);
                return face;
            }
            finally
            {
                Marshal.FreeHGlobal(ansi);
            }
        }
    }

    private void EnsureFaceSize(FT_FaceRec_* face, string fontPath, int pixelSize)
    {
        if (_activeFacePixelSizes.TryGetValue(fontPath, out var activePixelSize) &&
            activePixelSize == pixelSize)
        {
            _faceSizeReuseHitCount++;
            return;
        }

        var error = FT_Set_Pixel_Sizes(face, 0, (uint)Math.Max(1, pixelSize));
        if (error != 0)
        {
            throw new InvalidOperationException($"FT_Set_Pixel_Sizes failed with error {error}.");
        }

        _faceSizeChangeCount++;
        _activeFacePixelSizes[fontPath] = pixelSize;
    }

    private static int ResolvePixelSize(float fontSize)
    {
        return Math.Max(1, (int)MathF.Round(fontSize));
    }

    private static FT_LOAD ResolveLoadFlags(UiTextAntialiasMode antialiasMode)
    {
        return antialiasMode == UiTextAntialiasMode.Lcd
            ? (FT_LOAD)((int)FT_LOAD_DEFAULT | (int)FT_LOAD_TARGET_LCD)
            : (FT_LOAD)((int)FT_LOAD_DEFAULT | (int)FT_LOAD_TARGET_NORMAL);
    }

    private static uint ResolveGlyphIndex(FT_FaceRec_* face, int codePoint)
    {
        var glyphIndex = FT_Get_Char_Index(face, (uint)codePoint);
        if (glyphIndex == 0)
        {
            glyphIndex = FT_Get_Char_Index(face, (uint)'?');
        }

        return glyphIndex;
    }

    private UiGlyphAdvanceEntry ResolveGlyphAdvance(FT_FaceRec_* face, string fontPath, int pixelSize, int codePoint, FT_LOAD loadFlags)
    {
        var key = new UiGlyphAdvanceCacheKey(fontPath, pixelSize, codePoint);
        if (_glyphAdvanceCache.TryGetValue(key, out var cached))
        {
            _glyphAdvanceCacheHitCount++;
            return cached;
        }

        _glyphAdvanceCacheMissCount++;
        var glyphIndex = ResolveGlyphIndex(face, codePoint);
        var error = FT_Load_Glyph(face, glyphIndex, loadFlags);
        if (error != 0)
        {
            throw new InvalidOperationException($"FT_Load_Glyph failed with error {error}.");
        }

        cached = new UiGlyphAdvanceEntry(glyphIndex, face->glyph->advance.x / 64f);
        _glyphAdvanceCache[key] = cached;
        return cached;
    }

    private float ResolveKerning(FT_FaceRec_* face, string fontPath, int pixelSize, uint leftGlyphIndex, uint rightGlyphIndex)
    {
        var key = new UiKerningCacheKey(fontPath, pixelSize, leftGlyphIndex, rightGlyphIndex);
        if (_kerningCache.TryGetValue(key, out var cached))
        {
            _kerningCacheHitCount++;
            return cached;
        }

        _kerningCacheMissCount++;
        FT_Vector_ kerning;
        FT_Get_Kerning(face, leftGlyphIndex, rightGlyphIndex, FT_KERNING_DEFAULT, &kerning);
        cached = kerning.x / 64f;
        _kerningCache[key] = cached;
        return cached;
    }

    private UiFaceVerticalMetrics ResolveVerticalMetrics(FT_FaceRec_* face, string fontPath, int pixelSize)
    {
        var key = new UiFaceSizeCacheKey(fontPath, pixelSize);
        if (_verticalMetricsCache.TryGetValue(key, out var cached))
        {
            _verticalMetricsCacheHitCount++;
            return cached;
        }

        _verticalMetricsCacheMissCount++;
        var lineHeight = face->size->metrics.height / 64f;
        var ascent = face->size->metrics.ascender / 64f;
        var descent = Math.Abs(face->size->metrics.descender / 64f);
        cached = new UiFaceVerticalMetrics(ascent + descent, lineHeight, ascent, descent);
        _verticalMetricsCache[key] = cached;
        return cached;
    }

    internal static UiRuntimeFontBackendTimingSnapshot GetTimingSnapshotForTests()
    {
        return new UiRuntimeFontBackendTimingSnapshot(
            _measureElapsedTicks,
            _measureCallCount,
            _rasterizeElapsedTicks,
            _rasterizeCallCount,
            _faceCacheHitCount,
            _faceCacheMissCount,
            _faceSizeReuseHitCount,
            _faceSizeChangeCount,
            _glyphAdvanceCacheHitCount,
            _glyphAdvanceCacheMissCount,
            _kerningCacheHitCount,
            _kerningCacheMissCount,
            _verticalMetricsCacheHitCount,
            _verticalMetricsCacheMissCount);
    }

    internal static void ResetTimingForTests()
    {
        _measureElapsedTicks = 0;
        _measureCallCount = 0;
        _rasterizeElapsedTicks = 0;
        _rasterizeCallCount = 0;
        _faceCacheHitCount = 0;
        _faceCacheMissCount = 0;
        _faceSizeReuseHitCount = 0;
        _faceSizeChangeCount = 0;
        _glyphAdvanceCacheHitCount = 0;
        _glyphAdvanceCacheMissCount = 0;
        _kerningCacheHitCount = 0;
        _kerningCacheMissCount = 0;
        _verticalMetricsCacheHitCount = 0;
        _verticalMetricsCacheMissCount = 0;
    }

    private static Color[] ConvertBitmap(FT_Bitmap_ bitmap, UiTextAntialiasMode antialiasMode)
    {
        var source = bitmap.buffer;
        if (source == null || bitmap.rows <= 0 || bitmap.width <= 0)
        {
            return [Color.Transparent];
        }

        if (antialiasMode == UiTextAntialiasMode.Lcd)
        {
            var width = bitmap.width / 3;
            var pixels = new Color[Math.Max(1, width * bitmap.rows)];
            for (var y = 0; y < bitmap.rows; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = (y * bitmap.pitch) + (x * 3);
                    var r = source[index];
                    var g = source[index + 1];
                    var b = source[index + 2];
                    pixels[(y * width) + x] = CreatePremultipliedLcdCoverageColor(r, g, b);
                }
            }

            return pixels;
        }

        {
            var pixels = new Color[Math.Max(1, bitmap.width * bitmap.rows)];
            for (var y = 0; y < bitmap.rows; y++)
            {
                for (var x = 0; x < bitmap.width; x++)
                {
                    var coverage = source[(y * bitmap.pitch) + x];
                    pixels[(y * bitmap.width) + x] = CreatePremultipliedCoverageColor(coverage);
                }
            }

            return pixels;
        }
    }

    internal static Color CreatePremultipliedCoverageColor(byte coverage)
    {
        return new Color(coverage, coverage, coverage, coverage);
    }

    internal static Color CreatePremultipliedLcdCoverageColor(byte redCoverage, byte greenCoverage, byte blueCoverage)
    {
        var alpha = Math.Max(redCoverage, Math.Max(greenCoverage, blueCoverage));
        if (alpha == 0)
        {
            return Color.Transparent;
        }

        return new Color(
            PremultiplyChannel(redCoverage, alpha),
            PremultiplyChannel(greenCoverage, alpha),
            PremultiplyChannel(blueCoverage, alpha),
            alpha);
    }

    private static byte PremultiplyChannel(byte channel, byte alpha)
    {
        return (byte)((channel * alpha + 127) / 255);
    }
}

internal readonly record struct UiGlyphAdvanceCacheKey(
    string FontPath,
    int PixelSize,
    int CodePoint);

internal readonly record struct UiKerningCacheKey(
    string FontPath,
    int PixelSize,
    uint LeftGlyphIndex,
    uint RightGlyphIndex);

internal readonly record struct UiFaceSizeCacheKey(
    string FontPath,
    int PixelSize);

internal readonly record struct UiGlyphAdvanceEntry(
    uint GlyphIndex,
    float AdvanceX);

internal readonly record struct UiFaceVerticalMetrics(
    float Height,
    float LineHeight,
    float Ascent,
    float Descent);

internal readonly record struct UiRuntimeFontBackendTimingSnapshot(
    long MeasureElapsedTicks,
    int MeasureCallCount,
    long RasterizeElapsedTicks,
    int RasterizeCallCount,
    int FaceCacheHitCount,
    int FaceCacheMissCount,
    int FaceSizeReuseHitCount,
    int FaceSizeChangeCount,
    int GlyphAdvanceCacheHitCount,
    int GlyphAdvanceCacheMissCount,
    int KerningCacheHitCount,
    int KerningCacheMissCount,
    int VerticalMetricsCacheHitCount,
    int VerticalMetricsCacheMissCount);
