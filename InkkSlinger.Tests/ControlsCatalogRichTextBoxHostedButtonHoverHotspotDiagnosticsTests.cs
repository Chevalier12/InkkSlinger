using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogRichTextBoxHostedButtonHoverHotspotDiagnosticsTests
{
    private const int ViewportWidth = 1400;
    private const int ViewportHeight = 900;

    [Fact]
    public void ControlsCatalog_RichTextBoxEmbeddedUiHostedButtonHover_WritesHotspotDiagnosticsLog()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();
            Dispatcher.ResetForTests();
            AnimationManager.Current.ResetForTests();
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();
            VisualTreeHelper.ResetInstrumentationForTests();
            Freezable.ResetTelemetryForTests();
            UIElement.ResetFreezableInvalidationBatchTelemetryForTests();

            var catalog = new ControlsCatalogView();
            catalog.ShowControl("RichTextBox");
            var uiRoot = CreateUiRoot(catalog);
            RunFrame(uiRoot, 16);
            RunFrame(uiRoot, 16);
            RunFrame(uiRoot, 16);

            var view = GetSelectedRichTextBoxView(catalog);
            var embeddedUiButton = FindFirstVisualChild<Button>(
                                       view,
                                       static button => string.Equals(button.GetContentText(), "Embedded UI", StringComparison.Ordinal))
                                   ?? throw new InvalidOperationException("Could not find Embedded UI preset button.");
            InvokeButtonClick(embeddedUiButton);
            RunFrame(uiRoot, 16);
            RunFrame(uiRoot, 16);

            var editor = Assert.IsType<RichTextBox>(view.FindName("Editor"));
            var inlineButton = FindFirstVisualChild<Button>(
                                   view,
                                   static button => string.Equals(button.GetContentText(), "Inline", StringComparison.Ordinal))
                               ?? throw new InvalidOperationException("Could not find inline hosted button.");
            var blockButton = FindFirstVisualChild<Button>(
                                  view,
                                  static button => string.Equals(button.GetContentText(), "Block", StringComparison.Ordinal))
                              ?? throw new InvalidOperationException("Could not find block hosted button.");

            Assert.True(inlineButton.LayoutSlot.Width > 0f && inlineButton.LayoutSlot.Height > 0f);
            Assert.True(blockButton.LayoutSlot.Width > 0f && blockButton.LayoutSlot.Height > 0f);

            var safeEditorPoint = new Vector2(editor.LayoutSlot.X + 24f, editor.LayoutSlot.Y + 24f);
            PrimeRetainedRenderStateForDiagnostics(uiRoot);
            AnimationManager.Current.ResetTelemetryForTests();
            VisualTreeHelper.ResetInstrumentationForTests();
            Freezable.ResetTelemetryForTests();
            UIElement.ResetFreezableInvalidationBatchTelemetryForTests();
            uiRoot.CompleteDrawStateForTests();
            uiRoot.ResetDirtyStateForTests();
            uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight));

            var logPath = GetDiagnosticsLogPath("controls-catalog-richtextbox-embedded-ui-hosted-button-hover-hotspot");
            var lines = new List<string>
            {
                "scenario=Controls Catalog RichTextBox embedded UI hosted button hover hotspot diagnostics",
                $"timestamp_utc={DateTime.UtcNow:O}",
                $"log_path={logPath}",
                "step_1=open Controls Catalog",
                "step_2=open RichTextBox view",
                "step_3=click Embedded UI preset",
                "step_4=move pointer between editor background, inline hosted button, and block hosted button",
                $"editor_slot={FormatRect(editor.LayoutSlot)}",
                $"inline_button_slot={FormatRect(inlineButton.LayoutSlot)}",
                $"block_button_slot={FormatRect(blockButton.LayoutSlot)}"
            };

            var stepMetrics = new List<HoverStepMetrics>();
            var sequence = new List<(string Label, Vector2 Point)>
            {
                ("editor-bg", safeEditorPoint),
                ("inline-enter", GetCenter(inlineButton.LayoutSlot)),
                ("editor-bg", safeEditorPoint),
                ("block-enter", GetCenter(blockButton.LayoutSlot)),
                ("editor-bg", safeEditorPoint),
                ("inline-enter", GetCenter(inlineButton.LayoutSlot)),
                ("block-enter", GetCenter(blockButton.LayoutSlot)),
                ("editor-bg", safeEditorPoint)
            };

            for (var pass = 0; pass < 3; pass++)
            {
                for (var index = 0; index < sequence.Count; index++)
                {
                    var step = sequence[index];
                    stepMetrics.Add(RunHoverStep(uiRoot, editor, step.Point, $"pass-{pass + 1}:{step.Label}", (pass * sequence.Count) + index));
                }
            }

            var summary = Summarize(stepMetrics);
            lines.Add(string.Empty);
            lines.Add("summary:");
            lines.Add($"total_steps={stepMetrics.Count}");
            lines.Add($"total_hit_tests={summary.TotalHitTests}");
            lines.Add($"total_routed_events={summary.TotalRoutedEvents}");
            lines.Add($"total_hover_update_ms={summary.TotalHoverUpdateMilliseconds:0.###}");
            lines.Add($"total_pointer_resolve_ms={summary.TotalPointerResolveMilliseconds:0.###}");
            lines.Add($"total_begin_storyboards={summary.TotalBeginStoryboardCalls}");
            lines.Add($"total_storyboard_starts={summary.TotalStoryboardStarts}");
            lines.Add($"total_lane_applications={summary.TotalLaneApplications}");
            lines.Add($"total_sink_value_sets={summary.TotalSinkValueSets}");
            lines.Add($"total_begin_storyboard_ms={summary.TotalBeginStoryboardMilliseconds:0.###}");
            lines.Add($"total_storyboard_start_ms={summary.TotalStoryboardStartMilliseconds:0.###}");
            lines.Add($"total_storyboard_update_ms={summary.TotalStoryboardUpdateMilliseconds:0.###}");
            lines.Add($"total_compose_ms={summary.TotalComposeMilliseconds:0.###}");
            lines.Add($"total_compose_collect_ms={summary.TotalComposeCollectMilliseconds:0.###}");
            lines.Add($"total_compose_sort_ms={summary.TotalComposeSortMilliseconds:0.###}");
            lines.Add($"total_compose_merge_ms={summary.TotalComposeMergeMilliseconds:0.###}");
            lines.Add($"total_compose_apply_ms={summary.TotalComposeApplyMilliseconds:0.###}");
            lines.Add($"total_compose_batch_begin_ms={summary.TotalComposeBatchBeginMilliseconds:0.###}");
            lines.Add($"total_compose_batch_end_ms={summary.TotalComposeBatchEndMilliseconds:0.###}");
            lines.Add($"total_sink_setvalue_ms={summary.TotalSinkSetValueMilliseconds:0.###}");
            lines.Add($"total_cleanup_completed_ms={summary.TotalCleanupCompletedMilliseconds:0.###}");
            lines.Add($"total_freezable_onchanged_ms={summary.TotalFreezableOnChangedMilliseconds:0.###}");
            lines.Add($"total_freezable_end_batch_ms={summary.TotalFreezableEndBatchMilliseconds:0.###}");
            lines.Add($"total_freezable_batch_flush_ms={summary.TotalFreezableBatchFlushMilliseconds:0.###}");
            lines.Add($"total_freezable_batch_flushes={summary.TotalFreezableBatchFlushes}");
            lines.Add($"total_freezable_batch_flush_targets={summary.TotalFreezableBatchFlushTargets}");
            lines.Add($"max_freezable_batch_pending_targets={summary.MaxFreezableBatchPendingTargets}");
            lines.Add($"total_dp_sink_sets={summary.TotalDependencyPropertySinkSetCount}");
            lines.Add($"total_dp_sink_ms={summary.TotalDependencyPropertySinkSetMilliseconds:0.###}");
            lines.Add($"total_clr_sink_sets={summary.TotalClrPropertySinkSetCount}");
            lines.Add($"total_clr_sink_ms={summary.TotalClrPropertySinkSetMilliseconds:0.###}");
            lines.Add($"total_render_invalidations={summary.TotalRenderInvalidations}");
            lines.Add($"total_measure_invalidations={summary.TotalMeasureInvalidations}");
            lines.Add($"total_arrange_invalidations={summary.TotalArrangeInvalidations}");
            lines.Add($"total_partial_dirty_steps={summary.TotalPartialDirtySteps}");
            lines.Add($"total_full_redraw_steps={summary.TotalFullRedrawSteps}");
            lines.Add($"full_dirty_initial_state_count={summary.FullDirtyInitialStateCount}");
            lines.Add($"full_dirty_viewport_change_count={summary.FullDirtyViewportChangeCount}");
            lines.Add($"full_dirty_surface_reset_count={summary.FullDirtySurfaceResetCount}");
            lines.Add($"full_dirty_visual_structure_change_count={summary.FullDirtyVisualStructureChangeCount}");
            lines.Add($"full_dirty_retained_rebuild_count={summary.FullDirtyRetainedRebuildCount}");
            lines.Add($"full_dirty_detached_visual_count={summary.FullDirtyDetachedVisualCount}");
            lines.Add($"max_dirty_coverage={summary.MaxDirtyCoverage:0.###}");
            lines.Add($"max_traversal_drawn={summary.MaxTraversalDrawn}");
            lines.Add($"max_non_editor_drawn={summary.MaxNonEditorDrawn}");
            lines.Add($"max_active_storyboards={summary.MaxActiveStoryboards}");
            lines.Add($"max_active_lanes={summary.MaxActiveLanes}");
            lines.Add($"max_active_storyboard_entries={summary.MaxActiveStoryboardEntries}");
            lines.Add($"hottest_freezable_onchanged_type={summary.HottestFreezableOnChangedType}:{summary.HottestFreezableOnChangedMilliseconds:0.###}");
            lines.Add($"hottest_freezable_endbatch_type={summary.HottestFreezableEndBatchType}:{summary.HottestFreezableEndBatchMilliseconds:0.###}");
            lines.Add($"hottest_setvalue_paths={summary.HottestSetValuePaths}");
            lines.Add($"hotspot_inference={summary.HotspotInference}");
            lines.Add($"hottest_step={summary.HottestStepLabel}");
            lines.Add($"hottest_step_detail={summary.HottestStepDetail}");

            lines.Add(string.Empty);
            lines.Add("steps:");
            lines.AddRange(stepMetrics.Select(FormatStep));

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllLines(logPath, lines);

            Assert.True(File.Exists(logPath));
        }
        finally
        {
            RestoreApplicationResources(backup);
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();
        }
    }

    private static HoverStepMetrics RunHoverStep(UiRoot uiRoot, RichTextBox editor, Vector2 pointer, string label, int stepIndex)
    {
        uiRoot.ClearDirtyBoundsEventTraceForTests();
        var beforeAnimation = AnimationManager.Current.GetTelemetrySnapshotForTests();
        var beforeHitTest = VisualTreeHelper.GetInstrumentationSnapshotForTests();
        var beforeVisualTree = uiRoot.GetVisualTreeMetricsSnapshot();
        var beforeRender = uiRoot.GetRenderTelemetrySnapshotForTests();
        var beforeFreezable = Freezable.GetTelemetrySnapshotForTests();
        var beforeInvalidationBatch = UIElement.GetFreezableInvalidationBatchSnapshotForTests();
        AnimationValueSink.ResetTelemetryForTests();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer));
        var pointerMove = uiRoot.GetPointerMoveTelemetrySnapshotForTests();
        RunFrame(uiRoot, 32 + (stepIndex * 4));

        var afterAnimation = AnimationManager.Current.GetTelemetrySnapshotForTests();
        var afterHitTest = VisualTreeHelper.GetInstrumentationSnapshotForTests();
        var afterVisualTree = uiRoot.GetVisualTreeMetricsSnapshot();
        var afterRender = uiRoot.GetRenderTelemetrySnapshotForTests();
        var afterFreezable = Freezable.GetTelemetrySnapshotForTests();
        var afterInvalidationBatch = UIElement.GetFreezableInvalidationBatchSnapshotForTests();
        var sinkTelemetry = AnimationValueSink.GetTelemetrySnapshotForTests();
        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();
        var dirtyCoverage = uiRoot.GetDirtyCoverageForTests();
        var partialDirty = uiRoot.WouldUsePartialDirtyRedrawForTests();
        var largestDirtyRegion = dirtyRegions.OrderByDescending(static region => region.Width * region.Height).FirstOrDefault();
        var traversalClip = dirtyRegions.Count > 0
            ? largestDirtyRegion
            : new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight);
        var traversalMetrics = uiRoot.GetRetainedTraversalMetricsForClipForTests(traversalClip);
        var drawOrder = uiRoot.GetRetainedDrawOrderForClipForTests(traversalClip);
        var editorSubtreeDrawn = drawOrder.Count(visual => IsDescendantOrSelf(editor, visual));
        var nonEditorSubtreeDrawn = drawOrder.Count - editorSubtreeDrawn;
        var dirtyTrace = string.Join(" | ", uiRoot.GetDirtyBoundsEventTraceForTests());

        var step = new HoverStepMetrics(
            stepIndex,
            label,
            pointer,
            pointerMove,
            sinkTelemetry,
            afterAnimation.BeginStoryboardCallCount - beforeAnimation.BeginStoryboardCallCount,
            afterAnimation.StoryboardStartCount - beforeAnimation.StoryboardStartCount,
            afterAnimation.BeginStoryboardMilliseconds - beforeAnimation.BeginStoryboardMilliseconds,
            afterAnimation.StoryboardStartMilliseconds - beforeAnimation.StoryboardStartMilliseconds,
            afterAnimation.StoryboardUpdateMilliseconds - beforeAnimation.StoryboardUpdateMilliseconds,
            afterAnimation.ComposeMilliseconds - beforeAnimation.ComposeMilliseconds,
            afterAnimation.ComposeCollectMilliseconds - beforeAnimation.ComposeCollectMilliseconds,
            afterAnimation.ComposeSortMilliseconds - beforeAnimation.ComposeSortMilliseconds,
            afterAnimation.ComposeMergeMilliseconds - beforeAnimation.ComposeMergeMilliseconds,
            afterAnimation.ComposeApplyMilliseconds - beforeAnimation.ComposeApplyMilliseconds,
            afterAnimation.ComposeBatchBeginMilliseconds - beforeAnimation.ComposeBatchBeginMilliseconds,
            afterAnimation.ComposeBatchEndMilliseconds - beforeAnimation.ComposeBatchEndMilliseconds,
            afterAnimation.SinkSetValueMilliseconds - beforeAnimation.SinkSetValueMilliseconds,
            afterAnimation.CleanupCompletedMilliseconds - beforeAnimation.CleanupCompletedMilliseconds,
            afterFreezable.OnChangedMilliseconds - beforeFreezable.OnChangedMilliseconds,
            afterFreezable.EndBatchMilliseconds - beforeFreezable.EndBatchMilliseconds,
            afterInvalidationBatch.FlushMilliseconds - beforeInvalidationBatch.FlushMilliseconds,
            afterInvalidationBatch.FlushCount - beforeInvalidationBatch.FlushCount,
            afterInvalidationBatch.FlushTargetCount - beforeInvalidationBatch.FlushTargetCount,
            afterInvalidationBatch.QueuedTargetCount - beforeInvalidationBatch.QueuedTargetCount,
            afterInvalidationBatch.MaxPendingTargetCount,
            afterFreezable.HottestOnChangedType,
            afterFreezable.HottestOnChangedMilliseconds,
            afterFreezable.HottestEndBatchType,
            afterFreezable.HottestEndBatchMilliseconds,
            afterAnimation.HottestSetValuePathSummary,
            afterAnimation.ActiveStoryboardCount,
            afterAnimation.ActiveLaneCount,
            afterAnimation.ActiveStoryboardEntryCount,
            afterAnimation.ComposePassCount - beforeAnimation.ComposePassCount,
            afterAnimation.LaneApplicationCount - beforeAnimation.LaneApplicationCount,
            afterAnimation.SinkValueSetCount - beforeAnimation.SinkValueSetCount,
            afterAnimation.ClearedLaneCount - beforeAnimation.ClearedLaneCount,
            afterHitTest.ItemsPresenterNeighborProbes - beforeHitTest.ItemsPresenterNeighborProbes,
            afterHitTest.ItemsPresenterFullFallbackScans - beforeHitTest.ItemsPresenterFullFallbackScans,
            afterHitTest.LegacyEnumerableFallbacks - beforeHitTest.LegacyEnumerableFallbacks,
            afterHitTest.MonotonicPanelFastPathCount - beforeHitTest.MonotonicPanelFastPathCount,
            afterVisualTree.MeasureInvalidationCount - beforeVisualTree.MeasureInvalidationCount,
            afterVisualTree.ArrangeInvalidationCount - beforeVisualTree.ArrangeInvalidationCount,
            afterVisualTree.RenderInvalidationCount - beforeVisualTree.RenderInvalidationCount,
            Math.Max(0, afterRender.DirtyRootCount - beforeRender.DirtyRootCount),
            Math.Max(0, afterRender.RetainedTraversalCount - beforeRender.RetainedTraversalCount),
            uiRoot.IsFullDirtyForTests(),
            Math.Max(0, afterRender.FullDirtyInitialStateCount - beforeRender.FullDirtyInitialStateCount),
            Math.Max(0, afterRender.FullDirtyViewportChangeCount - beforeRender.FullDirtyViewportChangeCount),
            Math.Max(0, afterRender.FullDirtySurfaceResetCount - beforeRender.FullDirtySurfaceResetCount),
            Math.Max(0, afterRender.FullDirtyVisualStructureChangeCount - beforeRender.FullDirtyVisualStructureChangeCount),
            Math.Max(0, afterRender.FullDirtyRetainedRebuildCount - beforeRender.FullDirtyRetainedRebuildCount),
            Math.Max(0, afterRender.FullDirtyDetachedVisualCount - beforeRender.FullDirtyDetachedVisualCount),
            dirtyRegions.Count,
            dirtyCoverage,
            partialDirty,
            FormatRect(traversalClip),
            traversalMetrics.NodesVisited,
            traversalMetrics.NodesDrawn,
            editorSubtreeDrawn,
            nonEditorSubtreeDrawn,
            dirtyTrace);

        SimulatePostDrawCleanup(uiRoot);
        return step;
    }

    private static HoverSummary Summarize(IReadOnlyList<HoverStepMetrics> steps)
    {
        var hottest = steps
            .OrderByDescending(static step => step.FreezableEndBatchMilliseconds)
            .ThenByDescending(static step => step.ComposeApplyMilliseconds)
            .ThenByDescending(static step => step.PointerMove.HoverUpdateMilliseconds)
            .First();

        var totalBeginStoryboardCalls = steps.Sum(static step => step.BeginStoryboardCalls);
        var totalLaneApplications = steps.Sum(static step => step.LaneApplicationCount);
        var totalSinkValueSets = steps.Sum(static step => step.SinkValueSetCount);
        var totalComposeApplyMs = steps.Sum(static step => step.ComposeApplyMilliseconds);
        var totalFreezableEndBatchMs = steps.Sum(static step => step.FreezableEndBatchMilliseconds);
        var totalFreezableBatchFlushMs = steps.Sum(static step => step.FreezableBatchFlushMilliseconds);
        var hottestSetValuePaths = string.Join(
            " | ",
            steps.Select(static step => step.HottestSetValuePathSummary)
                .Where(static summary => !string.IsNullOrWhiteSpace(summary) && !string.Equals(summary, "none", StringComparison.Ordinal))
                .GroupBy(static summary => summary)
                .OrderByDescending(static group => group.Count())
                .Take(3)
                .Select(static group => group.Key));

        var hotspotInference =
            totalBeginStoryboardCalls > 0 &&
            totalLaneApplications > steps.Count &&
            totalSinkValueSets > steps.Count &&
            totalFreezableEndBatchMs >= totalComposeApplyMs &&
            totalFreezableBatchFlushMs > 0d
                ? "Exact hotspot candidate: hosted-button hover is dominated by AnimationManager.ApplyPendingWrites -> Freezable.EndBatchUpdate() flushes on animated button transforms/effects."
                : steps.Any(static step => step.IsFullDirty || !step.WouldUsePartialDirtyRedraw)
                    ? "Exact hotspot candidate: hosted-button hover is promoting retained rendering to a broad redraw instead of staying inside the editor subtree."
                    : steps.Any(static step => step.PointerMove.HoverUpdateMilliseconds > step.ComposeApplyMilliseconds &&
                                               step.PointerMove.PointerTargetResolveMilliseconds > 0.5d)
                        ? "Exact hotspot candidate: hosted-button hover spends more time in pointer target resolution / hover update than in storyboard application."
                        : "Logs did not isolate the hosted-button hover hotspot yet; add narrower instrumentation.";

        return new HoverSummary(
            steps.Sum(static step => step.PointerMove.HitTestCount),
            steps.Sum(static step => step.PointerMove.RoutedEventCount),
            steps.Sum(static step => step.PointerMove.HoverUpdateMilliseconds),
            steps.Sum(static step => step.PointerMove.PointerTargetResolveMilliseconds),
            totalBeginStoryboardCalls,
            steps.Sum(static step => step.StoryboardStarts),
            totalLaneApplications,
            totalSinkValueSets,
            steps.Sum(static step => step.BeginStoryboardMilliseconds),
            steps.Sum(static step => step.StoryboardStartMilliseconds),
            steps.Sum(static step => step.StoryboardUpdateMilliseconds),
            steps.Sum(static step => step.ComposeMilliseconds),
            steps.Sum(static step => step.ComposeCollectMilliseconds),
            steps.Sum(static step => step.ComposeSortMilliseconds),
            steps.Sum(static step => step.ComposeMergeMilliseconds),
            totalComposeApplyMs,
            steps.Sum(static step => step.ComposeBatchBeginMilliseconds),
            steps.Sum(static step => step.ComposeBatchEndMilliseconds),
            steps.Sum(static step => step.SinkSetValueMilliseconds),
            steps.Sum(static step => step.CleanupCompletedMilliseconds),
            steps.Sum(static step => step.FreezableOnChangedMilliseconds),
            totalFreezableEndBatchMs,
            totalFreezableBatchFlushMs,
            steps.Sum(static step => step.FreezableBatchFlushCount),
            steps.Sum(static step => step.FreezableBatchFlushTargetCount),
            steps.Max(static step => step.FreezableBatchMaxPendingTargetCount),
            steps.Sum(static step => step.SinkTelemetry.DependencyPropertySetValueCount),
            steps.Sum(static step => step.SinkTelemetry.DependencyPropertySetValueMilliseconds),
            steps.Sum(static step => step.SinkTelemetry.ClrPropertySetValueCount),
            steps.Sum(static step => step.SinkTelemetry.ClrPropertySetValueMilliseconds),
            steps.Sum(static step => step.RenderInvalidationCount),
            steps.Sum(static step => step.MeasureInvalidationCount),
            steps.Sum(static step => step.ArrangeInvalidationCount),
            steps.Count(static step => step.WouldUsePartialDirtyRedraw),
            steps.Count(static step => !step.WouldUsePartialDirtyRedraw || step.IsFullDirty),
            steps.Sum(static step => step.FullDirtyInitialStateCount),
            steps.Sum(static step => step.FullDirtyViewportChangeCount),
            steps.Sum(static step => step.FullDirtySurfaceResetCount),
            steps.Sum(static step => step.FullDirtyVisualStructureChangeCount),
            steps.Sum(static step => step.FullDirtyRetainedRebuildCount),
            steps.Sum(static step => step.FullDirtyDetachedVisualCount),
            steps.Max(static step => step.DirtyCoverage),
            steps.Max(static step => step.TraversalNodesDrawn),
            steps.Max(static step => step.NonEditorSubtreeDrawn),
            steps.Max(static step => step.ActiveStoryboardCount),
            steps.Max(static step => step.ActiveLaneCount),
            steps.Max(static step => step.ActiveStoryboardEntryCount),
            steps.OrderByDescending(static step => step.HottestFreezableOnChangedMilliseconds).First().HottestFreezableOnChangedType,
            steps.Max(static step => step.HottestFreezableOnChangedMilliseconds),
            steps.OrderByDescending(static step => step.HottestFreezableEndBatchMilliseconds).First().HottestFreezableEndBatchType,
            steps.Max(static step => step.HottestFreezableEndBatchMilliseconds),
            hottestSetValuePaths,
            hotspotInference,
            hottest.Label,
            $"resolvePath={hottest.PointerMove.PointerResolvePath}, hoverMs={hottest.PointerMove.HoverUpdateMilliseconds:0.###}, resolveMs={hottest.PointerMove.PointerTargetResolveMilliseconds:0.###}, beginStoryboards={hottest.BeginStoryboardCalls}, laneApplications={hottest.LaneApplicationCount}, sinkValueSets={hottest.SinkValueSetCount}, beginMs={hottest.BeginStoryboardMilliseconds:0.###}, startMs={hottest.StoryboardStartMilliseconds:0.###}, updateMs={hottest.StoryboardUpdateMilliseconds:0.###}, composeMs={hottest.ComposeMilliseconds:0.###}, composeApplyMs={hottest.ComposeApplyMilliseconds:0.###}, composeBatchEndMs={hottest.ComposeBatchEndMilliseconds:0.###}, cleanupCompletedMs={hottest.CleanupCompletedMilliseconds:0.###}, freezableOnChangedMs={hottest.FreezableOnChangedMilliseconds:0.###}, freezableEndBatchMs={hottest.FreezableEndBatchMilliseconds:0.###}, batchFlushMs={hottest.FreezableBatchFlushMilliseconds:0.###}, fullDirty={hottest.IsFullDirty}, dirtyCoverage={hottest.DirtyCoverage:0.###}, traversalDrawn={hottest.TraversalNodesDrawn}, nonEditorDrawn={hottest.NonEditorSubtreeDrawn}, hottestEndBatchType={hottest.HottestFreezableEndBatchType}, hottestSetValuePaths={hottest.HottestSetValuePathSummary}");
    }

    private static string FormatStep(HoverStepMetrics step)
    {
        return
            $"step={step.StepIndex:000} label={step.Label} pointer=({step.Pointer.X:0.##},{step.Pointer.Y:0.##}) " +
            $"resolvePath={step.PointerMove.PointerResolvePath} hitTests={step.PointerMove.HitTestCount} routedEvents={step.PointerMove.RoutedEventCount} " +
            $"hoverMs={step.PointerMove.HoverUpdateMilliseconds:0.###} resolveMs={step.PointerMove.PointerTargetResolveMilliseconds:0.###} routeMs={step.PointerMove.PointerRouteMilliseconds:0.###} " +
            $"beginStoryboards={step.BeginStoryboardCalls} storyboardStarts={step.StoryboardStarts} activeStoryboards={step.ActiveStoryboardCount} activeLanes={step.ActiveLaneCount} activeEntries={step.ActiveStoryboardEntryCount} composePasses={step.ComposePassCount} laneApplications={step.LaneApplicationCount} sinkValueSets={step.SinkValueSetCount} clearedLanes={step.ClearedLaneCount} " +
            $"dpSinkSets={step.SinkTelemetry.DependencyPropertySetValueCount} dpSinkMs={step.SinkTelemetry.DependencyPropertySetValueMilliseconds:0.###} clrSinkSets={step.SinkTelemetry.ClrPropertySetValueCount} clrSinkMs={step.SinkTelemetry.ClrPropertySetValueMilliseconds:0.###} " +
            $"beginMs={step.BeginStoryboardMilliseconds:0.###} startMs={step.StoryboardStartMilliseconds:0.###} updateMs={step.StoryboardUpdateMilliseconds:0.###} composeMs={step.ComposeMilliseconds:0.###} composeCollectMs={step.ComposeCollectMilliseconds:0.###} composeSortMs={step.ComposeSortMilliseconds:0.###} composeMergeMs={step.ComposeMergeMilliseconds:0.###} composeApplyMs={step.ComposeApplyMilliseconds:0.###} composeBatchBeginMs={step.ComposeBatchBeginMilliseconds:0.###} composeBatchEndMs={step.ComposeBatchEndMilliseconds:0.###} sinkSetValueMs={step.SinkSetValueMilliseconds:0.###} cleanupCompletedMs={step.CleanupCompletedMilliseconds:0.###} freezableOnChangedMs={step.FreezableOnChangedMilliseconds:0.###} freezableEndBatchMs={step.FreezableEndBatchMilliseconds:0.###} batchFlushMs={step.FreezableBatchFlushMilliseconds:0.###} batchFlushes={step.FreezableBatchFlushCount} batchFlushTargets={step.FreezableBatchFlushTargetCount} queuedTargets={step.FreezableBatchQueuedTargetCount} maxPendingTargets={step.FreezableBatchMaxPendingTargetCount} hottestFreezableOnChanged={step.HottestFreezableOnChangedType}:{step.HottestFreezableOnChangedMilliseconds:0.###} hottestFreezableEndBatch={step.HottestFreezableEndBatchType}:{step.HottestFreezableEndBatchMilliseconds:0.###} hottestSetValuePaths={step.HottestSetValuePathSummary} " +
            $"measureInvalidations={step.MeasureInvalidationCount} arrangeInvalidations={step.ArrangeInvalidationCount} renderInvalidations={step.RenderInvalidationCount} dirtyRoots={step.DirtyRootCount} retainedTraversals={step.RetainedTraversalCount} dirtyRegions={step.DirtyRegionCount} dirtyCoverage={step.DirtyCoverage:0.###} partialDirty={step.WouldUsePartialDirtyRedraw} fullDirty={step.IsFullDirty} fullDirtyInitial={step.FullDirtyInitialStateCount} fullDirtyViewport={step.FullDirtyViewportChangeCount} fullDirtySurfaceReset={step.FullDirtySurfaceResetCount} fullDirtyStructure={step.FullDirtyVisualStructureChangeCount} fullDirtyRetainedRebuild={step.FullDirtyRetainedRebuildCount} fullDirtyDetached={step.FullDirtyDetachedVisualCount} traversalClip={step.TraversalClip} traversalVisited={step.TraversalNodesVisited} traversalDrawn={step.TraversalNodesDrawn} editorDrawn={step.EditorSubtreeDrawn} nonEditorDrawn={step.NonEditorSubtreeDrawn} dirtyBoundsTrace={step.DirtyBoundsTrace}";
    }

    private static UiRoot CreateUiRoot(UIElement content)
    {
        var host = new Grid
        {
            Width = ViewportWidth,
            Height = ViewportHeight
        };

        host.AddChild(content);
        if (content is FrameworkElement frameworkElement)
        {
            frameworkElement.Width = ViewportWidth;
            frameworkElement.Height = ViewportHeight;
        }

        return new UiRoot(host);
    }

    private static void RunFrame(UiRoot uiRoot, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, ViewportWidth, ViewportHeight));
    }

    private static void PrimeRetainedRenderStateForDiagnostics(UiRoot uiRoot)
    {
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.CompleteDrawStateForTests();
    }

    private static void SimulatePostDrawCleanup(UiRoot uiRoot)
    {
        uiRoot.ApplyRenderInvalidationCleanupForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = [],
            ReleasedKeys = [],
            TextInput = [],
            PointerMoved = true,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static RichTextBoxView GetSelectedRichTextBoxView(ControlsCatalogView catalog)
    {
        var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
        return Assert.IsType<RichTextBoxView>(previewHost.Content);
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

    private static bool IsDescendantOrSelf(UIElement ancestor, UIElement element)
    {
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static void InvokeButtonClick(Button button)
    {
        var onClick = typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(onClick);
        onClick!.Invoke(button, null);
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"({rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##})";
    }

    private static string GetDiagnosticsLogPath(string fileNameWithoutExtension)
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "artifacts", "diagnostics", $"{fileNameWithoutExtension}.txt");
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

    private static ResourceSnapshot CaptureApplicationResources()
    {
        var resources = UiApplication.Current.Resources;
        return new ResourceSnapshot(resources.ToList(), resources.MergedDictionaries.ToList());
    }

    private static void RestoreApplicationResources(ResourceSnapshot backup)
    {
        TestApplicationResources.Restore(backup.Entries, backup.MergedDictionaries);
    }

    private static void LoadRootAppResources()
    {
        var appPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "App.xml"));
        Assert.True(File.Exists(appPath), $"Expected App.xml to exist at '{appPath}'.");
        XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);
    }

    private readonly record struct HoverStepMetrics(
        int StepIndex,
        string Label,
        Vector2 Pointer,
        UiPointerMoveTelemetrySnapshot PointerMove,
        AnimationSinkTelemetrySnapshot SinkTelemetry,
        int BeginStoryboardCalls,
        int StoryboardStarts,
        double BeginStoryboardMilliseconds,
        double StoryboardStartMilliseconds,
        double StoryboardUpdateMilliseconds,
        double ComposeMilliseconds,
        double ComposeCollectMilliseconds,
        double ComposeSortMilliseconds,
        double ComposeMergeMilliseconds,
        double ComposeApplyMilliseconds,
        double ComposeBatchBeginMilliseconds,
        double ComposeBatchEndMilliseconds,
        double SinkSetValueMilliseconds,
        double CleanupCompletedMilliseconds,
        double FreezableOnChangedMilliseconds,
        double FreezableEndBatchMilliseconds,
        double FreezableBatchFlushMilliseconds,
        int FreezableBatchFlushCount,
        int FreezableBatchFlushTargetCount,
        int FreezableBatchQueuedTargetCount,
        int FreezableBatchMaxPendingTargetCount,
        string HottestFreezableOnChangedType,
        double HottestFreezableOnChangedMilliseconds,
        string HottestFreezableEndBatchType,
        double HottestFreezableEndBatchMilliseconds,
        string HottestSetValuePathSummary,
        int ActiveStoryboardCount,
        int ActiveLaneCount,
        int ActiveStoryboardEntryCount,
        int ComposePassCount,
        int LaneApplicationCount,
        int SinkValueSetCount,
        int ClearedLaneCount,
        int ItemsPresenterNeighborProbes,
        int ItemsPresenterFullFallbackScans,
        int LegacyEnumerableFallbacks,
        int MonotonicPanelFastPathCount,
        long MeasureInvalidationCount,
        long ArrangeInvalidationCount,
        long RenderInvalidationCount,
        int DirtyRootCount,
        int RetainedTraversalCount,
        bool IsFullDirty,
        int FullDirtyInitialStateCount,
        int FullDirtyViewportChangeCount,
        int FullDirtySurfaceResetCount,
        int FullDirtyVisualStructureChangeCount,
        int FullDirtyRetainedRebuildCount,
        int FullDirtyDetachedVisualCount,
        int DirtyRegionCount,
        double DirtyCoverage,
        bool WouldUsePartialDirtyRedraw,
        string TraversalClip,
        int TraversalNodesVisited,
        int TraversalNodesDrawn,
        int EditorSubtreeDrawn,
        int NonEditorSubtreeDrawn,
        string DirtyBoundsTrace);

    private readonly record struct HoverSummary(
        int TotalHitTests,
        int TotalRoutedEvents,
        double TotalHoverUpdateMilliseconds,
        double TotalPointerResolveMilliseconds,
        int TotalBeginStoryboardCalls,
        int TotalStoryboardStarts,
        int TotalLaneApplications,
        int TotalSinkValueSets,
        double TotalBeginStoryboardMilliseconds,
        double TotalStoryboardStartMilliseconds,
        double TotalStoryboardUpdateMilliseconds,
        double TotalComposeMilliseconds,
        double TotalComposeCollectMilliseconds,
        double TotalComposeSortMilliseconds,
        double TotalComposeMergeMilliseconds,
        double TotalComposeApplyMilliseconds,
        double TotalComposeBatchBeginMilliseconds,
        double TotalComposeBatchEndMilliseconds,
        double TotalSinkSetValueMilliseconds,
        double TotalCleanupCompletedMilliseconds,
        double TotalFreezableOnChangedMilliseconds,
        double TotalFreezableEndBatchMilliseconds,
        double TotalFreezableBatchFlushMilliseconds,
        int TotalFreezableBatchFlushes,
        int TotalFreezableBatchFlushTargets,
        int MaxFreezableBatchPendingTargets,
        int TotalDependencyPropertySinkSetCount,
        double TotalDependencyPropertySinkSetMilliseconds,
        int TotalClrPropertySinkSetCount,
        double TotalClrPropertySinkSetMilliseconds,
        long TotalRenderInvalidations,
        long TotalMeasureInvalidations,
        long TotalArrangeInvalidations,
        int TotalPartialDirtySteps,
        int TotalFullRedrawSteps,
        int FullDirtyInitialStateCount,
        int FullDirtyViewportChangeCount,
        int FullDirtySurfaceResetCount,
        int FullDirtyVisualStructureChangeCount,
        int FullDirtyRetainedRebuildCount,
        int FullDirtyDetachedVisualCount,
        double MaxDirtyCoverage,
        int MaxTraversalDrawn,
        int MaxNonEditorDrawn,
        int MaxActiveStoryboards,
        int MaxActiveLanes,
        int MaxActiveStoryboardEntries,
        string HottestFreezableOnChangedType,
        double HottestFreezableOnChangedMilliseconds,
        string HottestFreezableEndBatchType,
        double HottestFreezableEndBatchMilliseconds,
        string HottestSetValuePaths,
        string HotspotInference,
        string HottestStepLabel,
        string HottestStepDetail);

    private readonly record struct ResourceSnapshot(
        IReadOnlyList<KeyValuePair<object, object?>> Entries,
        IReadOnlyList<ResourceDictionary> MergedDictionaries);
}
