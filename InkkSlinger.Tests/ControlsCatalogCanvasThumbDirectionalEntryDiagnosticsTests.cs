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

public sealed class ControlsCatalogCanvasThumbDirectionalEntryDiagnosticsTests
{
    private const int ViewportWidth = 1280;
    private const int ViewportHeight = 820;
    private const string DiagnosticsEnvironmentVariable = "INKKSLINGER_CANVAS_THUMB_DIAGNOSTICS";

    [Fact]
    public void CanvasPreview_ThumbDirectionalEntry_WritesDiagnosticsLog()
    {
        var previousDiagnosticsValue = Environment.GetEnvironmentVariable(DiagnosticsEnvironmentVariable);
        Environment.SetEnvironmentVariable(DiagnosticsEnvironmentVariable, "1");
        CanvasThumbInvestigationLog.ResetForTests();

        try
        {
            var results = new List<DirectionalVariantResult>();
            foreach (var variant in CreateVariants())
            {
                results.Add(RunVariant(variant));
            }

            var left = results.Single(static result => string.Equals(result.Name, "left-entry", StringComparison.Ordinal));
            var top = results.Single(static result => string.Equals(result.Name, "top-entry", StringComparison.Ordinal));
            var right = results.Single(static result => string.Equals(result.Name, "right-entry", StringComparison.Ordinal));
            var bottom = results.Single(static result => string.Equals(result.Name, "bottom-entry", StringComparison.Ordinal));

            var tracePath = CanvasThumbInvestigationLog.LogPath;
            Assert.True(File.Exists(tracePath), $"Expected Canvas Thumb trace log at '{tracePath}'.");
            var traceText = File.ReadAllText(tracePath);

            var diagnosticsLogPath = GetDiagnosticsLogPath("controls-catalog-canvas-thumb-directional-entry");
            var lines = new List<string>
            {
                "scenario=Controls Catalog Canvas Thumb directional-entry diagnostics",
                $"timestamp_utc={DateTime.UtcNow:O}",
                $"log_path={diagnosticsLogPath}",
                $"trace_log_path={tracePath}",
                "step_1=open Controls Catalog",
                "step_2=select Canvas",
                "step_3=move into CanvasSceneDragThumb from left, top, right, and bottom using deterministic pointer paths",
                "step_4=press and attempt to drag after each entry path",
                "step_5=compare pointer-resolution paths, hover state, routed-event work, and drag telemetry"
            };

            lines.Add(string.Empty);
            lines.Add("variant_results:");
            foreach (var result in results)
            {
                lines.Add(FormatVariant(result));
            }

            lines.Add(string.Empty);
            lines.Add($"inference={BuildInference(left, top, right, bottom)}");

            Directory.CreateDirectory(Path.GetDirectoryName(diagnosticsLogPath)!);
            File.WriteAllLines(diagnosticsLogPath, lines);

            foreach (var result in results)
            {
                if (result.EntryPointOverlapsBadge)
                {
                    Assert.False(result.ThumbIsMouseOverAfterEntry, $"Expected {result.Name} to stay on the overlapping badge instead of hovering the thumb.");
                    Assert.False(result.ThumbIsDraggingAfterPress, $"Expected {result.Name} press to be blocked by the badge.");
                    Assert.Equal(0, result.DragMoveCallCount);
                    Assert.Equal(0, result.DragDeltaEventCount);
                    Assert.True(MathF.Abs(result.FocusCardDelta.X) <= 0.01f && MathF.Abs(result.FocusCardDelta.Y) <= 0.01f, $"Expected {result.Name} to leave the focus card in place.");
                    continue;
                }

                Assert.True(result.ThumbIsMouseOverAfterEntry, $"Expected {result.Name} to hover the thumb.");
                Assert.True(result.ThumbIsDraggingAfterPress, $"Expected {result.Name} to start dragging the thumb on press.");
                Assert.True(result.DragMoveCallCount > 0, $"Expected {result.Name} to route pointer move into Thumb.HandlePointerMoveFromInput.");
                Assert.True(result.DragDeltaEventCount > 0, $"Expected {result.Name} to raise Thumb.DragDelta.");
                Assert.True(MathF.Abs(result.FocusCardDelta.X) > 0.01f || MathF.Abs(result.FocusCardDelta.Y) > 0.01f, $"Expected {result.Name} to move the focus card.");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(DiagnosticsEnvironmentVariable, previousDiagnosticsValue);
            CanvasThumbInvestigationLog.ResetForTests();
        }
    }

    private static DirectionalVariantResult RunVariant(VariantDefinition variant)
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            AnimationManager.Current.ResetForTests();
            VisualTreeHelper.ResetInstrumentationForTests();
            Thumb.GetDragTelemetryAndReset();

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

            uiRoot.RebuildRenderListForTests();
            uiRoot.CompleteDrawStateForTests();
            uiRoot.ResetDirtyStateForTests();
            uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight));

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var canvasView = Assert.IsType<CanvasView>(previewHost.Content);
            var workbench = Assert.IsType<Canvas>(canvasView.FindName("CanvasWorkbench"));
            var focusCard = Assert.IsType<Border>(canvasView.FindName("CanvasSceneRootCard"));
            var thumb = Assert.IsType<Thumb>(canvasView.FindName("CanvasSceneDragThumb"));
            var badge = Assert.IsType<Border>(canvasView.FindName("CanvasSceneBadge"));

            var traceBeforeVariant = File.Exists(CanvasThumbInvestigationLog.LogPath)
                ? File.ReadAllText(CanvasThumbInvestigationLog.LogPath)
                : string.Empty;
            CanvasThumbInvestigationLog.Write("Variant", $"name={variant.Name}");

            var thumbRect = thumb.LayoutSlot;
            var focusRect = focusCard.LayoutSlot;
            var badgeRect = badge.LayoutSlot;
            var startPoint = variant.GetStartPoint(thumbRect, focusRect, badgeRect);
            var entryPoint = variant.GetEntryPoint(thumbRect, focusRect, badgeRect);
            var focusCardStart = new Vector2(Canvas.GetLeft(focusCard), Canvas.GetTop(focusCard));
            var dragDeltaEventCount = 0;
            thumb.DragDelta += (_, args) =>
            {
                if (MathF.Abs(args.HorizontalChange) > 0.001f || MathF.Abs(args.VerticalChange) > 0.001f)
                {
                    dragDeltaEventCount++;
                }
            };

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(startPoint, pointerMoved: true));
            RunFrame(uiRoot, 64);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(entryPoint, pointerMoved: true));
            var entryPointer = uiRoot.GetPointerMoveTelemetrySnapshotForTests();
            var entryPerformance = uiRoot.GetPerformanceTelemetrySnapshotForTests();
            var hoveredAfterEntry = uiRoot.GetHoveredElementForDiagnostics();
            var thumbIsMouseOverAfterEntry = thumb.IsMouseOver;
            RunFrame(uiRoot, 80);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(entryPoint, pointerMoved: false, leftPressed: true));
            var pressPointer = uiRoot.GetPointerMoveTelemetrySnapshotForTests();
            var thumbIsDraggingAfterPress = thumb.IsDragging;

            var dragPoint = entryPoint + variant.DragDelta;
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragPoint, pointerMoved: true));
            var dragPointer = uiRoot.GetPointerMoveTelemetrySnapshotForTests();
            var dragTelemetry = Thumb.GetDragTelemetryAndReset();
            RunFrame(uiRoot, 96);
            RunFrame(uiRoot, 112);
            var afterDragPerformance = uiRoot.GetPerformanceTelemetrySnapshotForTests();

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragPoint, pointerMoved: false, leftReleased: true));
            RunFrame(uiRoot, 128);

            var focusCardEnd = new Vector2(Canvas.GetLeft(focusCard), Canvas.GetTop(focusCard));
            var traceAfterVariant = File.ReadAllText(CanvasThumbInvestigationLog.LogPath);
            var traceSegment = traceAfterVariant.Substring(traceBeforeVariant.Length);

            return new DirectionalVariantResult(
                variant.Name,
                startPoint,
                entryPoint,
                PointInsideRect(entryPoint, badgeRect),
                variant.DragDelta,
                entryPointer,
                pressPointer,
                dragPointer,
                hoveredAfterEntry is FrameworkElement frameworkElement && !string.IsNullOrWhiteSpace(frameworkElement.Name)
                    ? $"{hoveredAfterEntry.GetType().Name}#{frameworkElement.Name}"
                    : hoveredAfterEntry?.GetType().Name ?? "null",
                thumbIsMouseOverAfterEntry,
                thumbIsDraggingAfterPress,
                dragTelemetry.HandlePointerMoveCallCount,
                dragDeltaEventCount,
                focusCardEnd - focusCardStart,
                entryPerformance.LayoutPhaseMilliseconds,
                entryPerformance.AnimationPhaseMilliseconds,
                entryPerformance.RenderSchedulingPhaseMilliseconds,
                afterDragPerformance.LayoutPhaseMilliseconds,
                afterDragPerformance.AnimationPhaseMilliseconds,
                afterDragPerformance.RenderSchedulingPhaseMilliseconds,
                traceSegment);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static IReadOnlyList<VariantDefinition> CreateVariants()
    {
        return
        [
            new VariantDefinition(
                "left-entry",
                static (thumbRect, _, _) => new Vector2(thumbRect.X - 24f, thumbRect.Y + (thumbRect.Height * 0.5f)),
                static (thumbRect, _, _) => new Vector2(thumbRect.X + 2f, thumbRect.Y + (thumbRect.Height * 0.5f)),
                new Vector2(28f, 18f)),
            new VariantDefinition(
                "top-entry",
                static (thumbRect, _, _) => new Vector2(thumbRect.X + (thumbRect.Width * 0.5f), thumbRect.Y - 18f),
                static (thumbRect, _, _) => new Vector2(thumbRect.X + (thumbRect.Width * 0.5f), thumbRect.Y + 2f),
                new Vector2(28f, 18f)),
            new VariantDefinition(
                "right-entry",
                static (thumbRect, _, badgeRect) => ClampToRect(new Vector2(thumbRect.X + thumbRect.Width + 18f, thumbRect.Y + thumbRect.Height - 4f), badgeRect),
                static (thumbRect, _, _) => new Vector2(thumbRect.X + thumbRect.Width - 10f, thumbRect.Y + thumbRect.Height - 4f),
                new Vector2(28f, 18f)),
            new VariantDefinition(
                "bottom-entry",
                static (thumbRect, _, badgeRect) => ClampToRect(new Vector2(thumbRect.X + thumbRect.Width - 14f, thumbRect.Y + thumbRect.Height + 18f), badgeRect),
                static (thumbRect, _, _) => new Vector2(thumbRect.X + thumbRect.Width - 14f, thumbRect.Y + thumbRect.Height - 2f),
                new Vector2(28f, 18f))
        ];
    }

    private static Vector2 ClampToRect(Vector2 point, LayoutRect rect)
    {
        return new Vector2(
            Math.Clamp(point.X, rect.X + 2f, rect.X + rect.Width - 2f),
            Math.Clamp(point.Y, rect.Y + 2f, rect.Y + rect.Height - 2f));
    }

    private static bool PointInsideRect(Vector2 point, LayoutRect rect)
    {
        return point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }

    private static string BuildInference(
        DirectionalVariantResult left,
        DirectionalVariantResult top,
        DirectionalVariantResult right,
        DirectionalVariantResult bottom)
    {
        _ = top;
        _ = right;
        _ = bottom;

        return
            "Unobstructed entry points should still resolve to the Thumb, but points that land on the overlapping badge should stay blocked by the badge. " +
            $"Left entry resolves move via {left.EntryPointer.PointerResolvePath}, resolves press via {left.PressPointer.PointerResolvePath}, and reaches thumb_mouse_over={left.ThumbIsMouseOverAfterEntry}, drag_move_calls={left.DragMoveCallCount}, and focus_card_delta=({left.FocusCardDelta.X:0.##},{left.FocusCardDelta.Y:0.##}). " +
            $"Right entry overlap={right.EntryPointOverlapsBadge} thumb_mouse_over={right.ThumbIsMouseOverAfterEntry}, and bottom entry overlap={bottom.EntryPointOverlapsBadge} thumb_mouse_over={bottom.ThumbIsMouseOverAfterEntry}.";
    }

    private static string FormatVariant(DirectionalVariantResult result)
    {
        return
            $"variant={result.Name} start=({result.StartPoint.X:0.##},{result.StartPoint.Y:0.##}) entry=({result.EntryPoint.X:0.##},{result.EntryPoint.Y:0.##}) dragDelta=({result.DragDelta.X:0.##},{result.DragDelta.Y:0.##}) " +
            $"entryResolvePath={result.EntryPointer.PointerResolvePath} pressResolvePath={result.PressPointer.PointerResolvePath} dragResolvePath={result.DragPointer.PointerResolvePath} hoveredAfterEntry={result.HoveredAfterEntry} " +
            $"thumbMouseOverAfterEntry={result.ThumbIsMouseOverAfterEntry} thumbIsDraggingAfterPress={result.ThumbIsDraggingAfterPress} dragMoveCalls={result.DragMoveCallCount} dragDeltaEvents={result.DragDeltaEventCount} " +
            $"entryHitTests={result.EntryPointer.HitTestCount} entryRoutedEvents={result.EntryPointer.RoutedEventCount} entryResolveMs={result.EntryPointer.PointerTargetResolveMilliseconds:0.###} entryHoverMs={result.EntryPointer.HoverUpdateMilliseconds:0.###} entryRouteMs={result.EntryPointer.PointerRouteMilliseconds:0.###} entryHoverReuseMs={result.EntryPointer.PointerResolveHoverReuseCheckMilliseconds:0.###} entryFinalHitTestMs={result.EntryPointer.PointerResolveFinalHitTestMilliseconds:0.###} " +
            $"pressHitTests={result.PressPointer.HitTestCount} pressResolveMs={result.PressPointer.PointerTargetResolveMilliseconds:0.###} pressHoverReuseMs={result.PressPointer.PointerResolveHoverReuseCheckMilliseconds:0.###} pressFinalHitTestMs={result.PressPointer.PointerResolveFinalHitTestMilliseconds:0.###} " +
            $"layoutAfterEntryMs={result.LayoutPhaseAfterEntryMs:0.###} animationAfterEntryMs={result.AnimationPhaseAfterEntryMs:0.###} renderSchedulingAfterEntryMs={result.RenderSchedulingAfterEntryMs:0.###} " +
            $"layoutAfterDragMs={result.LayoutPhaseAfterDragMs:0.###} animationAfterDragMs={result.AnimationPhaseAfterDragMs:0.###} renderSchedulingAfterDragMs={result.RenderSchedulingAfterDragMs:0.###} " +
            $"focusCardDelta=({result.FocusCardDelta.X:0.##},{result.FocusCardDelta.Y:0.##})";
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default(KeyboardState), default(MouseState), pointer),
            Current = new InputSnapshot(default(KeyboardState), default(MouseState), pointer),
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

    private readonly record struct VariantDefinition(
        string Name,
        Func<LayoutRect, LayoutRect, LayoutRect, Vector2> GetStartPoint,
        Func<LayoutRect, LayoutRect, LayoutRect, Vector2> GetEntryPoint,
        Vector2 DragDelta);

    private readonly record struct DirectionalVariantResult(
        string Name,
        Vector2 StartPoint,
        Vector2 EntryPoint,
        bool EntryPointOverlapsBadge,
        Vector2 DragDelta,
        UiPointerMoveTelemetrySnapshot EntryPointer,
        UiPointerMoveTelemetrySnapshot PressPointer,
        UiPointerMoveTelemetrySnapshot DragPointer,
        string HoveredAfterEntry,
        bool ThumbIsMouseOverAfterEntry,
        bool ThumbIsDraggingAfterPress,
        int DragMoveCallCount,
        int DragDeltaEventCount,
        Vector2 FocusCardDelta,
        double LayoutPhaseAfterEntryMs,
        double AnimationPhaseAfterEntryMs,
        double RenderSchedulingAfterEntryMs,
        double LayoutPhaseAfterDragMs,
        double AnimationPhaseAfterDragMs,
        double RenderSchedulingAfterDragMs,
        string TraceSegment);
}