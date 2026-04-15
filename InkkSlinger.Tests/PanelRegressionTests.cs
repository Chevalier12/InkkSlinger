using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class PanelRegressionTests
{
    [Fact]
    public void AddChild_WhenChildAlreadyOwnedByAnotherPanel_ThrowsAndLeavesParentsUnchanged()
    {
        var firstParent = new Panel();
        var secondParent = new Panel();
        var child = new Border();

        firstParent.AddChild(child);

        var exception = Assert.Throws<InvalidOperationException>(() => secondParent.AddChild(child));

        Assert.Equal("UIElement already has a parent.", exception.Message);
        Assert.Single(firstParent.Children);
        Assert.Empty(secondParent.Children);
        Assert.Same(firstParent, child.VisualParent);
        Assert.Same(firstParent, child.LogicalParent);
    }

    [Fact]
    public void MoveChildRange_MovingFirstChildForwardByOne_ReordersChildren()
    {
        var panel = CreateNamedPanel("A", "B", "C");

        Assert.True(panel.MoveChildRange(0, 1, 1));

        AssertPanelOrder(panel, "B", "A", "C");
    }

    [Fact]
    public void MoveChildRange_MovingMiddleChildForwardByOne_ReordersChildren()
    {
        var panel = CreateNamedPanel("A", "B", "C");

        Assert.True(panel.MoveChildRange(1, 1, 2));

        AssertPanelOrder(panel, "A", "C", "B");
    }

    [Fact]
    public void MoveChildRange_CanAppendChildToEnd()
    {
        var panel = CreateNamedPanel("A", "B", "C");

        Assert.True(panel.MoveChildRange(0, 1, panel.Children.Count));

        AssertPanelOrder(panel, "B", "C", "A");
    }

    [Fact]
    public void MoveChildRange_MovesMultipleChildrenAsSingleRange()
    {
        var panel = CreateNamedPanel("A", "B", "C", "D", "E");

        Assert.True(panel.MoveChildRange(1, 2, panel.Children.Count));

        AssertPanelOrder(panel, "A", "D", "E", "B", "C");
    }

    [Fact]
    public void MoveChildRange_InvalidatesMeasureForOrderSensitiveDockPanelLayout()
    {
        var panel = new DockPanel();
        var left = new Border
        {
            Width = 80f,
            Height = 10f
        };
        var top = new Border
        {
            Width = 20f,
            Height = 40f
        };

        DockPanel.SetDock(left, Dock.Left);
        DockPanel.SetDock(top, Dock.Top);
        panel.AddChild(left);
        panel.AddChild(top);

        var availableSize = new Vector2(100f, 100f);
        panel.Measure(availableSize);
        Assert.Equal(new Vector2(100f, 40f), panel.DesiredSize);

        Assert.True(panel.MoveChildRange(0, 1, 1));
        Assert.True(panel.NeedsMeasure);

        panel.Measure(availableSize);
        Assert.Equal(new Vector2(80f, 50f), panel.DesiredSize);
    }

    [Fact]
    public void DockPanel_TopDockedContentDrivenHeader_DoesNotConsumeRemainingHeight()
    {
        var panel = new DockPanel();

        var top = new Border
        {
            Padding = new Thickness(16f, 12f, 16f, 12f),
            Margin = new Thickness(0f, 0f, 0f, 10f),
            Child = BuildDockHeaderContent()
        };
        DockPanel.SetDock(top, Dock.Top);

        var bottom = new Border
        {
            Height = 38f,
            Margin = new Thickness(0f, 10f, 0f, 0f),
            Child = new TextBlock { Text = "Status rail" }
        };
        DockPanel.SetDock(bottom, Dock.Bottom);

        var left = new Border
        {
            Width = 176f,
            Margin = new Thickness(0f, 0f, 10f, 0f),
            Child = new TextBlock { Text = "Navigation" }
        };
        DockPanel.SetDock(left, Dock.Left);

        var right = new Border
        {
            Width = 188f,
            Margin = new Thickness(10f, 0f, 0f, 0f),
            Child = new TextBlock { Text = "Inspector" }
        };
        DockPanel.SetDock(right, Dock.Right);

        var center = new Border
        {
            Child = new TextBlock { Text = "Workspace" }
        };

        panel.AddChild(top);
        panel.AddChild(bottom);
        panel.AddChild(left);
        panel.AddChild(right);
        panel.AddChild(center);

        var uiRoot = new UiRoot(panel);
        RunLayout(uiRoot, 558, 360);

        Assert.InRange(top.ActualHeight, 40f, 120f);
        Assert.True(bottom.ActualHeight > 0f);
        Assert.True(center.ActualHeight > 120f, $"Center height should remain usable after top docking, but was {center.ActualHeight:0.###}.");
        Assert.True(center.ActualWidth > 120f, $"Center width should remain usable after side docking, but was {center.ActualWidth:0.###}.");
    }

    [Fact]
    public void MoveChildRange_RebuildsRetainedOrderAfterSync()
    {
        var root = CreateNamedPanel("A", "B", "C");
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        Assert.True(root.MoveChildRange(0, 1, root.Children.Count));

        uiRoot.SynchronizeRetainedRenderListForTests();

        var order = uiRoot.GetRetainedVisualOrderForTests();
        Assert.Collection(
            order,
            visual => Assert.Same(root, visual),
            visual => Assert.Equal("B", Assert.IsType<Border>(visual).Name),
            visual => Assert.Equal("C", Assert.IsType<Border>(visual).Name),
            visual => Assert.Equal("A", Assert.IsType<Border>(visual).Name));
    }

    [Fact]
    public void SetZIndex_RebuildsRetainedOrderAfterSync()
    {
        var root = CreateNamedPanel("A", "B");
        var first = root.Children[0];
        var second = root.Children[1];
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        Panel.SetZIndex(first, 10);
        Panel.SetZIndex(second, 0);

        uiRoot.SynchronizeRetainedRenderListForTests();

        var order = uiRoot.GetRetainedVisualOrderForTests();
        Assert.Collection(
            order,
            visual => Assert.Same(root, visual),
            visual => Assert.Equal("B", Assert.IsType<Border>(visual).Name),
            visual => Assert.Equal("A", Assert.IsType<Border>(visual).Name));
    }

    [Fact]
    public void DescendantMeasureInvalidation_PopupChildWithStableDesiredSize_DoesNotInvalidatePanelMeasure()
    {
        var root = new Panel();
        root.AddChild(new Border { Width = 240f, Height = 120f });

        var leaf = new FixedMeasureElement(84f, 32f);
        var popup = new Popup
        {
            Width = 180f,
            Height = 120f,
            TitleBarHeight = 0f,
            BorderThickness = 0f,
            Padding = new Thickness(0f),
            Content = leaf
        };
        root.AddChild(popup);

        var availableSize = new Vector2(640f, 360f);
        root.Measure(availableSize);
        var initialPanelMeasureWork = root.MeasureWorkCount;

        leaf.InvalidateMeasure();

        Assert.False(root.NeedsMeasure);

        root.Measure(availableSize);

        Assert.Equal(initialPanelMeasureWork, root.MeasureWorkCount);
        Assert.True(leaf.MeasureWorkCount >= 2);
    }

    [Fact]
    public void DescendantMeasureInvalidation_ExplicitSizeDecorator_DoesNotInvalidateAncestorGridMeasure()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100f, GridUnitType.Pixel) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100f, GridUnitType.Pixel) });

        var header = new Border { Width = 180f, Height = 80f };
        root.AddChild(header);

        var leaf = new FixedMeasureElement(60f, 24f);
        var wrapper = new Border
        {
            Width = 220f,
            Height = 100f,
            Child = leaf
        };
        Grid.SetRow(wrapper, 1);
        root.AddChild(wrapper);

        var availableSize = new Vector2(320f, 220f);
        root.Measure(availableSize);
        var initialGridMeasureWork = root.MeasureWorkCount;

        leaf.InvalidateMeasure();

        Assert.False(root.NeedsMeasure);
        Assert.False(wrapper.NeedsMeasure);

        root.Measure(availableSize);

        Assert.Equal(initialGridMeasureWork, root.MeasureWorkCount);
        Assert.True(leaf.MeasureWorkCount >= 2);
    }

    private static Panel CreateNamedPanel(params string[] names)
    {
        var panel = new Panel();
        for (var i = 0; i < names.Length; i++)
        {
            panel.AddChild(new Border { Name = names[i] });
        }

        return panel;
    }

    private static void AssertPanelOrder(Panel panel, params string[] expectedNames)
    {
        Assert.Equal(expectedNames.Length, panel.Children.Count);
        for (var i = 0; i < expectedNames.Length; i++)
        {
            Assert.Equal(expectedNames[i], Assert.IsType<Border>(panel.Children[i]).Name);
        }
    }

    private static Grid BuildDockHeaderContent()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textStack = new StackPanel();
        textStack.AddChild(new TextBlock { Text = "Top rail", FontWeight = "SemiBold" });
        textStack.AddChild(new TextBlock { Text = "Header content should size to text instead of consuming the full remaining height." });
        grid.AddChild(textStack);

        var badge = new Border
        {
            Padding = new Thickness(10f, 4f, 10f, 4f),
            Child = new TextBlock { Text = "Primary chrome" }
        };
        Grid.SetColumn(badge, 1);
        grid.AddChild(badge);

        return grid;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs = 16)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private sealed class FixedMeasureElement : FrameworkElement
    {
        private readonly Vector2 _desiredSize;

        public FixedMeasureElement(float width, float height)
        {
            _desiredSize = new Vector2(width, height);
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return _desiredSize;
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            return finalSize;
        }
    }
}
