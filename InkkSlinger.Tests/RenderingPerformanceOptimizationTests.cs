using Xunit;

namespace InkkSlinger.Tests;

public sealed class RenderingPerformanceOptimizationTests
{
    [Fact]
    public void RetainedTraversal_UsesLocalClipStateInsteadOfReplayingAncestorClipPath()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 240f, 180f));
        var outer = new ClipPanel();
        outer.SetLayoutSlot(new LayoutRect(10f, 10f, 180f, 140f));
        var inner = new ClipPanel();
        inner.SetLayoutSlot(new LayoutRect(20f, 20f, 120f, 90f));
        var leaf = new Border();
        leaf.SetLayoutSlot(new LayoutRect(30f, 30f, 40f, 24f));

        inner.AddChild(leaf);
        outer.AddChild(inner);
        root.AddChild(outer);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        var metrics = uiRoot.GetRetainedTraversalMetricsForClipForTests(new LayoutRect(0f, 0f, 240f, 180f));

        Assert.Equal(4, metrics.NodesDrawn);
        Assert.Equal(2, metrics.LocalClipPushCount);
    }

    [Fact]
    public void RetainedTraversal_SkipsContainerSelfDrawOutsideDirtyClip_WhileKeepingVisibleChild()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));
        var container = new Panel();
        container.SetLayoutSlot(new LayoutRect(0f, 0f, 8f, 8f));
        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(80f, 80f, 16f, 16f));

        container.AddChild(child);
        root.AddChild(container);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();

        var clip = new LayoutRect(80f, 80f, 16f, 16f);
        var order = uiRoot.GetRetainedDrawOrderForClipForTests(clip);
        var metrics = uiRoot.GetRetainedTraversalMetricsForClipForTests(clip);

        Assert.Contains(root, order);
        Assert.DoesNotContain(container, order);
        Assert.Contains(child, order);
        Assert.Equal(3, metrics.NodesVisited);
        Assert.Equal(2, metrics.NodesDrawn);
    }

    [Fact]
    public void DirtyRegionStrategy_UsesPartialRedraw_ForSmallLocalizedInvalidation()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(5f, 5f, 10f, 10f));
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 100f, 100f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        child.InvalidateVisual();

        Assert.True(uiRoot.WouldUsePartialDirtyRedrawForTests());
    }

    [Fact]
    public void DirtyRegionStrategy_FallsBackToFullRedraw_WhenCoverageExceedsThreshold()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(0f, 0f, 50f, 50f));
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 100f, 100f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        child.InvalidateVisual();

        Assert.False(uiRoot.WouldUsePartialDirtyRedrawForTests());
    }

    [Fact]
    public void DirtyRegionStrategy_FallsBackToFullRedraw_WhenRegionCountIsFragmented()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));
        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 200f));

        for (var i = 0; i < 5; i++)
        {
            var child = new Border();
            child.SetLayoutSlot(new LayoutRect(10f + (i * 30f), 10f, 10f, 10f));
            root.AddChild(child);
        }

        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        foreach (var child in root.Children)
        {
            child.InvalidateVisual();
        }

        Assert.False(uiRoot.WouldUsePartialDirtyRedrawForTests());
    }

    [Fact]
    public void DirtyRegionStrategy_FallsBackToFullRedraw_ForMultipleMediumCoverageRegions()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));

        var left = new Border();
        left.SetLayoutSlot(new LayoutRect(0f, 0f, 30f, 30f));
        root.AddChild(left);

        var right = new Border();
        right.SetLayoutSlot(new LayoutRect(60f, 0f, 30f, 30f));
        root.AddChild(right);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 100f, 100f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        left.InvalidateVisual();
        right.InvalidateVisual();

        Assert.Equal(2, uiRoot.GetDirtyRegionsSnapshotForTests().Count);
        Assert.Equal(0.18d, uiRoot.GetDirtyCoverageForTests(), 6);
        Assert.False(uiRoot.WouldUsePartialDirtyRedrawForTests());
    }

    [Fact]
    public void ColorSpectrum_HueSelectorDrag_UsesLocalizedDirtyBoundsHint()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 320f, 120f));

        var spectrum = new ColorSpectrum
        {
            Orientation = Orientation.Horizontal,
            Mode = ColorSpectrumMode.Hue
        };
        spectrum.SetLayoutSlot(new LayoutRect(20f, 40f, 200f, 20f));
        root.AddChild(spectrum);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 320f, 120f));
        uiRoot.RebuildRenderListForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        spectrum.Hue = 180f;

        var regions = uiRoot.GetDirtyRegionsSnapshotForTests();
        Assert.Single(regions);
        Assert.True(uiRoot.WouldUsePartialDirtyRedrawForTests());
    }

    [Fact]
    public void ColorPicker_SelectorMove_UsesLocalizedDirtyBoundsHint()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 320f, 220f));

        var picker = new ColorPicker();
        picker.SetLayoutSlot(new LayoutRect(20f, 20f, 200f, 150f));
        root.AddChild(picker);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 320f, 220f));
        uiRoot.RebuildRenderListForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        picker.Saturation = 0.35f;

        var regions = uiRoot.GetDirtyRegionsSnapshotForTests();
        Assert.Single(regions);
        Assert.True(uiRoot.WouldUsePartialDirtyRedrawForTests());
    }

    [Fact]
    public void ShapeRenderCache_InvalidatesOnRenderAndLayoutChanges()
    {
        Shape.ResetRenderCacheMetricsForTests();
        var shape = new PolygonShape
        {
            Points = "0,0 10,0 10,10 0,10"
        };
        shape.SetLayoutSlot(new LayoutRect(0f, 0f, 20f, 20f));

        shape.PrimeRenderCacheForTests();
        shape.PrimeRenderCacheForTests();
        Assert.True(Shape.GetRenderCacheMissCountForTests() >= 1);
        Assert.True(Shape.GetRenderCacheHitCountForTests() >= 1);

        shape.ClearMeasureInvalidation();
        shape.ClearArrangeInvalidation();
        shape.ClearRenderInvalidationShallow();
        var missesAfterWarm = Shape.GetRenderCacheMissCountForTests();
        shape.StrokeThickness = 2f;
        shape.PrimeRenderCacheForTests();
        Assert.True(Shape.GetRenderCacheMissCountForTests() > missesAfterWarm);

        shape.ClearMeasureInvalidation();
        shape.ClearArrangeInvalidation();
        shape.ClearRenderInvalidationShallow();
        var missesAfterRenderChange = Shape.GetRenderCacheMissCountForTests();
        shape.SetLayoutSlot(new LayoutRect(0f, 0f, 28f, 24f));
        shape.PrimeRenderCacheForTests();
        Assert.True(Shape.GetRenderCacheMissCountForTests() > missesAfterRenderChange);
    }

    private sealed class ClipPanel : Panel
    {
        protected override bool TryGetClipRect(out LayoutRect clipRect)
        {
            clipRect = LayoutSlot;
            return true;
        }
    }
}
