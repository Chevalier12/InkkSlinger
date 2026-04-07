using System;
using System.Diagnostics;
using System.Threading;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class GridSplitter : Control, IRenderDirtyBoundsHintProvider
{
    private static long _diagConstructorCallCount;
    private static long _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static long _diagMeasureOverrideColumnsDirectionCount;
    private static long _diagMeasureOverrideRowsDirectionCount;
    private static long _diagRenderCallCount;
    private static long _diagRenderElapsedTicks;
    private static long _diagRenderDraggingFillCount;
    private static long _diagRenderHoverFillCount;
    private static long _diagRenderBackgroundFillCount;
    private static long _diagPointerDownCallCount;
    private static long _diagPointerDownDisabledRejectCount;
    private static long _diagPointerDownHitTestRejectCount;
    private static long _diagPointerDownBeginDragSuccessCount;
    private static long _diagPointerDownBeginDragFailureCount;
    private static long _diagPointerMoveCallCount;
    private static long _diagPointerMoveRejectedNotDraggingCount;
    private static long _diagPointerMoveApplyCount;
    private static long _diagPointerMoveNoOpDeltaCount;
    private static long _diagPointerUpCallCount;
    private static long _diagPointerUpRejectedNotDraggingCount;
    private static long _diagPointerUpSuccessCount;
    private static long _diagSetMouseOverFromInputCallCount;
    private static long _diagSetMouseOverFromInputNoOpCount;
    private static long _diagSetMouseOverFromInputChangedCount;
    private static long _diagKeyDownCallCount;
    private static long _diagKeyDownElapsedTicks;
    private static long _diagKeyDownRejectedDisabledOrMissingGridCount;
    private static long _diagKeyDownRejectedUnsupportedKeyCount;
    private static long _diagKeyDownRejectedTargetResolutionCount;
    private static long _diagKeyDownApplyCount;
    private static long _diagKeyDownColumnsDirectionCount;
    private static long _diagKeyDownRowsDirectionCount;
    private static long _diagBeginDragCallCount;
    private static long _diagBeginDragRejectedMissingGridCount;
    private static long _diagBeginDragRejectedTargetResolutionCount;
    private static long _diagBeginDragSuccessCount;
    private static long _diagBeginDragColumnsDirectionCount;
    private static long _diagBeginDragRowsDirectionCount;
    private static long _diagTryGetKeyboardResizeDeltaCallCount;
    private static long _diagKeyboardDeltaLeftCount;
    private static long _diagKeyboardDeltaRightCount;
    private static long _diagKeyboardDeltaUpCount;
    private static long _diagKeyboardDeltaDownCount;
    private static long _diagKeyboardDeltaUnsupportedCount;
    private static long _diagEndDragCallCount;
    private static long _diagEndDragReleaseCaptureCount;
    private static long _diagEndDragRetainCaptureCount;
    private static long _diagApplyResizeDeltaCallCount;
    private static long _diagApplyResizeDeltaRejectedInactiveStateCount;
    private static long _diagApplyResizeDeltaApplyCount;
    private static long _diagApplyResizeCallCount;
    private static long _diagApplyResizeElapsedTicks;
    private static long _diagApplyResizeColumnsPathCount;
    private static long _diagApplyResizeRowsPathCount;
    private static long _diagApplyResizeRejectedInvalidTargetsCount;
    private static long _diagApplyResizeDeltaClampedCount;
    private static long _diagApplyResizeTotalCorrectionCount;
    private static long _diagApplyResizeProducedChangeCount;
    private static long _diagApplyResizeNoOpCount;
    private static long _diagTryResolveResizeTargetsCallCount;
    private static long _diagTryResolveResizeTargetsColumnsPathCount;
    private static long _diagTryResolveResizeTargetsRowsPathCount;
    private static long _diagTryResolveResizeTargetsRejectedInsufficientDefinitionsCount;
    private static long _diagTryResolveResizeTargetsSuccessCount;
    private static long _diagTryResolveResizeTargetsFailureCount;
    private static long _diagResolveEffectiveResizeDirectionCallCount;
    private static long _diagResolveEffectiveResizeDirectionExplicitCount;
    private static long _diagResolveEffectiveResizeDirectionAutoColumnsCount;
    private static long _diagResolveEffectiveResizeDirectionAutoRowsCount;
    private static long _diagResolveEffectiveResizeDirectionZeroSizeColumnsCount;
    private static long _diagResolveEffectiveResizeDirectionZeroSizeRowsCount;
    private static long _diagResolveDefinitionPairCallCount;
    private static long _diagResolveDefinitionPairHorizontalCallCount;
    private static long _diagResolveDefinitionPairVerticalCallCount;
    private static long _diagResolveDefinitionPairBasedOnAlignmentCount;
    private static long _diagResolveDefinitionPairCurrentAndNextCount;
    private static long _diagResolveDefinitionPairPreviousAndCurrentCount;
    private static long _diagResolveDefinitionPairPreviousAndNextCount;
    private static long _diagResolveDefinitionPairInvalidPairCount;
    private static long _diagResolveDefinitionPairSuccessCount;
    private static long _diagResolveColumnSizeCallCount;
    private static long _diagResolveColumnSizeActualSizeHitCount;
    private static long _diagResolveColumnSizePixelWidthHitCount;
    private static long _diagResolveColumnSizeZeroFallbackCount;
    private static long _diagResolveRowSizeCallCount;
    private static long _diagResolveRowSizeActualSizeHitCount;
    private static long _diagResolveRowSizePixelHeightHitCount;
    private static long _diagResolveRowSizeZeroFallbackCount;
    private static long _diagSnapCallCount;
    private static long _diagSnapIncrementDisabledCount;
    private static long _diagSnapRoundedChangeCount;
    private static long _diagSnapRoundedNoChangeCount;

    public static readonly DependencyProperty ResizeDirectionProperty =
        DependencyProperty.Register(
            nameof(ResizeDirection),
            typeof(GridResizeDirection),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(
                GridResizeDirection.Auto,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ResizeBehaviorProperty =
        DependencyProperty.Register(
            nameof(ResizeBehavior),
            typeof(GridResizeBehavior),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(GridResizeBehavior.BasedOnAlignment));

    public static readonly DependencyProperty KeyboardIncrementProperty =
        DependencyProperty.Register(
            nameof(KeyboardIncrement),
            typeof(float),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(
                10f,
                FrameworkPropertyMetadataOptions.None,
                coerceValueCallback: static (_, value) => value is float step && step > 0f ? step : 1f));

    public static readonly DependencyProperty DragIncrementProperty =
        DependencyProperty.Register(
            nameof(DragIncrement),
            typeof(float),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.None,
                coerceValueCallback: static (_, value) => value is float step && step > 0f ? step : 1f));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(new Color(90, 90, 90), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(new Color(152, 152, 152), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDraggingProperty =
        DependencyProperty.Register(
            nameof(IsDragging),
            typeof(bool),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private Grid? _activeGrid;
    private GridResizeDirection _activeDirection;
    private int _definitionIndexA = -1;
    private int _definitionIndexB = -1;
    private float _startPointer;
    private float _startSizeA;
    private float _startSizeB;
    private float _runtimeLastRequestedDelta;
    private float _runtimeLastSnappedDelta;
    private float _runtimeLastAppliedDelta;
    private bool _hasPendingRenderDirtyBoundsHint;
    private LayoutRect _pendingRenderDirtyBoundsHint;

    private long _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private long _runtimeMeasureOverrideColumnsDirectionCount;
    private long _runtimeMeasureOverrideRowsDirectionCount;
    private long _runtimeRenderCallCount;
    private long _runtimeRenderElapsedTicks;
    private long _runtimeRenderDraggingFillCount;
    private long _runtimeRenderHoverFillCount;
    private long _runtimeRenderBackgroundFillCount;
    private long _runtimePointerDownCallCount;
    private long _runtimePointerDownDisabledRejectCount;
    private long _runtimePointerDownHitTestRejectCount;
    private long _runtimePointerDownBeginDragSuccessCount;
    private long _runtimePointerDownBeginDragFailureCount;
    private long _runtimePointerMoveCallCount;
    private long _runtimePointerMoveRejectedNotDraggingCount;
    private long _runtimePointerMoveApplyCount;
    private long _runtimePointerMoveNoOpDeltaCount;
    private long _runtimePointerUpCallCount;
    private long _runtimePointerUpRejectedNotDraggingCount;
    private long _runtimePointerUpSuccessCount;
    private long _runtimeSetMouseOverFromInputCallCount;
    private long _runtimeSetMouseOverFromInputNoOpCount;
    private long _runtimeSetMouseOverFromInputChangedCount;
    private long _runtimeKeyDownCallCount;
    private long _runtimeKeyDownElapsedTicks;
    private long _runtimeKeyDownRejectedDisabledOrMissingGridCount;
    private long _runtimeKeyDownRejectedUnsupportedKeyCount;
    private long _runtimeKeyDownRejectedTargetResolutionCount;
    private long _runtimeKeyDownApplyCount;
    private long _runtimeKeyDownColumnsDirectionCount;
    private long _runtimeKeyDownRowsDirectionCount;
    private long _runtimeBeginDragCallCount;
    private long _runtimeBeginDragRejectedMissingGridCount;
    private long _runtimeBeginDragRejectedTargetResolutionCount;
    private long _runtimeBeginDragSuccessCount;
    private long _runtimeBeginDragColumnsDirectionCount;
    private long _runtimeBeginDragRowsDirectionCount;
    private long _runtimeEndDragCallCount;
    private long _runtimeApplyResizeDeltaCallCount;
    private long _runtimeApplyResizeDeltaRejectedInactiveStateCount;
    private long _runtimeApplyResizeDeltaApplyCount;
    private long _runtimeApplyResizeCallCount;
    private long _runtimeApplyResizeElapsedTicks;
    private long _runtimeApplyResizeColumnsPathCount;
    private long _runtimeApplyResizeRowsPathCount;
    private long _runtimeApplyResizeRejectedInvalidTargetsCount;
    private long _runtimeApplyResizeDeltaClampedCount;
    private long _runtimeApplyResizeTotalCorrectionCount;
    private long _runtimeApplyResizeProducedChangeCount;
    private long _runtimeApplyResizeNoOpCount;
    private long _runtimeTryResolveResizeTargetsCallCount;
    private long _runtimeTryResolveResizeTargetsRejectedInsufficientDefinitionsCount;
    private long _runtimeTryResolveResizeTargetsSuccessCount;
    private long _runtimeTryResolveResizeTargetsFailureCount;
    private long _runtimeResolveEffectiveResizeDirectionCallCount;
    private long _runtimeResolveEffectiveResizeDirectionExplicitCount;
    private long _runtimeResolveEffectiveResizeDirectionAutoColumnsCount;
    private long _runtimeResolveEffectiveResizeDirectionAutoRowsCount;
    private long _runtimeResolveDefinitionPairCallCount;
    private long _runtimeResolveDefinitionPairInvalidPairCount;
    private long _runtimeResolveDefinitionPairSuccessCount;
    private long _runtimeResolveColumnSizeCallCount;
    private long _runtimeResolveColumnSizeActualSizeHitCount;
    private long _runtimeResolveColumnSizePixelWidthHitCount;
    private long _runtimeResolveColumnSizeZeroFallbackCount;
    private long _runtimeResolveRowSizeCallCount;
    private long _runtimeResolveRowSizeActualSizeHitCount;
    private long _runtimeResolveRowSizePixelHeightHitCount;
    private long _runtimeResolveRowSizeZeroFallbackCount;
    private long _runtimeSnapCallCount;
    private long _runtimeSnapRoundedChangeCount;
    private long _runtimeSnapRoundedNoChangeCount;

    public GridSplitter()
    {
        IncrementAggregate(ref _diagConstructorCallCount);
        Focusable = true;
        AddHandler<KeyRoutedEventArgs>(UIElement.KeyDownEvent, OnKeyDown, handledEventsToo: true);
    }

    public GridResizeDirection ResizeDirection
    {
        get => GetValue<GridResizeDirection>(ResizeDirectionProperty);
        set => SetValue(ResizeDirectionProperty, value);
    }

    public GridResizeBehavior ResizeBehavior
    {
        get => GetValue<GridResizeBehavior>(ResizeBehaviorProperty);
        set => SetValue(ResizeBehaviorProperty, value);
    }

    public float KeyboardIncrement
    {
        get => GetValue<float>(KeyboardIncrementProperty);
        set => SetValue(KeyboardIncrementProperty, value);
    }

    public float DragIncrement
    {
        get => GetValue<float>(DragIncrementProperty);
        set => SetValue(DragIncrementProperty, value);
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

    public new bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
    }

    public bool IsDragging
    {
        get => GetValue<bool>(IsDraggingProperty);
        private set => SetValue(IsDraggingProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeMeasureOverrideCallCount, ref _diagMeasureOverrideCallCount);
        try
        {
            var desired = base.MeasureOverride(availableSize);
            var direction = ResolveEffectiveResizeDirection();
            if (direction == GridResizeDirection.Columns)
            {
                IncrementMetric(ref _runtimeMeasureOverrideColumnsDirectionCount, ref _diagMeasureOverrideColumnsDirectionCount);
                desired.X = MathF.Max(desired.X, 5f);
                desired.Y = MathF.Max(desired.Y, 20f);
            }
            else
            {
                IncrementMetric(ref _runtimeMeasureOverrideRowsDirectionCount, ref _diagMeasureOverrideRowsDirectionCount);
                desired.X = MathF.Max(desired.X, 20f);
                desired.Y = MathF.Max(desired.Y, 5f);
            }

            return desired;
        }
        finally
        {
            AddMetric(ref _runtimeMeasureOverrideElapsedTicks, ref _diagMeasureOverrideElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeRenderCallCount, ref _diagRenderCallCount);
        try
        {
            var slot = LayoutSlot;
            Color fill;
            if (IsDragging)
            {
                IncrementMetric(ref _runtimeRenderDraggingFillCount, ref _diagRenderDraggingFillCount);
                fill = new Color(112, 170, 220);
            }
            else if (IsMouseOver)
            {
                IncrementMetric(ref _runtimeRenderHoverFillCount, ref _diagRenderHoverFillCount);
                fill = new Color(104, 104, 104);
            }
            else
            {
                IncrementMetric(ref _runtimeRenderBackgroundFillCount, ref _diagRenderBackgroundFillCount);
                fill = Background;
            }

            UiDrawing.DrawFilledRect(spriteBatch, slot, fill, Opacity);
            UiDrawing.DrawRectStroke(spriteBatch, slot, 1f, BorderBrush, Opacity);
        }
        finally
        {
            AddMetric(ref _runtimeRenderElapsedTicks, ref _diagRenderElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        IncrementMetric(ref _runtimePointerDownCallCount, ref _diagPointerDownCallCount);
        if (!IsEnabled)
        {
            IncrementMetric(ref _runtimePointerDownDisabledRejectCount, ref _diagPointerDownDisabledRejectCount);
            return false;
        }

        if (!HitTest(pointerPosition))
        {
            IncrementMetric(ref _runtimePointerDownHitTestRejectCount, ref _diagPointerDownHitTestRejectCount);
            return false;
        }

        FocusManager.SetFocus(this);
        var started = BeginDrag(pointerPosition);
        if (started)
        {
            IncrementMetric(ref _runtimePointerDownBeginDragSuccessCount, ref _diagPointerDownBeginDragSuccessCount);
        }
        else
        {
            IncrementMetric(ref _runtimePointerDownBeginDragFailureCount, ref _diagPointerDownBeginDragFailureCount);
        }

        return started;
    }

    internal bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        IncrementMetric(ref _runtimePointerMoveCallCount, ref _diagPointerMoveCallCount);
        if (!IsDragging)
        {
            IncrementMetric(ref _runtimePointerMoveRejectedNotDraggingCount, ref _diagPointerMoveRejectedNotDraggingCount);
            return false;
        }

        var pointer = _activeDirection == GridResizeDirection.Columns ? pointerPosition.X : pointerPosition.Y;
        _runtimeLastRequestedDelta = pointer - _startPointer;
        var delta = Snap(_runtimeLastRequestedDelta, DragIncrement);
        _runtimeLastSnappedDelta = delta;
        var producedChange = ApplyResizeDelta(delta);

        if (!producedChange && MathF.Abs(delta) > 0.001f)
        {
            RebaseDragAnchor(pointer);
        }

        if (MathF.Abs(delta) > 0.001f)
        {
            IncrementMetric(ref _runtimePointerMoveApplyCount, ref _diagPointerMoveApplyCount);
        }
        else
        {
            IncrementMetric(ref _runtimePointerMoveNoOpDeltaCount, ref _diagPointerMoveNoOpDeltaCount);
        }

        return MathF.Abs(delta) > 0.001f;
    }

    internal bool HandlePointerUpFromInput()
    {
        IncrementMetric(ref _runtimePointerUpCallCount, ref _diagPointerUpCallCount);
        if (!IsDragging)
        {
            IncrementMetric(ref _runtimePointerUpRejectedNotDraggingCount, ref _diagPointerUpRejectedNotDraggingCount);
            return false;
        }

        EndDrag(releaseCapture: true);
        IncrementMetric(ref _runtimePointerUpSuccessCount, ref _diagPointerUpSuccessCount);
        return true;
    }

    internal void SetMouseOverFromInput(bool isMouseOver)
    {
        IncrementMetric(ref _runtimeSetMouseOverFromInputCallCount, ref _diagSetMouseOverFromInputCallCount);
        if (IsMouseOver == isMouseOver)
        {
            IncrementMetric(ref _runtimeSetMouseOverFromInputNoOpCount, ref _diagSetMouseOverFromInputNoOpCount);
            return;
        }

        IncrementMetric(ref _runtimeSetMouseOverFromInputChangedCount, ref _diagSetMouseOverFromInputChangedCount);
        PrimeRenderDirtyBoundsHint();
        IsMouseOver = isMouseOver;
    }

    private void OnKeyDown(object? sender, KeyRoutedEventArgs args)
    {
        _ = sender;

        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeKeyDownCallCount, ref _diagKeyDownCallCount);
        try
        {
            if (!IsEnabled || VisualParent is not Grid grid)
            {
                IncrementMetric(ref _runtimeKeyDownRejectedDisabledOrMissingGridCount, ref _diagKeyDownRejectedDisabledOrMissingGridCount);
                return;
            }

            var direction = ResolveEffectiveResizeDirection();
            if (direction == GridResizeDirection.Columns)
            {
                IncrementMetric(ref _runtimeKeyDownColumnsDirectionCount, ref _diagKeyDownColumnsDirectionCount);
            }
            else
            {
                IncrementMetric(ref _runtimeKeyDownRowsDirectionCount, ref _diagKeyDownRowsDirectionCount);
            }

            if (!TryGetKeyboardResizeDelta(direction, args.Key, out var delta))
            {
                IncrementMetric(ref _runtimeKeyDownRejectedUnsupportedKeyCount, ref _diagKeyDownRejectedUnsupportedKeyCount);
                return;
            }

            if (!TryResolveResizeTargets(grid, direction, out var indexA, out var indexB))
            {
                IncrementMetric(ref _runtimeKeyDownRejectedTargetResolutionCount, ref _diagKeyDownRejectedTargetResolutionCount);
                return;
            }

            FocusManager.SetFocus(this);
            _runtimeLastRequestedDelta = delta;
            _runtimeLastSnappedDelta = delta;

            if (direction == GridResizeDirection.Columns)
            {
                _ = ApplyResizeWithTelemetry(
                    grid,
                    direction,
                    indexA,
                    indexB,
                    ResolveColumnSize(grid, indexA),
                    ResolveColumnSize(grid, indexB),
                    delta);
            }
            else
            {
                _ = ApplyResizeWithTelemetry(
                    grid,
                    direction,
                    indexA,
                    indexB,
                    ResolveRowSize(grid, indexA),
                    ResolveRowSize(grid, indexB),
                    delta);
            }

            IncrementMetric(ref _runtimeKeyDownApplyCount, ref _diagKeyDownApplyCount);
            args.Handled = true;
        }
        finally
        {
            AddMetric(ref _runtimeKeyDownElapsedTicks, ref _diagKeyDownElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private bool BeginDrag(Vector2 pointerPosition)
    {
        IncrementMetric(ref _runtimeBeginDragCallCount, ref _diagBeginDragCallCount);
        if (VisualParent is not Grid grid)
        {
            IncrementMetric(ref _runtimeBeginDragRejectedMissingGridCount, ref _diagBeginDragRejectedMissingGridCount);
            return false;
        }

        var direction = ResolveEffectiveResizeDirection();
        if (direction == GridResizeDirection.Columns)
        {
            IncrementMetric(ref _runtimeBeginDragColumnsDirectionCount, ref _diagBeginDragColumnsDirectionCount);
        }
        else
        {
            IncrementMetric(ref _runtimeBeginDragRowsDirectionCount, ref _diagBeginDragRowsDirectionCount);
        }

        if (!TryResolveResizeTargets(grid, direction, out var indexA, out var indexB))
        {
            IncrementMetric(ref _runtimeBeginDragRejectedTargetResolutionCount, ref _diagBeginDragRejectedTargetResolutionCount);
            return false;
        }

        _activeGrid = grid;
        _activeDirection = direction;
        _definitionIndexA = indexA;
        _definitionIndexB = indexB;
        _startPointer = direction == GridResizeDirection.Columns ? pointerPosition.X : pointerPosition.Y;
        _startSizeA = direction == GridResizeDirection.Columns
            ? ResolveColumnSize(grid, indexA)
            : ResolveRowSize(grid, indexA);
        _startSizeB = direction == GridResizeDirection.Columns
            ? ResolveColumnSize(grid, indexB)
            : ResolveRowSize(grid, indexB);

        PrimeRenderDirtyBoundsHint();
        IsDragging = true;
        IncrementMetric(ref _runtimeBeginDragSuccessCount, ref _diagBeginDragSuccessCount);
        return true;
    }

    private bool TryGetKeyboardResizeDelta(GridResizeDirection direction, Keys key, out float delta)
    {
        IncrementAggregate(ref _diagTryGetKeyboardResizeDeltaCallCount);
        var step = KeyboardIncrement > 0f ? KeyboardIncrement : 1f;
        switch (direction)
        {
            case GridResizeDirection.Columns when key == Keys.Left:
                IncrementAggregate(ref _diagKeyboardDeltaLeftCount);
                delta = -step;
                return true;
            case GridResizeDirection.Columns when key == Keys.Right:
                IncrementAggregate(ref _diagKeyboardDeltaRightCount);
                delta = step;
                return true;
            case GridResizeDirection.Rows when key == Keys.Up:
                IncrementAggregate(ref _diagKeyboardDeltaUpCount);
                delta = -step;
                return true;
            case GridResizeDirection.Rows when key == Keys.Down:
                IncrementAggregate(ref _diagKeyboardDeltaDownCount);
                delta = step;
                return true;
            default:
                IncrementAggregate(ref _diagKeyboardDeltaUnsupportedCount);
                delta = 0f;
                return false;
        }
    }

    private void EndDrag(bool releaseCapture)
    {
        IncrementMetric(ref _runtimeEndDragCallCount, ref _diagEndDragCallCount);
        PrimeRenderDirtyBoundsHint();
        IsDragging = false;
        _activeGrid = null;
        _activeDirection = GridResizeDirection.Auto;
        _definitionIndexA = -1;
        _definitionIndexB = -1;

        if (releaseCapture)
        {
            IncrementAggregate(ref _diagEndDragReleaseCaptureCount);
        }
        else
        {
            IncrementAggregate(ref _diagEndDragRetainCaptureCount);
        }
    }

    bool IRenderDirtyBoundsHintProvider.TryConsumeRenderDirtyBoundsHint(out LayoutRect bounds)
    {
        if (!_hasPendingRenderDirtyBoundsHint)
        {
            bounds = default;
            return false;
        }

        bounds = _pendingRenderDirtyBoundsHint;
        _hasPendingRenderDirtyBoundsHint = false;
        return true;
    }

    private bool ApplyResizeDelta(float delta)
    {
        IncrementMetric(ref _runtimeApplyResizeDeltaCallCount, ref _diagApplyResizeDeltaCallCount);
        if (_activeGrid == null || _definitionIndexA < 0 || _definitionIndexB < 0)
        {
            IncrementMetric(ref _runtimeApplyResizeDeltaRejectedInactiveStateCount, ref _diagApplyResizeDeltaRejectedInactiveStateCount);
            return false;
        }

        IncrementMetric(ref _runtimeApplyResizeDeltaApplyCount, ref _diagApplyResizeDeltaApplyCount);
        return ApplyResizeWithTelemetry(
            _activeGrid,
            _activeDirection,
            _definitionIndexA,
            _definitionIndexB,
            _startSizeA,
            _startSizeB,
            delta);
    }

    private void RebaseDragAnchor(float pointer)
    {
        if (_activeGrid == null || _definitionIndexA < 0 || _definitionIndexB < 0)
        {
            return;
        }

        _startPointer = pointer;
        if (_activeDirection == GridResizeDirection.Columns)
        {
            _startSizeA = ResolveColumnSize(_activeGrid, _definitionIndexA);
            _startSizeB = ResolveColumnSize(_activeGrid, _definitionIndexB);
        }
        else
        {
            _startSizeA = ResolveRowSize(_activeGrid, _definitionIndexA);
            _startSizeB = ResolveRowSize(_activeGrid, _definitionIndexB);
        }
    }

    private bool ApplyResizeWithTelemetry(
        Grid grid,
        GridResizeDirection direction,
        int indexA,
        int indexB,
        float startSizeA,
        float startSizeB,
        float delta)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeApplyResizeCallCount, ref _diagApplyResizeCallCount);
        if (direction == GridResizeDirection.Columns)
        {
            IncrementMetric(ref _runtimeApplyResizeColumnsPathCount, ref _diagApplyResizeColumnsPathCount);
        }
        else
        {
            IncrementMetric(ref _runtimeApplyResizeRowsPathCount, ref _diagApplyResizeRowsPathCount);
        }

        try
        {
            var result = ApplyResizeCore(grid, direction, indexA, indexB, startSizeA, startSizeB, delta);
            _runtimeLastAppliedDelta = result.BoundedDelta;

            if (!result.TargetsValid)
            {
                IncrementMetric(ref _runtimeApplyResizeRejectedInvalidTargetsCount, ref _diagApplyResizeRejectedInvalidTargetsCount);
                return false;
            }

            if (result.DeltaClamped)
            {
                IncrementMetric(ref _runtimeApplyResizeDeltaClampedCount, ref _diagApplyResizeDeltaClampedCount);
            }

            if (result.TotalCorrected)
            {
                IncrementMetric(ref _runtimeApplyResizeTotalCorrectionCount, ref _diagApplyResizeTotalCorrectionCount);
            }

            if (result.ProducedChange)
            {
                IncrementMetric(ref _runtimeApplyResizeProducedChangeCount, ref _diagApplyResizeProducedChangeCount);
            }
            else
            {
                IncrementMetric(ref _runtimeApplyResizeNoOpCount, ref _diagApplyResizeNoOpCount);
            }

            return result.ProducedChange;
        }
        finally
        {
            AddMetric(ref _runtimeApplyResizeElapsedTicks, ref _diagApplyResizeElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private static ResizeApplicationResult ApplyResizeCore(
        Grid grid,
        GridResizeDirection direction,
        int indexA,
        int indexB,
        float startSizeA,
        float startSizeB,
        float delta)
    {
        if (direction == GridResizeDirection.Columns)
        {
            if (indexA < 0 || indexA >= grid.ColumnDefinitions.Count ||
                indexB < 0 || indexB >= grid.ColumnDefinitions.Count)
            {
                return ResizeApplicationResult.Invalid();
            }

            var left = grid.ColumnDefinitions[indexA];
            var right = grid.ColumnDefinitions[indexB];
            var minDelta = left.MinWidth - startSizeA;
            var maxDelta = startSizeB - right.MinWidth;
            var bounded = Clamp(delta, minDelta, maxDelta);
            var newA = Clamp(startSizeA + bounded, left.MinWidth, left.MaxWidth);
            var newB = Clamp(startSizeB - bounded, right.MinWidth, right.MaxWidth);
            var total = startSizeA + startSizeB;
            var totalCorrected = false;
            if (newA + newB > 0f && MathF.Abs((newA + newB) - total) > 0.001f)
            {
                totalCorrected = true;
                newB = MathF.Max(right.MinWidth, total - newA);
            }

            left.Width = new GridLength(newA, GridUnitType.Pixel);
            right.Width = new GridLength(newB, GridUnitType.Pixel);
            return new ResizeApplicationResult(
                true,
                bounded,
                MathF.Abs(bounded - delta) > 0.001f,
                totalCorrected,
                MathF.Abs(newA - startSizeA) > 0.001f || MathF.Abs(newB - startSizeB) > 0.001f);
        }

        if (indexA < 0 || indexA >= grid.RowDefinitions.Count ||
            indexB < 0 || indexB >= grid.RowDefinitions.Count)
        {
            return ResizeApplicationResult.Invalid();
        }

        var top = grid.RowDefinitions[indexA];
        var bottom = grid.RowDefinitions[indexB];
        var minDeltaRows = top.MinHeight - startSizeA;
        var maxDeltaRows = startSizeB - bottom.MinHeight;
        var boundedRows = Clamp(delta, minDeltaRows, maxDeltaRows);
        var newTop = Clamp(startSizeA + boundedRows, top.MinHeight, top.MaxHeight);
        var newBottom = Clamp(startSizeB - boundedRows, bottom.MinHeight, bottom.MaxHeight);
        var rowTotal = startSizeA + startSizeB;
        var rowTotalCorrected = false;
        if (newTop + newBottom > 0f && MathF.Abs((newTop + newBottom) - rowTotal) > 0.001f)
        {
            rowTotalCorrected = true;
            newBottom = MathF.Max(bottom.MinHeight, rowTotal - newTop);
        }

        top.Height = new GridLength(newTop, GridUnitType.Pixel);
        bottom.Height = new GridLength(newBottom, GridUnitType.Pixel);
        return new ResizeApplicationResult(
            true,
            boundedRows,
            MathF.Abs(boundedRows - delta) > 0.001f,
            rowTotalCorrected,
            MathF.Abs(newTop - startSizeA) > 0.001f || MathF.Abs(newBottom - startSizeB) > 0.001f);
    }

    private bool TryResolveResizeTargets(Grid grid, GridResizeDirection direction, out int indexA, out int indexB)
    {
        IncrementMetric(ref _runtimeTryResolveResizeTargetsCallCount, ref _diagTryResolveResizeTargetsCallCount);
        indexA = -1;
        indexB = -1;

        if (direction == GridResizeDirection.Columns)
        {
            IncrementAggregate(ref _diagTryResolveResizeTargetsColumnsPathCount);
            if (grid.ColumnDefinitions.Count < 2)
            {
                _runtimeTryResolveResizeTargetsRejectedInsufficientDefinitionsCount++;
                IncrementAggregate(ref _diagTryResolveResizeTargetsRejectedInsufficientDefinitionsCount);
                _runtimeTryResolveResizeTargetsFailureCount++;
                IncrementAggregate(ref _diagTryResolveResizeTargetsFailureCount);
                return false;
            }

            var column = ClampIndex(Grid.GetColumn(this), grid.ColumnDefinitions.Count);
            var resolved = ResolveDefinitionPair(
                column,
                grid.ColumnDefinitions.Count,
                HorizontalAlignment,
                ResizeBehavior,
                out indexA,
                out indexB);
            if (resolved)
            {
                _runtimeTryResolveResizeTargetsSuccessCount++;
                IncrementAggregate(ref _diagTryResolveResizeTargetsSuccessCount);
            }
            else
            {
                _runtimeTryResolveResizeTargetsFailureCount++;
                IncrementAggregate(ref _diagTryResolveResizeTargetsFailureCount);
            }

            return resolved;
        }

        IncrementAggregate(ref _diagTryResolveResizeTargetsRowsPathCount);
        if (grid.RowDefinitions.Count < 2)
        {
            _runtimeTryResolveResizeTargetsRejectedInsufficientDefinitionsCount++;
            IncrementAggregate(ref _diagTryResolveResizeTargetsRejectedInsufficientDefinitionsCount);
            _runtimeTryResolveResizeTargetsFailureCount++;
            IncrementAggregate(ref _diagTryResolveResizeTargetsFailureCount);
            return false;
        }

        var row = ClampIndex(Grid.GetRow(this), grid.RowDefinitions.Count);
        var rowResolved = ResolveDefinitionPair(
            row,
            grid.RowDefinitions.Count,
            VerticalAlignment,
            ResizeBehavior,
            out indexA,
            out indexB);
        if (rowResolved)
        {
            _runtimeTryResolveResizeTargetsSuccessCount++;
            IncrementAggregate(ref _diagTryResolveResizeTargetsSuccessCount);
        }
        else
        {
            _runtimeTryResolveResizeTargetsFailureCount++;
            IncrementAggregate(ref _diagTryResolveResizeTargetsFailureCount);
        }

        return rowResolved;
    }

    private GridResizeDirection ResolveEffectiveResizeDirection()
    {
        IncrementMetric(ref _runtimeResolveEffectiveResizeDirectionCallCount, ref _diagResolveEffectiveResizeDirectionCallCount);
        var direction = ResolveEffectiveResizeDirectionCore(out var explicitDirection, out var zeroSizeFallback);
        if (explicitDirection)
        {
            IncrementMetric(ref _runtimeResolveEffectiveResizeDirectionExplicitCount, ref _diagResolveEffectiveResizeDirectionExplicitCount);
            return direction;
        }

        if (direction == GridResizeDirection.Columns)
        {
            IncrementMetric(ref _runtimeResolveEffectiveResizeDirectionAutoColumnsCount, ref _diagResolveEffectiveResizeDirectionAutoColumnsCount);
            if (zeroSizeFallback)
            {
                IncrementAggregate(ref _diagResolveEffectiveResizeDirectionZeroSizeColumnsCount);
            }
        }
        else
        {
            IncrementMetric(ref _runtimeResolveEffectiveResizeDirectionAutoRowsCount, ref _diagResolveEffectiveResizeDirectionAutoRowsCount);
            if (zeroSizeFallback)
            {
                IncrementAggregate(ref _diagResolveEffectiveResizeDirectionZeroSizeRowsCount);
            }
        }

        return direction;
    }

    private GridResizeDirection ResolveEffectiveResizeDirectionCore(out bool explicitDirection, out bool zeroSizeFallback)
    {
        explicitDirection = ResizeDirection != GridResizeDirection.Auto;
        zeroSizeFallback = false;
        if (explicitDirection)
        {
            return ResizeDirection;
        }

        var width = float.IsNaN(Width) ? MathF.Max(0f, RenderSize.X) : Width;
        var height = float.IsNaN(Height) ? MathF.Max(0f, RenderSize.Y) : Height;
        if (width <= 0f && height <= 0f)
        {
            zeroSizeFallback = true;
            return HorizontalAlignment == HorizontalAlignment.Stretch
                ? GridResizeDirection.Columns
                : GridResizeDirection.Rows;
        }

        return width <= height ? GridResizeDirection.Columns : GridResizeDirection.Rows;
    }

    private bool ResolveDefinitionPair(
        int currentIndex,
        int count,
        HorizontalAlignment alignment,
        GridResizeBehavior behavior,
        out int indexA,
        out int indexB)
    {
        TrackResolveDefinitionPairCall(isVertical: false, behavior);
        if (behavior == GridResizeBehavior.BasedOnAlignment)
        {
            behavior = alignment switch
            {
                HorizontalAlignment.Left => GridResizeBehavior.PreviousAndCurrent,
                HorizontalAlignment.Right => GridResizeBehavior.CurrentAndNext,
                _ => GridResizeBehavior.PreviousAndNext
            };
        }

        TrackResolvedResizeBehavior(behavior);

        indexA = -1;
        indexB = -1;
        switch (behavior)
        {
            case GridResizeBehavior.CurrentAndNext:
                indexA = currentIndex;
                indexB = currentIndex + 1;
                break;
            case GridResizeBehavior.PreviousAndCurrent:
                indexA = currentIndex - 1;
                indexB = currentIndex;
                break;
            case GridResizeBehavior.PreviousAndNext:
                indexA = currentIndex - 1;
                indexB = currentIndex + 1;
                break;
        }

        if (indexA < 0 || indexB < 0 || indexA >= count || indexB >= count || indexA == indexB)
        {
            _runtimeResolveDefinitionPairInvalidPairCount++;
            IncrementAggregate(ref _diagResolveDefinitionPairInvalidPairCount);
            return false;
        }

        _runtimeResolveDefinitionPairSuccessCount++;
        IncrementAggregate(ref _diagResolveDefinitionPairSuccessCount);
        return true;
    }

    private bool ResolveDefinitionPair(
        int currentIndex,
        int count,
        VerticalAlignment alignment,
        GridResizeBehavior behavior,
        out int indexA,
        out int indexB)
    {
        TrackResolveDefinitionPairCall(isVertical: true, behavior);
        if (behavior == GridResizeBehavior.BasedOnAlignment)
        {
            behavior = alignment switch
            {
                VerticalAlignment.Top => GridResizeBehavior.PreviousAndCurrent,
                VerticalAlignment.Bottom => GridResizeBehavior.CurrentAndNext,
                _ => GridResizeBehavior.PreviousAndNext
            };
        }

        TrackResolvedResizeBehavior(behavior);

        indexA = -1;
        indexB = -1;
        switch (behavior)
        {
            case GridResizeBehavior.CurrentAndNext:
                indexA = currentIndex;
                indexB = currentIndex + 1;
                break;
            case GridResizeBehavior.PreviousAndCurrent:
                indexA = currentIndex - 1;
                indexB = currentIndex;
                break;
            case GridResizeBehavior.PreviousAndNext:
                indexA = currentIndex - 1;
                indexB = currentIndex + 1;
                break;
        }

        if (indexA < 0 || indexB < 0 || indexA >= count || indexB >= count || indexA == indexB)
        {
            _runtimeResolveDefinitionPairInvalidPairCount++;
            IncrementAggregate(ref _diagResolveDefinitionPairInvalidPairCount);
            return false;
        }

        _runtimeResolveDefinitionPairSuccessCount++;
        IncrementAggregate(ref _diagResolveDefinitionPairSuccessCount);
        return true;
    }

    private float ResolveColumnSize(Grid grid, int index)
    {
        _runtimeResolveColumnSizeCallCount++;
        IncrementAggregate(ref _diagResolveColumnSizeCallCount);
        var definition = grid.ColumnDefinitions[index];
        if (definition.ActualWidth > 0f)
        {
            _runtimeResolveColumnSizeActualSizeHitCount++;
            IncrementAggregate(ref _diagResolveColumnSizeActualSizeHitCount);
            return definition.ActualWidth;
        }

        if (definition.Width.IsPixel)
        {
            _runtimeResolveColumnSizePixelWidthHitCount++;
            IncrementAggregate(ref _diagResolveColumnSizePixelWidthHitCount);
            return definition.Width.Value;
        }

        _runtimeResolveColumnSizeZeroFallbackCount++;
        IncrementAggregate(ref _diagResolveColumnSizeZeroFallbackCount);
        return 0f;
    }

    private float ResolveRowSize(Grid grid, int index)
    {
        _runtimeResolveRowSizeCallCount++;
        IncrementAggregate(ref _diagResolveRowSizeCallCount);
        var definition = grid.RowDefinitions[index];
        if (definition.ActualHeight > 0f)
        {
            _runtimeResolveRowSizeActualSizeHitCount++;
            IncrementAggregate(ref _diagResolveRowSizeActualSizeHitCount);
            return definition.ActualHeight;
        }

        if (definition.Height.IsPixel)
        {
            _runtimeResolveRowSizePixelHeightHitCount++;
            IncrementAggregate(ref _diagResolveRowSizePixelHeightHitCount);
            return definition.Height.Value;
        }

        _runtimeResolveRowSizeZeroFallbackCount++;
        IncrementAggregate(ref _diagResolveRowSizeZeroFallbackCount);
        return 0f;
    }

    private static int ClampIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        if (index < 0)
        {
            return 0;
        }

        if (index >= count)
        {
            return count - 1;
        }

        return index;
    }

    private float Snap(float delta, float increment)
    {
        _runtimeSnapCallCount++;
        IncrementAggregate(ref _diagSnapCallCount);
        if (increment <= 0f)
        {
            IncrementAggregate(ref _diagSnapIncrementDisabledCount);
            _runtimeSnapRoundedNoChangeCount++;
            IncrementAggregate(ref _diagSnapRoundedNoChangeCount);
            return delta;
        }

        var snapped = MathF.Round(delta / increment) * increment;
        if (MathF.Abs(snapped - delta) > 0.001f)
        {
            _runtimeSnapRoundedChangeCount++;
            IncrementAggregate(ref _diagSnapRoundedChangeCount);
        }
        else
        {
            _runtimeSnapRoundedNoChangeCount++;
            IncrementAggregate(ref _diagSnapRoundedNoChangeCount);
        }

        return snapped;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private void PrimeRenderDirtyBoundsHint()
    {
        if (!TryGetRenderBoundsInRootSpace(out var bounds) || bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        _pendingRenderDirtyBoundsHint = bounds;
        _hasPendingRenderDirtyBoundsHint = true;
    }

    internal GridSplitterRuntimeDiagnosticsSnapshot GetGridSplitterSnapshotForDiagnostics()
    {
        var effectiveDirection = ResolveEffectiveResizeDirectionCore(out _, out _);
        var slot = LayoutSlot;
        return new GridSplitterRuntimeDiagnosticsSnapshot(
            IsEnabled,
            IsMouseOver,
            IsDragging,
            _activeGrid != null,
            VisualParent?.GetType().Name ?? string.Empty,
            ResizeDirection,
            effectiveDirection,
            ResizeBehavior,
            KeyboardIncrement,
            DragIncrement,
            _definitionIndexA,
            _definitionIndexB,
            _startPointer,
            _startSizeA,
            _startSizeB,
            _runtimeLastRequestedDelta,
            _runtimeLastSnappedDelta,
            _runtimeLastAppliedDelta,
            slot.X,
            slot.Y,
            slot.Width,
            slot.Height,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeMeasureOverrideColumnsDirectionCount,
            _runtimeMeasureOverrideRowsDirectionCount,
            _runtimeRenderCallCount,
            TicksToMilliseconds(_runtimeRenderElapsedTicks),
            _runtimeRenderDraggingFillCount,
            _runtimeRenderHoverFillCount,
            _runtimeRenderBackgroundFillCount,
            _runtimePointerDownCallCount,
            _runtimePointerDownDisabledRejectCount,
            _runtimePointerDownHitTestRejectCount,
            _runtimePointerDownBeginDragSuccessCount,
            _runtimePointerDownBeginDragFailureCount,
            _runtimePointerMoveCallCount,
            _runtimePointerMoveRejectedNotDraggingCount,
            _runtimePointerMoveApplyCount,
            _runtimePointerMoveNoOpDeltaCount,
            _runtimePointerUpCallCount,
            _runtimePointerUpRejectedNotDraggingCount,
            _runtimePointerUpSuccessCount,
            _runtimeSetMouseOverFromInputCallCount,
            _runtimeSetMouseOverFromInputNoOpCount,
            _runtimeSetMouseOverFromInputChangedCount,
            _runtimeKeyDownCallCount,
            TicksToMilliseconds(_runtimeKeyDownElapsedTicks),
            _runtimeKeyDownRejectedDisabledOrMissingGridCount,
            _runtimeKeyDownRejectedUnsupportedKeyCount,
            _runtimeKeyDownRejectedTargetResolutionCount,
            _runtimeKeyDownApplyCount,
            _runtimeKeyDownColumnsDirectionCount,
            _runtimeKeyDownRowsDirectionCount,
            _runtimeBeginDragCallCount,
            _runtimeBeginDragRejectedMissingGridCount,
            _runtimeBeginDragRejectedTargetResolutionCount,
            _runtimeBeginDragSuccessCount,
            _runtimeBeginDragColumnsDirectionCount,
            _runtimeBeginDragRowsDirectionCount,
            _runtimeEndDragCallCount,
            _runtimeApplyResizeDeltaCallCount,
            _runtimeApplyResizeDeltaRejectedInactiveStateCount,
            _runtimeApplyResizeDeltaApplyCount,
            _runtimeApplyResizeCallCount,
            TicksToMilliseconds(_runtimeApplyResizeElapsedTicks),
            _runtimeApplyResizeColumnsPathCount,
            _runtimeApplyResizeRowsPathCount,
            _runtimeApplyResizeRejectedInvalidTargetsCount,
            _runtimeApplyResizeDeltaClampedCount,
            _runtimeApplyResizeTotalCorrectionCount,
            _runtimeApplyResizeProducedChangeCount,
            _runtimeApplyResizeNoOpCount,
            _runtimeTryResolveResizeTargetsCallCount,
            _runtimeTryResolveResizeTargetsRejectedInsufficientDefinitionsCount,
            _runtimeTryResolveResizeTargetsSuccessCount,
            _runtimeTryResolveResizeTargetsFailureCount,
            _runtimeResolveEffectiveResizeDirectionCallCount,
            _runtimeResolveEffectiveResizeDirectionExplicitCount,
            _runtimeResolveEffectiveResizeDirectionAutoColumnsCount,
            _runtimeResolveEffectiveResizeDirectionAutoRowsCount,
            _runtimeResolveDefinitionPairCallCount,
            _runtimeResolveDefinitionPairInvalidPairCount,
            _runtimeResolveDefinitionPairSuccessCount,
            _runtimeResolveColumnSizeCallCount,
            _runtimeResolveColumnSizeActualSizeHitCount,
            _runtimeResolveColumnSizePixelWidthHitCount,
            _runtimeResolveColumnSizeZeroFallbackCount,
            _runtimeResolveRowSizeCallCount,
            _runtimeResolveRowSizeActualSizeHitCount,
            _runtimeResolveRowSizePixelHeightHitCount,
            _runtimeResolveRowSizeZeroFallbackCount,
            _runtimeSnapCallCount,
            _runtimeSnapRoundedChangeCount,
            _runtimeSnapRoundedNoChangeCount);
    }

    internal new static GridSplitterTelemetrySnapshot GetTelemetryAndReset()
    {
        var snapshot = CreateAggregateTelemetrySnapshot();
        ResetAggregate(ref _diagConstructorCallCount);
        ResetAggregate(ref _diagMeasureOverrideCallCount);
        ResetAggregate(ref _diagMeasureOverrideElapsedTicks);
        ResetAggregate(ref _diagMeasureOverrideColumnsDirectionCount);
        ResetAggregate(ref _diagMeasureOverrideRowsDirectionCount);
        ResetAggregate(ref _diagRenderCallCount);
        ResetAggregate(ref _diagRenderElapsedTicks);
        ResetAggregate(ref _diagRenderDraggingFillCount);
        ResetAggregate(ref _diagRenderHoverFillCount);
        ResetAggregate(ref _diagRenderBackgroundFillCount);
        ResetAggregate(ref _diagPointerDownCallCount);
        ResetAggregate(ref _diagPointerDownDisabledRejectCount);
        ResetAggregate(ref _diagPointerDownHitTestRejectCount);
        ResetAggregate(ref _diagPointerDownBeginDragSuccessCount);
        ResetAggregate(ref _diagPointerDownBeginDragFailureCount);
        ResetAggregate(ref _diagPointerMoveCallCount);
        ResetAggregate(ref _diagPointerMoveRejectedNotDraggingCount);
        ResetAggregate(ref _diagPointerMoveApplyCount);
        ResetAggregate(ref _diagPointerMoveNoOpDeltaCount);
        ResetAggregate(ref _diagPointerUpCallCount);
        ResetAggregate(ref _diagPointerUpRejectedNotDraggingCount);
        ResetAggregate(ref _diagPointerUpSuccessCount);
        ResetAggregate(ref _diagSetMouseOverFromInputCallCount);
        ResetAggregate(ref _diagSetMouseOverFromInputNoOpCount);
        ResetAggregate(ref _diagSetMouseOverFromInputChangedCount);
        ResetAggregate(ref _diagKeyDownCallCount);
        ResetAggregate(ref _diagKeyDownElapsedTicks);
        ResetAggregate(ref _diagKeyDownRejectedDisabledOrMissingGridCount);
        ResetAggregate(ref _diagKeyDownRejectedUnsupportedKeyCount);
        ResetAggregate(ref _diagKeyDownRejectedTargetResolutionCount);
        ResetAggregate(ref _diagKeyDownApplyCount);
        ResetAggregate(ref _diagKeyDownColumnsDirectionCount);
        ResetAggregate(ref _diagKeyDownRowsDirectionCount);
        ResetAggregate(ref _diagBeginDragCallCount);
        ResetAggregate(ref _diagBeginDragRejectedMissingGridCount);
        ResetAggregate(ref _diagBeginDragRejectedTargetResolutionCount);
        ResetAggregate(ref _diagBeginDragSuccessCount);
        ResetAggregate(ref _diagBeginDragColumnsDirectionCount);
        ResetAggregate(ref _diagBeginDragRowsDirectionCount);
        ResetAggregate(ref _diagTryGetKeyboardResizeDeltaCallCount);
        ResetAggregate(ref _diagKeyboardDeltaLeftCount);
        ResetAggregate(ref _diagKeyboardDeltaRightCount);
        ResetAggregate(ref _diagKeyboardDeltaUpCount);
        ResetAggregate(ref _diagKeyboardDeltaDownCount);
        ResetAggregate(ref _diagKeyboardDeltaUnsupportedCount);
        ResetAggregate(ref _diagEndDragCallCount);
        ResetAggregate(ref _diagEndDragReleaseCaptureCount);
        ResetAggregate(ref _diagEndDragRetainCaptureCount);
        ResetAggregate(ref _diagApplyResizeDeltaCallCount);
        ResetAggregate(ref _diagApplyResizeDeltaRejectedInactiveStateCount);
        ResetAggregate(ref _diagApplyResizeDeltaApplyCount);
        ResetAggregate(ref _diagApplyResizeCallCount);
        ResetAggregate(ref _diagApplyResizeElapsedTicks);
        ResetAggregate(ref _diagApplyResizeColumnsPathCount);
        ResetAggregate(ref _diagApplyResizeRowsPathCount);
        ResetAggregate(ref _diagApplyResizeRejectedInvalidTargetsCount);
        ResetAggregate(ref _diagApplyResizeDeltaClampedCount);
        ResetAggregate(ref _diagApplyResizeTotalCorrectionCount);
        ResetAggregate(ref _diagApplyResizeProducedChangeCount);
        ResetAggregate(ref _diagApplyResizeNoOpCount);
        ResetAggregate(ref _diagTryResolveResizeTargetsCallCount);
        ResetAggregate(ref _diagTryResolveResizeTargetsColumnsPathCount);
        ResetAggregate(ref _diagTryResolveResizeTargetsRowsPathCount);
        ResetAggregate(ref _diagTryResolveResizeTargetsRejectedInsufficientDefinitionsCount);
        ResetAggregate(ref _diagTryResolveResizeTargetsSuccessCount);
        ResetAggregate(ref _diagTryResolveResizeTargetsFailureCount);
        ResetAggregate(ref _diagResolveEffectiveResizeDirectionCallCount);
        ResetAggregate(ref _diagResolveEffectiveResizeDirectionExplicitCount);
        ResetAggregate(ref _diagResolveEffectiveResizeDirectionAutoColumnsCount);
        ResetAggregate(ref _diagResolveEffectiveResizeDirectionAutoRowsCount);
        ResetAggregate(ref _diagResolveEffectiveResizeDirectionZeroSizeColumnsCount);
        ResetAggregate(ref _diagResolveEffectiveResizeDirectionZeroSizeRowsCount);
        ResetAggregate(ref _diagResolveDefinitionPairCallCount);
        ResetAggregate(ref _diagResolveDefinitionPairHorizontalCallCount);
        ResetAggregate(ref _diagResolveDefinitionPairVerticalCallCount);
        ResetAggregate(ref _diagResolveDefinitionPairBasedOnAlignmentCount);
        ResetAggregate(ref _diagResolveDefinitionPairCurrentAndNextCount);
        ResetAggregate(ref _diagResolveDefinitionPairPreviousAndCurrentCount);
        ResetAggregate(ref _diagResolveDefinitionPairPreviousAndNextCount);
        ResetAggregate(ref _diagResolveDefinitionPairInvalidPairCount);
        ResetAggregate(ref _diagResolveDefinitionPairSuccessCount);
        ResetAggregate(ref _diagResolveColumnSizeCallCount);
        ResetAggregate(ref _diagResolveColumnSizeActualSizeHitCount);
        ResetAggregate(ref _diagResolveColumnSizePixelWidthHitCount);
        ResetAggregate(ref _diagResolveColumnSizeZeroFallbackCount);
        ResetAggregate(ref _diagResolveRowSizeCallCount);
        ResetAggregate(ref _diagResolveRowSizeActualSizeHitCount);
        ResetAggregate(ref _diagResolveRowSizePixelHeightHitCount);
        ResetAggregate(ref _diagResolveRowSizeZeroFallbackCount);
        ResetAggregate(ref _diagSnapCallCount);
        ResetAggregate(ref _diagSnapIncrementDisabledCount);
        ResetAggregate(ref _diagSnapRoundedChangeCount);
        ResetAggregate(ref _diagSnapRoundedNoChangeCount);
        return snapshot;
    }

    internal new static GridSplitterTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot();
    }

    internal static GridSplitterTelemetrySnapshot GetTelemetrySnapshotForDiagnostics()
    {
        return GetAggregateTelemetrySnapshotForDiagnostics();
    }

    private static GridSplitterTelemetrySnapshot CreateAggregateTelemetrySnapshot()
    {
        return new GridSplitterTelemetrySnapshot(
            ReadAggregate(ref _diagConstructorCallCount),
            ReadAggregate(ref _diagMeasureOverrideCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagMeasureOverrideElapsedTicks)),
            ReadAggregate(ref _diagMeasureOverrideColumnsDirectionCount),
            ReadAggregate(ref _diagMeasureOverrideRowsDirectionCount),
            ReadAggregate(ref _diagRenderCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagRenderElapsedTicks)),
            ReadAggregate(ref _diagRenderDraggingFillCount),
            ReadAggregate(ref _diagRenderHoverFillCount),
            ReadAggregate(ref _diagRenderBackgroundFillCount),
            ReadAggregate(ref _diagPointerDownCallCount),
            ReadAggregate(ref _diagPointerDownDisabledRejectCount),
            ReadAggregate(ref _diagPointerDownHitTestRejectCount),
            ReadAggregate(ref _diagPointerDownBeginDragSuccessCount),
            ReadAggregate(ref _diagPointerDownBeginDragFailureCount),
            ReadAggregate(ref _diagPointerMoveCallCount),
            ReadAggregate(ref _diagPointerMoveRejectedNotDraggingCount),
            ReadAggregate(ref _diagPointerMoveApplyCount),
            ReadAggregate(ref _diagPointerMoveNoOpDeltaCount),
            ReadAggregate(ref _diagPointerUpCallCount),
            ReadAggregate(ref _diagPointerUpRejectedNotDraggingCount),
            ReadAggregate(ref _diagPointerUpSuccessCount),
            ReadAggregate(ref _diagSetMouseOverFromInputCallCount),
            ReadAggregate(ref _diagSetMouseOverFromInputNoOpCount),
            ReadAggregate(ref _diagSetMouseOverFromInputChangedCount),
            ReadAggregate(ref _diagKeyDownCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagKeyDownElapsedTicks)),
            ReadAggregate(ref _diagKeyDownRejectedDisabledOrMissingGridCount),
            ReadAggregate(ref _diagKeyDownRejectedUnsupportedKeyCount),
            ReadAggregate(ref _diagKeyDownRejectedTargetResolutionCount),
            ReadAggregate(ref _diagKeyDownApplyCount),
            ReadAggregate(ref _diagKeyDownColumnsDirectionCount),
            ReadAggregate(ref _diagKeyDownRowsDirectionCount),
            ReadAggregate(ref _diagBeginDragCallCount),
            ReadAggregate(ref _diagBeginDragRejectedMissingGridCount),
            ReadAggregate(ref _diagBeginDragRejectedTargetResolutionCount),
            ReadAggregate(ref _diagBeginDragSuccessCount),
            ReadAggregate(ref _diagBeginDragColumnsDirectionCount),
            ReadAggregate(ref _diagBeginDragRowsDirectionCount),
            ReadAggregate(ref _diagTryGetKeyboardResizeDeltaCallCount),
            ReadAggregate(ref _diagKeyboardDeltaLeftCount),
            ReadAggregate(ref _diagKeyboardDeltaRightCount),
            ReadAggregate(ref _diagKeyboardDeltaUpCount),
            ReadAggregate(ref _diagKeyboardDeltaDownCount),
            ReadAggregate(ref _diagKeyboardDeltaUnsupportedCount),
            ReadAggregate(ref _diagEndDragCallCount),
            ReadAggregate(ref _diagEndDragReleaseCaptureCount),
            ReadAggregate(ref _diagEndDragRetainCaptureCount),
            ReadAggregate(ref _diagApplyResizeDeltaCallCount),
            ReadAggregate(ref _diagApplyResizeDeltaRejectedInactiveStateCount),
            ReadAggregate(ref _diagApplyResizeDeltaApplyCount),
            ReadAggregate(ref _diagApplyResizeCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagApplyResizeElapsedTicks)),
            ReadAggregate(ref _diagApplyResizeColumnsPathCount),
            ReadAggregate(ref _diagApplyResizeRowsPathCount),
            ReadAggregate(ref _diagApplyResizeRejectedInvalidTargetsCount),
            ReadAggregate(ref _diagApplyResizeDeltaClampedCount),
            ReadAggregate(ref _diagApplyResizeTotalCorrectionCount),
            ReadAggregate(ref _diagApplyResizeProducedChangeCount),
            ReadAggregate(ref _diagApplyResizeNoOpCount),
            ReadAggregate(ref _diagTryResolveResizeTargetsCallCount),
            ReadAggregate(ref _diagTryResolveResizeTargetsColumnsPathCount),
            ReadAggregate(ref _diagTryResolveResizeTargetsRowsPathCount),
            ReadAggregate(ref _diagTryResolveResizeTargetsRejectedInsufficientDefinitionsCount),
            ReadAggregate(ref _diagTryResolveResizeTargetsSuccessCount),
            ReadAggregate(ref _diagTryResolveResizeTargetsFailureCount),
            ReadAggregate(ref _diagResolveEffectiveResizeDirectionCallCount),
            ReadAggregate(ref _diagResolveEffectiveResizeDirectionExplicitCount),
            ReadAggregate(ref _diagResolveEffectiveResizeDirectionAutoColumnsCount),
            ReadAggregate(ref _diagResolveEffectiveResizeDirectionAutoRowsCount),
            ReadAggregate(ref _diagResolveEffectiveResizeDirectionZeroSizeColumnsCount),
            ReadAggregate(ref _diagResolveEffectiveResizeDirectionZeroSizeRowsCount),
            ReadAggregate(ref _diagResolveDefinitionPairCallCount),
            ReadAggregate(ref _diagResolveDefinitionPairHorizontalCallCount),
            ReadAggregate(ref _diagResolveDefinitionPairVerticalCallCount),
            ReadAggregate(ref _diagResolveDefinitionPairBasedOnAlignmentCount),
            ReadAggregate(ref _diagResolveDefinitionPairCurrentAndNextCount),
            ReadAggregate(ref _diagResolveDefinitionPairPreviousAndCurrentCount),
            ReadAggregate(ref _diagResolveDefinitionPairPreviousAndNextCount),
            ReadAggregate(ref _diagResolveDefinitionPairInvalidPairCount),
            ReadAggregate(ref _diagResolveDefinitionPairSuccessCount),
            ReadAggregate(ref _diagResolveColumnSizeCallCount),
            ReadAggregate(ref _diagResolveColumnSizeActualSizeHitCount),
            ReadAggregate(ref _diagResolveColumnSizePixelWidthHitCount),
            ReadAggregate(ref _diagResolveColumnSizeZeroFallbackCount),
            ReadAggregate(ref _diagResolveRowSizeCallCount),
            ReadAggregate(ref _diagResolveRowSizeActualSizeHitCount),
            ReadAggregate(ref _diagResolveRowSizePixelHeightHitCount),
            ReadAggregate(ref _diagResolveRowSizeZeroFallbackCount),
            ReadAggregate(ref _diagSnapCallCount),
            ReadAggregate(ref _diagSnapIncrementDisabledCount),
            ReadAggregate(ref _diagSnapRoundedChangeCount),
            ReadAggregate(ref _diagSnapRoundedNoChangeCount));
    }

    private void TrackResolveDefinitionPairCall(bool isVertical, GridResizeBehavior behavior)
    {
        _runtimeResolveDefinitionPairCallCount++;
        IncrementAggregate(ref _diagResolveDefinitionPairCallCount);
        if (isVertical)
        {
            IncrementAggregate(ref _diagResolveDefinitionPairVerticalCallCount);
        }
        else
        {
            IncrementAggregate(ref _diagResolveDefinitionPairHorizontalCallCount);
        }

        if (behavior == GridResizeBehavior.BasedOnAlignment)
        {
            IncrementAggregate(ref _diagResolveDefinitionPairBasedOnAlignmentCount);
        }
    }

    private static void TrackResolvedResizeBehavior(GridResizeBehavior behavior)
    {
        switch (behavior)
        {
            case GridResizeBehavior.CurrentAndNext:
                IncrementAggregate(ref _diagResolveDefinitionPairCurrentAndNextCount);
                break;
            case GridResizeBehavior.PreviousAndCurrent:
                IncrementAggregate(ref _diagResolveDefinitionPairPreviousAndCurrentCount);
                break;
            case GridResizeBehavior.PreviousAndNext:
                IncrementAggregate(ref _diagResolveDefinitionPairPreviousAndNextCount);
                break;
        }
    }

    private void IncrementMetric(ref long runtimeField, ref long aggregateField)
    {
        runtimeField++;
        IncrementAggregate(ref aggregateField);
    }

    private void AddMetric(ref long runtimeField, ref long aggregateField, long value)
    {
        runtimeField += value;
        AddAggregate(ref aggregateField, value);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private static void IncrementAggregate(ref long field)
    {
        Interlocked.Increment(ref field);
    }

    private static void AddAggregate(ref long field, long value)
    {
        Interlocked.Add(ref field, value);
    }

    private static long ReadAggregate(ref long field)
    {
        return Interlocked.Read(ref field);
    }

    private static long ResetAggregate(ref long field)
    {
        return Interlocked.Exchange(ref field, 0);
    }

    private readonly record struct ResizeApplicationResult(
        bool TargetsValid,
        float BoundedDelta,
        bool DeltaClamped,
        bool TotalCorrected,
        bool ProducedChange)
    {
        public static ResizeApplicationResult Invalid()
        {
            return new ResizeApplicationResult(false, 0f, false, false, false);
        }
    }
}
