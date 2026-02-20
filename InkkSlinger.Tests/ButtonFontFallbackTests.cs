using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class ButtonFontFallbackTests
{
    [Fact]
    public void Measure_WithNullFont_StillIncludesTextLineHeight()
    {
        var button = new Button
        {
            Text = "Open Popup",
            Font = null,
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };

        button.Measure(new Vector2(300f, 120f));

        var minimumWithoutText = button.Padding.Vertical + (button.BorderThickness * 2f);
        var expectedWithText = minimumWithoutText + FontStashTextRenderer.GetLineHeight(null);

        Assert.True(button.DesiredSize.Y >= expectedWithText - 0.01f);
    }

    [Fact]
    public void Measure_WhenFontStashEnabled_AndFontNull_ExpandsWidthForText()
    {
        if (!FontStashTextRenderer.IsEnabled)
        {
            return;
        }

        var button = new Button
        {
            Text = "Toggle Fullscreen",
            Font = null,
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };

        button.Measure(new Vector2(500f, 120f));

        var chromeWidth = button.Padding.Horizontal + (button.BorderThickness * 2f);
        Assert.True(button.DesiredSize.X > chromeWidth);
    }
}
