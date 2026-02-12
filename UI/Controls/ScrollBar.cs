using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class ScrollBar : Control
{
    private static readonly Lazy<Style> DefaultScrollBarStyle = new(BuildDefaultScrollBarStyle);

    public static readonly RoutedEvent ValueChangedEvent =
        new(nameof(ValueChanged), RoutingStrategy.Bubble);

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is ScrollBar scrollBar)
                    {
                        scrollBar.CoerceRangeAndValue();
                    }
                }));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is ScrollBar scrollBar)
                    {
                        scrollBar.CoerceRangeAndValue();
                    }
                }));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ScrollBar scrollBar &&
                        args.OldValue is float oldValue &&
                        args.NewValue is float newValue &&
                        MathF.Abs(oldValue - newValue) > 0.0001f)
                    {
                        scrollBar.RaiseRoutedEvent(ValueChangedEvent, new RoutedSimpleEventArgs(ValueChangedEvent));
                    }
                },
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    var numericValue = value is float v ? v : 0f;
                    if (dependencyObject is not ScrollBar scrollBar)
                    {
                        return numericValue;
                    }

                    var min = MathF.Min(scrollBar.Minimum, scrollBar.Maximum);
                    var max = MathF.Max(scrollBar.Minimum, scrollBar.Maximum);
                    if (numericValue < min)
                    {
                        return min;
                    }

                    if (numericValue > max)
                    {
                        return max;
                    }

                    return numericValue;
                }));

    public static readonly DependencyProperty ViewportSizeProperty =
        DependencyProperty.Register(
            nameof(ViewportSize),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float numeric && numeric >= 0f ? numeric : 0f));

    public static readonly DependencyProperty SmallChangeProperty =
        DependencyProperty.Register(
            nameof(SmallChange),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                16f,
                FrameworkPropertyMetadataOptions.None,
                coerceValueCallback: static (_, value) => value is float numeric && numeric > 0f ? numeric : 1f));

    public static readonly DependencyProperty LargeChangeProperty =
        DependencyProperty.Register(
            nameof(LargeChange),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                64f,
                FrameworkPropertyMetadataOptions.None,
                coerceValueCallback: static (_, value) => value is float numeric && numeric > 0f ? numeric : 1f));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(new Color(22, 22, 22), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(
            nameof(TrackBrush),
            typeof(Color),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(new Color(35, 35, 35), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThumbBrushProperty =
        DependencyProperty.Register(
            nameof(ThumbBrush),
            typeof(Color),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(new Color(118, 118, 118), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(new Color(88, 88, 88), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty =
        DependencyProperty.Register(
            nameof(Thickness),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                16f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 8f ? thickness : 8f));

    public static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDraggingThumbProperty =
        DependencyProperty.Register(
            nameof(IsDraggingThumb),
            typeof(bool),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private bool _isDragging;
    private float _dragOffsetAlongThumb;

    public ScrollBar()
    {
        Focusable = true;
    }

    public event EventHandler<RoutedSimpleEventArgs> ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public float Minimum
    {
        get => GetValue<float>(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public float Maximum
    {
        get => GetValue<float>(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public float Value
    {
        get => GetValue<float>(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public float ViewportSize
    {
        get => GetValue<float>(ViewportSizeProperty);
        set => SetValue(ViewportSizeProperty, value);
    }

    public float SmallChange
    {
        get => GetValue<float>(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    public float LargeChange
    {
        get => GetValue<float>(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color TrackBrush
    {
        get => GetValue<Color>(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Color ThumbBrush
    {
        get => GetValue<Color>(ThumbBrushProperty);
        set => SetValue(ThumbBrushProperty, value);
    }

    public Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public float Thickness
    {
        get => GetValue<float>(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
    }

    public bool IsDraggingThumb
    {
        get => GetValue<bool>(IsDraggingThumbProperty);
        private set => SetValue(IsDraggingThumbProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        if (Orientation == Orientation.Vertical)
        {
            desired.X = MathF.Max(desired.X, Thickness);
            desired.Y = MathF.Max(desired.Y, 32f);
        }
        else
        {
            desired.X = MathF.Max(desired.X, 32f);
            desired.Y = MathF.Max(desired.Y, Thickness);
        }

        return desired;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

        var trackRect = GetTrackRect();
        UiDrawing.DrawFilledRect(spriteBatch, trackRect, TrackBrush, Opacity);
        UiDrawing.DrawRectStroke(spriteBatch, slot, 1f, BorderBrush, Opacity);

        var thumbRect = GetThumbRect(trackRect);
        var thumbColor = IsDraggingThumb
            ? new Color(145, 145, 145)
            : IsMouseOver ? new Color(132, 132, 132) : ThumbBrush;
        UiDrawing.DrawFilledRect(spriteBatch, thumbRect, thumbColor, Opacity);
        UiDrawing.DrawRectStroke(spriteBatch, thumbRect, 1f, BorderBrush, Opacity);
    }

    protected override void OnMouseEnter(RoutedMouseEventArgs args)
    {
        base.OnMouseEnter(args);
        IsMouseOver = true;
    }

    protected override void OnMouseLeave(RoutedMouseEventArgs args)
    {
        base.OnMouseLeave(args);
        IsMouseOver = false;
    }

    protected override void OnMouseLeftButtonDown(RoutedMouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonDown(args);

        if (!IsEnabled)
        {
            return;
        }

        Focus();
        CaptureMouse();

        var trackRect = GetTrackRect();
        var thumbRect = GetThumbRect(trackRect);
        var pointer = Orientation == Orientation.Vertical ? args.Position.Y : args.Position.X;
        var thumbStart = Orientation == Orientation.Vertical ? thumbRect.Y : thumbRect.X;
        var thumbEnd = Orientation == Orientation.Vertical ? thumbRect.Y + thumbRect.Height : thumbRect.X + thumbRect.Width;

        if (pointer >= thumbStart && pointer <= thumbEnd)
        {
            _isDragging = true;
            IsDraggingThumb = true;
            _dragOffsetAlongThumb = pointer - thumbStart;
        }
        else
        {
            Value += pointer < thumbStart ? -LargeChange : LargeChange;
        }

        args.Handled = true;
    }

    protected override void OnMouseMove(RoutedMouseEventArgs args)
    {
        base.OnMouseMove(args);

        if (!_isDragging || !ReferenceEquals(InputManager.MouseCapturedElement, this))
        {
            return;
        }

        var trackRect = GetTrackRect();
        var thumbRect = GetThumbRect(trackRect);
        var trackLength = Orientation == Orientation.Vertical ? trackRect.Height : trackRect.Width;
        var thumbLength = Orientation == Orientation.Vertical ? thumbRect.Height : thumbRect.Width;
        var travel = MathF.Max(1f, trackLength - thumbLength);

        var pointer = Orientation == Orientation.Vertical ? args.Position.Y : args.Position.X;
        var trackStart = Orientation == Orientation.Vertical ? trackRect.Y : trackRect.X;
        var thumbStart = MathF.Max(0f, MathF.Min(travel, pointer - trackStart - _dragOffsetAlongThumb));
        var range = Maximum - Minimum;

        if (range <= 0f)
        {
            Value = Minimum;
        }
        else
        {
            Value = Minimum + ((thumbStart / travel) * range);
        }

        args.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(RoutedMouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonUp(args);

        EndDrag(releaseCapture: true);
        args.Handled = true;
    }

    protected override void OnLostMouseCapture(RoutedMouseCaptureEventArgs args)
    {
        base.OnLostMouseCapture(args);
        EndDrag(releaseCapture: false);
    }

    protected override void OnMouseWheel(RoutedMouseWheelEventArgs args)
    {
        base.OnMouseWheel(args);

        if (!IsEnabled)
        {
            return;
        }

        var steps = args.Delta / 120f;
        Value -= steps * SmallChange;
        args.Handled = true;
    }

    protected override void OnKeyDown(RoutedKeyEventArgs args)
    {
        base.OnKeyDown(args);

        if (!IsEnabled)
        {
            return;
        }

        var handled = false;

        if (Orientation == Orientation.Vertical)
        {
            if (args.Key == Keys.Up)
            {
                Value -= SmallChange;
                handled = true;
            }
            else if (args.Key == Keys.Down)
            {
                Value += SmallChange;
                handled = true;
            }
        }
        else
        {
            if (args.Key == Keys.Left)
            {
                Value -= SmallChange;
                handled = true;
            }
            else if (args.Key == Keys.Right)
            {
                Value += SmallChange;
                handled = true;
            }
        }

        if (args.Key == Keys.PageUp)
        {
            Value -= LargeChange;
            handled = true;
        }
        else if (args.Key == Keys.PageDown)
        {
            Value += LargeChange;
            handled = true;
        }
        else if (args.Key == Keys.Home)
        {
            Value = Minimum;
            handled = true;
        }
        else if (args.Key == Keys.End)
        {
            Value = Maximum;
            handled = true;
        }

        if (handled)
        {
            args.Handled = true;
        }
    }

    protected override Style? GetFallbackStyle()
    {
        return DefaultScrollBarStyle.Value;
    }

    private static Style BuildDefaultScrollBarStyle()
    {
        var style = new Style(typeof(ScrollBar));

        var disabledTrigger = new Trigger(IsEnabledProperty, false);
        disabledTrigger.Setters.Add(new Setter(ThumbBrushProperty, new Color(92, 92, 92)));
        disabledTrigger.Setters.Add(new Setter(BorderBrushProperty, new Color(70, 70, 70)));

        style.Triggers.Add(disabledTrigger);
        return style;
    }

    private LayoutRect GetTrackRect()
    {
        var slot = LayoutSlot;
        return new LayoutRect(slot.X + 1f, slot.Y + 1f, MathF.Max(0f, slot.Width - 2f), MathF.Max(0f, slot.Height - 2f));
    }

    private LayoutRect GetThumbRect(LayoutRect trackRect)
    {
        var range = MathF.Max(0f, Maximum - Minimum);
        var trackLength = Orientation == Orientation.Vertical ? trackRect.Height : trackRect.Width;
        var viewport = MathF.Max(0f, ViewportSize);

        var minThumbLength = MathF.Min(trackLength, 10f);
        var thumbLength = minThumbLength;
        if (range > 0f)
        {
            if (viewport > 0f)
            {
                thumbLength = MathF.Max(minThumbLength, trackLength * (viewport / (viewport + range)));
            }
        }
        else
        {
            thumbLength = trackLength;
        }

        var travel = MathF.Max(0f, trackLength - thumbLength);
        var normalized = range > 0f ? (Value - Minimum) / range : 0f;
        normalized = MathF.Max(0f, MathF.Min(1f, normalized));
        var thumbOffset = travel * normalized;

        if (Orientation == Orientation.Vertical)
        {
            return new LayoutRect(trackRect.X, trackRect.Y + thumbOffset, trackRect.Width, thumbLength);
        }

        return new LayoutRect(trackRect.X + thumbOffset, trackRect.Y, thumbLength, trackRect.Height);
    }

    private void CoerceRangeAndValue()
    {
        var min = MathF.Min(Minimum, Maximum);
        var max = MathF.Max(Minimum, Maximum);

        if (MathF.Abs(min - Minimum) > 0.0001f)
        {
            SetValue(MinimumProperty, min);
        }

        if (MathF.Abs(max - Maximum) > 0.0001f)
        {
            SetValue(MaximumProperty, max);
        }

        Value = MathF.Max(min, MathF.Min(max, Value));
    }

    private void EndDrag(bool releaseCapture)
    {
        _isDragging = false;
        IsDraggingThumb = false;

        if (releaseCapture && ReferenceEquals(InputManager.MouseCapturedElement, this))
        {
            ReleaseMouseCapture();
        }
    }
}
