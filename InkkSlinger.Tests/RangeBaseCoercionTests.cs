using Xunit;

namespace InkkSlinger.Tests;

public sealed class RangeBaseCoercionTests
{
    [Fact]
    public void Slider_MinimumAboveMaximum_CoercesMaximumAndValue()
    {
        var slider = new Slider
        {
            Maximum = 5f,
            Value = 3f
        };

        slider.Minimum = 10f;

        Assert.Equal(10f, slider.Minimum);
        Assert.Equal(10f, slider.Maximum);
        Assert.Equal(10f, slider.Value);
    }

    [Fact]
    public void ScrollBar_ViewportIncrease_CoercesValueToScrollableRange()
    {
        var scrollBar = new ScrollBar
        {
            Minimum = 0f,
            Maximum = 100f,
            ViewportSize = 40f,
            Value = 50f
        };

        scrollBar.ViewportSize = 80f;

        Assert.Equal(20f, scrollBar.Value, 3);
    }

    [Fact]
    public void ProgressBar_MaximumBelowMinimum_CoercesMaximumAndValue()
    {
        var progressBar = new ProgressBar
        {
            Minimum = 40f,
            Value = 60f
        };

        progressBar.Maximum = 20f;

        Assert.Equal(40f, progressBar.Minimum);
        Assert.Equal(40f, progressBar.Maximum);
        Assert.Equal(40f, progressBar.Value);
    }
}