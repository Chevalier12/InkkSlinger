using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class OwnerDrawnFontSizeParityTests
{
    [Fact]
    public void MenuItem_FontSize_AffectsDesiredSize()
    {
        var smaller = new MenuItem
        {
            Header = "Open",
            FontSize = 12f
        };
        var larger = new MenuItem
        {
            Header = "Open",
            FontSize = 24f
        };

        smaller.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        larger.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

        Assert.True(larger.DesiredSize.X > smaller.DesiredSize.X);
        Assert.True(larger.DesiredSize.Y > smaller.DesiredSize.Y);
    }

    [Fact]
    public void CheckBox_FontSize_AffectsDesiredSize()
    {
        var smaller = new CheckBox
        {
            Content = "Enable feature",
            FontSize = 12f
        };
        var larger = new CheckBox
        {
            Content = "Enable feature",
            FontSize = 24f
        };

        smaller.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        larger.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

        Assert.True(larger.DesiredSize.X > smaller.DesiredSize.X);
        Assert.True(larger.DesiredSize.Y > smaller.DesiredSize.Y);
    }

    [Fact]
    public void ComboBox_FontSize_AffectsDesiredHeight()
    {
        var smaller = new ComboBox
        {
            FontSize = 12f
        };
        smaller.Items.Add("Option");
        smaller.SelectedIndex = 0;

        var larger = new ComboBox
        {
            FontSize = 24f
        };
        larger.Items.Add("Option");
        larger.SelectedIndex = 0;

        smaller.Measure(new Vector2(300f, float.PositiveInfinity));
        larger.Measure(new Vector2(300f, float.PositiveInfinity));

        Assert.True(larger.DesiredSize.Y > smaller.DesiredSize.Y);
    }
}
