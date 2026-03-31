using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger.Tests;

public sealed class ThinTargetPointerSamplingInvestigationTests
{
    private const int ViewportWidth = 220;
    private const int ViewportHeight = 160;

    [Fact]
    public void ThinThumb_FastMoveAndPressAcrossUnsampledPath_StartsDrag()
    {
        var scenario = RunScenario(pointerXOffset: 0f);

        Assert.Equal("Thumb#ThinThumb", scenario.ManualCrossingHitElement);
        Assert.Equal("Thumb#ThinThumb", scenario.MoveAndPressResolvedElement);
        Assert.Equal("Thumb#ThinThumb", scenario.CapturedElementAfterMoveAndPress);
        Assert.True(scenario.ThumbIsMouseOverAfterMoveAndPress);
        Assert.True(scenario.ThumbIsDraggingAfterMoveAndPress);
        Assert.Equal(1, scenario.EventCounters.DragStartedCount);
        Assert.True(scenario.EventCounters.PreviewMouseDownCount >= 1);
        Assert.True(scenario.EventCounters.MouseDownCount >= 1);
        Assert.True(scenario.EventCounters.DragDeltaCount >= 1);
        Assert.True(scenario.FocusCardDeltaAfterDrag.LengthSquared() > 0.01f);
    }

    [Fact]
    public void ThinThumb_FastMoveAndPressBesideThumb_DoesNotStartDrag()
    {
        var scenario = RunScenario(pointerXOffset: -58f);

        Assert.NotEqual("Thumb#ThinThumb", scenario.ManualCrossingHitElement);
        Assert.NotEqual("Thumb#ThinThumb", scenario.MoveAndPressResolvedElement);
        Assert.NotEqual("Thumb#ThinThumb", scenario.CapturedElementAfterMoveAndPress);
        Assert.False(scenario.ThumbIsMouseOverAfterMoveAndPress);
        Assert.False(scenario.ThumbIsDraggingAfterMoveAndPress);
        Assert.Equal(0, scenario.EventCounters.DragStartedCount);
        Assert.Equal(0, scenario.EventCounters.PreviewMouseDownCount);
        Assert.Equal(0, scenario.EventCounters.MouseDownCount);
        Assert.Equal(0, scenario.EventCounters.DragDeltaCount);
        Assert.True(scenario.FocusCardDeltaAfterDrag.LengthSquared() < 0.01f);
    }

