using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ShapeVisibilityInvalidationTests
{
    [Fact]
    public void PathShape_VisibilityToggle_DoesNotTriggerRootMeasureInvalidation()
    {
        var root = new Panel();
        var host = new Border
        {
            Width = 60f,
            Height = 60f
        };
        var path = new PathShape
        {
            Width = 20f,
            Height = 20f,
            Data = new PathGeometry("M 0,0 L 10,0 L 10,10")
        };
        host.Child = path;
        root.AddChild(host);

        var uiRoot = new UiRoot(root);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), new Viewport(0, 0, 320, 200));

        var measureBefore = uiRoot.MeasureInvalidationCount;

        path.Visibility = Visibility.Collapsed;
        path.Visibility = Visibility.Visible;
        path.Visibility = Visibility.Collapsed;
        path.Visibility = Visibility.Visible;

        Assert.Equal(measureBefore, uiRoot.MeasureInvalidationCount);
    }
}
