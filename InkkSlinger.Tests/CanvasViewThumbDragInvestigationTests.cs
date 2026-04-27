using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class CanvasViewThumbDragInvestigationTests
{
    [Fact]
    public void CanvasView_ThumbDrag_ReattachesThumbOnNextNormalFrameWithoutResize()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new CanvasView();
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 1280, 820, 16);

            var focusCard = Assert.IsType<Border>(view.FindName("CanvasSceneRootCard"));
            var thumb = Assert.IsType<Thumb>(view.FindName("CanvasSceneDragThumb"));

            var initialFocusLeft = Canvas.GetLeft(focusCard);
            var initialFocusTop = Canvas.GetTop(focusCard);
            var initialThumbLeft = Canvas.GetLeft(thumb);
            var initialThumbTop = Canvas.GetTop(thumb);

            AssertThumbAnchoredToFocusCard(focusCard, thumb);

            var dragStart = GetCenter(thumb.LayoutSlot);
            var dragDelta = new Vector2(36f, 24f);
            var dragEnd = dragStart + dragDelta;

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragStart, pointerMoved: true));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragStart, leftPressed: true));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragEnd, pointerMoved: true));

            Assert.True(thumb.IsDragging);

            Assert.Equal(initialFocusLeft + dragDelta.X, Canvas.GetLeft(focusCard), 0.5f);
            Assert.Equal(initialFocusTop + dragDelta.Y, Canvas.GetTop(focusCard), 0.5f);
            Assert.Equal(initialThumbLeft, Canvas.GetLeft(thumb), 0.01f);
            Assert.Equal(initialThumbTop, Canvas.GetTop(thumb), 0.01f);

            RunLayout(uiRoot, 1280, 820, 32);

            AssertThumbAnchoredToFocusCard(focusCard, thumb);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragEnd, leftReleased: true));
            Assert.False(thumb.IsDragging);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void CanvasView_ThumbDragTelemetry_ShouldNotRemeasureWorkbenchForPositionOnlyDrag()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new CanvasView();
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 1280, 820, 16);

            var workbench = Assert.IsType<Canvas>(view.FindName("CanvasWorkbench"));
            var focusCard = Assert.IsType<Border>(view.FindName("CanvasSceneRootCard"));
            var thumb = Assert.IsType<Thumb>(view.FindName("CanvasSceneDragThumb"));

            AssertThumbAnchoredToFocusCard(focusCard, thumb);

            var dragStart = GetCenter(thumb.LayoutSlot);
            var dragEnd = dragStart + new Vector2(36f, 24f);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragStart, pointerMoved: true));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragStart, leftPressed: true));
            ResetCanvasDragTelemetry(uiRoot);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragEnd, pointerMoved: true));
            Assert.True(thumb.IsDragging);

            RunLayout(uiRoot, 1280, 820, 16);

            var canvasTelemetry = Canvas.GetTelemetryAndReset();
            var frameworkTelemetry = FrameworkElement.GetTelemetryAndReset();
            var controlTelemetry = Control.GetTelemetryAndReset();
            var workbenchSnapshot = workbench.GetFrameworkElementSnapshotForDiagnostics();
            var focusSnapshot = focusCard.GetFrameworkElementSnapshotForDiagnostics();
            var thumbSnapshot = thumb.GetFrameworkElementSnapshotForDiagnostics();
            var viewDiagnostics = CanvasView.GetDiagnosticsAndReset();
            var rootTelemetry = uiRoot.GetUiRootTelemetrySnapshot();
            uiRoot.GetTelemetryAndReset();

            Assert.True(
                canvasTelemetry.MeasureCallCount == 0,
                $"Position-only thumb drag should not rerun Canvas.MeasureOverride. " +
                $"canvasMeasureCalls={canvasTelemetry.MeasureCallCount}, canvasMeasuredChildren={canvasTelemetry.MeasuredChildCount}, " +
                $"canvasArrangeCalls={canvasTelemetry.ArrangeCallCount}, canvasArrangedChildren={canvasTelemetry.ArrangedChildCount}, " +
                $"workbenchMeasures={workbenchSnapshot.MeasureCallCount}, workbenchArranges={workbenchSnapshot.ArrangeCallCount}, " +
                $"workbenchMeasureInvalidations={workbenchSnapshot.Invalidation.DirectMeasureInvalidationCount + workbenchSnapshot.Invalidation.PropagatedMeasureInvalidationCount}, " +
                $"workbenchArrangeInvalidations={workbenchSnapshot.Invalidation.DirectArrangeInvalidationCount + workbenchSnapshot.Invalidation.PropagatedArrangeInvalidationCount}, " +
                $"workbenchMeasureSources={workbenchSnapshot.Invalidation.TopMeasureInvalidationSources}, " +
                $"workbenchArrangeSources={workbenchSnapshot.Invalidation.TopArrangeInvalidationSources}, " +
                $"frameworkMeasureCalls={frameworkTelemetry.MeasureCallCount}, frameworkMeasureCachedReuse={frameworkTelemetry.MeasureCachedReuseCount}, " +
                $"frameworkInvalidateMeasureCalls={frameworkTelemetry.InvalidateMeasureCallCount}, frameworkInvalidateArrangeCalls={frameworkTelemetry.InvalidateArrangeCallCount}, " +
                $"controlVisualChildCalls={controlTelemetry.GetVisualChildrenCallCount}, focusDpChanges={focusSnapshot.DependencyPropertyChangedCallCount}, " +
                $"thumbDpChanges={thumbSnapshot.DependencyPropertyChangedCallCount}, viewSetLeftChanges={viewDiagnostics.SetCanvasLeftChangeCount}, " +
                $"viewSetTopChanges={viewDiagnostics.SetCanvasTopChangeCount}, uiRootUpdates={rootTelemetry.UpdateCallCount}.");
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    private static void AssertThumbAnchoredToFocusCard(Border focusCard, Thumb thumb)
    {
        var expectedLeft = Canvas.GetLeft(focusCard) + focusCard.ActualWidth - thumb.ActualWidth - 14f;
        var expectedTop = Canvas.GetTop(focusCard) + 14f;

        Assert.Equal(expectedLeft, Canvas.GetLeft(thumb), 0.5f);
        Assert.Equal(expectedTop, Canvas.GetTop(thumb), 0.5f);
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
            PressedKeys = new List<Microsoft.Xna.Framework.Input.Keys>(),
            ReleasedKeys = new List<Microsoft.Xna.Framework.Input.Keys>(),
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

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private static void ResetCanvasDragTelemetry(UiRoot uiRoot)
    {
        _ = Canvas.GetTelemetryAndReset();
        _ = FrameworkElement.GetTelemetryAndReset();
        _ = Control.GetTelemetryAndReset();
        _ = CanvasView.GetDiagnosticsAndReset();
        uiRoot.GetTelemetryAndReset();
    }

    private static Dictionary<object, object> SnapshotApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
    }

    private static void RestoreApplicationResources(Dictionary<object, object> snapshot)
    {
        TestApplicationResources.Restore(snapshot);
    }

    private static void LoadRootAppResources()
    {
        TestApplicationResources.LoadDemoAppResources();
    }
}