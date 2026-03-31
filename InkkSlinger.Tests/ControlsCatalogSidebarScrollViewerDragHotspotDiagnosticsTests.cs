using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogSidebarScrollViewerDragHotspotDiagnosticsTests
{
    private const int ViewportWidth = 1400;
    private const int ViewportHeight = 900;
    private const int DragStepCountPerDirection = 14;

    [Fact]
    public void SidebarScrollViewer_ScrollBarThumbDrag_WritesHotspotDiagnosticsLog()
    {
        AnimationManager.Current.ResetForTests();
        VisualTreeHelper.ResetInstrumentationForTests();
        Freezable.ResetTelemetryForTests();
        UIElement.ResetFreezableInvalidationBatchTelemetryForTests();
        Style.ResetTelemetryForTests();
        TemplateTriggerEngine.ResetTelemetryForTests();
        VisualStateManager.ResetTelemetryForTests();

        var catalog = new ControlsCatalogView();
        var uiRoot = new UiRoot(catalog);
        RunFrame(uiRoot, 16);

        var sidebarViewer = FindSidebarScrollViewer(catalog);
        var viewport = GetViewerViewportRect(sidebarViewer);
        var verticalBar = FindFirstVisualChild<ScrollBar>(
                              sidebarViewer,
                              static bar => bar.Orientation == Orientation.Vertical && bar.IsVisible)
                          ?? throw new InvalidOperationException("Could not find the sidebar vertical scrollbar.");
        var thumb = FindFirstVisualChild<Thumb>(verticalBar)
                    ?? throw new InvalidOperationException("Could not find the sidebar scrollbar thumb.");

        var thumbRect = verticalBar.GetThumbRectForInput();
        var thumbCenter = GetCenter(thumbRect);
        var dragBottomY = verticalBar.LayoutSlot.Y + verticalBar.LayoutSlot.Height - 3f;
        var dragTopY = verticalBar.LayoutSlot.Y + 3f;
        var outsidePointerX = sidebarViewer.LayoutSlot.X + sidebarViewer.LayoutSlot.Width + 80f;
        var logPath = GetDiagnosticsLogPath("controls-catalog-sidebar-scrollbar-drag-hotspot");

        var lines = new List<string>
        {
            "scenario=ControlsCatalog sidebar ScrollViewer scrollbar thumb drag",
            $"timestamp_utc={DateTime.UtcNow:O}",
            $"log_path={logPath}",
            "step_1=move pointer to the vertical scrollbar thumb",
            "step_2=press left button to capture the thumb",
            "step_3=drag down and then up while keeping the pointer on the scrollbar thumb x-position",
            "step_4=repeat the same drag while moving the pointer outside the ScrollViewer after capture",
            $"viewer_slot={FormatRect(sidebarViewer.LayoutSlot)}",
            $"viewport_rect={FormatRect(viewport)}",
            $"vertical_scrollbar_slot={FormatRect(verticalBar.LayoutSlot)}",
            $"thumb_rect={FormatRect(thumbRect)}",
            $"thumb_center=({thumbCenter.X:0.##},{thumbCenter.Y:0.##})",
            $"drag_bottom_y={dragBottomY:0.##}",
            $"drag_top_y={dragTopY:0.##}",
            $"outside_pointer_x={outsidePointerX:0.##}"
        };

        var pointerOnThumbMetrics = RunDragVariant(
            uiRoot,
            variantName: "pointer-on-thumb",
            pressPoint: thumbCenter,
            movePoints: BuildDragPoints(thumbCenter.X, dragTopY, dragBottomY, DragStepCountPerDirection));

        ResetForNextVariant(catalog, uiRoot);

        sidebarViewer = FindSidebarScrollViewer(catalog);
        verticalBar = FindFirstVisualChild<ScrollBar>(
                          sidebarViewer,
                          static bar => bar.Orientation == Orientation.Vertical && bar.IsVisible)
                      ?? throw new InvalidOperationException("Could not find the sidebar vertical scrollbar after reset.");
        thumbRect = verticalBar.GetThumbRectForInput();
        thumbCenter = GetCenter(thumbRect);

        var pointerOutsideMetrics = RunDragVariant(
            uiRoot,
            variantName: "pointer-outside-scrollviewer",
            pressPoint: thumbCenter,
            movePoints: BuildDragPoints(outsidePointerX, dragTopY, dragBottomY, DragStepCountPerDirection));

        var variantSummaries = new[]
        {
            Summarize(pointerOnThumbMetrics),
            Summarize(pointerOutsideMetrics)
        };

        lines.Add(string.Empty);
        lines.Add("summary:");
        foreach (var summary in variantSummaries)
        {
            lines.Add($"variant={summary.VariantName}");
            lines.Add($"  total_steps={summary.TotalSteps}");
            lines.Add($"  total_vertical_offset_delta={summary.TotalVerticalOffsetDelta:0.###}");
            lines.Add($"  total_pointer_dispatch_ms={summary.TotalPointerDispatchMs:0.###}");
            lines.Add($"  total_pointer_resolve_ms={summary.TotalPointerResolveMs:0.###}");
            lines.Add($"  total_hover_update_ms={summary.TotalHoverUpdateMs:0.###}");
            lines.Add($"  total_pointer_route_ms={summary.TotalPointerRouteMs:0.###}");
            lines.Add($"  total_pointer_move_handler_ms={summary.TotalPointerMoveHandlerMs:0.###}");
            lines.Add($"  total_thumb_handle_pointer_move_ms={summary.TotalThumbHandlePointerMoveMs:0.###}");
            lines.Add($"  total_thumb_raise_dragdelta_ms={summary.TotalThumbRaiseDragDeltaMs:0.###}");
            lines.Add($"  total_scrollbar_thumb_drag_ms={summary.TotalScrollBarThumbDragMs:0.###}");
            lines.Add($"  total_scrollbar_thumb_valueset_ms={summary.TotalScrollBarThumbValueSetMs:0.###}");
            lines.Add($"  total_scrollbar_onvaluechanged_base_ms={summary.TotalScrollBarOnValueChangedBaseMs:0.###}");
            lines.Add($"  total_valuechanged_raise_ms={summary.TotalValueChangedRaiseMs:0.###}");
            lines.Add($"  total_valuechanged_routebuild_ms={summary.TotalValueChangedRouteBuildMs:0.###}");
            lines.Add($"  total_valuechanged_routetraverse_ms={summary.TotalValueChangedRouteTraverseMs:0.###}");
            lines.Add($"  total_valuechanged_classhandlers_ms={summary.TotalValueChangedClassHandlerMs:0.###}");
            lines.Add($"  total_valuechanged_instancedispatch_ms={summary.TotalValueChangedInstanceDispatchMs:0.###}");
            lines.Add($"  total_valuechanged_instanceprepare_ms={summary.TotalValueChangedInstancePrepareMs:0.###}");
            lines.Add($"  total_valuechanged_instanceinvoke_ms={summary.TotalValueChangedInstanceInvokeMs:0.###}");
            lines.Add($"  max_valuechanged_route_length={summary.MaxValueChangedRouteLength}");
            lines.Add($"  total_scrollbar_onvaluechanged_synctrack_ms={summary.TotalScrollBarOnValueChangedSyncTrackMs:0.###}");
            lines.Add($"  total_scrollbar_synctrack_ms={summary.TotalScrollBarSyncTrackMs:0.###}");
            lines.Add($"  total_scrollbar_refreshtracklayout_ms={summary.TotalScrollBarRefreshTrackLayoutMs:0.###}");
            lines.Add($"  total_track_valuefromthumbtravel_ms={summary.TotalTrackValueFromThumbTravelMs:0.###}");
            lines.Add($"  total_track_refreshlayout_mutation_ms={summary.TotalTrackRefreshLayoutMutationMs:0.###}");
            lines.Add($"  total_track_refreshlayout_value_mutation_ms={summary.TotalTrackRefreshLayoutValueMutationMs:0.###}");
            lines.Add($"  total_track_refreshlayout_viewport_mutation_ms={summary.TotalTrackRefreshLayoutViewportMutationMs:0.###}");
            lines.Add($"  total_track_refreshlayout_minimum_mutation_ms={summary.TotalTrackRefreshLayoutMinimumMutationMs:0.###}");
            lines.Add($"  total_track_refreshlayout_maximum_mutation_ms={summary.TotalTrackRefreshLayoutMaximumMutationMs:0.###}");
            lines.Add($"  total_track_refreshlayout_direction_mutation_ms={summary.TotalTrackRefreshLayoutDirectionMutationMs:0.###}");
            lines.Add($"  total_track_refreshlayout_snapshot_ms={summary.TotalTrackRefreshLayoutSnapshotMs:0.###}");
            lines.Add($"  total_track_refreshlayout_invalidatearrange_ms={summary.TotalTrackRefreshLayoutInvalidateArrangeMs:0.###}");
            lines.Add($"  total_track_refreshlayout_arrange_ms={summary.TotalTrackRefreshLayoutArrangeMs:0.###}");
            lines.Add($"  total_track_refreshlayout_dirtybounds_ms={summary.TotalTrackRefreshLayoutDirtyBoundsMs:0.###}");
            lines.Add($"  total_track_refreshlayout_visualinvalidate_ms={summary.TotalTrackRefreshLayoutVisualInvalidateMs:0.###}");
            lines.Add($"  total_track_refreshlayout_needsmeasure_fallback_ms={summary.TotalTrackRefreshLayoutNeedsMeasureFallbackMs:0.###}");
            lines.Add($"  total_track_refreshlayout_dirtybounds_hints={summary.TotalTrackRefreshLayoutDirtyBoundsHints}");
            lines.Add($"  total_track_refreshlayout_visual_fallbacks={summary.TotalTrackRefreshLayoutVisualFallbacks}");
            lines.Add($"  total_panel_measure_ms={summary.TotalPanelMeasureMs:0.###}");
            lines.Add($"  total_panel_arrange_ms={summary.TotalPanelArrangeMs:0.###}");
            lines.Add($"  total_panel_measure_calls={summary.TotalPanelMeasureCalls}");
            lines.Add($"  total_panel_arrange_calls={summary.TotalPanelArrangeCalls}");
            lines.Add($"  total_panel_measured_children={summary.TotalPanelMeasuredChildren}");
            lines.Add($"  total_panel_arranged_children={summary.TotalPanelArrangedChildren}");
            lines.Add($"  total_stackpanel_measure_ms={summary.TotalStackPanelMeasureMs:0.###}");
            lines.Add($"  total_stackpanel_arrange_ms={summary.TotalStackPanelArrangeMs:0.###}");
            lines.Add($"  total_stackpanel_measure_calls={summary.TotalStackPanelMeasureCalls}");
            lines.Add($"  total_stackpanel_arrange_calls={summary.TotalStackPanelArrangeCalls}");
            lines.Add($"  total_stackpanel_measured_children={summary.TotalStackPanelMeasuredChildren}");
            lines.Add($"  total_stackpanel_arranged_children={summary.TotalStackPanelArrangedChildren}");
            lines.Add($"  total_button_measure_ms={summary.TotalButtonMeasureMs:0.###}");
            lines.Add($"  total_button_render_ms={summary.TotalButtonRenderMs:0.###}");
            lines.Add($"  total_button_resolvetextlayout_ms={summary.TotalButtonResolveTextLayoutMs:0.###}");
            lines.Add($"  total_button_renderchrome_ms={summary.TotalButtonRenderChromeMs:0.###}");
            lines.Add($"  total_button_rendertextprep_ms={summary.TotalButtonRenderTextPreparationMs:0.###}");
            lines.Add($"  total_button_rendertextdraw_ms={summary.TotalButtonRenderTextDrawDispatchMs:0.###}");
            lines.Add($"  total_button_rendertextprep_calls={summary.TotalButtonRenderTextPreparationCalls}");
            lines.Add($"  total_button_rendertextdraw_calls={summary.TotalButtonRenderTextDrawDispatchCalls}");
            lines.Add($"  total_scrollviewer_vertical_valuechanged_ms={summary.TotalScrollViewerVerticalValueChangedMs:0.###}");
            lines.Add($"  total_scrollviewer_vertical_setoffsets_ms={summary.TotalScrollViewerVerticalSetOffsetsMs:0.###}");
            lines.Add($"  total_render_scheduling_ms={summary.TotalRenderSchedulingMs:0.###}");
            lines.Add($"  total_visual_update_ms={summary.TotalVisualUpdateMs:0.###}");
            lines.Add($"  total_layout_phase_ms={summary.TotalLayoutPhaseMs:0.###}");
            lines.Add($"  total_animation_phase_ms={summary.TotalAnimationPhaseMs:0.###}");
            lines.Add($"  total_retained_traversals={summary.TotalRetainedTraversals}");
            lines.Add($"  total_dirty_roots={summary.TotalDirtyRoots}");
            lines.Add($"  total_render_invalidations={summary.TotalRenderInvalidations}");
            lines.Add($"  total_measure_invalidations={summary.TotalMeasureInvalidations}");
            lines.Add($"  total_arrange_invalidations={summary.TotalArrangeInvalidations}");
            lines.Add($"  total_scrollviewer_set_offset_calls={summary.TotalScrollViewerSetOffsetCalls}");
            lines.Add($"  total_scrollviewer_set_offset_no_ops={summary.TotalScrollViewerSetOffsetNoOps}");
            lines.Add($"  total_hit_tests={summary.TotalHitTests}");
            lines.Add($"  total_routed_events={summary.TotalRoutedEvents}");
            lines.Add($"  total_begin_storyboard_calls={summary.TotalBeginStoryboardCalls}");
            lines.Add($"  total_lane_applications={summary.TotalLaneApplications}");
            lines.Add($"  total_sink_value_sets={summary.TotalSinkValueSets}");
            lines.Add($"  total_style_apply_ms={summary.TotalStyleApplyMs:0.###}");
            lines.Add($"  total_style_apply_setters_ms={summary.TotalStyleApplySettersMs:0.###}");
            lines.Add($"  total_style_apply_triggers_ms={summary.TotalStyleApplyTriggersMs:0.###}");
            lines.Add($"  total_style_collect_triggered_values_ms={summary.TotalStyleCollectTriggeredValuesMs:0.###}");
            lines.Add($"  total_style_trigger_action_ms={summary.TotalStyleApplyTriggerActionsMs:0.###}");
            lines.Add($"  total_style_invoke_actions_ms={summary.TotalStyleInvokeActionsMs:0.###}");
            lines.Add($"  total_style_trigger_matches={summary.TotalStyleTriggerMatches}");
            lines.Add($"  total_style_matched_triggers={summary.TotalStyleMatchedTriggers}");
            lines.Add($"  total_style_trigger_sets={summary.TotalStyleTriggerSets}");
            lines.Add($"  total_style_trigger_clears={summary.TotalStyleTriggerClears}");
            lines.Add($"  total_template_reapply_ms={summary.TotalTemplateReapplyMs:0.###}");
            lines.Add($"  total_template_match_ms={summary.TotalTemplateTriggerMatchMs:0.###}");
            lines.Add($"  total_template_setter_resolve_ms={summary.TotalTemplateSetterResolveMs:0.###}");
            lines.Add($"  total_template_actions_ms={summary.TotalTemplateInvokeActionsMs:0.###}");
            lines.Add($"  total_template_matched_triggers={summary.TotalTemplateMatchedTriggers}");
            lines.Add($"  total_template_sets={summary.TotalTemplateSetValues}");
            lines.Add($"  total_template_clears={summary.TotalTemplateClears}");
            lines.Add($"  total_visualstate_go_to_state_ms={summary.TotalVisualStateGoToStateMs:0.###}");
            lines.Add($"  total_visualstate_try_go_to_state_ms={summary.TotalVisualStateTryGoToStateMs:0.###}");
            lines.Add($"  total_visualstate_group_transition_ms={summary.TotalVisualStateGroupTransitionMs:0.###}");
            lines.Add($"  total_visualstate_apply_setters_ms={summary.TotalVisualStateApplySettersMs:0.###}");
            lines.Add($"  total_visualstate_storyboard_ms={summary.TotalVisualStateStoryboardMs:0.###}");
            lines.Add($"  total_visualstate_sets={summary.TotalVisualStateSetValues}");
            lines.Add($"  total_visualstate_clears={summary.TotalVisualStateClears}");
            lines.Add($"  total_freezable_endbatch_ms={summary.TotalFreezableEndBatchMs:0.###}");
            lines.Add($"  total_freezable_invalidation_flush_ms={summary.TotalFreezableInvalidationFlushMs:0.###}");
            lines.Add($"  max_dirty_roots_single_step={summary.MaxDirtyRootsSingleStep}");
            lines.Add($"  hottest_step={summary.HottestStepLabel}");
            lines.Add($"  hottest_step_detail={summary.HottestStepDetail}");
            lines.Add($"  hotspot_inference={summary.HotspotInference}");
        }

        var comparison = Compare(variantSummaries[0], variantSummaries[1]);
        lines.Add($"comparison_inference={comparison}");

        lines.Add(string.Empty);
        lines.Add("steps:");
        lines.AddRange(pointerOnThumbMetrics.Select(FormatStep));
        lines.AddRange(pointerOutsideMetrics.Select(FormatStep));

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.WriteAllLines(logPath, lines);

        Assert.True(File.Exists(logPath));
        Assert.NotEmpty(pointerOnThumbMetrics);
        Assert.NotEmpty(pointerOutsideMetrics);
    }

    private static List<DragStepMetrics> RunDragVariant(
        UiRoot uiRoot,
        string variantName,
        Vector2 pressPoint,
        IReadOnlyList<Vector2> movePoints)
    {
        var metrics = new List<DragStepMetrics>();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pressPoint, pointerMoved: true));
        RunFrame(uiRoot, 16);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pressPoint, leftPressed: true));
        RunFrame(uiRoot, 16);

        for (var i = 0; i < movePoints.Count; i++)
        {
            metrics.Add(RunDragStep(uiRoot, variantName, i, movePoints[i]));
        }

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(movePoints[^1], leftReleased: true));
        RunFrame(uiRoot, 16);

        return metrics;
    }

    private static DragStepMetrics RunDragStep(UiRoot uiRoot, string variantName, int stepIndex, Vector2 pointer)
    {
        var beforeVisualTree = uiRoot.GetVisualTreeMetricsSnapshot();
        var beforeRender = uiRoot.GetRenderTelemetrySnapshotForTests();
        var beforePerformance = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        var beforeAnimation = AnimationManager.Current.GetTelemetrySnapshotForTests();
        var beforeFreezable = Freezable.GetTelemetrySnapshotForTests();
        var beforeFreezableInvalidation = UIElement.GetFreezableInvalidationBatchSnapshotForTests();
        var beforeHitTest = VisualTreeHelper.GetInstrumentationSnapshotForTests();
        _ = ScrollViewer.GetScrollMetricsAndReset();
        _ = ScrollViewer.GetValueChangedTelemetryAndReset();
        _ = ScrollBar.GetThumbDragTelemetryAndReset();
        _ = Track.GetThumbTravelTelemetryAndReset();
        _ = Thumb.GetDragTelemetryAndReset();
        _ = Panel.GetTelemetryAndReset();
        _ = StackPanel.GetTelemetryAndReset();
        Button.ResetTimingForTests();
        _ = UIElement.GetValueChangedRoutedEventTelemetryAndReset();
        AnimationValueSink.ResetTelemetryForTests();
        var beforeStyle = Style.GetTelemetrySnapshotForTests();
        var beforeTemplateTrigger = TemplateTriggerEngine.GetTelemetrySnapshotForTests();
        var beforeVisualState = VisualStateManager.GetTelemetrySnapshotForTests();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        var pointerMove = uiRoot.GetPointerMoveTelemetrySnapshotForTests();

        RunFrame(uiRoot, 16);

        var afterVisualTree = uiRoot.GetVisualTreeMetricsSnapshot();
        var afterRender = uiRoot.GetRenderTelemetrySnapshotForTests();
        var afterPerformance = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        var afterAnimation = AnimationManager.Current.GetTelemetrySnapshotForTests();
        var afterFreezable = Freezable.GetTelemetrySnapshotForTests();
        var afterFreezableInvalidation = UIElement.GetFreezableInvalidationBatchSnapshotForTests();
        var afterHitTest = VisualTreeHelper.GetInstrumentationSnapshotForTests();
        var scrollMetrics = ScrollViewer.GetScrollMetricsAndReset();
        var scrollViewerValueChangedTelemetry = ScrollViewer.GetValueChangedTelemetryAndReset();
        var scrollBarTelemetry = ScrollBar.GetThumbDragTelemetryAndReset();
        var trackTelemetry = Track.GetThumbTravelTelemetryAndReset();
        var thumbTelemetry = Thumb.GetDragTelemetryAndReset();
        var panelTelemetry = Panel.GetTelemetryAndReset();
        var stackPanelTelemetry = StackPanel.GetTelemetryAndReset();
        var buttonTiming = Button.GetTimingSnapshotForTests();
        var valueChangedTelemetry = UIElement.GetValueChangedRoutedEventTelemetryAndReset();
        var sinkTelemetry = AnimationValueSink.GetTelemetrySnapshotForTests();
        var afterStyle = Style.GetTelemetrySnapshotForTests();
        var afterTemplateTrigger = TemplateTriggerEngine.GetTelemetrySnapshotForTests();
        var afterVisualState = VisualStateManager.GetTelemetrySnapshotForTests();
        var hovered = uiRoot.GetHoveredElementForDiagnostics();
        var hoveredName = hovered is FrameworkElement frameworkElement ? frameworkElement.Name ?? string.Empty : string.Empty;

        return new DragStepMetrics(
            variantName,
            stepIndex,
            pointer,
            hovered?.GetType().Name ?? "null",
            hoveredName,
            pointerMove,
            thumbTelemetry,
            scrollBarTelemetry,
            trackTelemetry,
            panelTelemetry,
            stackPanelTelemetry,
            TicksToMilliseconds(buttonTiming.MeasureOverrideElapsedTicks),
            TicksToMilliseconds(buttonTiming.RenderElapsedTicks),
            TicksToMilliseconds(buttonTiming.ResolveTextLayoutElapsedTicks),
            TicksToMilliseconds(buttonTiming.RenderChromeElapsedTicks),
            TicksToMilliseconds(buttonTiming.RenderTextPreparationElapsedTicks),
            TicksToMilliseconds(buttonTiming.RenderTextDrawDispatchElapsedTicks),
            buttonTiming.RenderTextPreparationCallCount,
            buttonTiming.RenderTextDrawDispatchCallCount,
            valueChangedTelemetry,
            scrollMetrics,
            scrollViewerValueChangedTelemetry,
            sinkTelemetry,
            afterPerformance.InputPhaseMilliseconds - beforePerformance.InputPhaseMilliseconds,
            afterPerformance.LayoutPhaseMilliseconds - beforePerformance.LayoutPhaseMilliseconds,
            afterPerformance.AnimationPhaseMilliseconds - beforePerformance.AnimationPhaseMilliseconds,
            afterPerformance.RenderSchedulingPhaseMilliseconds - beforePerformance.RenderSchedulingPhaseMilliseconds,
            afterPerformance.VisualUpdateMilliseconds - beforePerformance.VisualUpdateMilliseconds,
            afterPerformance.RetainedQueueCompactionMilliseconds - beforePerformance.RetainedQueueCompactionMilliseconds,
            afterPerformance.RetainedCandidateCoalescingMilliseconds - beforePerformance.RetainedCandidateCoalescingMilliseconds,
            afterPerformance.RetainedSubtreeUpdateMilliseconds - beforePerformance.RetainedSubtreeUpdateMilliseconds,
            afterPerformance.RetainedShallowSyncMilliseconds - beforePerformance.RetainedShallowSyncMilliseconds,
            afterPerformance.RetainedDeepSyncMilliseconds - beforePerformance.RetainedDeepSyncMilliseconds,
            afterPerformance.RetainedAncestorRefreshMilliseconds - beforePerformance.RetainedAncestorRefreshMilliseconds,
            afterPerformance.RetainedForceDeepSyncCount - beforePerformance.RetainedForceDeepSyncCount,
            afterPerformance.RetainedForcedDeepDowngradeBlockedCount - beforePerformance.RetainedForcedDeepDowngradeBlockedCount,
            afterPerformance.RetainedShallowSuccessCount - beforePerformance.RetainedShallowSuccessCount,
            afterPerformance.RetainedShallowRejectRenderStateCount - beforePerformance.RetainedShallowRejectRenderStateCount,
            afterPerformance.RetainedShallowRejectVisibilityCount - beforePerformance.RetainedShallowRejectVisibilityCount,
            afterPerformance.RetainedShallowRejectStructureCount - beforePerformance.RetainedShallowRejectStructureCount,
            afterPerformance.RetainedOverlapForcedDeepCount - beforePerformance.RetainedOverlapForcedDeepCount,
            afterAnimation.BeginStoryboardCallCount - beforeAnimation.BeginStoryboardCallCount,
            afterAnimation.StoryboardStartCount - beforeAnimation.StoryboardStartCount,
            afterAnimation.ActiveStoryboardCount,
            afterAnimation.ActiveLaneCount,
            afterAnimation.LaneApplicationCount - beforeAnimation.LaneApplicationCount,
            afterAnimation.SinkValueSetCount - beforeAnimation.SinkValueSetCount,
            afterAnimation.ComposeMilliseconds - beforeAnimation.ComposeMilliseconds,
            afterAnimation.ComposeCollectMilliseconds - beforeAnimation.ComposeCollectMilliseconds,
            afterAnimation.ComposeSortMilliseconds - beforeAnimation.ComposeSortMilliseconds,
            afterAnimation.ComposeMergeMilliseconds - beforeAnimation.ComposeMergeMilliseconds,
            afterAnimation.ComposeApplyMilliseconds - beforeAnimation.ComposeApplyMilliseconds,
            afterAnimation.ComposeBatchEndMilliseconds - beforeAnimation.ComposeBatchEndMilliseconds,
            afterAnimation.SinkSetValueMilliseconds - beforeAnimation.SinkSetValueMilliseconds,
            afterAnimation.HottestSetValuePathSummary,
            afterStyle.ApplyMilliseconds - beforeStyle.ApplyMilliseconds,
            afterStyle.ApplySettersMilliseconds - beforeStyle.ApplySettersMilliseconds,
            afterStyle.ApplyTriggersMilliseconds - beforeStyle.ApplyTriggersMilliseconds,
            afterStyle.CollectTriggeredValuesMilliseconds - beforeStyle.CollectTriggeredValuesMilliseconds,
            afterStyle.TriggerMatchCount - beforeStyle.TriggerMatchCount,
            afterStyle.MatchedTriggerCount - beforeStyle.MatchedTriggerCount,
            afterStyle.SetStyleTriggerValueCount - beforeStyle.SetStyleTriggerValueCount,
            afterStyle.ClearStyleTriggerValueCount - beforeStyle.ClearStyleTriggerValueCount,
            afterStyle.ApplyTriggerActionsMilliseconds - beforeStyle.ApplyTriggerActionsMilliseconds,
            afterStyle.InvokeActionsMilliseconds - beforeStyle.InvokeActionsMilliseconds,
            afterTemplateTrigger.ReapplyMilliseconds - beforeTemplateTrigger.ReapplyMilliseconds,
            afterTemplateTrigger.TriggerMatchMilliseconds - beforeTemplateTrigger.TriggerMatchMilliseconds,
            afterTemplateTrigger.SetterResolveMilliseconds - beforeTemplateTrigger.SetterResolveMilliseconds,
            afterTemplateTrigger.MatchedTriggerCount - beforeTemplateTrigger.MatchedTriggerCount,
            afterTemplateTrigger.SetTemplateTriggerValueCount - beforeTemplateTrigger.SetTemplateTriggerValueCount,
            afterTemplateTrigger.ClearTemplateTriggerValueCount - beforeTemplateTrigger.ClearTemplateTriggerValueCount,
            afterTemplateTrigger.InvokeActionsMilliseconds - beforeTemplateTrigger.InvokeActionsMilliseconds,
            afterVisualState.GoToStateMilliseconds - beforeVisualState.GoToStateMilliseconds,
            afterVisualState.TryGoToStateMilliseconds - beforeVisualState.TryGoToStateMilliseconds,
            afterVisualState.GroupGoToStateMilliseconds - beforeVisualState.GroupGoToStateMilliseconds,
            afterVisualState.GroupApplySettersMilliseconds - beforeVisualState.GroupApplySettersMilliseconds,
            afterVisualState.GroupStoryboardMilliseconds - beforeVisualState.GroupStoryboardMilliseconds,
            afterVisualState.SetTemplateTriggerValueCount - beforeVisualState.SetTemplateTriggerValueCount,
            afterVisualState.ClearTemplateTriggerValueCount - beforeVisualState.ClearTemplateTriggerValueCount,
            afterFreezable.EndBatchMilliseconds - beforeFreezable.EndBatchMilliseconds,
            afterFreezable.OnChangedMilliseconds - beforeFreezable.OnChangedMilliseconds,
            afterFreezable.EndBatchFlushCount - beforeFreezable.EndBatchFlushCount,
            afterFreezable.HottestEndBatchType,
            afterFreezableInvalidation.FlushCount - beforeFreezableInvalidation.FlushCount,
            afterFreezableInvalidation.FlushMilliseconds - beforeFreezableInvalidation.FlushMilliseconds,
            afterFreezableInvalidation.LastFlushTargetSummary,
            afterRender.DirtyRootCount - beforeRender.DirtyRootCount,
            afterRender.RetainedTraversalCount - beforeRender.RetainedTraversalCount,
            afterRender.RetainedNodesVisited - beforeRender.RetainedNodesVisited,
            afterRender.RetainedNodesDrawn - beforeRender.RetainedNodesDrawn,
            afterVisualTree.MeasureInvalidationCount - beforeVisualTree.MeasureInvalidationCount,
            afterVisualTree.ArrangeInvalidationCount - beforeVisualTree.ArrangeInvalidationCount,
            afterVisualTree.RenderInvalidationCount - beforeVisualTree.RenderInvalidationCount,
            afterHitTest.ItemsPresenterNeighborProbes - beforeHitTest.ItemsPresenterNeighborProbes,
            afterHitTest.ItemsPresenterFullFallbackScans - beforeHitTest.ItemsPresenterFullFallbackScans,
            afterHitTest.LegacyEnumerableFallbacks - beforeHitTest.LegacyEnumerableFallbacks,
            afterHitTest.MonotonicPanelFastPathCount - beforeHitTest.MonotonicPanelFastPathCount);
    }

    private static DragVariantSummary Summarize(IReadOnlyList<DragStepMetrics> steps)
    {
        var hottest = steps
            .OrderByDescending(static step => step.VisualUpdateMs + step.RenderSchedulingMs)
            .ThenByDescending(static step => step.PointerMove.PointerMoveHandlerMilliseconds)
            .ThenByDescending(static step => step.ScrollViewer.TotalVerticalDelta)
            .First();

        var totalPointerHandlerMs = steps.Sum(static step => step.PointerMove.PointerMoveHandlerMilliseconds);
        var totalThumbHandlePointerMoveMs = steps.Sum(static step => step.Thumb.HandlePointerMoveMilliseconds);
        var totalThumbRaiseDragDeltaMs = steps.Sum(static step => step.Thumb.RaiseDragDeltaMilliseconds);
        var totalScrollBarThumbDragMs = steps.Sum(static step => step.ScrollBar.OnThumbDragDeltaMilliseconds);
        var totalScrollBarThumbValueSetMs = steps.Sum(static step => step.ScrollBar.OnThumbDragDeltaValueSetMilliseconds);
        var totalScrollBarOnValueChangedBaseMs = steps.Sum(static step => step.ScrollBar.OnValueChangedBaseMilliseconds);
        var totalValueChangedRaiseMs = steps.Sum(static step => step.ValueChanged.RaiseMilliseconds);
        var totalValueChangedRouteBuildMs = steps.Sum(static step => step.ValueChanged.RouteBuildMilliseconds);
        var totalValueChangedRouteTraverseMs = steps.Sum(static step => step.ValueChanged.RouteTraverseMilliseconds);
        var totalValueChangedClassHandlerMs = steps.Sum(static step => step.ValueChanged.ClassHandlerMilliseconds);
        var totalValueChangedInstanceDispatchMs = steps.Sum(static step => step.ValueChanged.InstanceDispatchMilliseconds);
        var totalValueChangedInstancePrepareMs = steps.Sum(static step => step.ValueChanged.InstancePrepareMilliseconds);
        var totalValueChangedInstanceInvokeMs = steps.Sum(static step => step.ValueChanged.InstanceInvokeMilliseconds);
        var maxValueChangedRouteLength = steps.Max(static step => step.ValueChanged.MaxRouteLength);
        var totalScrollBarOnValueChangedSyncTrackMs = steps.Sum(static step => step.ScrollBar.OnValueChangedSyncTrackStateMilliseconds);
        var totalScrollBarSyncTrackMs = steps.Sum(static step => step.ScrollBar.SyncTrackStateMilliseconds);
        var totalScrollBarRefreshTrackLayoutMs = steps.Sum(static step => step.ScrollBar.RefreshTrackLayoutMilliseconds);
        var totalTrackValueFromThumbTravelMs = steps.Sum(static step => step.Track.GetValueFromThumbTravelMilliseconds);
        var totalTrackRefreshLayoutMutationMs = steps.Sum(static step => step.Track.RefreshLayoutForStateMutationMilliseconds);
        var totalTrackRefreshLayoutValueMutationMs = steps.Sum(static step => step.Track.RefreshLayoutValueMutationMilliseconds);
        var totalTrackRefreshLayoutViewportMutationMs = steps.Sum(static step => step.Track.RefreshLayoutViewportMutationMilliseconds);
        var totalTrackRefreshLayoutMinimumMutationMs = steps.Sum(static step => step.Track.RefreshLayoutMinimumMutationMilliseconds);
        var totalTrackRefreshLayoutMaximumMutationMs = steps.Sum(static step => step.Track.RefreshLayoutMaximumMutationMilliseconds);
        var totalTrackRefreshLayoutDirectionMutationMs = steps.Sum(static step => step.Track.RefreshLayoutDirectionMutationMilliseconds);
        var totalTrackRefreshLayoutSnapshotMs = steps.Sum(static step => step.Track.RefreshLayoutCaptureSnapshotMilliseconds);
        var totalTrackRefreshLayoutInvalidateArrangeMs = steps.Sum(static step => step.Track.RefreshLayoutInvalidateArrangeMilliseconds);
        var totalTrackRefreshLayoutArrangeMs = steps.Sum(static step => step.Track.RefreshLayoutArrangeMilliseconds);
        var totalTrackRefreshLayoutDirtyBoundsMs = steps.Sum(static step => step.Track.RefreshLayoutDirtyBoundsMilliseconds);
        var totalTrackRefreshLayoutVisualInvalidateMs = steps.Sum(static step => step.Track.RefreshLayoutVisualInvalidationMilliseconds);
        var totalTrackRefreshLayoutNeedsMeasureFallbackMs = steps.Sum(static step => step.Track.RefreshLayoutNeedsMeasureFallbackMilliseconds);
        var totalTrackRefreshLayoutDirtyBoundsHints = steps.Sum(static step => step.Track.RefreshLayoutDirtyBoundsHintCount);
        var totalTrackRefreshLayoutVisualFallbacks = steps.Sum(static step => step.Track.RefreshLayoutVisualFallbackCount);
        var totalPanelMeasureMs = steps.Sum(static step => step.Panel.MeasureMilliseconds);
        var totalPanelArrangeMs = steps.Sum(static step => step.Panel.ArrangeMilliseconds);
        var totalPanelMeasureCalls = steps.Sum(static step => step.Panel.MeasureCallCount);
        var totalPanelArrangeCalls = steps.Sum(static step => step.Panel.ArrangeCallCount);
        var totalPanelMeasuredChildren = steps.Sum(static step => step.Panel.MeasuredChildCount);
        var totalPanelArrangedChildren = steps.Sum(static step => step.Panel.ArrangedChildCount);
        var totalStackPanelMeasureMs = steps.Sum(static step => step.StackPanel.MeasureMilliseconds);
        var totalStackPanelArrangeMs = steps.Sum(static step => step.StackPanel.ArrangeMilliseconds);
        var totalStackPanelMeasureCalls = steps.Sum(static step => step.StackPanel.MeasureCallCount);
        var totalStackPanelArrangeCalls = steps.Sum(static step => step.StackPanel.ArrangeCallCount);
        var totalStackPanelMeasuredChildren = steps.Sum(static step => step.StackPanel.MeasuredChildCount);
        var totalStackPanelArrangedChildren = steps.Sum(static step => step.StackPanel.ArrangedChildCount);
        var totalButtonMeasureMs = steps.Sum(static step => step.ButtonMeasureMs);
        var totalButtonRenderMs = steps.Sum(static step => step.ButtonRenderMs);
        var totalButtonResolveTextLayoutMs = steps.Sum(static step => step.ButtonResolveTextLayoutMs);
        var totalButtonRenderChromeMs = steps.Sum(static step => step.ButtonRenderChromeMs);
        var totalButtonRenderTextPreparationMs = steps.Sum(static step => step.ButtonRenderTextPreparationMs);
        var totalButtonRenderTextDrawDispatchMs = steps.Sum(static step => step.ButtonRenderTextDrawDispatchMs);
        var totalButtonRenderTextPreparationCalls = steps.Sum(static step => step.ButtonRenderTextPreparationCalls);
        var totalButtonRenderTextDrawDispatchCalls = steps.Sum(static step => step.ButtonRenderTextDrawDispatchCalls);
        var totalScrollViewerVerticalValueChangedMs = steps.Sum(static step => step.ScrollViewerValueChanged.VerticalValueChangedMilliseconds);
        var totalScrollViewerVerticalSetOffsetsMs = steps.Sum(static step => step.ScrollViewerValueChanged.VerticalValueChangedSetOffsetsMilliseconds);
        var totalVisualUpdateMs = steps.Sum(static step => step.VisualUpdateMs);
        var totalRenderSchedulingMs = steps.Sum(static step => step.RenderSchedulingMs);
        var totalAnimationPhaseMs = steps.Sum(static step => step.AnimationPhaseMs);
        var totalHoverUpdateMs = steps.Sum(static step => step.PointerMove.HoverUpdateMilliseconds);
        var totalPointerResolveMs = steps.Sum(static step => step.PointerMove.PointerTargetResolveMilliseconds);
        var totalPointerRouteMs = steps.Sum(static step => step.PointerMove.PointerRouteMilliseconds);
        var totalLayoutPhaseMs = steps.Sum(static step => step.LayoutPhaseMs);
        var totalScrollOffsetCalls = steps.Sum(static step => step.ScrollViewer.SetOffsetCalls);
        var totalVerticalOffsetDelta = steps.Sum(static step => step.ScrollViewer.TotalVerticalDelta);
        var totalRetainedTraversals = steps.Sum(static step => step.RetainedTraversalCount);
        var totalDirtyRoots = steps.Sum(static step => step.DirtyRootCount);
        var totalBeginStoryboardCalls = steps.Sum(static step => step.BeginStoryboardCalls);
        var totalLaneApplications = steps.Sum(static step => step.LaneApplicationCount);
        var totalSinkValueSets = steps.Sum(static step => step.SinkValueSetCount);
        var totalStyleApplyMs = steps.Sum(static step => step.StyleApplyMs);
        var totalStyleApplySettersMs = steps.Sum(static step => step.StyleApplySettersMs);
        var totalStyleApplyTriggersMs = steps.Sum(static step => step.StyleApplyTriggersMs);
        var totalStyleCollectTriggeredValuesMs = steps.Sum(static step => step.StyleCollectTriggeredValuesMs);
        var totalStyleApplyTriggerActionsMs = steps.Sum(static step => step.StyleApplyTriggerActionsMs);
        var totalStyleInvokeActionsMs = steps.Sum(static step => step.StyleInvokeActionsMs);
        var totalTemplateReapplyMs = steps.Sum(static step => step.TemplateReapplyMs);
        var totalTemplateTriggerMatchMs = steps.Sum(static step => step.TemplateTriggerMatchMs);
        var totalTemplateSetterResolveMs = steps.Sum(static step => step.TemplateSetterResolveMs);
        var totalTemplateInvokeActionsMs = steps.Sum(static step => step.TemplateInvokeActionsMs);
        var totalVisualStateGoToStateMs = steps.Sum(static step => step.VisualStateGoToStateMs);
        var totalVisualStateTryGoToStateMs = steps.Sum(static step => step.VisualStateTryGoToStateMs);
        var totalVisualStateGroupTransitionMs = steps.Sum(static step => step.VisualStateGroupTransitionMs);
        var totalVisualStateApplySettersMs = steps.Sum(static step => step.VisualStateApplySettersMs);
        var totalVisualStateStoryboardMs = steps.Sum(static step => step.VisualStateStoryboardMs);
        var totalFreezableEndBatchMs = steps.Sum(static step => step.FreezableEndBatchMs);
        var totalFreezableInvalidationFlushMs = steps.Sum(static step => step.FreezableInvalidationFlushMs);

                var hotspotInference = totalValueChangedInstanceInvokeMs >= totalScrollBarOnValueChangedSyncTrackMs &&
                                                             totalValueChangedInstanceInvokeMs >= totalScrollViewerVerticalSetOffsetsMs &&
                                                             totalValueChangedInstanceInvokeMs >= totalTrackRefreshLayoutMutationMs
                        ? "Exact drag hotspot candidate: source ScrollBar ValueChanged instance delegate invocation dominates; class handlers and instance preparation are negligible."
                        : totalValueChangedRouteTraverseMs >= totalValueChangedRouteBuildMs &&
                            totalValueChangedRouteTraverseMs >= totalScrollBarOnValueChangedSyncTrackMs &&
                            totalValueChangedRouteTraverseMs >= totalScrollViewerVerticalSetOffsetsMs &&
                            totalValueChangedRouteTraverseMs >= totalTrackRefreshLayoutMutationMs
                        ? "Exact drag hotspot candidate: UIElement.RaiseRoutedEvent route traversal for the bubbled ValueChanged event dominates the captured drag chain."
                        : totalValueChangedRouteBuildMs >= totalScrollBarOnValueChangedSyncTrackMs &&
                            totalValueChangedRouteBuildMs >= totalScrollViewerVerticalSetOffsetsMs &&
                            totalValueChangedRouteBuildMs >= totalTrackRefreshLayoutMutationMs
                                ? "Exact drag hotspot candidate: UIElement.BuildRoute for the bubbled ValueChanged event dominates the captured drag chain."
                                : totalTrackRefreshLayoutMutationMs >= totalScrollBarOnValueChangedSyncTrackMs &&
                                    totalTrackRefreshLayoutMutationMs >= totalScrollViewerVerticalSetOffsetsMs &&
                                    totalTrackRefreshLayoutMutationMs >= totalTrackValueFromThumbTravelMs
                        ? "Exact drag hotspot candidate: Track.RefreshLayoutForStateMutation dominates inside Track.Value property change handling."
                                : totalScrollBarOnValueChangedSyncTrackMs >= totalScrollViewerVerticalSetOffsetsMs &&
                                    totalScrollBarOnValueChangedSyncTrackMs >= totalTrackValueFromThumbTravelMs &&
                                    totalScrollBarOnValueChangedSyncTrackMs >= totalScrollBarOnValueChangedBaseMs
                        ? "Exact drag hotspot candidate: ScrollBar.OnValueChanged -> SyncTrackState, with track sync/layout work dominating the captured drag chain."
                        : totalScrollViewerVerticalSetOffsetsMs >= totalTrackValueFromThumbTravelMs &&
                            totalScrollViewerVerticalSetOffsetsMs >= totalScrollBarOnValueChangedBaseMs
                                ? "Exact drag hotspot candidate: ScrollViewer.OnVerticalScrollBarValueChanged -> SetOffsets dominates the captured drag chain."
                                : totalTrackValueFromThumbTravelMs >= totalScrollBarOnValueChangedBaseMs
                                        ? "Exact drag hotspot candidate: Track.GetValueFromThumbTravel dominates the captured drag chain."
                                        : totalScrollBarOnValueChangedBaseMs > 0
                                                ? "Exact drag hotspot candidate: RangeBase value-changed routed event work dominates inside ScrollBar.OnValueChanged."
                                                : "Thumb drag dispatch still dominates as a whole; inspect the hottest chain segment totals below.";

        return new DragVariantSummary(
            steps[0].VariantName,
            steps.Count,
            totalVerticalOffsetDelta,
            steps.Sum(static step => step.PointerMove.PointerDispatchMilliseconds),
            totalPointerResolveMs,
            totalHoverUpdateMs,
            totalPointerRouteMs,
            totalPointerHandlerMs,
            totalThumbHandlePointerMoveMs,
            totalThumbRaiseDragDeltaMs,
            totalScrollBarThumbDragMs,
            totalScrollBarThumbValueSetMs,
            totalScrollBarOnValueChangedBaseMs,
            totalValueChangedRaiseMs,
            totalValueChangedRouteBuildMs,
            totalValueChangedRouteTraverseMs,
            totalValueChangedClassHandlerMs,
            totalValueChangedInstanceDispatchMs,
            totalValueChangedInstancePrepareMs,
            totalValueChangedInstanceInvokeMs,
            maxValueChangedRouteLength,
            totalScrollBarOnValueChangedSyncTrackMs,
            totalScrollBarSyncTrackMs,
            totalScrollBarRefreshTrackLayoutMs,
            totalTrackValueFromThumbTravelMs,
            totalTrackRefreshLayoutMutationMs,
            totalTrackRefreshLayoutValueMutationMs,
            totalTrackRefreshLayoutViewportMutationMs,
            totalTrackRefreshLayoutMinimumMutationMs,
            totalTrackRefreshLayoutMaximumMutationMs,
            totalTrackRefreshLayoutDirectionMutationMs,
            totalTrackRefreshLayoutSnapshotMs,
            totalTrackRefreshLayoutInvalidateArrangeMs,
            totalTrackRefreshLayoutArrangeMs,
            totalTrackRefreshLayoutDirtyBoundsMs,
            totalTrackRefreshLayoutVisualInvalidateMs,
            totalTrackRefreshLayoutNeedsMeasureFallbackMs,
            totalTrackRefreshLayoutDirtyBoundsHints,
            totalTrackRefreshLayoutVisualFallbacks,
            totalPanelMeasureMs,
            totalPanelArrangeMs,
            totalPanelMeasureCalls,
            totalPanelArrangeCalls,
            totalPanelMeasuredChildren,
            totalPanelArrangedChildren,
            totalStackPanelMeasureMs,
            totalStackPanelArrangeMs,
            totalStackPanelMeasureCalls,
            totalStackPanelArrangeCalls,
            totalStackPanelMeasuredChildren,
            totalStackPanelArrangedChildren,
            totalButtonMeasureMs,
            totalButtonRenderMs,
            totalButtonResolveTextLayoutMs,
            totalButtonRenderChromeMs,
            totalButtonRenderTextPreparationMs,
            totalButtonRenderTextDrawDispatchMs,
            totalButtonRenderTextPreparationCalls,
            totalButtonRenderTextDrawDispatchCalls,
            totalScrollViewerVerticalValueChangedMs,
            totalScrollViewerVerticalSetOffsetsMs,
            totalRenderSchedulingMs,
            totalVisualUpdateMs,
            totalLayoutPhaseMs,
            totalAnimationPhaseMs,
            totalRetainedTraversals,
            totalDirtyRoots,
            steps.Sum(static step => step.RenderInvalidationCount),
            steps.Sum(static step => step.MeasureInvalidationCount),
            steps.Sum(static step => step.ArrangeInvalidationCount),
            totalScrollOffsetCalls,
            steps.Sum(static step => step.ScrollViewer.SetOffsetNoOpCalls),
            steps.Sum(static step => step.PointerMove.HitTestCount),
            steps.Sum(static step => step.PointerMove.RoutedEventCount),
            totalBeginStoryboardCalls,
            totalLaneApplications,
            totalSinkValueSets,
            totalStyleApplyMs,
            totalStyleApplySettersMs,
            totalStyleApplyTriggersMs,
            totalStyleCollectTriggeredValuesMs,
            steps.Sum(static step => step.StyleTriggerMatchCount),
            steps.Sum(static step => step.StyleMatchedTriggerCount),
            steps.Sum(static step => step.StyleTriggerSetCount),
            steps.Sum(static step => step.StyleTriggerClearCount),
            totalStyleApplyTriggerActionsMs,
            totalStyleInvokeActionsMs,
            totalTemplateReapplyMs,
            totalTemplateTriggerMatchMs,
            totalTemplateSetterResolveMs,
            steps.Sum(static step => step.TemplateMatchedTriggerCount),
            steps.Sum(static step => step.TemplateSetValueCount),
            steps.Sum(static step => step.TemplateClearValueCount),
            totalTemplateInvokeActionsMs,
            totalVisualStateGoToStateMs,
            totalVisualStateTryGoToStateMs,
            totalVisualStateGroupTransitionMs,
            totalVisualStateApplySettersMs,
            totalVisualStateStoryboardMs,
            steps.Sum(static step => step.VisualStateSetValueCount),
            steps.Sum(static step => step.VisualStateClearValueCount),
            totalFreezableEndBatchMs,
            totalFreezableInvalidationFlushMs,
            steps.Max(static step => step.DirtyRootCount),
            $"step-{hottest.StepIndex:000}",
                $"hovered={hottest.HoveredType}:{hottest.HoveredName}, verticalDelta={hottest.ScrollViewer.TotalVerticalDelta:0.###}, pointerResolvePath={hottest.PointerMove.PointerResolvePath}, pointerHandlerMs={hottest.PointerMove.PointerMoveHandlerMilliseconds:0.###}, thumbHandleMs={hottest.Thumb.HandlePointerMoveMilliseconds:0.###}, thumbRaiseDragDeltaMs={hottest.Thumb.RaiseDragDeltaMilliseconds:0.###}, scrollBarThumbDragMs={hottest.ScrollBar.OnThumbDragDeltaMilliseconds:0.###}, scrollBarThumbValueSetMs={hottest.ScrollBar.OnThumbDragDeltaValueSetMilliseconds:0.###}, scrollBarOnValueChangedBaseMs={hottest.ScrollBar.OnValueChangedBaseMilliseconds:0.###}, valueChangedRaiseMs={hottest.ValueChanged.RaiseMilliseconds:0.###}, valueChangedRouteBuildMs={hottest.ValueChanged.RouteBuildMilliseconds:0.###}, valueChangedRouteTraverseMs={hottest.ValueChanged.RouteTraverseMilliseconds:0.###}, valueChangedClassHandlersMs={hottest.ValueChanged.ClassHandlerMilliseconds:0.###}, valueChangedInstanceDispatchMs={hottest.ValueChanged.InstanceDispatchMilliseconds:0.###}, valueChangedInstancePrepareMs={hottest.ValueChanged.InstancePrepareMilliseconds:0.###}, valueChangedInstanceInvokeMs={hottest.ValueChanged.InstanceInvokeMilliseconds:0.###}, valueChangedMaxRouteLength={hottest.ValueChanged.MaxRouteLength}, scrollBarOnValueChangedSyncTrackMs={hottest.ScrollBar.OnValueChangedSyncTrackStateMilliseconds:0.###}, scrollBarSyncTrackMs={hottest.ScrollBar.SyncTrackStateMilliseconds:0.###}, trackValueFromTravelMs={hottest.Track.GetValueFromThumbTravelMilliseconds:0.###}, trackRefreshLayoutMs={hottest.Track.RefreshLayoutForStateMutationMilliseconds:0.###}, trackRefreshValueMutationMs={hottest.Track.RefreshLayoutValueMutationMilliseconds:0.###}, trackRefreshSnapshotMs={hottest.Track.RefreshLayoutCaptureSnapshotMilliseconds:0.###}, trackRefreshInvalidateArrangeMs={hottest.Track.RefreshLayoutInvalidateArrangeMilliseconds:0.###}, trackRefreshArrangeMs={hottest.Track.RefreshLayoutArrangeMilliseconds:0.###}, trackRefreshDirtyBoundsMs={hottest.Track.RefreshLayoutDirtyBoundsMilliseconds:0.###}, trackRefreshVisualInvalidateMs={hottest.Track.RefreshLayoutVisualInvalidationMilliseconds:0.###}, panelMeasureMs={hottest.Panel.MeasureMilliseconds:0.###}, panelArrangeMs={hottest.Panel.ArrangeMilliseconds:0.###}, panelMeasureCalls={hottest.Panel.MeasureCallCount}, panelArrangeCalls={hottest.Panel.ArrangeCallCount}, stackPanelMeasureMs={hottest.StackPanel.MeasureMilliseconds:0.###}, stackPanelArrangeMs={hottest.StackPanel.ArrangeMilliseconds:0.###}, stackPanelMeasureCalls={hottest.StackPanel.MeasureCallCount}, stackPanelArrangeCalls={hottest.StackPanel.ArrangeCallCount}, buttonMeasureMs={hottest.ButtonMeasureMs:0.###}, buttonRenderMs={hottest.ButtonRenderMs:0.###}, buttonRenderTextPrepCalls={hottest.ButtonRenderTextPreparationCalls}, buttonRenderTextDrawCalls={hottest.ButtonRenderTextDrawDispatchCalls}, scrollViewerVerticalSetOffsetsMs={hottest.ScrollViewerValueChanged.VerticalValueChangedSetOffsetsMilliseconds:0.###}, renderSchedulingMs={hottest.RenderSchedulingMs:0.###}, visualUpdateMs={hottest.VisualUpdateMs:0.###}, layoutPhaseMs={hottest.LayoutPhaseMs:0.###}, animationPhaseMs={hottest.AnimationPhaseMs:0.###}, styleApplyMs={hottest.StyleApplyMs:0.###}, styleApplyTriggersMs={hottest.StyleApplyTriggersMs:0.###}, templateReapplyMs={hottest.TemplateReapplyMs:0.###}, visualStateGoToStateMs={hottest.VisualStateGoToStateMs:0.###}, visualStateStoryboardMs={hottest.VisualStateStoryboardMs:0.###}, retainedTraversals={hottest.RetainedTraversalCount}, dirtyRoots={hottest.DirtyRootCount}, beginStoryboards={hottest.BeginStoryboardCalls}, laneApplications={hottest.LaneApplicationCount}, freezableEndBatchMs={hottest.FreezableEndBatchMs:0.###}, freezableInvalidationFlushMs={hottest.FreezableInvalidationFlushMs:0.###}, hottestSetValuePath={hottest.HottestSetValuePathSummary}",
            hotspotInference);
    }

    private static string Compare(DragVariantSummary left, DragVariantSummary right)
    {
        var visualDelta = left.TotalVisualUpdateMs - right.TotalVisualUpdateMs;
        var animationDelta = left.TotalAnimationPhaseMs - right.TotalAnimationPhaseMs;
        var routeDelta = left.TotalPointerRouteMs - right.TotalPointerRouteMs;
        var dirtyRootDelta = left.TotalDirtyRoots - right.TotalDirtyRoots;

        return $"pointer-on-thumb minus pointer-outside-scrollviewer: visualUpdateMs={visualDelta:0.###}, animationPhaseMs={animationDelta:0.###}, pointerRouteMs={routeDelta:0.###}, dirtyRoots={dirtyRootDelta}";
    }

    private static void ResetForNextVariant(ControlsCatalogView catalog, UiRoot uiRoot)
    {
        _ = catalog;
        AnimationManager.Current.ResetForTests();
        VisualTreeHelper.ResetInstrumentationForTests();
        Freezable.ResetTelemetryForTests();
        UIElement.ResetFreezableInvalidationBatchTelemetryForTests();
        Style.ResetTelemetryForTests();
        TemplateTriggerEngine.ResetTelemetryForTests();
        VisualStateManager.ResetTelemetryForTests();
        RunFrame(uiRoot, 32);
    }

    private static string FormatStep(DragStepMetrics step)
    {
        return
            $"variant={step.VariantName} step={step.StepIndex:000} pointer=({step.Pointer.X:0.##},{step.Pointer.Y:0.##}) hovered={step.HoveredType}:{step.HoveredName} " +
            $"resolvePath={step.PointerMove.PointerResolvePath} pointerDispatchMs={step.PointerMove.PointerDispatchMilliseconds:0.###} resolveMs={step.PointerMove.PointerTargetResolveMilliseconds:0.###} hoverMs={step.PointerMove.HoverUpdateMilliseconds:0.###} routeMs={step.PointerMove.PointerRouteMilliseconds:0.###} handlerMs={step.PointerMove.PointerMoveHandlerMilliseconds:0.###} " +
            $"hitTests={step.PointerMove.HitTestCount} routedEvents={step.PointerMove.RoutedEventCount} thumbHandleMs={step.Thumb.HandlePointerMoveMilliseconds:0.###} thumbRaiseDragDeltaMs={step.Thumb.RaiseDragDeltaMilliseconds:0.###} scrollBarThumbDragMs={step.ScrollBar.OnThumbDragDeltaMilliseconds:0.###} scrollBarThumbValueSetMs={step.ScrollBar.OnThumbDragDeltaValueSetMilliseconds:0.###} scrollBarOnValueChangedBaseMs={step.ScrollBar.OnValueChangedBaseMilliseconds:0.###} valueChangedRaiseMs={step.ValueChanged.RaiseMilliseconds:0.###} valueChangedRouteBuildMs={step.ValueChanged.RouteBuildMilliseconds:0.###} valueChangedRouteTraverseMs={step.ValueChanged.RouteTraverseMilliseconds:0.###} valueChangedClassHandlersMs={step.ValueChanged.ClassHandlerMilliseconds:0.###} valueChangedInstanceDispatchMs={step.ValueChanged.InstanceDispatchMilliseconds:0.###} valueChangedInstancePrepareMs={step.ValueChanged.InstancePrepareMilliseconds:0.###} valueChangedInstanceInvokeMs={step.ValueChanged.InstanceInvokeMilliseconds:0.###} valueChangedMaxRouteLength={step.ValueChanged.MaxRouteLength} scrollBarOnValueChangedSyncTrackMs={step.ScrollBar.OnValueChangedSyncTrackStateMilliseconds:0.###} scrollBarSyncTrackMs={step.ScrollBar.SyncTrackStateMilliseconds:0.###} scrollBarRefreshTrackLayoutMs={step.ScrollBar.RefreshTrackLayoutMilliseconds:0.###} trackValueFromTravelMs={step.Track.GetValueFromThumbTravelMilliseconds:0.###} trackRefreshLayoutMs={step.Track.RefreshLayoutForStateMutationMilliseconds:0.###} trackRefreshValueMutationMs={step.Track.RefreshLayoutValueMutationMilliseconds:0.###} trackRefreshViewportMutationMs={step.Track.RefreshLayoutViewportMutationMilliseconds:0.###} trackRefreshSnapshotMs={step.Track.RefreshLayoutCaptureSnapshotMilliseconds:0.###} trackRefreshInvalidateArrangeMs={step.Track.RefreshLayoutInvalidateArrangeMilliseconds:0.###} trackRefreshArrangeMs={step.Track.RefreshLayoutArrangeMilliseconds:0.###} trackRefreshDirtyBoundsMs={step.Track.RefreshLayoutDirtyBoundsMilliseconds:0.###} trackRefreshVisualInvalidateMs={step.Track.RefreshLayoutVisualInvalidationMilliseconds:0.###} trackRefreshDirtyBoundsHints={step.Track.RefreshLayoutDirtyBoundsHintCount} trackRefreshVisualFallbacks={step.Track.RefreshLayoutVisualFallbackCount} panelMeasureMs={step.Panel.MeasureMilliseconds:0.###} panelArrangeMs={step.Panel.ArrangeMilliseconds:0.###} panelMeasureCalls={step.Panel.MeasureCallCount} panelArrangeCalls={step.Panel.ArrangeCallCount} panelMeasuredChildren={step.Panel.MeasuredChildCount} panelArrangedChildren={step.Panel.ArrangedChildCount} stackPanelMeasureMs={step.StackPanel.MeasureMilliseconds:0.###} stackPanelArrangeMs={step.StackPanel.ArrangeMilliseconds:0.###} stackPanelMeasureCalls={step.StackPanel.MeasureCallCount} stackPanelArrangeCalls={step.StackPanel.ArrangeCallCount} stackPanelMeasuredChildren={step.StackPanel.MeasuredChildCount} stackPanelArrangedChildren={step.StackPanel.ArrangedChildCount} buttonMeasureMs={step.ButtonMeasureMs:0.###} buttonRenderMs={step.ButtonRenderMs:0.###} buttonResolveTextLayoutMs={step.ButtonResolveTextLayoutMs:0.###} buttonRenderChromeMs={step.ButtonRenderChromeMs:0.###} buttonRenderTextPrepMs={step.ButtonRenderTextPreparationMs:0.###} buttonRenderTextDrawMs={step.ButtonRenderTextDrawDispatchMs:0.###} buttonRenderTextPrepCalls={step.ButtonRenderTextPreparationCalls} buttonRenderTextDrawCalls={step.ButtonRenderTextDrawDispatchCalls} scrollViewerVerticalValueChangedMs={step.ScrollViewerValueChanged.VerticalValueChangedMilliseconds:0.###} scrollViewerVerticalSetOffsetsMs={step.ScrollViewerValueChanged.VerticalValueChangedSetOffsetsMilliseconds:0.###} scrollSetOffsets={step.ScrollViewer.SetOffsetCalls} scrollNoOps={step.ScrollViewer.SetOffsetNoOpCalls} verticalDelta={step.ScrollViewer.TotalVerticalDelta:0.###} " +
            $"inputPhaseMs={step.InputPhaseMs:0.###} layoutPhaseMs={step.LayoutPhaseMs:0.###} animationPhaseMs={step.AnimationPhaseMs:0.###} renderSchedulingMs={step.RenderSchedulingMs:0.###} visualUpdateMs={step.VisualUpdateMs:0.###} " +
            $"retainedQueueCompactionMs={step.RetainedQueueCompactionMs:0.###} retainedCandidateCoalescingMs={step.RetainedCandidateCoalescingMs:0.###} retainedSubtreeUpdateMs={step.RetainedSubtreeUpdateMs:0.###} retainedShallowSyncMs={step.RetainedShallowSyncMs:0.###} retainedDeepSyncMs={step.RetainedDeepSyncMs:0.###} retainedAncestorRefreshMs={step.RetainedAncestorRefreshMs:0.###} retainedForceDeepSyncCount={step.RetainedForceDeepSyncCount} retainedForcedDeepDowngradeBlockedCount={step.RetainedForcedDeepDowngradeBlockedCount} retainedShallowSuccessCount={step.RetainedShallowSuccessCount} retainedShallowRejectRenderStateCount={step.RetainedShallowRejectRenderStateCount} retainedShallowRejectVisibilityCount={step.RetainedShallowRejectVisibilityCount} retainedShallowRejectStructureCount={step.RetainedShallowRejectStructureCount} retainedOverlapForcedDeepCount={step.RetainedOverlapForcedDeepCount} " +
            $"styleApplyMs={step.StyleApplyMs:0.###} styleApplySettersMs={step.StyleApplySettersMs:0.###} styleApplyTriggersMs={step.StyleApplyTriggersMs:0.###} styleCollectTriggeredValuesMs={step.StyleCollectTriggeredValuesMs:0.###} styleTriggerActionsMs={step.StyleApplyTriggerActionsMs:0.###} styleInvokeActionsMs={step.StyleInvokeActionsMs:0.###} styleTriggerMatches={step.StyleTriggerMatchCount} styleMatchedTriggers={step.StyleMatchedTriggerCount} styleTriggerSets={step.StyleTriggerSetCount} styleTriggerClears={step.StyleTriggerClearCount} " +
            $"templateReapplyMs={step.TemplateReapplyMs:0.###} templateMatchMs={step.TemplateTriggerMatchMs:0.###} templateSetterResolveMs={step.TemplateSetterResolveMs:0.###} templateMatchedTriggers={step.TemplateMatchedTriggerCount} templateSets={step.TemplateSetValueCount} templateClears={step.TemplateClearValueCount} templateInvokeActionsMs={step.TemplateInvokeActionsMs:0.###} " +
            $"visualStateGoToStateMs={step.VisualStateGoToStateMs:0.###} visualStateTryGoToStateMs={step.VisualStateTryGoToStateMs:0.###} visualStateGroupTransitionMs={step.VisualStateGroupTransitionMs:0.###} visualStateApplySettersMs={step.VisualStateApplySettersMs:0.###} visualStateStoryboardMs={step.VisualStateStoryboardMs:0.###} visualStateSets={step.VisualStateSetValueCount} visualStateClears={step.VisualStateClearValueCount} " +
            $"retainedTraversals={step.RetainedTraversalCount} dirtyRoots={step.DirtyRootCount} retainedVisited={step.RetainedNodesVisited} retainedDrawn={step.RetainedNodesDrawn} measureInvalidations={step.MeasureInvalidationCount} arrangeInvalidations={step.ArrangeInvalidationCount} renderInvalidations={step.RenderInvalidationCount} " +
            $"beginStoryboards={step.BeginStoryboardCalls} storyboardStarts={step.StoryboardStarts} activeStoryboards={step.ActiveStoryboardCount} activeLanes={step.ActiveLaneCount} laneApplications={step.LaneApplicationCount} sinkValueSets={step.SinkValueSetCount} composeMs={step.ComposeMs:0.###} composeApplyMs={step.ComposeApplyMs:0.###} composeBatchEndMs={step.ComposeBatchEndMs:0.###} sinkSetValueMs={step.SinkSetValueMs:0.###} hottestSetValuePath={step.HottestSetValuePathSummary} " +
            $"freezableEndBatchMs={step.FreezableEndBatchMs:0.###} freezableOnChangedMs={step.FreezableOnChangedMs:0.###} freezableEndBatchFlushCount={step.FreezableEndBatchFlushCount} freezableHottestEndBatchType={step.FreezableHottestEndBatchType} freezableInvalidationFlushCount={step.FreezableInvalidationFlushCount} freezableInvalidationFlushMs={step.FreezableInvalidationFlushMs:0.###} freezableLastFlushTargets={step.FreezableInvalidationLastFlushTargets} " +
            $"neighborProbes={step.ItemsPresenterNeighborProbes} fullFallbackScans={step.ItemsPresenterFullFallbackScans} legacyFallbacks={step.LegacyEnumerableFallbacks} monotonicFastPaths={step.MonotonicPanelFastPathCount}";
    }

    private static List<Vector2> BuildDragPoints(float pointerX, float topY, float bottomY, int stepsPerDirection)
    {
        var points = new List<Vector2>(stepsPerDirection * 2);
        for (var i = 1; i <= stepsPerDirection; i++)
        {
            var progress = i / (float)stepsPerDirection;
            points.Add(new Vector2(pointerX, MathHelper.Lerp(topY, bottomY, progress)));
        }

        for (var i = 1; i <= stepsPerDirection; i++)
        {
            var progress = i / (float)stepsPerDirection;
            points.Add(new Vector2(pointerX, MathHelper.Lerp(bottomY, topY, progress)));
        }

        return points;
    }

    private static ScrollViewer FindSidebarScrollViewer(ControlsCatalogView catalog)
    {
        return FindFirstVisualChild<ScrollViewer>(
                   catalog,
                   static viewer => viewer.Content is StackPanel host &&
                                    string.Equals(host.Name, "ControlButtonsHost", StringComparison.Ordinal))
               ?? throw new InvalidOperationException("Could not find sidebar ScrollViewer.");
    }

    private static LayoutRect GetViewerViewportRect(ScrollViewer viewer)
    {
        if (viewer.TryGetContentViewportClipRect(out var viewport))
        {
            return viewport;
        }

        throw new InvalidOperationException("Sidebar ScrollViewer did not expose a viewport.");
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

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
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
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunFrame(UiRoot uiRoot, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, ViewportWidth, ViewportHeight));
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

    private static string FormatRect(LayoutRect rect)
    {
        return $"({rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##})";
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    private readonly record struct DragStepMetrics(
        string VariantName,
        int StepIndex,
        Vector2 Pointer,
        string HoveredType,
        string HoveredName,
        UiPointerMoveTelemetrySnapshot PointerMove,
        ThumbDragTelemetrySnapshot Thumb,
        ScrollBarThumbDragTelemetrySnapshot ScrollBar,
        TrackThumbTravelTelemetrySnapshot Track,
        PanelTelemetrySnapshot Panel,
        StackPanelTelemetrySnapshot StackPanel,
        double ButtonMeasureMs,
        double ButtonRenderMs,
        double ButtonResolveTextLayoutMs,
        double ButtonRenderChromeMs,
        double ButtonRenderTextPreparationMs,
        double ButtonRenderTextDrawDispatchMs,
        int ButtonRenderTextPreparationCalls,
        int ButtonRenderTextDrawDispatchCalls,
        ValueChangedRoutedEventTelemetrySnapshot ValueChanged,
        ScrollViewerScrollMetricsSnapshot ScrollViewer,
        ScrollViewerValueChangedTelemetrySnapshot ScrollViewerValueChanged,
        AnimationSinkTelemetrySnapshot SinkTelemetry,
        double InputPhaseMs,
        double LayoutPhaseMs,
        double AnimationPhaseMs,
        double RenderSchedulingMs,
        double VisualUpdateMs,
        double RetainedQueueCompactionMs,
        double RetainedCandidateCoalescingMs,
        double RetainedSubtreeUpdateMs,
        double RetainedShallowSyncMs,
        double RetainedDeepSyncMs,
        double RetainedAncestorRefreshMs,
        int RetainedForceDeepSyncCount,
        int RetainedForcedDeepDowngradeBlockedCount,
        int RetainedShallowSuccessCount,
        int RetainedShallowRejectRenderStateCount,
        int RetainedShallowRejectVisibilityCount,
        int RetainedShallowRejectStructureCount,
        int RetainedOverlapForcedDeepCount,
        int BeginStoryboardCalls,
        int StoryboardStarts,
        int ActiveStoryboardCount,
        int ActiveLaneCount,
        int LaneApplicationCount,
        int SinkValueSetCount,
        double ComposeMs,
        double ComposeCollectMs,
        double ComposeSortMs,
        double ComposeMergeMs,
        double ComposeApplyMs,
        double ComposeBatchEndMs,
        double SinkSetValueMs,
        string HottestSetValuePathSummary,
        double StyleApplyMs,
        double StyleApplySettersMs,
        double StyleApplyTriggersMs,
        double StyleCollectTriggeredValuesMs,
        long StyleTriggerMatchCount,
        long StyleMatchedTriggerCount,
        long StyleTriggerSetCount,
        long StyleTriggerClearCount,
        double StyleApplyTriggerActionsMs,
        double StyleInvokeActionsMs,
        double TemplateReapplyMs,
        double TemplateTriggerMatchMs,
        double TemplateSetterResolveMs,
        long TemplateMatchedTriggerCount,
        long TemplateSetValueCount,
        long TemplateClearValueCount,
        double TemplateInvokeActionsMs,
        double VisualStateGoToStateMs,
        double VisualStateTryGoToStateMs,
        double VisualStateGroupTransitionMs,
        double VisualStateApplySettersMs,
        double VisualStateStoryboardMs,
        long VisualStateSetValueCount,
        long VisualStateClearValueCount,
        double FreezableEndBatchMs,
        double FreezableOnChangedMs,
        int FreezableEndBatchFlushCount,
        string FreezableHottestEndBatchType,
        int FreezableInvalidationFlushCount,
        double FreezableInvalidationFlushMs,
        string FreezableInvalidationLastFlushTargets,
        int DirtyRootCount,
        int RetainedTraversalCount,
        int RetainedNodesVisited,
        int RetainedNodesDrawn,
        long MeasureInvalidationCount,
        long ArrangeInvalidationCount,
        long RenderInvalidationCount,
        int ItemsPresenterNeighborProbes,
        int ItemsPresenterFullFallbackScans,
        int LegacyEnumerableFallbacks,
        int MonotonicPanelFastPathCount);

    private readonly record struct DragVariantSummary(
        string VariantName,
        int TotalSteps,
        float TotalVerticalOffsetDelta,
        double TotalPointerDispatchMs,
        double TotalPointerResolveMs,
        double TotalHoverUpdateMs,
        double TotalPointerRouteMs,
        double TotalPointerMoveHandlerMs,
        double TotalThumbHandlePointerMoveMs,
        double TotalThumbRaiseDragDeltaMs,
        double TotalScrollBarThumbDragMs,
        double TotalScrollBarThumbValueSetMs,
        double TotalScrollBarOnValueChangedBaseMs,
        double TotalValueChangedRaiseMs,
        double TotalValueChangedRouteBuildMs,
        double TotalValueChangedRouteTraverseMs,
        double TotalValueChangedClassHandlerMs,
        double TotalValueChangedInstanceDispatchMs,
        double TotalValueChangedInstancePrepareMs,
        double TotalValueChangedInstanceInvokeMs,
        int MaxValueChangedRouteLength,
        double TotalScrollBarOnValueChangedSyncTrackMs,
        double TotalScrollBarSyncTrackMs,
        double TotalScrollBarRefreshTrackLayoutMs,
        double TotalTrackValueFromThumbTravelMs,
        double TotalTrackRefreshLayoutMutationMs,
        double TotalTrackRefreshLayoutValueMutationMs,
        double TotalTrackRefreshLayoutViewportMutationMs,
        double TotalTrackRefreshLayoutMinimumMutationMs,
        double TotalTrackRefreshLayoutMaximumMutationMs,
        double TotalTrackRefreshLayoutDirectionMutationMs,
        double TotalTrackRefreshLayoutSnapshotMs,
        double TotalTrackRefreshLayoutInvalidateArrangeMs,
        double TotalTrackRefreshLayoutArrangeMs,
        double TotalTrackRefreshLayoutDirtyBoundsMs,
        double TotalTrackRefreshLayoutVisualInvalidateMs,
        double TotalTrackRefreshLayoutNeedsMeasureFallbackMs,
        int TotalTrackRefreshLayoutDirtyBoundsHints,
        int TotalTrackRefreshLayoutVisualFallbacks,
        double TotalPanelMeasureMs,
        double TotalPanelArrangeMs,
        int TotalPanelMeasureCalls,
        int TotalPanelArrangeCalls,
        int TotalPanelMeasuredChildren,
        int TotalPanelArrangedChildren,
        double TotalStackPanelMeasureMs,
        double TotalStackPanelArrangeMs,
        int TotalStackPanelMeasureCalls,
        int TotalStackPanelArrangeCalls,
        int TotalStackPanelMeasuredChildren,
        int TotalStackPanelArrangedChildren,
        double TotalButtonMeasureMs,
        double TotalButtonRenderMs,
        double TotalButtonResolveTextLayoutMs,
        double TotalButtonRenderChromeMs,
        double TotalButtonRenderTextPreparationMs,
        double TotalButtonRenderTextDrawDispatchMs,
        int TotalButtonRenderTextPreparationCalls,
        int TotalButtonRenderTextDrawDispatchCalls,
        double TotalScrollViewerVerticalValueChangedMs,
        double TotalScrollViewerVerticalSetOffsetsMs,
        double TotalRenderSchedulingMs,
        double TotalVisualUpdateMs,
        double TotalLayoutPhaseMs,
        double TotalAnimationPhaseMs,
        int TotalRetainedTraversals,
        int TotalDirtyRoots,
        long TotalRenderInvalidations,
        long TotalMeasureInvalidations,
        long TotalArrangeInvalidations,
        int TotalScrollViewerSetOffsetCalls,
        int TotalScrollViewerSetOffsetNoOps,
        int TotalHitTests,
        int TotalRoutedEvents,
        int TotalBeginStoryboardCalls,
        int TotalLaneApplications,
        int TotalSinkValueSets,
        double TotalStyleApplyMs,
        double TotalStyleApplySettersMs,
        double TotalStyleApplyTriggersMs,
        double TotalStyleCollectTriggeredValuesMs,
        long TotalStyleTriggerMatches,
        long TotalStyleMatchedTriggers,
        long TotalStyleTriggerSets,
        long TotalStyleTriggerClears,
        double TotalStyleApplyTriggerActionsMs,
        double TotalStyleInvokeActionsMs,
        double TotalTemplateReapplyMs,
        double TotalTemplateTriggerMatchMs,
        double TotalTemplateSetterResolveMs,
        long TotalTemplateMatchedTriggers,
        long TotalTemplateSetValues,
        long TotalTemplateClears,
        double TotalTemplateInvokeActionsMs,
        double TotalVisualStateGoToStateMs,
        double TotalVisualStateTryGoToStateMs,
        double TotalVisualStateGroupTransitionMs,
        double TotalVisualStateApplySettersMs,
        double TotalVisualStateStoryboardMs,
        long TotalVisualStateSetValues,
        long TotalVisualStateClears,
        double TotalFreezableEndBatchMs,
        double TotalFreezableInvalidationFlushMs,
        int MaxDirtyRootsSingleStep,
        string HottestStepLabel,
        string HottestStepDetail,
        string HotspotInference);
}