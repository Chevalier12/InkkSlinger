using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class ButtonFontFallbackTests
{
    [Fact]
    public void Measure_WithoutExplicitTypography_StillIncludesTextLineHeight()
    {
        var button = new Button
        {
            Content = "Open Popup",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };

        button.Measure(new Vector2(300f, 120f));

        var minimumWithoutText = button.Padding.Vertical + (button.BorderThickness * 2f);
        var expectedWithText = minimumWithoutText + UiTextRenderer.GetLineHeight(button, button.FontSize);

        Assert.True(button.DesiredSize.Y >= expectedWithText - 0.01f);
    }

    [Fact]
    public void Measure_WithoutExplicitTypography_ExpandsWidthForText()
    {
        var button = new Button
        {
            Content = "Toggle Fullscreen",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };

        button.Measure(new Vector2(500f, 120f));

        var chromeWidth = button.Padding.Horizontal + (button.BorderThickness * 2f);
        Assert.True(button.DesiredSize.X > chromeWidth);
    }
}
