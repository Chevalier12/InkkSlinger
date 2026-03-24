using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class InkCanvasInputTests
{
    [Fact]
    public void PointerDown_StartsStroke()
    {
        var canvas = CreateInkCanvas();
        var uiRoot = CreateUiRoot(canvas);

        var pointer = new Vector2(100f, 100f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));

        Assert.True(canvas.IsDrawing);
        Assert.Equal(1, canvas.Strokes!.Count);
    }

    [Fact]
    public void PointerMove_AppendsPoints()
    {
        var canvas = CreateInkCanvas();
        var uiRoot = CreateUiRoot(canvas);

        var startPoint = new Vector2(50f, 50f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(startPoint, leftPressed: true));

        var movePoint = new Vector2(100f, 100f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(movePoint, pointerMoved: true));

        var stroke = canvas.Strokes![0];
        Assert.Equal(2, stroke.PointCount);
    }

    [Fact]
    public void PointerUp_FinalizesStroke()
    {
        var canvas = CreateInkCanvas();
        var uiRoot = CreateUiRoot(canvas);

        var startPoint = new Vector2(50f, 50f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(startPoint, leftPressed: true));

        var movePoint = new Vector2(100f, 100f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(movePoint, pointerMoved: true));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(movePoint, leftReleased: true));

        Assert.False(canvas.IsDrawing);
        Assert.Equal(1, canvas.Strokes!.Count);

        var stroke = canvas.Strokes[0];
        Assert.Equal(2, stroke.PointCount);
    }

    [Fact]
    public void MultipleStrokes_AreAccumulated()
    {
        var canvas = CreateInkCanvas();
        var uiRoot = CreateUiRoot(canvas);

        // First stroke
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(50f, 50f), leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(100f, 100f), pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(100f, 100f), leftReleased: true));

        // Second stroke
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(200f, 200f), leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(250f, 250f), pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(250f, 250f), leftReleased: true));

        Assert.Equal(2, canvas.Strokes!.Count);
    }

    [Fact]
    public void DisabledCanvas_DoesNotDraw()
    {
        var canvas = CreateInkCanvas();
        canvas.IsEnabled = false;
        var uiRoot = CreateUiRoot(canvas);

        var pointer = new Vector2(100f, 100f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));

        Assert.False(canvas.IsDrawing);
        Assert.Equal(0, canvas.Strokes!.Count);
    }

    [Fact]
    public void DrawingAttributes_AreAppliedToStrokes()
    {
        var canvas = CreateInkCanvas();
        canvas.DefaultDrawingAttributes = new InkDrawingAttributes
        {
            Color = Color.Red,
            Width = 10f
        };

        var uiRoot = CreateUiRoot(canvas);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(50f, 50f), leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(50f, 50f), leftReleased: true));

        var stroke = canvas.Strokes![0];
        Assert.Equal(Color.Red, stroke.DrawingAttributes.Color);
        Assert.Equal(10f, stroke.DrawingAttributes.Width);
    }

    [Fact]
    public void StrokePoints_AreInCorrectSequence()
    {
        var canvas = CreateInkCanvas();
        var uiRoot = CreateUiRoot(canvas);

        var p1 = new Vector2(10f, 10f);
        var p2 = new Vector2(20f, 20f);
        var p3 = new Vector2(30f, 30f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(p1, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(p2, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(p3, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(p3, leftReleased: true));

        var stroke = canvas.Strokes![0];
        Assert.Equal(3, stroke.PointCount);
        Assert.Equal(p1, stroke.Points[0]);
        Assert.Equal(p2, stroke.Points[1]);
        Assert.Equal(p3, stroke.Points[2]);
    }

    private static InkCanvas CreateInkCanvas()
    {
        return new InkCanvas
        {
            Width = 400f,
            Height = 300f
        };
    }

    private static UiRoot CreateUiRoot(InkCanvas canvas)
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 340f
        };
        host.AddChild(canvas);
        Canvas.SetLeft(canvas, 20f);
        Canvas.SetTop(canvas, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        return uiRoot;
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(System.TimeSpan.FromMilliseconds(16), System.TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 460, 340));
    }
}
