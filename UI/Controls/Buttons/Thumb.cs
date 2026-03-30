using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Thumb : Control
{
    private static int _diagHandlePointerMoveCallCount;
    private static long _diagHandlePointerMoveElapsedTicks;
    private static long _diagRaiseDragDeltaElapsedTicks;

    public static readonly RoutedEvent DragStartedEvent =
        new(nameof(DragStarted), RoutingStrategy.Bubble);

    public static readonly RoutedEvent DragDeltaEvent =
        new(nameof(DragDelta), RoutingStrategy.Bubble);

    public static readonly RoutedEvent DragCompletedEvent =
        new(nameof(DragCompleted), RoutingStrategy.Bubble);

    public static readonly DependencyProperty IsDraggingProperty =
        DependencyProperty.Register(
            nameof(IsDragging),
            typeof(bool),
            typeof(Thumb),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(Thumb),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(Thumb),
            new FrameworkPropertyMetadata(new Color(88, 88, 88), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Thumb),
            new FrameworkPropertyMetadata(new Color(164, 164, 164), FrameworkPropertyMetadataOptions.AffectsRender));

    private bool _isDragging;
    private Vector2 _dragStartPosition;
    private Vector2 _lastPointerPosition;

    public Thumb()
    {
        Focusable = false;
    }

    public event System.EventHandler<DragStartedEventArgs> DragStarted
    {
        add => AddHandler(DragStartedEvent, value);
        remove => RemoveHandler(DragStartedEvent, value);
    }

    public event System.EventHandler<DragDeltaEventArgs> DragDelta
    {
        add => AddHandler(DragDeltaEvent, value);
        remove => RemoveHandler(DragDeltaEvent, value);
    }

    public event System.EventHandler<DragCompletedEventArgs> DragCompleted
    {
        add => AddHandler(DragCompletedEvent, value);
        remove => RemoveHandler(DragCompletedEvent, value);
    }

    public bool IsDragging
    {
        get => GetValue<bool>(IsDraggingProperty);
        private set => SetValue(IsDraggingProperty, value);
    }

    public new bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
    }

    public new Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public new Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public void CancelDrag()
    {
        if (!_isDragging)
        {
            return;
        }

        EndDrag(canceled: true, releaseCapture: true);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        desired.X = MathF.Max(desired.X, 10f);
        desired.Y = MathF.Max(desired.Y, 10f);
        return desired;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        var fill = IsDragging
            ? new Color(132, 188, 240)
            : IsMouseOver ? new Color(116, 116, 116) : Background;

        UiDrawing.DrawFilledRect(spriteBatch, slot, fill, Opacity);
        UiDrawing.DrawRectStroke(spriteBatch, slot, 1f, BorderBrush, Opacity);
    }

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        if (CanvasThumbInvestigationLog.ShouldTrace(this, pointerPosition, this))
        {
            CanvasThumbInvestigationLog.Write(
                "Thumb",
                $"HandlePointerDown target={CanvasThumbInvestigationLog.DescribeElement(this)} pointer={CanvasThumbInvestigationLog.DescribePointer(pointerPosition)} hit={HitTest(pointerPosition)} enabled={IsEnabled} dragging={_isDragging}");
        }

        if (!IsEnabled || !HitTest(pointerPosition))
        {
            return false;
        }

        _isDragging = true;
        _dragStartPosition = pointerPosition;
        _lastPointerPosition = pointerPosition;
        IsDragging = true;

        RaiseRoutedEvent(
            DragStartedEvent,
            new DragStartedEventArgs(
                DragStartedEvent,
                pointerPosition.X - LayoutSlot.X,
                pointerPosition.Y - LayoutSlot.Y));

        if (CanvasThumbInvestigationLog.ShouldTrace(this, pointerPosition, this))
        {
            CanvasThumbInvestigationLog.Write(
                "Thumb",
                $"DragStarted target={CanvasThumbInvestigationLog.DescribeElement(this)} pointer={CanvasThumbInvestigationLog.DescribePointer(pointerPosition)} start=({ _dragStartPosition.X:0.##},{ _dragStartPosition.Y:0.##})");
        }

        return true;
    }

    internal bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        if (!_isDragging)
        {
            return false;
        }

        var startTicks = Stopwatch.GetTimestamp();
        var delta = pointerPosition - _lastPointerPosition;
        _lastPointerPosition = pointerPosition;

        var raiseStartTicks = Stopwatch.GetTimestamp();
        RaiseRoutedEvent(
            DragDeltaEvent,
            new DragDeltaEventArgs(
                DragDeltaEvent,
                delta.X,
                delta.Y));

        if (CanvasThumbInvestigationLog.ShouldTrace(this, pointerPosition, this))
        {
            CanvasThumbInvestigationLog.Write(
                "Thumb",
                $"HandlePointerMove target={CanvasThumbInvestigationLog.DescribeElement(this)} pointer={CanvasThumbInvestigationLog.DescribePointer(pointerPosition)} delta=({delta.X:0.##},{delta.Y:0.##}) last=({_lastPointerPosition.X:0.##},{_lastPointerPosition.Y:0.##})");
        }

        _diagRaiseDragDeltaElapsedTicks += Stopwatch.GetTimestamp() - raiseStartTicks;
        _diagHandlePointerMoveCallCount++;
        _diagHandlePointerMoveElapsedTicks += Stopwatch.GetTimestamp() - startTicks;

        return MathF.Abs(delta.X) > 0.001f || MathF.Abs(delta.Y) > 0.001f;
    }

    internal static ThumbDragTelemetrySnapshot GetDragTelemetryAndReset()
    {
        var snapshot = new ThumbDragTelemetrySnapshot(
            _diagHandlePointerMoveCallCount,
            TicksToMilliseconds(_diagHandlePointerMoveElapsedTicks),
            TicksToMilliseconds(_diagRaiseDragDeltaElapsedTicks));
        _diagHandlePointerMoveCallCount = 0;
        _diagHandlePointerMoveElapsedTicks = 0L;
        _diagRaiseDragDeltaElapsedTicks = 0L;
        return snapshot;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    internal bool HandlePointerUpFromInput()
    {
        if (CanvasThumbInvestigationLog.IsEnabled)
        {
            CanvasThumbInvestigationLog.Write(
                "Thumb",
                $"HandlePointerUp target={CanvasThumbInvestigationLog.DescribeElement(this)} dragging={_isDragging} total=({_lastPointerPosition.X - _dragStartPosition.X:0.##},{_lastPointerPosition.Y - _dragStartPosition.Y:0.##})");
        }

        if (!_isDragging)
        {
            return false;
        }

        EndDrag(canceled: false, releaseCapture: true);
        return true;
    }

    internal void SetMouseOverFromInput(bool isMouseOver)
    {
        if (IsMouseOver == isMouseOver)
        {
            return;
        }

        IsMouseOver = isMouseOver;

        if (CanvasThumbInvestigationLog.IsEnabled)
        {
            CanvasThumbInvestigationLog.Write(
                "Thumb",
                $"SetMouseOver target={CanvasThumbInvestigationLog.DescribeElement(this)} isMouseOver={isMouseOver}");
        }
    }

    private void EndDrag(bool canceled, bool releaseCapture)
    {
        var total = _lastPointerPosition - _dragStartPosition;

        _isDragging = false;
        IsDragging = false;

        _ = releaseCapture;

        RaiseRoutedEvent(
            DragCompletedEvent,
            new DragCompletedEventArgs(DragCompletedEvent, total.X, total.Y, canceled));
    }
}

internal readonly record struct ThumbDragTelemetrySnapshot(
    int HandlePointerMoveCallCount,
    double HandlePointerMoveMilliseconds,
    double RaiseDragDeltaMilliseconds);
