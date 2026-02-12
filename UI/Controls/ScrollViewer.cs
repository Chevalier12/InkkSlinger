using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class ScrollViewer : Control
{
    private static readonly bool EnableScrollTrace = false;

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content),
            typeof(object),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(
            nameof(HorizontalOffset),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ScrollViewer scrollViewer && args.NewValue is float offset)
                    {
                        scrollViewer.Trace(
                            $"HorizontalOffset changed old={args.OldValue} new={args.NewValue} " +
                            $"scrollable={scrollViewer.ScrollableWidth:0.##}");
                        scrollViewer._viewportHost.HorizontalOffset = offset;
                        // Scrolling should not trigger a full layout pass of the entire UI tree.
                        // The viewport host applies the offset via a render transform.
                        scrollViewer._viewportHost.InvalidateVisual();
                        if (!scrollViewer._isHandlingScrollBarInput)
                        {
                            scrollViewer.SyncScrollBars();
                        }
                    }
                },
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    var numeric = value is float v ? v : 0f;
                    if (dependencyObject is not ScrollViewer scrollViewer)
                    {
                        return MathF.Max(0f, numeric);
                    }

                    return MathF.Max(0f, MathF.Min(scrollViewer.ScrollableWidth, numeric));
                }));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(
            nameof(VerticalOffset),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ScrollViewer scrollViewer && args.NewValue is float offset)
                    {
                        scrollViewer.Trace(
                            $"VerticalOffset changed old={args.OldValue} new={args.NewValue} " +
                            $"scrollable={scrollViewer.ScrollableHeight:0.##}");
                        scrollViewer._viewportHost.VerticalOffset = offset;
                        // Scrolling should not trigger a full layout pass of the entire UI tree.
                        // The viewport host applies the offset via a render transform.
                        scrollViewer._viewportHost.InvalidateVisual();
                        if (!scrollViewer._isHandlingScrollBarInput)
                        {
                            scrollViewer.SyncScrollBars();
                        }
                    }
                },
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    var numeric = value is float v ? v : 0f;
                    if (dependencyObject is not ScrollViewer scrollViewer)
                    {
                        return MathF.Max(0f, numeric);
                    }

                    return MathF.Max(0f, MathF.Min(scrollViewer.ScrollableHeight, numeric));
                }));

    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(HorizontalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Auto, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(VerticalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Auto, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineScrollAmountProperty =
        DependencyProperty.Register(
            nameof(LineScrollAmount),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(
                16f,
                FrameworkPropertyMetadataOptions.None,
                coerceValueCallback: static (_, value) => value is float amount && amount > 0f ? amount : 1f));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(new Color(16, 16, 16), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(new Color(92, 92, 92), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(ScrollViewer),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 0f ? thickness : 0f));

    private readonly ScrollViewportHost _viewportHost = new();
    private readonly ScrollBar _horizontalScrollBar;
    private readonly ScrollBar _verticalScrollBar;

    private LayoutRect _viewportRect;
    private bool _showHorizontalScrollBar;
    private bool _showVerticalScrollBar;
    private bool _isUpdatingBarValue;
    private bool _isHandlingScrollBarInput;

    public ScrollViewer()
    {
        _horizontalScrollBar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            SmallChange = 16f,
            LargeChange = 64f
        };

        _verticalScrollBar = new ScrollBar
        {
            Orientation = Orientation.Vertical,
            SmallChange = 16f,
            LargeChange = 64f
        };

        _horizontalScrollBar.ValueChanged += OnHorizontalScrollBarValueChanged;
        _verticalScrollBar.ValueChanged += OnVerticalScrollBarValueChanged;

        _viewportHost.SetVisualParent(this);
        _viewportHost.SetLogicalParent(this);
        _horizontalScrollBar.SetVisualParent(this);
        _horizontalScrollBar.SetLogicalParent(this);
        _verticalScrollBar.SetVisualParent(this);
        _verticalScrollBar.SetLogicalParent(this);
    }

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public float HorizontalOffset
    {
        get => GetValue<float>(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    public float VerticalOffset
    {
        get => GetValue<float>(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
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

    public float ExtentWidth => _viewportHost.ExtentWidth;

    public float ExtentHeight => _viewportHost.ExtentHeight;

    public float ViewportWidth => _viewportRect.Width;

    public float ViewportHeight => _viewportRect.Height;

    public float ScrollableWidth => MathF.Max(0f, ExtentWidth - ViewportWidth);

    public float ScrollableHeight => MathF.Max(0f, ExtentHeight - ViewportHeight);

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        yield return _viewportHost;

        if (_showHorizontalScrollBar)
        {
            yield return _horizontalScrollBar;
        }

        if (_showVerticalScrollBar)
        {
            yield return _verticalScrollBar;
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        yield return _viewportHost;

        if (_showHorizontalScrollBar)
        {
            yield return _horizontalScrollBar;
        }

        if (_showVerticalScrollBar)
        {
            yield return _verticalScrollBar;
        }
    }

    public void ScrollToHorizontalOffset(float offset)
    {
        HorizontalOffset = offset;
    }

    public void ScrollToVerticalOffset(float offset)
    {
        VerticalOffset = offset;
    }

    public void LineUp()
    {
        VerticalOffset -= LineScrollAmount;
    }

    public void LineDown()
    {
        VerticalOffset += LineScrollAmount;
    }

    public void LineLeft()
    {
        HorizontalOffset -= LineScrollAmount;
    }

    public void LineRight()
    {
        HorizontalOffset += LineScrollAmount;
    }

    public void PageUp()
    {
        VerticalOffset -= MathF.Max(LineScrollAmount, ViewportHeight);
    }

    public void PageDown()
    {
        VerticalOffset += MathF.Max(LineScrollAmount, ViewportHeight);
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property == ContentProperty)
        {
            if (args.NewValue == null)
            {
                _viewportHost.Content = null;
                return;
            }

            if (args.NewValue is UIElement element)
            {
                _viewportHost.Content = element;
                return;
            }

            throw new InvalidOperationException("ScrollViewer content must be a UIElement.");
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        Trace($"Measure start available={availableSize} offsets=({HorizontalOffset:0.##},{VerticalOffset:0.##})");
        var desired = base.MeasureOverride(availableSize);
        var border = BorderThickness * 2f;

        var innerWidth = MathF.Max(0f, availableSize.X - border);
        var innerHeight = MathF.Max(0f, availableSize.Y - border);
        var barThickness = _verticalScrollBar.Thickness;

        var viewportWidth = innerWidth;
        var viewportHeight = innerHeight;

        var contentMeasureSize = _viewportHost.Content is VirtualizingStackPanel
            ? new Vector2(innerWidth, innerHeight)
            : new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        _viewportHost.Measure(contentMeasureSize);
        var contentSize = _viewportHost.DesiredSize;

        var horizontalPolicy = HorizontalScrollBarVisibility;
        var verticalPolicy = VerticalScrollBarVisibility;

        var showHorizontal = horizontalPolicy == ScrollBarVisibility.Visible;
        var showVertical = verticalPolicy == ScrollBarVisibility.Visible;

        if (horizontalPolicy == ScrollBarVisibility.Auto && contentSize.X > viewportWidth)
        {
            showHorizontal = true;
        }

        if (verticalPolicy == ScrollBarVisibility.Auto && contentSize.Y > viewportHeight)
        {
            showVertical = true;
        }

        if (showVertical)
        {
            viewportWidth = MathF.Max(0f, viewportWidth - barThickness);
        }

        if (showHorizontal)
        {
            viewportHeight = MathF.Max(0f, viewportHeight - barThickness);
        }

        if (!showVertical && verticalPolicy == ScrollBarVisibility.Auto && contentSize.Y > viewportHeight)
        {
            showVertical = true;
            viewportWidth = MathF.Max(0f, viewportWidth - barThickness);
        }

        if (!showHorizontal && horizontalPolicy == ScrollBarVisibility.Auto && contentSize.X > viewportWidth)
        {
            showHorizontal = true;
            viewportHeight = MathF.Max(0f, viewportHeight - barThickness);
        }

        _showHorizontalScrollBar = showHorizontal && horizontalPolicy != ScrollBarVisibility.Disabled;
        _showVerticalScrollBar = showVertical && verticalPolicy != ScrollBarVisibility.Disabled;

        _viewportHost.ExtentWidth = contentSize.X;
        _viewportHost.ExtentHeight = contentSize.Y;

        if (_showHorizontalScrollBar)
        {
            _horizontalScrollBar.Measure(new Vector2(viewportWidth, barThickness));
        }

        if (_showVerticalScrollBar)
        {
            _verticalScrollBar.Measure(new Vector2(barThickness, viewportHeight));
        }

        var measuredWidth = viewportWidth + (_showVerticalScrollBar ? barThickness : 0f) + border;
        var measuredHeight = viewportHeight + (_showHorizontalScrollBar ? barThickness : 0f) + border;

        if (float.IsInfinity(availableSize.X) || float.IsNaN(availableSize.X))
        {
            measuredWidth = contentSize.X + (_showVerticalScrollBar ? barThickness : 0f) + border;
        }

        if (float.IsInfinity(availableSize.Y) || float.IsNaN(availableSize.Y))
        {
            measuredHeight = contentSize.Y + (_showHorizontalScrollBar ? barThickness : 0f) + border;
        }

        desired.X = MathF.Max(desired.X, measuredWidth);
        desired.Y = MathF.Max(desired.Y, measuredHeight);

        CoerceOffsets();
        ConfigureScrollBars();
        Trace(
            $"Measure end desired={desired} content={contentSize} viewport=({viewportWidth:0.##},{viewportHeight:0.##}) " +
            $"extent=({ExtentWidth:0.##},{ExtentHeight:0.##}) scrollable=({ScrollableWidth:0.##},{ScrollableHeight:0.##}) " +
            $"bars=(h:{_showHorizontalScrollBar},v:{_showVerticalScrollBar})");

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        Trace($"Arrange start final={finalSize} offsets=({HorizontalOffset:0.##},{VerticalOffset:0.##})");
        base.ArrangeOverride(finalSize);

        var slot = LayoutSlot;
        var border = BorderThickness;
        var barThickness = _verticalScrollBar.Thickness;

        var x = slot.X + border;
        var y = slot.Y + border;
        var width = MathF.Max(0f, finalSize.X - (border * 2f));
        var height = MathF.Max(0f, finalSize.Y - (border * 2f));

        var viewportWidth = MathF.Max(0f, width - (_showVerticalScrollBar ? barThickness : 0f));
        var viewportHeight = MathF.Max(0f, height - (_showHorizontalScrollBar ? barThickness : 0f));

        _viewportRect = new LayoutRect(x, y, viewportWidth, viewportHeight);
        _viewportHost.Arrange(_viewportRect);

        if (_showVerticalScrollBar)
        {
            _verticalScrollBar.Arrange(new LayoutRect(x + viewportWidth, y, barThickness, viewportHeight));
        }

        if (_showHorizontalScrollBar)
        {
            _horizontalScrollBar.Arrange(new LayoutRect(x, y + viewportHeight, viewportWidth, barThickness));
        }

        CoerceOffsets();
        ConfigureScrollBars();
        Trace(
            $"Arrange end viewportRect={_viewportRect} extent=({ExtentWidth:0.##},{ExtentHeight:0.##}) " +
            $"scrollable=({ScrollableWidth:0.##},{ScrollableHeight:0.##}) offsets=({HorizontalOffset:0.##},{VerticalOffset:0.##})");

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, slot, BorderThickness, BorderBrush, Opacity);
        }
    }

    protected override void OnMouseWheel(RoutedMouseWheelEventArgs args)
    {
        base.OnMouseWheel(args);

        if (!IsEnabled || ScrollableHeight <= 0f)
        {
            return;
        }

        var steps = args.Delta / 120f;
        var nextOffset = MathF.Max(0f, MathF.Min(ScrollableHeight, VerticalOffset - (steps * LineScrollAmount)));
        Trace(
            $"MouseWheel delta={args.Delta} steps={steps:0.##} line={LineScrollAmount:0.##} " +
            $"offsetBefore={VerticalOffset:0.##} offsetAfter={nextOffset:0.##} scrollable={ScrollableHeight:0.##}");
        if (!AreClose(nextOffset, VerticalOffset))
        {
            VerticalOffset = nextOffset;
        }

        args.Handled = true;
    }

    protected override void OnKeyDown(RoutedKeyEventArgs args)
    {
        base.OnKeyDown(args);

        if (!IsEnabled)
        {
            return;
        }

        var handled = true;
        switch (args.Key)
        {
            case Keys.Down:
                LineDown();
                break;
            case Keys.Up:
                LineUp();
                break;
            case Keys.Right:
                LineRight();
                break;
            case Keys.Left:
                LineLeft();
                break;
            case Keys.PageDown:
                PageDown();
                break;
            case Keys.PageUp:
                PageUp();
                break;
            case Keys.Home:
                ScrollToVerticalOffset(0f);
                ScrollToHorizontalOffset(0f);
                break;
            case Keys.End:
                ScrollToVerticalOffset(ScrollableHeight);
                ScrollToHorizontalOffset(ScrollableWidth);
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            args.Handled = true;
        }
    }

    private void ConfigureScrollBars()
    {
        _isUpdatingBarValue = true;
        try
        {
            _horizontalScrollBar.Minimum = 0f;
            _horizontalScrollBar.Maximum = ScrollableWidth;
            _horizontalScrollBar.ViewportSize = ViewportWidth;
            _horizontalScrollBar.LargeChange = MathF.Max(LineScrollAmount, ViewportWidth * 0.9f);
            _horizontalScrollBar.SmallChange = LineScrollAmount;
            _horizontalScrollBar.Value = HorizontalOffset;
            _horizontalScrollBar.IsEnabled = ScrollableWidth > 0f;

            _verticalScrollBar.Minimum = 0f;
            _verticalScrollBar.Maximum = ScrollableHeight;
            _verticalScrollBar.ViewportSize = ViewportHeight;
            _verticalScrollBar.LargeChange = MathF.Max(LineScrollAmount, ViewportHeight * 0.9f);
            _verticalScrollBar.SmallChange = LineScrollAmount;
            _verticalScrollBar.Value = VerticalOffset;
            _verticalScrollBar.IsEnabled = ScrollableHeight > 0f;
            Trace(
                $"ConfigureScrollBars h(value={_horizontalScrollBar.Value:0.##}, max={_horizontalScrollBar.Maximum:0.##}, vp={_horizontalScrollBar.ViewportSize:0.##}) " +
                $"v(value={_verticalScrollBar.Value:0.##}, max={_verticalScrollBar.Maximum:0.##}, vp={_verticalScrollBar.ViewportSize:0.##})");
        }
        finally
        {
            _isUpdatingBarValue = false;
        }
    }

    private void CoerceOffsets()
    {
        var coercedHorizontal = MathF.Max(0f, MathF.Min(ScrollableWidth, HorizontalOffset));
        if (!AreClose(HorizontalOffset, coercedHorizontal))
        {
            Trace($"Coerce HorizontalOffset {HorizontalOffset:0.##} -> {coercedHorizontal:0.##}");
            HorizontalOffset = coercedHorizontal;
        }

        var coercedVertical = MathF.Max(0f, MathF.Min(ScrollableHeight, VerticalOffset));
        if (!AreClose(VerticalOffset, coercedVertical))
        {
            Trace($"Coerce VerticalOffset {VerticalOffset:0.##} -> {coercedVertical:0.##}");
            VerticalOffset = coercedVertical;
        }

        _viewportHost.HorizontalOffset = HorizontalOffset;
        _viewportHost.VerticalOffset = VerticalOffset;
    }

    private void SyncScrollBars()
    {
        if (_isUpdatingBarValue)
        {
            return;
        }

        _isUpdatingBarValue = true;
        try
        {
            _horizontalScrollBar.Value = HorizontalOffset;
            _verticalScrollBar.Value = VerticalOffset;
            Trace($"SyncScrollBars set h={HorizontalOffset:0.##} v={VerticalOffset:0.##}");
        }
        finally
        {
            _isUpdatingBarValue = false;
        }
    }

    private void OnHorizontalScrollBarValueChanged(object? sender, RoutedSimpleEventArgs args)
    {
        if (_isUpdatingBarValue)
        {
            return;
        }

        Trace($"HorizontalScrollBar ValueChanged value={_horizontalScrollBar.Value:0.##}");
        _isHandlingScrollBarInput = true;
        try
        {
            if (!AreClose(HorizontalOffset, _horizontalScrollBar.Value))
            {
                HorizontalOffset = _horizontalScrollBar.Value;
            }
        }
        finally
        {
            _isHandlingScrollBarInput = false;
        }
    }

    private void OnVerticalScrollBarValueChanged(object? sender, RoutedSimpleEventArgs args)
    {
        if (_isUpdatingBarValue)
        {
            return;
        }

        Trace($"VerticalScrollBar ValueChanged value={_verticalScrollBar.Value:0.##}");
        _isHandlingScrollBarInput = true;
        try
        {
            if (!AreClose(VerticalOffset, _verticalScrollBar.Value))
            {
                VerticalOffset = _verticalScrollBar.Value;
            }
        }
        finally
        {
            _isHandlingScrollBarInput = false;
        }
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }

    private void Trace(string message)
    {
        if (!EnableScrollTrace)
        {
            return;
        }

        Console.WriteLine($"[ScrollViewer#{GetHashCode():X8}] t={Environment.TickCount64} {message}");
    }

    private sealed class ScrollViewportHost : FrameworkElement
    {
        private UIElement? _content;

        public UIElement? Content
        {
            get => _content;
            set
            {
                if (ReferenceEquals(_content, value))
                {
                    return;
                }

                if (_content != null)
                {
                    _content.SetVisualParent(null);
                    _content.SetLogicalParent(null);
                }

                _content = value;
                if (_content != null)
                {
                    _content.SetVisualParent(this);
                    _content.SetLogicalParent(this);
                }

                InvalidateMeasure();
            }
        }

        public float ExtentWidth { get; set; }

        public float ExtentHeight { get; set; }

        public float HorizontalOffset { get; set; }

        public float VerticalOffset { get; set; }

        public override IEnumerable<UIElement> GetVisualChildren()
        {
            if (_content != null)
            {
                yield return _content;
            }
        }

        public override IEnumerable<UIElement> GetLogicalChildren()
        {
            if (_content != null)
            {
                yield return _content;
            }
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            if (_content is not FrameworkElement content)
            {
                return Vector2.Zero;
            }

            var childMeasureSize = content is VirtualizingStackPanel
                ? availableSize
                : new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            content.Measure(childMeasureSize);
            return content.DesiredSize;
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            if (_content is FrameworkElement content)
            {
                var arrangedWidth = MathF.Max(finalSize.X, ExtentWidth);
                var arrangedHeight = MathF.Max(finalSize.Y, ExtentHeight);
                content.Arrange(new LayoutRect(
                    LayoutSlot.X,
                    LayoutSlot.Y,
                    arrangedWidth,
                    arrangedHeight));
            }

            return finalSize;
        }

        protected override bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)
        {
            // Apply scrolling as a render transform so offset changes do not force a full layout pass.
            if (HorizontalOffset == 0f && VerticalOffset == 0f)
            {
                transform = Matrix.Identity;
                inverseTransform = Matrix.Identity;
                return false;
            }

            transform = Matrix.CreateTranslation(-HorizontalOffset, -VerticalOffset, 0f);
            inverseTransform = Matrix.CreateTranslation(HorizontalOffset, VerticalOffset, 0f);
            return true;
        }

        protected override bool TryGetClipRect(out LayoutRect clipRect)
        {
            clipRect = LayoutSlot;
            return true;
        }
    }
}

