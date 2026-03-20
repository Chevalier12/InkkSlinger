using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogScrollPersistenceTests
{
    [Fact]
    public void SelectingPreviewViaButtonClick_ShouldPreserveLeftCatalogScrollOffset()
    {
        var view = new ControlsCatalogView();
        var uiRoot = new UiRoot(view);
        RunLayout(uiRoot, 1280, 820, 16);

        var viewer = FindFirstVisualChild<ScrollViewer>(view);
        Assert.NotNull(viewer);

        var buttonsHost = Assert.IsType<StackPanel>(view.FindName("ControlButtonsHost"));
        var targetButton = buttonsHost.Children.OfType<Button>().Skip(20).First();

        viewer!.ScrollToVerticalOffset(300f);
        RunLayout(uiRoot, 1280, 820, 32);
        var beforeClickOffset = viewer.VerticalOffset;
        Assert.True(beforeClickOffset > 0f, $"Expected precondition scroll offset > 0, got {beforeClickOffset:0.###}");

        var clickPoint = GetVisibleCenter(targetButton.LayoutSlot, beforeClickOffset);
        Click(uiRoot, clickPoint);
        RunLayout(uiRoot, 1280, 820, 48);

        var afterClickOffset = viewer.VerticalOffset;
        Assert.True(
            MathF.Abs(beforeClickOffset - afterClickOffset) <= 0.01f,
            $"Expected left catalog scroll offset to persist after button click. before={beforeClickOffset:0.###}, after={afterClickOffset:0.###}");
    }

    [Fact]
    public void SelectingPreviewViaButtonInvoke_ShouldPreserveLeftCatalogScrollOffset()
    {
        var view = new ControlsCatalogView();
        var uiRoot = new UiRoot(view);
        RunLayout(uiRoot, 1280, 820, 16);

        var viewer = FindFirstVisualChild<ScrollViewer>(view);
        Assert.NotNull(viewer);

        var buttonsHost = Assert.IsType<StackPanel>(view.FindName("ControlButtonsHost"));
        var targetButton = buttonsHost.Children.OfType<Button>().Skip(20).First();

        viewer!.ScrollToVerticalOffset(300f);
        RunLayout(uiRoot, 1280, 820, 32);
        var beforeInvokeOffset = viewer.VerticalOffset;
        Assert.True(beforeInvokeOffset > 0f, $"Expected precondition scroll offset > 0, got {beforeInvokeOffset:0.###}");

        targetButton.InvokeFromInput();
        RunLayout(uiRoot, 1280, 820, 48);

        var afterInvokeOffset = viewer.VerticalOffset;
        Assert.True(
            MathF.Abs(beforeInvokeOffset - afterInvokeOffset) <= 0.01f,
            $"Expected left catalog scroll offset to persist after direct invoke. before={beforeInvokeOffset:0.###}, after={afterInvokeOffset:0.###}");
    }

    [Fact]
    public void SidebarWheelScroll_ShouldMoveCatalogScrollViewer()
    {
        var view = new ControlsCatalogView();
        var uiRoot = new UiRoot(view);
        RunLayout(uiRoot, 1280, 820, 16);

        var viewer = FindFirstVisualChild<ScrollViewer>(view);
        Assert.NotNull(viewer);
        var verticalBar = GetPrivateScrollBar(viewer!, "_verticalBar");
        var beforeThumb = verticalBar.GetThumbRectForInput();

        var pointer = new Vector2(viewer!.LayoutSlot.X + 24f, viewer.LayoutSlot.Y + 48f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, wheelDelta: -120));
        RunLayout(uiRoot, 1280, 820, 32);

        var afterThumb = verticalBar.GetThumbRectForInput();

        Assert.True(viewer.VerticalOffset > 0f, $"Expected catalog sidebar wheel scroll to change offset, got {viewer.VerticalOffset:0.###}.");
        Assert.True(afterThumb.Y > beforeThumb.Y + 0.01f, $"Expected catalog sidebar thumb to move after wheel scroll. before={beforeThumb}, after={afterThumb}");
    }

    private static Vector2 GetVisibleCenter(LayoutRect slot, float verticalOffset)
    {
        var visibleY = slot.Y - verticalOffset;
        return new Vector2(slot.X + (slot.Width * 0.5f), visibleY + (slot.Height * 0.5f));
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static InputDelta CreatePointerDelta(
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
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = wheelDelta,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root)
        where TElement : UIElement
    {
        if (root is TElement match)
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild<TElement>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static ScrollBar GetPrivateScrollBar(ScrollViewer viewer, string fieldName)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ScrollBar>(field!.GetValue(viewer));
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}
