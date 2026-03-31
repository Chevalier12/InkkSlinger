using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    public static readonly DependencyProperty LineScrollAmountProperty =
        DependencyProperty.Register(
            nameof(LineScrollAmount),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(24f));

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
    private bool _showHorizontalBar;
    private bool _showVerticalBar;
    private bool _hasPreviousScrollBarResolution;
    private bool _previousShowHorizontalScrollBar;
    private bool _previousShowVerticalScrollBar;
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
    private static int _diagWheelEvents;
    private static int _diagWheelHandled;
    private static int _diagSetOffsetCalls;
    private static int _diagSetOffsetNoOp;
    private static float _diagVerticalDelta;
    private static float _diagHorizontalDelta;
    private static int _diagVerticalValueChangedCallCount;
    private static long _diagVerticalValueChangedElapsedTicks;
    private static long _diagVerticalValueChangedSetOffsetsElapsedTicks;
    private static int _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static int _diagArrangeOverrideCallCount;
    private static long _diagArrangeOverrideElapsedTicks;
    private static int _diagResolveBarsAndMeasureContentCallCount;
    private static long _diagResolveBarsAndMeasureContentElapsedTicks;
    private static int _diagResolveBarsAndMeasureContentIterationCount;
    private static int _diagResolveBarsAndMeasureContentHorizontalFlipCount;
    private static int _diagResolveBarsAndMeasureContentVerticalFlipCount;
    private static int _diagResolveBarsForArrangeCallCount;
    private static long _diagResolveBarsForArrangeElapsedTicks;
    private static int _diagResolveBarsForArrangeIterationCount;
    private static int _diagResolveBarsForArrangeHorizontalFlipCount;
    private static int _diagResolveBarsForArrangeVerticalFlipCount;
    private static int _diagMeasureContentCallCount;
    private static long _diagMeasureContentElapsedTicks;
    private static int _diagUpdateScrollBarsCallCount;
    private static long _diagUpdateScrollBarsElapsedTicks;

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

    public float LineScrollAmount
    {
        get => GetValue<float>(LineScrollAmountProperty);
        set => SetValue(LineScrollAmountProperty, value);
    }

    public override void InvalidateMeasure()
    {
        base.InvalidateMeasure();
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
        SetOffsets(offset, VerticalOffset);
    }

    public void ScrollToVerticalOffset(float offset)
    {
        SetOffsets(HorizontalOffset, offset);
    }

    public void InvalidateScrollInfo()
    {
        InvalidateMeasure();
    }

    internal bool TryGetContentViewportClipRect(out LayoutRect clipRect)
    {
        clipRect = _contentViewportRect;
        return clipRect.Width > 0f && clipRect.Height > 0f;
    }

    internal static ScrollViewerScrollMetricsSnapshot GetScrollMetricsAndReset()
    {
        var snapshot = new ScrollViewerScrollMetricsSnapshot(
            _diagWheelEvents,
            _diagWheelHandled,
            _diagSetOffsetCalls,
            _diagSetOffsetNoOp,
            _diagHorizontalDelta,
            _diagVerticalDelta);
        _diagWheelEvents = 0;
        _diagWheelHandled = 0;
        _diagSetOffsetCalls = 0;
        _diagSetOffsetNoOp = 0;
        _diagHorizontalDelta = 0f;
        _diagVerticalDelta = 0f;
        return snapshot;
    }

    internal static ScrollViewerValueChangedTelemetrySnapshot GetValueChangedTelemetryAndReset()
    {
        var snapshot = new ScrollViewerValueChangedTelemetrySnapshot(
            _diagVerticalValueChangedCallCount,
            (double)_diagVerticalValueChangedElapsedTicks * 1000d / Stopwatch.Frequency,
            (double)_diagVerticalValueChangedSetOffsetsElapsedTicks * 1000d / Stopwatch.Frequency);
        _diagVerticalValueChangedCallCount = 0;
        _diagVerticalValueChangedElapsedTicks = 0L;
        _diagVerticalValueChangedSetOffsetsElapsedTicks = 0L;
        return snapshot;
    }

    internal static ScrollViewerLayoutTelemetrySnapshot GetLayoutTelemetryAndReset()
    {
        var snapshot = new ScrollViewerLayoutTelemetrySnapshot(
            _diagMeasureOverrideCallCount,
            (double)_diagMeasureOverrideElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagArrangeOverrideCallCount,
            (double)_diagArrangeOverrideElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagResolveBarsAndMeasureContentCallCount,
            (double)_diagResolveBarsAndMeasureContentElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagResolveBarsAndMeasureContentIterationCount,
            _diagResolveBarsAndMeasureContentHorizontalFlipCount,
            _diagResolveBarsAndMeasureContentVerticalFlipCount,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            "n/a",
            "n/a",
            _diagResolveBarsForArrangeCallCount,
            (double)_diagResolveBarsForArrangeElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagResolveBarsForArrangeIterationCount,
            _diagResolveBarsForArrangeHorizontalFlipCount,
            _diagResolveBarsForArrangeVerticalFlipCount,
            _diagMeasureContentCallCount,
            (double)_diagMeasureContentElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagUpdateScrollBarsCallCount,
            (double)_diagUpdateScrollBarsElapsedTicks * 1000d / Stopwatch.Frequency);
        _diagMeasureOverrideCallCount = 0;
        _diagMeasureOverrideElapsedTicks = 0L;
        _diagArrangeOverrideCallCount = 0;
        _diagArrangeOverrideElapsedTicks = 0L;
        _diagResolveBarsAndMeasureContentCallCount = 0;
        _diagResolveBarsAndMeasureContentElapsedTicks = 0L;
        _diagResolveBarsAndMeasureContentIterationCount = 0;
        _diagResolveBarsAndMeasureContentHorizontalFlipCount = 0;
        _diagResolveBarsAndMeasureContentVerticalFlipCount = 0;
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

    internal ScrollViewerLayoutTelemetrySnapshot GetRuntimeLayoutTelemetryForDiagnostics()
    {
        return new ScrollViewerLayoutTelemetrySnapshot(
            _runtimeMeasureOverrideCallCount,
            (double)_runtimeMeasureOverrideElapsedTicks * 1000d / Stopwatch.Frequency,
            _runtimeArrangeOverrideCallCount,
            (double)_runtimeArrangeOverrideElapsedTicks * 1000d / Stopwatch.Frequency,
            _runtimeResolveBarsAndMeasureContentCallCount,
            (double)_runtimeResolveBarsAndMeasureContentElapsedTicks * 1000d / Stopwatch.Frequency,
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
            _runtimeResolveBarsAndMeasureContentLastTrace,
            _runtimeResolveBarsAndMeasureContentHottestTrace,
            _runtimeResolveBarsForArrangeCallCount,
            (double)_runtimeResolveBarsForArrangeElapsedTicks * 1000d / Stopwatch.Frequency,
            _runtimeResolveBarsForArrangeIterationCount,
            _runtimeResolveBarsForArrangeHorizontalFlipCount,
            _runtimeResolveBarsForArrangeVerticalFlipCount,
            _runtimeMeasureContentCallCount,
            (double)_runtimeMeasureContentElapsedTicks * 1000d / Stopwatch.Frequency,
            _runtimeUpdateScrollBarsCallCount,
            (double)_runtimeUpdateScrollBarsElapsedTicks * 1000d / Stopwatch.Frequency);
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
            ? decision.ViewportRect.Width
            : decision.ExtentWidth;
        var desiredViewportHeight = float.IsFinite(contentBounds.Height)
            ? decision.ViewportRect.Height
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
        var border = MathF.Max(0f, BorderThickness);
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var fullRect = new LayoutRect(LayoutSlot.X + border, LayoutSlot.Y + border, MathF.Max(0f, finalSize.X - (border * 2f)), MathF.Max(0f, finalSize.Y - (border * 2f)));
        var decision = ResolveBarsForArrange(fullRect);
        ApplyScrollMetrics(decision.ExtentWidth, decision.ExtentHeight, decision.ViewportWidth, decision.ViewportHeight, publishViewportMetrics: true);
        SetOffsets(HorizontalOffset, VerticalOffset);
        _showHorizontalBar = decision.ShowHorizontalBar;
        _showVerticalBar = decision.ShowVerticalBar;
        CacheResolvedScrollBarVisibility(decision.ShowHorizontalBar, decision.ShowVerticalBar);
        _contentViewportRect = decision.ViewportRect;
        ArrangeContentForCurrentOffsets();

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
            _runtimeResolveBarsAndMeasureContentInitialHorizontalVisibleCount++;
        }
        else
        {
            _runtimeResolveBarsAndMeasureContentInitialHorizontalHiddenCount++;
        }

        if (showVertical)
        {
            _runtimeResolveBarsAndMeasureContentInitialVerticalVisibleCount++;
        }
        else
        {
            _runtimeResolveBarsAndMeasureContentInitialVerticalHiddenCount++;
        }

        if (canResolveAutoBarsWithoutRemeasure)
        {
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
                    MeasureContent(viewportWidth, viewportHeight, out extentWidth, out extentHeight);
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

        _runtimeResolveBarsAndMeasureContentRemeasurePathCount++;
        for (var i = 0; i < 3; i++)
        {
            _diagResolveBarsAndMeasureContentIterationCount++;
            _runtimeResolveBarsAndMeasureContentIterationCount++;
            var viewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
            var viewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));

            MeasureContent(viewportWidth, viewportHeight, out var extentWidth, out var extentHeight);
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
                MeasureContent(fallbackViewportWidth, fallbackViewportHeight, out var fallbackExtentWidth, out var fallbackExtentHeight);
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
        MeasureContent(finalViewportWidth, finalViewportHeight, out var finalExtentWidth, out var finalExtentHeight);
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
            _runtimeResolveBarsAndMeasureContentResolvedHorizontalVisibleCount++;
        }
        else
        {
            _runtimeResolveBarsAndMeasureContentResolvedHorizontalHiddenCount++;
        }

        if (showVertical)
        {
            _runtimeResolveBarsAndMeasureContentResolvedVerticalVisibleCount++;
        }
        else
        {
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

    private bool CanResolveAutoBarsWithoutRemeasure()
    {
        var horizontalCanUseSingleMeasure = HorizontalScrollBarVisibility != ScrollBarVisibility.Auto ||
                                            VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
        var verticalCanUseSingleMeasure = VerticalScrollBarVisibility != ScrollBarVisibility.Auto ||
                                          HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
        return horizontalCanUseSingleMeasure && verticalCanUseSingleMeasure;
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
        _hasPreviousScrollBarResolution = true;
        _previousShowHorizontalScrollBar = showHorizontalBar;
        _previousShowVerticalScrollBar = showVerticalBar;
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
    }

    internal bool HandleMouseWheelFromInput(int delta)
    {
        _diagWheelEvents++;
        if (!IsEnabled || delta == 0)
        {
            return false;
        }

        var beforeHorizontal = HorizontalOffset;
        var beforeVertical = VerticalOffset;
        var amount = MathF.Max(1f, LineScrollAmount);
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
        }

        return handled;
    }

    private void SetOffsets(float horizontal, float vertical, ScrollOffsetUpdateSource updateSource = ScrollOffsetUpdateSource.External)
    {
        _diagSetOffsetCalls++;
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
        if (horizontalDelta <= 0.001f && verticalDelta <= 0.001f)
        {
            _diagSetOffsetNoOp++;
            if (updateSource == ScrollOffsetUpdateSource.External)
            {
                UpdateScrollBarValues();
            }

            return;
        }

        SetIfChanged(HorizontalOffsetProperty, nextHorizontal);
        SetIfChanged(VerticalOffsetProperty, nextVertical);

        if (!NeedsMeasure &&
            !NeedsArrange)
        {
            if (ContentElement is VirtualizingStackPanel virtualizingStackPanel)
            {
                if (virtualizingStackPanel.RequiresMeasureForViewerOwnedOffsetChange(beforeHorizontal, nextHorizontal, beforeVertical, nextVertical))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
                else
                {
                    InvalidateArrange();
                }
            }
            else if (UsesTransformBasedContentScrolling())
            {
                if (ContentElement is UIElement contentElement)
                {
                    UiRoot.Current?.NotifyDirectRenderInvalidation(contentElement);
                }
            }
            else
            {
                ArrangeContentForCurrentOffsets();
                InvalidateVisual();
            }
        }

        if (ContentElement is UIElement transformScrollContent &&
            UsesTransformBasedContentScrolling() &&
            ContentElement is not VirtualizingStackPanel)
        {
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
    }

    private void BeginInputScrollMutation()
    {
        _inputScrollMutationDepth++;
    }

    private void EndInputScrollMutation()
    {
        _inputScrollMutationDepth--;
    }

    private void ArrangeContentForCurrentOffsets()
    {
        if (ContentElement is not FrameworkElement content)
        {
            return;
        }

        if (_contentViewportRect.Width <= 0f || _contentViewportRect.Height <= 0f)
        {
            return;
        }

        var arrangedWidth = HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled
            ? ViewportWidth
            : MathF.Max(ViewportWidth, ExtentWidth);
        var arrangedHeight = MathF.Max(ViewportHeight, ExtentHeight);
        var useTransformScrolling = UsesTransformBasedContentScrolling();
        var contentX = useTransformScrolling ? _contentViewportRect.X : _contentViewportRect.X - HorizontalOffset;
        var contentY = useTransformScrolling ? _contentViewportRect.Y : _contentViewportRect.Y - VerticalOffset;

        content.Arrange(new LayoutRect(
            contentX,
            contentY,
            arrangedWidth,
            arrangedHeight));
        content.InvalidateVisual();
    }

    private bool UsesTransformBasedContentScrolling()
    {
        if (ContentElement is IScrollTransformContent)
        {
            return true;
        }

        return ContentElement is Panel panel &&
               panel is not VirtualizingStackPanel &&
               GetUseTransformContentScrolling(panel);
    }

    private void UpdateScrollBars()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeUpdateScrollBarsCallCount++;
        _suppressInternalScrollBarValueChange = true;
        try
        {
            SetIfChanged(ScrollBar.ViewportSizeProperty, _horizontalBar, ViewportWidth);
            SetIfChanged(ScrollBar.ViewportSizeProperty, _verticalBar, ViewportHeight);
            SetIfChanged(ScrollBar.MinimumProperty, _horizontalBar, 0f);
            SetIfChanged(ScrollBar.MinimumProperty, _verticalBar, 0f);
            SetIfChanged(ScrollBar.MaximumProperty, _horizontalBar, ExtentWidth);
            SetIfChanged(ScrollBar.MaximumProperty, _verticalBar, ExtentHeight);
            SetIfChanged(ScrollBar.SmallChangeProperty, _horizontalBar, MathF.Max(1f, LineScrollAmount));
            SetIfChanged(ScrollBar.SmallChangeProperty, _verticalBar, MathF.Max(1f, LineScrollAmount));
            SetIfChanged(ScrollBar.LargeChangeProperty, _horizontalBar, MathF.Max(1f, ViewportWidth));
            SetIfChanged(ScrollBar.LargeChangeProperty, _verticalBar, MathF.Max(1f, ViewportHeight));
            SetIfChanged(ScrollBar.ValueProperty, _horizontalBar, HorizontalOffset);
            SetIfChanged(ScrollBar.ValueProperty, _verticalBar, VerticalOffset);
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
    }

    private void UpdateHorizontalScrollBarValue()
    {
        _suppressInternalScrollBarValueChange = true;
        try
        {
            SetIfChanged(ScrollBar.ValueProperty, _horizontalBar, HorizontalOffset);
        }
        finally
        {
            _suppressInternalScrollBarValueChange = false;
        }
    }

    private void UpdateVerticalScrollBarValue()
    {
        _suppressInternalScrollBarValueChange = true;
        try
        {
            SetIfChanged(ScrollBar.ValueProperty, _verticalBar, VerticalOffset);
        }
        finally
        {
            _suppressInternalScrollBarValueChange = false;
        }
    }

    private void OnHorizontalScrollBarValueChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_suppressInternalScrollBarValueChange)
        {
            return;
        }

        BeginInputScrollMutation();
        try
        {
            SetOffsets(_horizontalBar.Value, VerticalOffset, ScrollOffsetUpdateSource.HorizontalScrollBar);
        }
        finally
        {
            EndInputScrollMutation();
        }
    }

    private void OnVerticalScrollBarValueChanged(object? sender, RoutedSimpleEventArgs args)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _ = sender;
        _ = args;
        if (_suppressInternalScrollBarValueChange)
        {
            _diagVerticalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
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
        _diagVerticalValueChangedCallCount++;
        _diagVerticalValueChangedElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
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

public readonly record struct ScrollViewerScrollMetricsSnapshot(
    int WheelEvents,
    int WheelHandled,
    int SetOffsetCalls,
    int SetOffsetNoOpCalls,
    float TotalHorizontalDelta,
    float TotalVerticalDelta);

internal readonly record struct ScrollViewerValueChangedTelemetrySnapshot(
    int VerticalValueChangedCallCount,
    double VerticalValueChangedMilliseconds,
    double VerticalValueChangedSetOffsetsMilliseconds);

internal readonly record struct ScrollViewerLayoutTelemetrySnapshot(
    int MeasureOverrideCallCount,
    double MeasureOverrideMilliseconds,
    int ArrangeOverrideCallCount,
    double ArrangeOverrideMilliseconds,
    int ResolveBarsAndMeasureContentCallCount,
    double ResolveBarsAndMeasureContentMilliseconds,
    int ResolveBarsAndMeasureContentIterationCount,
    int ResolveBarsAndMeasureContentHorizontalFlipCount,
    int ResolveBarsAndMeasureContentVerticalFlipCount,
    int ResolveBarsAndMeasureContentSingleMeasurePathCount,
    int ResolveBarsAndMeasureContentRemeasurePathCount,
    int ResolveBarsAndMeasureContentFallbackCount,
    int ResolveBarsAndMeasureContentInitialHorizontalVisibleCount,
    int ResolveBarsAndMeasureContentInitialHorizontalHiddenCount,
    int ResolveBarsAndMeasureContentInitialVerticalVisibleCount,
    int ResolveBarsAndMeasureContentInitialVerticalHiddenCount,
    int ResolveBarsAndMeasureContentResolvedHorizontalVisibleCount,
    int ResolveBarsAndMeasureContentResolvedHorizontalHiddenCount,
    int ResolveBarsAndMeasureContentResolvedVerticalVisibleCount,
    int ResolveBarsAndMeasureContentResolvedVerticalHiddenCount,
    string ResolveBarsAndMeasureContentLastTrace,
    string ResolveBarsAndMeasureContentHottestTrace,
    int ResolveBarsForArrangeCallCount,
    double ResolveBarsForArrangeMilliseconds,
    int ResolveBarsForArrangeIterationCount,
    int ResolveBarsForArrangeHorizontalFlipCount,
    int ResolveBarsForArrangeVerticalFlipCount,
    int MeasureContentCallCount,
    double MeasureContentMilliseconds,
    int UpdateScrollBarsCallCount,
    double UpdateScrollBarsMilliseconds);

