using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ThumbInputTests
{
    [Fact]
    public void PointerDrag_RaisesDragEvents_AndReleasesPointerCapture()
    {
        var host = new Canvas
        {
            Width = 160f,
            Height = 120f
        };
        var thumb = new Thumb
        {
            Width = 40f,
            Height = 20f
        };
        host.AddChild(thumb);
        Canvas.SetLeft(thumb, 24f);
        Canvas.SetTop(thumb, 32f);

        DragStartedEventArgs? started = null;
        var deltas = new List<DragDeltaEventArgs>();
        DragCompletedEventArgs? completed = null;
        thumb.DragStarted += (_, args) => started = args;
        thumb.DragDelta += (_, args) => deltas.Add(args);
        thumb.DragCompleted += (_, args) => completed = args;

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 160, 120);

        var start = GetCenter(thumb.LayoutSlot);
        var end = new Vector2(start.X + 24f, start.Y + 12f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        Assert.Same(thumb, FocusManager.GetCapturedPointerElement());
        Assert.True(thumb.IsDragging);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));

        Assert.NotNull(started);
        Assert.NotEmpty(deltas);
        Assert.NotNull(completed);
        Assert.False(completed!.Canceled);
        Assert.Contains(deltas, args =>
            MathF.Abs(args.HorizontalChange - 24f) <= 0.01f &&
            MathF.Abs(args.VerticalChange - 12f) <= 0.01f);
        Assert.True(MathF.Abs(completed.HorizontalChange - 24f) <= 0.01f);
        Assert.True(MathF.Abs(completed.VerticalChange - 12f) <= 0.01f);
        Assert.False(thumb.IsDragging);
        Assert.Null(FocusManager.GetCapturedPointerElement());
    }

    [Fact]
    public void CancelDrag_RaisesCanceledCompletion()
    {
        var host = new Canvas
        {
            Width = 160f,
            Height = 120f
        };
        var thumb = new Thumb
        {
            Width = 40f,
            Height = 20f
        };
        host.AddChild(thumb);
        Canvas.SetLeft(thumb, 24f);
        Canvas.SetTop(thumb, 32f);

        DragCompletedEventArgs? completed = null;
        thumb.DragCompleted += (_, args) => completed = args;

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 160, 120);

        var start = GetCenter(thumb.LayoutSlot);
        Assert.True(thumb.HandlePointerDownFromInput(start));

        thumb.CancelDrag();

        Assert.NotNull(completed);
        Assert.True(completed!.Canceled);
        Assert.False(thumb.IsDragging);
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
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
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }
}
