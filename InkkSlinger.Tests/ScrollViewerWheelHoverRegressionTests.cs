using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ScrollViewerWheelHoverRegressionTests
{
    [Fact]
    public void WheelScroll_NoOffsetChange_DoesNotForceHoverTransition()
    {
        var (root, viewer) = BuildButtonScrollSurface();
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 280, 220, 16);

        var pointer = new Vector2(viewer.LayoutSlot.X + 28f, viewer.LayoutSlot.Y + 26f);
        MovePointer(uiRoot, pointer);

        var beforeButton = FindAncestor<Button>(VisualTreeHelper.HitTest(root, pointer));
        Assert.NotNull(beforeButton);
        Assert.True(beforeButton!.IsMouseOver);

        Wheel(uiRoot, pointer, delta: +120);

        var afterButton = FindAncestor<Button>(VisualTreeHelper.HitTest(root, pointer));
        Assert.Same(beforeButton, afterButton);
        Assert.Equal(0f, viewer.VerticalOffset, 3);
        Assert.True(beforeButton.IsMouseOver);
    }

    [Fact]
    public void ClickThenPointerMove_InScrolledViewer_TransfersHoverToNewButton()
    {
        var (root, viewer) = BuildButtonScrollSurface();
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 280, 220, 16);

        var clickPoint = new Vector2(viewer.LayoutSlot.X + 28f, viewer.LayoutSlot.Y + 26f);
        MovePointer(uiRoot, clickPoint);
        Wheel(uiRoot, clickPoint, delta: -120);

        var clickedButton = FindAncestor<Button>(VisualTreeHelper.HitTest(root, clickPoint));
        Assert.NotNull(clickedButton);

        Click(uiRoot, clickPoint);

        var movePoint = new Vector2(clickPoint.X, clickPoint.Y + 56f);
        var moveTargetButton = FindAncestor<Button>(VisualTreeHelper.HitTest(root, movePoint));
        Assert.NotNull(moveTargetButton);
        Assert.NotSame(clickedButton, moveTargetButton);

        MovePointer(uiRoot, movePoint);

        Assert.True(moveTargetButton!.IsMouseOver);
        Assert.False(clickedButton!.IsMouseOver);
    }

    [Fact]
    public void ClickWithoutPointerMove_AfterWheelSequence_RefreshesButtonHoverState()
    {
        var (root, viewer) = BuildButtonScrollSurface();
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 280, 220, 16);

        var topPoint = new Vector2(viewer.LayoutSlot.X + 28f, viewer.LayoutSlot.Y + 26f);
        MovePointer(uiRoot, topPoint);

        var initialButton = FindAncestor<Button>(VisualTreeHelper.HitTest(root, topPoint));
        Assert.NotNull(initialButton);
        Assert.True(initialButton!.IsMouseOver);

        Wheel(uiRoot, topPoint, delta: +120);

        var lowerPoint = new Vector2(topPoint.X, topPoint.Y + 64f);
        var clickedButton = FindAncestor<Button>(VisualTreeHelper.HitTest(root, lowerPoint));
        Assert.NotNull(clickedButton);
        Assert.NotSame(initialButton, clickedButton);

        Click(uiRoot, lowerPoint);

        Assert.True(clickedButton!.IsMouseOver);
        Assert.False(initialButton.IsMouseOver);
    }

    private static (Panel Root, ScrollViewer Viewer) BuildButtonScrollSurface()
    {
        var root = new Panel();
        var host = new StackPanel
        {
            Orientation = Orientation.Vertical
        };

        for (var i = 0; i < 10; i++)
        {
            host.AddChild(new Button
            {
                Content = $"Button {i}",
                Height = 44f
            });
        }

        var viewer = new ScrollViewer
        {
            Width = 220f,
            Height = 120f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = host
        };

        root.AddChild(viewer);
        return (root, viewer);
    }

    private static void MovePointer(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreateInputDelta(pointer, pointerMoved: true));
    }

    private static void Wheel(UiRoot uiRoot, Vector2 pointer, int delta)
    {
        uiRoot.RunInputDeltaForTests(CreateInputDelta(pointer, wheelDelta: delta));
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreateInputDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreateInputDelta(pointer, leftReleased: true));
    }

    private static InputDelta CreateInputDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        int wheelDelta = 0,
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
            WheelDelta = wheelDelta,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static TElement? FindAncestor<TElement>(UIElement? start)
        where TElement : UIElement
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement typed)
            {
                return typed;
            }
        }

        return null;
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root, Func<TElement, bool>? predicate = null)
        where TElement : UIElement
    {
        if (root is TElement match && (predicate == null || predicate(match)))
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild(child, predicate);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}