    private static ScenarioResult RunScenario(float pointerXOffset)
    {
        FocusManager.ReleasePointer(FocusManager.GetCapturedPointerElement());

        var host = new Canvas
        {
            Width = ViewportWidth,
            Height = ViewportHeight
        };

        var background = new Border
        {
            Name = "BackgroundProbe",
            Width = ViewportWidth,
            Height = ViewportHeight
        };

        var thumb = new Thumb
        {
            Name = "ThinThumb",
            Width = 84f,
            Height = 10f
        };

        host.AddChild(background);
        host.AddChild(thumb);
        Canvas.SetLeft(background, 0f);
        Canvas.SetTop(background, 0f);
        Canvas.SetLeft(thumb, 68f);
        Canvas.SetTop(thumb, 70f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, ViewportWidth, ViewportHeight);

        var counters = new ThumbEventCounters();
        AttachThumbEventCounters(thumb, counters);

        var thumbCenterX = thumb.LayoutSlot.X + (thumb.LayoutSlot.Width * 0.5f) + pointerXOffset;
        var abovePoint = new Vector2(thumbCenterX, thumb.LayoutSlot.Y - 12f);
        var crossingPoint = new Vector2(thumbCenterX, thumb.LayoutSlot.Y + (thumb.LayoutSlot.Height * 0.5f));
        var belowPoint = new Vector2(thumbCenterX, thumb.LayoutSlot.Y + thumb.LayoutSlot.Height + 12f);
        var dragPoint = crossingPoint + new Vector2(24f, 16f);

        var manualAboveHit = VisualTreeHelper.HitTest(host, abovePoint, out var aboveMetrics);
        var manualCrossingHit = VisualTreeHelper.HitTest(host, crossingPoint, out var crossingMetrics);
        var manualBelowHit = VisualTreeHelper.HitTest(host, belowPoint, out var belowMetrics);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(abovePoint, abovePoint, pointerMoved: true));
        var warmupMoveSnapshot = uiRoot.GetPointerMoveTelemetrySnapshotForTests();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(abovePoint, belowPoint, pointerMoved: true, leftPressed: true));
        var moveAndPressSnapshot = uiRoot.GetPointerMoveTelemetrySnapshotForTests();
        var moveAndPressResolvedElement = DescribeElement(uiRoot.GetHoveredElementForDiagnostics());
        var capturedElementAfterMoveAndPress = DescribeElement(FocusManager.GetCapturedPointerElement());
        var thumbIsDraggingAfterMoveAndPress = thumb.IsDragging;
        var thumbIsMouseOverAfterMoveAndPress = thumb.IsMouseOver;

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(belowPoint, dragPoint, pointerMoved: true));
        var dragSnapshot = uiRoot.GetPointerMoveTelemetrySnapshotForTests();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(dragPoint, dragPoint, leftReleased: true));
        var focusCardDeltaAfterDrag = counters.AccumulatedDragDelta;

        return new ScenarioResult(
            DescribeElement(manualAboveHit),
            FormatHitTestMetrics(aboveMetrics),
            DescribeElement(manualCrossingHit),
            FormatHitTestMetrics(crossingMetrics),
            DescribeElement(manualBelowHit),
            FormatHitTestMetrics(belowMetrics),
            warmupMoveSnapshot,
            moveAndPressSnapshot,
            moveAndPressResolvedElement,
            capturedElementAfterMoveAndPress,
            dragSnapshot,
            thumbIsMouseOverAfterMoveAndPress,
            thumbIsDraggingAfterMoveAndPress,
            counters,
            focusCardDeltaAfterDrag);
    }

    private static void AttachThumbEventCounters(Thumb thumb, ThumbEventCounters counters)
    {
        thumb.AddHandler<MouseRoutedEventArgs>(UIElement.MouseEnterEvent, (_, _) => counters.MouseEnterCount++);
        thumb.AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeaveEvent, (_, _) => counters.MouseLeaveCount++);
        thumb.AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseDownEvent, (_, _) => counters.PreviewMouseDownCount++);
        thumb.AddHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, (_, _) => counters.MouseDownCount++);
        thumb.DragStarted += (_, _) => counters.DragStartedCount++;
        thumb.DragDelta += (_, args) =>
        {
            counters.DragDeltaCount++;
            counters.AccumulatedDragDelta += new Vector2(args.HorizontalChange, args.VerticalChange);
        };
        thumb.DragCompleted += (_, _) => counters.DragCompletedCount++;
    }

    private static InputDelta CreatePointerDelta(
        Vector2 previousPointer,
        Vector2 currentPointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default(KeyboardState), default(MouseState), previousPointer),
            Current = new InputSnapshot(default(KeyboardState), default(MouseState), currentPointer),
            PressedKeys = Array.Empty<Keys>(),
            ReleasedKeys = Array.Empty<Keys>(),
            TextInput = Array.Empty<char>(),
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

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }

    private static string FormatPointerSnapshot(UiPointerMoveTelemetrySnapshot snapshot)
    {
        return
            $"resolvePath={snapshot.PointerResolvePath}; hitTests={snapshot.HitTestCount}; routedEvents={snapshot.RoutedEventCount}; pointerEvents={snapshot.PointerEventCount}; " +
            $"resolveMs={snapshot.PointerTargetResolveMilliseconds:0.###}; hoverMs={snapshot.HoverUpdateMilliseconds:0.###}; routeMs={snapshot.PointerRouteMilliseconds:0.###}; " +
            $"moveDispatchMs={snapshot.PointerMoveDispatchMilliseconds:0.###}; moveEventsMs={snapshot.PointerMoveRoutedEventsMilliseconds:0.###}; moveHandlerMs={snapshot.PointerMoveHandlerMilliseconds:0.###}; " +
            $"hoverReuseCheckMs={snapshot.PointerResolveHoverReuseCheckMilliseconds:0.###}; finalHitTestMs={snapshot.PointerResolveFinalHitTestMilliseconds:0.###}";
    }

    private static string FormatHitTestMetrics(HitTestMetrics metrics)
    {
        return
            $"nodes={metrics.NodesVisited}; depth={metrics.MaxDepth}; ms={metrics.TotalMilliseconds:0.###}; topLevel={metrics.TopLevelSubtreeSummary}; hottestType={metrics.HottestTypeSummary}; hottestNode={metrics.HottestNodeSummary}; traversal={metrics.TraversalSummary}; rejects={metrics.RejectSummary}";
    }

    private static string FormatCounters(ThumbEventCounters counters)
    {
        return
            $"mouseEnter={counters.MouseEnterCount}; mouseLeave={counters.MouseLeaveCount}; previewMouseDown={counters.PreviewMouseDownCount}; mouseDown={counters.MouseDownCount}; dragStarted={counters.DragStartedCount}; dragDelta={counters.DragDeltaCount}; dragCompleted={counters.DragCompletedCount}";
    }

    private static string DescribeElement(UIElement? element)
    {
        if (element == null)
        {
            return "null";
        }

        if (element is FrameworkElement frameworkElement && !string.IsNullOrWhiteSpace(frameworkElement.Name))
        {
            return $"{element.GetType().Name}#{frameworkElement.Name}";
        }

        return element.GetType().Name;
    }

    private sealed class ThumbEventCounters
    {
        public int MouseEnterCount { get; set; }

        public int MouseLeaveCount { get; set; }

        public int PreviewMouseDownCount { get; set; }

        public int MouseDownCount { get; set; }

        public int DragStartedCount { get; set; }

        public int DragDeltaCount { get; set; }

        public int DragCompletedCount { get; set; }

        public Vector2 AccumulatedDragDelta { get; set; }
    }

    private readonly record struct ScenarioResult(
        string ManualAboveHitElement,
        string ManualAboveHitMetrics,
        string ManualCrossingHitElement,
        string ManualCrossingHitMetrics,
        string ManualBelowHitElement,
        string ManualBelowHitMetrics,
        UiPointerMoveTelemetrySnapshot WarmupMoveSnapshot,
        UiPointerMoveTelemetrySnapshot MoveAndPressSnapshot,
        string MoveAndPressResolvedElement,
        string CapturedElementAfterMoveAndPress,
        UiPointerMoveTelemetrySnapshot DragSnapshot,
        bool ThumbIsMouseOverAfterMoveAndPress,
        bool ThumbIsDraggingAfterMoveAndPress,
        ThumbEventCounters EventCounters,
        Vector2 FocusCardDeltaAfterDrag);
}