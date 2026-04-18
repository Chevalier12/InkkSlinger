using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class ScrollViewer : ContentControl
{
    private enum ScrollOffsetUpdateSource
    {
        External,
        HorizontalScrollBar,
        VerticalScrollBar,
    }

    private const float ContentScrollBarGap = 1f;
    public static readonly DependencyProperty UseTransformContentScrollingProperty =
        DependencyProperty.RegisterAttached(
            "UseTransformContentScrolling",
            typeof(bool),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is not UIElement element)
                    {
                        return;
                    }

                    element.InvalidateArrange();
                    element.InvalidateVisual();
                    if (element.VisualParent is ScrollViewer visualViewer)
                    {
                        visualViewer.InvalidateArrange();
                        visualViewer.InvalidateVisual();
                    }
                    else if (element.LogicalParent is ScrollViewer logicalViewer)
                    {
                        logicalViewer.InvalidateArrange();
                        logicalViewer.InvalidateVisual();
                    }
                }));

    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(HorizontalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Disabled, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(VerticalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Auto, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(
            nameof(HorizontalOffset),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(
            nameof(VerticalOffset),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f));

    public static readonly DependencyProperty ExtentWidthProperty =
        DependencyProperty.Register(
            nameof(ExtentWidth),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ExtentHeightProperty =
        DependencyProperty.Register(
            nameof(ExtentHeight),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ViewportWidthProperty =
        DependencyProperty.Register(
            nameof(ViewportWidth),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ViewportHeightProperty =
        DependencyProperty.Register(
            nameof(ViewportHeight),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ScrollBarThicknessProperty =
        DependencyProperty.Register(
            nameof(ScrollBarThickness),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(12f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(new Color(20, 20, 20), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(new Color(78, 78, 78), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(1f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly ScrollBar _horizontalBar;
    private readonly ScrollBar _verticalBar;
    private LayoutRect _contentViewportRect;
    private LayoutRect _lastArrangedContentRect;
    private FrameworkElement? _lastArrangedContentElement;
    private bool _hasSyncedScrollBarState;
    private float _lastSyncedHorizontalViewportSize;
    private float _lastSyncedVerticalViewportSize;
    private float _lastSyncedHorizontalMaximum;
    private float _lastSyncedVerticalMaximum;
    private float _lastSyncedHorizontalLargeChange;
    private float _lastSyncedVerticalLargeChange;
    private float _lastSyncedHorizontalValue;
    private float _lastSyncedVerticalValue;
    private bool _showHorizontalBar;
    private bool _showVerticalBar;
    private bool _hasArrangedContentRect;
    private bool _hasPreviousScrollBarResolution;
    private bool _previousShowHorizontalScrollBar;
    private bool _previousShowVerticalScrollBar;
    private bool _isReconcilingDescendantMeasureInvalidation;
    private int _inputScrollMutationDepth;
    private bool _suppressInternalScrollBarValueChange;
    private int _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private int _runtimeArrangeOverrideCallCount;
    private long _runtimeArrangeOverrideElapsedTicks;
    private int _runtimeResolveBarsAndMeasureContentCallCount;
    private long _runtimeResolveBarsAndMeasureContentElapsedTicks;
    private int _runtimeResolveBarsAndMeasureContentIterationCount;
    private int _runtimeResolveBarsAndMeasureContentHorizontalFlipCount;
    private int _runtimeResolveBarsAndMeasureContentVerticalFlipCount;
    private int _runtimeResolveBarsAndMeasureContentSingleMeasurePathCount;
    private int _runtimeResolveBarsAndMeasureContentRemeasurePathCount;
    private int _runtimeResolveBarsAndMeasureContentFallbackCount;
    private int _runtimeResolveBarsAndMeasureContentInitialHorizontalVisibleCount;
    private int _runtimeResolveBarsAndMeasureContentInitialHorizontalHiddenCount;
    private int _runtimeResolveBarsAndMeasureContentInitialVerticalVisibleCount;
    private int _runtimeResolveBarsAndMeasureContentInitialVerticalHiddenCount;
    private int _runtimeResolveBarsAndMeasureContentResolvedHorizontalVisibleCount;
    private int _runtimeResolveBarsAndMeasureContentResolvedHorizontalHiddenCount;
    private int _runtimeResolveBarsAndMeasureContentResolvedVerticalVisibleCount;
    private int _runtimeResolveBarsAndMeasureContentResolvedVerticalHiddenCount;
    private long _runtimeResolveBarsAndMeasureContentHottestTicks;
    private string _runtimeResolveBarsAndMeasureContentLastTrace = "n/a";
    private string _runtimeResolveBarsAndMeasureContentHottestTrace = "n/a";
    private int _runtimeResolveBarsForArrangeCallCount;
    private long _runtimeResolveBarsForArrangeElapsedTicks;
    private int _runtimeResolveBarsForArrangeIterationCount;
    private int _runtimeResolveBarsForArrangeHorizontalFlipCount;
    private int _runtimeResolveBarsForArrangeVerticalFlipCount;
    private int _runtimeMeasureContentCallCount;
    private long _runtimeMeasureContentElapsedTicks;
    private int _runtimeUpdateScrollBarsCallCount;
    private long _runtimeUpdateScrollBarsElapsedTicks;
    private int _runtimeScrollToHorizontalOffsetCallCount;
    private int _runtimeScrollToVerticalOffsetCallCount;
    private int _runtimeInvalidateScrollInfoCallCount;
    private int _runtimeHandleMouseWheelCallCount;
    private long _runtimeHandleMouseWheelElapsedTicks;
    private int _runtimeWheelEvents;
    private int _runtimeWheelHandled;
    private int _runtimeHandleMouseWheelHandledCount;
    private int _runtimeHandleMouseWheelIgnoredDisabledCount;
    private int _runtimeHandleMouseWheelIgnoredZeroDeltaCount;
    private int _runtimeSetOffsetsCallCount;
    private long _runtimeSetOffsetsElapsedTicks;
    private float _runtimeHorizontalDelta;
    private float _runtimeVerticalDelta;
    private int _runtimeSetOffsetsExternalSourceCount;
    private int _runtimeSetOffsetsHorizontalScrollBarSourceCount;
    private int _runtimeSetOffsetsVerticalScrollBarSourceCount;
    private int _runtimeSetOffsetsWorkCount;
    private int _runtimeSetOffsetsNoOpCount;
    private int _runtimeSetOffsetsDeferredLayoutPathCount;
    private int _runtimeSetOffsetsVirtualizingMeasureInvalidationPathCount;
    private int _runtimeSetOffsetsVirtualizingArrangeOnlyPathCount;
    private int _runtimeSetOffsetsTransformInvalidationPathCount;
    private int _runtimeSetOffsetsManualArrangePathCount;
    private int _runtimePopupCloseCallCount;
    private int _runtimeArrangeContentForCurrentOffsetsCallCount;
    private long _runtimeArrangeContentForCurrentOffsetsElapsedTicks;
    private int _runtimeArrangeContentSkippedNoContentCount;
    private int _runtimeArrangeContentSkippedZeroViewportCount;
    private int _runtimeArrangeContentTransformPathCount;
    private int _runtimeArrangeContentOffsetPathCount;
    private int _runtimeUpdateScrollBarValuesCallCount;
    private long _runtimeUpdateScrollBarValuesElapsedTicks;
    private int _runtimeUpdateHorizontalScrollBarValueCallCount;
    private long _runtimeUpdateHorizontalScrollBarValueElapsedTicks;
    private int _runtimeUpdateVerticalScrollBarValueCallCount;
    private long _runtimeUpdateVerticalScrollBarValueElapsedTicks;
    private int _runtimeHorizontalValueChangedCallCount;
    private long _runtimeHorizontalValueChangedElapsedTicks;
    private long _runtimeHorizontalValueChangedSetOffsetsElapsedTicks;
    private int _runtimeHorizontalValueChangedSuppressedCount;
    private int _runtimeVerticalValueChangedCallCount;
    private long _runtimeVerticalValueChangedElapsedTicks;
    private long _runtimeVerticalValueChangedSetOffsetsElapsedTicks;
    private int _lastTransformScrollDirtyHintDrawCount = -1;
    private int _runtimeVerticalValueChangedSuppressedCount;
    private static int _diagWheelEvents;
    private static int _diagWheelHandled;
    private static int _diagSetOffsetCalls;
    private static int _diagSetOffsetNoOp;
    private static float _diagVerticalDelta;
    private static float _diagHorizontalDelta;
    private static int _diagScrollToHorizontalOffsetCallCount;
    private static int _diagScrollToVerticalOffsetCallCount;
    private static int _diagInvalidateScrollInfoCallCount;
    private static int _diagHandleMouseWheelCallCount;
    private static long _diagHandleMouseWheelElapsedTicks;
    private static int _diagHandleMouseWheelHandledCount;
    private static int _diagHandleMouseWheelIgnoredDisabledCount;
    private static int _diagHandleMouseWheelIgnoredZeroDeltaCount;
    private static int _diagInteractionSetOffsetsCallCount;
    private static int _diagInteractionSetOffsetsNoOpCount;
    private static long _diagSetOffsetsElapsedTicks;
    private static int _diagSetOffsetsExternalSourceCount;
    private static int _diagSetOffsetsHorizontalScrollBarSourceCount;
    private static int _diagSetOffsetsVerticalScrollBarSourceCount;
    private static int _diagSetOffsetsWorkCount;
    private static int _diagSetOffsetsDeferredLayoutPathCount;
    private static int _diagSetOffsetsVirtualizingMeasureInvalidationPathCount;
    private static int _diagSetOffsetsVirtualizingArrangeOnlyPathCount;
    private static int _diagSetOffsetsTransformInvalidationPathCount;
    private static int _diagSetOffsetsManualArrangePathCount;
    private static int _diagPopupCloseCallCount;
    private static int _diagArrangeContentForCurrentOffsetsCallCount;
    private static long _diagArrangeContentForCurrentOffsetsElapsedTicks;
    private static int _diagArrangeContentSkippedNoContentCount;
    private static int _diagArrangeContentSkippedZeroViewportCount;
    private static int _diagArrangeContentTransformPathCount;
    private static int _diagArrangeContentOffsetPathCount;
    private static int _diagUpdateScrollBarValuesCallCount;
    private static long _diagUpdateScrollBarValuesElapsedTicks;
    private static int _diagUpdateHorizontalScrollBarValueCallCount;
    private static long _diagUpdateHorizontalScrollBarValueElapsedTicks;
    private static int _diagUpdateVerticalScrollBarValueCallCount;
    private static long _diagUpdateVerticalScrollBarValueElapsedTicks;
    private static int _diagHorizontalValueChangedCallCount;
    private static long _diagHorizontalValueChangedElapsedTicks;
    private static long _diagHorizontalValueChangedSetOffsetsElapsedTicks;
    private static int _diagHorizontalValueChangedSuppressedCount;
    private static int _diagVerticalValueChangedCallCount;
    private static long _diagVerticalValueChangedElapsedTicks;
    private static long _diagVerticalValueChangedSetOffsetsElapsedTicks;
    private static int _diagVerticalValueChangedSuppressedCount;
    private static int _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static int _diagArrangeOverrideCallCount;
    private static long _diagArrangeOverrideElapsedTicks;
    private static int _diagResolveBarsAndMeasureContentCallCount;
    private static long _diagResolveBarsAndMeasureContentElapsedTicks;
    private static int _diagResolveBarsAndMeasureContentIterationCount;
    private static int _diagResolveBarsAndMeasureContentHorizontalFlipCount;
    private static int _diagResolveBarsAndMeasureContentVerticalFlipCount;
    private static int _diagResolveBarsAndMeasureContentSingleMeasurePathCount;
    private static int _diagResolveBarsAndMeasureContentRemeasurePathCount;
    private static int _diagResolveBarsAndMeasureContentFallbackCount;
    private static int _diagResolveBarsAndMeasureContentInitialHorizontalVisibleCount;
    private static int _diagResolveBarsAndMeasureContentInitialHorizontalHiddenCount;
    private static int _diagResolveBarsAndMeasureContentInitialVerticalVisibleCount;
    private static int _diagResolveBarsAndMeasureContentInitialVerticalHiddenCount;
    private static int _diagResolveBarsAndMeasureContentResolvedHorizontalVisibleCount;
    private static int _diagResolveBarsAndMeasureContentResolvedHorizontalHiddenCount;
    private static int _diagResolveBarsAndMeasureContentResolvedVerticalVisibleCount;
    private static int _diagResolveBarsAndMeasureContentResolvedVerticalHiddenCount;
    private static int _diagResolveBarsForArrangeCallCount;
    private static long _diagResolveBarsForArrangeElapsedTicks;
    private static int _diagResolveBarsForArrangeIterationCount;
    private static int _diagResolveBarsForArrangeHorizontalFlipCount;
    private static int _diagResolveBarsForArrangeVerticalFlipCount;
    private static int _diagMeasureContentCallCount;
    private static long _diagMeasureContentElapsedTicks;
    private static int _diagUpdateScrollBarsCallCount;
    private static long _diagUpdateScrollBarsElapsedTicks;
    private const float DefaultLineScrollStep = 24f;

    public event EventHandler? ViewportChanged;

    public ScrollViewer()
    {
        _horizontalBar = new ScrollBar { Orientation = Orientation.Horizontal };
        _verticalBar = new ScrollBar { Orientation = Orientation.Vertical };
        _horizontalBar.ValueChanged += OnHorizontalScrollBarValueChanged;
        _verticalBar.ValueChanged += OnVerticalScrollBarValueChanged;

        _horizontalBar.SetVisualParent(this);
        _horizontalBar.SetLogicalParent(this);
        _verticalBar.SetVisualParent(this);
        _verticalBar.SetLogicalParent(this);
    }

    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => GetValue<ScrollBarVisibility>(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => GetValue<ScrollBarVisibility>(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    internal void SetInternalScrollBarLineButtonVisibility(bool showHorizontalLineButtons, bool showVerticalLineButtons)
    {
        var horizontalChanged = _horizontalBar.ShowLineButtons != showHorizontalLineButtons;
        var verticalChanged = _verticalBar.ShowLineButtons != showVerticalLineButtons;
        if (!horizontalChanged && !verticalChanged)
        {
            return;
        }

        _horizontalBar.ShowLineButtons = showHorizontalLineButtons;
        _verticalBar.ShowLineButtons = showVerticalLineButtons;
        InvalidateMeasure();
        InvalidateArrange();
    }

    public float HorizontalOffset
    {
        get => GetValue<float>(HorizontalOffsetProperty);
        private set => SetValue(HorizontalOffsetProperty, value);
    }

    public float VerticalOffset
    {
        get => GetValue<float>(VerticalOffsetProperty);
        private set => SetValue(VerticalOffsetProperty, value);
    }

    public float ExtentWidth
    {
        get => GetValue<float>(ExtentWidthProperty);
        private set => SetValue(ExtentWidthProperty, value);
    }

    public float ExtentHeight
    {
        get => GetValue<float>(ExtentHeightProperty);
        private set => SetValue(ExtentHeightProperty, value);
    }

    public float ViewportWidth
    {
        get => GetValue<float>(ViewportWidthProperty);
        private set => SetValue(ViewportWidthProperty, value);
    }

    public float ViewportHeight
    {
        get => GetValue<float>(ViewportHeightProperty);
        private set => SetValue(ViewportHeightProperty, value);
    }

    public float ScrollBarThickness
    {
        get => GetValue<float>(ScrollBarThicknessProperty);
        set => SetValue(ScrollBarThicknessProperty, value);
    }

    public override void InvalidateMeasure()
    {
        base.InvalidateMeasure();
    }

    protected override bool TryHandleMeasureInvalidation(UIElement origin, UIElement? source, string reason)
    {
        _ = source;
        _ = reason;

        if (ReferenceEquals(origin, this) ||
            _isReconcilingDescendantMeasureInvalidation ||
            NeedsMeasure ||
            ContentElement is not FrameworkElement content ||
            !IsContentDescendant(origin))
        {
            return false;
        }

        if (source is FrameworkElement sourceElement &&
            (!sourceElement.IsMeasureValidForTests ||
             !sourceElement.IsArrangeValidForTests ||
             sourceElement.NeedsMeasure ||
             sourceElement.NeedsArrange))
        {
            return false;
        }

        var availableSize = PreviousAvailableSizeForTests;
        if (float.IsNaN(availableSize.X) || float.IsNaN(availableSize.Y))
        {
            return false;
        }

        var margin = Margin;
        var innerAvailable = new Vector2(
            MathF.Max(0f, availableSize.X - margin.Horizontal),
            MathF.Max(0f, availableSize.Y - margin.Vertical));
        var border = MathF.Max(0f, BorderThickness);
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var contentBounds = new LayoutRect(
            LayoutSlot.X + border,
            LayoutSlot.Y + border,
            MathF.Max(0f, innerAvailable.X - (border * 2f)),
            MathF.Max(0f, innerAvailable.Y - (border * 2f)));

        _isReconcilingDescendantMeasureInvalidation = true;
        try
        {
            var decision = ResolveBarsAndMeasureContent(contentBounds);
            var canPreserveArrangeRepairPath = CanPreserveArrangeRepairPath(decision.ShowHorizontalBar, decision.ShowVerticalBar, decision.ViewportRect);
            var desiredViewportWidth = float.IsFinite(contentBounds.Width)
                ? MathF.Min(decision.ExtentWidth, decision.ViewportRect.Width)
                : decision.ExtentWidth;
            var desiredViewportHeight = float.IsFinite(contentBounds.Height)
                ? MathF.Min(decision.ExtentHeight, decision.ViewportRect.Height)
                : decision.ExtentHeight;
            var desiredWidth = desiredViewportWidth + (border * 2f) + GetVerticalBarReservation(decision.ShowVerticalBar, verticalBarThickness);
            var desiredHeight = desiredViewportHeight + (border * 2f) + GetHorizontalBarReservation(decision.ShowHorizontalBar, horizontalBarThickness);
            var measuredWidth = float.IsNaN(Width) ? desiredWidth : Width;
            var measuredHeight = float.IsNaN(Height) ? desiredHeight : Height;
            measuredWidth = Math.Clamp(measuredWidth, MinWidth, MaxWidth);
            measuredHeight = Math.Clamp(measuredHeight, MinHeight, MaxHeight);
            var reconciledDesired = new Vector2(
                MathF.Max(0f, measuredWidth + margin.Horizontal),
                MathF.Max(0f, measuredHeight + margin.Vertical));
            if (!AreFloatsClose(reconciledDesired.X, DesiredSize.X) ||
                !AreFloatsClose(reconciledDesired.Y, DesiredSize.Y))
            {
                return false;
            }

            ApplyScrollMetrics(decision.ExtentWidth, decision.ExtentHeight, decision.ViewportWidth, decision.ViewportHeight, publishViewportMetrics: false);
            _showHorizontalBar = decision.ShowHorizontalBar;
            _showVerticalBar = decision.ShowVerticalBar;
            CacheResolvedScrollBarVisibility(decision.ShowHorizontalBar, decision.ShowVerticalBar);
            MarkMeasureValidAfterLocalReconciliation();

            if (canPreserveArrangeRepairPath)
            {
                _contentViewportRect = decision.ViewportRect;
                SetOffsets(HorizontalOffset, VerticalOffset);
                UpdateScrollBars();
            }
            else
            {
                InvalidateArrangeForDirectLayoutOnly();
            }

            return true;
        }
        finally
        {
            _isReconcilingDescendantMeasureInvalidation = false;
        }
    }

    protected internal override bool ShouldSuppressMeasureInvalidationFromDescendantDuringMeasure(FrameworkElement descendant)
    {
        return _isReconcilingDescendantMeasureInvalidation && IsContentDescendant(descendant);
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

    public new float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public static bool GetUseTransformContentScrolling(UIElement element)
    {
        return element.GetValue<bool>(UseTransformContentScrollingProperty);
    }

    public static void SetUseTransformContentScrolling(UIElement element, bool value)
    {
        element.SetValue(UseTransformContentScrollingProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var child in base.GetVisualChildren())
        {
            yield return child;
        }

        if (_showHorizontalBar)
        {
            yield return _horizontalBar;
        }

        if (_showVerticalBar)
        {
            yield return _verticalBar;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return base.GetVisualChildCountForTraversal() +
               (_showHorizontalBar ? 1 : 0) +
               (_showVerticalBar ? 1 : 0);
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        var baseCount = base.GetVisualChildCountForTraversal();
        if (index < baseCount)
        {
            return base.GetVisualChildAtForTraversal(index);
        }

        var extraIndex = index - baseCount;
        if (_showHorizontalBar)
        {
            if (extraIndex == 0)
            {
                return _horizontalBar;
            }

            extraIndex--;
        }

        if (_showVerticalBar && extraIndex == 0)
        {
            return _verticalBar;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var child in base.GetLogicalChildren())
        {
            yield return child;
        }

        if (_showHorizontalBar)
        {
            yield return _horizontalBar;
        }

        if (_showVerticalBar)
        {
            yield return _verticalBar;
        }
    }

    public void ScrollToHorizontalOffset(float offset)
    {
        _diagScrollToHorizontalOffsetCallCount++;
        _runtimeScrollToHorizontalOffsetCallCount++;
        SetOffsets(offset, VerticalOffset);
    }

    public void ScrollToVerticalOffset(float offset)
    {
        _diagScrollToVerticalOffsetCallCount++;
        _runtimeScrollToVerticalOffsetCallCount++;
        SetOffsets(HorizontalOffset, offset);
    }

    public void InvalidateScrollInfo()
    {
        _diagInvalidateScrollInfoCallCount++;
        _runtimeInvalidateScrollInfoCallCount++;
        InvalidateMeasure();
    }

    internal bool TryGetContentViewportClipRect(out LayoutRect clipRect)
    {
        clipRect = _contentViewportRect;
        return clipRect.Width > 0f && clipRect.Height > 0f;
    }

    internal bool ShouldUseTransformScrollViewportDirtyHint()
    {
        var uiRoot = UiRoot.Current;
        if (uiRoot == null)
        {
            return false;
        }

        return _lastTransformScrollDirtyHintDrawCount >= uiRoot.DrawExecutedFrameCount;
    }

    internal new static ScrollViewerTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot();
    }

    internal new static ScrollViewerTelemetrySnapshot GetTelemetryAndReset()
    {
        var snapshot = CreateAggregateTelemetrySnapshot();
        _diagWheelEvents = 0;
        _diagWheelHandled = 0;
        _diagSetOffsetCalls = 0;
        _diagSetOffsetNoOp = 0;
        _diagHorizontalDelta = 0f;
        _diagVerticalDelta = 0f;
        _diagScrollToHorizontalOffsetCallCount = 0;
        _diagScrollToVerticalOffsetCallCount = 0;
        _diagInvalidateScrollInfoCallCount = 0;
        _diagHandleMouseWheelCallCount = 0;
        _diagHandleMouseWheelElapsedTicks = 0L;
        _diagHandleMouseWheelHandledCount = 0;
        _diagHandleMouseWheelIgnoredDisabledCount = 0;
        _diagHandleMouseWheelIgnoredZeroDeltaCount = 0;
        _diagInteractionSetOffsetsCallCount = 0;
        _diagInteractionSetOffsetsNoOpCount = 0;
        _diagSetOffsetsElapsedTicks = 0L;
        _diagSetOffsetsExternalSourceCount = 0;
        _diagSetOffsetsHorizontalScrollBarSourceCount = 0;
        _diagSetOffsetsVerticalScrollBarSourceCount = 0;
        _diagSetOffsetsWorkCount = 0;
        _diagSetOffsetsDeferredLayoutPathCount = 0;
        _diagSetOffsetsVirtualizingMeasureInvalidationPathCount = 0;
        _diagSetOffsetsVirtualizingArrangeOnlyPathCount = 0;
        _diagSetOffsetsTransformInvalidationPathCount = 0;
        _diagSetOffsetsManualArrangePathCount = 0;
        _diagPopupCloseCallCount = 0;
        _diagArrangeContentForCurrentOffsetsCallCount = 0;
        _diagArrangeContentForCurrentOffsetsElapsedTicks = 0L;
        _diagArrangeContentSkippedNoContentCount = 0;
        _diagArrangeContentSkippedZeroViewportCount = 0;
        _diagArrangeContentTransformPathCount = 0;
        _diagArrangeContentOffsetPathCount = 0;
        _diagUpdateScrollBarValuesCallCount = 0;
        _diagUpdateScrollBarValuesElapsedTicks = 0L;
        _diagUpdateHorizontalScrollBarValueCallCount = 0;
        _diagUpdateHorizontalScrollBarValueElapsedTicks = 0L;
        _diagUpdateVerticalScrollBarValueCallCount = 0;
        _diagUpdateVerticalScrollBarValueElapsedTicks = 0L;
        _diagHorizontalValueChangedCallCount = 0;
        _diagHorizontalValueChangedElapsedTicks = 0L;
        _diagHorizontalValueChangedSetOffsetsElapsedTicks = 0L;
        _diagHorizontalValueChangedSuppressedCount = 0;
        _diagVerticalValueChangedCallCount = 0;
        _diagVerticalValueChangedElapsedTicks = 0L;
        _diagVerticalValueChangedSetOffsetsElapsedTicks = 0L;
        _diagVerticalValueChangedSuppressedCount = 0;
        _diagMeasureOverrideCallCount = 0;
        _diagMeasureOverrideElapsedTicks = 0L;
        _diagArrangeOverrideCallCount = 0;
        _diagArrangeOverrideElapsedTicks = 0L;
        _diagResolveBarsAndMeasureContentCallCount = 0;
        _diagResolveBarsAndMeasureContentElapsedTicks = 0L;
        _diagResolveBarsAndMeasureContentIterationCount = 0;
        _diagResolveBarsAndMeasureContentHorizontalFlipCount = 0;
        _diagResolveBarsAndMeasureContentVerticalFlipCount = 0;
        _diagResolveBarsAndMeasureContentSingleMeasurePathCount = 0;
        _diagResolveBarsAndMeasureContentRemeasurePathCount = 0;
        _diagResolveBarsAndMeasureContentFallbackCount = 0;
        _diagResolveBarsAndMeasureContentInitialHorizontalVisibleCount = 0;
        _diagResolveBarsAndMeasureContentInitialHorizontalHiddenCount = 0;
        _diagResolveBarsAndMeasureContentInitialVerticalVisibleCount = 0;
        _diagResolveBarsAndMeasureContentInitialVerticalHiddenCount = 0;
        _diagResolveBarsAndMeasureContentResolvedHorizontalVisibleCount = 0;
        _diagResolveBarsAndMeasureContentResolvedHorizontalHiddenCount = 0;
        _diagResolveBarsAndMeasureContentResolvedVerticalVisibleCount = 0;
        _diagResolveBarsAndMeasureContentResolvedVerticalHiddenCount = 0;
        _diagResolveBarsForArrangeCallCount = 0;
        _diagResolveBarsForArrangeElapsedTicks = 0L;
        _diagResolveBarsForArrangeIterationCount = 0;
        _diagResolveBarsForArrangeHorizontalFlipCount = 0;
        _diagResolveBarsForArrangeVerticalFlipCount = 0;
        _diagMeasureContentCallCount = 0;
        _diagMeasureContentElapsedTicks = 0L;
        _diagUpdateScrollBarsCallCount = 0;
        _diagUpdateScrollBarsElapsedTicks = 0L;
        return snapshot;
    }

    internal ScrollViewerRuntimeDiagnosticsSnapshot GetScrollViewerSnapshotForDiagnostics()
    {
        return new ScrollViewerRuntimeDiagnosticsSnapshot(
            _showHorizontalBar,
            _showVerticalBar,
            _hasPreviousScrollBarResolution,
            _previousShowHorizontalScrollBar,
            _previousShowVerticalScrollBar,
            _suppressInternalScrollBarValueChange,
            _inputScrollMutationDepth,
            _contentViewportRect.X,
            _contentViewportRect.Y,
            _contentViewportRect.Width,
            _contentViewportRect.Height,
            _runtimeResolveBarsAndMeasureContentLastTrace,
            _runtimeResolveBarsAndMeasureContentHottestTrace,
            TicksToMilliseconds(_runtimeResolveBarsAndMeasureContentHottestTicks),
            _runtimeWheelEvents,
            _runtimeWheelHandled,
            _runtimeSetOffsetsCallCount,
            _runtimeSetOffsetsNoOpCount,
            _runtimeHorizontalDelta,
            _runtimeVerticalDelta,
            _runtimeScrollToHorizontalOffsetCallCount,
            _runtimeScrollToVerticalOffsetCallCount,
            _runtimeInvalidateScrollInfoCallCount,
            _runtimeHandleMouseWheelCallCount,
            TicksToMilliseconds(_runtimeHandleMouseWheelElapsedTicks),
            _runtimeHandleMouseWheelHandledCount,
            _runtimeHandleMouseWheelIgnoredDisabledCount,
            _runtimeHandleMouseWheelIgnoredZeroDeltaCount,
            TicksToMilliseconds(_runtimeSetOffsetsElapsedTicks),
            _runtimeSetOffsetsExternalSourceCount,
            _runtimeSetOffsetsHorizontalScrollBarSourceCount,
            _runtimeSetOffsetsVerticalScrollBarSourceCount,
            _runtimeSetOffsetsWorkCount,
            _runtimeSetOffsetsDeferredLayoutPathCount,
            _runtimeSetOffsetsVirtualizingMeasureInvalidationPathCount,
            _runtimeSetOffsetsVirtualizingArrangeOnlyPathCount,
            _runtimeSetOffsetsTransformInvalidationPathCount,
            _runtimeSetOffsetsManualArrangePathCount,
            _runtimePopupCloseCallCount,
            _runtimeArrangeContentForCurrentOffsetsCallCount,
            TicksToMilliseconds(_runtimeArrangeContentForCurrentOffsetsElapsedTicks),
            _runtimeArrangeContentSkippedNoContentCount,
            _runtimeArrangeContentSkippedZeroViewportCount,
            _runtimeArrangeContentTransformPathCount,
            _runtimeArrangeContentOffsetPathCount,
            _runtimeUpdateScrollBarValuesCallCount,
            TicksToMilliseconds(_runtimeUpdateScrollBarValuesElapsedTicks),
            _runtimeUpdateHorizontalScrollBarValueCallCount,
            TicksToMilliseconds(_runtimeUpdateHorizontalScrollBarValueElapsedTicks),
            _runtimeUpdateVerticalScrollBarValueCallCount,
            TicksToMilliseconds(_runtimeUpdateVerticalScrollBarValueElapsedTicks),
            _runtimeHorizontalValueChangedCallCount,
            TicksToMilliseconds(_runtimeHorizontalValueChangedElapsedTicks),
            TicksToMilliseconds(_runtimeHorizontalValueChangedSetOffsetsElapsedTicks),
            _runtimeHorizontalValueChangedSuppressedCount,
            _runtimeVerticalValueChangedCallCount,
            TicksToMilliseconds(_runtimeVerticalValueChangedElapsedTicks),
            TicksToMilliseconds(_runtimeVerticalValueChangedSetOffsetsElapsedTicks),
            _runtimeVerticalValueChangedSuppressedCount,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeArrangeOverrideCallCount,
            TicksToMilliseconds(_runtimeArrangeOverrideElapsedTicks),
            _runtimeResolveBarsAndMeasureContentCallCount,
            TicksToMilliseconds(_runtimeResolveBarsAndMeasureContentElapsedTicks),
            _runtimeResolveBarsAndMeasureContentIterationCount,
            _runtimeResolveBarsAndMeasureContentHorizontalFlipCount,
            _runtimeResolveBarsAndMeasureContentVerticalFlipCount,
            _runtimeResolveBarsAndMeasureContentSingleMeasurePathCount,
            _runtimeResolveBarsAndMeasureContentRemeasurePathCount,
            _runtimeResolveBarsAndMeasureContentFallbackCount,
            _runtimeResolveBarsAndMeasureContentInitialHorizontalVisibleCount,
            _runtimeResolveBarsAndMeasureContentInitialHorizontalHiddenCount,
            _runtimeResolveBarsAndMeasureContentInitialVerticalVisibleCount,
            _runtimeResolveBarsAndMeasureContentInitialVerticalHiddenCount,
            _runtimeResolveBarsAndMeasureContentResolvedHorizontalVisibleCount,
            _runtimeResolveBarsAndMeasureContentResolvedHorizontalHiddenCount,
            _runtimeResolveBarsAndMeasureContentResolvedVerticalVisibleCount,
            _runtimeResolveBarsAndMeasureContentResolvedVerticalHiddenCount,
            _runtimeResolveBarsForArrangeCallCount,
            TicksToMilliseconds(_runtimeResolveBarsForArrangeElapsedTicks),
            _runtimeResolveBarsForArrangeIterationCount,
            _runtimeResolveBarsForArrangeHorizontalFlipCount,
            _runtimeResolveBarsForArrangeVerticalFlipCount,
            _runtimeMeasureContentCallCount,
            TicksToMilliseconds(_runtimeMeasureContentElapsedTicks),
            _runtimeUpdateScrollBarsCallCount,
            TicksToMilliseconds(_runtimeUpdateScrollBarsElapsedTicks));
    }

    private static ScrollViewerTelemetrySnapshot CreateAggregateTelemetrySnapshot()
    {
        return new ScrollViewerTelemetrySnapshot(
            _diagWheelEvents,
            _diagWheelHandled,
            _diagSetOffsetCalls,
            _diagSetOffsetNoOp,
            _diagHorizontalDelta,
            _diagVerticalDelta,
            _diagScrollToHorizontalOffsetCallCount,
            _diagScrollToVerticalOffsetCallCount,
            _diagInvalidateScrollInfoCallCount,
            _diagHandleMouseWheelCallCount,
            TicksToMilliseconds(_diagHandleMouseWheelElapsedTicks),
            _diagHandleMouseWheelHandledCount,
            _diagHandleMouseWheelIgnoredDisabledCount,
            _diagHandleMouseWheelIgnoredZeroDeltaCount,
            TicksToMilliseconds(_diagSetOffsetsElapsedTicks),
            _diagSetOffsetsExternalSourceCount,
            _diagSetOffsetsHorizontalScrollBarSourceCount,
            _diagSetOffsetsVerticalScrollBarSourceCount,
            _diagSetOffsetsWorkCount,
            _diagSetOffsetsDeferredLayoutPathCount,
            _diagSetOffsetsVirtualizingMeasureInvalidationPathCount,
            _diagSetOffsetsVirtualizingArrangeOnlyPathCount,
            _diagSetOffsetsTransformInvalidationPathCount,
            _diagSetOffsetsManualArrangePathCount,
            _diagPopupCloseCallCount,
            _diagArrangeContentForCurrentOffsetsCallCount,
            TicksToMilliseconds(_diagArrangeContentForCurrentOffsetsElapsedTicks),
            _diagArrangeContentSkippedNoContentCount,
            _diagArrangeContentSkippedZeroViewportCount,
            _diagArrangeContentTransformPathCount,
            _diagArrangeContentOffsetPathCount,
            _diagUpdateScrollBarValuesCallCount,
            TicksToMilliseconds(_diagUpdateScrollBarValuesElapsedTicks),
            _diagUpdateHorizontalScrollBarValueCallCount,
            TicksToMilliseconds(_diagUpdateHorizontalScrollBarValueElapsedTicks),
            _diagUpdateVerticalScrollBarValueCallCount,
            TicksToMilliseconds(_diagUpdateVerticalScrollBarValueElapsedTicks),
            _diagHorizontalValueChangedCallCount,
            TicksToMilliseconds(_diagHorizontalValueChangedElapsedTicks),
            TicksToMilliseconds(_diagHorizontalValueChangedSetOffsetsElapsedTicks),
            _diagHorizontalValueChangedSuppressedCount,
            _diagVerticalValueChangedCallCount,
            TicksToMilliseconds(_diagVerticalValueChangedElapsedTicks),
            TicksToMilliseconds(_diagVerticalValueChangedSetOffsetsElapsedTicks),
            _diagVerticalValueChangedSuppressedCount,
            _diagMeasureOverrideCallCount,
            TicksToMilliseconds(_diagMeasureOverrideElapsedTicks),
            _diagArrangeOverrideCallCount,
            TicksToMilliseconds(_diagArrangeOverrideElapsedTicks),
            _diagResolveBarsAndMeasureContentCallCount,
            TicksToMilliseconds(_diagResolveBarsAndMeasureContentElapsedTicks),
            _diagResolveBarsAndMeasureContentIterationCount,
            _diagResolveBarsAndMeasureContentHorizontalFlipCount,
            _diagResolveBarsAndMeasureContentVerticalFlipCount,
            _diagResolveBarsAndMeasureContentSingleMeasurePathCount,
            _diagResolveBarsAndMeasureContentRemeasurePathCount,
            _diagResolveBarsAndMeasureContentFallbackCount,
            _diagResolveBarsAndMeasureContentInitialHorizontalVisibleCount,
            _diagResolveBarsAndMeasureContentInitialHorizontalHiddenCount,
            _diagResolveBarsAndMeasureContentInitialVerticalVisibleCount,
            _diagResolveBarsAndMeasureContentInitialVerticalHiddenCount,
            _diagResolveBarsAndMeasureContentResolvedHorizontalVisibleCount,
            _diagResolveBarsAndMeasureContentResolvedHorizontalHiddenCount,
            _diagResolveBarsAndMeasureContentResolvedVerticalVisibleCount,
            _diagResolveBarsAndMeasureContentResolvedVerticalHiddenCount,
            _diagResolveBarsForArrangeCallCount,
            TicksToMilliseconds(_diagResolveBarsForArrangeElapsedTicks),
            _diagResolveBarsForArrangeIterationCount,
            _diagResolveBarsForArrangeHorizontalFlipCount,
            _diagResolveBarsForArrangeVerticalFlipCount,
            _diagMeasureContentCallCount,
            TicksToMilliseconds(_diagMeasureContentElapsedTicks),
            _diagUpdateScrollBarsCallCount,
            TicksToMilliseconds(_diagUpdateScrollBarsElapsedTicks));
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        _ = pointerPosition;
        return false;
    }

    internal bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        _ = pointerPosition;
        return false;
    }

    internal bool HandlePointerUpFromInput()
    {
        return false;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var border = MathF.Max(0f, BorderThickness);
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var contentBounds = new LayoutRect(
            LayoutSlot.X + border,
            LayoutSlot.Y + border,
            MathF.Max(0f, availableSize.X - (border * 2f)),
            MathF.Max(0f, availableSize.Y - (border * 2f)));

        var decision = ResolveBarsAndMeasureContent(contentBounds);
        ApplyScrollMetrics(decision.ExtentWidth, decision.ExtentHeight, decision.ViewportWidth, decision.ViewportHeight, publishViewportMetrics: false);
        _showHorizontalBar = decision.ShowHorizontalBar;
        _showVerticalBar = decision.ShowVerticalBar;
        CacheResolvedScrollBarVisibility(decision.ShowHorizontalBar, decision.ShowVerticalBar);
        _contentViewportRect = decision.ViewportRect;
        UpdateScrollBars();

        var desiredViewportWidth = float.IsFinite(contentBounds.Width)
            ? MathF.Min(decision.ExtentWidth, decision.ViewportRect.Width)
            : decision.ExtentWidth;
        var desiredViewportHeight = float.IsFinite(contentBounds.Height)
            ? MathF.Min(decision.ExtentHeight, decision.ViewportRect.Height)
            : decision.ExtentHeight;

        var desiredWidth = desiredViewportWidth + (border * 2f) + GetVerticalBarReservation(_showVerticalBar, verticalBarThickness);
        var desiredHeight = desiredViewportHeight + (border * 2f) + GetHorizontalBarReservation(_showHorizontalBar, horizontalBarThickness);
        _diagMeasureOverrideCallCount++;
        _diagMeasureOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeMeasureOverrideCallCount++;
        _runtimeMeasureOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        return new Vector2(MathF.Max(0f, desiredWidth), MathF.Max(0f, desiredHeight));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var previousViewportRect = _contentViewportRect;
        var previousShowHorizontalBar = _showHorizontalBar;
        var previousShowVerticalBar = _showVerticalBar;
        var border = MathF.Max(0f, BorderThickness);
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var fullRect = new LayoutRect(LayoutSlot.X + border, LayoutSlot.Y + border, MathF.Max(0f, finalSize.X - (border * 2f)), MathF.Max(0f, finalSize.Y - (border * 2f)));
        var decision = ResolveBarsForArrange(fullRect);
        ApplyScrollMetrics(decision.ExtentWidth, decision.ExtentHeight, decision.ViewportWidth, decision.ViewportHeight, publishViewportMetrics: true);
        _showHorizontalBar = decision.ShowHorizontalBar;
        _showVerticalBar = decision.ShowVerticalBar;
        CacheResolvedScrollBarVisibility(decision.ShowHorizontalBar, decision.ShowVerticalBar);
        _contentViewportRect = decision.ViewportRect;
        var clipStateChanged =
            previousShowHorizontalBar != _showHorizontalBar ||
            previousShowVerticalBar != _showVerticalBar ||
            !AreLayoutRectsClose(previousViewportRect, _contentViewportRect);
        CoerceOffsetsToCurrentMetrics();
        ArrangeContentForCurrentOffsets(previousViewportRect);

        if (clipStateChanged)
        {
            InvalidateVisual();
        }

        if (_showHorizontalBar)
        {
            _horizontalBar.Arrange(new LayoutRect(
                fullRect.X,
                fullRect.Y + fullRect.Height - horizontalBarThickness,
                MathF.Max(0f, fullRect.Width - GetVerticalBarReservation(_showVerticalBar, verticalBarThickness)),
                horizontalBarThickness));
        }

        if (_showVerticalBar)
        {
            _verticalBar.Arrange(new LayoutRect(
                fullRect.X + fullRect.Width - verticalBarThickness,
                fullRect.Y,
                verticalBarThickness,
                MathF.Max(0f, fullRect.Height - GetHorizontalBarReservation(_showHorizontalBar, horizontalBarThickness))));
        }

        UpdateScrollBars();
        _diagArrangeOverrideCallCount++;
        _diagArrangeOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeArrangeOverrideCallCount++;
        _runtimeArrangeOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        return finalSize;
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        if (!CanResolveAutoBarsWithoutRemeasure())
        {
            if (TryCanReuseSingleAutoAxisMeasure(previousAvailableSize, nextAvailableSize, out var canReuse))
            {
                return canReuse;
            }

            return false;
        }

        var widthChanged = !AreFloatsClose(previousAvailableSize.X, nextAvailableSize.X);
        var heightChanged = !AreFloatsClose(previousAvailableSize.Y, nextAvailableSize.Y);
        if (!widthChanged && !heightChanged)
        {
            return true;
        }

        if (ContentElement is not FrameworkElement content)
        {
            return true;
        }

        return content.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousAvailableSize, nextAvailableSize);
    }

    private bool TryCanReuseSingleAutoAxisMeasure(Vector2 previousAvailableSize, Vector2 nextAvailableSize, out bool canReuse)
    {
        canReuse = false;

        var verticalAutoWithHorizontalDisabled =
            VerticalScrollBarVisibility == ScrollBarVisibility.Auto &&
            HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled;
        var horizontalAutoWithVerticalDisabled =
            HorizontalScrollBarVisibility == ScrollBarVisibility.Auto &&
            VerticalScrollBarVisibility == ScrollBarVisibility.Disabled;

        if (!verticalAutoWithHorizontalDisabled && !horizontalAutoWithVerticalDisabled)
        {
            return false;
        }

        if (!_hasPreviousScrollBarResolution)
        {
            return true;
        }

        if (ContentElement is not FrameworkElement content)
        {
            canReuse = true;
            return true;
        }

        var border = MathF.Max(0f, BorderThickness);
        var previousContentWidth = MathF.Max(0f, previousAvailableSize.X - (border * 2f));
        var previousContentHeight = MathF.Max(0f, previousAvailableSize.Y - (border * 2f));
        var nextContentWidth = MathF.Max(0f, nextAvailableSize.X - (border * 2f));
        var nextContentHeight = MathF.Max(0f, nextAvailableSize.Y - (border * 2f));

        if (verticalAutoWithHorizontalDisabled)
        {
            var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
            var previousChildAvailable = new Vector2(
                MathF.Max(0f, previousContentWidth - GetVerticalBarReservation(_previousShowVerticalScrollBar, verticalBarThickness)),
                float.PositiveInfinity);
            var nextShowVertical = ExtentHeight > nextContentHeight + 0.01f;
            var nextChildAvailable = new Vector2(
                MathF.Max(0f, nextContentWidth - GetVerticalBarReservation(nextShowVertical, verticalBarThickness)),
                float.PositiveInfinity);
            canReuse = content.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousChildAvailable, nextChildAvailable);
            return true;
        }

        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var previousHorizontalChildAvailable = new Vector2(
            float.PositiveInfinity,
            MathF.Max(0f, previousContentHeight - GetHorizontalBarReservation(_previousShowHorizontalScrollBar, horizontalBarThickness)));
        var nextShowHorizontal = ExtentWidth > nextContentWidth + 0.01f;
        var nextHorizontalChildAvailable = new Vector2(
            float.PositiveInfinity,
            MathF.Max(0f, nextContentHeight - GetHorizontalBarReservation(nextShowHorizontal, horizontalBarThickness)));
        canReuse = content.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousHorizontalChildAvailable, nextHorizontalChildAvailable);
        return true;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background, Opacity);
        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, BorderThickness, BorderBrush, Opacity);
        }
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        clipRect = new LayoutRect(
            _contentViewportRect.X,
            _contentViewportRect.Y,
            _contentViewportRect.Width + GetVerticalBarReservation(_showVerticalBar, verticalBarThickness),
            _contentViewportRect.Height + GetHorizontalBarReservation(_showHorizontalBar, horizontalBarThickness));
        return true;
    }

    protected override void OnArrangedSubtreeTranslated(Vector2 delta)
    {
        if (!AreFloatsClose(delta.X, 0f) || !AreFloatsClose(delta.Y, 0f))
        {
            _contentViewportRect = OffsetCachedRect(_contentViewportRect, delta);

            if (_hasArrangedContentRect)
            {
                _lastArrangedContentRect = OffsetCachedRect(_lastArrangedContentRect, delta);
            }
        }

        base.OnArrangedSubtreeTranslated(delta);
    }

    private (bool ShowHorizontalBar, bool ShowVerticalBar, float ExtentWidth, float ExtentHeight, float ViewportWidth, float ViewportHeight, LayoutRect ViewportRect)
        ResolveBarsAndMeasureContent(LayoutRect bounds)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var canResolveAutoBarsWithoutRemeasure = CanResolveAutoBarsWithoutRemeasure();
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var showHorizontal = canResolveAutoBarsWithoutRemeasure
            ? ResolveInitialHorizontalScrollBarVisibility()
            : ResolveInitialHorizontalScrollBarVisibilityForRemeasure(bounds.Width);
        var showVertical = canResolveAutoBarsWithoutRemeasure
            ? ResolveInitialVerticalScrollBarVisibility()
            : ResolveInitialVerticalScrollBarVisibilityForRemeasure(bounds.Height);
        var initialShowHorizontal = showHorizontal;
        var initialShowVertical = showVertical;
        var trace = $"bounds={bounds.Width:0.##}x{bounds.Height:0.##};path={(canResolveAutoBarsWithoutRemeasure ? "single" : "remeasure")};initial={(showHorizontal ? 1 : 0)},{(showVertical ? 1 : 0)}";
        _diagResolveBarsAndMeasureContentCallCount++;
        _runtimeResolveBarsAndMeasureContentCallCount++;
        if (showHorizontal)
        {
            _diagResolveBarsAndMeasureContentInitialHorizontalVisibleCount++;
            _runtimeResolveBarsAndMeasureContentInitialHorizontalVisibleCount++;
        }
        else
        {
            _diagResolveBarsAndMeasureContentInitialHorizontalHiddenCount++;
            _runtimeResolveBarsAndMeasureContentInitialHorizontalHiddenCount++;
        }

        if (showVertical)
        {
            _diagResolveBarsAndMeasureContentInitialVerticalVisibleCount++;
            _runtimeResolveBarsAndMeasureContentInitialVerticalVisibleCount++;
        }
        else
        {
            _diagResolveBarsAndMeasureContentInitialVerticalHiddenCount++;
            _runtimeResolveBarsAndMeasureContentInitialVerticalHiddenCount++;
        }

        if (canResolveAutoBarsWithoutRemeasure)
        {
            _diagResolveBarsAndMeasureContentSingleMeasurePathCount++;
            _runtimeResolveBarsAndMeasureContentSingleMeasurePathCount++;
            var extentWidth = 0f;
            var extentHeight = 0f;

            for (var i = 0; i < 3; i++)
            {
                _diagResolveBarsAndMeasureContentIterationCount++;
                _runtimeResolveBarsAndMeasureContentIterationCount++;
                var viewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
                var viewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));

                if (i == 0)
                {
                    if (!TryReuseCurrentMeasuredContentForViewport(viewportWidth, viewportHeight, out extentWidth, out extentHeight))
                    {
                        MeasureContent(viewportWidth, viewportHeight, out extentWidth, out extentHeight);
                    }
                }

                var nextShowHorizontal = ResolveHorizontalAutoBarVisibility(showHorizontal, extentWidth, viewportWidth);
                var nextShowVertical = ResolveVerticalAutoBarVisibility(showVertical, extentHeight, viewportHeight);
                trace += $"|i{i}:vp={viewportWidth:0.##}x{viewportHeight:0.##},ext={extentWidth:0.##}x{extentHeight:0.##},next={(nextShowHorizontal ? 1 : 0)},{(nextShowVertical ? 1 : 0)}";

                if (nextShowHorizontal == showHorizontal && nextShowVertical == showVertical)
                {
                    RecordResolvedBarState(nextShowHorizontal, nextShowVertical);
                    RecordResolveBarsMeasureTrace(trace + $"|result={(nextShowHorizontal ? 1 : 0)},{(nextShowVertical ? 1 : 0)}", Stopwatch.GetTimestamp() - startTicks);
                    _diagResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
                    _runtimeResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
                    return (
                        showHorizontal,
                        showVertical,
                        extentWidth,
                        extentHeight,
                        viewportWidth,
                        viewportHeight,
                        new LayoutRect(bounds.X, bounds.Y, viewportWidth, viewportHeight));
                }

                if (i > 0 && nextShowHorizontal == initialShowHorizontal && nextShowVertical == initialShowVertical)
                {
                    var fallbackShowHorizontal = showHorizontal || nextShowHorizontal;
                    var fallbackShowVertical = showVertical || nextShowVertical;
                    var fallbackViewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(fallbackShowVertical, verticalBarThickness));
                    var fallbackViewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(fallbackShowHorizontal, horizontalBarThickness));
                    _diagResolveBarsAndMeasureContentFallbackCount++;
                    _runtimeResolveBarsAndMeasureContentFallbackCount++;
                    RecordResolvedBarState(fallbackShowHorizontal, fallbackShowVertical);
                    RecordResolveBarsMeasureTrace(trace + $"|fallback=1,result={(fallbackShowHorizontal ? 1 : 0)},{(fallbackShowVertical ? 1 : 0)}", Stopwatch.GetTimestamp() - startTicks);
                    _diagResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
                    _runtimeResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
                    return (
                        fallbackShowHorizontal,
                        fallbackShowVertical,
                        extentWidth,
                        extentHeight,
                        fallbackViewportWidth,
                        fallbackViewportHeight,
                        new LayoutRect(bounds.X, bounds.Y, fallbackViewportWidth, fallbackViewportHeight));
                }

                showHorizontal = nextShowHorizontal;
                showVertical = nextShowVertical;
            }

            var resolvedViewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
            var resolvedViewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));
            RecordResolvedBarState(showHorizontal, showVertical);
            RecordResolveBarsMeasureTrace(trace + $"|result={(showHorizontal ? 1 : 0)},{(showVertical ? 1 : 0)}", Stopwatch.GetTimestamp() - startTicks);
            _diagResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _runtimeResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return (
                showHorizontal,
                showVertical,
                extentWidth,
                extentHeight,
                resolvedViewportWidth,
                resolvedViewportHeight,
                new LayoutRect(bounds.X, bounds.Y, resolvedViewportWidth, resolvedViewportHeight));
        }

        _diagResolveBarsAndMeasureContentRemeasurePathCount++;
        _runtimeResolveBarsAndMeasureContentRemeasurePathCount++;
        var hasRemeasureViewport = false;
        var previousRemeasureViewportWidth = 0f;
        var previousRemeasureViewportHeight = 0f;
        for (var i = 0; i < 3; i++)
        {
            _diagResolveBarsAndMeasureContentIterationCount++;
            _runtimeResolveBarsAndMeasureContentIterationCount++;
            var viewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
            var viewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));

            var extentWidth = 0f;
            var extentHeight = 0f;
            var reusedMeasure = hasRemeasureViewport && TryReuseMeasuredContent(
                previousRemeasureViewportWidth,
                previousRemeasureViewportHeight,
                viewportWidth,
                viewportHeight,
                out extentWidth,
                out extentHeight);
            if (!reusedMeasure)
            {
                reusedMeasure = TryReuseCurrentMeasuredContentForViewport(viewportWidth, viewportHeight, out extentWidth, out extentHeight);
            }

            if (!reusedMeasure)
            {
                MeasureContent(viewportWidth, viewportHeight, out extentWidth, out extentHeight);
            }

            hasRemeasureViewport = true;
            previousRemeasureViewportWidth = viewportWidth;
            previousRemeasureViewportHeight = viewportHeight;
            var nextShowHorizontal = showHorizontal;
            var nextShowVertical = showVertical;

            nextShowHorizontal = ResolveHorizontalAutoBarVisibility(showHorizontal, extentWidth, viewportWidth);
            nextShowVertical = ResolveVerticalAutoBarVisibility(showVertical, extentHeight, viewportHeight);
            trace += $"|i{i}:vp={viewportWidth:0.##}x{viewportHeight:0.##},ext={extentWidth:0.##}x{extentHeight:0.##},next={(nextShowHorizontal ? 1 : 0)},{(nextShowVertical ? 1 : 0)}";

            if (nextShowHorizontal == showHorizontal && nextShowVertical == showVertical)
            {
                RecordResolvedBarState(nextShowHorizontal, nextShowVertical);
                RecordResolveBarsMeasureTrace(trace + $"|result={(nextShowHorizontal ? 1 : 0)},{(nextShowVertical ? 1 : 0)}", Stopwatch.GetTimestamp() - startTicks);
                _diagResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
                _runtimeResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
                return (
                    nextShowHorizontal,
                    nextShowVertical,
                    extentWidth,
                    extentHeight,
                    viewportWidth,
                    viewportHeight,
                    new LayoutRect(bounds.X, bounds.Y, viewportWidth, viewportHeight));
            }

            if (i > 0 && nextShowHorizontal == initialShowHorizontal && nextShowVertical == initialShowVertical)
            {
                var fallbackShowHorizontal = showHorizontal || nextShowHorizontal;
                var fallbackShowVertical = showVertical || nextShowVertical;
                var fallbackViewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(fallbackShowVertical, verticalBarThickness));
                var fallbackViewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(fallbackShowHorizontal, horizontalBarThickness));
                float fallbackExtentWidth;
                float fallbackExtentHeight;
                if (!TryReuseMeasuredContent(
                        previousRemeasureViewportWidth,
                        previousRemeasureViewportHeight,
                        fallbackViewportWidth,
                        fallbackViewportHeight,
                        out fallbackExtentWidth,
                        out fallbackExtentHeight) &&
                    !TryReuseCurrentMeasuredContentForViewport(fallbackViewportWidth, fallbackViewportHeight, out fallbackExtentWidth, out fallbackExtentHeight))
                {
                    MeasureContent(fallbackViewportWidth, fallbackViewportHeight, out fallbackExtentWidth, out fallbackExtentHeight);
                }

                _diagResolveBarsAndMeasureContentFallbackCount++;
                _runtimeResolveBarsAndMeasureContentFallbackCount++;
                RecordResolvedBarState(fallbackShowHorizontal, fallbackShowVertical);
                RecordResolveBarsMeasureTrace(trace + $"|fallback=1,fallbackVp={fallbackViewportWidth:0.##}x{fallbackViewportHeight:0.##},fallbackExt={fallbackExtentWidth:0.##}x{fallbackExtentHeight:0.##},result={(fallbackShowHorizontal ? 1 : 0)},{(fallbackShowVertical ? 1 : 0)}", Stopwatch.GetTimestamp() - startTicks);
                _diagResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
                _runtimeResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
                return (
                    fallbackShowHorizontal,
                    fallbackShowVertical,
                    fallbackExtentWidth,
                    fallbackExtentHeight,
                    fallbackViewportWidth,
                    fallbackViewportHeight,
                    new LayoutRect(bounds.X, bounds.Y, fallbackViewportWidth, fallbackViewportHeight));
            }

            showHorizontal = nextShowHorizontal;
            showVertical = nextShowVertical;
        }

        var finalViewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
        var finalViewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));
        float finalExtentWidth;
        float finalExtentHeight;
        if (!TryReuseMeasuredContent(
                previousRemeasureViewportWidth,
                previousRemeasureViewportHeight,
                finalViewportWidth,
                finalViewportHeight,
                out finalExtentWidth,
                out finalExtentHeight) &&
            !TryReuseCurrentMeasuredContentForViewport(finalViewportWidth, finalViewportHeight, out finalExtentWidth, out finalExtentHeight))
        {
            MeasureContent(finalViewportWidth, finalViewportHeight, out finalExtentWidth, out finalExtentHeight);
        }

        RecordResolvedBarState(showHorizontal, showVertical);
        RecordResolveBarsMeasureTrace(trace + $"|finalVp={finalViewportWidth:0.##}x{finalViewportHeight:0.##},finalExt={finalExtentWidth:0.##}x{finalExtentHeight:0.##},result={(showHorizontal ? 1 : 0)},{(showVertical ? 1 : 0)}", Stopwatch.GetTimestamp() - startTicks);
        _diagResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeResolveBarsAndMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;

        return (
            showHorizontal,
            showVertical,
            finalExtentWidth,
            finalExtentHeight,
            finalViewportWidth,
            finalViewportHeight,
            new LayoutRect(bounds.X, bounds.Y, finalViewportWidth, finalViewportHeight));
    }

    private void RecordResolvedBarState(bool showHorizontal, bool showVertical)
    {
        if (showHorizontal)
        {
            _diagResolveBarsAndMeasureContentResolvedHorizontalVisibleCount++;
            _runtimeResolveBarsAndMeasureContentResolvedHorizontalVisibleCount++;
        }
        else
        {
            _diagResolveBarsAndMeasureContentResolvedHorizontalHiddenCount++;
            _runtimeResolveBarsAndMeasureContentResolvedHorizontalHiddenCount++;
        }

        if (showVertical)
        {
            _diagResolveBarsAndMeasureContentResolvedVerticalVisibleCount++;
            _runtimeResolveBarsAndMeasureContentResolvedVerticalVisibleCount++;
        }
        else
        {
            _diagResolveBarsAndMeasureContentResolvedVerticalHiddenCount++;
            _runtimeResolveBarsAndMeasureContentResolvedVerticalHiddenCount++;
        }
    }

    private void RecordResolveBarsMeasureTrace(string trace, long elapsedTicks)
    {
        _runtimeResolveBarsAndMeasureContentLastTrace = trace;
        if (elapsedTicks > _runtimeResolveBarsAndMeasureContentHottestTicks)
        {
            _runtimeResolveBarsAndMeasureContentHottestTicks = elapsedTicks;
            _runtimeResolveBarsAndMeasureContentHottestTrace = trace;
        }
    }

    private (bool ShowHorizontalBar, bool ShowVerticalBar, float ExtentWidth, float ExtentHeight, float ViewportWidth, float ViewportHeight, LayoutRect ViewportRect)
        ResolveBarsForArrange(LayoutRect bounds)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var extentWidth = MathF.Max(0f, ExtentWidth);
        var extentHeight = MathF.Max(0f, ExtentHeight);
        var showHorizontal = ResolveInitialHorizontalScrollBarVisibility();
        var showVertical = ResolveInitialVerticalScrollBarVisibility();
        var initialShowHorizontal = showHorizontal;
        var initialShowVertical = showVertical;
        _diagResolveBarsForArrangeCallCount++;
        _runtimeResolveBarsForArrangeCallCount++;

        for (var i = 0; i < 3; i++)
        {
            _diagResolveBarsForArrangeIterationCount++;
            _runtimeResolveBarsForArrangeIterationCount++;
            var viewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
            var viewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));
            var nextShowHorizontal = showHorizontal;
            var nextShowVertical = showVertical;

            if (HorizontalScrollBarVisibility == ScrollBarVisibility.Auto)
            {
                nextShowHorizontal = extentWidth > viewportWidth + 0.01f;
                if (showHorizontal != nextShowHorizontal)
                {
                    _diagResolveBarsForArrangeHorizontalFlipCount++;
                    _runtimeResolveBarsForArrangeHorizontalFlipCount++;
                }
            }

            if (VerticalScrollBarVisibility == ScrollBarVisibility.Auto)
            {
                nextShowVertical = extentHeight > viewportHeight + 0.01f;
                if (showVertical != nextShowVertical)
                {
                    _diagResolveBarsForArrangeVerticalFlipCount++;
                    _runtimeResolveBarsForArrangeVerticalFlipCount++;
                }
            }

            if (nextShowHorizontal == showHorizontal && nextShowVertical == showVertical)
            {
                showHorizontal = nextShowHorizontal;
                showVertical = nextShowVertical;
                break;
            }

            if (i > 0 && nextShowHorizontal == initialShowHorizontal && nextShowVertical == initialShowVertical)
            {
                showHorizontal = showHorizontal || nextShowHorizontal;
                showVertical = showVertical || nextShowVertical;
                break;
            }

            showHorizontal = nextShowHorizontal;
            showVertical = nextShowVertical;
        }

        var finalViewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
        var finalViewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));
        _diagResolveBarsForArrangeElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeResolveBarsForArrangeElapsedTicks += Stopwatch.GetTimestamp() - startTicks;

        return (
            showHorizontal,
            showVertical,
            extentWidth,
            extentHeight,
            finalViewportWidth,
            finalViewportHeight,
            new LayoutRect(bounds.X, bounds.Y, finalViewportWidth, finalViewportHeight));
    }

    private void MeasureContent(float viewportWidth, float viewportHeight, out float extentWidth, out float extentHeight)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var canScrollHorizontally = HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
        var canScrollVertically = VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;

        if (ContentElement is FrameworkElement content)
        {
            var constraint = new Vector2(
                canScrollHorizontally ? float.PositiveInfinity : viewportWidth,
                canScrollVertically ? float.PositiveInfinity : viewportHeight);
            content.Measure(constraint);
        }

        extentWidth = (ContentElement as FrameworkElement)?.DesiredSize.X ?? 0f;
        extentHeight = (ContentElement as FrameworkElement)?.DesiredSize.Y ?? 0f;
        _diagMeasureContentCallCount++;
        _diagMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeMeasureContentCallCount++;
        _runtimeMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private bool TryReuseCurrentMeasuredContentForViewport(float viewportWidth, float viewportHeight, out float extentWidth, out float extentHeight)
    {
        if (!_hasPreviousScrollBarResolution)
        {
            extentWidth = 0f;
            extentHeight = 0f;
            return false;
        }

        return TryReuseMeasuredContent(
            _contentViewportRect.Width,
            _contentViewportRect.Height,
            viewportWidth,
            viewportHeight,
            out extentWidth,
            out extentHeight);
    }

    private bool TryReuseMeasuredContent(
        float previousViewportWidth,
        float previousViewportHeight,
        float nextViewportWidth,
        float nextViewportHeight,
        out float extentWidth,
        out float extentHeight)
    {
        if (ContentElement is not FrameworkElement content)
        {
            extentWidth = 0f;
            extentHeight = 0f;
            return false;
        }

        var previousConstraint = CreateContentMeasureConstraint(previousViewportWidth, previousViewportHeight);
        var nextConstraint = CreateContentMeasureConstraint(nextViewportWidth, nextViewportHeight);
        if (!content.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousConstraint, nextConstraint))
        {
            extentWidth = 0f;
            extentHeight = 0f;
            return false;
        }

        extentWidth = content.DesiredSize.X;
        extentHeight = content.DesiredSize.Y;
        return true;
    }

    private bool IsContentDescendant(UIElement element)
    {
        for (UIElement? current = element; current != null; current = current.GetInvalidationParent())
        {
            if (ReferenceEquals(current, ContentElement))
            {
                return true;
            }

            if (ReferenceEquals(current, this))
            {
                break;
            }
        }

        return false;
    }

    private bool CanResolveAutoBarsWithoutRemeasure()
    {
        var horizontalCanUseSingleMeasure = HorizontalScrollBarVisibility != ScrollBarVisibility.Auto ||
                                            VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
        var verticalCanUseSingleMeasure = VerticalScrollBarVisibility != ScrollBarVisibility.Auto ||
                                          HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
        return horizontalCanUseSingleMeasure && verticalCanUseSingleMeasure;
    }

    private static bool AreFloatsClose(float first, float second)
    {
        if (float.IsNaN(first) || float.IsNaN(second))
        {
            return false;
        }

        if (float.IsInfinity(first) || float.IsInfinity(second))
        {
            return float.IsPositiveInfinity(first) == float.IsPositiveInfinity(second) &&
                   float.IsNegativeInfinity(first) == float.IsNegativeInfinity(second);
        }

        return MathF.Abs(first - second) < 0.01f;
    }

    private static LayoutRect OffsetCachedRect(LayoutRect rect, Vector2 delta)
    {
        return new LayoutRect(rect.X + delta.X, rect.Y + delta.Y, rect.Width, rect.Height);
    }

    private bool ResolveHorizontalAutoBarVisibility(bool currentShowHorizontal, float extentWidth, float viewportWidth)
    {
        if (HorizontalScrollBarVisibility != ScrollBarVisibility.Auto)
        {
            return currentShowHorizontal;
        }

        var nextShowHorizontal = extentWidth > viewportWidth + 0.01f;
        if (currentShowHorizontal != nextShowHorizontal)
        {
            _diagResolveBarsAndMeasureContentHorizontalFlipCount++;
            _runtimeResolveBarsAndMeasureContentHorizontalFlipCount++;
        }

        return nextShowHorizontal;
    }

    private bool ResolveVerticalAutoBarVisibility(bool currentShowVertical, float extentHeight, float viewportHeight)
    {
        if (VerticalScrollBarVisibility != ScrollBarVisibility.Auto)
        {
            return currentShowVertical;
        }

        var nextShowVertical = extentHeight > viewportHeight + 0.01f;
        if (currentShowVertical != nextShowVertical)
        {
            _diagResolveBarsAndMeasureContentVerticalFlipCount++;
            _runtimeResolveBarsAndMeasureContentVerticalFlipCount++;
        }

        return nextShowVertical;
    }

    private void CacheResolvedScrollBarVisibility(bool showHorizontalBar, bool showVerticalBar)
    {
        var visibilityChanged = _hasPreviousScrollBarResolution &&
            (_previousShowHorizontalScrollBar != showHorizontalBar ||
             _previousShowVerticalScrollBar != showVerticalBar);

        _hasPreviousScrollBarResolution = true;
        _previousShowHorizontalScrollBar = showHorizontalBar;
        _previousShowVerticalScrollBar = showVerticalBar;

        if (visibilityChanged)
        {
            UiRoot.Current?.NotifyVisualStructureChanged(this, VisualParent, VisualParent);
        }
    }

    private bool ResolveInitialHorizontalScrollBarVisibility()
    {
        return HorizontalScrollBarVisibility switch
        {
            ScrollBarVisibility.Visible => true,
            ScrollBarVisibility.Disabled => false,
            ScrollBarVisibility.Hidden => false,
            ScrollBarVisibility.Auto => _hasPreviousScrollBarResolution && _previousShowHorizontalScrollBar,
            _ => false
        };
    }


    private bool CanPreserveArrangeRepairPath(bool showHorizontalBar, bool showVerticalBar, LayoutRect viewportRect)
    {
        return NeedsArrange &&
               UsesTransformBasedContentScrolling() &&
               showHorizontalBar == _showHorizontalBar &&
               showVerticalBar == _showVerticalBar &&
               AreLayoutRectsClose(viewportRect, _contentViewportRect);
    }

    private static bool AreLayoutRectsClose(LayoutRect left, LayoutRect right)
    {
        return AreFloatsClose(left.X, right.X) &&
               AreFloatsClose(left.Y, right.Y) &&
               AreFloatsClose(left.Width, right.Width) &&
               AreFloatsClose(left.Height, right.Height);
    }
    private bool ResolveInitialVerticalScrollBarVisibility()
    {
        return VerticalScrollBarVisibility switch
        {
            ScrollBarVisibility.Visible => true,
            ScrollBarVisibility.Disabled => false,
            ScrollBarVisibility.Hidden => false,
            ScrollBarVisibility.Auto => _hasPreviousScrollBarResolution && _previousShowVerticalScrollBar,
            _ => false
        };
    }

    private bool ResolveInitialHorizontalScrollBarVisibilityForRemeasure(float availableWidth)
    {
        return HorizontalScrollBarVisibility switch
        {
            ScrollBarVisibility.Visible => true,
            ScrollBarVisibility.Disabled => false,
            ScrollBarVisibility.Hidden => false,
            ScrollBarVisibility.Auto => _hasPreviousScrollBarResolution && ExtentWidth > availableWidth + 0.01f,
            _ => false
        };
    }

    private bool ResolveInitialVerticalScrollBarVisibilityForRemeasure(float availableHeight)
    {
        return VerticalScrollBarVisibility switch
        {
            ScrollBarVisibility.Visible => true,
            ScrollBarVisibility.Disabled => false,
            ScrollBarVisibility.Hidden => false,
            ScrollBarVisibility.Auto => _hasPreviousScrollBarResolution && ExtentHeight > availableHeight + 0.01f,
            _ => false
        };
    }

    private void ApplyScrollMetrics(
        float extentWidth,
        float extentHeight,
        float viewportWidth,
        float viewportHeight,
        bool publishViewportMetrics)
    {
        var previousExtentWidth = ExtentWidth;
        var previousExtentHeight = ExtentHeight;
        var previousViewportWidth = ViewportWidth;
        var previousViewportHeight = ViewportHeight;

        SetIfChanged(ExtentWidthProperty, CoerceNonNegativeFinite(extentWidth, previousExtentWidth), "scrollviewer_extent_width_write_count");
        SetIfChanged(ExtentHeightProperty, CoerceNonNegativeFinite(extentHeight, previousExtentHeight), "scrollviewer_extent_height_write_count");
        if (!publishViewportMetrics)
        {
            return;
        }

        SetIfChanged(ViewportWidthProperty, CoerceViewportMetric(viewportWidth, previousViewportWidth, ExtentWidth), "scrollviewer_viewport_width_write_count");
        SetIfChanged(ViewportHeightProperty, CoerceViewportMetric(viewportHeight, previousViewportHeight, ExtentHeight), "scrollviewer_viewport_height_write_count");

        if (MathF.Abs(previousExtentWidth - ExtentWidth) > 0.01f ||
            MathF.Abs(previousExtentHeight - ExtentHeight) > 0.01f ||
            MathF.Abs(previousViewportWidth - ViewportWidth) > 0.01f ||
            MathF.Abs(previousViewportHeight - ViewportHeight) > 0.01f)
        {
            ViewportChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CoerceOffsetsToCurrentMetrics()
    {
        var maxHorizontal = MathF.Max(0f, ExtentWidth - ViewportWidth);
        var maxVertical = MathF.Max(0f, ExtentHeight - ViewportHeight);
        var nextHorizontal = Math.Clamp(HorizontalOffset, 0f, maxHorizontal);
        var nextVertical = Math.Clamp(VerticalOffset, 0f, maxVertical);
        SetIfChanged(HorizontalOffsetProperty, nextHorizontal);
        SetIfChanged(VerticalOffsetProperty, nextVertical);
    }

    internal bool HandleMouseWheelFromInput(int delta)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagWheelEvents++;
        _runtimeWheelEvents++;
        _diagHandleMouseWheelCallCount++;
        _runtimeHandleMouseWheelCallCount++;
        if (!IsEnabled || delta == 0)
        {
            if (!IsEnabled)
            {
                _diagHandleMouseWheelIgnoredDisabledCount++;
                _runtimeHandleMouseWheelIgnoredDisabledCount++;
            }
            else
            {
                _diagHandleMouseWheelIgnoredZeroDeltaCount++;
                _runtimeHandleMouseWheelIgnoredZeroDeltaCount++;
            }

            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagHandleMouseWheelElapsedTicks += elapsedTicks;
            _runtimeHandleMouseWheelElapsedTicks += elapsedTicks;
            return false;
        }

        var beforeHorizontal = HorizontalOffset;
        var beforeVertical = VerticalOffset;
        var amount = DefaultLineScrollStep;
        var direction = delta > 0 ? -1f : 1f;
        BeginInputScrollMutation();
        try
        {
            SetOffsets(HorizontalOffset, VerticalOffset + (direction * amount));
        }
        finally
        {
            EndInputScrollMutation();
        }

        var handled = MathF.Abs(beforeHorizontal - HorizontalOffset) > 0.001f ||
                      MathF.Abs(beforeVertical - VerticalOffset) > 0.001f;
        if (handled)
        {
            _diagWheelHandled++;
            _runtimeWheelHandled++;
            _diagHandleMouseWheelHandledCount++;
            _runtimeHandleMouseWheelHandledCount++;
        }

        var totalElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagHandleMouseWheelElapsedTicks += totalElapsedTicks;
        _runtimeHandleMouseWheelElapsedTicks += totalElapsedTicks;

        return handled;
    }

    private void SetOffsets(float horizontal, float vertical, ScrollOffsetUpdateSource updateSource = ScrollOffsetUpdateSource.External)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagSetOffsetCalls++;
        _diagInteractionSetOffsetsCallCount++;
        _runtimeSetOffsetsCallCount++;
        switch (updateSource)
        {
            case ScrollOffsetUpdateSource.External:
                _diagSetOffsetsExternalSourceCount++;
                _runtimeSetOffsetsExternalSourceCount++;
                break;
            case ScrollOffsetUpdateSource.HorizontalScrollBar:
                _diagSetOffsetsHorizontalScrollBarSourceCount++;
                _runtimeSetOffsetsHorizontalScrollBarSourceCount++;
                break;
            case ScrollOffsetUpdateSource.VerticalScrollBar:
                _diagSetOffsetsVerticalScrollBarSourceCount++;
                _runtimeSetOffsetsVerticalScrollBarSourceCount++;
                break;
        }
        var beforeHorizontal = HorizontalOffset;
        var beforeVertical = VerticalOffset;
        var maxHorizontal = MathF.Max(0f, ExtentWidth - ViewportWidth);
        var maxVertical = MathF.Max(0f, ExtentHeight - ViewportHeight);
        var nextHorizontal = MathF.Max(0f, MathF.Min(maxHorizontal, horizontal));
        var nextVertical = MathF.Max(0f, MathF.Min(maxVertical, vertical));
        var horizontalDelta = MathF.Abs(beforeHorizontal - nextHorizontal);
        var verticalDelta = MathF.Abs(beforeVertical - nextVertical);

        _diagHorizontalDelta += horizontalDelta;
        _diagVerticalDelta += verticalDelta;
        _runtimeHorizontalDelta += horizontalDelta;
        _runtimeVerticalDelta += verticalDelta;
        if (horizontalDelta <= 0.001f && verticalDelta <= 0.001f)
        {
            _diagSetOffsetNoOp++;
            _diagInteractionSetOffsetsNoOpCount++;
            _runtimeSetOffsetsNoOpCount++;
            if (updateSource == ScrollOffsetUpdateSource.External)
            {
                UpdateScrollBarValues();
            }

            _diagSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _runtimeSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;

            return;
        }

        _diagSetOffsetsWorkCount++;
        _runtimeSetOffsetsWorkCount++;
        SetIfChanged(HorizontalOffsetProperty, nextHorizontal);
        SetIfChanged(VerticalOffsetProperty, nextVertical);
        ViewportChanged?.Invoke(this, EventArgs.Empty);

        if (!NeedsMeasure &&
            !NeedsArrange)
        {
            if (ContentElement is VirtualizingStackPanel virtualizingStackPanel)
            {
                if (virtualizingStackPanel.RequiresMeasureForViewerOwnedOffsetChange(beforeHorizontal, nextHorizontal, beforeVertical, nextVertical))
                {
                    _diagSetOffsetsVirtualizingMeasureInvalidationPathCount++;
                    _runtimeSetOffsetsVirtualizingMeasureInvalidationPathCount++;
                    virtualizingStackPanel.InvalidateMeasure();
                }
                else
                {
                    _diagSetOffsetsVirtualizingArrangeOnlyPathCount++;
                    _runtimeSetOffsetsVirtualizingArrangeOnlyPathCount++;
                    InvalidateArrange();
                }
            }
            else if (UsesTransformBasedContentScrolling())
            {
                _diagSetOffsetsTransformInvalidationPathCount++;
                _runtimeSetOffsetsTransformInvalidationPathCount++;
                RecordTransformScrollDirtyHintFrame();
                if (ContentElement is UIElement contentElement)
                {
                    UiRoot.Current?.NotifyDirectRenderInvalidation(contentElement);
                }
            }
            else
            {
                _diagSetOffsetsManualArrangePathCount++;
                _runtimeSetOffsetsManualArrangePathCount++;
                ArrangeContentForCurrentOffsets();
                InvalidateVisual();
            }
        }
        else
        {
            _diagSetOffsetsDeferredLayoutPathCount++;
            _runtimeSetOffsetsDeferredLayoutPathCount++;
        }

        if (ContentElement is UIElement transformScrollContent &&
            UsesTransformBasedContentScrolling() &&
            ContentElement is not VirtualizingStackPanel)
        {
            RecordTransformScrollDirtyHintFrame();
            UiRoot.Current?.NotifyDirectRenderInvalidation(transformScrollContent);
        }

        switch (updateSource)
        {
            case ScrollOffsetUpdateSource.External:
                UpdateScrollBarValues();
                break;
            case ScrollOffsetUpdateSource.HorizontalScrollBar:
                UpdateVerticalScrollBarValue();
                break;
            case ScrollOffsetUpdateSource.VerticalScrollBar:
                UpdateHorizontalScrollBarValue();
                break;
        }

        Popup.CloseAnchoredPopupsWithin(this);
        _diagPopupCloseCallCount++;
        _runtimePopupCloseCallCount++;
        _diagSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private void BeginInputScrollMutation()
    {
        _inputScrollMutationDepth++;
    }

    private void EndInputScrollMutation()
    {
        _inputScrollMutationDepth--;
    }

    private void RecordTransformScrollDirtyHintFrame()
    {
        var uiRoot = UiRoot.Current;
        if (uiRoot == null)
        {
            return;
        }

        _lastTransformScrollDirtyHintDrawCount = uiRoot.DrawExecutedFrameCount;
    }

    private void ArrangeContentForCurrentOffsets()
    {
        ArrangeContentForCurrentOffsets(_contentViewportRect);
    }

    private void ArrangeContentForCurrentOffsets(LayoutRect previousViewportRect)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagArrangeContentForCurrentOffsetsCallCount++;
        _runtimeArrangeContentForCurrentOffsetsCallCount++;
        if (ContentElement is not FrameworkElement content)
        {
            _hasArrangedContentRect = false;
            _lastArrangedContentElement = null;
            _diagArrangeContentSkippedNoContentCount++;
            _runtimeArrangeContentSkippedNoContentCount++;
            _diagArrangeContentForCurrentOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _runtimeArrangeContentForCurrentOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        if (_contentViewportRect.Width <= 0f || _contentViewportRect.Height <= 0f)
        {
            _hasArrangedContentRect = false;
            _lastArrangedContentElement = null;
            _diagArrangeContentSkippedZeroViewportCount++;
            _runtimeArrangeContentSkippedZeroViewportCount++;
            _diagArrangeContentForCurrentOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _runtimeArrangeContentForCurrentOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        var arrangedWidth = ResolveContentArrangeWidth(content, previousViewportRect);
        var arrangedHeight = ResolveContentArrangeHeight(content, previousViewportRect);
        var useTransformScrolling = UsesTransformBasedContentScrolling();
        if (useTransformScrolling)
        {
            _diagArrangeContentTransformPathCount++;
            _runtimeArrangeContentTransformPathCount++;
        }
        else
        {
            _diagArrangeContentOffsetPathCount++;
            _runtimeArrangeContentOffsetPathCount++;
        }
        var contentX = useTransformScrolling ? _contentViewportRect.X : _contentViewportRect.X - HorizontalOffset;
        var contentY = useTransformScrolling ? _contentViewportRect.Y : _contentViewportRect.Y - VerticalOffset;

        var arrangeRect = new LayoutRect(
            contentX,
            contentY,
            arrangedWidth,
            arrangedHeight);

        if (CanReuseExistingContentArrange(content, arrangeRect))
        {
            _diagArrangeContentForCurrentOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _runtimeArrangeContentForCurrentOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        if (CanTranslateExistingContentArrange(content, arrangeRect) &&
            content.TryTranslateArrangedSubtree(arrangeRect))
        {
            _hasArrangedContentRect = true;
            _lastArrangedContentRect = arrangeRect;
            _lastArrangedContentElement = content;
            content.InvalidateVisual();
            _diagArrangeContentForCurrentOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _runtimeArrangeContentForCurrentOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        content.Arrange(arrangeRect);
        _hasArrangedContentRect = true;
        _lastArrangedContentRect = arrangeRect;
        _lastArrangedContentElement = content;
        content.InvalidateVisual();
        _diagArrangeContentForCurrentOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeArrangeContentForCurrentOffsetsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private bool CanReuseExistingContentArrange(FrameworkElement content, LayoutRect nextArrangeRect)
    {
        return _hasArrangedContentRect &&
               ReferenceEquals(_lastArrangedContentElement, content) &&
               !content.NeedsMeasure &&
               !content.NeedsArrange &&
               AreLayoutRectsClose(_lastArrangedContentRect, nextArrangeRect);
    }

    private bool CanTranslateExistingContentArrange(FrameworkElement content, LayoutRect nextArrangeRect)
    {
        return UsesTransformBasedContentScrolling() &&
               _hasArrangedContentRect &&
               ReferenceEquals(_lastArrangedContentElement, content) &&
               AreFloatsClose(_lastArrangedContentRect.Width, nextArrangeRect.Width) &&
               AreFloatsClose(_lastArrangedContentRect.Height, nextArrangeRect.Height) &&
               (!AreFloatsClose(_lastArrangedContentRect.X, nextArrangeRect.X) ||
                !AreFloatsClose(_lastArrangedContentRect.Y, nextArrangeRect.Y));
    }

    private float ResolveContentArrangeWidth(FrameworkElement content, LayoutRect previousViewportRect)
    {
        if (ShouldArrangeVirtualizedContentToViewport(content, horizontalAxis: true))
        {
            return ViewportWidth;
        }

        var arrangedWidth = HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled
            ? ViewportWidth
            : MathF.Max(ViewportWidth, ExtentWidth);

        if (HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled)
        {
            return arrangedWidth;
        }

        return TryPreservePreviousContentArrangeSpan(content, previousViewportRect, horizontalAxis: true, out var preservedWidth)
            ? preservedWidth
            : arrangedWidth;
    }

    private float ResolveContentArrangeHeight(FrameworkElement content, LayoutRect previousViewportRect)
    {
        if (ShouldArrangeVirtualizedContentToViewport(content, horizontalAxis: false))
        {
            return ViewportHeight;
        }

        var arrangedHeight = MathF.Max(ViewportHeight, ExtentHeight);

        if (VerticalScrollBarVisibility != ScrollBarVisibility.Disabled)
        {
            return arrangedHeight;
        }

        return TryPreservePreviousContentArrangeSpan(content, previousViewportRect, horizontalAxis: false, out var preservedHeight)
            ? preservedHeight
            : arrangedHeight;
    }

    private bool TryPreservePreviousContentArrangeSpan(
        FrameworkElement content,
        LayoutRect previousViewportRect,
        bool horizontalAxis,
        out float preservedSpan)
    {
        preservedSpan = 0f;

        if (!_hasArrangedContentRect ||
            !ReferenceEquals(_lastArrangedContentElement, content) ||
            content.NeedsMeasure)
        {
            return false;
        }

        var previousViewportSpan = horizontalAxis ? previousViewportRect.Width : previousViewportRect.Height;
        var nextViewportSpan = horizontalAxis ? _contentViewportRect.Width : _contentViewportRect.Height;
        var previousArrangedSpan = horizontalAxis ? _lastArrangedContentRect.Width : _lastArrangedContentRect.Height;
        if (previousArrangedSpan <= nextViewportSpan + 0.01f ||
            previousViewportSpan <= 0f ||
            nextViewportSpan <= 0f)
        {
            return false;
        }

        var previousAvailable = CreateContentMeasureConstraint(previousViewportRect.Width, previousViewportRect.Height);
        var nextAvailable = CreateContentMeasureConstraint(_contentViewportRect.Width, _contentViewportRect.Height);
        if (!content.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousAvailable, nextAvailable))
        {
            return false;
        }

        preservedSpan = previousArrangedSpan;
        return true;
    }

    private static bool ShouldArrangeVirtualizedContentToViewport(FrameworkElement content, bool horizontalAxis)
    {
        if (content is not VirtualizingStackPanel panel)
        {
            return false;
        }

        return horizontalAxis
            ? panel.Orientation == Orientation.Horizontal
            : panel.Orientation == Orientation.Vertical;
    }

    private Vector2 CreateContentMeasureConstraint(float viewportWidth, float viewportHeight)
    {
        var canScrollHorizontally = HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
        var canScrollVertically = VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
        return new Vector2(
            canScrollHorizontally ? float.PositiveInfinity : MathF.Max(0f, viewportWidth),
            canScrollVertically ? float.PositiveInfinity : MathF.Max(0f, viewportHeight));
    }

    private bool UsesTransformBasedContentScrolling()
    {
        return ContentElement is IScrollTransformContent;
    }

    private void UpdateScrollBars()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeUpdateScrollBarsCallCount++;
        var desiredHorizontalViewportSize = ViewportWidth;
        var desiredVerticalViewportSize = ViewportHeight;
        var desiredHorizontalMaximum = ExtentWidth;
        var desiredVerticalMaximum = ExtentHeight;
        var desiredHorizontalLargeChange = MathF.Max(1f, ViewportWidth);
        var desiredVerticalLargeChange = MathF.Max(1f, ViewportHeight);
        var desiredHorizontalValue = HorizontalOffset;
        var desiredVerticalValue = VerticalOffset;
        if (HasSynchronizedScrollBarState(
                desiredHorizontalViewportSize,
                desiredVerticalViewportSize,
                desiredHorizontalMaximum,
                desiredVerticalMaximum,
                desiredHorizontalLargeChange,
                desiredVerticalLargeChange,
                desiredHorizontalValue,
                desiredVerticalValue))
        {
            _runtimeUpdateScrollBarsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _diagUpdateScrollBarsCallCount++;
            _diagUpdateScrollBarsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        _suppressInternalScrollBarValueChange = true;
        try
        {
            SetIfChanged(ScrollBar.ViewportSizeProperty, _horizontalBar, desiredHorizontalViewportSize);
            SetIfChanged(ScrollBar.ViewportSizeProperty, _verticalBar, desiredVerticalViewportSize);
            SetIfChanged(ScrollBar.MinimumProperty, _horizontalBar, 0f);
            SetIfChanged(ScrollBar.MinimumProperty, _verticalBar, 0f);
            SetIfChanged(ScrollBar.MaximumProperty, _horizontalBar, desiredHorizontalMaximum);
            SetIfChanged(ScrollBar.MaximumProperty, _verticalBar, desiredVerticalMaximum);
            SetIfChanged(ScrollBar.SmallChangeProperty, _horizontalBar, DefaultLineScrollStep);
            SetIfChanged(ScrollBar.SmallChangeProperty, _verticalBar, DefaultLineScrollStep);
            SetIfChanged(ScrollBar.LargeChangeProperty, _horizontalBar, desiredHorizontalLargeChange);
            SetIfChanged(ScrollBar.LargeChangeProperty, _verticalBar, desiredVerticalLargeChange);
            SetIfChanged(ScrollBar.ValueProperty, _horizontalBar, desiredHorizontalValue);
            SetIfChanged(ScrollBar.ValueProperty, _verticalBar, desiredVerticalValue);
            CacheSynchronizedScrollBarState(
                desiredHorizontalViewportSize,
                desiredVerticalViewportSize,
                desiredHorizontalMaximum,
                desiredVerticalMaximum,
                desiredHorizontalLargeChange,
                desiredVerticalLargeChange,
                desiredHorizontalValue,
                desiredVerticalValue);
        }
        finally
        {
                _runtimeUpdateScrollBarsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _suppressInternalScrollBarValueChange = false;
        }
        _diagUpdateScrollBarsCallCount++;
        _diagUpdateScrollBarsElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private float ResolveHorizontalBarThicknessForLayout()
    {
        if (_horizontalBar.GetValueSource(FrameworkElement.HeightProperty) != DependencyPropertyValueSource.Default &&
            float.IsFinite(_horizontalBar.Height) &&
            _horizontalBar.Height > 0f)
        {
            return MathF.Max(8f, _horizontalBar.Height);
        }

        return MathF.Max(8f, ScrollBarThickness);
    }

    private float ResolveVerticalBarThicknessForLayout()
    {
        if (_verticalBar.GetValueSource(FrameworkElement.WidthProperty) != DependencyPropertyValueSource.Default &&
            float.IsFinite(_verticalBar.Width) &&
            _verticalBar.Width > 0f)
        {
            return MathF.Max(8f, _verticalBar.Width);
        }

        return MathF.Max(8f, ScrollBarThickness);
    }

    private void UpdateScrollBarValues()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagUpdateScrollBarValuesCallCount++;
        _runtimeUpdateScrollBarValuesCallCount++;
        _suppressInternalScrollBarValueChange = true;
        try
        {
            SetIfChanged(ScrollBar.ValueProperty, _horizontalBar, HorizontalOffset);
            SetIfChanged(ScrollBar.ValueProperty, _verticalBar, VerticalOffset);
        }
        finally
        {
            _suppressInternalScrollBarValueChange = false;
        }

        _diagUpdateScrollBarValuesElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeUpdateScrollBarValuesElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private void UpdateHorizontalScrollBarValue()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagUpdateHorizontalScrollBarValueCallCount++;
        _runtimeUpdateHorizontalScrollBarValueCallCount++;
        _suppressInternalScrollBarValueChange = true;
        try
        {
            SetIfChanged(ScrollBar.ValueProperty, _horizontalBar, HorizontalOffset);
        }
        finally
        {
            _suppressInternalScrollBarValueChange = false;
        }

        _diagUpdateHorizontalScrollBarValueElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeUpdateHorizontalScrollBarValueElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private void UpdateVerticalScrollBarValue()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagUpdateVerticalScrollBarValueCallCount++;
        _runtimeUpdateVerticalScrollBarValueCallCount++;
        _suppressInternalScrollBarValueChange = true;
        try
        {
            SetIfChanged(ScrollBar.ValueProperty, _verticalBar, VerticalOffset);
        }
        finally
        {
            _suppressInternalScrollBarValueChange = false;
        }

        _diagUpdateVerticalScrollBarValueElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeUpdateVerticalScrollBarValueElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private void OnHorizontalScrollBarValueChanged(object? sender, RoutedSimpleEventArgs args)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _ = sender;
        _ = args;
        if (_suppressInternalScrollBarValueChange)
        {
            _diagHorizontalValueChangedSuppressedCount++;
            _runtimeHorizontalValueChangedSuppressedCount++;
            _diagHorizontalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _runtimeHorizontalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        var setOffsetsStartTicks = Stopwatch.GetTimestamp();
        BeginInputScrollMutation();
        try
        {
            SetOffsets(_horizontalBar.Value, VerticalOffset, ScrollOffsetUpdateSource.HorizontalScrollBar);
        }
        finally
        {
            EndInputScrollMutation();
        }

        _diagHorizontalValueChangedSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - setOffsetsStartTicks;
        _runtimeHorizontalValueChangedSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - setOffsetsStartTicks;
        _diagHorizontalValueChangedCallCount++;
        _runtimeHorizontalValueChangedCallCount++;
        _diagHorizontalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeHorizontalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private void OnVerticalScrollBarValueChanged(object? sender, RoutedSimpleEventArgs args)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _ = sender;
        _ = args;
        if (_suppressInternalScrollBarValueChange)
        {
            _diagVerticalValueChangedSuppressedCount++;
            _runtimeVerticalValueChangedSuppressedCount++;
            _diagVerticalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _runtimeVerticalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        var setOffsetsStartTicks = Stopwatch.GetTimestamp();
        BeginInputScrollMutation();
        try
        {
            SetOffsets(HorizontalOffset, _verticalBar.Value, ScrollOffsetUpdateSource.VerticalScrollBar);
        }
        finally
        {
            EndInputScrollMutation();
        }
        _diagVerticalValueChangedSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - setOffsetsStartTicks;
        _runtimeVerticalValueChangedSetOffsetsElapsedTicks += Stopwatch.GetTimestamp() - setOffsetsStartTicks;
        _diagVerticalValueChangedCallCount++;
        _runtimeVerticalValueChangedCallCount++;
        _diagVerticalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _runtimeVerticalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    private void SetIfChanged(DependencyProperty property, float value, string? diagnosticsCounterName = null)
    {
        if (AreClose(GetValue<float>(property), value))
        {
            return;
        }

        SetValue(property, value);
    }

    private static void SetIfChanged(DependencyProperty property, ScrollBar scrollBar, float value)
    {
        if (AreClose(scrollBar.GetValue<float>(property), value))
        {
            return;
        }

        scrollBar.SetValue(property, value);
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }

    private bool HasSynchronizedScrollBarState(
        float horizontalViewportSize,
        float verticalViewportSize,
        float horizontalMaximum,
        float verticalMaximum,
        float horizontalLargeChange,
        float verticalLargeChange,
        float horizontalValue,
        float verticalValue)
    {
        return _hasSyncedScrollBarState &&
               AreClose(_lastSyncedHorizontalViewportSize, horizontalViewportSize) &&
               AreClose(_lastSyncedVerticalViewportSize, verticalViewportSize) &&
               AreClose(_lastSyncedHorizontalMaximum, horizontalMaximum) &&
               AreClose(_lastSyncedVerticalMaximum, verticalMaximum) &&
               AreClose(_lastSyncedHorizontalLargeChange, horizontalLargeChange) &&
               AreClose(_lastSyncedVerticalLargeChange, verticalLargeChange) &&
               AreClose(_lastSyncedHorizontalValue, horizontalValue) &&
               AreClose(_lastSyncedVerticalValue, verticalValue);
    }

    private void CacheSynchronizedScrollBarState(
        float horizontalViewportSize,
        float verticalViewportSize,
        float horizontalMaximum,
        float verticalMaximum,
        float horizontalLargeChange,
        float verticalLargeChange,
        float horizontalValue,
        float verticalValue)
    {
        _hasSyncedScrollBarState = true;
        _lastSyncedHorizontalViewportSize = horizontalViewportSize;
        _lastSyncedVerticalViewportSize = verticalViewportSize;
        _lastSyncedHorizontalMaximum = horizontalMaximum;
        _lastSyncedVerticalMaximum = verticalMaximum;
        _lastSyncedHorizontalLargeChange = horizontalLargeChange;
        _lastSyncedVerticalLargeChange = verticalLargeChange;
        _lastSyncedHorizontalValue = horizontalValue;
        _lastSyncedVerticalValue = verticalValue;
    }

    private static float CoerceViewportMetric(float candidate, float previous, float extent)
    {
        if (float.IsFinite(candidate) && candidate >= 0f)
        {
            return candidate;
        }

        if (float.IsFinite(previous) && previous >= 0f)
        {
            return previous;
        }

        if (float.IsFinite(extent) && extent >= 0f)
        {
            return extent;
        }

        return 0f;
    }

    private static float CoerceNonNegativeFinite(float candidate, float previous)
    {
        if (float.IsFinite(candidate) && candidate >= 0f)
        {
            return candidate;
        }

        if (float.IsFinite(previous) && previous >= 0f)
        {
            return previous;
        }

        return 0f;
    }

    private static float GetVerticalBarReservation(bool showVerticalBar, float barSize)
    {
        if (!showVerticalBar)
        {
            return 0f;
        }

        return barSize + ContentScrollBarGap;
    }

    private static float GetHorizontalBarReservation(bool showHorizontalBar, float barSize)
    {
        if (!showHorizontalBar)
        {
            return 0f;
        }

        return barSize + ContentScrollBarGap;
    }

}



