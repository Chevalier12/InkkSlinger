using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class PointerHitUnderlapResolutionTests
{
    private const int ViewportWidth = 420;
    private const int ViewportHeight = 260;

    [Fact]
    public void PassiveOverlap_BlocksUnderlyingThumbForHoverAndPress()
    {
        FocusManager.ReleasePointer(FocusManager.GetCapturedPointerElement());

        var root = new Canvas
        {
            Width = ViewportWidth,
            Height = ViewportHeight
        };

        var focusCard = new Border
        {
            Width = 180f,
            Height = 120f,
            Child = new Grid()
        };
        Canvas.SetLeft(focusCard, 60f);
        Canvas.SetTop(focusCard, 50f);
        Panel.SetZIndex(focusCard, 2);

        var thumb = new Thumb
        {
            Width = 72f,
            Height = 18f
        };
        Canvas.SetLeft(thumb, 168f);
        Canvas.SetTop(thumb, 74f);
        Panel.SetZIndex(thumb, 3);

        var badge = new Border
        {
            Width = 124f,
            Height = 48f,
            Child = new TextBlock
            {
                Text = "Badge"
            }
        };
        Canvas.SetLeft(badge, 200f);
        Canvas.SetTop(badge, 70f);
        Panel.SetZIndex(badge, 4);

        root.AddChild(focusCard);
        root.AddChild(thumb);
        root.AddChild(badge);

        var dragDeltaCount = 0;
        thumb.DragDelta += (_, _) => dragDeltaCount++;

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot);

        var focusPoint = new Vector2(focusCard.LayoutSlot.X + 24f, focusCard.LayoutSlot.Y + 24f);
        var overlapPoint = new Vector2(thumb.LayoutSlot.X + thumb.LayoutSlot.Width - 10f, thumb.LayoutSlot.Y + (thumb.LayoutSlot.Height * 0.5f));
        var dragPoint = overlapPoint + new Vector2(28f, 16f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(focusPoint, focusPoint, pointerMoved: true));
        Assert.NotSame(thumb, uiRoot.GetHoveredElementForDiagnostics());

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(focusPoint, overlapPoint, pointerMoved: true));

        Assert.NotSame(thumb, uiRoot.GetHoveredElementForDiagnostics());
        Assert.False(thumb.IsMouseOver);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(overlapPoint, overlapPoint, pointerMoved: true));

        Assert.False(thumb.IsMouseOver);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(overlapPoint, overlapPoint, leftPressed: true));

        Assert.False(thumb.IsDragging);
        Assert.NotSame(thumb, FocusManager.GetCapturedPointerElement());

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(overlapPoint, dragPoint, pointerMoved: true));
        Assert.Equal(0, dragDeltaCount);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragPoint, dragPoint, leftReleased: true));
        Assert.False(thumb.IsDragging);
    }

    [Fact]
    public void InteractiveOverlap_StillBlocksUnderlyingThumb()
    {
        FocusManager.ReleasePointer(FocusManager.GetCapturedPointerElement());

        var root = new Canvas
        {
            Width = ViewportWidth,
            Height = ViewportHeight
        };

        var focusCard = new Border
        {
            Width = 180f,
            Height = 120f,
            Child = new Grid()
        };
        Canvas.SetLeft(focusCard, 60f);
        Canvas.SetTop(focusCard, 50f);
        Panel.SetZIndex(focusCard, 2);

        var thumb = new Thumb
        {
            Width = 72f,
            Height = 18f
        };
        Canvas.SetLeft(thumb, 168f);
        Canvas.SetTop(thumb, 74f);
        Panel.SetZIndex(thumb, 3);

        var badgeMouseDownCount = 0;
        var badge = new Border
        {
            Width = 124f,
            Height = 48f,
            Child = new TextBlock
            {
                Text = "Badge"
            }
        };
        badge.AddHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, (_, _) => badgeMouseDownCount++);
        Canvas.SetLeft(badge, 200f);
        Canvas.SetTop(badge, 70f);
        Panel.SetZIndex(badge, 4);

        root.AddChild(focusCard);
        root.AddChild(thumb);
        root.AddChild(badge);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot);

        var focusPoint = new Vector2(focusCard.LayoutSlot.X + 24f, focusCard.LayoutSlot.Y + 24f);
        var overlapPoint = new Vector2(thumb.LayoutSlot.X + thumb.LayoutSlot.Width - 10f, thumb.LayoutSlot.Y + (thumb.LayoutSlot.Height * 0.5f));

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(focusPoint, focusPoint, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(focusPoint, overlapPoint, pointerMoved: true));

        Assert.NotSame(thumb, uiRoot.GetHoveredElementForDiagnostics());
        Assert.False(thumb.IsMouseOver);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(overlapPoint, overlapPoint, leftPressed: true));

        Assert.Equal(1, badgeMouseDownCount);
        Assert.False(thumb.IsDragging);
        Assert.NotSame(thumb, FocusManager.GetCapturedPointerElement());
    }

    private static InputDelta CreatePointerDelta(
        Vector2 previousPointer,
        Vector2 currentPointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default(KeyboardState), default(MouseState), previousPointer),
            Current = new InputSnapshot(default(KeyboardState), default(MouseState), currentPointer),
            PressedKeys = Array.Empty<Keys>(),
            ReleasedKeys = Array.Empty<Keys>(),
            TextInput = Array.Empty<char>(),
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

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, ViewportWidth, ViewportHeight));
    }
}