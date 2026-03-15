using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ScrollViewerTemplateScrollBarTests
{
    [Fact]
    public void InternalVerticalScrollBarThumbDrag_UpdatesOffset()
    {
        var (uiRoot, viewer) = BuildViewerSurface();
        var verticalBar = GetPrivateScrollBar(viewer, "_verticalBar");
        var thumb = FindNamedVisualChild<Thumb>(verticalBar, "PART_Thumb");
        Assert.NotNull(thumb);

        var start = GetCenter(verticalBar.GetThumbRectForInput());
        var end = new Vector2(start.X, verticalBar.LayoutSlot.Y + verticalBar.LayoutSlot.Height - 18f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        Assert.Same(thumb, FocusManager.GetCapturedPointerElement());

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));

        Assert.True(viewer.VerticalOffset > 40f);
        Assert.Null(FocusManager.GetCapturedPointerElement());
    }

    [Fact]
    public void InternalVerticalScrollBarLineButton_UsesLineScrollAmount()
    {
        var (uiRoot, viewer) = BuildViewerSurface();
        var verticalBar = GetPrivateScrollBar(viewer, "_verticalBar");
        var lineDownButton = FindNamedVisualChild<RepeatButton>(verticalBar, "PART_LineDownButton");
        Assert.NotNull(lineDownButton);

        Click(uiRoot, GetCenter(lineDownButton!.LayoutSlot));

        Assert.True(MathF.Abs(viewer.VerticalOffset - viewer.LineScrollAmount) <= 0.01f);
    }

    [Fact]
    public void InternalVerticalScrollBarTrackClick_UsesViewportLargeChange()
    {
        var (uiRoot, viewer) = BuildViewerSurface();
        var verticalBar = GetPrivateScrollBar(viewer, "_verticalBar");
        var thumb = verticalBar.GetThumbRectForInput();
        var increasePoint = new Vector2(GetCenter(thumb).X, thumb.Y + thumb.Height + 8f);

        Click(uiRoot, increasePoint);

        Assert.True(MathF.Abs(viewer.VerticalOffset - viewer.ViewportHeight) <= 0.5f);
    }

    private static (UiRoot UiRoot, ScrollViewer Viewer) BuildViewerSurface()
    {
        var root = new Canvas
        {
            Width = 320f,
            Height = 240f
        };

        var viewer = new ScrollViewer
        {
            Width = 220f,
            Height = 140f,
            LineScrollAmount = 24f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = CreateTallStackPanel(30)
        };

        root.AddChild(viewer);
        Canvas.SetLeft(viewer, 20f);
        Canvas.SetTop(viewer, 20f);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 240);
        return (uiRoot, viewer);
    }

    private static StackPanel CreateTallStackPanel(int itemCount)
    {
        var panel = new StackPanel();
        for (var i = 0; i < itemCount; i++)
        {
            panel.AddChild(new Border { Height = 22f });
        }

        return panel;
    }

    private static ScrollBar GetPrivateScrollBar(ScrollViewer viewer, string fieldName)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ScrollBar>(field!.GetValue(viewer));
    }

    private static TElement? FindNamedVisualChild<TElement>(UIElement root, string name)
        where TElement : FrameworkElement
    {
        if (root is TElement typed && typed.Name == name)
        {
            return typed;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindNamedVisualChild<TElement>(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
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

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }
}
