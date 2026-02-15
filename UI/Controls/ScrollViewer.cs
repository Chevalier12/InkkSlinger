using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class ScrollViewer : ContentControl
{
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
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(
            nameof(VerticalOffset),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

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

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(new Color(20, 20, 20), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(new Color(78, 78, 78), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(1f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly ScrollBar _horizontalBar;
    private readonly ScrollBar _verticalBar;
    private IScrollInfo? _scrollInfo;
    private LayoutRect _contentViewportRect;
    private bool _showHorizontalBar;
    private bool _showVerticalBar;

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

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
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
        InvalidateArrange();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        AttachScrollInfoIfNeeded();

        var border = MathF.Max(0f, BorderThickness);
        var contentBounds = new LayoutRect(
            LayoutSlot.X + border,
            LayoutSlot.Y + border,
            MathF.Max(0f, availableSize.X - (border * 2f)),
            MathF.Max(0f, availableSize.Y - (border * 2f)));

        var decision = ResolveBarsAndMeasureContent(contentBounds);
        ApplyScrollMetrics(decision.ExtentWidth, decision.ExtentHeight, decision.ViewportWidth, decision.ViewportHeight);
        _showHorizontalBar = decision.ShowHorizontalBar;
        _showVerticalBar = decision.ShowVerticalBar;
        _contentViewportRect = decision.ViewportRect;

        _horizontalBar.Thickness = MathF.Max(8f, ScrollBarThickness);
        _verticalBar.Thickness = MathF.Max(8f, ScrollBarThickness);
        _horizontalBar.ViewportSize = ViewportWidth;
        _verticalBar.ViewportSize = ViewportHeight;
        _horizontalBar.Minimum = 0f;
        _verticalBar.Minimum = 0f;
        _horizontalBar.Maximum = ExtentWidth;
        _verticalBar.Maximum = ExtentHeight;
        _horizontalBar.Value = HorizontalOffset;
        _verticalBar.Value = VerticalOffset;
        _horizontalBar.IsVisible = _showHorizontalBar;
        _verticalBar.IsVisible = _showVerticalBar;

        var desiredWidth = decision.ViewportRect.Width + (border * 2f) + (_showVerticalBar ? ScrollBarThickness : 0f);
        var desiredHeight = decision.ViewportRect.Height + (border * 2f) + (_showHorizontalBar ? ScrollBarThickness : 0f);
        return new Vector2(MathF.Max(0f, desiredWidth), MathF.Max(0f, desiredHeight));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        AttachScrollInfoIfNeeded();

        var border = MathF.Max(0f, BorderThickness);
        var fullRect = new LayoutRect(LayoutSlot.X + border, LayoutSlot.Y + border, MathF.Max(0f, finalSize.X - (border * 2f)), MathF.Max(0f, finalSize.Y - (border * 2f)));
        var decision = ResolveBarsAndMeasureContent(fullRect);

        _showHorizontalBar = decision.ShowHorizontalBar;
        _showVerticalBar = decision.ShowVerticalBar;
        _contentViewportRect = decision.ViewportRect;
        ApplyScrollMetrics(decision.ExtentWidth, decision.ExtentHeight, decision.ViewportWidth, decision.ViewportHeight);

        if (ContentElement is FrameworkElement content)
        {
            var arrangedWidth = MathF.Max(decision.ViewportWidth, decision.ExtentWidth);
            var arrangedHeight = MathF.Max(decision.ViewportHeight, decision.ExtentHeight);
            content.Arrange(new LayoutRect(
                decision.ViewportRect.X - HorizontalOffset,
                decision.ViewportRect.Y - VerticalOffset,
                arrangedWidth,
                arrangedHeight));
        }

        var barThickness = MathF.Max(0f, ScrollBarThickness);
        if (_showHorizontalBar)
        {
            _horizontalBar.IsVisible = true;
            _horizontalBar.Arrange(new LayoutRect(
                fullRect.X,
                fullRect.Y + fullRect.Height - barThickness,
                MathF.Max(0f, fullRect.Width - (_showVerticalBar ? barThickness : 0f)),
                barThickness));
        }
        else
        {
            _horizontalBar.IsVisible = false;
        }

        if (_showVerticalBar)
        {
            _verticalBar.IsVisible = true;
            _verticalBar.Arrange(new LayoutRect(
                fullRect.X + fullRect.Width - barThickness,
                fullRect.Y,
                barThickness,
                MathF.Max(0f, fullRect.Height - (_showHorizontalBar ? barThickness : 0f))));
        }
        else
        {
            _verticalBar.IsVisible = false;
        }

        _horizontalBar.Minimum = 0f;
        _verticalBar.Minimum = 0f;
        _horizontalBar.Maximum = ExtentWidth;
        _verticalBar.Maximum = ExtentHeight;
        _horizontalBar.ViewportSize = ViewportWidth;
        _verticalBar.ViewportSize = ViewportHeight;
        _horizontalBar.Value = HorizontalOffset;
        _verticalBar.Value = VerticalOffset;

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
        clipRect = _contentViewportRect;
        return true;
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);
        if (args.Property == ContentProperty)
        {
            AttachScrollInfoIfNeeded();
        }
    }

    private void AttachScrollInfoIfNeeded()
    {
        if (_scrollInfo != null && !ReferenceEquals(_scrollInfo, ContentElement))
        {
            _scrollInfo.ScrollOwner = null;
            _scrollInfo = null;
        }

        if (ContentElement is IScrollInfo info && !ReferenceEquals(info, _scrollInfo))
        {
            _scrollInfo = info;
            _scrollInfo.ScrollOwner = this;
        }
    }

    private (bool ShowHorizontalBar, bool ShowVerticalBar, float ExtentWidth, float ExtentHeight, float ViewportWidth, float ViewportHeight, LayoutRect ViewportRect)
        ResolveBarsAndMeasureContent(LayoutRect bounds)
    {
        var barSize = MathF.Max(0f, ScrollBarThickness);
        var showHorizontal = HorizontalScrollBarVisibility == ScrollBarVisibility.Visible;
        var showVertical = VerticalScrollBarVisibility == ScrollBarVisibility.Visible;

        for (var i = 0; i < 2; i++)
        {
            var viewportWidth = MathF.Max(0f, bounds.Width - (showVertical ? barSize : 0f));
            var viewportHeight = MathF.Max(0f, bounds.Height - (showHorizontal ? barSize : 0f));

            MeasureContent(viewportWidth, viewportHeight, out var extentWidth, out var extentHeight);

            if (HorizontalScrollBarVisibility == ScrollBarVisibility.Auto)
            {
                showHorizontal = extentWidth > viewportWidth + 0.01f;
            }

            if (VerticalScrollBarVisibility == ScrollBarVisibility.Auto)
            {
                showVertical = extentHeight > viewportHeight + 0.01f;
            }
        }

        var finalViewportWidth = MathF.Max(0f, bounds.Width - (showVertical ? barSize : 0f));
        var finalViewportHeight = MathF.Max(0f, bounds.Height - (showHorizontal ? barSize : 0f));
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

    private void MeasureContent(float viewportWidth, float viewportHeight, out float extentWidth, out float extentHeight)
    {
        var canScrollHorizontally = HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
        var canScrollVertically = VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;

        if (_scrollInfo != null)
        {
            _scrollInfo.CanHorizontallyScroll = canScrollHorizontally;
            _scrollInfo.CanVerticallyScroll = canScrollVertically;
        }

        if (ContentElement is FrameworkElement content)
        {
            var constraint = new Vector2(
                canScrollHorizontally ? float.PositiveInfinity : viewportWidth,
                canScrollVertically ? float.PositiveInfinity : viewportHeight);
            content.Measure(constraint);
        }

        extentWidth = _scrollInfo?.ExtentWidth ?? (ContentElement as FrameworkElement)?.DesiredSize.X ?? 0f;
        extentHeight = _scrollInfo?.ExtentHeight ?? (ContentElement as FrameworkElement)?.DesiredSize.Y ?? 0f;
    }

    private void ApplyScrollMetrics(float extentWidth, float extentHeight, float viewportWidth, float viewportHeight)
    {
        ExtentWidth = MathF.Max(0f, extentWidth);
        ExtentHeight = MathF.Max(0f, extentHeight);
        ViewportWidth = MathF.Max(0f, viewportWidth);
        ViewportHeight = MathF.Max(0f, viewportHeight);

        if (_scrollInfo != null)
        {
            HorizontalOffset = MathF.Max(0f, MathF.Min(_scrollInfo.HorizontalOffset, MathF.Max(0f, ExtentWidth - ViewportWidth)));
            VerticalOffset = MathF.Max(0f, MathF.Min(_scrollInfo.VerticalOffset, MathF.Max(0f, ExtentHeight - ViewportHeight)));
            return;
        }

        SetOffsets(HorizontalOffset, VerticalOffset);
    }

    internal bool HandleMouseWheelFromInput(int delta)
    {
        if (!IsEnabled || delta == 0)
        {
            return false;
        }

        var beforeHorizontal = HorizontalOffset;
        var beforeVertical = VerticalOffset;
        if (_scrollInfo != null)
        {
            if (delta > 0)
            {
                _scrollInfo.MouseWheelUp();
            }
            else
            {
                _scrollInfo.MouseWheelDown();
            }

            SetOffsets(_scrollInfo.HorizontalOffset, _scrollInfo.VerticalOffset);
        }
        else
        {
            var amount = MathF.Max(1f, LineScrollAmount);
            var direction = delta > 0 ? -1f : 1f;
            SetOffsets(HorizontalOffset, VerticalOffset + (direction * amount));
        }

        return MathF.Abs(beforeHorizontal - HorizontalOffset) > 0.001f ||
               MathF.Abs(beforeVertical - VerticalOffset) > 0.001f;
    }

    private void SetOffsets(float horizontal, float vertical)
    {
        var maxHorizontal = MathF.Max(0f, ExtentWidth - ViewportWidth);
        var maxVertical = MathF.Max(0f, ExtentHeight - ViewportHeight);

        var nextHorizontal = MathF.Max(0f, MathF.Min(maxHorizontal, horizontal));
        var nextVertical = MathF.Max(0f, MathF.Min(maxVertical, vertical));

        if (_scrollInfo != null)
        {
            _scrollInfo.SetHorizontalOffset(nextHorizontal);
            _scrollInfo.SetVerticalOffset(nextVertical);
            nextHorizontal = MathF.Max(0f, MathF.Min(maxHorizontal, _scrollInfo.HorizontalOffset));
            nextVertical = MathF.Max(0f, MathF.Min(maxVertical, _scrollInfo.VerticalOffset));
        }

        HorizontalOffset = nextHorizontal;
        VerticalOffset = nextVertical;
    }
}
