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
        Assert.Equal(0, second.HitTestCount);
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
        Assert.Equal(0, uiRoot.GetInputMetricsSnapshot().HitTestCount);
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
                Text = $"Item {i}",
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
                Text = $"Virtualized {i}",
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
        Assert.Equal(0, uiRoot.GetInputMetricsSnapshot().HitTestCount);
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
        Assert.Equal(0, uiRoot.GetInputMetricsSnapshot().HitTestCount);
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

        // Move to the right button with max-savings hover reuse: no new hit-test.
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: new Vector2(230f, 30f)));
        Assert.Equal(0, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        // Click transition must force precise targeting.
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: new Vector2(230f, 30f), leftPressed: true));
        Assert.InRange(uiRoot.GetInputMetricsSnapshot().HitTestCount, 0, 1);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: new Vector2(230f, 30f), leftReleased: true));
        Assert.Equal(0, uiRoot.GetInputMetricsSnapshot().HitTestCount);

        Assert.Equal(0, leftClicks);
        Assert.Equal(1, rightClicks);
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
    public void ListBox_ClickResolve_UsesSmallSubtreeHitTestAfterFirstClick()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 1200f, 700f));

        var listBox = new ListBox();
        listBox.SetLayoutSlot(new LayoutRect(20f, 20f, 320f, 620f));
        for (var i = 0; i < 600; i++)
        {
            listBox.Items.Add($"Item {i}");
        }

        root.AddChild(listBox);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        RunLayout(uiRoot, 1200, 700, 16);

        var firstPoint = GetCenterPointForItem(listBox, 5);
        var secondPoint = GetCenterPointForItem(listBox, 6);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: firstPoint));
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: firstPoint, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: firstPoint, leftReleased: true));
        Assert.Equal(5, listBox.SelectedIndex);

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: secondPoint, leftPressed: true));
        Assert.True(uiRoot.TryGetLastPointerResolveHitTestMetricsForTests(out var metrics));
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: secondPoint, leftReleased: true));

        Assert.Equal(6, listBox.SelectedIndex);
        Assert.InRange(metrics.NodesVisited, 1, 32);
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

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        const string inputPipelineVariable = "INKKSLINGER_ENABLE_INPUT_PIPELINE";
        var previous = Environment.GetEnvironmentVariable(inputPipelineVariable);
        Environment.SetEnvironmentVariable(inputPipelineVariable, "0");
        try
        {
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
                new Viewport(0, 0, width, height));
        }
        finally
        {
            Environment.SetEnvironmentVariable(inputPipelineVariable, previous);
        }
    }

}
