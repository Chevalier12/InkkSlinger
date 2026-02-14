using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class ScrollViewer : Control
{
    private const string PartScrollContentPresenter = "PART_ScrollContentPresenter";
    private const string PartHorizontalScrollBar = "PART_HorizontalScrollBar";
    private const string PartVerticalScrollBar = "PART_VerticalScrollBar";

    public static readonly RoutedEvent ScrollChangedEvent = new(nameof(ScrollChanged), RoutingStrategy.Bubble);
    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(object), typeof(ScrollViewer), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange, propertyChangedCallback: static (d, a) =>
        {
            if (d is ScrollViewer s)
            {
                s.OnContentChanged(a.NewValue);
            }
        }));
    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(nameof(HorizontalOffset), typeof(float), typeof(ScrollViewer), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.None, propertyChangedCallback: static (d, _) =>
        {
            if (d is ScrollViewer s)
            {
                s.OnOffsetChanged(true);
            }
        }, coerceValueCallback: static (d, v) =>
        {
            var n = v is float f ? f : 0f;
            if (d is not ScrollViewer s)
            {
                return MathF.Max(0f, n);
            }

            return MathF.Max(0f, MathF.Min(s.ScrollableWidth, n));
        }));
    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(nameof(VerticalOffset), typeof(float), typeof(ScrollViewer), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.None, propertyChangedCallback: static (d, _) =>
        {
            if (d is ScrollViewer s)
            {
                s.OnOffsetChanged(false);
            }
        }, coerceValueCallback: static (d, v) =>
        {
            var n = v is float f ? f : 0f;
            if (d is not ScrollViewer s)
            {
                return MathF.Max(0f, n);
            }

            return MathF.Max(0f, MathF.Min(s.ScrollableHeight, n));
        }));
    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(nameof(HorizontalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(ScrollViewer), new FrameworkPropertyMetadata(ScrollBarVisibility.Auto, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(nameof(VerticalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(ScrollViewer), new FrameworkPropertyMetadata(ScrollBarVisibility.Auto, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CanContentScrollProperty =
        DependencyProperty.Register(nameof(CanContentScroll), typeof(bool), typeof(ScrollViewer), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange, propertyChangedCallback: static (d, _) =>
        {
            if (d is ScrollViewer s)
            {
                s.RefreshScrollInfoBinding();
                s.InvalidateMeasure();
            }
        }));
    public static readonly DependencyProperty LineScrollAmountProperty =
        DependencyProperty.Register(nameof(LineScrollAmount), typeof(float), typeof(ScrollViewer), new FrameworkPropertyMetadata(16f, FrameworkPropertyMetadataOptions.None, coerceValueCallback: static (_, v) => v is float a && a > 0f ? a : 1f));
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(nameof(Background), typeof(Color), typeof(ScrollViewer), new FrameworkPropertyMetadata(new Color(16, 16, 16), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(nameof(BorderBrush), typeof(Color), typeof(ScrollViewer), new FrameworkPropertyMetadata(new Color(92, 92, 92), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(nameof(BorderThickness), typeof(float), typeof(ScrollViewer), new FrameworkPropertyMetadata(1f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender, coerceValueCallback: static (_, v) => v is float t && t >= 0f ? t : 0f));

    private readonly ScrollContentPresenter _fallbackPresenter = new();
    private readonly ScrollBar _fallbackHorizontalScrollBar;
    private readonly ScrollBar _fallbackVerticalScrollBar;
    private ScrollContentPresenter _presenter;
    private ScrollBar _horizontalScrollBar;
    private ScrollBar _verticalScrollBar;
    private IScrollInfo? _scrollInfo;
    private UIElement? _contentElement;
    private bool _showHorizontalScrollBar;
    private bool _showVerticalScrollBar;
    private bool _isUpdatingBarValue;
    private bool _isSyncingScrollInfo;
    private bool _isUpdatingOffsetsFromScrollInfo;
    private float _extentWidth;
    private float _extentHeight;
    private float _viewportWidth;
    private float _viewportHeight;
    private float _lastExtentWidth;
    private float _lastExtentHeight;
    private float _lastViewportWidth;
    private float _lastViewportHeight;
    private float _lastHorizontalOffset;
    private float _lastVerticalOffset;

    public ScrollViewer()
    {
        _fallbackHorizontalScrollBar = new ScrollBar { Orientation = Orientation.Horizontal, SmallChange = 16f, LargeChange = 64f };
        _fallbackVerticalScrollBar = new ScrollBar { Orientation = Orientation.Vertical, SmallChange = 16f, LargeChange = 64f };
        _fallbackHorizontalScrollBar.ValueChanged += OnHorizontalScrollBarValueChanged;
        _fallbackVerticalScrollBar.ValueChanged += OnVerticalScrollBarValueChanged;
        _fallbackPresenter.SetVisualParent(this);
        _fallbackPresenter.SetLogicalParent(this);
        _fallbackHorizontalScrollBar.SetVisualParent(this);
        _fallbackHorizontalScrollBar.SetLogicalParent(this);
        _fallbackVerticalScrollBar.SetVisualParent(this);
        _fallbackVerticalScrollBar.SetLogicalParent(this);
        _presenter = _fallbackPresenter;
        _horizontalScrollBar = _fallbackHorizontalScrollBar;
        _verticalScrollBar = _fallbackVerticalScrollBar;
    }

    public event EventHandler<ScrollChangedEventArgs> ScrollChanged { add => AddHandler(ScrollChangedEvent, value); remove => RemoveHandler(ScrollChangedEvent, value); }
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    public float HorizontalOffset { get => GetValue<float>(HorizontalOffsetProperty); set => SetValue(HorizontalOffsetProperty, value); }
    public float VerticalOffset { get => GetValue<float>(VerticalOffsetProperty); set => SetValue(VerticalOffsetProperty, value); }
    public ScrollBarVisibility HorizontalScrollBarVisibility { get => GetValue<ScrollBarVisibility>(HorizontalScrollBarVisibilityProperty); set => SetValue(HorizontalScrollBarVisibilityProperty, value); }
    public ScrollBarVisibility VerticalScrollBarVisibility { get => GetValue<ScrollBarVisibility>(VerticalScrollBarVisibilityProperty); set => SetValue(VerticalScrollBarVisibilityProperty, value); }
    public bool CanContentScroll { get => GetValue<bool>(CanContentScrollProperty); set => SetValue(CanContentScrollProperty, value); }
    public float LineScrollAmount { get => GetValue<float>(LineScrollAmountProperty); set => SetValue(LineScrollAmountProperty, value); }
    public Color Background { get => GetValue<Color>(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public Color BorderBrush { get => GetValue<Color>(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }
    public float BorderThickness { get => GetValue<float>(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
    public ScrollBarVisibility ComputedHorizontalScrollBarVisibility { get; private set; } = ScrollBarVisibility.Hidden;
    public ScrollBarVisibility ComputedVerticalScrollBarVisibility { get; private set; } = ScrollBarVisibility.Hidden;
    public float ExtentWidth => _extentWidth;
    public float ExtentHeight => _extentHeight;
    public float ViewportWidth => _viewportWidth;
    public float ViewportHeight => _viewportHeight;
    public float ScrollableWidth => MathF.Max(0f, ExtentWidth - ViewportWidth);
    public float ScrollableHeight => MathF.Max(0f, ExtentHeight - ViewportHeight);

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        yield return _presenter;
        if (_showHorizontalScrollBar) yield return _horizontalScrollBar;
        if (_showVerticalScrollBar) yield return _verticalScrollBar;
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        yield return _presenter;
        if (_showHorizontalScrollBar) yield return _horizontalScrollBar;
        if (_showVerticalScrollBar) yield return _verticalScrollBar;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        var p = GetTemplateChild(PartScrollContentPresenter) as ScrollContentPresenter;
        var h = GetTemplateChild(PartHorizontalScrollBar) as ScrollBar;
        var v = GetTemplateChild(PartVerticalScrollBar) as ScrollBar;
        if (h != null) h.Orientation = Orientation.Horizontal;
        if (v != null) v.Orientation = Orientation.Vertical;
        var nextP = p ?? _fallbackPresenter;
        var nextH = h ?? _fallbackHorizontalScrollBar;
        var nextV = v ?? _fallbackVerticalScrollBar;
        if (!ReferenceEquals(_horizontalScrollBar, nextH)) { _horizontalScrollBar.ValueChanged -= OnHorizontalScrollBarValueChanged; nextH.ValueChanged += OnHorizontalScrollBarValueChanged; }
        if (!ReferenceEquals(_verticalScrollBar, nextV)) { _verticalScrollBar.ValueChanged -= OnVerticalScrollBarValueChanged; nextV.ValueChanged += OnVerticalScrollBarValueChanged; }
        _presenter = nextP;
        _horizontalScrollBar = nextH;
        _verticalScrollBar = nextV;
        if (_presenter == _fallbackPresenter) { _presenter.SetVisualParent(this); _presenter.SetLogicalParent(this); }
        if (_horizontalScrollBar == _fallbackHorizontalScrollBar) { _horizontalScrollBar.SetVisualParent(this); _horizontalScrollBar.SetLogicalParent(this); }
        if (_verticalScrollBar == _fallbackVerticalScrollBar) { _verticalScrollBar.SetVisualParent(this); _verticalScrollBar.SetLogicalParent(this); }
        _presenter.Content = _contentElement;
        RefreshScrollInfoBinding();
        InvalidateMeasure();
    }

    public void ScrollToHorizontalOffset(float offset) { if (HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled) return; if (_scrollInfo != null) { _scrollInfo.SetHorizontalOffset(offset); InvalidateScrollInfo(); } else { HorizontalOffset = offset; } }
    public void ScrollToVerticalOffset(float offset) { if (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled) return; if (_scrollInfo != null) { _scrollInfo.SetVerticalOffset(offset); InvalidateScrollInfo(); } else { VerticalOffset = offset; } }
    public void LineUp() { if (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled) return; if (_scrollInfo != null) { _scrollInfo.LineUp(); InvalidateScrollInfo(); } else { VerticalOffset -= LineScrollAmount; } }
    public void LineDown() { if (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled) return; if (_scrollInfo != null) { _scrollInfo.LineDown(); InvalidateScrollInfo(); } else { VerticalOffset += LineScrollAmount; } }
    public void LineLeft() { if (HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled) return; if (_scrollInfo != null) { _scrollInfo.LineLeft(); InvalidateScrollInfo(); } else { HorizontalOffset -= LineScrollAmount; } }
    public void LineRight() { if (HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled) return; if (_scrollInfo != null) { _scrollInfo.LineRight(); InvalidateScrollInfo(); } else { HorizontalOffset += LineScrollAmount; } }
    public void PageUp() { if (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled) return; if (_scrollInfo != null) { _scrollInfo.PageUp(); InvalidateScrollInfo(); } else { VerticalOffset -= MathF.Max(LineScrollAmount, ViewportHeight); } }
    public void PageDown() { if (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled) return; if (_scrollInfo != null) { _scrollInfo.PageDown(); InvalidateScrollInfo(); } else { VerticalOffset += MathF.Max(LineScrollAmount, ViewportHeight); } }
    public void PageLeft() { if (HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled) return; if (_scrollInfo != null) { _scrollInfo.PageLeft(); InvalidateScrollInfo(); } else { HorizontalOffset -= MathF.Max(LineScrollAmount, ViewportWidth); } }
    public void PageRight() { if (HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled) return; if (_scrollInfo != null) { _scrollInfo.PageRight(); InvalidateScrollInfo(); } else { HorizontalOffset += MathF.Max(LineScrollAmount, ViewportWidth); } }
    public void InvalidateScrollInfo() { SyncOffsetsFromScrollInfo(); CoerceOffsets(); ConfigureScrollBars(); RaiseScrollChangedIfNeeded(); InvalidateVisual(); }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        ApplyTemplate();
        var border = BorderThickness * 2f;
        var innerWidth = MathF.Max(0f, availableSize.X - border);
        var innerHeight = MathF.Max(0f, availableSize.Y - border);
        var thickness = _verticalScrollBar.Thickness;
        var viewportWidth = innerWidth;
        var viewportHeight = innerHeight;
        _presenter.UseScrollInfo = CanContentScroll || _presenter.ScrollInfo is VirtualizingStackPanel;
        _presenter.Measure(new Vector2(viewportWidth, viewportHeight));
        RefreshScrollInfoBinding();
        _extentWidth = _scrollInfo != null ? MathF.Max(0f, _scrollInfo.ExtentWidth) : _presenter.ExtentWidth;
        _extentHeight = _scrollInfo != null ? MathF.Max(0f, _scrollInfo.ExtentHeight) : _presenter.ExtentHeight;
        ResolveBarVisibility(innerWidth, innerHeight, thickness, out viewportWidth, out viewportHeight);
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
        CoerceOffsets();
        if (_showHorizontalScrollBar) _horizontalScrollBar.Measure(new Vector2(viewportWidth, thickness));
        if (_showVerticalScrollBar) _verticalScrollBar.Measure(new Vector2(thickness, viewportHeight));
        var measuredWidth = viewportWidth + (_showVerticalScrollBar ? thickness : 0f) + border;
        var measuredHeight = viewportHeight + (_showHorizontalScrollBar ? thickness : 0f) + border;
        if (float.IsInfinity(availableSize.X) || float.IsNaN(availableSize.X)) measuredWidth = _extentWidth + (_showVerticalScrollBar ? thickness : 0f) + border;
        if (float.IsInfinity(availableSize.Y) || float.IsNaN(availableSize.Y)) measuredHeight = _extentHeight + (_showHorizontalScrollBar ? thickness : 0f) + border;
        ConfigureScrollBars();
        RaiseScrollChangedIfNeeded();
        return new Vector2(MathF.Max(0f, measuredWidth), MathF.Max(0f, measuredHeight));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var slot = LayoutSlot;
        var border = BorderThickness;
        var thickness = _verticalScrollBar.Thickness;
        var x = slot.X + border;
        var y = slot.Y + border;
        var width = MathF.Max(0f, finalSize.X - (border * 2f));
        var height = MathF.Max(0f, finalSize.Y - (border * 2f));
        var viewportWidth = MathF.Max(0f, width - (_showVerticalScrollBar ? thickness : 0f));
        var viewportHeight = MathF.Max(0f, height - (_showHorizontalScrollBar ? thickness : 0f));
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
        _presenter.Arrange(new LayoutRect(x, y, viewportWidth, viewportHeight));
        _extentWidth = _scrollInfo != null ? MathF.Max(0f, _scrollInfo.ExtentWidth) : _presenter.ExtentWidth;
        _extentHeight = _scrollInfo != null ? MathF.Max(0f, _scrollInfo.ExtentHeight) : _presenter.ExtentHeight;
        if (_showVerticalScrollBar) _verticalScrollBar.Arrange(new LayoutRect(x + viewportWidth, y, thickness, viewportHeight));
        if (_showHorizontalScrollBar) _horizontalScrollBar.Arrange(new LayoutRect(x, y + viewportHeight, viewportWidth, thickness));
        CoerceOffsets();
        ConfigureScrollBars();
        RaiseScrollChangedIfNeeded();
        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);
        if (BorderThickness > 0f) UiDrawing.DrawRectStroke(spriteBatch, slot, BorderThickness, BorderBrush, Opacity);
    }

    protected override void OnMouseWheel(RoutedMouseWheelEventArgs args)
    {
        base.OnMouseWheel(args);
        if (!IsEnabled) return;
        var before = VerticalOffset;
        var steps = args.Delta / 120f;
        if (_scrollInfo != null)
        {
            if (steps > 0f) _scrollInfo.MouseWheelUp();
            else if (steps < 0f) _scrollInfo.MouseWheelDown();
            InvalidateScrollInfo();
        }
        else
        {
            var next = MathF.Max(0f, MathF.Min(ScrollableHeight, VerticalOffset - (steps * LineScrollAmount)));
            if (!AreClose(next, VerticalOffset)) VerticalOffset = next;
        }

        if (!AreClose(before, VerticalOffset)) args.Handled = true;
    }

    protected override void OnKeyDown(RoutedKeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (!IsEnabled) return;
        var handled = true;
        switch (args.Key)
        {
            case Keys.Down: LineDown(); break;
            case Keys.Up: LineUp(); break;
            case Keys.Right: LineRight(); break;
            case Keys.Left: LineLeft(); break;
            case Keys.PageDown: PageDown(); break;
            case Keys.PageUp: PageUp(); break;
            case Keys.Home: ScrollToVerticalOffset(0f); ScrollToHorizontalOffset(0f); break;
            case Keys.End: ScrollToVerticalOffset(ScrollableHeight); ScrollToHorizontalOffset(ScrollableWidth); break;
            default: handled = false; break;
        }

        if (handled) args.Handled = true;
    }

    private void OnContentChanged(object? newValue)
    {
        if (newValue == null)
        {
            _contentElement = null;
            _presenter.Content = null;
            RefreshScrollInfoBinding();
            InvalidateMeasure();
            return;
        }

        if (newValue is not UIElement element) throw new InvalidOperationException("ScrollViewer content must be a UIElement.");
        _contentElement = element;
        _presenter.Content = element;
        RefreshScrollInfoBinding();
        InvalidateMeasure();
    }

    private void OnOffsetChanged(bool horizontal)
    {
        if (_scrollInfo != null && !_isUpdatingOffsetsFromScrollInfo)
        {
            _isSyncingScrollInfo = true;
            try
            {
                if (horizontal) _scrollInfo.SetHorizontalOffset(HorizontalOffset);
                else _scrollInfo.SetVerticalOffset(VerticalOffset);
            }
            finally
            {
                _isSyncingScrollInfo = false;
            }
        }

        _presenter.HorizontalOffset = HorizontalOffset;
        _presenter.VerticalOffset = VerticalOffset;
        _presenter.InvalidateVisual();
        InputManager.NotifyHitTestGeometryChanged();
        SyncScrollBars();
        RaiseScrollChangedIfNeeded();
    }

    private void RefreshScrollInfoBinding()
    {
        var presenterScrollInfo = _presenter.ScrollInfo;
        var next = (CanContentScroll || presenterScrollInfo is VirtualizingStackPanel) ? presenterScrollInfo : null;
        if (ReferenceEquals(_scrollInfo, next))
        {
            if (_scrollInfo != null)
            {
                _scrollInfo.CanHorizontallyScroll = HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
                _scrollInfo.CanVerticallyScroll = VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
            }

            return;
        }

        if (_scrollInfo != null && ReferenceEquals(_scrollInfo.ScrollOwner, this)) _scrollInfo.ScrollOwner = null;
        _scrollInfo = next;
        if (_scrollInfo != null)
        {
            _scrollInfo.ScrollOwner = this;
            _scrollInfo.CanHorizontallyScroll = HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
            _scrollInfo.CanVerticallyScroll = VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
            SyncOffsetsFromScrollInfo();
        }
    }

    private void SyncOffsetsFromScrollInfo()
    {
        if (_scrollInfo == null || _isSyncingScrollInfo) return;
        _isUpdatingOffsetsFromScrollInfo = true;
        try
        {
            if (!AreClose(HorizontalOffset, _scrollInfo.HorizontalOffset)) SetValue(HorizontalOffsetProperty, _scrollInfo.HorizontalOffset);
            if (!AreClose(VerticalOffset, _scrollInfo.VerticalOffset)) SetValue(VerticalOffsetProperty, _scrollInfo.VerticalOffset);
        }
        finally
        {
            _isUpdatingOffsetsFromScrollInfo = false;
        }
    }

    private void ResolveBarVisibility(float innerWidth, float innerHeight, float thickness, out float viewportWidth, out float viewportHeight)
    {
        var hPolicy = HorizontalScrollBarVisibility;
        var vPolicy = VerticalScrollBarVisibility;
        var showH = hPolicy == ScrollBarVisibility.Visible;
        var showV = vPolicy == ScrollBarVisibility.Visible;
        viewportWidth = innerWidth;
        viewportHeight = innerHeight;
        if (hPolicy == ScrollBarVisibility.Auto && _extentWidth > viewportWidth) showH = true;
        if (vPolicy == ScrollBarVisibility.Auto && _extentHeight > viewportHeight) showV = true;
        if (showV) viewportWidth = MathF.Max(0f, viewportWidth - thickness);
        if (showH) viewportHeight = MathF.Max(0f, viewportHeight - thickness);
        if (!showV && vPolicy == ScrollBarVisibility.Auto && _extentHeight > viewportHeight) { showV = true; viewportWidth = MathF.Max(0f, viewportWidth - thickness); }
        if (!showH && hPolicy == ScrollBarVisibility.Auto && _extentWidth > viewportWidth) { showH = true; viewportHeight = MathF.Max(0f, viewportHeight - thickness); }
        _showHorizontalScrollBar = hPolicy switch { ScrollBarVisibility.Visible => true, ScrollBarVisibility.Auto => showH, _ => false };
        _showVerticalScrollBar = vPolicy switch { ScrollBarVisibility.Visible => true, ScrollBarVisibility.Auto => showV, _ => false };
        ComputedHorizontalScrollBarVisibility = hPolicy == ScrollBarVisibility.Disabled ? ScrollBarVisibility.Disabled : _showHorizontalScrollBar ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
        ComputedVerticalScrollBarVisibility = vPolicy == ScrollBarVisibility.Disabled ? ScrollBarVisibility.Disabled : _showVerticalScrollBar ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
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
            _horizontalScrollBar.IsEnabled = HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled && ScrollableWidth > 0f;
            _verticalScrollBar.Minimum = 0f;
            _verticalScrollBar.Maximum = ScrollableHeight;
            _verticalScrollBar.ViewportSize = ViewportHeight;
            _verticalScrollBar.LargeChange = MathF.Max(LineScrollAmount, ViewportHeight * 0.9f);
            _verticalScrollBar.SmallChange = LineScrollAmount;
            _verticalScrollBar.Value = VerticalOffset;
            _verticalScrollBar.IsEnabled = VerticalScrollBarVisibility != ScrollBarVisibility.Disabled && ScrollableHeight > 0f;
        }
        finally
        {
            _isUpdatingBarValue = false;
        }
    }

    private void CoerceOffsets()
    {
        SyncOffsetsFromScrollInfo();
        var ch = MathF.Max(0f, MathF.Min(ScrollableWidth, HorizontalOffset));
        if (!AreClose(HorizontalOffset, ch)) SetValue(HorizontalOffsetProperty, ch);
        var cv = MathF.Max(0f, MathF.Min(ScrollableHeight, VerticalOffset));
        if (!AreClose(VerticalOffset, cv)) SetValue(VerticalOffsetProperty, cv);
        _presenter.HorizontalOffset = HorizontalOffset;
        _presenter.VerticalOffset = VerticalOffset;
    }

    private void SyncScrollBars()
    {
        if (_isUpdatingBarValue) return;
        _isUpdatingBarValue = true;
        try
        {
            _horizontalScrollBar.Value = HorizontalOffset;
            _verticalScrollBar.Value = VerticalOffset;
        }
        finally
        {
            _isUpdatingBarValue = false;
        }
    }

    private void OnHorizontalScrollBarValueChanged(object? sender, RoutedSimpleEventArgs args)
    {
        if (_isUpdatingBarValue) return;
        ScrollToHorizontalOffset(_horizontalScrollBar.Value);
    }

    private void OnVerticalScrollBarValueChanged(object? sender, RoutedSimpleEventArgs args)
    {
        if (_isUpdatingBarValue) return;
        ScrollToVerticalOffset(_verticalScrollBar.Value);
    }

    private void RaiseScrollChangedIfNeeded()
    {
        if (AreClose(_extentWidth, _lastExtentWidth) && AreClose(_extentHeight, _lastExtentHeight) && AreClose(_viewportWidth, _lastViewportWidth) && AreClose(_viewportHeight, _lastViewportHeight) && AreClose(HorizontalOffset, _lastHorizontalOffset) && AreClose(VerticalOffset, _lastVerticalOffset)) return;
        var args = new ScrollChangedEventArgs(ScrollChangedEvent, _extentWidth, _extentHeight, _viewportWidth, _viewportHeight, HorizontalOffset, VerticalOffset, _extentWidth - _lastExtentWidth, _extentHeight - _lastExtentHeight, _viewportWidth - _lastViewportWidth, _viewportHeight - _lastViewportHeight, HorizontalOffset - _lastHorizontalOffset, VerticalOffset - _lastVerticalOffset);
        RaiseRoutedEvent(ScrollChangedEvent, args);
        _lastExtentWidth = _extentWidth;
        _lastExtentHeight = _extentHeight;
        _lastViewportWidth = _viewportWidth;
        _lastViewportHeight = _viewportHeight;
        _lastHorizontalOffset = HorizontalOffset;
        _lastVerticalOffset = VerticalOffset;
    }

    private static bool AreClose(float left, float right) => MathF.Abs(left - right) <= 0.01f;
}
