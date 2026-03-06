using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TextFontSizeParityTests
{
    [Fact]
    public void TextBlock_FontSize_AffectsDesiredHeight()
    {
        if (!IsScalableTextRendererEnabled())
        {
            return;
        }

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
        if (!IsScalableTextRendererEnabled())
        {
            return;
        }

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

    private static bool IsScalableTextRendererEnabled()
    {
        var rendererType = typeof(TextBlock).Assembly.GetType("InkkSlinger.FontStashTextRenderer");
        var property = rendererType?.GetProperty("IsEnabled", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        return property?.GetValue(null) as bool? == true;
    }
}
