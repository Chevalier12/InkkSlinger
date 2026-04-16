using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DefaultFontFallbackControlTests
{
    [Fact]
    public void CheckBox_WithoutExplicitTypography_UsesDefaultRendererTypographyForMeasure()
    {
        var checkBox = new CheckBox
        {
            Content = "Enable feature"
        };

        checkBox.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

        Assert.True(checkBox.DesiredSize.X > checkBox.Padding.Horizontal + 14f);
    }

    [Fact]
    public void RadioButton_WithoutExplicitTypography_UsesDefaultRendererTypographyForMeasure()
    {
        var radioButton = new RadioButton
        {
            Content = "Option A"
        };

        radioButton.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

        Assert.True(radioButton.DesiredSize.X > radioButton.Padding.Horizontal + 14f);
    }

    [Fact]
    public void TabControl_WithoutExplicitTypography_UsesDefaultRendererTypographyForHeaderMeasure()
    {
        var tabControl = new TabControl();
        tabControl.Items.Add("General");
        tabControl.Items.Add("Advanced");
        tabControl.SelectedIndex = 0;

        tabControl.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

        Assert.True(tabControl.DesiredSize.X > 0f);
        Assert.True(tabControl.DesiredSize.Y > 0f);
    }

    [Fact]
    public void ControlTree_Inherits_FontStyle_AlongsideOtherTypographyProperties()
    {
        var host = new StackPanel();
        FrameworkElement.SetFontFamily(host, "Segoe UI");
        FrameworkElement.SetFontSize(host, 15f);
        FrameworkElement.SetFontWeight(host, "SemiBold");
        FrameworkElement.SetFontStyle(host, "Italic");
        var checkBox = new CheckBox
        {
            Content = "Inherit me"
        };

        host.AddChild(checkBox);

        Assert.Equal("Segoe UI", checkBox.FontFamily);
        Assert.True(checkBox.FontSize >= 14f);
        Assert.Equal("SemiBold", checkBox.FontWeight);
        Assert.Equal("Italic", checkBox.FontStyle);
    }
}
