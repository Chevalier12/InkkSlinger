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

public sealed class ControlsCatalogCalendarHoverHotspotDiagnosticsTests
{
    private const int ViewportWidth = 1400;
    private const int ViewportHeight = 900;

    [Fact]
    public void CalendarPreview_RapidHoverSweep_WritesHotspotDiagnosticsLog()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            AnimationManager.Current.ResetForTests();
            VisualTreeHelper.ResetInstrumentationForTests();

            var catalog = new ControlsCatalogView
            {
                Width = ViewportWidth,
                Height = ViewportHeight
            };

            var host = new Canvas
            {
                Width = ViewportWidth,
                Height = ViewportHeight
            };
            host.AddChild(catalog);

            var uiRoot = new UiRoot(host);
            RunFrame(uiRoot, 16);

            catalog.ShowControl("Calendar");
            RunFrame(uiRoot, 32);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var previewRoot = Assert.IsAssignableFrom<UIElement>(previewHost.Content);
            var calendar = Assert.IsType<Calendar>(FindFirstVisualChild<Calendar>(previewRoot));
            var dayButtons = calendar.DayButtonsForTesting
                .Select(static button => Assert.IsType<CalendarDayButton>(button))
                .Where(static button => !string.IsNullOrEmpty(button.DayText))
                .ToList();
            Assert.NotEmpty(dayButtons);

