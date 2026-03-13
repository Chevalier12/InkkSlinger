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

        public UiTextMetrics Measure(UiResolvedTypeface typeface, float fontSize, string text, UiTextStyleOverride styleOverride)
        {
            _ = fontSize;
            _ = styleOverride;
            LastTypeface = typeface;
            return new UiTextMetrics(text.Length * 5f, 11f, 12f);
        }

        public UiGlyphRasterized Rasterize(UiResolvedTypeface typeface, float fontSize, int codePoint, UiTextAntialiasMode antialiasMode)
        {
            _ = typeface;
            _ = fontSize;
            _ = codePoint;
            _ = antialiasMode;
            throw new NotSupportedException("Rasterization is not used by this test.");
        }

        public void Dispose()
        {
        }
    }
}
