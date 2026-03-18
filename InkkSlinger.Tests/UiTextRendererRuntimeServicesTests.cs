using System;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class UiTextRendererRuntimeServicesTests
{
    [Fact]
    public void MeasureWidth_UsesInjectedRuntimeServices()
    {
        var catalog = new FakeCatalog();
        var rasterizer = new FakeRasterizer();
        UiTextRenderer.ConfigureRuntimeServicesForTests(catalog, rasterizer);

        try
        {
            var typography = new UiTypography("Injected Sans", 10f, "Normal", "Italic");

            var width = UiTextRenderer.MeasureWidth(typography, "abcd");
            var height = UiTextRenderer.MeasureHeight(typography, "abcd");
            var lineHeight = UiTextRenderer.GetLineHeight(typography);

            Assert.Equal(20f, width);
            Assert.Equal(11f, height);
            Assert.Equal(12f, lineHeight);
            Assert.Equal(8f, UiTextRenderer.GetBaselineOffsetForTests(typography, "abcd"));
            Assert.Equal("Injected Sans", catalog.LastTypography.Family);
            Assert.Equal("Italic", catalog.LastTypography.Style);
            Assert.Equal("Injected Sans", rasterizer.LastTypeface.Family);
            Assert.Equal("Italic", rasterizer.LastTypeface.Style);
        }
        finally
        {
            UiTextRenderer.ConfigureRuntimeServicesForTests();
        }
    }

    [Fact]
    public void DrawWidth_UsesInjectedKerningRuntimeService()
    {
        var catalog = new FakeCatalog();
        var rasterizer = new FakeRasterizer();
        UiTextRenderer.ConfigureRuntimeServicesForTests(catalog, rasterizer);

        try
        {
            var typography = new UiTypography("Injected Sans", 10f, "Normal", "Normal");

            var measuredWidth = UiTextRenderer.MeasureWidth(typography, "AV");
            var drawnWidth = UiTextRenderer.GetDrawWidthForTests(typography, "AV");

            Assert.Equal(8.25f, measuredWidth);
            Assert.Equal(measuredWidth, drawnWidth);
            Assert.Equal(1, rasterizer.KerningRequestCount);
        }
        finally
        {
            UiTextRenderer.ConfigureRuntimeServicesForTests();
        }
    }

    [Fact]
    public void GlyphDrawPositions_PreserveFractionalAdvanceSpacing()
    {
        var catalog = new FakeCatalog();
        var rasterizer = new FakeRasterizer();
        UiTextRenderer.ConfigureRuntimeServicesForTests(catalog, rasterizer);

        try
        {
            var typography = new UiTypography("Injected Sans", 10f, "Normal", "Normal");

            var positions = UiTextRenderer.GetGlyphDrawPositionsForTests(typography, "ABC");

            Assert.Equal(3, positions.Count);
            Assert.Equal(0f, positions[0].X, 3);
            Assert.Equal(5.25f, positions[1].X, 3);
            Assert.Equal(10.5f, positions[2].X, 3);
        }
        finally
        {
            UiTextRenderer.ConfigureRuntimeServicesForTests();
        }
    }

    [Fact]
    public void GlyphDrawPositions_Newline_ResetsXAndAdvancesY()
    {
        var catalog = new FakeCatalog();
        var rasterizer = new FakeRasterizer();
        UiTextRenderer.ConfigureRuntimeServicesForTests(catalog, rasterizer);

        try
        {
            var typography = new UiTypography("Injected Sans", 10f, "Normal", "Normal");

            var positions = UiTextRenderer.GetGlyphDrawPositionsForTests(typography, "A\nBC");

            Assert.Equal(3, positions.Count);
            Assert.Equal(0f, positions[0].X, 3);
            Assert.Equal(0f, positions[1].X, 3);
            Assert.True(positions[1].Y > positions[0].Y);
            Assert.Equal(5.25f, positions[2].X, 3);
            Assert.True(positions[2].Y >= positions[1].Y);
        }
        finally
        {
            UiTextRenderer.ConfigureRuntimeServicesForTests();
        }
    }

    private sealed class FakeCatalog : IUiFontCatalog
    {
        public UiTypography LastTypography { get; private set; }

        public UiResolvedTypeface Resolve(UiTypography typography)
        {
            LastTypography = typography;
            return new UiResolvedTypeface(typography.Family, typography.Weight, typography.Style, "fake.ttf", 400);
        }
    }

    private sealed class FakeRasterizer : IUiFontRasterizer
    {
        public UiResolvedTypeface LastTypeface { get; private set; }
        public int KerningRequestCount { get; private set; }

        public UiTextMetrics Measure(UiResolvedTypeface typeface, float fontSize, string text, UiTextStyleOverride styleOverride)
        {
            _ = fontSize;
            _ = styleOverride;
            LastTypeface = typeface;
            var width = text switch
            {
                "AV" => 8.25f,
                _ => text.Length * 5f
            };
            return new UiTextMetrics(width, 11f, 12f, 8f, 3f);
        }

        public UiGlyphRasterized Rasterize(UiResolvedTypeface typeface, float fontSize, int codePoint, UiTextAntialiasMode antialiasMode)
        {
            LastTypeface = typeface;
            _ = fontSize;
            _ = antialiasMode;
            return codePoint switch
            {
                'A' => new UiGlyphRasterized([], 0, 0, 1, 0f, 8f, 5.25f, antialiasMode),
                'B' => new UiGlyphRasterized([], 0, 0, 3, 0f, 8f, 5.25f, antialiasMode),
                'C' => new UiGlyphRasterized([], 0, 0, 4, 0f, 8f, 5.25f, antialiasMode),
                'V' => new UiGlyphRasterized([], 0, 0, 2, 0f, 8f, 5f, antialiasMode),
                _ => new UiGlyphRasterized([], 0, 0, (uint)codePoint, 0f, 8f, 5f, antialiasMode)
            };
        }

        public float GetKerning(UiResolvedTypeface typeface, float fontSize, uint leftGlyphIndex, uint rightGlyphIndex)
        {
            LastTypeface = typeface;
            _ = fontSize;
            KerningRequestCount++;
            return leftGlyphIndex == 1 && rightGlyphIndex == 2 ? -2f : 0f;
        }

        public void Dispose()
        {
        }
    }
}