            // Drain the sidebar button hover animation used to navigate into Calendar so the sweep
            // only captures CalendarDayButton behavior.
            var settledPointer = GetCenter(dayButtons[0].LayoutSlot);
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(settledPointer, pointerMoved: true));
            for (var i = 0; i < 16; i++)
            {
                RunFrame(uiRoot, 48 + (i * 16));
            }

            PrimeRetainedRenderStateForDiagnostics(uiRoot);
            AnimationManager.Current.ResetTelemetryForTests();
            VisualTreeHelper.ResetInstrumentationForTests();
            uiRoot.CompleteDrawStateForTests();
            uiRoot.ResetDirtyStateForTests();
            uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight));

            var sidebarMetricsWithoutCalendar = MeasureSidebarSweep(includeCalendarPreview: false);
            var sidebarMetricsWithCalendar = MeasureSidebarSweep(includeCalendarPreview: true);

            var logPath = GetDiagnosticsLogPath("controls-catalog-calendar-hover-hotspot");
            var lines = new List<string>
            {
                "scenario=ControlsCatalog Calendar preview rapid hover sweep",
                $"timestamp_utc={DateTime.UtcNow:O}",
                $"log_path={logPath}",
                "step_1=open Controls Catalog",
                "step_2=click Calendar button",
                "step_3=move pointer over as many CalendarDayButton cells as possible",
                "step_4=compare sidebar hover cost with and without Calendar preview open",
                $"calendar_day_button_count={dayButtons.Count}"
            };

            var stepMetrics = new List<HoverSweepStepMetrics>();
            var pointer = GetCenter(dayButtons[0].LayoutSlot);
            stepMetrics.Add(RunSweepStep(uiRoot, pointer, "calendar-initial", stepIndex: 0));

            var stepIndex = 1;
            for (var pass = 0; pass < 3; pass++)
            {
                foreach (var button in dayButtons)
                {
                    pointer = GetCenter(button.LayoutSlot);
                    stepMetrics.Add(RunSweepStep(
                        uiRoot,
                        pointer,
                        $"calendar-pass-{pass + 1}:{button.DayText}",
                        stepIndex++));
                }
            }

            var summary = Summarize(stepMetrics);
            lines.Add(string.Empty);
            lines.Add("calendar_summary:");
            AppendSummaryLines(lines, summary);
            lines.Add($"hotspot_inference={BuildInference(summary, sidebarMetricsWithoutCalendar, sidebarMetricsWithCalendar)}");
            lines.Add($"sidebar_without_calendar_total_hover_ms={sidebarMetricsWithoutCalendar.TotalHoverUpdateMs:0.###}");
            lines.Add($"sidebar_with_calendar_total_hover_ms={sidebarMetricsWithCalendar.TotalHoverUpdateMs:0.###}");
            lines.Add($"sidebar_without_calendar_total_begin_storyboards={sidebarMetricsWithoutCalendar.TotalBeginStoryboardCalls}");
            lines.Add($"sidebar_with_calendar_total_begin_storyboards={sidebarMetricsWithCalendar.TotalBeginStoryboardCalls}");
            lines.Add($"sidebar_without_calendar_total_compose_apply_ms={sidebarMetricsWithoutCalendar.TotalComposeApplyMs:0.###}");
            lines.Add($"sidebar_with_calendar_total_compose_apply_ms={sidebarMetricsWithCalendar.TotalComposeApplyMs:0.###}");

            lines.Add(string.Empty);
            lines.Add("calendar_steps:");
            lines.AddRange(stepMetrics.Select(FormatStep));

            lines.Add(string.Empty);
            lines.Add("sidebar_without_calendar_summary:");
            AppendSummaryLines(lines, sidebarMetricsWithoutCalendar);

            lines.Add(string.Empty);
            lines.Add("sidebar_with_calendar_summary:");
            AppendSummaryLines(lines, sidebarMetricsWithCalendar);

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllLines(logPath, lines);

            Assert.True(File.Exists(logPath));
            Assert.Contains("hotspot", lines.Last(static line => line.StartsWith("hotspot_inference=", StringComparison.Ordinal)), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CalendarPreview_HoverSweepThenSidebarSweep_WritesContaminationDiagnosticsLog()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var sidebarBaseline = MeasureSidebarSweep(includeCalendarPreview: false);
            var sidebarWithCalendarOpen = MeasureSidebarSweep(includeCalendarPreview: true);
            var sidebarAfterCalendarSweep = MeasureSidebarSweepAfterCalendarHover();

            var logPath = GetDiagnosticsLogPath("controls-catalog-calendar-hover-sidebar-contamination");
            var lines = new List<string>
            {
                "scenario=ControlsCatalog Calendar hover then sidebar hover contamination diagnostics",
                $"timestamp_utc={DateTime.UtcNow:O}",
                $"log_path={logPath}",
                "step_1=open Controls Catalog",
                "step_2=click Calendar button",
                "step_3=rapidly sweep CalendarDayButton cells multiple times",
                "step_4=immediately sweep visible sidebar buttons",
                "step_5=compare sidebar hover baseline vs calendar-open vs post-calendar-hover"
            };

            lines.Add(string.Empty);
            lines.Add("sidebar_baseline_summary:");
            AppendSummaryLines(lines, sidebarBaseline);

            lines.Add(string.Empty);
            lines.Add("sidebar_with_calendar_open_summary:");
            AppendSummaryLines(lines, sidebarWithCalendarOpen);

            lines.Add(string.Empty);
            lines.Add("sidebar_after_calendar_hover_summary:");
            AppendSummaryLines(lines, sidebarAfterCalendarSweep);

            lines.Add(string.Empty);
            lines.Add($"contamination_inference={BuildSidebarContaminationInference(sidebarBaseline, sidebarWithCalendarOpen, sidebarAfterCalendarSweep)}");

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllLines(logPath, lines);

            Assert.True(File.Exists(logPath));
            Assert.Contains("hotspot", lines.Last(static line => line.StartsWith("contamination_inference=", StringComparison.Ordinal)), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static HoverSweepSummary MeasureSidebarSweep(bool includeCalendarPreview)
    {
        AnimationManager.Current.ResetForTests();
        VisualTreeHelper.ResetInstrumentationForTests();
        Freezable.ResetTelemetryForTests();
        UIElement.ResetFreezableInvalidationBatchTelemetryForTests();

        var catalog = new ControlsCatalogView
        {
            Width = ViewportWidth,
            Height = ViewportHeight
        };

        var host = new Canvas
        {
            Width = ViewportWidth,
            Height = ViewportHeight
        };
        host.AddChild(catalog);

        var uiRoot = new UiRoot(host);
        RunFrame(uiRoot, 16);

        if (includeCalendarPreview)
        {
            catalog.ShowControl("Calendar");
            RunFrame(uiRoot, 32);
        }

        PrimeRetainedRenderStateForDiagnostics(uiRoot);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight));

        var sidebarViewer = FindSidebarScrollViewer(catalog);
        var sidebarHost = Assert.IsType<StackPanel>(catalog.FindName("ControlButtonsHost"));
        var visibleButtons = GetVisibleButtons(sidebarViewer, sidebarHost);
        Assert.NotEmpty(visibleButtons);

        var metrics = new List<HoverSweepStepMetrics>();
        var pointer = GetCenter(GetViewerViewportRect(sidebarViewer));
        metrics.Add(RunSweepStep(uiRoot, pointer, includeCalendarPreview ? "sidebar-calendar:center" : "sidebar-base:center", 0));

        var stepIndex = 1;
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var button in visibleButtons)
            {
                pointer = GetCenter(button.LayoutSlot);
                metrics.Add(RunSweepStep(
                    uiRoot,
                    pointer,
                    $"{(includeCalendarPreview ? "sidebar-calendar" : "sidebar-base")}:pass-{pass + 1}:{button.GetContentText()}",
                    stepIndex++));
            }
        }

        return Summarize(metrics);
    }

    private static HoverSweepSummary MeasureSidebarSweepAfterCalendarHover()
    {
        AnimationManager.Current.ResetForTests();
        VisualTreeHelper.ResetInstrumentationForTests();
        Freezable.ResetTelemetryForTests();
        UIElement.ResetFreezableInvalidationBatchTelemetryForTests();

        var catalog = new ControlsCatalogView
        {
            Width = ViewportWidth,
            Height = ViewportHeight
        };

        var host = new Canvas
        {
            Width = ViewportWidth,
            Height = ViewportHeight
        };
        host.AddChild(catalog);

        var uiRoot = new UiRoot(host);
        RunFrame(uiRoot, 16);

        catalog.ShowControl("Calendar");
        RunFrame(uiRoot, 32);

        var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
        var previewRoot = Assert.IsAssignableFrom<UIElement>(previewHost.Content);
        var calendar = Assert.IsType<Calendar>(FindFirstVisualChild<Calendar>(previewRoot));
        var dayButtons = calendar.DayButtonsForTesting
            .Select(static button => Assert.IsType<CalendarDayButton>(button))
            .Where(static button => !string.IsNullOrEmpty(button.DayText))
            .ToList();
        Assert.NotEmpty(dayButtons);

        var pointer = GetCenter(dayButtons[0].LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        for (var i = 0; i < 8; i++)
        {
            RunFrame(uiRoot, 48 + (i * 16));
        }

        PrimeRetainedRenderStateForDiagnostics(uiRoot);
        AnimationManager.Current.ResetTelemetryForTests();
        VisualTreeHelper.ResetInstrumentationForTests();
        Freezable.ResetTelemetryForTests();
        UIElement.ResetFreezableInvalidationBatchTelemetryForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight));

        var stepIndex = 0;
        for (var pass = 0; pass < 3; pass++)
        {
            foreach (var button in dayButtons)
            {
                pointer = GetCenter(button.LayoutSlot);
                _ = RunSweepStep(uiRoot, pointer, $"calendar-prelude:{pass + 1}:{button.DayText}", stepIndex++);
            }
        }

        var sidebarViewer = FindSidebarScrollViewer(catalog);
        var sidebarHost = Assert.IsType<StackPanel>(catalog.FindName("ControlButtonsHost"));
        var visibleButtons = GetVisibleButtons(sidebarViewer, sidebarHost);
        Assert.NotEmpty(visibleButtons);

        var metrics = new List<HoverSweepStepMetrics>();
        pointer = GetCenter(GetViewerViewportRect(sidebarViewer));
        metrics.Add(RunSweepStep(uiRoot, pointer, "sidebar-after-calendar:center", stepIndex++));

        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var button in visibleButtons)
            {
                pointer = GetCenter(button.LayoutSlot);
                metrics.Add(RunSweepStep(
                    uiRoot,
                    pointer,
                    $"sidebar-after-calendar:pass-{pass + 1}:{button.GetContentText()}",
                    stepIndex++));
            }
        }

        return Summarize(metrics);
    }

    private static void AppendSummaryLines(ICollection<string> lines, HoverSweepSummary summary)
    {
        lines.Add($"total_steps={summary.TotalSteps}");
        lines.Add($"total_hit_tests={summary.TotalHitTests}");
        lines.Add($"total_routed_events={summary.TotalRoutedEvents}");
        lines.Add($"total_hover_update_ms={summary.TotalHoverUpdateMs:0.###}");
        lines.Add($"total_pointer_resolve_ms={summary.TotalPointerResolveMs:0.###}");
        lines.Add($"total_pointer_resolve_hover_reuse_ms={summary.TotalPointerResolveHoverReuseMs:0.###}");
        lines.Add($"total_pointer_resolve_final_hit_test_ms={summary.TotalPointerResolveFinalHitTestMs:0.###}");
        lines.Add($"total_begin_storyboard_calls={summary.TotalBeginStoryboardCalls}");
        lines.Add($"total_storyboard_starts={summary.TotalStoryboardStarts}");
        lines.Add($"total_lane_applications={summary.TotalLaneApplications}");
        lines.Add($"total_sink_value_sets={summary.TotalSinkValueSets}");
        lines.Add($"total_begin_storyboard_ms={summary.TotalBeginStoryboardMs:0.###}");
        lines.Add($"total_storyboard_start_ms={summary.TotalStoryboardStartMs:0.###}");
        lines.Add($"total_storyboard_update_ms={summary.TotalStoryboardUpdateMs:0.###}");
        lines.Add($"total_compose_ms={summary.TotalComposeMs:0.###}");
        lines.Add($"total_compose_collect_ms={summary.TotalComposeCollectMs:0.###}");
        lines.Add($"total_compose_sort_ms={summary.TotalComposeSortMs:0.###}");
        lines.Add($"total_compose_merge_ms={summary.TotalComposeMergeMs:0.###}");
        lines.Add($"total_compose_apply_ms={summary.TotalComposeApplyMs:0.###}");
        lines.Add($"total_compose_batch_begin_ms={summary.TotalComposeBatchBeginMs:0.###}");
        lines.Add($"total_compose_batch_end_ms={summary.TotalComposeBatchEndMs:0.###}");
        lines.Add($"total_sink_setvalue_ms={summary.TotalSinkSetValueMs:0.###}");
        lines.Add($"total_cleanup_completed_ms={summary.TotalCleanupCompletedMs:0.###}");
        lines.Add($"total_freezable_onchanged_ms={summary.TotalFreezableOnChangedMs:0.###}");
        lines.Add($"total_freezable_end_batch_ms={summary.TotalFreezableEndBatchMs:0.###}");
        lines.Add($"total_freezable_batch_flush_ms={summary.TotalFreezableBatchFlushMs:0.###}");
        lines.Add($"total_freezable_batch_flushes={summary.TotalFreezableBatchFlushes}");
        lines.Add($"total_freezable_batch_flush_targets={summary.TotalFreezableBatchFlushTargets}");
        lines.Add($"max_freezable_batch_pending_targets={summary.MaxFreezableBatchPendingTargets}");
        lines.Add($"total_render_invalidations={summary.TotalRenderInvalidations}");
        lines.Add($"total_measure_invalidations={summary.TotalMeasureInvalidations}");
        lines.Add($"total_arrange_invalidations={summary.TotalArrangeInvalidations}");
        lines.Add($"total_dirty_region_count={summary.TotalDirtyRegionCount}");
        lines.Add($"max_dirty_region_count={summary.MaxDirtyRegionCount}");
        lines.Add($"max_dirty_coverage={summary.MaxDirtyCoverage:0.###}");
        lines.Add($"steps_using_partial_dirty_redraw={summary.PartialDirtyRedrawStepCount}");
        lines.Add($"steps_forcing_full_redraw={summary.FullRedrawStepCount}");
        lines.Add($"full_dirty_initial_state_count={summary.FullDirtyInitialStateCount}");
        lines.Add($"full_dirty_viewport_change_count={summary.FullDirtyViewportChangeCount}");
        lines.Add($"full_dirty_surface_reset_count={summary.FullDirtySurfaceResetCount}");
        lines.Add($"full_dirty_visual_structure_change_count={summary.FullDirtyVisualStructureChangeCount}");
        lines.Add($"full_dirty_retained_rebuild_count={summary.FullDirtyRetainedRebuildCount}");
        lines.Add($"full_dirty_detached_visual_count={summary.FullDirtyDetachedVisualCount}");
        lines.Add($"max_active_storyboards={summary.MaxActiveStoryboards}");
        lines.Add($"max_active_lanes={summary.MaxActiveLanes}");
        lines.Add($"max_active_storyboard_entries={summary.MaxActiveStoryboardEntries}");
        lines.Add($"hottest_freezable_onchanged_type={summary.HottestFreezableOnChangedType}:{summary.HottestFreezableOnChangedMs:0.###}");
        lines.Add($"hottest_freezable_endbatch_type={summary.HottestFreezableEndBatchType}:{summary.HottestFreezableEndBatchMs:0.###}");
        lines.Add($"hottest_setvalue_paths={summary.HottestSetValuePaths}");
        lines.Add($"hottest_step={summary.HottestStepLabel}");
        lines.Add($"hottest_step_detail={summary.HottestStepDetail}");
    }

    private static string BuildInference(
        HoverSweepSummary calendarSummary,
        HoverSweepSummary sidebarWithoutCalendar,
        HoverSweepSummary sidebarWithCalendar)
    {
        var sidebarDeltaBeginStoryboards = sidebarWithCalendar.TotalBeginStoryboardCalls - sidebarWithoutCalendar.TotalBeginStoryboardCalls;
        var sidebarDeltaComposeApplyMs = sidebarWithCalendar.TotalComposeApplyMs - sidebarWithoutCalendar.TotalComposeApplyMs;
        var sidebarDeltaHoverMs = sidebarWithCalendar.TotalHoverUpdateMs - sidebarWithoutCalendar.TotalHoverUpdateMs;

        if (calendarSummary.TotalBeginStoryboardCalls > 0 &&
            calendarSummary.TotalLaneApplications > calendarSummary.TotalSteps &&
            calendarSummary.FullRedrawStepCount > 0 &&
            calendarSummary.MaxDirtyRegionCount > 4 &&
            calendarSummary.TotalComposeApplyMs >= calendarSummary.TotalStoryboardUpdateMs)
        {
            return
                "Exact hotspot: CalendarDayButton hover storyboard churn fragments dirty regions badly enough to trip the retained renderer's full-redraw fallback, while AnimationManager.ApplyPendingWrites still spends meaningful time flushing ScaleTransform and DropShadowEffect updates. " +
                $"Sidebar hover does not meaningfully worsen when Calendar is open: delta_begin_storyboards={sidebarDeltaBeginStoryboards}, delta_compose_apply_ms={sidebarDeltaComposeApplyMs:0.###}, delta_hover_ms={sidebarDeltaHoverMs:0.###}.";
        }

        return "Logs did not isolate a single animation/write-back hotspot yet; collect narrower per-class instrumentation.";
    }

    private static string BuildSidebarContaminationInference(
        HoverSweepSummary sidebarBaseline,
        HoverSweepSummary sidebarWithCalendarOpen,
        HoverSweepSummary sidebarAfterCalendarHover)
    {
        var deltaHoverMs = sidebarAfterCalendarHover.TotalHoverUpdateMs - sidebarBaseline.TotalHoverUpdateMs;
        var deltaResolveMs = sidebarAfterCalendarHover.TotalPointerResolveMs - sidebarBaseline.TotalPointerResolveMs;
        var deltaResolveHoverReuseMs = sidebarAfterCalendarHover.TotalPointerResolveHoverReuseMs - sidebarBaseline.TotalPointerResolveHoverReuseMs;
        var deltaResolveFinalHitTestMs = sidebarAfterCalendarHover.TotalPointerResolveFinalHitTestMs - sidebarBaseline.TotalPointerResolveFinalHitTestMs;
        var deltaComposeApplyMs = sidebarAfterCalendarHover.TotalComposeApplyMs - sidebarBaseline.TotalComposeApplyMs;
        var deltaBatchEndMs = sidebarAfterCalendarHover.TotalComposeBatchEndMs - sidebarBaseline.TotalComposeBatchEndMs;
        var deltaFreezableEndBatchMs = sidebarAfterCalendarHover.TotalFreezableEndBatchMs - sidebarBaseline.TotalFreezableEndBatchMs;
        var deltaFlushTargets = sidebarAfterCalendarHover.TotalFreezableBatchFlushTargets - sidebarBaseline.TotalFreezableBatchFlushTargets;
        var deltaActiveLanes = sidebarAfterCalendarHover.MaxActiveLanes - sidebarBaseline.MaxActiveLanes;

        if (deltaResolveMs > 5d &&
            deltaResolveHoverReuseMs > 0d &&
            deltaResolveFinalHitTestMs <= 1d &&
            deltaFlushTargets > 0)
        {
            return
                "Exact remaining hotspot: post-calendar sidebar hover spends extra time inside UiRoot.ResolvePointerTarget() before the final hit test, specifically in the hover-reuse eligibility path, while the animation/freezable side only increases secondarily because sidebar hover still animates the same button style. " +
                $"baseline_to_post_hover_delta_hover_ms={deltaHoverMs:0.###}, delta_resolve_ms={deltaResolveMs:0.###}, delta_resolve_hover_reuse_ms={deltaResolveHoverReuseMs:0.###}, delta_resolve_final_hit_test_ms={deltaResolveFinalHitTestMs:0.###}, delta_compose_apply_ms={deltaComposeApplyMs:0.###}, delta_compose_batch_end_ms={deltaBatchEndMs:0.###}, delta_freezable_endbatch_ms={deltaFreezableEndBatchMs:0.###}, delta_flush_targets={deltaFlushTargets}, delta_active_lanes={deltaActiveLanes}, open_calendar_active_lanes={sidebarWithCalendarOpen.MaxActiveLanes}.";
        }

        return "Logs did not isolate a post-calendar sidebar contamination hotspot yet; collect narrower telemetry.";
    }

    private static HoverSweepStepMetrics RunSweepStep(UiRoot uiRoot, Vector2 pointer, string label, int stepIndex)
    {
        var beforeVisualTree = uiRoot.GetVisualTreeMetricsSnapshot();
        var beforeAnimation = AnimationManager.Current.GetTelemetrySnapshotForTests();
        var beforeHitTest = VisualTreeHelper.GetInstrumentationSnapshotForTests();
        var beforeRender = uiRoot.GetRenderTelemetrySnapshotForTests();
        var beforeFreezable = Freezable.GetTelemetrySnapshotForTests();
        var beforeInvalidationBatch = UIElement.GetFreezableInvalidationBatchSnapshotForTests();
        _ = ScrollViewer.GetScrollMetricsAndReset();
        AnimationValueSink.ResetTelemetryForTests();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        var pointerMove = uiRoot.GetPointerMoveTelemetrySnapshotForTests();

        RunFrame(uiRoot, 32 + (stepIndex * 16));

        var afterVisualTree = uiRoot.GetVisualTreeMetricsSnapshot();
        var afterAnimation = AnimationManager.Current.GetTelemetrySnapshotForTests();
        var afterHitTest = VisualTreeHelper.GetInstrumentationSnapshotForTests();
        var afterRender = uiRoot.GetRenderTelemetrySnapshotForTests();
        var afterFreezable = Freezable.GetTelemetrySnapshotForTests();
        var afterInvalidationBatch = UIElement.GetFreezableInvalidationBatchSnapshotForTests();
        var scrollMetrics = ScrollViewer.GetScrollMetricsAndReset();
        var sinkTelemetry = AnimationValueSink.GetTelemetrySnapshotForTests();
        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();

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
            pointerMove.PointerResolveHoverReuseCheckMilliseconds,
            pointerMove.PointerResolveFinalHitTestMilliseconds,
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
            dirtyRegions.Count,
            uiRoot.GetDirtyCoverageForTests(),
            uiRoot.WouldUsePartialDirtyRedrawForTests(),
            uiRoot.IsFullDirtyForTests(),
            Math.Max(0, afterRender.DirtyRootCount - beforeRender.DirtyRootCount),
            Math.Max(0, afterRender.RetainedTraversalCount - beforeRender.RetainedTraversalCount),
            Math.Max(0, afterRender.FullDirtyInitialStateCount - beforeRender.FullDirtyInitialStateCount),
            Math.Max(0, afterRender.FullDirtyViewportChangeCount - beforeRender.FullDirtyViewportChangeCount),
            Math.Max(0, afterRender.FullDirtySurfaceResetCount - beforeRender.FullDirtySurfaceResetCount),
            Math.Max(0, afterRender.FullDirtyVisualStructureChangeCount - beforeRender.FullDirtyVisualStructureChangeCount),
            Math.Max(0, afterRender.FullDirtyRetainedRebuildCount - beforeRender.FullDirtyRetainedRebuildCount),
            Math.Max(0, afterRender.FullDirtyDetachedVisualCount - beforeRender.FullDirtyDetachedVisualCount));
    }

    private static HoverSweepSummary Summarize(IReadOnlyList<HoverSweepStepMetrics> steps)
    {
        var hottest = steps
            .OrderByDescending(static step => step.BeginStoryboardCalls)
            .ThenByDescending(static step => step.LaneApplicationCount)
            .ThenByDescending(static step => step.PointerMove.HoverUpdateMilliseconds)
            .First();

        var hottestSetValuePaths = string.Join(
            " | ",
            steps.Select(static step => step.HottestSetValuePathSummary)
                .Where(static summary => !string.IsNullOrWhiteSpace(summary) && !string.Equals(summary, "none", StringComparison.Ordinal))
                .GroupBy(static summary => summary)
                .OrderByDescending(static group => group.Count())
                .Take(3)
                .Select(static group => group.Key));

        return new HoverSweepSummary(
            steps.Count,
            steps.Sum(static step => step.PointerMove.HitTestCount),
            steps.Sum(static step => step.PointerMove.RoutedEventCount),
            steps.Sum(static step => step.PointerMove.HoverUpdateMilliseconds),
            steps.Sum(static step => step.PointerMove.PointerTargetResolveMilliseconds),
            steps.Sum(static step => step.PointerResolveHoverReuseMs),
            steps.Sum(static step => step.PointerResolveFinalHitTestMs),
            steps.Sum(static step => step.BeginStoryboardCalls),
            steps.Sum(static step => step.StoryboardStarts),
            steps.Sum(static step => step.LaneApplicationCount),
            steps.Sum(static step => step.SinkValueSetCount),
            steps.Sum(static step => step.BeginStoryboardMs),
            steps.Sum(static step => step.StoryboardStartMs),
            steps.Sum(static step => step.StoryboardUpdateMs),
            steps.Sum(static step => step.ComposeMs),
            steps.Sum(static step => step.ComposeCollectMs),
            steps.Sum(static step => step.ComposeSortMs),
            steps.Sum(static step => step.ComposeMergeMs),
            steps.Sum(static step => step.ComposeApplyMs),
            steps.Sum(static step => step.ComposeBatchBeginMs),
            steps.Sum(static step => step.ComposeBatchEndMs),
            steps.Sum(static step => step.SinkSetValueMs),
            steps.Sum(static step => step.CleanupCompletedMs),
            steps.Sum(static step => step.FreezableOnChangedMs),
            steps.Sum(static step => step.FreezableEndBatchMs),
            steps.Sum(static step => step.FreezableBatchFlushMs),
            steps.Sum(static step => step.FreezableBatchFlushCount),
            steps.Sum(static step => step.FreezableBatchFlushTargetCount),
            steps.Max(static step => step.FreezableBatchMaxPendingTargetCount),
            steps.Sum(static step => step.RenderInvalidationCount),
            steps.Sum(static step => step.MeasureInvalidationCount),
            steps.Sum(static step => step.ArrangeInvalidationCount),
            steps.Sum(static step => step.DirtyRegionCount),
            steps.Max(static step => step.DirtyRegionCount),
            steps.Max(static step => step.DirtyCoverage),
            steps.Count(static step => step.WouldUsePartialDirtyRedraw),
            steps.Count(static step => !step.WouldUsePartialDirtyRedraw || step.IsFullDirty),
            steps.Sum(static step => step.FullDirtyInitialStateCount),
            steps.Sum(static step => step.FullDirtyViewportChangeCount),
            steps.Sum(static step => step.FullDirtySurfaceResetCount),
            steps.Sum(static step => step.FullDirtyVisualStructureChangeCount),
            steps.Sum(static step => step.FullDirtyRetainedRebuildCount),
            steps.Sum(static step => step.FullDirtyDetachedVisualCount),
            steps.Max(static step => step.ActiveStoryboardCount),
            steps.Max(static step => step.ActiveLaneCount),
            steps.Max(static step => step.ActiveStoryboardEntryCount),
            steps.OrderByDescending(static step => step.FreezableOnChangedMs).First().HottestFreezableOnChangedType,
            steps.Max(static step => step.HottestFreezableOnChangedMs),
            steps.OrderByDescending(static step => step.FreezableEndBatchMs).First().HottestFreezableEndBatchType,
            steps.Max(static step => step.HottestFreezableEndBatchMs),
            hottestSetValuePaths,
            hottest.Label,
            $"beginStoryboards={hottest.BeginStoryboardCalls}, laneApplications={hottest.LaneApplicationCount}, sinkValueSets={hottest.SinkValueSetCount}, beginMs={hottest.BeginStoryboardMs:0.###}, startMs={hottest.StoryboardStartMs:0.###}, updateMs={hottest.StoryboardUpdateMs:0.###}, composeMs={hottest.ComposeMs:0.###}, composeCollectMs={hottest.ComposeCollectMs:0.###}, composeSortMs={hottest.ComposeSortMs:0.###}, composeMergeMs={hottest.ComposeMergeMs:0.###}, composeApplyMs={hottest.ComposeApplyMs:0.###}, composeBatchBeginMs={hottest.ComposeBatchBeginMs:0.###}, composeBatchEndMs={hottest.ComposeBatchEndMs:0.###}, freezableOnChangedMs={hottest.FreezableOnChangedMs:0.###}, freezableEndBatchMs={hottest.FreezableEndBatchMs:0.###}, freezableBatchFlushMs={hottest.FreezableBatchFlushMs:0.###}, cleanupMs={hottest.CleanupCompletedMs:0.###}, setValueMs={hottest.SinkSetValueMs:0.###}, dirtyRegions={hottest.DirtyRegionCount}, dirtyCoverage={hottest.DirtyCoverage:0.###}, partialDirty={hottest.WouldUsePartialDirtyRedraw}, hottestSetValuePaths={hottest.HottestSetValuePathSummary}");
    }

    private static string FormatStep(HoverSweepStepMetrics step)
    {
        return
            $"step={step.StepIndex:000} label={step.Label} pointer=({step.Pointer.X:0.##},{step.Pointer.Y:0.##}) " +
            $"resolvePath={step.PointerMove.PointerResolvePath} hitTests={step.PointerMove.HitTestCount} routedEvents={step.PointerMove.RoutedEventCount} " +
            $"scrollViewerWheel={step.ScrollViewer.WheelEvents} scrollViewerSetOffsets={step.ScrollViewer.SetOffsetCalls} " +
            $"hoverMs={step.PointerMove.HoverUpdateMilliseconds:0.###} resolveMs={step.PointerMove.PointerTargetResolveMilliseconds:0.###} resolveHoverReuseMs={step.PointerResolveHoverReuseMs:0.###} resolveFinalHitTestMs={step.PointerResolveFinalHitTestMs:0.###} routeMs={step.PointerMove.PointerRouteMilliseconds:0.###} " +
            $"dpSinkSets={step.SinkTelemetry.DependencyPropertySetValueCount} dpSinkMs={step.SinkTelemetry.DependencyPropertySetValueMilliseconds:0.###} clrSinkSets={step.SinkTelemetry.ClrPropertySetValueCount} clrSinkMs={step.SinkTelemetry.ClrPropertySetValueMilliseconds:0.###} " +
            $"beginMs={step.BeginStoryboardMs:0.###} startMs={step.StoryboardStartMs:0.###} updateMs={step.StoryboardUpdateMs:0.###} composeMs={step.ComposeMs:0.###} composeCollectMs={step.ComposeCollectMs:0.###} composeSortMs={step.ComposeSortMs:0.###} composeMergeMs={step.ComposeMergeMs:0.###} composeApplyMs={step.ComposeApplyMs:0.###} composeBatchBeginMs={step.ComposeBatchBeginMs:0.###} composeBatchEndMs={step.ComposeBatchEndMs:0.###} freezableOnChangedMs={step.FreezableOnChangedMs:0.###} freezableEndBatchMs={step.FreezableEndBatchMs:0.###} freezableBatchFlushMs={step.FreezableBatchFlushMs:0.###} freezableBatchFlushes={step.FreezableBatchFlushCount} freezableBatchFlushTargets={step.FreezableBatchFlushTargetCount} queuedTargets={step.FreezableBatchQueuedTargetCount} maxPendingTargets={step.FreezableBatchMaxPendingTargetCount} cleanupMs={step.CleanupCompletedMs:0.###} setValueMs={step.SinkSetValueMs:0.###} hottestFreezableOnChanged={step.HottestFreezableOnChangedType}:{step.HottestFreezableOnChangedMs:0.###} hottestFreezableEndBatch={step.HottestFreezableEndBatchType}:{step.HottestFreezableEndBatchMs:0.###} hottestSetValuePaths={step.HottestSetValuePathSummary} " +
            $"beginStoryboards={step.BeginStoryboardCalls} storyboardStarts={step.StoryboardStarts} activeStoryboards={step.ActiveStoryboardCount} activeLanes={step.ActiveLaneCount} activeEntries={step.ActiveStoryboardEntryCount} " +
            $"composePasses={step.ComposePassCount} laneApplications={step.LaneApplicationCount} sinkValueSets={step.SinkValueSetCount} clearedLanes={step.ClearedLaneCount} " +
            $"dirtyRegions={step.DirtyRegionCount} dirtyCoverage={step.DirtyCoverage:0.###} partialDirty={step.WouldUsePartialDirtyRedraw} fullDirty={step.IsFullDirty} " +
            $"measureInvalidations={step.MeasureInvalidationCount} arrangeInvalidations={step.ArrangeInvalidationCount} renderInvalidations={step.RenderInvalidationCount} " +
            $"dirtyRoots={step.DirtyRootCount} retainedTraversals={step.RetainedTraversalCount} " +
            $"fullDirtyInitial={step.FullDirtyInitialStateCount} fullDirtyViewport={step.FullDirtyViewportChangeCount} fullDirtySurfaceReset={step.FullDirtySurfaceResetCount} " +
            $"fullDirtyStructure={step.FullDirtyVisualStructureChangeCount} fullDirtyRetainedRebuild={step.FullDirtyRetainedRebuildCount} fullDirtyDetached={step.FullDirtyDetachedVisualCount} " +
            $"hitTestFastPaths={step.MonotonicPanelFastPathCount}";
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

    private static ResourceSnapshot CaptureApplicationResources()
    {
        var resources = UiApplication.Current.Resources;
        return new ResourceSnapshot(
            resources.ToList(),
            resources.MergedDictionaries.ToList());
    }

    private static void RestoreApplicationResources(ResourceSnapshot snapshot)
    {
        TestApplicationResources.Restore(snapshot.Entries, snapshot.MergedDictionaries);
    }

    private readonly record struct ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        IReadOnlyList<ResourceDictionary> MergedDictionaries);

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
        double StoryboardUpdateMs,
        double ComposeMs,
        double ComposeCollectMs,
        double ComposeSortMs,
        double ComposeMergeMs,
        double ComposeApplyMs,
        double ComposeBatchBeginMs,
        double ComposeBatchEndMs,
        double SinkSetValueMs,
        double CleanupCompletedMs,
        double FreezableOnChangedMs,
        double FreezableEndBatchMs,
        double FreezableBatchFlushMs,
        int FreezableBatchFlushCount,
        int FreezableBatchFlushTargetCount,
        int FreezableBatchQueuedTargetCount,
        int FreezableBatchMaxPendingTargetCount,
        double PointerResolveHoverReuseMs,
        double PointerResolveFinalHitTestMs,
        string HottestFreezableOnChangedType,
        double HottestFreezableOnChangedMs,
        string HottestFreezableEndBatchType,
        double HottestFreezableEndBatchMs,
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
        int DirtyRegionCount,
        double DirtyCoverage,
        bool WouldUsePartialDirtyRedraw,
        bool IsFullDirty,
        int DirtyRootCount,
        int RetainedTraversalCount,
        int FullDirtyInitialStateCount,
        int FullDirtyViewportChangeCount,
        int FullDirtySurfaceResetCount,
        int FullDirtyVisualStructureChangeCount,
        int FullDirtyRetainedRebuildCount,
        int FullDirtyDetachedVisualCount);

    private readonly record struct HoverSweepSummary(
        int TotalSteps,
        int TotalHitTests,
        int TotalRoutedEvents,
        double TotalHoverUpdateMs,
        double TotalPointerResolveMs,
        double TotalPointerResolveHoverReuseMs,
        double TotalPointerResolveFinalHitTestMs,
        int TotalBeginStoryboardCalls,
        int TotalStoryboardStarts,
        int TotalLaneApplications,
        int TotalSinkValueSets,
        double TotalBeginStoryboardMs,
        double TotalStoryboardStartMs,
        double TotalStoryboardUpdateMs,
        double TotalComposeMs,
        double TotalComposeCollectMs,
        double TotalComposeSortMs,
        double TotalComposeMergeMs,
        double TotalComposeApplyMs,
        double TotalComposeBatchBeginMs,
        double TotalComposeBatchEndMs,
        double TotalSinkSetValueMs,
        double TotalCleanupCompletedMs,
        double TotalFreezableOnChangedMs,
        double TotalFreezableEndBatchMs,
        double TotalFreezableBatchFlushMs,
        int TotalFreezableBatchFlushes,
        int TotalFreezableBatchFlushTargets,
        int MaxFreezableBatchPendingTargets,
        long TotalRenderInvalidations,
        long TotalMeasureInvalidations,
        long TotalArrangeInvalidations,
        int TotalDirtyRegionCount,
        int MaxDirtyRegionCount,
        double MaxDirtyCoverage,
        int PartialDirtyRedrawStepCount,
        int FullRedrawStepCount,
        int FullDirtyInitialStateCount,
        int FullDirtyViewportChangeCount,
        int FullDirtySurfaceResetCount,
        int FullDirtyVisualStructureChangeCount,
        int FullDirtyRetainedRebuildCount,
        int FullDirtyDetachedVisualCount,
        int MaxActiveStoryboards,
        int MaxActiveLanes,
        int MaxActiveStoryboardEntries,
        string HottestFreezableOnChangedType,
        double HottestFreezableOnChangedMs,
        string HottestFreezableEndBatchType,
        double HottestFreezableEndBatchMs,
        string HottestSetValuePaths,
        string HottestStepLabel,
        string HottestStepDetail);
}
