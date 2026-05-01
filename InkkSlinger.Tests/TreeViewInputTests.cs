using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
            docs.LayoutSlot.X + 24f,
            docs.LayoutSlot.Y + 9f);
        var hit = VisualTreeHelper.HitTest(host, visibleDocsExpanderPoint);
        Assert.True(
            IsDescendantOrSelf(docs, hit),
            $"Expected hit under docs. hit={hit?.GetType().Name ?? "<null>"}, docsSlot={docs.LayoutSlot}, offset={scrollViewer.VerticalOffset:0.###}, point={visibleDocsExpanderPoint}.");
        Assert.True(docs.HitExpander(visibleDocsExpanderPoint));

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

    [Fact(Timeout = 5000)]
    public async Task ClickingCollapsedNestedFolderExpander_ShouldExpandAndRealizeChildren()
    {
        await Task.Yield();

        var host = new Canvas
        {
            Width = 460f,
            Height = 320f
        };

        var treeView = new TreeView
        {
            Width = 320f,
            Height = 260f
        };

        var root = new TreeViewItem
        {
            Header = "[+] InkkSlinger",
            IsExpanded = true
        };
        var claude = new TreeViewItem
        {
            Header = "[+] .claude",
            IsExpanded = true
        };
        var classTelemetryAuthor = new TreeViewItem
        {
            Header = "[+] class-telemetry-author",
            IsExpanded = false
        };
        var skillFile = new TreeViewItem { Header = "    SKILL.md" };
        classTelemetryAuthor.Items.Add(skillFile);
        claude.Items.Add(classTelemetryAuthor);
        claude.Items.Add(new TreeViewItem { Header = "[+] inkkoops-diagnostics-contributor-author" });
        claude.Items.Add(new TreeViewItem { Header = "[+] pre-warm-author" });
        root.Items.Add(claude);
        treeView.Items.Add(root);

        host.AddChild(treeView);
        Canvas.SetLeft(treeView, 20f);
        Canvas.SetTop(treeView, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        Assert.False(classTelemetryAuthor.IsExpanded);
        Assert.Null(skillFile.VisualParent);

        var expanderPoint = new Vector2(classTelemetryAuthor.LayoutSlot.X + 40f, classTelemetryAuthor.LayoutSlot.Y + 9f);
        Assert.True(classTelemetryAuthor.HitExpander(expanderPoint));

        Click(uiRoot, expanderPoint);
        RunLayout(uiRoot);

        Assert.True(classTelemetryAuthor.IsExpanded);
        Assert.True(IsDescendantOrSelf(treeView, skillFile));
        Assert.True(skillFile.LayoutSlot.Height > 0f, $"Expected expanded child to be arranged, but got {skillFile.LayoutSlot}.");

        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(treeView.GetVisualChildren()));
        var hostPanel = Assert.IsAssignableFrom<Panel>(scrollViewer.Content);
        Assert.Collection(
            hostPanel.Children.OfType<TreeViewItem>().Take(5),
            item => Assert.Same(root, item),
            item => Assert.Same(claude, item),
            item => Assert.Same(classTelemetryAuthor, item),
            item => Assert.Same(skillFile, item),
            item => Assert.Equal("[+] inkkoops-diagnostics-contributor-author", item.Header));
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

        var branchExpanderPoint = new Vector2(branch.LayoutSlot.X + 24f, branch.LayoutSlot.Y + 9f);
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

    [Fact]
    public void StaticPointerWheel_WhenHoveredItemScrolledAway_ShouldNotReuseHoveredTarget()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 320f
        };

        var treeView = new TreeView
        {
            Width = 340f,
            Height = 100f
        };

        for (var i = 0; i < 40; i++)
        {
            var item = new TreeViewItem { Header = $"Node {i}" };
            treeView.Items.Add(item);
        }

        host.AddChild(treeView);
        Canvas.SetLeft(treeView, 20f);
        Canvas.SetTop(treeView, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(treeView.GetVisualChildren()));
        Assert.True(scrollViewer.ExtentHeight > scrollViewer.ViewportHeight,
            "TreeView must have scrollable overflow");

        var firstRealizedItem = FindDescendant<TreeViewItem>(scrollViewer);
        Assert.NotNull(firstRealizedItem);
        var pointerPoint = new Vector2(
            firstRealizedItem.LayoutSlot.X + 24f,
            firstRealizedItem.LayoutSlot.Y + 8f);

        var initialDelta = CreatePointerDelta(
            pointerPoint,
            pointerMoved: true,
            wheelDelta: 0);
        uiRoot.RunInputDeltaForTests(initialDelta);
        var initialResolvePath = uiRoot.LastPointerResolvePathForDiagnostics;
        Assert.NotEqual("NoInputBypass", initialResolvePath);
        Assert.NotEqual("None", initialResolvePath);

        RunLayout(uiRoot);

        scrollViewer.ScrollToVerticalOffset(400f);
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(
            pointerPoint,
            pointerMoved: false,
            wheelDelta: -120));
        var afterScrollWheelPath = uiRoot.LastPointerResolvePathForDiagnostics;
        Assert.NotEqual("HoverReuse", afterScrollWheelPath);
    }

    [Fact]
    public void ClickingNestedTreeViewItemAfterScroll_ShouldSelectClickedVisibleRow()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 320f
        };

        var treeView = new TreeView
        {
            Width = 340f,
            Height = 120f
        };

        var root = new TreeViewItem
        {
            Header = "InkkSlinger",
            IsExpanded = true
        };

        for (var i = 0; i < 18; i++)
        {
            root.Items.Add(new TreeViewItem { Header = $"Before {i:00}" });
        }

        var git = new TreeViewItem
        {
            Header = ".git",
            IsExpanded = true
        };
        var hooks = new TreeViewItem
        {
            Header = "hooks",
            IsExpanded = true
        };
        var preCommit = new TreeViewItem { Header = "prepare-commit-msg.sample" };
        var preMerge = new TreeViewItem { Header = "pre-merge-commit.sample" };
        var prePush = new TreeViewItem { Header = "pre-push.sample" };
        hooks.Items.Add(preCommit);
        hooks.Items.Add(preMerge);
        hooks.Items.Add(prePush);
        git.Items.Add(hooks);
        root.Items.Add(git);

        for (var i = 0; i < 18; i++)
        {
            root.Items.Add(new TreeViewItem { Header = $"After {i:00}" });
        }

        treeView.Items.Add(root);
        host.AddChild(treeView);
        Canvas.SetLeft(treeView, 20f);
        Canvas.SetTop(treeView, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(treeView.GetVisualChildren()));
        scrollViewer.ScrollToVerticalOffset(425f);
        RunLayout(uiRoot);

        var targetPoint = new Vector2(prePush.LayoutSlot.X + 40f, prePush.LayoutSlot.Y + 8f);
        var hit = VisualTreeHelper.HitTest(host, targetPoint);
        Assert.True(
            IsDescendantOrSelf(prePush, hit),
            $"Expected direct hit under pre-push. hit={hit?.GetType().Name ?? "<null>"}, prePushSlot={prePush.LayoutSlot}, point={targetPoint}, offset={scrollViewer.VerticalOffset:0.###}.");

        ClickWithoutPointerMove(uiRoot, targetPoint);

        Assert.Same(prePush, treeView.SelectedItem);
        Assert.True(prePush.IsSelected);
        Assert.False(preCommit.IsSelected);
        Assert.False(preMerge.IsSelected);
        Assert.False(hooks.IsSelected);
    }

    [Fact]
    public void InkkOopsActionPoint_ForScrolledVirtualizedTreeViewItem_ShouldClickVisibleRow()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 320f
        };

        var treeView = new TreeView
        {
            Width = 340f,
            Height = 120f
        };

        var root = new TreeViewItem
        {
            Header = "InkkSlinger",
            IsExpanded = true
        };

        for (var i = 0; i < 18; i++)
        {
            root.Items.Add(new TreeViewItem { Header = $"Before {i:00}" });
        }

        var git = new TreeViewItem
        {
            Header = ".git",
            IsExpanded = true
        };
        var hooks = new TreeViewItem
        {
            Header = "hooks",
            IsExpanded = true
        };
        var preCommit = new TreeViewItem { Header = "prepare-commit-msg.sample" };
        var preMerge = new TreeViewItem { Header = "pre-merge-commit.sample" };
        var prePush = new TreeViewItem { Header = "pre-push.sample" };
        hooks.Items.Add(preCommit);
        hooks.Items.Add(preMerge);
        hooks.Items.Add(prePush);
        git.Items.Add(hooks);
        root.Items.Add(git);

        for (var i = 0; i < 18; i++)
        {
            root.Items.Add(new TreeViewItem { Header = $"After {i:00}" });
        }

        treeView.Items.Add(root);
        host.AddChild(treeView);
        Canvas.SetLeft(treeView, 20f);
        Canvas.SetTop(treeView, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(treeView.GetVisualChildren()));
        scrollViewer.ScrollToVerticalOffset(360f);
        RunLayout(uiRoot);

        Assert.True(prePush.TryGetRenderBoundsInRootSpace(out var bounds));
        var viewport = host.LayoutSlot;
        var actionPoint = InkkOopsCommandUtilities.GetPreferredActionPoint(prePush, bounds, viewport, InkkOopsPointerAnchor.Center);
        var hit = VisualTreeHelper.HitTest(host, actionPoint);
        Assert.True(
            IsDescendantOrSelf(prePush, hit),
            $"Expected InkkOops action point to hit pre-push. hit={hit?.GetType().Name ?? "<null>"}, prePushSlot={prePush.LayoutSlot}, bounds={bounds}, actionPoint={actionPoint}, offset={scrollViewer.VerticalOffset:0.###}.");

        ClickWithoutPointerMove(uiRoot, actionPoint);

        Assert.Same(prePush, treeView.SelectedItem);
        Assert.True(prePush.IsSelected);
        Assert.False(preCommit.IsSelected);
        Assert.False(preMerge.IsSelected);
        Assert.False(hooks.IsSelected);
    }

    [Fact]
    public void StationaryClickAfterTreeViewScroll_ShouldResolveRowNowUnderPointer()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 320f
        };

        var treeView = new TreeView
        {
            Width = 340f,
            Height = 120f
        };

        var root = new TreeViewItem
        {
            Header = "InkkSlinger",
            IsExpanded = true
        };

        for (var i = 0; i < 18; i++)
        {
            root.Items.Add(new TreeViewItem { Header = $"Before {i:00}" });
        }

        var git = new TreeViewItem
        {
            Header = ".git",
            IsExpanded = true
        };
        var hooks = new TreeViewItem
        {
            Header = "hooks",
            IsExpanded = true
        };
        var prePush = new TreeViewItem { Header = "pre-push.sample" };
        hooks.Items.Add(new TreeViewItem { Header = "prepare-commit-msg.sample" });
        hooks.Items.Add(new TreeViewItem { Header = "pre-merge-commit.sample" });
        hooks.Items.Add(prePush);
        git.Items.Add(hooks);
        root.Items.Add(git);

        for (var i = 0; i < 18; i++)
        {
            root.Items.Add(new TreeViewItem { Header = $"After {i:00}" });
        }

        treeView.Items.Add(root);
        host.AddChild(treeView);
        Canvas.SetLeft(treeView, 20f);
        Canvas.SetTop(treeView, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var initialPointer = new Vector2(treeView.LayoutSlot.X + 40f, treeView.LayoutSlot.Y + 44f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(initialPointer, pointerMoved: true));
        var initiallyHovered = uiRoot.GetHoveredElementForDiagnostics();
        Assert.IsType<TreeViewItem>(initiallyHovered);
        Assert.NotSame(prePush, initiallyHovered);

        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(treeView.GetVisualChildren()));
        scrollViewer.ScrollToVerticalOffset(425f);
        RunLayout(uiRoot);

        var scrolledPoint = new Vector2(prePush.LayoutSlot.X + 40f, prePush.LayoutSlot.Y + 8f);
        Assert.InRange(MathF.Abs(scrolledPoint.Y - initialPointer.Y), 0f, 18f);
        var hit = VisualTreeHelper.HitTest(host, scrolledPoint);
        Assert.True(IsDescendantOrSelf(prePush, hit));

        ClickWithoutPointerMove(uiRoot, scrolledPoint);

        Assert.Same(prePush, treeView.SelectedItem);
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
