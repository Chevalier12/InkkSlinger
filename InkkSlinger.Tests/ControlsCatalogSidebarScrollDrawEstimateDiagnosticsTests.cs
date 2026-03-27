using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogSidebarScrollDrawEstimateDiagnosticsTests
{
    private const int ViewportWidth = 1400;
    private const int ViewportHeight = 900;
    private const int WheelStepCount = 24;
    private const int WheelDeltaDown = -120;

    [Fact]
    public void SidebarScrollBar_RapidWheelScroll_WritesDrawEstimateDiagnosticsLog()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var baseline = MeasureRun(includeProgressBarPreview: false);
            var progressBar = MeasureRun(includeProgressBarPreview: true);

            var logPath = GetDiagnosticsLogPath("controls-catalog-sidebar-scroll-draw-estimate-hotspot");
            var lines = new List<string>
            {
                "scenario=ControlsCatalog sidebar vertical scrollbar rapid wheel scroll with draw-estimate diagnostics",
                $"timestamp_utc={DateTime.UtcNow:O}",
                $"log_path={logPath}",
                "step_1=open Controls Catalog",
                "step_2=move pointer over the sidebar vertical scrollbar",
                $"step_3=wheel-scroll down {WheelStepCount} times",
                "step_4=estimate draw work after each update for default preview and ProgressBar preview",
                string.Empty,
                "baseline_summary:"
            };

            AppendSummary(lines, baseline);
            lines.Add(string.Empty);
            lines.Add("progressbar_summary:");
            AppendSummary(lines, progressBar);
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
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static RunSummary MeasureRun(bool includeProgressBarPreview)
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
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight));

        var steps = new List<StepMetrics>(WheelStepCount);
        for (var i = 0; i < WheelStepCount; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, wheelDelta: WheelDeltaDown));
            RunFrame(uiRoot, 64 + (i * 16));

            var drawEstimate = EstimateDrawWork(uiRoot, 64 + (i * 16));
            var perf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
            var render = uiRoot.GetRenderTelemetrySnapshotForTests();
            var renderInvalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
            var batch = UIElement.GetFreezableInvalidationBatchSnapshotForTests();

            steps.Add(new StepMetrics(
                i,
                includeProgressBarPreview ? $"progressbar:{i + 1}" : $"baseline:{i + 1}",
                uiRoot.LastUpdateMs,
                uiRoot.LastInputPhaseMs,
                uiRoot.LastLayoutPhaseMs,
                uiRoot.LastAnimationPhaseMs,
                uiRoot.LastRenderSchedulingPhaseMs,
                perf.FrameUpdateParticipantUpdateMilliseconds,
                perf.HottestFrameUpdateParticipantType,
                perf.HottestFrameUpdateParticipantMilliseconds,
                render.DirtyRootCount,
                renderInvalidation.EffectiveSourceType,
                renderInvalidation.EffectiveSourceName,
                batch.FlushMilliseconds,
                batch.FlushCount,
                drawEstimate.ShouldDraw,
                drawEstimate.ScheduledReasons,
                drawEstimate.IsFullDirty,
                drawEstimate.DirtyRegionCount,
                drawEstimate.DirtyCoverage,
                drawEstimate.WouldUsePartialRedraw,
                drawEstimate.EstimatedTraversalCount,
                drawEstimate.EstimatedNodesVisited,
                drawEstimate.EstimatedNodesDrawn,
                drawEstimate.EstimatedClipPushCount,
                drawEstimate.EstimatedTopDrawnTypes));

            uiRoot.CompleteDrawStateForTests();
            uiRoot.ResetDirtyStateForTests();
            uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight));
        }

        return new RunSummary(
            includeProgressBarPreview ? "ProgressBar" : "Default",
            steps,
            steps.Sum(static step => step.UpdateMs),
            steps.Sum(static step => step.RenderSchedulingMs),
            steps.Sum(static step => step.FrameParticipantMs),
            steps.Sum(static step => step.DirtyRoots),
            steps.Sum(static step => step.EstimatedNodesVisited),
            steps.Sum(static step => step.EstimatedNodesDrawn),
            steps.Max(static step => step.DirtyCoverage),
            steps.MaxBy(static step => step.UpdateMs)!);
    }

    private static DrawEstimate EstimateDrawWork(UiRoot uiRoot, int elapsedMs)
    {
        var viewport = new Viewport(0, 0, ViewportWidth, ViewportHeight);
        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(16)),
            viewport);

        var isFullDirty = uiRoot.IsFullDirtyForTests();
        var dirtyCoverage = uiRoot.GetDirtyCoverageForTests();
        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();
        var wouldUsePartial = uiRoot.WouldUsePartialDirtyRedrawForTests();
        var clips = new List<LayoutRect>();
        if (isFullDirty || dirtyRegions.Count == 0 || !wouldUsePartial)
        {
            clips.Add(new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight));
        }
        else
        {
            clips.AddRange(dirtyRegions);
        }

        var totalVisited = 0;
        var totalDrawn = 0;
        var totalClipPushCount = 0;
        var drawnTypeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var clip in clips)
        {
            var metrics = uiRoot.GetRetainedTraversalMetricsForClipForTests(clip);
            totalVisited += metrics.NodesVisited;
            totalDrawn += metrics.NodesDrawn;
            totalClipPushCount += metrics.LocalClipPushCount;

            foreach (var visual in uiRoot.GetRetainedDrawOrderForClipForTests(clip))
            {
                var typeName = visual.GetType().Name;
                drawnTypeCounts[typeName] = drawnTypeCounts.TryGetValue(typeName, out var count) ? count + 1 : 1;
            }
        }

        var topTypes = drawnTypeCounts.Count == 0
            ? "none"
            : string.Join(
                ", ",
                drawnTypeCounts
                    .OrderByDescending(static pair => pair.Value)
                    .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Take(6)
                    .Select(static pair => $"{pair.Key}x{pair.Value}"));

        return new DrawEstimate(
            shouldDraw,
            uiRoot.GetScheduledDrawReasonsForTests(),
            isFullDirty,
            dirtyRegions.Count,
            dirtyCoverage,
            wouldUsePartial,
            clips.Count,
            totalVisited,
            totalDrawn,
            totalClipPushCount,
            topTypes);
    }

    private static void AppendSummary(ICollection<string> lines, RunSummary summary)
    {
        lines.Add($"label={summary.Label}");
        lines.Add($"steps={summary.Steps.Count}");
        lines.Add($"total_update_ms={summary.TotalUpdateMs:0.###}");
        lines.Add($"total_render_scheduling_ms={summary.TotalRenderSchedulingMs:0.###}");
        lines.Add($"total_frame_participant_ms={summary.TotalFrameParticipantMs:0.###}");
        lines.Add($"total_dirty_roots={summary.TotalDirtyRoots}");
        lines.Add($"total_estimated_nodes_visited={summary.TotalEstimatedNodesVisited}");
        lines.Add($"total_estimated_nodes_drawn={summary.TotalEstimatedNodesDrawn}");
        lines.Add($"max_dirty_coverage={summary.MaxDirtyCoverage:0.###}");
        lines.Add($"hottest_step={summary.HottestStep.Label}");
        lines.Add($"hottest_step_detail={FormatStep(summary.HottestStep)}");
    }

    private static string BuildInference(RunSummary baseline, RunSummary progressBar)
    {
        if (progressBar.Steps.All(static step => !step.WouldUsePartialRedraw) &&
            progressBar.Steps.All(static step => step.EstimatedTraversalCount == 1) &&
            progressBar.TotalRenderSchedulingMs >= progressBar.TotalFrameParticipantMs)
        {
            return "The old repeated partial-redraw hotspot is gone. With ProgressBar open, the renderer still falls back to one retained traversal per scroll step instead of 2-3 clipped traversals. Track#PART_Track remains the effective render invalidation source, but it no longer adds its own retained dirty root, leaving one scroll dirty root in the default case and two total dirty roots once PART_Indicator/PART_GlowRect join in during ProgressBar animation.";
        }

        return "Draw-estimate instrumentation did not overturn the retained-render hypothesis. The next loop should instrument actual OnRender cost per control type if manual FPS is still much worse than this synthetic repro suggests.";
    }

    private static string FormatStep(StepMetrics step)
    {
        return
            $"step={step.StepIndex:000} label={step.Label} updateMs={step.UpdateMs:0.###} inputMs={step.InputMs:0.###} layoutMs={step.LayoutMs:0.###} animationMs={step.AnimationMs:0.###} renderScheduleMs={step.RenderSchedulingMs:0.###} " +
            $"frameParticipantMs={step.FrameParticipantMs:0.###} hottestFrameParticipant={step.HottestFrameParticipantType}:{step.HottestFrameParticipantMs:0.###} " +
            $"dirtyRoots={step.DirtyRoots} effectiveRenderSource={step.EffectiveRenderSourceType}#{step.EffectiveRenderSourceName} " +
            $"freezableBatchFlushMs={step.FreezableBatchFlushMs:0.###} freezableBatchFlushes={step.FreezableBatchFlushCount} " +
            $"shouldDraw={step.ShouldDraw} scheduledReasons={step.ScheduledReasons} fullDirty={step.IsFullDirty} dirtyRegionCount={step.DirtyRegionCount} dirtyCoverage={step.DirtyCoverage:0.###} partialDirty={step.WouldUsePartialRedraw} " +
            $"estimatedTraversals={step.EstimatedTraversalCount} estimatedVisited={step.EstimatedNodesVisited} estimatedDrawn={step.EstimatedNodesDrawn} estimatedClipPushes={step.EstimatedClipPushCount} topDrawnTypes={step.EstimatedTopDrawnTypes}";
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
        var appPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "App.xml"));
        Assert.True(File.Exists(appPath), $"Expected App.xml to exist at '{appPath}'.");
        XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);
    }

    private static ResourceSnapshot CaptureApplicationResources()
    {
        var resources = UiApplication.Current.Resources;
        return new ResourceSnapshot(resources.ToList(), resources.MergedDictionaries.ToList());
    }

    private static void RestoreApplicationResources(ResourceSnapshot snapshot)
    {
        TestApplicationResources.Restore(snapshot.Entries, snapshot.MergedDictionaries);
    }

    private readonly record struct ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        IReadOnlyList<ResourceDictionary> MergedDictionaries);

    private readonly record struct DrawEstimate(
        bool ShouldDraw,
        UiRedrawReason ScheduledReasons,
        bool IsFullDirty,
        int DirtyRegionCount,
        double DirtyCoverage,
        bool WouldUsePartialRedraw,
        int EstimatedTraversalCount,
        int EstimatedNodesVisited,
        int EstimatedNodesDrawn,
        int EstimatedClipPushCount,
        string EstimatedTopDrawnTypes);

    private readonly record struct StepMetrics(
        int StepIndex,
        string Label,
        double UpdateMs,
        double InputMs,
        double LayoutMs,
        double AnimationMs,
        double RenderSchedulingMs,
        double FrameParticipantMs,
        string HottestFrameParticipantType,
        double HottestFrameParticipantMs,
        int DirtyRoots,
        string EffectiveRenderSourceType,
        string EffectiveRenderSourceName,
        double FreezableBatchFlushMs,
        int FreezableBatchFlushCount,
        bool ShouldDraw,
        UiRedrawReason ScheduledReasons,
        bool IsFullDirty,
        int DirtyRegionCount,
        double DirtyCoverage,
        bool WouldUsePartialRedraw,
        int EstimatedTraversalCount,
        int EstimatedNodesVisited,
        int EstimatedNodesDrawn,
        int EstimatedClipPushCount,
        string EstimatedTopDrawnTypes);

    private readonly record struct RunSummary(
        string Label,
        IReadOnlyList<StepMetrics> Steps,
        double TotalUpdateMs,
        double TotalRenderSchedulingMs,
        double TotalFrameParticipantMs,
        int TotalDirtyRoots,
        int TotalEstimatedNodesVisited,
        int TotalEstimatedNodesDrawn,
        double MaxDirtyCoverage,
        StepMetrics HottestStep);
}
