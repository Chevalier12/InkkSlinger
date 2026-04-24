using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TreeViewInputTests
{
    [Fact]
    public void ClickingItemRow_ShouldSelectTreeViewItem()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 280f
        };

        var treeView = new TreeView
        {
            Width = 320f,
            Height = 220f
        };
        var root = new TreeViewItem
        {
            Header = "Root",
            IsExpanded = true
        };
        var child = new TreeViewItem
        {
            Header = "Child"
        };
        root.Items.Add(child);
        treeView.Items.Add(root);

        host.AddChild(treeView);
        Canvas.SetLeft(treeView, 20f);
        Canvas.SetTop(treeView, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var childRowPoint = new Vector2(child.LayoutSlot.X + 24f, child.LayoutSlot.Y + 8f);
        Click(uiRoot, childRowPoint);

        Assert.Same(child, treeView.SelectedItem);
        Assert.True(child.IsSelected);
        Assert.False(root.IsSelected);
    }

    [Fact]
    public void DefaultTreeViewTemplate_ShouldHostItemsInScrollViewer()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 280f
        };

        var treeView = new TreeView
        {
            Width = 180f,
            Height = 80f
        };

        for (var i = 0; i < 12; i++)
        {
            treeView.Items.Add(new TreeViewItem { Header = $"Node {i}" });
        }

        host.AddChild(treeView);
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(treeView.GetVisualChildren()));
        Assert.Same(treeView.GetItemContainersForPresenter()[0], FindDescendant<TreeViewItem>(scrollViewer));
        Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);
        Assert.True(scrollViewer.ExtentHeight > scrollViewer.ViewportHeight);
    }

    [Fact]
    public void MouseWheel_OnOverflowingTreeView_ShouldScrollContainedItems()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 280f
        };

        var treeView = new TreeView
        {
            Width = 180f,
            Height = 80f
        };

        for (var i = 0; i < 12; i++)
        {
            treeView.Items.Add(new TreeViewItem { Header = $"Node {i}" });
        }

        host.AddChild(treeView);
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(treeView.GetVisualChildren()));
        Assert.Equal(0f, scrollViewer.VerticalOffset, 3);

        var pointer = new Vector2(treeView.LayoutSlot.X + 20f, treeView.LayoutSlot.Y + 20f);
        Wheel(uiRoot, pointer, -120);

        Assert.True(scrollViewer.VerticalOffset > 0f);
    }

    [Fact]
    public void ClickingExpanderAfterTreeViewScroll_ShouldToggleVisibleBranch()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 280f
        };

        var treeView = new TreeView
        {
            Width = 220f,
            Height = 90f
        };

        var root = new TreeViewItem
        {
            Header = "Project Root",
            IsExpanded = true
        };

        var src = new TreeViewItem
        {
            Header = "src",
            IsExpanded = true
        };

        for (var i = 0; i < 8; i++)
        {
            src.Items.Add(new TreeViewItem { Header = $"File {i}.cs" });
        }

        var docs = new TreeViewItem
        {
            Header = "docs"
        };
        docs.Items.Add(new TreeViewItem { Header = "architecture.md" });

        root.Items.Add(src);
        root.Items.Add(docs);
        treeView.Items.Add(root);
        host.AddChild(treeView);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(treeView.GetVisualChildren()));
        scrollViewer.ScrollToVerticalOffset(scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);
        RunLayout(uiRoot);

        Assert.False(docs.IsExpanded);
        var visibleDocsExpanderPoint = new Vector2(
            docs.LayoutSlot.X + 8f,
            docs.LayoutSlot.Y - scrollViewer.VerticalOffset + 9f);
        var hit = VisualTreeHelper.HitTest(host, visibleDocsExpanderPoint);
        Assert.True(IsDescendantOrSelf(docs, hit));
        Assert.False(docs.HitExpander(visibleDocsExpanderPoint));

        Click(uiRoot, visibleDocsExpanderPoint);

        Assert.True(docs.IsExpanded);
    }

    [Fact]
    public void ClickingExpanderGlyph_ShouldToggleExpandedState()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 280f
        };

        var treeView = new TreeView
        {
            Width = 320f,
            Height = 220f
        };
        var root = new TreeViewItem
        {
            Header = "Root",
            IsExpanded = true
        };
        root.Items.Add(new TreeViewItem { Header = "Child" });
        treeView.Items.Add(root);

        host.AddChild(treeView);
        Canvas.SetLeft(treeView, 20f);
        Canvas.SetTop(treeView, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        Assert.True(root.IsExpanded);

        var expanderPoint = new Vector2(root.LayoutSlot.X + 8f, root.LayoutSlot.Y + 9f);
        Click(uiRoot, expanderPoint);
        Assert.False(root.IsExpanded);

        RunLayout(uiRoot);
        Click(uiRoot, expanderPoint);
        Assert.True(root.IsExpanded);
    }

    [Fact]
    public void CollapsingExpandedNode_ShouldRemoveDescendantsFromRetainedRenderList()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 280f
        };

        var treeView = new TreeView
        {
            Width = 320f,
            Height = 220f
        };
        var root = new TreeViewItem
        {
            Header = "Root",
            IsExpanded = true
        };
        var child = new TreeViewItem
        {
            Header = "Child"
        };
        root.Items.Add(child);
        treeView.Items.Add(root);

        host.AddChild(treeView);
        Canvas.SetLeft(treeView, 20f);
        Canvas.SetTop(treeView, 20f);

        var uiRoot = new UiRoot(host)
        {
            UseRetainedRenderList = true,
            UseDirtyRegionRendering = true
        };

        RunLayout(uiRoot);
        uiRoot.RebuildRenderListForTests();
        Assert.Contains(child, uiRoot.GetRetainedVisualOrderForTests());

        host.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.IsExpanded = false;
        RunLayout(uiRoot);

        Assert.DoesNotContain(child, uiRoot.GetRetainedVisualOrderForTests());
        Assert.True(uiRoot.IsFullDirtyForTests());
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static void Wheel(UiRoot uiRoot, Vector2 pointer, int delta)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, wheelDelta: delta));
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

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 460, 280));
    }

    private static TElement? FindDescendant<TElement>(UIElement root)
        where TElement : UIElement
    {
        if (root is TElement match)
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindDescendant<TElement>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool IsDescendantOrSelf(UIElement ancestor, UIElement? candidate)
    {
        for (var current = candidate; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void ClickingChildAfterRootReselection_WithStationaryClicks_ShouldSelectChildImmediately()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 280f
        };

        var treeView = new TreeView
        {
            Width = 320f,
            Height = 220f
        };

        var root = new TreeViewItem
        {
            Header = "Root",
            IsExpanded = true
        };
        var childA = new TreeViewItem { Header = "Child A" };
        var childB = new TreeViewItem { Header = "Child B" };
        root.Items.Add(childA);
        root.Items.Add(childB);
        treeView.Items.Add(root);

        host.AddChild(treeView);
        Canvas.SetLeft(treeView, 20f);
        Canvas.SetTop(treeView, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var childAPoint = new Vector2(childA.LayoutSlot.X + 24f, childA.LayoutSlot.Y + 8f);
        var rootPoint = new Vector2(root.LayoutSlot.X + 24f, root.LayoutSlot.Y + 8f);
        var childBPoint = new Vector2(childB.LayoutSlot.X + 24f, childB.LayoutSlot.Y + 8f);

        ClickWithoutPointerMove(uiRoot, childAPoint);
        Assert.Same(childA, treeView.SelectedItem);

        ClickWithoutPointerMove(uiRoot, rootPoint);
        Assert.Same(root, treeView.SelectedItem);

        ClickWithoutPointerMove(uiRoot, childBPoint);
        Assert.Same(childB, treeView.SelectedItem);
    }

    [Fact]
    public void ClickingSiblingAfterCollapsingBranch_WithStationaryClicks_ShouldSelectSiblingImmediately()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 320f
        };

        var treeView = new TreeView
        {
            Width = 340f,
            Height = 260f
        };

        var root = new TreeViewItem
        {
            Header = "Root",
            IsExpanded = true
        };

        var branch = new TreeViewItem
        {
            Header = "Branch",
            IsExpanded = true
        };
        var grandchild = new TreeViewItem { Header = "Grandchild" };
        branch.Items.Add(grandchild);

        var sibling = new TreeViewItem { Header = "Sibling" };
        root.Items.Add(branch);
        root.Items.Add(sibling);
        treeView.Items.Add(root);

        host.AddChild(treeView);
        Canvas.SetLeft(treeView, 20f);
        Canvas.SetTop(treeView, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var grandchildPoint = new Vector2(grandchild.LayoutSlot.X + 24f, grandchild.LayoutSlot.Y + 8f);
        ClickWithoutPointerMove(uiRoot, grandchildPoint);
        Assert.Same(grandchild, treeView.SelectedItem);

        var branchExpanderPoint = new Vector2(branch.LayoutSlot.X + 8f, branch.LayoutSlot.Y + 9f);
        ClickWithoutPointerMove(uiRoot, branchExpanderPoint);
        Assert.False(branch.IsExpanded);

        RunLayout(uiRoot);
        var siblingPoint = new Vector2(sibling.LayoutSlot.X + 24f, sibling.LayoutSlot.Y + 8f);
        ClickWithoutPointerMove(uiRoot, siblingPoint);
        Assert.Same(sibling, treeView.SelectedItem);
    }

    [Fact]
    public void ClickingChildAfterRootClick_WithStaleRootHover_ShouldStillSelectChild()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 320f
        };

        var treeView = new TreeView
        {
            Width = 340f,
            Height = 260f
        };

        var root = new TreeViewItem
        {
            Header = "Root",
            IsExpanded = true
        };
        var child = new TreeViewItem { Header = "Child" };
        root.Items.Add(child);
        treeView.Items.Add(root);

        host.AddChild(treeView);
        Canvas.SetLeft(treeView, 20f);
        Canvas.SetTop(treeView, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var rootPoint = new Vector2(root.LayoutSlot.X + 24f, root.LayoutSlot.Y + 8f);
        var childPoint = new Vector2(child.LayoutSlot.X + 24f, child.LayoutSlot.Y + 8f);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(rootPoint, pointerMoved: true));
        ClickWithoutPointerMove(uiRoot, rootPoint);
        Assert.Same(root, treeView.SelectedItem);

        ClickWithoutPointerMove(uiRoot, childPoint);
        Assert.Same(child, treeView.SelectedItem);
    }

    private static void ClickWithoutPointerMove(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDeltaNoMove(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDeltaNoMove(pointer, leftReleased: true));
    }

    private static InputDelta CreatePointerDeltaNoMove(
        Vector2 pointer,
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
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }
}
