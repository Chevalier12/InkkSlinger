using System;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RetainedRenderSyncRegressionTests
{
    [Fact]
    public void RebuildRetainedList_RootSubtreeEndIndex_MatchesRetainedNodeCount()
    {
        var root = new Panel();
        var first = new Border();
        var second = new Border();
        root.AddChild(first);
        root.AddChild(second);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        Assert.Equal(
            uiRoot.RetainedRenderNodeCount,
            uiRoot.GetRetainedNodeSubtreeEndIndexForTests(root));
    }

    [Fact]
    public void DirtyQueueCompaction_AncestorAndChildInvalidated_KeepsRetainedOrderStable()
    {
        var root = new Panel();
        var parent = new Border();
        var child = new Border();
        parent.Child = child;
        root.AddChild(parent);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        parent.InvalidateVisual();
        child.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var order = uiRoot.GetRetainedVisualOrderForTests();
        Assert.Collection(
            order,
            visual => Assert.Same(root, visual),
            visual => Assert.Same(parent, visual),
            visual => Assert.Same(child, visual));
        Assert.Equal(1, uiRoot.GetPerformanceTelemetrySnapshotForTests().DirtyRootCount);
        Assert.Equal(0, uiRoot.DirtyRenderQueueCount);
    }

    [Fact]
    public void DirtyQueueCompaction_ChildThenAncestorInvalidated_KeepsRetainedOrderStable()
    {
        var root = new Panel();
        var parent = new Border();
        var child = new Border();
        parent.Child = child;
        root.AddChild(parent);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        child.InvalidateVisual();
        parent.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var order = uiRoot.GetRetainedVisualOrderForTests();
        Assert.Collection(
            order,
            visual => Assert.Same(root, visual),
            visual => Assert.Same(parent, visual),
            visual => Assert.Same(child, visual));
        Assert.Equal(1, uiRoot.GetPerformanceTelemetrySnapshotForTests().DirtyRootCount);
        Assert.Equal(0, uiRoot.DirtyRenderQueueCount);
    }

    [Fact]
    public void DirtyQueueCompaction_SiblingInvalidations_DoNotCollapseToUnrelatedAncestor()
    {
        var root = new Panel();
        var first = new Border();
        var second = new Border();
        root.AddChild(first);
        root.AddChild(second);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        first.InvalidateVisual();
        second.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var order = uiRoot.GetRetainedVisualOrderForTests();
        Assert.Equal(3, order.Count);
        Assert.Contains(first, order);
        Assert.Contains(second, order);
        Assert.Equal(2, uiRoot.GetPerformanceTelemetrySnapshotForTests().DirtyRootCount);
        Assert.Equal(0, uiRoot.DirtyRenderQueueCount);
    }

    [Fact]
    public void DirtyQueueCompaction_SingleInvalidation_KeepsOneDirtyRootAndClearsQueue()
    {
        var root = new Panel();
        var child = new Border();
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        child.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Equal(1, uiRoot.GetPerformanceTelemetrySnapshotForTests().DirtyRootCount);
        Assert.Equal(0, uiRoot.DirtyRenderQueueCount);
        Assert.Contains(child, uiRoot.GetRetainedVisualOrderForTests());
    }

    [Fact]
    public void ShallowSyncReject_EffectiveVisibilityChanged_RetainedStructureRemainsValid()
    {
        var root = new Panel();
        var child = new Border();
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 200f));
        uiRoot.ResetDirtyStateForTests();

        child.Visibility = Visibility.Collapsed;
        uiRoot.SynchronizeRetainedRenderListForTests();

        var order = uiRoot.GetRetainedVisualOrderForTests();
        Assert.Collection(
            order,
            visual => Assert.Same(root, visual),
            visual => Assert.Same(child, visual));
        Assert.True(uiRoot.GetRetainedNodeSubtreeEndIndexForTests(root) >= 2);
        Assert.Equal(uiRoot.RetainedRenderNodeCount, uiRoot.GetRetainedNodeSubtreeEndIndexForTests(root));
    }

    [Fact]
    public void ShallowSyncReject_RenderStateChangedByTransform_TracksDisjointDirtyRegions()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 240f, 120f));
        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 240f, 120f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        child.RenderTransform = new TranslateTransform { X = 50f, Y = 0f };
        uiRoot.SynchronizeRetainedRenderListForTests();

        var regions = uiRoot.GetDirtyRegionsSnapshotForTests();
        Assert.Equal(2, regions.Count);
        Assert.Equal(10f, regions[0].X);
        Assert.Equal(20f, regions[0].Width);
        Assert.Equal(60f, regions[1].X);
        Assert.Equal(20f, regions[1].Width);
        Assert.Equal(uiRoot.RetainedRenderNodeCount, uiRoot.GetRetainedNodeSubtreeEndIndexForTests(root));
    }

    [Fact]
    public void RenderStateChange_ByRenderTransformOrigin_MarksDirtyRegionAndKeepsSubtreeMetadataConsistent()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 240f, 120f));
        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(20f, 20f, 30f, 20f));
        child.RenderTransform = new ScaleTransform { ScaleX = 1.2f, ScaleY = 1.1f };
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 240f, 120f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        child.RenderTransformOrigin = new Microsoft.Xna.Framework.Vector2(0.5f, 0.5f);
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.NotEmpty(uiRoot.GetDirtyRegionsSnapshotForTests());
        Assert.Equal(uiRoot.RetainedRenderNodeCount, uiRoot.GetRetainedNodeSubtreeEndIndexForTests(root));
    }

    [Fact]
    public void VisibilityToggle_CollapsedThenVisible_PreservesRetainedMembershipAndIndices()
    {
        var root = new Panel();
        var child = new Border();
        root.AddChild(child);
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        child.Visibility = Visibility.Collapsed;
        uiRoot.SynchronizeRetainedRenderListForTests();
        child.Visibility = Visibility.Visible;
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Contains(child, uiRoot.GetRetainedVisualOrderForTests());
        Assert.Equal(uiRoot.RetainedRenderNodeCount, uiRoot.GetRetainedNodeSubtreeEndIndexForTests(root));
    }

    [Fact]
    public void ForcedDeepSync_WithCleanDescendants_DowngradesToShallowPath()
    {
        var root = new Panel();
        var parent = new Panel();
        var child = new Panel();
        var grandChild = new Border();
        child.AddChild(grandChild);
        parent.AddChild(child);
        root.AddChild(parent);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        child.InvalidateVisual();
        parent.InvalidateVisual();
        root.ClearRenderInvalidationRecursive();
        uiRoot.RemoveRetainedNodeIndexForTests(grandChild);

        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.False(uiRoot.IsRenderListFullRebuildPendingForTests());
        Assert.Throws<ArgumentException>(() => uiRoot.GetRetainedNodeSubtreeEndIndexForTests(grandChild));
    }

    [Fact]
    public void ForcedDeepSync_WithDirtyDescendants_DoesNotDowngradeAndRebuilds()
    {
        var root = new Panel();
        var parent = new Panel();
        var child = new Panel();
        var grandChild = new Border();
        child.AddChild(grandChild);
        parent.AddChild(child);
        root.AddChild(parent);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        child.InvalidateVisual();
        parent.InvalidateVisual();
        uiRoot.RemoveRetainedNodeIndexForTests(grandChild);

        uiRoot.SynchronizeRetainedRenderListForTests();

        _ = uiRoot.GetRetainedNodeSubtreeEndIndexForTests(grandChild);
        Assert.Contains(grandChild, uiRoot.GetRetainedVisualOrderForTests());
    }

    [Fact]
    public void StructureMismatch_MissingChildIndex_TriggersFullRebuildAndRecovery()
    {
        var root = new Panel();
        var parent = new Panel();
        var child = new Border();
        parent.AddChild(child);
        root.AddChild(parent);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        uiRoot.RemoveRetainedNodeIndexForTests(child);
        parent.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        _ = uiRoot.GetRetainedNodeSubtreeEndIndexForTests(child);
        Assert.True(uiRoot.IsFullDirtyForTests());
        Assert.Equal(3, uiRoot.GetRetainedVisualOrderForTests().Count);
    }

    [Fact]
    public void DeepHierarchy_MultipleInvalidations_RootSubtreeIndexRemainsConsistentAfterSync()
    {
        var root = new Panel();
        var level1 = new Panel();
        var level2 = new Panel();
        var leaf1 = new Border();
        var leaf2 = new Border();
        level2.AddChild(leaf1);
        level2.AddChild(leaf2);
        level1.AddChild(level2);
        root.AddChild(level1);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        leaf1.InvalidateVisual();
        leaf2.InvalidateVisual();
        level1.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Equal(uiRoot.RetainedRenderNodeCount, uiRoot.GetRetainedNodeSubtreeEndIndexForTests(root));
        Assert.Equal(0, uiRoot.DirtyRenderQueueCount);
    }

    [Fact]
    public void TransformScrolledContent_WheelScroll_SynchronizesRetainedTreeWithoutFullRebuild()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 320f, 240f));

        var content = new StackPanel();
        for (var i = 0; i < 12; i++)
        {
            content.AddChild(new Border
            {
                Height = 40f
            });
        }

        var viewer = new ScrollViewer
        {
            Content = content,
            Width = 180f,
            Height = 120f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        var viewport = new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 320, 240);
        uiRoot.Update(new Microsoft.Xna.Framework.GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), viewport);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        viewer.ScrollToVerticalOffset(24f);
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.False(uiRoot.IsRenderListFullRebuildPendingForTests());
        Assert.Equal(uiRoot.RetainedRenderNodeCount, uiRoot.GetRetainedNodeSubtreeEndIndexForTests(root));
    }

    [Fact]
    public void SiblingDirtyLeaves_SharedAncestorChain_IsRefreshedOncePerNode()
    {
        var root = new Panel();
        var parent = new Panel();
        var left = new Border();
        var right = new Border();
        parent.AddChild(left);
        parent.AddChild(right);
        root.AddChild(parent);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        left.InvalidateVisual();
        right.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var perfSnapshot = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        Assert.Equal(2, perfSnapshot.DirtyRootCount);
        Assert.Equal(2, perfSnapshot.AncestorMetadataRefreshNodeCount);
    }

    [Fact]
    public void StructureChangeThenSync_AddedVisualIsTrackedByRetainedIndices()
    {
        var root = new Panel();
        var first = new Border();
        root.AddChild(first);
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        var second = new Border();
        root.AddChild(second);
        uiRoot.SynchronizeRetainedRenderListForTests();

        _ = uiRoot.GetRetainedNodeSubtreeEndIndexForTests(second);
        Assert.Equal(uiRoot.RetainedRenderNodeCount, uiRoot.GetRetainedNodeSubtreeEndIndexForTests(root));
    }

    [Fact]
    public void RetainedDrawOrderForClip_FullDrawSkipsOffViewportSubtrees()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));

        var visible = new Border();
        visible.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));
        var offscreenParent = new Panel();
        offscreenParent.SetLayoutSlot(new LayoutRect(260f, 10f, 40f, 40f));
        var offscreenChild = new Border();
        offscreenChild.SetLayoutSlot(new LayoutRect(265f, 15f, 10f, 10f));
        offscreenParent.AddChild(offscreenChild);

        root.AddChild(visible);
        root.AddChild(offscreenParent);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        var order = uiRoot.GetRetainedDrawOrderForClipForTests(new LayoutRect(0f, 0f, 200f, 200f));

        Assert.Collection(
            order,
            visual => Assert.Same(root, visual),
            visual => Assert.Same(visible, visual));
    }

    [Fact]
    public void RetainedDrawOrderForClip_NodeWithoutBoundsSnapshotIsKeptConservative()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));
        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(20f, 20f, 0f, 0f));
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        var order = uiRoot.GetRetainedDrawOrderForClipForTests(new LayoutRect(0f, 0f, 40f, 40f));

        Assert.Collection(
            order,
            visual => Assert.Same(root, visual),
            visual => Assert.Same(child, visual));
    }

    [Fact]
    public void IncrementalCleanup_ClearsProcessedDirtyRootsAndAncestors()
    {
        var root = new Panel();
        var left = new Border();
        var right = new Border();
        root.AddChild(left);
        root.AddChild(right);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        left.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.ApplyRenderInvalidationCleanupForTests();

        Assert.False(left.NeedsRender);
        Assert.False(left.SubtreeDirty);
        Assert.False(root.SubtreeDirty);
        Assert.False(root.NeedsRender);
        Assert.False(right.NeedsRender);
        Assert.False(right.SubtreeDirty);
    }

    [Fact]
    public void IncrementalCleanup_DoesNotClearUnprocessedDirtySibling()
    {
        var root = new Panel();
        var left = new Border();
        var right = new Border();
        root.AddChild(left);
        root.AddChild(right);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        left.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();
        right.InvalidateVisual();

        uiRoot.ApplyRenderInvalidationCleanupForTests();

        Assert.False(left.NeedsRender);
        Assert.False(left.SubtreeDirty);
        Assert.True(right.NeedsRender);
        Assert.True(right.SubtreeDirty);
        Assert.True(root.SubtreeDirty);
    }
}
