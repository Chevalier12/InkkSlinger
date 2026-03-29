using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class BrushFreezableTests
{
    [Fact]
    public void FrozenSolidColorBrush_ColorMutation_Throws()
    {
        var brush = new SolidColorBrush(new Color(10, 20, 30));
        brush.Freeze();

        Assert.Throws<InvalidOperationException>(() => brush.Color = new Color(1, 2, 3));
    }

    [Fact]
    public void FrozenLinearGradientBrush_StopMutation_Throws()
    {
        var brush = new LinearGradientBrush();
        var stop = new GradientStop(new Color(255, 0, 0), 0f);
        brush.GradientStops.Add(stop);
        brush.Freeze();

        Assert.Throws<InvalidOperationException>(() => stop.Color = new Color(0, 0, 255));
    }

    [Fact]
    public void LinearGradientBrush_Clone_DeepCopiesGradientStops()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Vector2(0f, 0f),
            EndPoint = new Vector2(1f, 0f)
        };
        brush.GradientStops.Add(new GradientStop(new Color(255, 0, 0), 0f));
        brush.GradientStops.Add(new GradientStop(new Color(0, 0, 255), 1f));

        var clone = Assert.IsType<LinearGradientBrush>(brush.Clone());
        clone.GradientStops[0].Color = new Color(0, 255, 0);

        Assert.Equal(new Color(255, 0, 0), brush.GradientStops[0].Color);
        Assert.Equal(new Color(0, 255, 0), clone.GradientStops[0].Color);
    }
}
