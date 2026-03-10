using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DirtyBoundsEdgeRegressionTests
{
    [Fact]
    public void RenderInvalidation_WithNullSource_EscalatesToFullFrameDirty()
    {
        var uiRoot = new UiRoot(new Panel());
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 200f));
        uiRoot.ResetDirtyStateForTests();

        uiRoot.NotifyInvalidation(UiInvalidationType.Render, null);

        Assert.True(uiRoot.IsFullDirtyForTests());
    }

    [Fact]
    public void RenderInvalidation_FromDetachedSource_EscalatesToFullFrameDirty()
    {
        var uiRoot = new UiRoot(new Panel());
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 200f));
        uiRoot.ResetDirtyStateForTests();

        uiRoot.NotifyInvalidation(UiInvalidationType.Render, new Border());

        Assert.True(uiRoot.IsFullDirtyForTests());
    }

    [Fact]
    public void ZeroSizeBounds_DoNotAddDirtyRegions()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));
        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(10f, 10f, 0f, 0f));
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 200f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        child.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        Assert.Empty(uiRoot.GetDirtyRegionsSnapshotForTests());
        Assert.False(uiRoot.IsFullDirtyForTests());
    }

    [Fact]
    public void TransformMovement_WithDisjointBounds_TracksOldAndNewRegionsSeparately()
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
    }

    [Fact]
    public void DirtyRegionTracker_ClipsRegionsToViewportBounds()
    {
        var tracker = new DirtyRegionTracker(maxRegionCount: 8);
        tracker.SetViewport(new LayoutRect(0f, 0f, 100f, 100f));

        tracker.AddDirtyRegion(new LayoutRect(-20f, -10f, 60f, 40f));

        Assert.False(tracker.IsFullFrameDirty);
        Assert.Single(tracker.Regions);
        Assert.Equal(0f, tracker.Regions[0].X);
        Assert.Equal(0f, tracker.Regions[0].Y);
        Assert.Equal(40f, tracker.Regions[0].Width);
        Assert.Equal(30f, tracker.Regions[0].Height);
    }

    [Fact]
    public void DirtyRegionTracker_NormalizesNegativeWidthAndHeight()
    {
        var tracker = new DirtyRegionTracker(maxRegionCount: 8);
        tracker.SetViewport(new LayoutRect(0f, 0f, 100f, 100f));

        tracker.AddDirtyRegion(new LayoutRect(20f, 20f, -10f, -6f));

        Assert.False(tracker.IsFullFrameDirty);
        Assert.Single(tracker.Regions);
        Assert.Equal(10f, tracker.Regions[0].X);
        Assert.Equal(14f, tracker.Regions[0].Y);
        Assert.Equal(10f, tracker.Regions[0].Width);
        Assert.Equal(6f, tracker.Regions[0].Height);
    }

    [Fact]
    public void VisualStructureChange_WhenElementIsOutsideTree_IsNoOp()
    {
        var root = new Panel();
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        var outside = new Border();
        uiRoot.NotifyVisualStructureChanged(outside, oldParent: null, newParent: null);

        Assert.False(uiRoot.IsRenderListFullRebuildPendingForTests());
        Assert.False(uiRoot.IsFullDirtyForTests());
    }

    [Fact]
    public void VisualStructureChange_WhenOldParentIsInsideTree_TriggersRebuildAndDirty()
    {
        var root = new Panel();
        var attachedParent = new Panel();
        root.AddChild(attachedParent);
        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        var detachedElement = new Border();
        uiRoot.NotifyVisualStructureChanged(detachedElement, oldParent: attachedParent, newParent: null);

        Assert.True(uiRoot.IsRenderListFullRebuildPendingForTests());
        Assert.True(uiRoot.IsFullDirtyForTests());
    }

    [Fact]
    public void StructuralAndRenderInvalidationSameFrame_RebuildsDeterministically()
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
        first.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var order = uiRoot.GetRetainedVisualOrderForTests();
        Assert.Collection(
            order,
            visual => Assert.Same(root, visual),
            visual => Assert.Same(first, visual),
            visual => Assert.Same(second, visual));
        Assert.False(uiRoot.IsRenderListFullRebuildPendingForTests());
    }

    [Fact]
    public void DirtyRegionBudget_AtLimitRemainsPartial_AboveLimitFallsBackOnce()
    {
        var tracker = new DirtyRegionTracker(maxRegionCount: 12);
        tracker.SetViewport(new LayoutRect(0f, 0f, 1200f, 120f));

        for (var i = 0; i < 12; i++)
        {
            tracker.AddDirtyRegion(new LayoutRect(i * 100f, 0f, 10f, 10f));
        }

        Assert.False(tracker.IsFullFrameDirty);
        Assert.Equal(12, tracker.RegionCount);

        tracker.AddDirtyRegion(new LayoutRect(1190f, 0f, 10f, 10f));

        Assert.True(tracker.IsFullFrameDirty);
        Assert.Equal(1, tracker.FullRedrawFallbackCount);
    }

    [Fact]
    public void DirtyCoverage_IsClampedToOneForFullFrameState()
    {
        var tracker = new DirtyRegionTracker(maxRegionCount: 2);
        tracker.SetViewport(new LayoutRect(0f, 0f, 100f, 100f));
        tracker.AddDirtyRegion(new LayoutRect(0f, 0f, 40f, 20f));
        tracker.AddDirtyRegion(new LayoutRect(50f, 0f, 40f, 20f));
        tracker.AddDirtyRegion(new LayoutRect(95f, 0f, 5f, 10f));

        Assert.True(tracker.IsFullFrameDirty);
        Assert.Equal(1d, tracker.GetDirtyAreaCoverage());
    }
}
