using Xunit;

namespace InkkSlinger.Tests;

public sealed class SliderXamlTests
{
    [Fact]
    public void LoadFromXaml_ParsesTicksAndParityProperties()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Slider x:Name="ParitySlider"
                    Minimum="0"
                    Maximum="10"
                    TickPlacement="Both"
                    AutoToolTipPlacement="BottomRight"
                    IsSelectionRangeEnabled="True"
                    SelectionStart="2.5"
                    SelectionEnd="7.5"
                    Ticks="0,2.5,5,10" />
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var slider = Assert.IsType<Slider>(root.FindName("ParitySlider"));

        Assert.Equal(TickPlacement.Both, slider.TickPlacement);
        Assert.Equal(AutoToolTipPlacement.BottomRight, slider.AutoToolTipPlacement);
        Assert.True(slider.IsSelectionRangeEnabled);
        Assert.Equal(2.5f, slider.SelectionStart);
        Assert.Equal(7.5f, slider.SelectionEnd);
        Assert.Collection(
            slider.Ticks,
            value => Assert.Equal(0d, value),
            value => Assert.Equal(2.5d, value),
            value => Assert.Equal(5d, value),
            value => Assert.Equal(10d, value));
    }
}