using System;
using Microsoft.Xna.Framework;
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
}
