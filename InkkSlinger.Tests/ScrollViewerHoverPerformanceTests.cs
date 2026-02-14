using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class ScrollViewerHoverPerformanceTests
{
    public ScrollViewerHoverPerformanceTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        AnimationManager.Current.ResetForTests();
        InputManager.ResetForTests();
        FocusManager.ResetForTests();
    }

    [Fact]
    public void InputManager_UnchangedPointer_DoesNotDispatchMouseMove()
    {
        var root = new Canvas
        {
            Width = 400f,
            Height = 240f
        };
        var tracker = new TrackingElement(300f, 200f);
        root.AddChild(tracker);
        root.Measure(new Vector2(400f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 400f, 240f));

        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        InputManager.ResetForTests();

        InputManager.UpdateForTesting(root, gameTime, new Vector2(20f, 20f));
        var moveCountAfterFirst = tracker.MouseMoveCount;

        InputManager.UpdateForTesting(root, gameTime, new Vector2(20f, 20f));
        InputManager.UpdateForTesting(root, gameTime, new Vector2(20f, 20f));

        Assert.Equal(moveCountAfterFirst, tracker.MouseMoveCount);
    }

    [Fact]
    public void InputManager_HoverTransition_RaisesEnterAndLeave_WhenPointerCrossesItems()
    {
        var root = new Canvas
        {
            Width = 400f,
            Height = 240f
        };
        var left = new TrackingElement(180f, 200f);
        var right = new TrackingElement(180f, 200f);
        Canvas.SetLeft(right, 200f);
        root.AddChild(left);
        root.AddChild(right);
        root.Measure(new Vector2(400f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 400f, 240f));

        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        InputManager.ResetForTests();

        InputManager.UpdateForTesting(root, gameTime, new Vector2(20f, 20f));
        InputManager.UpdateForTesting(root, gameTime, new Vector2(220f, 20f));

        Assert.Equal(1, left.MouseEnterCount);
        Assert.Equal(1, left.MouseLeaveCount);
        Assert.Equal(1, right.MouseEnterCount);
    }

    [Fact]
    public void InputManager_MovingWithinSameListItem_DoesNotRaiseExtraEnterLeave()
    {
        var (root, _, first, _, _) = BuildTrackingListScenario();
        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        var p1 = ProbePoint(first, 6f, 6f);
        var p2 = new Vector2(p1.X + 1f, p1.Y);

        InputManager.ResetForTests();
        InputManager.UpdateForTesting(root, gameTime, p1);
        InputManager.UpdateForTesting(root, gameTime, p2);
        InputManager.UpdateForTesting(root, gameTime, p1);

        Assert.Equal(1, first.MouseEnterCount);
        Assert.Equal(0, first.MouseLeaveCount);
    }

    [Fact]
    public void InputManager_MovingAcrossAdjacentListItems_RaisesSingleLeaveAndEnter()
    {
        var (root, _, first, second, _) = BuildTrackingListScenario();
        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        var p1 = ProbePoint(first, 6f, 6f);
        var p2 = ProbePoint(second, 6f, 6f);

        InputManager.ResetForTests();
        InputManager.UpdateForTesting(root, gameTime, p1);
        InputManager.UpdateForTesting(root, gameTime, p2);

        Assert.Equal(1, first.MouseEnterCount);
        Assert.Equal(1, first.MouseLeaveCount);
        Assert.Equal(1, second.MouseEnterCount);
        Assert.Equal(0, second.MouseLeaveCount);
    }

    [Fact]
    public void InputManager_MovingWithinSameListItem_StillDispatchesMouseMove()
    {
        var (root, _, first, _, _) = BuildTrackingListScenario();
        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        var p1 = ProbePoint(first, 6f, 6f);
        var p2 = new Vector2(p1.X + 1f, p1.Y);
        var p3 = new Vector2(p2.X + 1f, p2.Y);

        InputManager.ResetForTests();
        InputManager.UpdateForTesting(root, gameTime, p1);
        var initialMoveCount = first.MouseMoveCount;

        InputManager.UpdateForTesting(root, gameTime, p2);
        InputManager.UpdateForTesting(root, gameTime, p3);

        Assert.Equal(initialMoveCount + 2, first.MouseMoveCount);
    }

    [Fact]
    public void InputManager_NonZeroScrollOffset_ReusesWithinSameVisibleItem()
    {
        var (root, viewer, first, _, _) = BuildTrackingListScenario();
        viewer.ScrollToVerticalOffset(4f);

        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        var basePoint = ProbePoint(first, 6f, 6f);
        var p1 = new Vector2(basePoint.X, basePoint.Y - viewer.VerticalOffset);
        var p2 = new Vector2(p1.X + 1f, p1.Y);

        InputManager.ResetForTests();
        InputManager.UpdateForTesting(root, gameTime, p1);
        InputManager.UpdateForTesting(root, gameTime, p2);

        var stats = InputManager.GetHoverReuseStatsForTests();
        Assert.True(stats.Attempts >= 2);
        Assert.True(stats.Successes >= 1);
    }

    [Fact]
    public void InputManager_LabelsInScrollViewer_SuccessfullyReusesHoverHit()
    {
        var root = new Panel
        {
            Width = 400f,
            Height = 240f
        };

        var scrollViewer = new ScrollViewer
        {
            Width = 300f,
            Height = 180f
        };

        var stackPanel = new StackPanel();
        var label1 = new Label { Text = "Label 1", Height = 30f };
        var label2 = new Label { Text = "Label 2", Height = 30f };
        var label3 = new Label { Text = "Label 3", Height = 30f };
        stackPanel.AddChild(label1);
        stackPanel.AddChild(label2);
        stackPanel.AddChild(label3);

        scrollViewer.Content = stackPanel;
        root.AddChild(scrollViewer);

        root.Measure(new Vector2(400f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 400f, 240f));

        var gameTime = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        var p1 = ProbePoint(label1, 6f, 6f);
        var p2 = new Vector2(p1.X + 1f, p1.Y);

        InputManager.ResetForTests();
        InputManager.UpdateForTesting(root, gameTime, p1);
        InputManager.UpdateForTesting(root, gameTime, p2);

        var stats = InputManager.GetHoverReuseStatsForTests();
        Assert.True(stats.Attempts >= 2);
        Assert.True(stats.Successes >= 1);
    }

    private static (Panel Root, ScrollViewer Viewer, TrackingListBoxItem First, TrackingListBoxItem Second, TrackingListBoxItem Third)
        BuildTrackingListScenario()
    {
        var root = new Panel
        {
            Width = 400f,
            Height = 240f
        };

        var viewer = new ScrollViewer
        {
            Width = 300f,
            Height = 180f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var listBox = new ListBox
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var first = new TrackingListBoxItem { Content = new Label { Text = "Label A" } };
        var second = new TrackingListBoxItem { Content = new Label { Text = "Label B" } };
        var third = new TrackingListBoxItem { Content = new Label { Text = "Label C" } };
        listBox.Items.Add(first);
        listBox.Items.Add(second);
        listBox.Items.Add(third);

        viewer.Content = listBox;
        root.AddChild(viewer);

        root.Measure(new Vector2(400f, 240f));
        root.Arrange(new LayoutRect(0f, 0f, 400f, 240f));
        return (root, viewer, first, second, third);
    }

    private static Vector2 ProbePoint(FrameworkElement element, float dx, float dy)
    {
        var slot = element.LayoutSlot;
        var x = MathF.Max(slot.X + 1f, MathF.Min(slot.X + slot.Width - 1f, slot.X + dx));
        var y = MathF.Max(slot.Y + 1f, MathF.Min(slot.Y + slot.Height - 1f, slot.Y + dy));
        return new Vector2(x, y);
    }

    private sealed class TrackingElement : FrameworkElement
    {
        private readonly Vector2 _size;

        public TrackingElement(float width, float height)
        {
            _size = new Vector2(width, height);
            Width = width;
            Height = height;
        }

        public int MouseMoveCount { get; private set; }

        public int MouseEnterCount { get; private set; }

        public int MouseLeaveCount { get; private set; }

        protected override void OnMouseMove(RoutedMouseEventArgs args)
        {
            base.OnMouseMove(args);
            MouseMoveCount++;
        }

        protected override void OnMouseEnter(RoutedMouseEventArgs args)
        {
            base.OnMouseEnter(args);
            MouseEnterCount++;
        }

        protected override void OnMouseLeave(RoutedMouseEventArgs args)
        {
            base.OnMouseLeave(args);
            MouseLeaveCount++;
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return _size;
        }
    }

    private sealed class TrackingListBoxItem : ListBoxItem
    {
        public int MouseMoveCount { get; private set; }

        public int MouseEnterCount { get; private set; }

        public int MouseLeaveCount { get; private set; }

        protected override void OnMouseMove(RoutedMouseEventArgs args)
        {
            base.OnMouseMove(args);
            MouseMoveCount++;
        }

        protected override void OnMouseEnter(RoutedMouseEventArgs args)
        {
            base.OnMouseEnter(args);
            MouseEnterCount++;
        }

        protected override void OnMouseLeave(RoutedMouseEventArgs args)
        {
            base.OnMouseLeave(args);
            MouseLeaveCount++;
        }
    }
}
