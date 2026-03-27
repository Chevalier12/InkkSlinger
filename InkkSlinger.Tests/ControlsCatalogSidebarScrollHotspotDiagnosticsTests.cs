using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogSidebarScrollHotspotDiagnosticsTests
{
    private const int ViewportWidth = 1400;
    private const int ViewportHeight = 900;
    private const int WheelStepCount = 24;
    private const int WheelDeltaDown = -120;

    [Fact]
    public void SidebarScrollBar_RapidWheelScroll_WritesHotspotDiagnosticsLog()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var baseline = MeasureSidebarRapidWheelScroll(includeProgressBarPreview: false);
            var progressBar = MeasureSidebarRapidWheelScroll(includeProgressBarPreview: true);

            var logPath = GetDiagnosticsLogPath("controls-catalog-sidebar-scroll-hotspot");
            var lines = new List<string>
            {
                "scenario=ControlsCatalog sidebar vertical scrollbar rapid wheel scroll",
                $"timestamp_utc={DateTime.UtcNow:O}",
                $"log_path={logPath}",
                "step_1=open Controls Catalog",
                "step_2=move pointer over the sidebar vertical scrollbar",
                $"step_3=wheel-scroll down {WheelStepCount} times",
                "step_4=compare default preview vs ProgressBar preview",
                string.Empty,
                "baseline_summary:"
            };

            AppendSummaryLines(lines, baseline);
            lines.Add(string.Empty);
            lines.Add("progressbar_summary:");
            AppendSummaryLines(lines, progressBar);
            lines.Add(string.Empty);
            lines.Add($"hotspot_inference={BuildInference(baseline, progressBar)}");
            lines.Add(string.Empty);
            lines.Add("baseline_steps:");
            lines.AddRange(baseline.Steps.Select(FormatStep));
            lines.Add(string.Empty);
            lines.Add("progressbar_steps:");
            lines.AddRange(progressBar.Steps.Select(FormatStep));

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

    private static ScrollRunSummary MeasureSidebarRapidWheelScroll(bool includeProgressBarPreview)
    {
        _ = MeasureSidebarRapidWheelScrollCore(includeProgressBarPreview);
        return MeasureSidebarRapidWheelScrollCore(includeProgressBarPreview);
    }

    private static ScrollRunSummary MeasureSidebarRapidWheelScrollCore(bool includeProgressBarPreview)
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

        if (includeProgressBarPreview)
        {
            catalog.ShowControl("ProgressBar");
            RunFrame(uiRoot, 32);
        }

        PrimeRetainedRenderStateForDiagnostics(uiRoot);
        AnimationManager.Current.ResetTelemetryForTests();
        VisualTreeHelper.ResetInstrumentationForTests();
        Freezable.ResetTelemetryForTests();
        UIElement.ResetFreezableInvalidationBatchTelemetryForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight));

        var sidebarViewer = FindSidebarScrollViewer(catalog);
        var verticalBar = FindVerticalScrollBar(sidebarViewer);
        var pointer = GetCenter(verticalBar.LayoutSlot);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunFrame(uiRoot, 48);

        var steps = new List<ScrollStepMetrics>(WheelStepCount);
        for (var stepIndex = 0; stepIndex < WheelStepCount; stepIndex++)
        {
            steps.Add(RunWheelStep(
                uiRoot,
                pointer,
                WheelDeltaDown,
                includeProgressBarPreview ? $"progressbar:{stepIndex + 1}" : $"baseline:{stepIndex + 1}",
                stepIndex));
        }

        return Summarize(includeProgressBarPreview ? "ProgressBar" : "Default", steps);
    }

    private static ScrollStepMetrics RunWheelStep(UiRoot uiRoot, Vector2 pointer, int wheelDelta, string label, int stepIndex)
    {
        var beforeAnimation = AnimationManager.Current.GetTelemetrySnapshotForTests();
        var beforeVisualTree = uiRoot.GetVisualTreeMetricsSnapshot();
        var beforeRender = uiRoot.GetRenderTelemetrySnapshotForTests();
        var beforeFreezable = Freezable.GetTelemetrySnapshotForTests();
        var beforeFreezableBatch = UIElement.GetFreezableInvalidationBatchSnapshotForTests();
        _ = ScrollViewer.GetScrollMetricsAndReset();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, wheelDelta: wheelDelta));
        var pointerTelemetry = uiRoot.GetPointerMoveTelemetrySnapshotForTests();

        RunFrame(uiRoot, 64 + (stepIndex * 16));

        var afterAnimation = AnimationManager.Current.GetTelemetrySnapshotForTests();
        var afterVisualTree = uiRoot.GetVisualTreeMetricsSnapshot();
        var afterRender = uiRoot.GetRenderTelemetrySnapshotForTests();
        var afterPerformance = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        var renderInvalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
        var afterFreezable = Freezable.GetTelemetrySnapshotForTests();
        var afterFreezableBatch = UIElement.GetFreezableInvalidationBatchSnapshotForTests();
        var scrollMetrics = ScrollViewer.GetScrollMetricsAndReset();

        return new ScrollStepMetrics(
            stepIndex,
            label,
            pointer,
            scrollMetrics,
            pointerTelemetry,
            uiRoot.LastUpdateMs,
            uiRoot.LastInputPhaseMs,
            uiRoot.LastLayoutPhaseMs,
            uiRoot.LastAnimationPhaseMs,
            uiRoot.LastRenderSchedulingPhaseMs,
            afterPerformance.FrameUpdateParticipantCount,
            afterPerformance.FrameUpdateParticipantRefreshCount,
            afterPerformance.FrameUpdateParticipantRefreshMilliseconds,
            afterPerformance.FrameUpdateParticipantUpdateMilliseconds,
            afterPerformance.HottestFrameUpdateParticipantType,
            afterPerformance.HottestFrameUpdateParticipantMilliseconds,
            afterPerformance.HottestLayoutMeasureElementType,
            afterPerformance.HottestLayoutMeasureElementName,
            afterPerformance.HottestLayoutMeasureElementMilliseconds,
            afterPerformance.HottestLayoutArrangeElementType,
            afterPerformance.HottestLayoutArrangeElementName,
            afterPerformance.HottestLayoutArrangeElementMilliseconds,
            afterAnimation.BeginStoryboardCallCount - beforeAnimation.BeginStoryboardCallCount,
            afterAnimation.StoryboardStartCount - beforeAnimation.StoryboardStartCount,
            afterAnimation.ComposePassCount - beforeAnimation.ComposePassCount,
            afterAnimation.LaneApplicationCount - beforeAnimation.LaneApplicationCount,
            afterAnimation.SinkValueSetCount - beforeAnimation.SinkValueSetCount,
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
            afterAnimation.HottestSetValuePathSummary,
            afterFreezable.OnChangedMilliseconds - beforeFreezable.OnChangedMilliseconds,
            afterFreezable.EndBatchMilliseconds - beforeFreezable.EndBatchMilliseconds,
            afterFreezable.HottestOnChangedType,
            afterFreezable.HottestOnChangedMilliseconds,
            afterFreezable.HottestEndBatchType,
            afterFreezable.HottestEndBatchMilliseconds,
            afterFreezableBatch.FlushMilliseconds - beforeFreezableBatch.FlushMilliseconds,
            afterFreezableBatch.FlushCount - beforeFreezableBatch.FlushCount,
            afterFreezableBatch.FlushTargetCount - beforeFreezableBatch.FlushTargetCount,
            afterFreezableBatch.MaxPendingTargetCount,
            afterVisualTree.MeasureInvalidationCount - beforeVisualTree.MeasureInvalidationCount,
            afterVisualTree.ArrangeInvalidationCount - beforeVisualTree.ArrangeInvalidationCount,
            afterVisualTree.RenderInvalidationCount - beforeVisualTree.RenderInvalidationCount,
            afterRender.DirtyRootCount,
            afterRender.RetainedTraversalCount,
            renderInvalidation.EffectiveSourceType,
            renderInvalidation.EffectiveSourceName,
            afterRender.FullDirtyInitialStateCount,
            afterRender.FullDirtyViewportChangeCount,
            afterRender.FullDirtySurfaceResetCount,
            afterRender.FullDirtyVisualStructureChangeCount,
            afterRender.FullDirtyRetainedRebuildCount,
            afterRender.FullDirtyDetachedVisualCount);
    }

    private static ScrollRunSummary Summarize(string label, IReadOnlyList<ScrollStepMetrics> steps)
    {
        var hottest = steps
            .OrderByDescending(static step => step.LastUpdateMs)
            .ThenByDescending(static step => step.FrameUpdateParticipantUpdateMs)
            .ThenByDescending(static step => step.LastLayoutPhaseMs)
            .First();

        return new ScrollRunSummary(
            label,
            steps,
            steps.Sum(static step => step.LastUpdateMs),
            steps.Sum(static step => step.LastInputPhaseMs),
            steps.Sum(static step => step.LastLayoutPhaseMs),
            steps.Sum(static step => step.LastAnimationPhaseMs),
            steps.Sum(static step => step.LastRenderSchedulingPhaseMs),
            steps.Sum(static step => step.ScrollViewer.WheelEvents),
            steps.Sum(static step => step.ScrollViewer.WheelHandled),
            steps.Sum(static step => step.ScrollViewer.SetOffsetCalls),
            steps.Sum(static step => step.ScrollViewer.SetOffsetNoOpCalls),
            steps.Sum(static step => step.ScrollViewer.TotalVerticalDelta),
            steps.Sum(static step => step.FrameUpdateParticipantCount),
            steps.Sum(static step => step.FrameUpdateParticipantRefreshCount),
            steps.Sum(static step => step.FrameUpdateParticipantRefreshMs),
            steps.Sum(static step => step.FrameUpdateParticipantUpdateMs),
            steps.OrderByDescending(static step => step.FrameUpdateParticipantUpdateMs).First().HottestFrameUpdateParticipantType,
            steps.Max(static step => step.HottestFrameUpdateParticipantMs),
            steps.Sum(static step => step.BeginStoryboardCalls),
            steps.Sum(static step => step.ComposePassCount),
            steps.Sum(static step => step.LaneApplicationCount),
            steps.Sum(static step => step.SinkValueSetCount),
            steps.Sum(static step => step.ComposeApplyMs),
            steps.Sum(static step => step.SinkSetValueMs),
            steps.Sum(static step => step.CleanupCompletedMs),
            steps.Sum(static step => step.FreezableOnChangedMs),
            steps.Sum(static step => step.FreezableEndBatchMs),
            steps.Sum(static step => step.FreezableBatchFlushMs),
            steps.Sum(static step => step.FreezableBatchFlushCount),
            steps.Sum(static step => step.FreezableBatchFlushTargetCount),
            steps.Max(static step => step.FreezableBatchMaxPendingTargetCount),
            steps.Sum(static step => step.MeasureInvalidationCount),
            steps.Sum(static step => step.ArrangeInvalidationCount),
            steps.Sum(static step => step.RenderInvalidationCount),
            steps.Sum(static step => step.DirtyRootCount),
            steps.Sum(static step => step.RetainedTraversalCount),
            hottest.Label,
            $"updateMs={hottest.LastUpdateMs:0.###}, inputMs={hottest.LastInputPhaseMs:0.###}, layoutMs={hottest.LastLayoutPhaseMs:0.###}, animationMs={hottest.LastAnimationPhaseMs:0.###}, renderScheduleMs={hottest.LastRenderSchedulingPhaseMs:0.###}, frameParticipantMs={hottest.FrameUpdateParticipantUpdateMs:0.###}, hottestFrameParticipant={hottest.HottestFrameUpdateParticipantType}:{hottest.HottestFrameUpdateParticipantMs:0.###}, hottestLayoutMeasure={hottest.HottestLayoutMeasureElementType}#{hottest.HottestLayoutMeasureElementName}:{hottest.HottestLayoutMeasureElementMs:0.###}, hottestLayoutArrange={hottest.HottestLayoutArrangeElementType}#{hottest.HottestLayoutArrangeElementName}:{hottest.HottestLayoutArrangeElementMs:0.###}, effectiveRenderSource={hottest.EffectiveRenderInvalidationSourceType}#{hottest.EffectiveRenderInvalidationSourceName}, composeApplyMs={hottest.ComposeApplyMs:0.###}, freezableEndBatchMs={hottest.FreezableEndBatchMs:0.###}, renderInvalidations={hottest.RenderInvalidationCount}, dirtyRoots={hottest.DirtyRootCount}, retainedTraversals={hottest.RetainedTraversalCount}");
    }

    private static void AppendSummaryLines(ICollection<string> lines, ScrollRunSummary summary)
    {
        lines.Add($"label={summary.Label}");
        lines.Add($"steps={summary.Steps.Count}");
        lines.Add($"total_update_ms={summary.TotalUpdateMs:0.###}");
        lines.Add($"total_input_ms={summary.TotalInputMs:0.###}");
        lines.Add($"total_layout_ms={summary.TotalLayoutMs:0.###}");
        lines.Add($"total_animation_ms={summary.TotalAnimationMs:0.###}");
        lines.Add($"total_render_scheduling_ms={summary.TotalRenderSchedulingMs:0.###}");
        lines.Add($"total_scrollviewer_wheel_events={summary.TotalWheelEvents}");
        lines.Add($"total_scrollviewer_wheel_handled={summary.TotalWheelHandled}");
        lines.Add($"total_scrollviewer_setoffset_calls={summary.TotalSetOffsetCalls}");
        lines.Add($"total_scrollviewer_setoffset_noops={summary.TotalSetOffsetNoOps}");
        lines.Add($"total_scrollviewer_vertical_delta={summary.TotalVerticalDelta:0.###}");
        lines.Add($"total_frame_update_participants={summary.TotalFrameUpdateParticipants}");
        lines.Add($"total_frame_update_refreshes={summary.TotalFrameUpdateRefreshes}");
        lines.Add($"total_frame_update_refresh_ms={summary.TotalFrameUpdateRefreshMs:0.###}");
        lines.Add($"total_frame_update_update_ms={summary.TotalFrameUpdateUpdateMs:0.###}");
        lines.Add($"hottest_frame_update_participant={summary.HottestFrameUpdateParticipantType}:{summary.HottestFrameUpdateParticipantMs:0.###}");
        lines.Add($"total_begin_storyboard_calls={summary.TotalBeginStoryboardCalls}");
        lines.Add($"total_compose_passes={summary.TotalComposePasses}");
        lines.Add($"total_lane_applications={summary.TotalLaneApplications}");
        lines.Add($"total_sink_value_sets={summary.TotalSinkValueSets}");
        lines.Add($"total_compose_apply_ms={summary.TotalComposeApplyMs:0.###}");
        lines.Add($"total_sink_setvalue_ms={summary.TotalSinkSetValueMs:0.###}");
        lines.Add($"total_cleanup_completed_ms={summary.TotalCleanupCompletedMs:0.###}");
        lines.Add($"total_freezable_onchanged_ms={summary.TotalFreezableOnChangedMs:0.###}");
        lines.Add($"total_freezable_end_batch_ms={summary.TotalFreezableEndBatchMs:0.###}");
        lines.Add($"total_freezable_batch_flush_ms={summary.TotalFreezableBatchFlushMs:0.###}");
        lines.Add($"total_freezable_batch_flushes={summary.TotalFreezableBatchFlushCount}");
        lines.Add($"total_freezable_batch_flush_targets={summary.TotalFreezableBatchFlushTargetCount}");
        lines.Add($"max_freezable_batch_pending_targets={summary.MaxFreezableBatchPendingTargets}");
        lines.Add($"total_measure_invalidations={summary.TotalMeasureInvalidations}");
        lines.Add($"total_arrange_invalidations={summary.TotalArrangeInvalidations}");
        lines.Add($"total_render_invalidations={summary.TotalRenderInvalidations}");
        lines.Add($"total_dirty_roots={summary.TotalDirtyRoots}");
        lines.Add($"total_retained_traversals={summary.TotalRetainedTraversals}");
        lines.Add($"hottest_step={summary.HottestStepLabel}");
        lines.Add($"hottest_step_detail={summary.HottestStepDetail}");
    }

    private static string BuildInference(ScrollRunSummary baseline, ScrollRunSummary progressBar)
    {
        var progressBarFrameUpdateDeltaMs = progressBar.TotalFrameUpdateUpdateMs - baseline.TotalFrameUpdateUpdateMs;
        var progressBarLayoutDeltaMs = progressBar.TotalLayoutMs - baseline.TotalLayoutMs;

        if (baseline.TotalLayoutMs < 1d &&
            progressBar.TotalLayoutMs < 1d &&
            baseline.TotalDirtyRoots > 0 &&
            progressBar.TotalDirtyRoots > baseline.TotalDirtyRoots)
        {
            return "The old layout hotspot is gone. The remaining hotspot is UiRoot.RunRenderSchedulingPhase -> SynchronizeRetainedRenderList, now driven by one retained dirty root per sidebar wheel step while Track#PART_Track remains the effective render invalidation source. The ProgressBar still adds PART_Indicator/PART_GlowRect dirty roots during its batched TranslateTransform animation, so retained-render sync remains the dominant shared cost when scrolling and animating at the same time.";
        }

        if (progressBar.TotalFrameUpdateParticipants > baseline.TotalFrameUpdateParticipants &&
            progressBarFrameUpdateDeltaMs > Math.Max(0.5d, progressBarLayoutDeltaMs) &&
            progressBarFrameUpdateDeltaMs > 1d)
        {
            return "Exact remaining hotspot: UiRoot.RunFrameUpdateParticipants spends most of the added ProgressBar-preview cost updating the indeterminate ProgressBar each frame while the sidebar is also processing wheel scroll. The scroll path itself stays light enough that the extra frame-update work dominates the regression.";
        }

        if (baseline.TotalLayoutMs >= baseline.TotalInputMs &&
            baseline.TotalLayoutMs >= baseline.TotalAnimationMs)
        {
            return "Exact remaining hotspot: sidebar wheel scroll is dominated by layout and retained-render synchronization even before ProgressBar is open, so the first fix target should stay on the ScrollViewer/layout/render-scheduling path rather than animation startup.";
        }

        return "Logs isolated a hotspot in the sidebar scroll scenario, but this pass only narrows it to a subsystem. Add narrower control-level instrumentation before changing runtime behavior.";
    }

    private static string FormatStep(ScrollStepMetrics step)
    {
        return
            $"step={step.StepIndex:000} label={step.Label} pointer=({step.Pointer.X:0.##},{step.Pointer.Y:0.##}) " +
            $"wheelEvents={step.ScrollViewer.WheelEvents} wheelHandled={step.ScrollViewer.WheelHandled} setOffsetCalls={step.ScrollViewer.SetOffsetCalls} setOffsetNoOps={step.ScrollViewer.SetOffsetNoOpCalls} verticalDelta={step.ScrollViewer.TotalVerticalDelta:0.###} " +
            $"updateMs={step.LastUpdateMs:0.###} inputMs={step.LastInputPhaseMs:0.###} layoutMs={step.LastLayoutPhaseMs:0.###} animationMs={step.LastAnimationPhaseMs:0.###} renderScheduleMs={step.LastRenderSchedulingPhaseMs:0.###} " +
            $"pointerResolvePath={step.PointerTelemetry.PointerResolvePath} hitTests={step.PointerTelemetry.HitTestCount} routedEvents={step.PointerTelemetry.RoutedEventCount} " +
            $"frameParticipants={step.FrameUpdateParticipantCount} frameRefreshes={step.FrameUpdateParticipantRefreshCount} frameRefreshMs={step.FrameUpdateParticipantRefreshMs:0.###} frameUpdateMs={step.FrameUpdateParticipantUpdateMs:0.###} hottestFrameParticipant={step.HottestFrameUpdateParticipantType}:{step.HottestFrameUpdateParticipantMs:0.###} hottestLayoutMeasure={step.HottestLayoutMeasureElementType}#{step.HottestLayoutMeasureElementName}:{step.HottestLayoutMeasureElementMs:0.###} hottestLayoutArrange={step.HottestLayoutArrangeElementType}#{step.HottestLayoutArrangeElementName}:{step.HottestLayoutArrangeElementMs:0.###} " +
            $"beginStoryboards={step.BeginStoryboardCalls} storyboardStarts={step.StoryboardStarts} composePasses={step.ComposePassCount} laneApplications={step.LaneApplicationCount} sinkValueSets={step.SinkValueSetCount} composeApplyMs={step.ComposeApplyMs:0.###} sinkSetValueMs={step.SinkSetValueMs:0.###} cleanupMs={step.CleanupCompletedMs:0.###} hottestSetValuePaths={step.HottestSetValuePathSummary} " +
            $"freezableOnChangedMs={step.FreezableOnChangedMs:0.###} freezableEndBatchMs={step.FreezableEndBatchMs:0.###} hottestFreezableOnChanged={step.HottestFreezableOnChangedType}:{step.HottestFreezableOnChangedMs:0.###} hottestFreezableEndBatch={step.HottestFreezableEndBatchType}:{step.HottestFreezableEndBatchMs:0.###} freezableBatchFlushMs={step.FreezableBatchFlushMs:0.###} freezableBatchFlushes={step.FreezableBatchFlushCount} flushTargets={step.FreezableBatchFlushTargetCount} maxPendingTargets={step.FreezableBatchMaxPendingTargetCount} " +
            $"measureInvalidations={step.MeasureInvalidationCount} arrangeInvalidations={step.ArrangeInvalidationCount} renderInvalidations={step.RenderInvalidationCount} dirtyRoots={step.DirtyRootCount} retainedTraversals={step.RetainedTraversalCount} effectiveRenderSource={step.EffectiveRenderInvalidationSourceType}#{step.EffectiveRenderInvalidationSourceName} " +
            $"fullDirtyInitial={step.FullDirtyInitialStateCount} fullDirtyViewport={step.FullDirtyViewportChangeCount} fullDirtySurfaceReset={step.FullDirtySurfaceResetCount} fullDirtyStructure={step.FullDirtyVisualStructureChangeCount} fullDirtyRetainedRebuild={step.FullDirtyRetainedRebuildCount} fullDirtyDetached={step.FullDirtyDetachedVisualCount}";
    }

    private static ScrollViewer FindSidebarScrollViewer(ControlsCatalogView catalog)
    {
        return FindFirstVisualChild<ScrollViewer>(
                   catalog,
                   static viewer => viewer.Content is StackPanel host &&
                                    string.Equals(host.Name, "ControlButtonsHost", StringComparison.Ordinal))
               ?? throw new InvalidOperationException("Could not find sidebar ScrollViewer.");
    }

    private static ScrollBar FindVerticalScrollBar(ScrollViewer viewer)
    {
        return FindFirstVisualChild<ScrollBar>(
                   viewer,
                   static bar => bar.Orientation == Orientation.Vertical && bar.LayoutSlot.Height > 0f)
               ?? throw new InvalidOperationException("Could not find sidebar vertical ScrollBar.");
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

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool pointerMoved = false, int wheelDelta = 0)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved,
            WheelDelta = wheelDelta,
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

    private readonly record struct ScrollStepMetrics(
        int StepIndex,
        string Label,
        Vector2 Pointer,
        ScrollViewerScrollMetricsSnapshot ScrollViewer,
        UiPointerMoveTelemetrySnapshot PointerTelemetry,
        double LastUpdateMs,
        double LastInputPhaseMs,
        double LastLayoutPhaseMs,
        double LastAnimationPhaseMs,
        double LastRenderSchedulingPhaseMs,
        int FrameUpdateParticipantCount,
        int FrameUpdateParticipantRefreshCount,
        double FrameUpdateParticipantRefreshMs,
        double FrameUpdateParticipantUpdateMs,
        string HottestFrameUpdateParticipantType,
        double HottestFrameUpdateParticipantMs,
        string HottestLayoutMeasureElementType,
        string HottestLayoutMeasureElementName,
        double HottestLayoutMeasureElementMs,
        string HottestLayoutArrangeElementType,
        string HottestLayoutArrangeElementName,
        double HottestLayoutArrangeElementMs,
        int BeginStoryboardCalls,
        int StoryboardStarts,
        int ComposePassCount,
        int LaneApplicationCount,
        int SinkValueSetCount,
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
        string HottestSetValuePathSummary,
        double FreezableOnChangedMs,
        double FreezableEndBatchMs,
        string HottestFreezableOnChangedType,
        double HottestFreezableOnChangedMs,
        string HottestFreezableEndBatchType,
        double HottestFreezableEndBatchMs,
        double FreezableBatchFlushMs,
        int FreezableBatchFlushCount,
        int FreezableBatchFlushTargetCount,
        int FreezableBatchMaxPendingTargetCount,
        long MeasureInvalidationCount,
        long ArrangeInvalidationCount,
        long RenderInvalidationCount,
        int DirtyRootCount,
        int RetainedTraversalCount,
        string EffectiveRenderInvalidationSourceType,
        string EffectiveRenderInvalidationSourceName,
        int FullDirtyInitialStateCount,
        int FullDirtyViewportChangeCount,
        int FullDirtySurfaceResetCount,
        int FullDirtyVisualStructureChangeCount,
        int FullDirtyRetainedRebuildCount,
        int FullDirtyDetachedVisualCount);

    private readonly record struct ScrollRunSummary(
        string Label,
        IReadOnlyList<ScrollStepMetrics> Steps,
        double TotalUpdateMs,
        double TotalInputMs,
        double TotalLayoutMs,
        double TotalAnimationMs,
        double TotalRenderSchedulingMs,
        int TotalWheelEvents,
        int TotalWheelHandled,
        int TotalSetOffsetCalls,
        int TotalSetOffsetNoOps,
        float TotalVerticalDelta,
        int TotalFrameUpdateParticipants,
        int TotalFrameUpdateRefreshes,
        double TotalFrameUpdateRefreshMs,
        double TotalFrameUpdateUpdateMs,
        string HottestFrameUpdateParticipantType,
        double HottestFrameUpdateParticipantMs,
        int TotalBeginStoryboardCalls,
        int TotalComposePasses,
        int TotalLaneApplications,
        int TotalSinkValueSets,
        double TotalComposeApplyMs,
        double TotalSinkSetValueMs,
        double TotalCleanupCompletedMs,
        double TotalFreezableOnChangedMs,
        double TotalFreezableEndBatchMs,
        double TotalFreezableBatchFlushMs,
        int TotalFreezableBatchFlushCount,
        int TotalFreezableBatchFlushTargetCount,
        int MaxFreezableBatchPendingTargets,
        long TotalMeasureInvalidations,
        long TotalArrangeInvalidations,
        long TotalRenderInvalidations,
        int TotalDirtyRoots,
        int TotalRetainedTraversals,
        string HottestStepLabel,
        string HottestStepDetail);
}
