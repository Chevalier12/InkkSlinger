using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class CanvasPanelAttachedPositionTests
{
    [Fact]
    public void Canvas_RightAndBottom_ShouldArrangeChildFromFarEdges()
    {
        var child = new Border
        {
            Width = 120f,
            Height = 40f
        };

        var canvas = new Canvas
        {
            Width = 500f,
            Height = 300f
        };

        canvas.AddChild(child);
        Canvas.SetRight(child, 30f);
        Canvas.SetBottom(child, 20f);

        var uiRoot = new UiRoot(canvas);
        RunLayout(uiRoot, 500, 300);

        Assert.Equal(350f, child.LayoutSlot.X, 0.5f);
        Assert.Equal(240f, child.LayoutSlot.Y, 0.5f);
    }

    [Fact]
    public void Canvas_LeftAndTop_ShouldTakePrecedenceOverRightAndBottom()
    {
        var child = new Border
        {
            Width = 120f,
            Height = 40f
        };

        var canvas = new Canvas
        {
            Width = 500f,
            Height = 300f
        };

        canvas.AddChild(child);
        Canvas.SetLeft(child, 16f);
        Canvas.SetTop(child, 24f);
        Canvas.SetRight(child, 30f);
        Canvas.SetBottom(child, 20f);

        var uiRoot = new UiRoot(canvas);
        RunLayout(uiRoot, 500, 300);

        Assert.Equal(16f, child.LayoutSlot.X, 0.5f);
        Assert.Equal(24f, child.LayoutSlot.Y, 0.5f);
    }

    [Fact]
    public void Canvas_RightAndBottom_ShouldContributeToDesiredSizeWhenPrimaryEdgesAreUnset()
    {
        var child = new Border
        {
            Width = 120f,
            Height = 40f
        };

        var canvas = new Canvas();
        canvas.AddChild(child);
        Canvas.SetRight(child, 30f);
        Canvas.SetBottom(child, 20f);

        canvas.Measure(new Vector2(800f, 600f));

        Assert.Equal(150f, canvas.DesiredSize.X, 0.5f);
        Assert.Equal(60f, canvas.DesiredSize.Y, 0.5f);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }
}