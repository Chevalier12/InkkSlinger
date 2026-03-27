using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogHoverRegressionTests
{
    [Fact]
    public void HoveringFromViewerGutterIntoButton_ShouldActivateButtonHover()
    {
        var view = new ControlsCatalogView();
        var uiRoot = new UiRoot(view);
        RunLayout(uiRoot, 1280, 820, 16);

        var viewer = FindFirstVisualChild<ScrollViewer>(view);
        Assert.NotNull(viewer);

        var host = Assert.IsType<StackPanel>(view.FindName("ControlButtonsHost"));
        var button = host.Children.OfType<Button>().First();

        var verticalBar = viewer!.GetVisualChildren()
            .OfType<ScrollBar>()
            .FirstOrDefault(static bar => bar.Orientation == Orientation.Vertical && bar.IsVisible);
        Assert.NotNull(verticalBar);

        var gutterPoint = new Vector2(
            verticalBar!.LayoutSlot.X - 0.25f,
            button.LayoutSlot.Y + (button.LayoutSlot.Height * 0.5f));
        MovePointer(uiRoot, gutterPoint);

        Assert.False(button.IsMouseOver);

        var buttonPoint = new Vector2(
            button.LayoutSlot.X + (button.LayoutSlot.Width * 0.5f),
            button.LayoutSlot.Y + (button.LayoutSlot.Height * 0.5f));
        MovePointer(uiRoot, buttonPoint);

        Assert.True(
            button.IsMouseOver,
            $"Expected button hover to recover after moving from viewer gutter. button={button.GetContentText()}, gutter=({gutterPoint.X:0.###},{gutterPoint.Y:0.###}), buttonPoint=({buttonPoint.X:0.###},{buttonPoint.Y:0.###})");
    }

    [Fact]
    public void HoveringScrolledSidebarButton_InvalidatesTemplatedButtonOwner()
    {
        var view = new ControlsCatalogView();
        var uiRoot = new UiRoot(view);
        RunLayout(uiRoot, 1280, 820, 16);

        var viewer = FindFirstVisualChild<ScrollViewer>(view);
        Assert.NotNull(viewer);

        viewer!.ScrollToVerticalOffset(320f);
        RunLayout(uiRoot, 1280, 820, 32);

        var host = Assert.IsType<StackPanel>(view.FindName("ControlButtonsHost"));
        var viewport = GetViewerViewportRect(viewer);

        var verticalBar = viewer.GetVisualChildren()
            .OfType<ScrollBar>()
            .FirstOrDefault(static bar => bar.Orientation == Orientation.Vertical && bar.IsVisible);
        Assert.NotNull(verticalBar);

        var (button, buttonPoint) = FindVisibleSidebarButtonHit(view, host, viewport, verticalBar!.LayoutSlot.X);

        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        view.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        var gutterPoint = new Vector2(
            verticalBar!.LayoutSlot.X - 0.25f,
            button.LayoutSlot.Y + (button.LayoutSlot.Height * 0.5f));
        MovePointer(uiRoot, gutterPoint);

        MovePointer(uiRoot, buttonPoint);
        uiRoot.SynchronizeRetainedRenderListForTests();

        var invalidationSnapshot = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
        var dirtyRootSummary = uiRoot.GetLastSynchronizedDirtyRootSummaryForTests();

        Assert.True(button.IsMouseOver);
        Assert.Equal("ScrollViewer", invalidationSnapshot.EffectiveSourceType);
        Assert.Equal("ScrollViewer", invalidationSnapshot.DirtyBoundsVisualType);
        Assert.Equal("Button", dirtyRootSummary);
    }

    [Fact]
    public void HoveringFromListBoxIntoSidebarButton_ShouldActivateSidebarHover()
    {
        var root = new Canvas
        {
            Width = 1000f,
            Height = 700f
        };

        var sidebarHost = new StackPanel();
        for (var i = 0; i < 18; i++)
        {
            sidebarHost.AddChild(new Button
            {
                Content = $"Control {i}",
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        var sidebarViewer = new ScrollViewer
        {
            Width = 260f,
            Height = 620f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = sidebarHost
        };
        root.AddChild(sidebarViewer);
        Canvas.SetLeft(sidebarViewer, 12f);
        Canvas.SetTop(sidebarViewer, 12f);

        var listBox = new ListBox
        {
            Width = 340f,
            Height = 260f
        };
        listBox.Items.Add("Alpha");
        listBox.Items.Add("Beta");
        listBox.Items.Add("Gamma");
        listBox.Items.Add("Delta");
        root.AddChild(listBox);
        Canvas.SetLeft(listBox, 340f);
        Canvas.SetTop(listBox, 56f);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 1000, 700, 16);

        var previewItemPoint = new Vector2(
            listBox.LayoutSlot.X + MathF.Max(12f, listBox.LayoutSlot.Width * 0.35f),
            listBox.LayoutSlot.Y + MathF.Max(12f, listBox.LayoutSlot.Height * 0.25f));
        var previewHit = VisualTreeHelper.HitTest(root, previewItemPoint);
        Assert.NotNull(FindAncestor<ListBoxItem>(previewHit));
        MovePointer(uiRoot, previewItemPoint);

        var firstSidebarButton = sidebarHost.Children.OfType<Button>().First();
        var sidebarButtonPoint = new Vector2(
            firstSidebarButton.LayoutSlot.X + (firstSidebarButton.LayoutSlot.Width * 0.5f),
            firstSidebarButton.LayoutSlot.Y + (firstSidebarButton.LayoutSlot.Height * 0.5f));
        var preMoveHit = VisualTreeHelper.HitTest(root, sidebarButtonPoint);
        var preMoveButton = FindAncestor<Button>(preMoveHit);
        MovePointer(uiRoot, sidebarButtonPoint);

        Assert.True(
            firstSidebarButton.IsMouseOver,
            $"Expected sidebar hover to activate after leaving ListBox. sidebar={firstSidebarButton.GetContentText()}, listBoxPoint=({previewItemPoint.X:0.###},{previewItemPoint.Y:0.###}), sidebarPoint=({sidebarButtonPoint.X:0.###},{sidebarButtonPoint.Y:0.###}), preMoveHit={preMoveHit?.GetType().Name ?? "null"}, preMoveButton={preMoveButton?.GetContentText() ?? "null"}");
    }

    private static void MovePointer(UiRoot uiRoot, Vector2 point)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, pointerMoved: true));
    }

    private static LayoutRect GetViewerViewportRect(ScrollViewer viewer)
    {
        if (viewer.TryGetContentViewportClipRect(out var viewport))
        {
            return viewport;
        }

        throw new InvalidOperationException("Sidebar ScrollViewer did not expose a viewport.");
    }

    private static bool Intersects(LayoutRect left, LayoutRect right)
    {
        return left.X < right.X + right.Width &&
               right.X < left.X + left.Width &&
               left.Y < right.Y + right.Height &&
               right.Y < left.Y + left.Height;
    }

    private static (Button Button, Vector2 Point) FindVisibleSidebarButtonHit(
        UIElement root,
        StackPanel host,
        LayoutRect viewport,
        float scrollbarLeft)
    {
        var minX = Math.Max(0, (int)MathF.Floor(viewport.X));
        var maxX = Math.Max(minX, (int)MathF.Ceiling(MathF.Min(scrollbarLeft - 1f, viewport.X + viewport.Width)));
        var minY = Math.Max(0, (int)MathF.Floor(viewport.Y));
        var maxY = Math.Max(minY, (int)MathF.Ceiling(viewport.Y + viewport.Height));

        for (var y = minY; y < maxY; y += 2)
        {
            for (var x = minX; x < maxX; x += 2)
            {
                var point = new Vector2(x, y);
                var hit = VisualTreeHelper.HitTest(root, point);
                var button = FindAncestor<Button>(hit);
                if (button != null && host.Children.OfType<Button>().Contains(button))
                {
                    return (button, point);
                }
            }
        }

        throw new InvalidOperationException("Could not locate visible sidebar button hit point.");
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

    private static TElement? FindAncestor<TElement>(UIElement? start)
        where TElement : UIElement
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement match)
            {
                return match;
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
