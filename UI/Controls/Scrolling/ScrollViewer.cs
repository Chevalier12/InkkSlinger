using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class ScrollViewer : ContentControl
{
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
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(
            nameof(VerticalOffset),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

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
    private bool _isDraggingHorizontalBar;
    private bool _isDraggingVerticalBar;
    private float _horizontalBarDragOffset;
    private float _verticalBarDragOffset;
    private int _inputScrollMutationDepth;
    private static int _diagWheelEvents;
    private static int _diagWheelHandled;
    private static int _diagSetOffsetCalls;
    private static int _diagSetOffsetNoOp;
    private static float _diagVerticalDelta;
    private static float _diagHorizontalDelta;

    public ScrollViewer()
    {
        _horizontalBar = new ScrollBar { Orientation = Orientation.Horizontal };
        _verticalBar = new ScrollBar { Orientation = Orientation.Vertical };

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

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (_showVerticalBar &&
            _verticalBar.TryHandlePointerDown(pointerPosition, out var verticalValue, out var startVerticalDrag, out var verticalDragOffset))
        {
            RunWithinInputScrollMutation(() => SetOffsets(HorizontalOffset, verticalValue));
            _isDraggingVerticalBar = startVerticalDrag;
            _verticalBarDragOffset = startVerticalDrag ? verticalDragOffset : 0f;
            _isDraggingHorizontalBar = false;
            _horizontalBarDragOffset = 0f;
            return true;
        }

        if (_showHorizontalBar &&
            _horizontalBar.TryHandlePointerDown(pointerPosition, out var horizontalValue, out var startHorizontalDrag, out var horizontalDragOffset))
        {
            RunWithinInputScrollMutation(() => SetOffsets(horizontalValue, VerticalOffset));
            _isDraggingHorizontalBar = startHorizontalDrag;
            _horizontalBarDragOffset = startHorizontalDrag ? horizontalDragOffset : 0f;
            _isDraggingVerticalBar = false;
            _verticalBarDragOffset = 0f;
            return true;
        }

        return false;
    }

    internal bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        var handled = false;
        if (_isDraggingVerticalBar)
        {
            var value = _verticalBar.GetValueFromDragPointer(pointerPosition, _verticalBarDragOffset);
            var before = VerticalOffset;
            RunWithinInputScrollMutation(() => SetOffsets(HorizontalOffset, value));
            handled = handled || MathF.Abs(before - VerticalOffset) > 0.001f;
        }

        if (_isDraggingHorizontalBar)
        {
            var value = _horizontalBar.GetValueFromDragPointer(pointerPosition, _horizontalBarDragOffset);
            var before = HorizontalOffset;
            RunWithinInputScrollMutation(() => SetOffsets(value, VerticalOffset));
            handled = handled || MathF.Abs(before - HorizontalOffset) > 0.001f;
        }

        return handled;
    }

    internal bool HandlePointerUpFromInput()
    {
        if (!_isDraggingHorizontalBar && !_isDraggingVerticalBar)
        {
            return false;
        }

        _isDraggingHorizontalBar = false;
        _isDraggingVerticalBar = false;
        _horizontalBarDragOffset = 0f;
        _verticalBarDragOffset = 0f;
        return true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
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
        return new Vector2(MathF.Max(0f, desiredWidth), MathF.Max(0f, desiredHeight));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var border = MathF.Max(0f, BorderThickness);
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var fullRect = new LayoutRect(LayoutSlot.X + border, LayoutSlot.Y + border, MathF.Max(0f, finalSize.X - (border * 2f)), MathF.Max(0f, finalSize.Y - (border * 2f)));
        var decision = ResolveBarsForArrange(fullRect);
        ApplyScrollMetrics(decision.ExtentWidth, decision.ExtentHeight, decision.ViewportWidth, decision.ViewportHeight, publishViewportMetrics: true);
        SetOffsets(HorizontalOffset, VerticalOffset);
        _showHorizontalBar = decision.ShowHorizontalBar;
        _showVerticalBar = decision.ShowVerticalBar;
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
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var showHorizontal = HorizontalScrollBarVisibility == ScrollBarVisibility.Visible;
        var showVertical = VerticalScrollBarVisibility == ScrollBarVisibility.Visible;
        var preferSinglePass = ShouldPreferSinglePassAutoBarMeasure();

        for (var i = 0; i < 2; i++)
        {
            var viewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
            var viewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));

            MeasureContent(viewportWidth, viewportHeight, out var extentWidth, out var extentHeight);

            if (HorizontalScrollBarVisibility == ScrollBarVisibility.Auto)
            {
                showHorizontal = extentWidth > viewportWidth + 0.01f;
            }

            if (VerticalScrollBarVisibility == ScrollBarVisibility.Auto)
            {
                showVertical = extentHeight > viewportHeight + 0.01f;
            }

            if (preferSinglePass)
            {
                return (
                    showHorizontal,
                    showVertical,
                    extentWidth,
                    extentHeight,
                    viewportWidth,
                    viewportHeight,
                    new LayoutRect(bounds.X, bounds.Y, viewportWidth, viewportHeight));
            }
        }

        var finalViewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
        var finalViewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));
        MeasureContent(finalViewportWidth, finalViewportHeight, out var finalExtentWidth, out var finalExtentHeight);

        return (
            showHorizontal,
            showVertical,
            finalExtentWidth,
            finalExtentHeight,
            finalViewportWidth,
            finalViewportHeight,
            new LayoutRect(bounds.X, bounds.Y, finalViewportWidth, finalViewportHeight));
    }

    private (bool ShowHorizontalBar, bool ShowVerticalBar, float ExtentWidth, float ExtentHeight, float ViewportWidth, float ViewportHeight, LayoutRect ViewportRect)
        ResolveBarsForArrange(LayoutRect bounds)
    {
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var extentWidth = MathF.Max(0f, ExtentWidth);
        var extentHeight = MathF.Max(0f, ExtentHeight);
        var showHorizontal = HorizontalScrollBarVisibility == ScrollBarVisibility.Visible;
        var showVertical = VerticalScrollBarVisibility == ScrollBarVisibility.Visible;

        for (var i = 0; i < 2; i++)
        {
            var viewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
            var viewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));

            if (HorizontalScrollBarVisibility == ScrollBarVisibility.Auto)
            {
                showHorizontal = extentWidth > viewportWidth + 0.01f;
            }

            if (VerticalScrollBarVisibility == ScrollBarVisibility.Auto)
            {
                showVertical = extentHeight > viewportHeight + 0.01f;
            }
        }

        var finalViewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
        var finalViewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));

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
        RunWithinInputScrollMutation(() => SetOffsets(HorizontalOffset, VerticalOffset + (direction * amount)));

        var handled = MathF.Abs(beforeHorizontal - HorizontalOffset) > 0.001f ||
                      MathF.Abs(beforeVertical - VerticalOffset) > 0.001f;
        if (handled)
        {
            _diagWheelHandled++;
        }

        return handled;
    }

    private void SetOffsets(float horizontal, float vertical)
    {
        _diagSetOffsetCalls++;
        var beforeHorizontal = HorizontalOffset;
        var beforeVertical = VerticalOffset;
        var maxHorizontal = MathF.Max(0f, ExtentWidth - ViewportWidth);
        var maxVertical = MathF.Max(0f, ExtentHeight - ViewportHeight);
        var nextHorizontal = MathF.Max(0f, MathF.Min(maxHorizontal, horizontal));
        var nextVertical = MathF.Max(0f, MathF.Min(maxVertical, vertical));

        SetIfChanged(HorizontalOffsetProperty, nextHorizontal);
        SetIfChanged(VerticalOffsetProperty, nextVertical);
        var horizontalDelta = MathF.Abs(beforeHorizontal - HorizontalOffset);
        var verticalDelta = MathF.Abs(beforeVertical - VerticalOffset);

        _diagHorizontalDelta += horizontalDelta;
        _diagVerticalDelta += verticalDelta;
        if (horizontalDelta <= 0.001f && verticalDelta <= 0.001f)
        {
            _diagSetOffsetNoOp++;
        }

        if ((horizontalDelta > 0.001f || verticalDelta > 0.001f) &&
            !NeedsMeasure &&
            !NeedsArrange)
        {
            if (ContentElement is VirtualizingStackPanel)
            {
                InvalidateMeasure();
                InvalidateArrange();
            }
            else if (UsesTransformBasedContentScrolling())
            {
                if (ContentElement is UIElement contentElement)
                {
                    if (contentElement.NeedsRender)
                    {
                        UiRoot.Current?.NotifyInvalidation(UiInvalidationType.Render, contentElement);
                    }
                    else
                    {
                        contentElement.InvalidateVisual();
                    }
                }
            }
            else
            {
                ArrangeContentForCurrentOffsets();
                InvalidateVisual();
            }
        }

        UpdateScrollBarValues();
        if (_inputScrollMutationDepth > 0 &&
            horizontalDelta <= 0.001f &&
            verticalDelta <= 0.001f)
        {
        }
    }

    private void RunWithinInputScrollMutation(Action action)
    {
        _inputScrollMutationDepth++;
        try
        {
            action();
        }
        finally
        {
            _inputScrollMutationDepth--;
        }
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

    private bool ShouldPreferSinglePassAutoBarMeasure()
    {
        if (ContentElement is not VirtualizingStackPanel)
        {
            return false;
        }

        return HorizontalScrollBarVisibility == ScrollBarVisibility.Auto ||
               VerticalScrollBarVisibility == ScrollBarVisibility.Auto;
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
        SetIfChanged(ScrollBar.ViewportSizeProperty, _horizontalBar, ViewportWidth);
        SetIfChanged(ScrollBar.ViewportSizeProperty, _verticalBar, ViewportHeight);
        SetIfChanged(ScrollBar.MinimumProperty, _horizontalBar, 0f);
        SetIfChanged(ScrollBar.MinimumProperty, _verticalBar, 0f);
        SetIfChanged(ScrollBar.MaximumProperty, _horizontalBar, ExtentWidth);
        SetIfChanged(ScrollBar.MaximumProperty, _verticalBar, ExtentHeight);
        SetIfChanged(ScrollBar.ValueProperty, _horizontalBar, HorizontalOffset);
        SetIfChanged(ScrollBar.ValueProperty, _verticalBar, VerticalOffset);
    }

    private float ResolveHorizontalBarThicknessForLayout()
    {
        if (_horizontalBar.GetValueSource(FrameworkElement.HeightProperty) != DependencyPropertyValueSource.Default &&
            float.IsFinite(_horizontalBar.Height) &&
            _horizontalBar.Height > 0f)
        {
            return MathF.Max(8f, _horizontalBar.Height);
        }

        if (_horizontalBar.GetValueSource(ScrollBar.ThicknessProperty) != DependencyPropertyValueSource.Default)
        {
            return MathF.Max(8f, _horizontalBar.Thickness);
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

        if (_verticalBar.GetValueSource(ScrollBar.ThicknessProperty) != DependencyPropertyValueSource.Default)
        {
            return MathF.Max(8f, _verticalBar.Thickness);
        }

        return MathF.Max(8f, ScrollBarThickness);
    }

    private void UpdateScrollBarValues()
    {
        SetIfChanged(ScrollBar.ValueProperty, _horizontalBar, HorizontalOffset);
        SetIfChanged(ScrollBar.ValueProperty, _verticalBar, VerticalOffset);
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

