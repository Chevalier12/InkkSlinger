using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogCanvasThumbDragHotspotDiagnosticsTests
{
    private const int ViewportWidth = 1280;
    private const int ViewportHeight = 820;

    [Fact]
    public void CanvasPreview_ThumbDrag_WritesHotspotDiagnosticsLog()
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
            _ = Panel.GetTelemetryAndReset();
            _ = Canvas.GetTelemetryAndReset();
            _ = Thumb.GetDragTelemetryAndReset();
            _ = CanvasView.GetDiagnosticsAndReset();

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

            catalog.ShowControl("Canvas");
            RunFrame(uiRoot, 32);
            RunFrame(uiRoot, 48);
            PrimeRetainedRenderStateForDiagnostics(uiRoot);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var canvasView = Assert.IsType<CanvasView>(previewHost.Content);
            var workbench = Assert.IsType<Canvas>(canvasView.FindName("CanvasWorkbench"));
            var focusCard = Assert.IsType<Border>(canvasView.FindName("CanvasSceneRootCard"));
            var thumb = Assert.IsType<Thumb>(canvasView.FindName("CanvasSceneDragThumb"));

            var thumbCenter = GetCenter(thumb.LayoutSlot);
            var dragPoints = BuildDragPoints(workbench.LayoutSlot, thumbCenter);
            var logPath = GetDiagnosticsLogPath("controls-catalog-canvas-thumb-drag-hotspot");
            var steps = new List<DragStepMetrics>();

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(thumbCenter, pointerMoved: true));
            RunFrame(uiRoot, 16);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(thumbCenter, leftPressed: true));
            RunFrame(uiRoot, 16);

            var focusCardStart = new Vector2(Canvas.GetLeft(focusCard), Canvas.GetTop(focusCard));

            for (var i = 0; i < dragPoints.Count; i++)
            {
                steps.Add(RunDragStep(uiRoot, i, dragPoints[i]));
            }

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragPoints[^1], leftReleased: true));
            RunFrame(uiRoot, 16);

            var focusCardEnd = new Vector2(Canvas.GetLeft(focusCard), Canvas.GetTop(focusCard));
            var summary = Summarize(steps, focusCardEnd - focusCardStart);

            var lines = new List<string>
            {
                "scenario=Controls Catalog Canvas thumb drag hotspot diagnostics",
                $"timestamp_utc={DateTime.UtcNow:O}",
                $"log_path={logPath}",
                "step_1=open Controls Catalog",
                "step_2=select Canvas",
                "step_3=move pointer onto CanvasSceneDragThumb and press to capture it",
                "step_4=perform a deterministic multi-step drag path across the Canvas preview",
                "step_5=compare input, layout, render-scheduling, Canvas panel, Grid, Thumb, and CanvasView method timings",
                $"thumb_center=({thumbCenter.X:0.##},{thumbCenter.Y:0.##})",
                $"drag_points={dragPoints.Count}",
                $"focus_card_delta=({summary.FocusCardDelta.X:0.##},{summary.FocusCardDelta.Y:0.##})",
                string.Empty,
                "summary:",
                $"total_update_ms={summary.TotalUpdateMs:0.###}",
                $"total_input_ms={summary.TotalInputMs:0.###}",
                $"total_layout_ms={summary.TotalLayoutMs:0.###}",
                $"total_animation_ms={summary.TotalAnimationMs:0.###}",
                $"total_render_scheduling_ms={summary.TotalRenderSchedulingMs:0.###}",
                $"total_visual_update_ms={summary.TotalVisualUpdateMs:0.###}",
                $"total_thumb_handle_pointer_move_ms={summary.TotalThumbHandlePointerMoveMs:0.###}",
                $"total_thumb_raise_dragdelta_ms={summary.TotalThumbRaiseDragDeltaMs:0.###}",
                $"total_canvas_measure_ms={summary.TotalCanvasMeasureMs:0.###}",
                $"total_canvas_arrange_ms={summary.TotalCanvasArrangeMs:0.###}",
                $"total_grid_measure_ms={summary.TotalGridMeasureMs:0.###}",
                $"total_grid_arrange_ms={summary.TotalGridArrangeMs:0.###}",
                $"total_panel_measure_ms={summary.TotalPanelMeasureMs:0.###}",
                $"total_panel_arrange_ms={summary.TotalPanelArrangeMs:0.###}",
                $"total_canvasview_dragdelta_ms={summary.TotalCanvasViewHandleDragDeltaMs:0.###}",
                $"total_canvasview_movefocus_ms={summary.TotalCanvasViewMoveFocusByMs:0.###}",
                $"total_canvasview_applyscenestate_ms={summary.TotalCanvasViewApplySceneStateMs:0.###}",
                $"total_canvasview_update_live_text_ms={summary.TotalCanvasViewUpdateLiveTextMs:0.###}",
                $"total_canvasview_update_telemetry_ms={summary.TotalCanvasViewUpdateTelemetryMs:0.###}",
                $"total_canvasview_sync_overlay_layout_ms={summary.TotalCanvasViewSyncOverlayLayoutMs:0.###}",
                $"total_canvasview_settext_changes={summary.TotalCanvasViewSetTextChanges}",
                $"total_canvasview_settext_ms={summary.TotalCanvasViewSetTextMs:0.###}",
                $"hottest_canvasview_settext_targets={summary.HottestCanvasViewSetTextTargets}",
                $"total_canvasview_setcanvasleft_changes={summary.TotalCanvasViewSetCanvasLeftChanges}",
                $"total_canvasview_setcanvastop_changes={summary.TotalCanvasViewSetCanvasTopChanges}",
                $"total_measure_invalidations={summary.TotalMeasureInvalidations}",
                $"total_arrange_invalidations={summary.TotalArrangeInvalidations}",
                $"total_render_invalidations={summary.TotalRenderInvalidations}",
                $"total_dirty_roots={summary.TotalDirtyRoots}",
                $"total_retained_traversals={summary.TotalRetainedTraversals}",
                $"hottest_step={summary.HottestStepLabel}",
                $"hottest_step_detail={summary.HottestStepDetail}",
                $"inference={BuildInference(summary)}",
                string.Empty,
                "steps:"
            };

            lines.AddRange(steps.Select(FormatStep));

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllLines(logPath, lines);

            Assert.True(File.Exists(logPath));
            Assert.NotEmpty(steps);
            Assert.True(summary.FocusCardDelta.Length() > 0.01f, "Expected drag steps to move the Canvas focus card.");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static DragStepMetrics RunDragStep(UiRoot uiRoot, int stepIndex, Vector2 pointer)
    {
        var beforeVisualTree = uiRoot.GetVisualTreeMetricsSnapshot();
        _ = Panel.GetTelemetryAndReset();
        _ = Canvas.GetTelemetryAndReset();
        _ = Thumb.GetDragTelemetryAndReset();
        _ = CanvasView.GetDiagnosticsAndReset();
        Grid.ResetTimingForTests();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        var pointerMove = uiRoot.GetPointerMoveTelemetrySnapshotForTests();

        RunFrame(uiRoot, 16);

        var performance = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        var render = uiRoot.GetRenderTelemetrySnapshotForTests();
        var renderInvalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
        var afterVisualTree = uiRoot.GetVisualTreeMetricsSnapshot();
        var thumbTelemetry = Thumb.GetDragTelemetryAndReset();
        var canvasTelemetry = Canvas.GetTelemetryAndReset();
        var panelTelemetry = Panel.GetTelemetryAndReset();
        var gridTiming = Grid.GetTimingSnapshotForTests();
        Grid.ResetTimingForTests();
        var canvasViewDiagnostics = CanvasView.GetDiagnosticsAndReset();

        return new DragStepMetrics(
            stepIndex,
            pointer,
            pointerMove,
            uiRoot.LastUpdateMs,
            performance.InputPhaseMilliseconds,
            performance.LayoutPhaseMilliseconds,
            performance.AnimationPhaseMilliseconds,
            performance.RenderSchedulingPhaseMilliseconds,
            performance.VisualUpdateMilliseconds,
            performance.HottestLayoutMeasureElementType,
            performance.HottestLayoutMeasureElementName,
            performance.HottestLayoutMeasureElementMilliseconds,
            performance.HottestLayoutArrangeElementType,
            performance.HottestLayoutArrangeElementName,
            performance.HottestLayoutArrangeElementMilliseconds,
            thumbTelemetry,
            canvasTelemetry,
            panelTelemetry,
            gridTiming,
            canvasViewDiagnostics,
            afterVisualTree.MeasureInvalidationCount - beforeVisualTree.MeasureInvalidationCount,
            afterVisualTree.ArrangeInvalidationCount - beforeVisualTree.ArrangeInvalidationCount,
            afterVisualTree.RenderInvalidationCount - beforeVisualTree.RenderInvalidationCount,
            render.DirtyRootCount,
            render.RetainedTraversalCount,
            renderInvalidation.EffectiveSourceType,
            renderInvalidation.EffectiveSourceName);
    }

    private static IReadOnlyList<Vector2> BuildDragPoints(LayoutRect workbenchRect, Vector2 start)
    {
        var points = new List<Vector2>();
        var left = workbenchRect.X + 72f;
        var right = workbenchRect.X + workbenchRect.Width - 72f;
        var top = workbenchRect.Y + 40f;
        var bottom = workbenchRect.Y + workbenchRect.Height - 40f;

        points.AddRange(Interpolate(start, new Vector2(right, start.Y + 18f), 8));
        points.AddRange(Interpolate(points[^1], new Vector2(right - 48f, bottom - 28f), 8));
        points.AddRange(Interpolate(points[^1], new Vector2(left + 36f, bottom - 12f), 8));
        points.AddRange(Interpolate(points[^1], new Vector2(left, top + 28f), 8));
        points.AddRange(Interpolate(points[^1], new Vector2(start.X + 36f, start.Y - 22f), 8));
        return points;
    }

    private static IEnumerable<Vector2> Interpolate(Vector2 from, Vector2 to, int steps)
    {
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            yield return Vector2.Lerp(from, to, t);
        }
    }

    private static DragRunSummary Summarize(IReadOnlyList<DragStepMetrics> steps, Vector2 focusCardDelta)
    {
        var hottest = steps
            .OrderByDescending(static step => step.LastUpdateMs)
            .ThenByDescending(static step => step.CanvasViewDiagnostics.UpdateTelemetryMilliseconds)
            .ThenByDescending(static step => step.CanvasViewDiagnostics.SyncOverlayLayoutMilliseconds)
            .First();

        return new DragRunSummary(
            steps,
            focusCardDelta,
            steps.Sum(static step => step.LastUpdateMs),
            steps.Sum(static step => step.InputPhaseMs),
            steps.Sum(static step => step.LayoutPhaseMs),
            steps.Sum(static step => step.AnimationPhaseMs),
            steps.Sum(static step => step.RenderSchedulingMs),
            steps.Sum(static step => step.VisualUpdateMs),
            steps.Sum(static step => step.ThumbTelemetry.HandlePointerMoveMilliseconds),
            steps.Sum(static step => step.ThumbTelemetry.RaiseDragDeltaMilliseconds),
            steps.Sum(static step => step.CanvasTelemetry.MeasureMilliseconds),
            steps.Sum(static step => step.CanvasTelemetry.ArrangeMilliseconds),
            steps.Sum(static step => TicksToMilliseconds(step.GridTiming.MeasureOverrideElapsedTicks)),
            steps.Sum(static step => TicksToMilliseconds(step.GridTiming.ArrangeOverrideElapsedTicks)),
            steps.Sum(static step => step.PanelTelemetry.MeasureMilliseconds),
            steps.Sum(static step => step.PanelTelemetry.ArrangeMilliseconds),
            steps.Sum(static step => step.CanvasViewDiagnostics.HandleFocusCardDragDeltaMilliseconds),
            steps.Sum(static step => step.CanvasViewDiagnostics.MoveFocusByMilliseconds),
            steps.Sum(static step => step.CanvasViewDiagnostics.ApplySceneStateMilliseconds),
            steps.Sum(static step => step.CanvasViewDiagnostics.UpdateLiveTextMilliseconds),
            steps.Sum(static step => step.CanvasViewDiagnostics.UpdateTelemetryMilliseconds),
            steps.Sum(static step => step.CanvasViewDiagnostics.SyncOverlayLayoutMilliseconds),
            steps.Sum(static step => step.CanvasViewDiagnostics.SetTextChangeCount),
            steps.Sum(static step => step.CanvasViewDiagnostics.SetTextMilliseconds),
            hottest.CanvasViewDiagnostics.SetTextTargetSummary,
            steps.Sum(static step => step.CanvasViewDiagnostics.SetCanvasLeftChangeCount),
            steps.Sum(static step => step.CanvasViewDiagnostics.SetCanvasTopChangeCount),
            steps.Sum(static step => step.MeasureInvalidations),
            steps.Sum(static step => step.ArrangeInvalidations),
            steps.Sum(static step => step.RenderInvalidations),
            steps.Sum(static step => step.DirtyRootCount),
            steps.Sum(static step => step.RetainedTraversalCount),
            $"step-{hottest.StepIndex:000}",
            $"updateMs={hottest.LastUpdateMs:0.###}, inputMs={hottest.InputPhaseMs:0.###}, layoutMs={hottest.LayoutPhaseMs:0.###}, animationMs={hottest.AnimationPhaseMs:0.###}, renderSchedulingMs={hottest.RenderSchedulingMs:0.###}, visualUpdateMs={hottest.VisualUpdateMs:0.###}, hottestLayoutMeasure={hottest.HottestLayoutMeasureElementType}#{hottest.HottestLayoutMeasureElementName}:{hottest.HottestLayoutMeasureElementMs:0.###}, hottestLayoutArrange={hottest.HottestLayoutArrangeElementType}#{hottest.HottestLayoutArrangeElementName}:{hottest.HottestLayoutArrangeElementMs:0.###}, canvasMeasureMs={hottest.CanvasTelemetry.MeasureMilliseconds:0.###}, canvasArrangeMs={hottest.CanvasTelemetry.ArrangeMilliseconds:0.###}, gridMeasureMs={TicksToMilliseconds(hottest.GridTiming.MeasureOverrideElapsedTicks):0.###}, gridArrangeMs={TicksToMilliseconds(hottest.GridTiming.ArrangeOverrideElapsedTicks):0.###}, updateTelemetryMs={hottest.CanvasViewDiagnostics.UpdateTelemetryMilliseconds:0.###}, syncOverlayLayoutMs={hottest.CanvasViewDiagnostics.SyncOverlayLayoutMilliseconds:0.###}, setTextChanges={hottest.CanvasViewDiagnostics.SetTextChangeCount}, setTextTargets={hottest.CanvasViewDiagnostics.SetTextTargetSummary}, handleThumbMoveMs={hottest.ThumbTelemetry.HandlePointerMoveMilliseconds:0.###}, raiseDragDeltaMs={hottest.ThumbTelemetry.RaiseDragDeltaMilliseconds:0.###}, effectiveRenderSource={hottest.EffectiveRenderInvalidationSourceType}#{hottest.EffectiveRenderInvalidationSourceName}");
    }

    private static string BuildInference(DragRunSummary summary)
    {
        var telemetryMs = summary.TotalCanvasViewUpdateTelemetryMs + summary.TotalCanvasViewUpdateLiveTextMs;
        var overlayMs = summary.TotalCanvasViewSyncOverlayLayoutMs;
        var canvasMs = summary.TotalCanvasMeasureMs + summary.TotalCanvasArrangeMs;
        var gridMs = summary.TotalGridMeasureMs + summary.TotalGridArrangeMs;

        if (summary.TotalLayoutMs >= summary.TotalInputMs &&
            summary.HottestCanvasViewSetTextTargets != "none" &&
            (telemetryMs > summary.TotalThumbHandlePointerMoveMs || summary.TotalCanvasViewSetTextMs > summary.TotalThumbHandlePointerMoveMs * 0.3d))
        {
            return $"The exact hotspot is the drag-triggered TextBlock.Text mutation path inside CanvasView.SetText, reached from HandleFocusCardDragDelta -> MoveFocusBy -> ApplySceneState -> UpdateTelemetry/SyncOverlayLayout. The logs show layout dominating input and the hottest drag step concentrates SetText time in {summary.HottestCanvasViewSetTextTargets}, which forces the surrounding Grid/TextBlocks to remeasure on nearly every drag step.";
        }

        if (summary.TotalLayoutMs >= summary.TotalInputMs &&
            overlayMs > Math.Max(telemetryMs, Math.Max(canvasMs, gridMs)) * 1.2d)
        {
            return "The exact hotspot is CanvasView.SyncOverlayLayout. The logs isolate most drag-step cost to the overlay repositioning path rather than Thumb input dispatch or Canvas panel layout.";
        }

        if (summary.TotalLayoutMs >= summary.TotalInputMs &&
            canvasMs > Math.Max(telemetryMs, overlayMs) * 1.2d)
        {
            return "The exact hotspot is Canvas.MeasureOverride/ArrangeOverride. The logs isolate drag-step cost to repeated Canvas child measurement and arrangement after every attached-position change.";
        }

        if (summary.TotalLayoutMs >= summary.TotalInputMs &&
            gridMs > Math.Max(canvasMs, Math.Max(telemetryMs, overlayMs)) * 1.2d)
        {
            return "The logs isolate the hotspot to full Grid re-layout around the Canvas preview. The most likely driver is drag-triggered telemetry text churn, but this pass still leaves the exact line slightly broad and would need one narrower per-text-block instrumentation loop before a fix.";
        }

        return "This pass proves the drag slowdown is layout-driven, not input-routing-driven, but it still leaves the dominant line too broad. Add one narrower instrumentation loop inside CanvasView telemetry text updates before applying a fix.";
    }

    private static string FormatStep(DragStepMetrics step)
    {
        return
            $"step={step.StepIndex:000} pointer=({step.Pointer.X:0.##},{step.Pointer.Y:0.##}) updateMs={step.LastUpdateMs:0.###} inputMs={step.InputPhaseMs:0.###} layoutMs={step.LayoutPhaseMs:0.###} animationMs={step.AnimationPhaseMs:0.###} renderSchedulingMs={step.RenderSchedulingMs:0.###} visualUpdateMs={step.VisualUpdateMs:0.###} " +
            $"resolvePath={step.PointerMove.PointerResolvePath} hitTests={step.PointerMove.HitTestCount} routedEvents={step.PointerMove.RoutedEventCount} pointerHandlerMs={step.PointerMove.PointerMoveHandlerMilliseconds:0.###} thumbHandlePointerMoveMs={step.ThumbTelemetry.HandlePointerMoveMilliseconds:0.###} thumbRaiseDragDeltaMs={step.ThumbTelemetry.RaiseDragDeltaMilliseconds:0.###} " +
            $"hottestLayoutMeasure={step.HottestLayoutMeasureElementType}#{step.HottestLayoutMeasureElementName}:{step.HottestLayoutMeasureElementMs:0.###} hottestLayoutArrange={step.HottestLayoutArrangeElementType}#{step.HottestLayoutArrangeElementName}:{step.HottestLayoutArrangeElementMs:0.###} " +
            $"canvasMeasureCalls={step.CanvasTelemetry.MeasureCallCount} canvasMeasureMs={step.CanvasTelemetry.MeasureMilliseconds:0.###} canvasArrangeCalls={step.CanvasTelemetry.ArrangeCallCount} canvasArrangeMs={step.CanvasTelemetry.ArrangeMilliseconds:0.###} " +
            $"gridMeasureMs={TicksToMilliseconds(step.GridTiming.MeasureOverrideElapsedTicks):0.###} gridArrangeMs={TicksToMilliseconds(step.GridTiming.ArrangeOverrideElapsedTicks):0.###} panelMeasureMs={step.PanelTelemetry.MeasureMilliseconds:0.###} panelArrangeMs={step.PanelTelemetry.ArrangeMilliseconds:0.###} " +
            $"canvasViewDragDeltaMs={step.CanvasViewDiagnostics.HandleFocusCardDragDeltaMilliseconds:0.###} moveFocusMs={step.CanvasViewDiagnostics.MoveFocusByMilliseconds:0.###} applySceneStateMs={step.CanvasViewDiagnostics.ApplySceneStateMilliseconds:0.###} updateLiveTextMs={step.CanvasViewDiagnostics.UpdateLiveTextMilliseconds:0.###} updateTelemetryMs={step.CanvasViewDiagnostics.UpdateTelemetryMilliseconds:0.###} syncOverlayLayoutMs={step.CanvasViewDiagnostics.SyncOverlayLayoutMilliseconds:0.###} setTextChanges={step.CanvasViewDiagnostics.SetTextChangeCount} setTextMs={step.CanvasViewDiagnostics.SetTextMilliseconds:0.###} setTextTargets={step.CanvasViewDiagnostics.SetTextTargetSummary} setCanvasLeftChanges={step.CanvasViewDiagnostics.SetCanvasLeftChangeCount} setCanvasTopChanges={step.CanvasViewDiagnostics.SetCanvasTopChangeCount} " +
            $"measureInvalidations={step.MeasureInvalidations} arrangeInvalidations={step.ArrangeInvalidations} renderInvalidations={step.RenderInvalidations} dirtyRoots={step.DirtyRootCount} retainedTraversals={step.RetainedTraversalCount} effectiveRenderSource={step.EffectiveRenderInvalidationSourceType}#{step.EffectiveRenderInvalidationSourceName}";
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

    private static void PrimeRetainedRenderStateForDiagnostics(UiRoot uiRoot)
    {
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.CompleteDrawStateForTests();
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

    private readonly record struct DragStepMetrics(
        int StepIndex,
        Vector2 Pointer,
        UiPointerMoveTelemetrySnapshot PointerMove,
        double LastUpdateMs,
        double InputPhaseMs,
        double LayoutPhaseMs,
        double AnimationPhaseMs,
        double RenderSchedulingMs,
        double VisualUpdateMs,
        string HottestLayoutMeasureElementType,
        string HottestLayoutMeasureElementName,
        double HottestLayoutMeasureElementMs,
        string HottestLayoutArrangeElementType,
        string HottestLayoutArrangeElementName,
        double HottestLayoutArrangeElementMs,
        ThumbDragTelemetrySnapshot ThumbTelemetry,
        CanvasTelemetrySnapshot CanvasTelemetry,
        PanelTelemetrySnapshot PanelTelemetry,
        GridTimingSnapshot GridTiming,
        CanvasViewDiagnosticsSnapshot CanvasViewDiagnostics,
        long MeasureInvalidations,
        long ArrangeInvalidations,
        long RenderInvalidations,
        int DirtyRootCount,
        int RetainedTraversalCount,
        string EffectiveRenderInvalidationSourceType,
        string EffectiveRenderInvalidationSourceName);

    private readonly record struct DragRunSummary(
        IReadOnlyList<DragStepMetrics> Steps,
        Vector2 FocusCardDelta,
        double TotalUpdateMs,
        double TotalInputMs,
        double TotalLayoutMs,
        double TotalAnimationMs,
        double TotalRenderSchedulingMs,
        double TotalVisualUpdateMs,
        double TotalThumbHandlePointerMoveMs,
        double TotalThumbRaiseDragDeltaMs,
        double TotalCanvasMeasureMs,
        double TotalCanvasArrangeMs,
        double TotalGridMeasureMs,
        double TotalGridArrangeMs,
        double TotalPanelMeasureMs,
        double TotalPanelArrangeMs,
        double TotalCanvasViewHandleDragDeltaMs,
        double TotalCanvasViewMoveFocusByMs,
        double TotalCanvasViewApplySceneStateMs,
        double TotalCanvasViewUpdateLiveTextMs,
        double TotalCanvasViewUpdateTelemetryMs,
        double TotalCanvasViewSyncOverlayLayoutMs,
        int TotalCanvasViewSetTextChanges,
        double TotalCanvasViewSetTextMs,
        string HottestCanvasViewSetTextTargets,
        int TotalCanvasViewSetCanvasLeftChanges,
        int TotalCanvasViewSetCanvasTopChanges,
        long TotalMeasureInvalidations,
        long TotalArrangeInvalidations,
        long TotalRenderInvalidations,
        int TotalDirtyRoots,
        int TotalRetainedTraversals,
        string HottestStepLabel,
        string HottestStepDetail);
}