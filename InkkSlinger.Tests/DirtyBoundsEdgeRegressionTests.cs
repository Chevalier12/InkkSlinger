using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    public void RenderInvalidation_FromDetachedSource_IsIgnoredByLiveRoot()
    {
        var uiRoot = new UiRoot(new Panel());
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 200f));
        uiRoot.ResetDirtyStateForTests();

        uiRoot.NotifyInvalidation(UiInvalidationType.Render, new Border());

        Assert.False(uiRoot.IsFullDirtyForTests());
        Assert.Empty(uiRoot.GetDirtyRegionsSnapshotForTests());
        Assert.Equal(0, uiRoot.RenderInvalidationCount);
    }

    [Fact]
    public void DetachedElementInvalidations_DoNotMutateLiveRootState()
    {
        var root = new Panel();
        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 200f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        var detached = new Border();
        detached.InvalidateMeasure();
        detached.InvalidateArrange();
        detached.InvalidateVisual();

        Assert.False(uiRoot.IsFullDirtyForTests());
        Assert.Empty(uiRoot.GetDirtyRegionsSnapshotForTests());
        Assert.Equal(0, uiRoot.MeasureInvalidationCount);
        Assert.Equal(0, uiRoot.ArrangeInvalidationCount);
        Assert.Equal(0, uiRoot.RenderInvalidationCount);
    }

    [Fact]
    public void AttachedElementInvalidations_StillMutateLiveRootState()
    {
        var root = new Panel();
        var child = new Border();
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 200f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        child.InvalidateMeasure();

        Assert.True(uiRoot.MeasureInvalidationCount > 0);
        Assert.True(uiRoot.ArrangeInvalidationCount > 0);
        Assert.True(uiRoot.RenderInvalidationCount > 0);
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
    public void ArrangeInvalidation_FromChild_DoesNotEscalateDirtyBoundsToRoot()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));

        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(40f, 50f, 30f, 20f));
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 200f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        child.InvalidateArrange();

        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();

        Assert.NotEmpty(dirtyRegions);
        Assert.All(dirtyRegions, region =>
        {
            Assert.True(region.Width < root.LayoutSlot.Width, $"Expected localized dirty width, got {region.Width:0.##}.");
            Assert.True(region.Height < root.LayoutSlot.Height, $"Expected localized dirty height, got {region.Height:0.##}.");
        });
        Assert.True(uiRoot.GetDirtyCoverageForTests() < 1d, $"Expected localized coverage, got {uiRoot.GetDirtyCoverageForTests():0.###}.");
    }

    [Fact]
    public void MeasureInvalidation_FromGridChild_DoesNotPromoteDirtyBoundsToAncestorGrid()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 320f, 240f));

        var host = new Grid
        {
            Width = 280f,
            Height = 180f
        };
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var child = new Border
        {
            Width = 96f,
            Height = 48f,
            Margin = new Thickness(12f)
        };
        host.AddChild(child);
        root.AddChild(host);

        var uiRoot = new UiRoot(root);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 320, 240));
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 320f, 240f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        child.InvalidateMeasure();

        var invalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();
        var dirtyTrace = uiRoot.GetDirtyBoundsEventTraceForTests();

        Assert.True(invalidation.HasDirtyBounds);
        Assert.NotEmpty(dirtyRegions);
        Assert.DoesNotContain(
            dirtyTrace,
            entry => entry.StartsWith("Grid#:bounds:", StringComparison.Ordinal) ||
                     entry.StartsWith("Grid#:begin", StringComparison.Ordinal) ||
                     entry.Contains("dirty-add:unchanged:0,0,280,180", StringComparison.Ordinal));
        Assert.All(dirtyRegions, region =>
        {
            Assert.True(region.Width < host.ActualWidth, $"Expected localized dirty width, got {region.Width:0.##} for host width {host.ActualWidth:0.##}.");
            Assert.True(region.Height < host.ActualHeight, $"Expected localized dirty height, got {region.Height:0.##} for host height {host.ActualHeight:0.##}.");
        });
    }

    [Fact]
    public void RenderInvalidation_OnGridWithoutGridLines_PreservesUnchangedDirtyRegion()
    {
        var root = new Panel();
        var grid = new Grid
        {
            Width = 220f,
            Height = 140f
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.AddChild(new Border { Width = 80f, Height = 32f });
        root.AddChild(grid);

        var uiRoot = new UiRoot(root);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 320, 240));
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 320f, 240f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        Assert.True(grid.TryGetRenderBoundsInRootSpace(out var gridBounds));

        grid.InvalidateVisual();

        Assert.Contains(
            uiRoot.GetDirtyRegionsSnapshotForTests(),
            region => Contains(region, gridBounds));
        Assert.DoesNotContain(
            uiRoot.GetDirtyBoundsEventTraceForTests(),
            entry => entry.StartsWith("dirty-skip:unchanged:Grid", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderInvalidation_OnGridWithGridLines_PreservesUnchangedDirtyRegion()
    {
        var root = new Panel();
        var grid = new Grid
        {
            Width = 220f,
            Height = 140f,
            ShowGridLines = true
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.AddChild(new Border { Width = 80f, Height = 32f });
        root.AddChild(grid);

        var uiRoot = new UiRoot(root);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 320, 240));
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 320f, 240f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        grid.InvalidateVisual();

        Assert.NotEmpty(uiRoot.GetDirtyRegionsSnapshotForTests());
        Assert.DoesNotContain(
            uiRoot.GetDirtyBoundsEventTraceForTests(),
            entry => entry.StartsWith("dirty-skip:unchanged:Grid", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderInvalidation_OnSelfRenderingPanelWithUnchangedBounds_PreservesDirtyRegionDuringPartialRedraw()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 800f, 400f));

        var panel = new StackPanel
        {
            Background = Color.Transparent
        };
        panel.SetLayoutSlot(new LayoutRect(20f, 20f, 100f, 100f));

        var marker = new Border();
        marker.SetLayoutSlot(new LayoutRect(700f, 20f, 10f, 10f));

        root.AddChild(panel);
        root.AddChild(marker);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 800f, 400f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        Assert.True(panel.TryGetRenderBoundsInRootSpace(out var panelBounds));

        panel.Background = new Color(0x30, 0x70, 0xB0);
        marker.InvalidateVisual();

        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();

        Assert.True(uiRoot.WouldUsePartialDirtyRedrawForTests());
        Assert.Contains(dirtyRegions, region => Contains(region, panelBounds));
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
    public void EscapingRenderMutation_UsesLocalizedDirtyBoundsSource_InsteadOfCanvasClipAncestor()
    {
        var root = new Canvas
        {
            Width = 200f,
            Height = 120f
        };
        var child = new Border
        {
            Width = 60f,
            Height = 30f,
            Effect = new DropShadowEffect
            {
                BlurRadius = 0f,
                Opacity = 0.5f
            },
            RenderTransform = new ScaleTransform
            {
                ScaleX = 1f,
                ScaleY = 1f
            }
        };
        root.AddChild(child);
        Canvas.SetLeft(child, 40f);
        Canvas.SetTop(child, 30f);

        var uiRoot = new UiRoot(root);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 200, 120));
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 200f, 120f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        var transform = Assert.IsType<ScaleTransform>(child.RenderTransform);
        transform.ScaleX = 1.2f;

        var invalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();

        Assert.True(invalidation.HasDirtyBounds);
        Assert.Equal(nameof(Border), invalidation.DirtyBoundsVisualType);
        Assert.NotEmpty(dirtyRegions);
        Assert.All(dirtyRegions, region =>
        {
            Assert.True(region.Width < root.Width, $"Expected localized dirty width, got {region.Width:0.##} for root width {root.Width:0.##}.");
            Assert.True(region.Height < root.Height, $"Expected localized dirty height, got {region.Height:0.##} for root height {root.Height:0.##}.");
        });
    }

    [Fact]
    public void GridSplitterHoverRenderInvalidation_UsesLocalizedDirtyBoundsHint_InsideScrollViewer()
    {
        var root = new Border
        {
            Width = 420f,
            Height = 260f,
            Padding = new Thickness(12f)
        };

        var viewer = new ScrollViewer
        {
            Width = 396f,
            Height = 236f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var content = new StackPanel();
        for (var i = 0; i < 6; i++)
        {
            content.AddChild(new Border
            {
                Height = 44f,
                Margin = new Thickness(0f, 0f, 0f, 8f)
            });
        }

        var grid = new Grid
        {
            Height = 80f
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.AddChild(new Border { Height = 48f });

        var splitter = new GridSplitter
        {
            Width = 8f,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext
        };
        Grid.SetColumn(splitter, 1);
        grid.AddChild(splitter);

        var right = new Border { Height = 48f };
        Grid.SetColumn(right, 2);
        grid.AddChild(right);
        content.AddChild(grid);

        for (var i = 0; i < 8; i++)
        {
            content.AddChild(new Border
            {
                Height = 44f,
                Margin = new Thickness(0f, 0f, 0f, 8f)
            });
        }

        viewer.Content = content;
        root.Child = viewer;

        var uiRoot = new UiRoot(root);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 420, 260));
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 420f, 260f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        Assert.True(splitter.TryGetRenderBoundsInRootSpace(out var splitterBounds));

        splitter.SetMouseOverFromInput(true);
        uiRoot.SynchronizeRetainedRenderListForTests();

        var invalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();
        var dirtyTrace = uiRoot.GetDirtyBoundsEventTraceForTests();

        Assert.False(uiRoot.IsFullDirtyForTests());
        if (invalidation.HasDirtyBounds)
        {
            if (invalidation.DirtyBoundsUsedHint)
            {
                Assert.Contains(dirtyTrace, entry => entry.Contains("dirty-add:hint:", StringComparison.Ordinal) || entry.Contains("dirty-add:scroll-clip-hint", StringComparison.Ordinal));
            }

            Assert.True(invalidation.DirtyBounds.Width <= splitterBounds.Width + 1f, $"Expected localized splitter dirty hint width, got {invalidation.DirtyBounds.Width:0.##} for splitter width {splitterBounds.Width:0.##}.");
            Assert.True(invalidation.DirtyBounds.Height <= splitterBounds.Height + 1f, $"Expected localized splitter dirty hint height, got {invalidation.DirtyBounds.Height:0.##} for splitter height {splitterBounds.Height:0.##}.");
            if (dirtyRegions.Count > 0)
            {
                Assert.Contains(dirtyRegions, region =>
                    region.X <= splitterBounds.X + 0.5f &&
                    region.Y <= splitterBounds.Y + 0.5f &&
                    region.X + region.Width >= splitterBounds.X + splitterBounds.Width - 0.5f &&
                    region.Y + region.Height >= splitterBounds.Y + splitterBounds.Height - 0.5f);
            }
        }
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
    public void ConstructingDetachedSubtree_DoesNotMutateLiveVisualStructureMetrics()
    {
        var root = new Panel();
        root.AddChild(new Border());

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        var beforeMetrics = uiRoot.GetMetricsSnapshot();
        var beforePerf = uiRoot.GetPerformanceTelemetrySnapshotForTests();

        var detachedRoot = new StackPanel();
        for (var i = 0; i < 8; i++)
        {
            var row = new StackPanel();
            row.AddChild(new Label { Content = $"Label {i}" });
            row.AddChild(new Button { Content = $"Button {i}" });
            detachedRoot.AddChild(row);
        }

        var afterMetrics = uiRoot.GetMetricsSnapshot();
        var afterPerf = uiRoot.GetPerformanceTelemetrySnapshotForTests();

        Assert.Equal(beforeMetrics.VisualStructureChangeCount, afterMetrics.VisualStructureChangeCount);
        Assert.Equal(beforePerf.VisualIndexVersion, afterPerf.VisualIndexVersion);
        Assert.False(uiRoot.IsRenderListFullRebuildPendingForTests());
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
    public void RenderDirtyHint_UnderTranslatedClipAncestor_PreservesRootSpaceDirtyRegion()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 200f));

        var clipHost = new Border
        {
            ClipToBounds = true,
            RenderTransform = new TranslateTransform { X = 120f, Y = 0f }
        };
        clipHost.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));

        var child = new HintDirtyBorder();
        child.SetLayoutSlot(new LayoutRect(70f, 10f, 60f, 20f));
        clipHost.Child = child;
        root.AddChild(clipHost);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 400f, 200f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        Assert.True(child.TryGetRenderBoundsInRootSpace(out var hintBounds));
        Assert.True(clipHost.TryGetRenderBoundsInRootSpace(out var clipHostBounds));

        child.PrimeRootSpaceHintAndInvalidate(hintBounds);

        var invalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();

        Assert.True(invalidation.DirtyBoundsUsedHint);
        Assert.Single(dirtyRegions);
        Assert.Equal(190f, dirtyRegions[0].X);
        Assert.Equal(10f, dirtyRegions[0].Y);
        Assert.Equal(30f, dirtyRegions[0].Width);
        Assert.Equal(20f, dirtyRegions[0].Height);
        Assert.True(dirtyRegions[0].X >= clipHostBounds.X, $"Expected dirty region to remain inside translated clip host. host={clipHostBounds} dirty={dirtyRegions[0]}");
        Assert.True(dirtyRegions[0].X + dirtyRegions[0].Width <= clipHostBounds.X + clipHostBounds.Width, $"Expected dirty region to remain inside translated clip host. host={clipHostBounds} dirty={dirtyRegions[0]}");
    }

    private sealed class HintDirtyBorder : Border, IRenderDirtyBoundsHintProvider
    {
        private bool _hasPendingHint;
        private LayoutRect _pendingHint;

        public void PrimeRootSpaceHintAndInvalidate(LayoutRect bounds)
        {
            _pendingHint = bounds;
            _hasPendingHint = true;
            base.InvalidateVisual();
        }

        bool IRenderDirtyBoundsHintProvider.TryConsumeRenderDirtyBoundsHint(out LayoutRect bounds)
        {
            if (!_hasPendingHint)
            {
                bounds = default;
                return false;
            }

            bounds = _pendingHint;
            _hasPendingHint = false;
            return true;
        }
    }

    private static bool Contains(LayoutRect outer, LayoutRect inner)
    {
        return inner.X >= outer.X &&
               inner.Y >= outer.Y &&
               inner.X + inner.Width <= outer.X + outer.Width &&
               inner.Y + inner.Height <= outer.Y + outer.Height;
    }

}
