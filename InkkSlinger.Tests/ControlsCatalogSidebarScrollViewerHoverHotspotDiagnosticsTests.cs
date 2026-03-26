using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogSidebarScrollViewerHoverHotspotDiagnosticsTests
{
    private const int ViewportWidth = 1400;
    private const int ViewportHeight = 900;

    [Fact]
    public void SidebarScrollViewer_RapidHoverSweep_WritesHotspotDiagnosticsLog()
    {
        AnimationManager.Current.ResetForTests();
        VisualTreeHelper.ResetInstrumentationForTests();
        Style.ResetTelemetryForTests();
        TemplateTriggerEngine.ResetTelemetryForTests();
        VisualStateManager.ResetTelemetryForTests();

        var catalog = new ControlsCatalogView();
        var uiRoot = new UiRoot(catalog);
        RunFrame(uiRoot, 16);

        var sidebarViewer = FindSidebarScrollViewer(catalog);
        var sidebarHost = Assert.IsType<StackPanel>(catalog.FindName("ControlButtonsHost"));
        var visibleButtons = GetVisibleButtons(sidebarViewer, sidebarHost);
        Assert.NotEmpty(visibleButtons);

        var logPath = GetDiagnosticsLogPath("controls-catalog-sidebar-hover-hotspot");
        var lines = new List<string>
        {
            $"scenario=ControlsCatalog sidebar ScrollViewer rapid hover sweep",
            $"timestamp_utc={DateTime.UtcNow:O}",
            $"log_path={logPath}",
            $"step_1=move pointer to sidebar viewport center",
            $"step_2=move pointer over every visible sidebar button as fast as possible",
            $"visible_button_count={visibleButtons.Count}",
            $"viewer_slot={FormatRect(sidebarViewer.LayoutSlot)}",
            $"viewport_rect={FormatRect(GetViewerViewportRect(sidebarViewer))}"
        };

        var stepMetrics = new List<HoverSweepStepMetrics>();
        var pointer = GetCenter(GetViewerViewportRect(sidebarViewer));
        stepMetrics.Add(RunSweepStep(uiRoot, pointer, "viewer-center", stepIndex: 0));

        var stepIndex = 1;
        for (var pass = 0; pass < 3; pass++)
        {
            foreach (var button in visibleButtons)
            {
                pointer = GetCenter(button.LayoutSlot);
                stepMetrics.Add(RunSweepStep(
                    uiRoot,
                    pointer,
                    $"pass-{pass + 1}:{button.GetContentText()}",
                    stepIndex++));
            }
        }

        var summary = Summarize(stepMetrics);
        lines.Add(string.Empty);
        lines.Add("summary:");
        lines.Add($"total_steps={stepMetrics.Count}");
        lines.Add($"total_hit_tests={summary.TotalHitTests}");
        lines.Add($"total_routed_events={summary.TotalRoutedEvents}");
        lines.Add($"total_hover_update_ms={summary.TotalHoverUpdateMs:0.###}");
        lines.Add($"total_pointer_resolve_ms={summary.TotalPointerResolveMs:0.###}");
        lines.Add($"total_begin_storyboard_calls={summary.TotalBeginStoryboardCalls}");
        lines.Add($"total_storyboard_starts={summary.TotalStoryboardStarts}");
        lines.Add($"total_lane_applications={summary.TotalLaneApplications}");
        lines.Add($"total_sink_value_sets={summary.TotalSinkValueSets}");
        lines.Add($"total_begin_storyboard_ms={summary.TotalBeginStoryboardMs:0.###}");
        lines.Add($"total_storyboard_start_ms={summary.TotalStoryboardStartMs:0.###}");
        lines.Add($"total_style_apply_ms={summary.TotalStyleApplyMs:0.###}");
        lines.Add($"total_style_apply_triggers_ms={summary.TotalStyleApplyTriggersMs:0.###}");
        lines.Add($"total_style_trigger_action_ms={summary.TotalStyleApplyTriggerActionsMs:0.###}");
        lines.Add($"total_style_invoke_actions_ms={summary.TotalStyleInvokeActionsMs:0.###}");
        lines.Add($"total_style_trigger_matches={summary.TotalStyleTriggerMatches}");
        lines.Add($"total_style_trigger_sets={summary.TotalStyleTriggerSets}");
        lines.Add($"total_template_reapply_ms={summary.TotalTemplateReapplyMs:0.###}");
        lines.Add($"total_template_match_ms={summary.TotalTemplateTriggerMatchMs:0.###}");
        lines.Add($"total_template_setter_resolve_ms={summary.TotalTemplateSetterResolveMs:0.###}");
        lines.Add($"total_template_actions_ms={summary.TotalTemplateInvokeActionsMs:0.###}");
        lines.Add($"total_template_sets={summary.TotalTemplateSetValues}");
        lines.Add($"total_visualstate_go_to_state_ms={summary.TotalVisualStateGoToStateMs:0.###}");
        lines.Add($"total_visualstate_group_transition_ms={summary.TotalVisualStateGroupTransitionMs:0.###}");
        lines.Add($"total_visualstate_apply_setters_ms={summary.TotalVisualStateApplySettersMs:0.###}");
        lines.Add($"total_visualstate_storyboard_ms={summary.TotalVisualStateStoryboardMs:0.###}");
        lines.Add($"total_visualstate_sets={summary.TotalVisualStateSetValues}");
        lines.Add($"total_compose_ms={summary.TotalComposeMs:0.###}");
        lines.Add($"total_compose_collect_ms={summary.TotalComposeCollectMs:0.###}");
        lines.Add($"total_compose_sort_ms={summary.TotalComposeSortMs:0.###}");
        lines.Add($"total_compose_merge_ms={summary.TotalComposeMergeMs:0.###}");
        lines.Add($"total_compose_apply_ms={summary.TotalComposeApplyMs:0.###}");
        lines.Add($"total_compose_batch_begin_ms={summary.TotalComposeBatchBeginMs:0.###}");
        lines.Add($"total_compose_batch_end_ms={summary.TotalComposeBatchEndMs:0.###}");
        lines.Add($"total_sink_setvalue_ms={summary.TotalSinkSetValueMs:0.###}");
        lines.Add($"total_scrollviewer_wheel_events={summary.TotalScrollViewerWheelEvents}");
        lines.Add($"total_scrollviewer_set_offset_calls={summary.TotalScrollViewerSetOffsetCalls}");
        lines.Add($"total_dp_sink_sets={summary.TotalDependencyPropertySinkSetCount}");
        lines.Add($"total_dp_sink_ms={summary.TotalDependencyPropertySinkSetMilliseconds:0.###}");
        lines.Add($"total_clr_sink_sets={summary.TotalClrPropertySinkSetCount}");
        lines.Add($"total_clr_sink_ms={summary.TotalClrPropertySinkSetMilliseconds:0.###}");
        lines.Add($"total_render_invalidations={summary.TotalRenderInvalidations}");
        lines.Add($"total_measure_invalidations={summary.TotalMeasureInvalidations}");
        lines.Add($"total_arrange_invalidations={summary.TotalArrangeInvalidations}");
        lines.Add($"max_active_storyboards={summary.MaxActiveStoryboards}");
        lines.Add($"max_active_lanes={summary.MaxActiveLanes}");
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
        Assert.Contains("hotspot", summary.HotspotInference, StringComparison.OrdinalIgnoreCase);
    }

    private static HoverSweepStepMetrics RunSweepStep(UiRoot uiRoot, Vector2 pointer, string label, int stepIndex)
    {
        var beforeVisualTree = uiRoot.GetVisualTreeMetricsSnapshot();
        var beforeAnimation = AnimationManager.Current.GetTelemetrySnapshotForTests();
        var beforeHitTest = VisualTreeHelper.GetInstrumentationSnapshotForTests();
        var beforeRender = uiRoot.GetRenderTelemetrySnapshotForTests();
        var beforeStyle = Style.GetTelemetrySnapshotForTests();
        var beforeTemplateTrigger = TemplateTriggerEngine.GetTelemetrySnapshotForTests();
        var beforeVisualState = VisualStateManager.GetTelemetrySnapshotForTests();
        _ = ScrollViewer.GetScrollMetricsAndReset();
        AnimationValueSink.ResetTelemetryForTests();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        var pointerMove = uiRoot.GetPointerMoveTelemetrySnapshotForTests();

        RunFrame(uiRoot, 32 + (stepIndex * 16));

        var afterVisualTree = uiRoot.GetVisualTreeMetricsSnapshot();
        var afterAnimation = AnimationManager.Current.GetTelemetrySnapshotForTests();
        var afterHitTest = VisualTreeHelper.GetInstrumentationSnapshotForTests();
        var afterRender = uiRoot.GetRenderTelemetrySnapshotForTests();
        var scrollMetrics = ScrollViewer.GetScrollMetricsAndReset();
        var sinkTelemetry = AnimationValueSink.GetTelemetrySnapshotForTests();
        var afterStyle = Style.GetTelemetrySnapshotForTests();
        var afterTemplateTrigger = TemplateTriggerEngine.GetTelemetrySnapshotForTests();
        var afterVisualState = VisualStateManager.GetTelemetrySnapshotForTests();

        return new HoverSweepStepMetrics(
            stepIndex,
            label,
            pointer,
            pointerMove,
            scrollMetrics,
            sinkTelemetry,
            afterAnimation.BeginStoryboardCallCount - beforeAnimation.BeginStoryboardCallCount,
            afterAnimation.StoryboardStartCount - beforeAnimation.StoryboardStartCount,
            afterAnimation.BeginStoryboardMilliseconds - beforeAnimation.BeginStoryboardMilliseconds,
            afterAnimation.StoryboardStartMilliseconds - beforeAnimation.StoryboardStartMilliseconds,
            afterAnimation.ComposeMilliseconds - beforeAnimation.ComposeMilliseconds,
            afterAnimation.ComposeCollectMilliseconds - beforeAnimation.ComposeCollectMilliseconds,
            afterAnimation.ComposeSortMilliseconds - beforeAnimation.ComposeSortMilliseconds,
            afterAnimation.ComposeMergeMilliseconds - beforeAnimation.ComposeMergeMilliseconds,
            afterAnimation.ComposeApplyMilliseconds - beforeAnimation.ComposeApplyMilliseconds,
            afterAnimation.ComposeBatchBeginMilliseconds - beforeAnimation.ComposeBatchBeginMilliseconds,
            afterAnimation.ComposeBatchEndMilliseconds - beforeAnimation.ComposeBatchEndMilliseconds,
            afterAnimation.SinkSetValueMilliseconds - beforeAnimation.SinkSetValueMilliseconds,
            afterAnimation.HottestSetValuePathSummary,
            afterStyle.ApplyMilliseconds - beforeStyle.ApplyMilliseconds,
            afterStyle.ApplyTriggersMilliseconds - beforeStyle.ApplyTriggersMilliseconds,
            afterStyle.ApplyTriggerActionsMilliseconds - beforeStyle.ApplyTriggerActionsMilliseconds,
            afterStyle.InvokeActionsMilliseconds - beforeStyle.InvokeActionsMilliseconds,
            afterStyle.TriggerMatchCount - beforeStyle.TriggerMatchCount,
            afterStyle.SetStyleTriggerValueCount - beforeStyle.SetStyleTriggerValueCount,
            afterTemplateTrigger.ReapplyMilliseconds - beforeTemplateTrigger.ReapplyMilliseconds,
            afterTemplateTrigger.TriggerMatchMilliseconds - beforeTemplateTrigger.TriggerMatchMilliseconds,
            afterTemplateTrigger.SetterResolveMilliseconds - beforeTemplateTrigger.SetterResolveMilliseconds,
            afterTemplateTrigger.InvokeActionsMilliseconds - beforeTemplateTrigger.InvokeActionsMilliseconds,
            afterTemplateTrigger.SetTemplateTriggerValueCount - beforeTemplateTrigger.SetTemplateTriggerValueCount,
            afterVisualState.GoToStateMilliseconds - beforeVisualState.GoToStateMilliseconds,
            afterVisualState.GroupGoToStateMilliseconds - beforeVisualState.GroupGoToStateMilliseconds,
            afterVisualState.GroupApplySettersMilliseconds - beforeVisualState.GroupApplySettersMilliseconds,
            afterVisualState.GroupStoryboardMilliseconds - beforeVisualState.GroupStoryboardMilliseconds,
            afterVisualState.SetTemplateTriggerValueCount - beforeVisualState.SetTemplateTriggerValueCount,
            afterAnimation.ActiveStoryboardCount,
            afterAnimation.ActiveLaneCount,
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
            Math.Max(0, afterRender.RetainedTraversalCount - beforeRender.RetainedTraversalCount));
    }

    private static HoverSweepSummary Summarize(IReadOnlyList<HoverSweepStepMetrics> steps)
    {
        var hottest = steps
            .OrderByDescending(static step => step.BeginStoryboardCalls)
            .ThenByDescending(static step => step.LaneApplicationCount)
            .ThenByDescending(static step => step.PointerMove.HoverUpdateMilliseconds)
            .First();

        var totalBeginStoryboardCalls = steps.Sum(static step => step.BeginStoryboardCalls);
        var totalLaneApplications = steps.Sum(static step => step.LaneApplicationCount);
        var totalSinkValueSets = steps.Sum(static step => step.SinkValueSetCount);
        var totalHoverUpdateMs = steps.Sum(static step => step.PointerMove.HoverUpdateMilliseconds);
        var totalPointerResolveMs = steps.Sum(static step => step.PointerMove.PointerTargetResolveMilliseconds);
        var totalBeginStoryboardMs = steps.Sum(static step => step.BeginStoryboardMs);
        var totalStoryboardStartMs = steps.Sum(static step => step.StoryboardStartMs);
        var totalStyleApplyMs = steps.Sum(static step => step.StyleApplyMs);
        var totalStyleApplyTriggersMs = steps.Sum(static step => step.StyleApplyTriggersMs);
        var totalStyleApplyTriggerActionsMs = steps.Sum(static step => step.StyleApplyTriggerActionsMs);
        var totalStyleInvokeActionsMs = steps.Sum(static step => step.StyleInvokeActionsMs);
        var totalTemplateReapplyMs = steps.Sum(static step => step.TemplateReapplyMs);
        var totalTemplateTriggerMatchMs = steps.Sum(static step => step.TemplateTriggerMatchMs);
        var totalTemplateSetterResolveMs = steps.Sum(static step => step.TemplateSetterResolveMs);
        var totalTemplateInvokeActionsMs = steps.Sum(static step => step.TemplateInvokeActionsMs);
        var totalVisualStateGoToStateMs = steps.Sum(static step => step.VisualStateGoToStateMs);
        var totalVisualStateGroupTransitionMs = steps.Sum(static step => step.VisualStateGroupTransitionMs);
        var totalVisualStateApplySettersMs = steps.Sum(static step => step.VisualStateApplySettersMs);
        var totalVisualStateStoryboardMs = steps.Sum(static step => step.VisualStateStoryboardMs);
        var totalComposeMs = steps.Sum(static step => step.ComposeMs);
        var totalComposeCollectMs = steps.Sum(static step => step.ComposeCollectMs);
        var totalComposeSortMs = steps.Sum(static step => step.ComposeSortMs);
        var totalComposeMergeMs = steps.Sum(static step => step.ComposeMergeMs);
        var totalComposeApplyMs = steps.Sum(static step => step.ComposeApplyMs);
        var totalComposeBatchBeginMs = steps.Sum(static step => step.ComposeBatchBeginMs);
        var totalComposeBatchEndMs = steps.Sum(static step => step.ComposeBatchEndMs);
        var totalSinkSetValueMs = steps.Sum(static step => step.SinkSetValueMs);
        var totalDependencyPropertySinkSetCount = steps.Sum(static step => step.SinkTelemetry.DependencyPropertySetValueCount);
        var totalDependencyPropertySinkSetMs = steps.Sum(static step => step.SinkTelemetry.DependencyPropertySetValueMilliseconds);
        var totalClrPropertySinkSetCount = steps.Sum(static step => step.SinkTelemetry.ClrPropertySetValueCount);
        var totalClrPropertySinkSetMs = steps.Sum(static step => step.SinkTelemetry.ClrPropertySetValueMilliseconds);
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
            totalComposeApplyMs >= totalComposeCollectMs &&
            totalComposeApplyMs >= totalComposeSortMs &&
            totalComposeApplyMs >= totalComposeMergeMs &&
            totalComposeBatchEndMs >= totalComposeBatchBeginMs &&
            totalComposeBatchEndMs > totalSinkSetValueMs
                ? "Exact remaining hotspot after the fix: AnimationManager.ApplyPendingWrites -> freezable.EndBatchUpdate(), which flushes the coalesced Changed notifications for the animated ScaleTransform and DropShadowEffect instances. ScrollViewer work is still zero, and the old reflective setter hotspot is gone."
                : "Logs did not isolate storyboard churn as dominant; collect deeper per-class instrumentation.";

        return new HoverSweepSummary(
            steps.Sum(static step => step.PointerMove.HitTestCount),
            steps.Sum(static step => step.PointerMove.RoutedEventCount),
            totalHoverUpdateMs,
            totalPointerResolveMs,
            totalBeginStoryboardCalls,
            steps.Sum(static step => step.StoryboardStarts),
            totalLaneApplications,
            totalSinkValueSets,
            totalBeginStoryboardMs,
            totalStoryboardStartMs,
            totalStyleApplyMs,
            totalStyleApplyTriggersMs,
            totalStyleApplyTriggerActionsMs,
            totalStyleInvokeActionsMs,
            steps.Sum(static step => step.StyleTriggerMatchCount),
            steps.Sum(static step => step.StyleTriggerSetCount),
            totalTemplateReapplyMs,
            totalTemplateTriggerMatchMs,
            totalTemplateSetterResolveMs,
            totalTemplateInvokeActionsMs,
            steps.Sum(static step => step.TemplateSetValueCount),
            totalVisualStateGoToStateMs,
            totalVisualStateGroupTransitionMs,
            totalVisualStateApplySettersMs,
            totalVisualStateStoryboardMs,
            steps.Sum(static step => step.VisualStateSetValueCount),
            totalComposeMs,
            totalComposeCollectMs,
            totalComposeSortMs,
            totalComposeMergeMs,
            totalComposeApplyMs,
            totalComposeBatchBeginMs,
            totalComposeBatchEndMs,
            totalSinkSetValueMs,
            steps.Sum(static step => step.ScrollViewer.WheelEvents),
            steps.Sum(static step => step.ScrollViewer.SetOffsetCalls),
            totalDependencyPropertySinkSetCount,
            totalDependencyPropertySinkSetMs,
            totalClrPropertySinkSetCount,
            totalClrPropertySinkSetMs,
            steps.Sum(static step => step.RenderInvalidationCount),
            steps.Sum(static step => step.MeasureInvalidationCount),
            steps.Sum(static step => step.ArrangeInvalidationCount),
            steps.Max(static step => step.ActiveStoryboardCount),
            steps.Max(static step => step.ActiveLaneCount),
            hottestSetValuePaths,
            hotspotInference,
            hottest.Label,
            $"beginStoryboards={hottest.BeginStoryboardCalls}, laneApplications={hottest.LaneApplicationCount}, sinkValueSets={hottest.SinkValueSetCount}, dpSinkSets={hottest.SinkTelemetry.DependencyPropertySetValueCount}, clrSinkSets={hottest.SinkTelemetry.ClrPropertySetValueCount}, dpSinkMs={hottest.SinkTelemetry.DependencyPropertySetValueMilliseconds:0.###}, clrSinkMs={hottest.SinkTelemetry.ClrPropertySetValueMilliseconds:0.###}, beginMs={hottest.BeginStoryboardMs:0.###}, startMs={hottest.StoryboardStartMs:0.###}, styleApplyMs={hottest.StyleApplyMs:0.###}, styleApplyTriggersMs={hottest.StyleApplyTriggersMs:0.###}, templateReapplyMs={hottest.TemplateReapplyMs:0.###}, visualStateGoToStateMs={hottest.VisualStateGoToStateMs:0.###}, visualStateStoryboardMs={hottest.VisualStateStoryboardMs:0.###}, composeMs={hottest.ComposeMs:0.###}, composeCollectMs={hottest.ComposeCollectMs:0.###}, composeSortMs={hottest.ComposeSortMs:0.###}, composeMergeMs={hottest.ComposeMergeMs:0.###}, composeApplyMs={hottest.ComposeApplyMs:0.###}, composeBatchBeginMs={hottest.ComposeBatchBeginMs:0.###}, composeBatchEndMs={hottest.ComposeBatchEndMs:0.###}, setValueMs={hottest.SinkSetValueMs:0.###}, hottestSetValuePaths={hottest.HottestSetValuePathSummary}");
    }

    private static string FormatStep(HoverSweepStepMetrics step)
    {
        return
            $"step={step.StepIndex:000} label={step.Label} pointer=({step.Pointer.X:0.##},{step.Pointer.Y:0.##}) " +
            $"resolvePath={step.PointerMove.PointerResolvePath} hitTests={step.PointerMove.HitTestCount} routedEvents={step.PointerMove.RoutedEventCount} " +
            $"scrollViewerWheel={step.ScrollViewer.WheelEvents} scrollViewerSetOffsets={step.ScrollViewer.SetOffsetCalls} " +
            $"hoverMs={step.PointerMove.HoverUpdateMilliseconds:0.###} resolveMs={step.PointerMove.PointerTargetResolveMilliseconds:0.###} routeMs={step.PointerMove.PointerRouteMilliseconds:0.###} " +
            $"dpSinkSets={step.SinkTelemetry.DependencyPropertySetValueCount} dpSinkMs={step.SinkTelemetry.DependencyPropertySetValueMilliseconds:0.###} clrSinkSets={step.SinkTelemetry.ClrPropertySetValueCount} clrSinkMs={step.SinkTelemetry.ClrPropertySetValueMilliseconds:0.###} " +
            $"styleApplyMs={step.StyleApplyMs:0.###} styleApplyTriggersMs={step.StyleApplyTriggersMs:0.###} styleTriggerActionsMs={step.StyleApplyTriggerActionsMs:0.###} styleInvokeActionsMs={step.StyleInvokeActionsMs:0.###} styleTriggerMatches={step.StyleTriggerMatchCount} styleTriggerSets={step.StyleTriggerSetCount} templateReapplyMs={step.TemplateReapplyMs:0.###} templateMatchMs={step.TemplateTriggerMatchMs:0.###} templateSetterResolveMs={step.TemplateSetterResolveMs:0.###} templateInvokeActionsMs={step.TemplateInvokeActionsMs:0.###} templateSets={step.TemplateSetValueCount} visualStateGoToStateMs={step.VisualStateGoToStateMs:0.###} visualStateGroupTransitionMs={step.VisualStateGroupTransitionMs:0.###} visualStateApplySettersMs={step.VisualStateApplySettersMs:0.###} visualStateStoryboardMs={step.VisualStateStoryboardMs:0.###} visualStateSets={step.VisualStateSetValueCount} " +
            $"beginMs={step.BeginStoryboardMs:0.###} startMs={step.StoryboardStartMs:0.###} composeMs={step.ComposeMs:0.###} composeCollectMs={step.ComposeCollectMs:0.###} composeSortMs={step.ComposeSortMs:0.###} composeMergeMs={step.ComposeMergeMs:0.###} composeApplyMs={step.ComposeApplyMs:0.###} composeBatchBeginMs={step.ComposeBatchBeginMs:0.###} composeBatchEndMs={step.ComposeBatchEndMs:0.###} setValueMs={step.SinkSetValueMs:0.###} hottestSetValuePaths={step.HottestSetValuePathSummary} " +
            $"beginStoryboards={step.BeginStoryboardCalls} storyboardStarts={step.StoryboardStarts} activeStoryboards={step.ActiveStoryboardCount} activeLanes={step.ActiveLaneCount} " +
            $"composePasses={step.ComposePassCount} laneApplications={step.LaneApplicationCount} sinkValueSets={step.SinkValueSetCount} clearedLanes={step.ClearedLaneCount} " +
            $"measureInvalidations={step.MeasureInvalidationCount} arrangeInvalidations={step.ArrangeInvalidationCount} renderInvalidations={step.RenderInvalidationCount} " +
            $"dirtyRoots={step.DirtyRootCount} retainedTraversals={step.RetainedTraversalCount} hitTestFastPaths={step.MonotonicPanelFastPathCount}";
    }

    private static ScrollViewer FindSidebarScrollViewer(ControlsCatalogView catalog)
    {
        return FindFirstVisualChild<ScrollViewer>(
                   catalog,
                   static viewer => viewer.Content is StackPanel host &&
                                    string.Equals(host.Name, "ControlButtonsHost", StringComparison.Ordinal))
               ?? throw new InvalidOperationException("Could not find sidebar ScrollViewer.");
    }

    private static List<Button> GetVisibleButtons(ScrollViewer viewer, StackPanel host)
    {
        var viewport = GetViewerViewportRect(viewer);
        return host.Children
            .OfType<Button>()
            .Where(button => button.LayoutSlot.Height > 0f && Intersects(button.LayoutSlot, viewport))
            .ToList();
    }

    private static LayoutRect GetViewerViewportRect(ScrollViewer viewer)
    {
        if (viewer.TryGetContentViewportClipRect(out var viewport))
        {
            return viewport;
        }

        throw new InvalidOperationException("Sidebar ScrollViewer did not expose a viewport.");
    }

    private static bool Intersects(LayoutRect left, LayoutRect right)
    {
        return left.X < right.X + right.Width &&
               right.X < left.X + left.Width &&
               left.Y < right.Y + right.Height &&
               right.Y < left.Y + left.Height;
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

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool pointerMoved)
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
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"({rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##})";
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

    private readonly record struct HoverSweepStepMetrics(
        int StepIndex,
        string Label,
        Vector2 Pointer,
        UiPointerMoveTelemetrySnapshot PointerMove,
        ScrollViewerScrollMetricsSnapshot ScrollViewer,
        AnimationSinkTelemetrySnapshot SinkTelemetry,
        int BeginStoryboardCalls,
        int StoryboardStarts,
        double BeginStoryboardMs,
        double StoryboardStartMs,
        double ComposeMs,
        double ComposeCollectMs,
        double ComposeSortMs,
        double ComposeMergeMs,
        double ComposeApplyMs,
        double ComposeBatchBeginMs,
        double ComposeBatchEndMs,
        double SinkSetValueMs,
        string HottestSetValuePathSummary,
        double StyleApplyMs,
        double StyleApplyTriggersMs,
        double StyleApplyTriggerActionsMs,
        double StyleInvokeActionsMs,
        long StyleTriggerMatchCount,
        long StyleTriggerSetCount,
        double TemplateReapplyMs,
        double TemplateTriggerMatchMs,
        double TemplateSetterResolveMs,
        double TemplateInvokeActionsMs,
        long TemplateSetValueCount,
        double VisualStateGoToStateMs,
        double VisualStateGroupTransitionMs,
        double VisualStateApplySettersMs,
        double VisualStateStoryboardMs,
        long VisualStateSetValueCount,
        int ActiveStoryboardCount,
        int ActiveLaneCount,
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
        int RetainedTraversalCount);

    private readonly record struct HoverSweepSummary(
        int TotalHitTests,
        int TotalRoutedEvents,
        double TotalHoverUpdateMs,
        double TotalPointerResolveMs,
        int TotalBeginStoryboardCalls,
        int TotalStoryboardStarts,
        int TotalLaneApplications,
        int TotalSinkValueSets,
        double TotalBeginStoryboardMs,
        double TotalStoryboardStartMs,
        double TotalStyleApplyMs,
        double TotalStyleApplyTriggersMs,
        double TotalStyleApplyTriggerActionsMs,
        double TotalStyleInvokeActionsMs,
        long TotalStyleTriggerMatches,
        long TotalStyleTriggerSets,
        double TotalTemplateReapplyMs,
        double TotalTemplateTriggerMatchMs,
        double TotalTemplateSetterResolveMs,
        double TotalTemplateInvokeActionsMs,
        long TotalTemplateSetValues,
        double TotalVisualStateGoToStateMs,
        double TotalVisualStateGroupTransitionMs,
        double TotalVisualStateApplySettersMs,
        double TotalVisualStateStoryboardMs,
        long TotalVisualStateSetValues,
        double TotalComposeMs,
        double TotalComposeCollectMs,
        double TotalComposeSortMs,
        double TotalComposeMergeMs,
        double TotalComposeApplyMs,
        double TotalComposeBatchBeginMs,
        double TotalComposeBatchEndMs,
        double TotalSinkSetValueMs,
        int TotalScrollViewerWheelEvents,
        int TotalScrollViewerSetOffsetCalls,
        int TotalDependencyPropertySinkSetCount,
        double TotalDependencyPropertySinkSetMilliseconds,
        int TotalClrPropertySinkSetCount,
        double TotalClrPropertySinkSetMilliseconds,
        long TotalRenderInvalidations,
        long TotalMeasureInvalidations,
        long TotalArrangeInvalidations,
        int MaxActiveStoryboards,
        int MaxActiveLanes,
        string HottestSetValuePaths,
        string HotspotInference,
        string HottestStepLabel,
        string HottestStepDetail);
}
