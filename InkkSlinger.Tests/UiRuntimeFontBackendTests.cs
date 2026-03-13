using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class UiRuntimeFontBackendTests
{
    [Fact]
    public void GrayscaleCoveragePixels_ArePremultipliedForAlphaBlend()
    {
        var pixel = FreeTypeFontRasterizer.CreatePremultipliedCoverageColor(128);

        Assert.Equal(new Color(128, 128, 128, 128), pixel);
    }

    [Fact]
    public void LcdCoveragePixels_ArePremultipliedForAlphaBlend()
    {
        var pixel = FreeTypeFontRasterizer.CreatePremultipliedLcdCoverageColor(64, 128, 255);

        Assert.Equal(255, pixel.A);
        Assert.Equal(64, pixel.R);
        Assert.Equal(128, pixel.G);
        Assert.Equal(255, pixel.B);
    }
}
