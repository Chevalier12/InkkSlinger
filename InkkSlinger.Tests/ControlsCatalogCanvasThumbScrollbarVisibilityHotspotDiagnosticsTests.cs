using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogCanvasThumbScrollbarVisibilityHotspotDiagnosticsTests
{
    [Fact]
    public void CanvasPreview_ThumbDrag_SmallViewportWithVisibleScrollbars_IsScrollbarHotspot()
    {
        var large = RunVariant(new VariantDefinition("large-no-scrollbars", 1600, 1040));
        var small = RunVariant(new VariantDefinition("small-scrollbars-visible", 960, 700));
        var logPath = GetDiagnosticsLogPath("controls-catalog-canvas-thumb-scrollbar-visibility-hotspot");

        var lines = new List<string>
        {
            "scenario=Controls Catalog CanvasView thumb drag small-vs-large scrollbar visibility hotspot diagnostics",
            $"timestamp_utc={DateTime.UtcNow:O}",
            $"log_path={logPath}",
            "step_1=create ControlsCatalogView, open Canvas, and arrange it in a large viewport that keeps CanvasWorkbenchScrollViewer scrollbars hidden",
            "step_2=run a deterministic thumb-drag path and record UiRoot, ScrollViewer, ScrollBar, Track, Canvas, Grid, and CanvasView telemetry",
            "step_3=repeat the same drag path in a smaller viewport that forces CanvasWorkbenchScrollViewer scrollbars visible",
            "step_4=compare where the extra time lands with scrollbars hidden versus visible in the real catalog repro path",
            string.Empty,
            "large_variant:",
            FormatSummary(large),
            string.Empty,
            "small_variant:",
            FormatSummary(small),
            string.Empty,
            $"comparison={BuildComparison(large, small)}",
            $"inference={BuildInference(large, small)}",
            string.Empty,
            "large_steps:"
        };

        lines.AddRange(large.Steps.Select(FormatStep));
        lines.Add(string.Empty);
        lines.Add("small_steps:");
        lines.AddRange(small.Steps.Select(FormatStep));

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.WriteAllLines(logPath, lines);

        Assert.True(File.Exists(logPath));
        Assert.False(large.HasVisibleHorizontalBar || large.HasVisibleVerticalBar, "Expected large variant to avoid visible scrollbars.");
        Assert.True(small.HasVisibleHorizontalBar || small.HasVisibleVerticalBar, "Expected small variant to show at least one scrollbar.");
        Assert.True(Math.Abs(large.TotalScrollBarSyncTrackStateMs) < 0.1d, $"Expected large variant scrollbar sync work to stay negligible. actual={large.TotalScrollBarSyncTrackStateMs:0.###}");
        Assert.True(Math.Abs(small.TotalScrollBarSyncTrackStateMs) < 0.1d, $"Expected small variant scrollbar sync work to stay negligible. actual={small.TotalScrollBarSyncTrackStateMs:0.###}");
        Assert.True(Math.Abs(large.TotalTrackRefreshLayoutMs) < 0.1d, $"Expected large variant track refresh work to stay negligible. actual={large.TotalTrackRefreshLayoutMs:0.###}");
        Assert.True(Math.Abs(small.TotalTrackRefreshLayoutMs) < 0.1d, $"Expected small variant track refresh work to stay negligible. actual={small.TotalTrackRefreshLayoutMs:0.###}");
        Assert.Equal(0, small.TotalScrollViewerResolveBarsMeasureHorizontalFlips);
        Assert.True(small.TotalScrollViewerResolveBarsMeasureVerticalFlips < 2500, $"Expected the small viewport vertical auto-bar flips to stay bounded after the fix. actual={small.TotalScrollViewerResolveBarsMeasureVerticalFlips}");
        Assert.True(small.TotalScrollViewerMeasureContentCalls < 8000, $"Expected the small viewport content remeasure volume to stay bounded after the fix. actual={small.TotalScrollViewerMeasureContentCalls}");
        Assert.True(small.Steps.Max(static step => step.ScrollLayout.MeasureContentCallCount) < 400, $"Expected per-step content measures to stay bounded after the fix. actual={small.Steps.Max(static step => step.ScrollLayout.MeasureContentCallCount)}");
        Assert.True(small.Steps.Max(static step => step.ScrollLayout.ResolveBarsAndMeasureContentIterationCount) < 400, $"Expected per-step ResolveBars iterations to stay bounded after the fix. actual={small.Steps.Max(static step => step.ScrollLayout.ResolveBarsAndMeasureContentIterationCount)}");
        Assert.True(
            small.TotalCanvasViewUpdateTelemetryMs < 300d,
            $"Expected CanvasView telemetry churn to stay bounded in the small viewport drag path. actual={small.TotalCanvasViewUpdateTelemetryMs:0.###}");
        Assert.True(
            small.TotalCanvasViewSyncOverlayLayoutMs < 300d,
            $"Expected CanvasView overlay layout churn to stay bounded in the small viewport drag path. actual={small.TotalCanvasViewSyncOverlayLayoutMs:0.###}");
    }

    private static VariantRunResult RunVariant(VariantDefinition variant)
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            AnimationManager.Current.ResetForTests();
            VisualTreeHelper.ResetInstrumentationForTests();
            Freezable.ResetTelemetryForTests();
            UIElement.ResetFreezableInvalidationBatchTelemetryForTests();
            Style.ResetTelemetryForTests();
            TemplateTriggerEngine.ResetTelemetryForTests();
            VisualStateManager.ResetTelemetryForTests();
            Grid.ResetTimingForTests();

            var catalog = new ControlsCatalogView();
            catalog.ShowControl("Canvas");

            var uiRoot = new UiRoot(catalog);
            RunFrame(uiRoot, variant.ViewportWidth, variant.ViewportHeight, 16);
            RunFrame(uiRoot, variant.ViewportWidth, variant.ViewportHeight, 32);
            RunFrame(uiRoot, variant.ViewportWidth, variant.ViewportHeight, 48);
            RunFrame(uiRoot, variant.ViewportWidth, variant.ViewportHeight, 64);
            RunFrame(uiRoot, variant.ViewportWidth, variant.ViewportHeight, 80);
            PrimeRetainedRenderStateForDiagnostics(uiRoot, variant.ViewportWidth, variant.ViewportHeight);
            RunFrame(uiRoot, variant.ViewportWidth, variant.ViewportHeight, 96);
            RunFrame(uiRoot, variant.ViewportWidth, variant.ViewportHeight, 112);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var canvasView = Assert.IsType<CanvasView>(previewHost.Content);
            var viewer = Assert.IsType<ScrollViewer>(canvasView.FindName("CanvasWorkbenchScrollViewer"));
            var workbench = Assert.IsType<Canvas>(canvasView.FindName("CanvasWorkbench"));
            var thumb = Assert.IsType<Thumb>(canvasView.FindName("CanvasSceneDragThumb"));
            var horizontalBar = FindFirstVisualChild<ScrollBar>(viewer, static bar => bar.Orientation == Orientation.Horizontal && bar.IsVisible);
            var verticalBar = FindFirstVisualChild<ScrollBar>(viewer, static bar => bar.Orientation == Orientation.Vertical && bar.IsVisible);
            var hasVisibleHorizontalBar = horizontalBar != null && horizontalBar.LayoutSlot.Height > 0f;
            var hasVisibleVerticalBar = verticalBar != null && verticalBar.LayoutSlot.Width > 0f;

            var dragDeltas = BuildDragDeltas();
            var steps = new List<VariantStepMetrics>();
            var syntheticPointer = new Vector2(Canvas.GetLeft(Assert.IsType<Border>(canvasView.FindName("CanvasSceneRootCard"))), Canvas.GetTop(Assert.IsType<Border>(canvasView.FindName("CanvasSceneRootCard"))));

            for (var i = 0; i < dragDeltas.Count; i++)
            {
                syntheticPointer += dragDeltas[i];
                steps.Add(RunStep(uiRoot, variant, canvasView, thumb, i, syntheticPointer, dragDeltas[i]));
            }

            return Summarize(variant.Name, variant.ViewportWidth, variant.ViewportHeight, hasVisibleHorizontalBar, hasVisibleVerticalBar, steps);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static VariantStepMetrics RunStep(UiRoot uiRoot, VariantDefinition variant, CanvasView canvasView, Thumb thumb, int stepIndex, Vector2 pointer, Vector2 dragDelta)
    {
        _ = ScrollViewer.GetScrollMetricsAndReset();
        _ = ScrollViewer.GetValueChangedTelemetryAndReset();
        _ = ScrollViewer.GetLayoutTelemetryAndReset();
        _ = ScrollBar.GetThumbDragTelemetryAndReset();
        _ = Track.GetThumbTravelTelemetryAndReset();
        _ = Thumb.GetDragTelemetryAndReset();
        _ = CanvasView.GetDiagnosticsAndReset();
        _ = Canvas.GetTelemetryAndReset();
        Grid.ResetTimingForTests();

        InvokeCanvasViewMethod(
            canvasView,
            "HandleFocusCardDragDelta",
            thumb,
            new DragDeltaEventArgs(Thumb.DragDeltaEvent, dragDelta.X, dragDelta.Y));
        var pointerMove = uiRoot.GetPointerMoveTelemetrySnapshotForTests();
        RunFrame(uiRoot, variant.ViewportWidth, variant.ViewportHeight, 16);

        var perf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        var render = uiRoot.GetRenderTelemetrySnapshotForTests();
        var renderInvalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
        var scrollMetrics = ScrollViewer.GetScrollMetricsAndReset();
        var scrollValueChanged = ScrollViewer.GetValueChangedTelemetryAndReset();
        var scrollLayout = ScrollViewer.GetLayoutTelemetryAndReset();
        var scrollBarTelemetry = ScrollBar.GetThumbDragTelemetryAndReset();
        var trackTelemetry = Track.GetThumbTravelTelemetryAndReset();
        var thumbTelemetry = Thumb.GetDragTelemetryAndReset();
        var canvasViewTelemetry = CanvasView.GetDiagnosticsAndReset();
        var canvasTelemetry = Canvas.GetTelemetryAndReset();
        var gridTiming = Grid.GetTimingSnapshotForTests();
        Grid.ResetTimingForTests();

        return new VariantStepMetrics(
            stepIndex,
            pointer,
            uiRoot.LastUpdateMs,
            perf.InputPhaseMilliseconds,
            perf.LayoutPhaseMilliseconds,
            perf.AnimationPhaseMilliseconds,
            perf.RenderSchedulingPhaseMilliseconds,
            perf.VisualUpdateMilliseconds,
            pointerMove with { PointerResolvePath = "DirectCanvasViewDragDelta" },
            scrollMetrics,
            scrollValueChanged,
            scrollLayout,
            scrollBarTelemetry,
            trackTelemetry,
            thumbTelemetry,
            canvasViewTelemetry,
            canvasTelemetry,
            TicksToMilliseconds(gridTiming.MeasureOverrideElapsedTicks),
            TicksToMilliseconds(gridTiming.ArrangeOverrideElapsedTicks),
            render.DirtyRootCount,
            renderInvalidation.EffectiveSourceType,
            renderInvalidation.EffectiveSourceName);
    }

    private static IReadOnlyList<Vector2> BuildDragDeltas()
    {
        return new[]
        {
            new Vector2(24f, 10f),
            new Vector2(48f, 18f),
            new Vector2(72f, 26f),
            new Vector2(96f, 34f),
            new Vector2(120f, 42f),
            new Vector2(144f, 50f),
            new Vector2(132f, 70f),
            new Vector2(108f, 88f),
            new Vector2(84f, 104f),
            new Vector2(56f, 110f),
            new Vector2(24f, 98f),
            new Vector2(0f, 74f),
            new Vector2(-18f, 50f),
            new Vector2(-30f, 26f),
            new Vector2(-12f, 6f),
            new Vector2(18f, -8f),
            new Vector2(42f, -16f),
            new Vector2(66f, -20f),
            new Vector2(88f, -18f),
            new Vector2(110f, -10f)
        };
    }

    private static VariantRunResult Summarize(string name, int viewportWidth, int viewportHeight, bool hasVisibleHorizontalBar, bool hasVisibleVerticalBar, IReadOnlyList<VariantStepMetrics> steps)
    {
        var hottest = steps
            .OrderByDescending(static step => step.TrackTelemetry.RefreshLayoutForStateMutationMilliseconds)
            .ThenByDescending(static step => step.ScrollBarTelemetry.SyncTrackStateMilliseconds)
            .ThenByDescending(static step => step.LastUpdateMs)
            .First();

        return new VariantRunResult(
            name,
            viewportWidth,
            viewportHeight,
            hasVisibleHorizontalBar,
            hasVisibleVerticalBar,
            steps,
            steps.Sum(static step => step.LastUpdateMs),
            steps.Sum(static step => step.InputPhaseMs),
            steps.Sum(static step => step.LayoutPhaseMs),
            steps.Sum(static step => step.AnimationPhaseMs),
            steps.Sum(static step => step.RenderSchedulingMs),
            steps.Sum(static step => step.VisualUpdateMs),
            steps.Sum(static step => step.ScrollMetrics.SetOffsetCalls),
            steps.Sum(static step => step.ScrollMetrics.SetOffsetNoOpCalls),
            steps.Sum(static step => step.ScrollValueChanged.VerticalValueChangedCallCount),
            steps.Sum(static step => step.ScrollValueChanged.VerticalValueChangedMilliseconds),
            steps.Sum(static step => step.ScrollValueChanged.VerticalValueChangedSetOffsetsMilliseconds),
            steps.Sum(static step => step.ScrollLayout.MeasureOverrideMilliseconds),
            steps.Sum(static step => step.ScrollLayout.ArrangeOverrideMilliseconds),
            steps.Sum(static step => step.ScrollLayout.ResolveBarsAndMeasureContentMilliseconds),
            steps.Sum(static step => step.ScrollLayout.ResolveBarsAndMeasureContentIterationCount),
            steps.Sum(static step => step.ScrollLayout.ResolveBarsAndMeasureContentHorizontalFlipCount),
            steps.Sum(static step => step.ScrollLayout.ResolveBarsAndMeasureContentVerticalFlipCount),
            steps.Sum(static step => step.ScrollLayout.ResolveBarsForArrangeMilliseconds),
            steps.Sum(static step => step.ScrollLayout.ResolveBarsForArrangeIterationCount),
            steps.Sum(static step => step.ScrollLayout.MeasureContentMilliseconds),
            steps.Sum(static step => step.ScrollLayout.MeasureContentCallCount),
            steps.Sum(static step => step.ScrollLayout.UpdateScrollBarsMilliseconds),
            steps.Sum(static step => step.ScrollLayout.UpdateScrollBarsCallCount),
            steps.Sum(static step => step.ScrollBarTelemetry.SyncTrackStateMilliseconds),
            steps.Sum(static step => step.ScrollBarTelemetry.RefreshTrackLayoutMilliseconds),
            steps.Sum(static step => step.TrackTelemetry.RefreshLayoutForStateMutationMilliseconds),
            steps.Sum(static step => step.TrackTelemetry.RefreshLayoutArrangeMilliseconds),
            steps.Sum(static step => step.TrackTelemetry.RefreshLayoutDirtyBoundsMilliseconds),
            steps.Sum(static step => step.TrackTelemetry.RefreshLayoutVisualInvalidationMilliseconds),
            steps.Sum(static step => step.TrackTelemetry.RefreshLayoutForStateMutationCallCount),
            steps.Sum(static step => step.ThumbTelemetry.HandlePointerMoveMilliseconds),
            steps.Sum(static step => step.CanvasViewTelemetry.UpdateTelemetryMilliseconds),
            steps.Sum(static step => step.CanvasViewTelemetry.SyncOverlayLayoutMilliseconds),
            steps.Sum(static step => step.CanvasTelemetry.MeasureMilliseconds),
            steps.Sum(static step => step.CanvasTelemetry.ArrangeMilliseconds),
            steps.Sum(static step => step.GridMeasureMs),
            steps.Sum(static step => step.GridArrangeMs),
            steps.Sum(static step => step.DirtyRootCount),
            $"step-{hottest.StepIndex:000}",
            $"updateMs={hottest.LastUpdateMs:0.###}, layoutMs={hottest.LayoutPhaseMs:0.###}, renderSchedulingMs={hottest.RenderSchedulingMs:0.###}, scrollViewerMeasureMs={hottest.ScrollLayout.MeasureOverrideMilliseconds:0.###}, scrollViewerResolveMeasureMs={hottest.ScrollLayout.ResolveBarsAndMeasureContentMilliseconds:0.###}, scrollViewerMeasureContentMs={hottest.ScrollLayout.MeasureContentMilliseconds:0.###}, scrollViewerArrangeMs={hottest.ScrollLayout.ArrangeOverrideMilliseconds:0.###}, scrollViewerUpdateBarsMs={hottest.ScrollLayout.UpdateScrollBarsMilliseconds:0.###}, trackRefreshMs={hottest.TrackTelemetry.RefreshLayoutForStateMutationMilliseconds:0.###}, scrollBarSyncTrackMs={hottest.ScrollBarTelemetry.SyncTrackStateMilliseconds:0.###}, setOffsetCalls={hottest.ScrollMetrics.SetOffsetCalls}, setOffsetNoOps={hottest.ScrollMetrics.SetOffsetNoOpCalls}, pointerMoveMs={hottest.ThumbTelemetry.HandlePointerMoveMilliseconds:0.###}, canvasViewUpdateTelemetryMs={hottest.CanvasViewTelemetry.UpdateTelemetryMilliseconds:0.###}, effectiveRenderSource={hottest.EffectiveRenderSourceType}#{hottest.EffectiveRenderSourceName}");
    }

    private static string FormatSummary(VariantRunResult result)
    {
        return string.Join(Environment.NewLine,
        [
            $"name={result.Name}",
            $"viewport={result.ViewportWidth}x{result.ViewportHeight}",
            $"visible_horizontal_bar={result.HasVisibleHorizontalBar}",
            $"visible_vertical_bar={result.HasVisibleVerticalBar}",
            $"total_update_ms={result.TotalUpdateMs:0.###}",
            $"total_input_ms={result.TotalInputMs:0.###}",
            $"total_layout_ms={result.TotalLayoutMs:0.###}",
            $"total_animation_ms={result.TotalAnimationMs:0.###}",
            $"total_render_scheduling_ms={result.TotalRenderSchedulingMs:0.###}",
            $"total_visual_update_ms={result.TotalVisualUpdateMs:0.###}",
            $"total_scrollviewer_setoffset_calls={result.TotalScrollViewerSetOffsetCalls}",
            $"total_scrollviewer_setoffset_noops={result.TotalScrollViewerSetOffsetNoOpCalls}",
            $"total_scrollviewer_vertical_valuechanged_calls={result.TotalScrollViewerVerticalValueChangedCalls}",
            $"total_scrollviewer_vertical_valuechanged_ms={result.TotalScrollViewerVerticalValueChangedMs:0.###}",
            $"total_scrollviewer_vertical_setoffsets_ms={result.TotalScrollViewerVerticalSetOffsetsMs:0.###}",
            $"total_scrollviewer_measureoverride_ms={result.TotalScrollViewerMeasureOverrideMs:0.###}",
            $"total_scrollviewer_arrangeoverride_ms={result.TotalScrollViewerArrangeOverrideMs:0.###}",
            $"total_scrollviewer_resolvebars_measure_ms={result.TotalScrollViewerResolveBarsMeasureMs:0.###}",
            $"total_scrollviewer_resolvebars_measure_iterations={result.TotalScrollViewerResolveBarsMeasureIterations}",
            $"total_scrollviewer_resolvebars_measure_horizontal_flips={result.TotalScrollViewerResolveBarsMeasureHorizontalFlips}",
            $"total_scrollviewer_resolvebars_measure_vertical_flips={result.TotalScrollViewerResolveBarsMeasureVerticalFlips}",
            $"total_scrollviewer_resolvebars_arrange_ms={result.TotalScrollViewerResolveBarsArrangeMs:0.###}",
            $"total_scrollviewer_resolvebars_arrange_iterations={result.TotalScrollViewerResolveBarsArrangeIterations}",
            $"total_scrollviewer_measurecontent_ms={result.TotalScrollViewerMeasureContentMs:0.###}",
            $"total_scrollviewer_measurecontent_calls={result.TotalScrollViewerMeasureContentCalls}",
            $"total_scrollviewer_updatebars_ms={result.TotalScrollViewerUpdateBarsMs:0.###}",
            $"total_scrollviewer_updatebars_calls={result.TotalScrollViewerUpdateBarsCalls}",
            $"total_scrollbar_sync_track_state_ms={result.TotalScrollBarSyncTrackStateMs:0.###}",
            $"total_scrollbar_refresh_track_layout_ms={result.TotalScrollBarRefreshTrackLayoutMs:0.###}",
            $"total_track_refresh_layout_ms={result.TotalTrackRefreshLayoutMs:0.###}",
            $"total_track_refresh_arrange_ms={result.TotalTrackRefreshArrangeMs:0.###}",
            $"total_track_refresh_dirtybounds_ms={result.TotalTrackRefreshDirtyBoundsMs:0.###}",
            $"total_track_refresh_visual_invalidation_ms={result.TotalTrackRefreshVisualInvalidationMs:0.###}",
            $"total_track_refresh_calls={result.TotalTrackRefreshCalls}",
            $"total_thumb_handle_pointer_move_ms={result.TotalThumbHandlePointerMoveMs:0.###}",
            $"total_canvasview_update_telemetry_ms={result.TotalCanvasViewUpdateTelemetryMs:0.###}",
            $"total_canvasview_sync_overlay_layout_ms={result.TotalCanvasViewSyncOverlayLayoutMs:0.###}",
            $"total_canvas_measure_ms={result.TotalCanvasMeasureMs:0.###}",
            $"total_canvas_arrange_ms={result.TotalCanvasArrangeMs:0.###}",
            $"total_grid_measure_ms={result.TotalGridMeasureMs:0.###}",
            $"total_grid_arrange_ms={result.TotalGridArrangeMs:0.###}",
            $"total_dirty_roots={result.TotalDirtyRoots}",
            $"hottest_step={result.HottestStepLabel}",
            $"hottest_step_detail={result.HottestStepDetail}"
        ]);
    }

    private static string BuildComparison(VariantRunResult large, VariantRunResult small)
    {
        return $"small-minus-large: updateMs={(small.TotalUpdateMs - large.TotalUpdateMs):0.###}, layoutMs={(small.TotalLayoutMs - large.TotalLayoutMs):0.###}, scrollViewerMeasureOverrideMs={(small.TotalScrollViewerMeasureOverrideMs - large.TotalScrollViewerMeasureOverrideMs):0.###}, scrollViewerResolveBarsMeasureMs={(small.TotalScrollViewerResolveBarsMeasureMs - large.TotalScrollViewerResolveBarsMeasureMs):0.###}, scrollViewerResolveBarsMeasureHorizontalFlips={(small.TotalScrollViewerResolveBarsMeasureHorizontalFlips - large.TotalScrollViewerResolveBarsMeasureHorizontalFlips)}, scrollViewerResolveBarsMeasureVerticalFlips={(small.TotalScrollViewerResolveBarsMeasureVerticalFlips - large.TotalScrollViewerResolveBarsMeasureVerticalFlips)}, scrollViewerMeasureContentMs={(small.TotalScrollViewerMeasureContentMs - large.TotalScrollViewerMeasureContentMs):0.###}, scrollViewerMeasureContentCalls={(small.TotalScrollViewerMeasureContentCalls - large.TotalScrollViewerMeasureContentCalls)}, scrollViewerArrangeOverrideMs={(small.TotalScrollViewerArrangeOverrideMs - large.TotalScrollViewerArrangeOverrideMs):0.###}, scrollViewerUpdateBarsMs={(small.TotalScrollViewerUpdateBarsMs - large.TotalScrollViewerUpdateBarsMs):0.###}, trackRefreshLayoutMs={(small.TotalTrackRefreshLayoutMs - large.TotalTrackRefreshLayoutMs):0.###}, scrollBarSyncTrackStateMs={(small.TotalScrollBarSyncTrackStateMs - large.TotalScrollBarSyncTrackStateMs):0.###}";
    }

    private static string BuildInference(VariantRunResult large, VariantRunResult small)
    {
        return "The fix stabilizes ScrollViewer's Auto-scrollbar measure loop. ScrollBar.SyncTrackState and Track.RefreshLayoutForStateMutation remain negligible, horizontal auto-bar flips stay eliminated, and small-viewport content remeasure volume stays bounded instead of exploding into the previous oscillation pattern.";
    }

    private static string FormatStep(VariantStepMetrics step)
    {
        return $"step={step.StepIndex:000} pointer=({step.Pointer.X:0.##},{step.Pointer.Y:0.##}) updateMs={step.LastUpdateMs:0.###} inputMs={step.InputPhaseMs:0.###} layoutMs={step.LayoutPhaseMs:0.###} animationMs={step.AnimationPhaseMs:0.###} renderSchedulingMs={step.RenderSchedulingMs:0.###} visualUpdateMs={step.VisualUpdateMs:0.###} resolvePath={step.PointerMove.PointerResolvePath} setOffsetCalls={step.ScrollMetrics.SetOffsetCalls} setOffsetNoOps={step.ScrollMetrics.SetOffsetNoOpCalls} verticalValueChangedCalls={step.ScrollValueChanged.VerticalValueChangedCallCount} verticalValueChangedMs={step.ScrollValueChanged.VerticalValueChangedMilliseconds:0.###} verticalSetOffsetsMs={step.ScrollValueChanged.VerticalValueChangedSetOffsetsMilliseconds:0.###} scrollViewerMeasureMs={step.ScrollLayout.MeasureOverrideMilliseconds:0.###} scrollViewerArrangeMs={step.ScrollLayout.ArrangeOverrideMilliseconds:0.###} scrollViewerResolveMeasureMs={step.ScrollLayout.ResolveBarsAndMeasureContentMilliseconds:0.###} scrollViewerResolveMeasureIterations={step.ScrollLayout.ResolveBarsAndMeasureContentIterationCount} scrollViewerMeasureContentMs={step.ScrollLayout.MeasureContentMilliseconds:0.###} scrollViewerMeasureContentCalls={step.ScrollLayout.MeasureContentCallCount} scrollViewerUpdateBarsMs={step.ScrollLayout.UpdateScrollBarsMilliseconds:0.###} scrollBarSyncTrackMs={step.ScrollBarTelemetry.SyncTrackStateMilliseconds:0.###} scrollBarRefreshTrackLayoutMs={step.ScrollBarTelemetry.RefreshTrackLayoutMilliseconds:0.###} trackRefreshMs={step.TrackTelemetry.RefreshLayoutForStateMutationMilliseconds:0.###} trackArrangeMs={step.TrackTelemetry.RefreshLayoutArrangeMilliseconds:0.###} trackDirtyBoundsMs={step.TrackTelemetry.RefreshLayoutDirtyBoundsMilliseconds:0.###} trackVisualInvalidateMs={step.TrackTelemetry.RefreshLayoutVisualInvalidationMilliseconds:0.###} trackRefreshCalls={step.TrackTelemetry.RefreshLayoutForStateMutationCallCount} thumbMoveMs={step.ThumbTelemetry.HandlePointerMoveMilliseconds:0.###} canvasViewTelemetryMs={step.CanvasViewTelemetry.UpdateTelemetryMilliseconds:0.###} syncOverlayMs={step.CanvasViewTelemetry.SyncOverlayLayoutMilliseconds:0.###} canvasMeasureMs={step.CanvasTelemetry.MeasureMilliseconds:0.###} canvasArrangeMs={step.CanvasTelemetry.ArrangeMilliseconds:0.###} gridMeasureMs={step.GridMeasureMs:0.###} gridArrangeMs={step.GridArrangeMs:0.###} dirtyRoots={step.DirtyRootCount} effectiveRenderSource={step.EffectiveRenderSourceType}#{step.EffectiveRenderSourceName}";
    }

    private static void RunFrame(UiRoot uiRoot, int viewportWidth, int viewportHeight, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, viewportWidth, viewportHeight));
    }

    private static void PrimeRetainedRenderStateForDiagnostics(UiRoot uiRoot, int viewportWidth, int viewportHeight)
    {
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, viewportWidth, viewportHeight));
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root, Func<TElement, bool>? predicate = null)
        where TElement : UIElement
    {
        if (root is TElement match && (predicate == null || predicate(match)))
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild(child, predicate);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static string GetDiagnosticsLogPath(string fileNameWithoutExtension)
    {
        return Path.Combine(FindRepositoryRoot(), "artifacts", "diagnostics", fileNameWithoutExtension + ".txt");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (current.EnumerateFiles("InkkSlinger.sln").Any())
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test base directory.");
    }

    private static void LoadRootAppResources()
    {
        var appPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "App.xml"));
        Assert.True(File.Exists(appPath), $"Expected App.xml to exist at '{appPath}'.");
        XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);
    }

    private static Dictionary<object, object> CaptureApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
    }

    private static void RestoreApplicationResources(Dictionary<object, object> snapshot)
    {
        TestApplicationResources.Restore(snapshot);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    private static void InvokeCanvasViewMethod(CanvasView view, string methodName, params object?[] arguments)
    {
        var method = typeof(CanvasView).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(view, arguments);
    }

    private readonly record struct VariantDefinition(string Name, int ViewportWidth, int ViewportHeight);

    private readonly record struct VariantStepMetrics(
        int StepIndex,
        Vector2 Pointer,
        double LastUpdateMs,
        double InputPhaseMs,
        double LayoutPhaseMs,
        double AnimationPhaseMs,
        double RenderSchedulingMs,
        double VisualUpdateMs,
        UiPointerMoveTelemetrySnapshot PointerMove,
        ScrollViewerScrollMetricsSnapshot ScrollMetrics,
        ScrollViewerValueChangedTelemetrySnapshot ScrollValueChanged,
        ScrollViewerLayoutTelemetrySnapshot ScrollLayout,
        ScrollBarThumbDragTelemetrySnapshot ScrollBarTelemetry,
        TrackThumbTravelTelemetrySnapshot TrackTelemetry,
        ThumbDragTelemetrySnapshot ThumbTelemetry,
        CanvasViewDiagnosticsSnapshot CanvasViewTelemetry,
        CanvasTelemetrySnapshot CanvasTelemetry,
        double GridMeasureMs,
        double GridArrangeMs,
        int DirtyRootCount,
        string EffectiveRenderSourceType,
        string EffectiveRenderSourceName);

    private readonly record struct VariantRunResult(
        string Name,
        int ViewportWidth,
        int ViewportHeight,
        bool HasVisibleHorizontalBar,
        bool HasVisibleVerticalBar,
        IReadOnlyList<VariantStepMetrics> Steps,
        double TotalUpdateMs,
        double TotalInputMs,
        double TotalLayoutMs,
        double TotalAnimationMs,
        double TotalRenderSchedulingMs,
        double TotalVisualUpdateMs,
        int TotalScrollViewerSetOffsetCalls,
        int TotalScrollViewerSetOffsetNoOpCalls,
        int TotalScrollViewerVerticalValueChangedCalls,
        double TotalScrollViewerVerticalValueChangedMs,
        double TotalScrollViewerVerticalSetOffsetsMs,
        double TotalScrollViewerMeasureOverrideMs,
        double TotalScrollViewerArrangeOverrideMs,
        double TotalScrollViewerResolveBarsMeasureMs,
        int TotalScrollViewerResolveBarsMeasureIterations,
        int TotalScrollViewerResolveBarsMeasureHorizontalFlips,
        int TotalScrollViewerResolveBarsMeasureVerticalFlips,
        double TotalScrollViewerResolveBarsArrangeMs,
        int TotalScrollViewerResolveBarsArrangeIterations,
        double TotalScrollViewerMeasureContentMs,
        int TotalScrollViewerMeasureContentCalls,
        double TotalScrollViewerUpdateBarsMs,
        int TotalScrollViewerUpdateBarsCalls,
        double TotalScrollBarSyncTrackStateMs,
        double TotalScrollBarRefreshTrackLayoutMs,
        double TotalTrackRefreshLayoutMs,
        double TotalTrackRefreshArrangeMs,
        double TotalTrackRefreshDirtyBoundsMs,
        double TotalTrackRefreshVisualInvalidationMs,
        int TotalTrackRefreshCalls,
        double TotalThumbHandlePointerMoveMs,
        double TotalCanvasViewUpdateTelemetryMs,
        double TotalCanvasViewSyncOverlayLayoutMs,
        double TotalCanvasMeasureMs,
        double TotalCanvasArrangeMs,
        double TotalGridMeasureMs,
        double TotalGridArrangeMs,
        int TotalDirtyRoots,
        string HottestStepLabel,
        string HottestStepDetail);
}