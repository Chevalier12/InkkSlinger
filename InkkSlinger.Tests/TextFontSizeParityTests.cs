using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TextFontSizeParityTests
{
    [Fact]
    public void TextBlock_FontSize_AffectsDesiredHeight()
    {
        var smaller = new TextBlock
        {
            Text = "Sample",
            FontSize = 12f
        };
        var larger = new TextBlock
        {
            Text = "Sample",
            FontSize = 20f
        };

        smaller.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        larger.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

        Assert.True(larger.DesiredSize.Y > smaller.DesiredSize.Y);
    }

    [Fact]
    public void TextBox_FontSize_AffectsDesiredHeight()
    {
        var smaller = new TextBox
        {
            Text = "Sample",
            FontSize = 12f
        };
        var larger = new TextBox
        {
            Text = "Sample",
            FontSize = 20f
        };

        smaller.Measure(new Vector2(240f, float.PositiveInfinity));
        larger.Measure(new Vector2(240f, float.PositiveInfinity));

        Assert.True(larger.DesiredSize.Y > smaller.DesiredSize.Y);
    }
}
