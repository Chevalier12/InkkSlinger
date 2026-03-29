using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class BorderParityTests
{
    [Fact]
    public void Border_InheritsDecorator()
    {
        Assert.IsAssignableFrom<Decorator>(new Border());
    }

    [Fact]
    public void Border_ColorAssignment_CreatesSolidColorBrushes()
    {
        var border = new Border
        {
            Background = new Color(0x22, 0x33, 0x44),
            BorderBrush = new Color(0x55, 0x66, 0x77),
            CornerRadius = new CornerRadius(6f, 4f, 2f, 1f)
        };

        AssertBrushColor(new Color(0x22, 0x33, 0x44), border.Background);
        AssertBrushColor(new Color(0x55, 0x66, 0x77), border.BorderBrush);
        AssertCornerRadius(border.CornerRadius, 6f, 4f, 2f, 1f);
    }

    [Fact]
    public void XamlLoader_BorderBrushAndCornerRadiusObjectElements_ParseToParitySurface()
    {
        const string xaml = """
<Border xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Background="CornflowerBlue"
        BorderBrush="#223344"
        BorderThickness="2">
  <Border.CornerRadius>
    <CornerRadius TopLeft="6" TopRight="4" BottomRight="2" BottomLeft="1" />
  </Border.CornerRadius>
  <TextBlock Text="Payload" />
</Border>
""";

        var border = Assert.IsType<Border>(XamlLoader.LoadFromString(xaml));

        AssertBrushColor(new Color(100, 149, 237), border.Background);
        AssertBrushColor(new Color(0x22, 0x33, 0x44), border.BorderBrush);
        Assert.Equal(new Thickness(2f), border.BorderThickness);
        AssertCornerRadius(border.CornerRadius, 6f, 4f, 2f, 1f);
        Assert.IsType<TextBlock>(Assert.Single(border.GetVisualChildren()));
    }

    [Fact]
    public void CornerRadius_CoercesNegativeValuesToZero()
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(-2f, 3f, 4f, -5f)
        };

        AssertCornerRadius(border.CornerRadius, 0f, 3f, 4f, 0f);
    }

    [Fact]
    public void BackgroundBrushMutation_InvalidatesRender()
    {
        var brush = new SolidColorBrush(new Color(10, 20, 30));
        var border = new Border
        {
            Background = brush
        };
        var uiRoot = new UiRoot(border);

        border.ClearRenderInvalidationRecursive();
        var startingBorderInvalidations = border.RenderInvalidationCount;
        var startingRootInvalidations = uiRoot.RenderInvalidationCount;

        brush.Color = new Color(30, 40, 50);

        Assert.True(border.NeedsRender);
        Assert.Equal(startingBorderInvalidations + 1, border.RenderInvalidationCount);
        Assert.Equal(startingRootInvalidations + 1, uiRoot.RenderInvalidationCount);
    }

    [Fact]
    public void GradientStopMutation_InvalidatesRender()
    {
        var brush = new LinearGradientBrush();
        var leading = new GradientStop(new Color(255, 0, 0), 0f);
        brush.GradientStops.Add(leading);
        brush.GradientStops.Add(new GradientStop(new Color(0, 0, 255), 1f));

        var border = new Border
        {
            Background = brush
        };
        var uiRoot = new UiRoot(border);

        border.ClearRenderInvalidationRecursive();
        var startingBorderInvalidations = border.RenderInvalidationCount;
        var startingRootInvalidations = uiRoot.RenderInvalidationCount;

        leading.Color = new Color(0, 255, 0);

        Assert.True(border.NeedsRender);
        Assert.Equal(startingBorderInvalidations + 1, border.RenderInvalidationCount);
        Assert.Equal(startingRootInvalidations + 1, uiRoot.RenderInvalidationCount);
    }

    private static void AssertBrushColor(Color expected, Brush? brush)
    {
        var actualBrush = Assert.IsAssignableFrom<Brush>(brush);
        Assert.Equal(expected, actualBrush.ToColor());
    }

    private static void AssertCornerRadius(CornerRadius actual, float topLeft, float topRight, float bottomRight, float bottomLeft)
    {
        Assert.Equal(topLeft, actual.TopLeft);
        Assert.Equal(topRight, actual.TopRight);
        Assert.Equal(bottomRight, actual.BottomRight);
        Assert.Equal(bottomLeft, actual.BottomLeft);
    }
}