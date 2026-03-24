using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class InputDispatchOptimizationTests
{
    [Fact]
    public void PointerMove_ReusesHoveredTarget_WithoutRepeatedHitTests()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 300f, 200f));
        var button = new Button();
        button.SetLayoutSlot(new LayoutRect(20f, 20f, 120f, 40f));
        root.AddChild(button);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(30f, 30f)));
        var first = uiRoot.GetInputMetricsSnapshot();
        Assert.Equal(1, first.HitTestCount);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(31f, 31f)));
        var second = uiRoot.GetInputMetricsSnapshot();
        Assert.InRange(second.HitTestCount, 0, 1);
    }

    [Fact]
    public void MouseWheel_WithHoveredTarget_AvoidsHitTesting()
    {
        var root = new Panel();
        var scrollViewer = new ScrollViewer
        {
            Content = new StackPanel()
        };
        root.AddChild(scrollViewer);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        RunLayout(uiRoot, 400, 300, 16);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(30f, 30f)));
        var move = uiRoot.GetInputMetricsSnapshot();
        Assert.Equal(1, move.HitTestCount);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, wheelDelta: 120, position: new Vector2(30f, 30f)));
        var wheel = uiRoot.GetInputMetricsSnapshot();
        Assert.Equal(0, wheel.HitTestCount);
        Assert.True(wheel.PointerEventCount > 0);
    }

    [Fact]
    public void MouseWheel_ReTargetsScrollViewer_WhenHoverReuseStalesTarget()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48f) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var header = new Border();
        Grid.SetRow(header, 0);
        root.AddChild(header);

        var scrollViewer = new ScrollViewer
        {
            Content = CreateTallStackPanel(120),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            LineScrollAmount = 30f
        };
        Grid.SetRow(scrollViewer, 1);
        root.AddChild(scrollViewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 220, 16);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(30f, 20f)));
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(30f, 120f)));
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);
        Assert.Equal(0f, scrollViewer.VerticalOffset);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, wheelDelta: -120, position: new Vector2(30f, 120f)));
        Assert.True(scrollViewer.VerticalOffset > 0f);
    }

    [Fact]
    public void MouseWheel_ScrollsVirtualizingStackPanelInsideScrollViewer()
    {
        var root = new Panel();
        var virtualizingPanel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true
        };
        for (var i = 0; i < 500; i++)
        {
            virtualizingPanel.AddChild(new Label
            {
                Content = $"Item {i}",
                Margin = new Thickness(0f, 0f, 0f, 6f)
            });
        }

        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            LineScrollAmount = 24f,
            Content = virtualizingPanel
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        RunLayout(uiRoot, 360, 260, 16);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(40f, 40f)));
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);
        Assert.Equal(0f, viewer.VerticalOffset);

        for (var i = 0; i < 20; i++)
        {
            uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, wheelDelta: -120, position: new Vector2(40f, 40f)));
        }
        RunLayout(uiRoot, 360, 260, 32);

        Assert.True(viewer.VerticalOffset >= 20f);
        Assert.True(virtualizingPanel.FirstRealizedIndex > 0);
    }

    [Fact]
    public void MouseWheel_ReTargetsFromListBoxToVirtualizingScrollViewer_InTwoPaneLayout()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        var leftVirtualizingPanel = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true
        };
        for (var i = 0; i < 500; i++)
        {
            leftVirtualizingPanel.AddChild(new Label
            {
                Content = $"Virtualized {i}",
                Margin = new Thickness(0f, 0f, 0f, 6f)
            });
        }

        var leftViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            LineScrollAmount = 24f,
            Content = leftVirtualizingPanel
        };
        Grid.SetColumn(leftViewer, 0);
        root.AddChild(leftViewer);

        var rightListBox = new ListBox();
        for (var i = 0; i < 200; i++)
        {
            rightListBox.Items.Add($"Right {i}");
        }

        Grid.SetColumn(rightListBox, 1);
        root.AddChild(rightListBox);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        RunLayout(uiRoot, 900, 500, 16);
        Assert.True(leftViewer.ExtentHeight > leftViewer.ViewportHeight);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(700f, 120f)));
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(120f, 120f)));
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);
        Assert.Equal(0f, leftViewer.VerticalOffset);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, wheelDelta: -120, position: new Vector2(120f, 120f)));
        var wheelMetrics = uiRoot.GetInputMetricsSnapshot();
        Assert.InRange(wheelMetrics.HitTestCount, 0, 1);
        Assert.True(leftViewer.VerticalOffset > 0f);
        RunLayout(uiRoot, 900, 500, 32);

        Assert.True(leftViewer.VerticalOffset > 0f);
    }

    [Fact]
    public void MouseWheel_ReTargetsScrollViewer_WithTransformDefault_AfterHoverReuse()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48f) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var header = new Border();
        Grid.SetRow(header, 0);
        root.AddChild(header);

        var content = CreateTallStackPanel(140);
        var scrollViewer = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            LineScrollAmount = 30f
        };
        Grid.SetRow(scrollViewer, 1);
        root.AddChild(scrollViewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 220, 16);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(30f, 20f)));
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(30f, 120f)));
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);
        Assert.Equal(0f, scrollViewer.VerticalOffset);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, wheelDelta: -120, position: new Vector2(30f, 120f)));
        Assert.True(scrollViewer.VerticalOffset > 0f);
        Assert.True(content.HasLocalRenderTransform());
    }

    [Fact]
    public void PointerClick_UsesPreciseHitTest_AfterHoverReuse()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 200f));
        var left = new Button();
        left.SetLayoutSlot(new LayoutRect(20f, 20f, 120f, 40f));
        var right = new Button();
        right.SetLayoutSlot(new LayoutRect(220f, 20f, 120f, 40f));
        root.AddChild(left);
        root.AddChild(right);

        var leftClicks = 0;
        var rightClicks = 0;
        left.Click += (_, _) => leftClicks++;
        right.Click += (_, _) => rightClicks++;

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(30f, 30f)));
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        // Move to the right button: pointer retarget should do one hit-test.
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(230f, 30f)));
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        // Click transition must force precise targeting.
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: new Vector2(230f, 30f), leftPressed: true));
        Assert.InRange(uiRoot.GetInputMetricsSnapshot().HitTestCount, 0, 1);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: new Vector2(230f, 30f), leftReleased: true));
        Assert.Equal(0, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        Assert.Equal(0, leftClicks);
        Assert.Equal(1, rightClicks);
    }

    [Fact]
    public void PointerMove_BetweenButtonsInSharedHost_UsesHoveredHostSubtreeHitTest()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 500f, 240f));

        var host = new UniformGrid
        {
            Rows = 1,
            Columns = 3
        };
        host.SetLayoutSlot(new LayoutRect(40f, 40f, 300f, 80f));

        var first = new Button { Content = "One" };
        var second = new Button { Content = "Two" };
        var third = new Button { Content = "Three" };
        host.AddChild(first);
        host.AddChild(second);
        host.AddChild(third);
        root.AddChild(host);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 500, 240, 16);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: GetCenter(first.LayoutSlot)));
        Assert.Equal("HitTest", uiRoot.LastPointerResolvePathForDiagnostics);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: GetCenter(second.LayoutSlot)));

        Assert.Equal("HoveredHostSubtreeHitTest", uiRoot.LastPointerResolvePathForDiagnostics);
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);
    }

    [Fact]
    public void ListHover_ManyItems_StaysLowHitTestRate()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 500f, 600f));
        var listBox = new ListBox();
        listBox.SetLayoutSlot(new LayoutRect(10f, 10f, 320f, 500f));
        for (var i = 0; i < 1000; i++)
        {
            listBox.Items.Add($"Item {i}");
        }

        root.AddChild(listBox);
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(40f, 40f)));
        Assert.Equal(1, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        for (var i = 0; i < 25; i++)
        {
            uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(40f, 40f + i)));
            Assert.Equal(0, uiRoot.GetInputMetricsSnapshot().HitTestCount);
        }
    }

    [Fact]
    public void StationaryNoInput_AfterHover_UsesNoInputBypass()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 300f, 200f));
        var button = new Button();
        button.SetLayoutSlot(new LayoutRect(20f, 20f, 120f, 40f));
        root.AddChild(button);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(30f, 30f)));
        Assert.Equal("HitTest", uiRoot.LastPointerResolvePathForDiagnostics);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: new Vector2(30f, 30f)));

        Assert.Equal("NoInputBypass", uiRoot.LastPointerResolvePathForDiagnostics);
        Assert.Equal(0, uiRoot.GetInputMetricsSnapshot().HitTestCount);
    }

    [Fact]
    public void PointerClick_RenderOnlyOpacityInvalidation_PreservesPointerResolveCache()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 300f, 200f));
        var button = new Button();
        button.SetLayoutSlot(new LayoutRect(20f, 20f, 120f, 40f));
        root.AddChild(button);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        var pointerPosition = new Vector2(30f, 30f);
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: pointerPosition));
        Assert.Equal("HitTest", uiRoot.LastPointerResolvePathForDiagnostics);

        button.Opacity = 0.5f;

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: pointerPosition, leftPressed: true));

        Assert.Equal("PointerResolveCacheReuse", uiRoot.LastPointerResolvePathForDiagnostics);
        Assert.Equal(0, uiRoot.GetInputMetricsSnapshot().HitTestCount);
    }

    [Fact]
    public void PointerClick_RenderTransformInvalidation_DoesNotReuseStalePointerCache()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 200f));
        var button = new Button();
        button.SetLayoutSlot(new LayoutRect(20f, 20f, 120f, 40f));
        root.AddChild(button);

        var clickCount = 0;
        button.Click += (_, _) => clickCount++;

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        var stalePointerPosition = new Vector2(30f, 30f);
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: stalePointerPosition));
        Assert.Equal("HitTest", uiRoot.LastPointerResolvePathForDiagnostics);

        button.RenderTransform = new TranslateTransform { X = 180f, Y = 0f };

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: stalePointerPosition, leftPressed: true));
        Assert.NotEqual("PointerResolveCacheReuse", uiRoot.LastPointerResolvePathForDiagnostics);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: stalePointerPosition, leftReleased: true));

        Assert.Equal(0, clickCount);
    }

    [Fact]
    public void ListBox_ClickingDifferentItems_UpdatesSelectedIndex()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 500f, 700f));

        var listBox = new ListBox();
        listBox.SetLayoutSlot(new LayoutRect(20f, 20f, 260f, 620f));
        for (var i = 0; i < 40; i++)
        {
            listBox.Items.Add($"Item {i}");
        }

        root.AddChild(listBox);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        RunLayout(uiRoot, 500, 700, 16);

        var firstIndex = FindFirstVisibleListBoxItemIndex(listBox);
        Assert.True(firstIndex >= 0);
        var secondIndex = firstIndex + 3;
        Assert.True(secondIndex < listBox.Items.Count);

        var firstPosition = GetCenterPointForItem(listBox, firstIndex);
        var secondPosition = GetCenterPointForItem(listBox, secondIndex);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: firstPosition));
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: firstPosition, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: firstPosition, leftReleased: true));
        Assert.Equal(firstIndex, listBox.SelectedIndex);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: secondPosition, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: secondPosition, leftReleased: true));
        Assert.Equal(secondIndex, listBox.SelectedIndex);
    }

    [Fact]
    public void KeyDispatch_BuildsKeyboardMenuScopeOncePerEvent()
    {
        var root = new Panel();
        var menu = new Menu();
        menu.Items.Add(new MenuItem { Header = "_File" });
        root.AddChild(menu);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 400, 240, 16);

        var delta = new InputDelta
        {
            Previous = new InputSnapshot(default(KeyboardState), default(MouseState), Vector2.Zero),
            Current = new InputSnapshot(default(KeyboardState), default(MouseState), Vector2.Zero),
            PressedKeys = new List<Keys> { Keys.F10 },
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };

        uiRoot.RunInputDeltaForTests(delta);

        Assert.Equal(1, uiRoot.GetPerformanceTelemetrySnapshotForTests().MenuScopeBuildCount);
    }

    private static InputDelta CreateDelta(
        bool pointerMoved,
        Vector2 position,
        int wheelDelta = 0,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        var previous = new InputSnapshot(default(KeyboardState), default(MouseState), position);
        var current = new InputSnapshot(default(KeyboardState), default(MouseState), position);
        return new InputDelta
        {
            Previous = previous,
            Current = current,
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

    private static StackPanel CreateTallStackPanel(int itemCount)
    {
        var panel = new StackPanel();
        for (var i = 0; i < itemCount; i++)
        {
            panel.AddChild(new Border { Height = 20f, Margin = new Thickness(0f, 0f, 0f, 2f) });
        }

        return panel;
    }

    private static int FindFirstVisibleListBoxItemIndex(ListBox listBox)
    {
        foreach (var visualChild in listBox.GetVisualChildren())
        {
            if (visualChild is not ScrollViewer scrollViewer)
            {
                continue;
            }

            var hostPanel = FindScrollViewerHostPanel(scrollViewer);
            if (hostPanel == null)
            {
                return -1;
            }

            for (var itemIndex = 0; itemIndex < hostPanel.Children.Count; itemIndex++)
            {
                if (hostPanel.Children[itemIndex] is FrameworkElement child && child.LayoutSlot.Height > 0f)
                {
                    return itemIndex;
                }
            }
        }

        return -1;
    }

    private static Vector2 GetCenterPointForItem(ListBox listBox, int index)
    {
        foreach (var visualChild in listBox.GetVisualChildren())
        {
            if (visualChild is not ScrollViewer scrollViewer)
            {
                continue;
            }

            var hostPanel = FindScrollViewerHostPanel(scrollViewer);
            if (hostPanel == null)
            {
                break;
            }

            if (index < 0 || index >= hostPanel.Children.Count || hostPanel.Children[index] is not FrameworkElement item)
            {
                break;
            }

            var slot = item.LayoutSlot;
            return new Vector2(slot.X + (slot.Width * 0.5f), slot.Y + (slot.Height * 0.5f));
        }

        throw new InvalidOperationException($"Could not resolve click point for ListBox item index {index}.");
    }

    private static Panel? FindScrollViewerHostPanel(ScrollViewer scrollViewer)
    {
        foreach (var child in scrollViewer.GetVisualChildren())
        {
            if (child is Panel panel)
            {
                return panel;
            }
        }

        return null;
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
                new Viewport(0, 0, width, height));
    }

}

