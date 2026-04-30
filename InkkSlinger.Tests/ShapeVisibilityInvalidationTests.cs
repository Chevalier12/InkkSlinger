using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ShapeVisibilityInvalidationTests
{
    [Theory]
    [InlineData("M 4,10 L 16,10", 200f, 40f, 212f, 40f)]
    [InlineData("M 10,4 L 10,16", 206f, 34f, 206f, 46f)]
    public void PathShape_DegenerateLineGeometry_TransformsIntoArrangedSlot(
        string data,
        float expectedFirstX,
        float expectedFirstY,
        float expectedSecondX,
        float expectedSecondY)
    {
        var path = new PathShape
        {
            Width = 12f,
            Height = 12f,
            Data = new PathGeometry(data),
            Stretch = Stretch.Uniform
        };

        path.Measure(new Vector2(100f, 100f));
        path.Arrange(new LayoutRect(200f, 34f, 12f, 12f));

        var figure = Assert.Single(path.GetTransformedFiguresForTests());
        Assert.Equal(2, figure.Points.Count);
        AssertVector(figure.Points[0], expectedFirstX, expectedFirstY);
        AssertVector(figure.Points[1], expectedSecondX, expectedSecondY);
    }

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

    private static void AssertVector(Vector2 actual, float expectedX, float expectedY, float tolerance = 0.001f)
    {
        Assert.InRange(actual.X, expectedX - tolerance, expectedX + tolerance);
        Assert.InRange(actual.Y, expectedY - tolerance, expectedY + tolerance);
    }
}
