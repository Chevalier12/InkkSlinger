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
}
