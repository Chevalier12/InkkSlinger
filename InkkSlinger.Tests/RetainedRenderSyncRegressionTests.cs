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
        Assert.Equal(0, uiRoot.DirtyRenderQueueCount);
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
    public void ShallowSyncReject_RenderStateChangedByTransform_TracksDirtyUnionEnvelope()
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
        Assert.Single(regions);
        Assert.Equal(10f, regions[0].X);
        Assert.Equal(70f, regions[0].Width);
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
}
