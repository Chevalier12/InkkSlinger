using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public class DirtyRegionTrackingTests
{
    [Fact]
    public void RenderInvalidation_TracksUnionOfOldAndNewBounds()
    {
        var root = new Panel();
        var child = new Border();
        root.AddChild(child);
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));
        child.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 200f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        child.SetLayoutSlot(new LayoutRect(60f, 10f, 20f, 20f));
        child.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var regions = uiRoot.GetDirtyRegionsSnapshotForTests();
        Assert.Single(regions);
        Assert.Equal(10f, regions[0].X);
        Assert.Equal(10f, regions[0].Y);
        Assert.Equal(70f, regions[0].Width);
        Assert.Equal(20f, regions[0].Height);
    }

    [Fact]
    public void DirtyRegions_FallbackToFullRedraw_WhenFragmented()
    {
        var tracker = new DirtyRegionTracker(maxRegionCount: 2);
        tracker.SetViewport(new LayoutRect(0f, 0f, 100f, 100f));

        tracker.AddDirtyRegion(new LayoutRect(0f, 0f, 10f, 10f));
        tracker.AddDirtyRegion(new LayoutRect(30f, 0f, 10f, 10f));
        tracker.AddDirtyRegion(new LayoutRect(60f, 0f, 10f, 10f));

        Assert.True(tracker.IsFullFrameDirty);
        Assert.Equal(1, tracker.FullRedrawFallbackCount);
    }

    [Fact]
    public void DirtyRegions_MergeIntersectingOrTouchingBounds()
    {
        var tracker = new DirtyRegionTracker(maxRegionCount: 8);
        tracker.SetViewport(new LayoutRect(0f, 0f, 100f, 100f));

        tracker.AddDirtyRegion(new LayoutRect(10f, 10f, 20f, 20f));
        tracker.AddDirtyRegion(new LayoutRect(30f, 10f, 20f, 20f));

        Assert.False(tracker.IsFullFrameDirty);
        Assert.Single(tracker.Regions);
        Assert.Equal(10f, tracker.Regions[0].X);
        Assert.Equal(10f, tracker.Regions[0].Y);
        Assert.Equal(40f, tracker.Regions[0].Width);
        Assert.Equal(20f, tracker.Regions[0].Height);
    }

    [Fact]
    public void UiRoot_DirtyDiagnostics_ReflectDirtyRegionState()
    {
        var root = new Panel();
        var child = new Border();
        root.AddChild(child);
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        child.SetLayoutSlot(new LayoutRect(0f, 0f, 20f, 20f));
        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 100f, 100f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        child.InvalidateVisual();

        Assert.False(uiRoot.IsFullDirtyForTests());
        Assert.Single(uiRoot.GetDirtyRegionsSnapshotForTests());
        Assert.True(uiRoot.GetDirtyCoverageForTests() > 0d);
    }

    [Fact]
    public void UiRoot_DirtyRegions_MergeTouchingInvalidations()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 100f));
        var left = new Border();
        var right = new Border();
        left.SetLayoutSlot(new LayoutRect(10f, 10f, 10f, 10f));
        right.SetLayoutSlot(new LayoutRect(20f, 10f, 10f, 10f));
        root.AddChild(left);
        root.AddChild(right);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 100f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        left.InvalidateVisual();
        right.InvalidateVisual();

        var regions = uiRoot.GetDirtyRegionsSnapshotForTests();
        Assert.Single(regions);
        Assert.Equal(10f, regions[0].X);
        Assert.Equal(20f, regions[0].Width);
    }

    [Fact]
    public void UiRoot_FallsBackToFullRedraw_WhenDirtyRegionBudgetExceeded()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 600f, 120f));

        var children = new List<Border>();
        for (var i = 0; i < 13; i++)
        {
            var child = new Border();
            child.SetLayoutSlot(new LayoutRect(i * 40f, 20f, 10f, 10f));
            children.Add(child);
            root.AddChild(child);
        }

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 600f, 120f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        foreach (var child in children)
        {
            child.InvalidateVisual();
        }

        Assert.True(uiRoot.IsFullDirtyForTests());
        Assert.Equal(1, uiRoot.FullRedrawFallbackCount);
    }
}
