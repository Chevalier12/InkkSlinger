using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Slider : Control
{
    private static readonly Lazy<Style> DefaultSliderStyle = new(BuildDefaultSliderStyle);

    public static readonly RoutedEvent ValueChangedEvent =
        new(nameof(ValueChanged), RoutingStrategy.Bubble);

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(Slider),
            new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(float),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.CoerceRangeAndValue();
                    }
                }));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(float),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                10f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.CoerceRangeAndValue();
                    }
                }));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(float),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is Slider slider &&
                        args.OldValue is float oldValue &&
                        args.NewValue is float newValue &&
                        MathF.Abs(oldValue - newValue) > 0.0001f)
                    {
                        slider.RaiseRoutedEvent(ValueChangedEvent, new RoutedSimpleEventArgs(ValueChangedEvent));
                    }
                },
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    var numeric = value is float v ? v : 0f;
                    if (dependencyObject is not Slider slider)
                    {
                        return numeric;
                    }

                    var min = MathF.Min(slider.Minimum, slider.Maximum);
                    var max = MathF.Max(slider.Minimum, slider.Maximum);
                    numeric = MathF.Max(min, MathF.Min(max, numeric));
                    return slider.SnapValueIfNeeded(numeric);
                }));

    public static readonly DependencyProperty SmallChangeProperty =
        DependencyProperty.Register(
            nameof(SmallChange),
            typeof(float),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                0.1f,
                FrameworkPropertyMetadataOptions.None,
                coerceValueCallback: static (_, value) => value is float change && change > 0f ? change : 0.1f));

    public static readonly DependencyProperty LargeChangeProperty =
        DependencyProperty.Register(
            nameof(LargeChange),
            typeof(float),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.None,
                coerceValueCallback: static (_, value) => value is float change && change > 0f ? change : 1f));

    public static readonly DependencyProperty IsSnapToTickEnabledProperty =
        DependencyProperty.Register(
            nameof(IsSnapToTickEnabled),
            typeof(bool),
            typeof(Slider),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty TickFrequencyProperty =
        DependencyProperty.Register(
            nameof(TickFrequency),
            typeof(float),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.None,
                coerceValueCallback: static (_, value) => value is float frequency && frequency > 0f ? frequency : 1f));

    public static readonly DependencyProperty IsMoveToPointEnabledProperty =
        DependencyProperty.Register(
            nameof(IsMoveToPointEnabled),
            typeof(bool),
            typeof(Slider),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty TrackThicknessProperty =
        DependencyProperty.Register(
            nameof(TrackThickness),
            typeof(float),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                4f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 1f ? thickness : 1f));

    public static readonly DependencyProperty ThumbSizeProperty =
        DependencyProperty.Register(
            nameof(ThumbSize),
            typeof(float),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                14f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float size && size >= 6f ? size : 6f));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(Slider),
            new FrameworkPropertyMetadata(new Color(20, 20, 20), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(
            nameof(TrackBrush),
            typeof(Color),
            typeof(Slider),
            new FrameworkPropertyMetadata(new Color(62, 62, 62), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThumbBrushProperty =
        DependencyProperty.Register(
            nameof(ThumbBrush),
            typeof(Color),
            typeof(Slider),
            new FrameworkPropertyMetadata(new Color(140, 140, 140), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Slider),
            new FrameworkPropertyMetadata(new Color(100, 100, 100), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(Slider),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDraggingThumbProperty =
        DependencyProperty.Register(
            nameof(IsDraggingThumb),
            typeof(bool),
            typeof(Slider),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));


    public Slider()
    {
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

    public bool IsSnapToTickEnabled
    {
        get => GetValue<bool>(IsSnapToTickEnabledProperty);
        set => SetValue(IsSnapToTickEnabledProperty, value);
    }

    public float TickFrequency
    {
        get => GetValue<float>(TickFrequencyProperty);
        set => SetValue(TickFrequencyProperty, value);
    }

    public bool IsMoveToPointEnabled
    {
        get => GetValue<bool>(IsMoveToPointEnabledProperty);
        set => SetValue(IsMoveToPointEnabledProperty, value);
    }

    public float TrackThickness
    {
        get => GetValue<float>(TrackThicknessProperty);
        set => SetValue(TrackThicknessProperty, value);
    }

    public float ThumbSize
    {
        get => GetValue<float>(ThumbSizeProperty);
        set => SetValue(ThumbSizeProperty, value);
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
        if (Orientation == Orientation.Horizontal)
        {
            desired.X = MathF.Max(desired.X, 120f);
            desired.Y = MathF.Max(desired.Y, MathF.Max(TrackThickness + 6f, ThumbSize));
        }
        else
        {
            desired.X = MathF.Max(desired.X, MathF.Max(TrackThickness + 6f, ThumbSize));
            desired.Y = MathF.Max(desired.Y, 120f);
        }

        return desired;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);
        UiDrawing.DrawRectStroke(spriteBatch, slot, 1f, BorderBrush, Opacity);

        var trackRect = GetTrackRect();
        UiDrawing.DrawFilledRect(spriteBatch, trackRect, TrackBrush, Opacity);

        var thumbRect = GetThumbRect(trackRect);
        var thumbColor = IsDraggingThumb
            ? new Color(164, 164, 164)
            : IsMouseOver ? new Color(152, 152, 152) : ThumbBrush;
        UiDrawing.DrawFilledRect(spriteBatch, thumbRect, thumbColor, Opacity);
        UiDrawing.DrawRectStroke(spriteBatch, thumbRect, 1f, BorderBrush, Opacity);
    }









    protected override Style? GetFallbackStyle()
    {
        return DefaultSliderStyle.Value;
    }

    private static Style BuildDefaultSliderStyle()
    {
        var style = new Style(typeof(Slider));

        var disabled = new Trigger(IsEnabledProperty, false);
        disabled.Setters.Add(new Setter(ThumbBrushProperty, new Color(96, 96, 96)));
        disabled.Setters.Add(new Setter(TrackBrushProperty, new Color(52, 52, 52)));
        style.Triggers.Add(disabled);

        return style;
    }

    private LayoutRect GetTrackRect()
    {
        var slot = LayoutSlot;
        if (Orientation == Orientation.Horizontal)
        {
            var trackY = slot.Y + ((slot.Height - TrackThickness) / 2f);
            var inset = ThumbSize / 2f;
            return new LayoutRect(slot.X + inset, trackY, MathF.Max(0f, slot.Width - (inset * 2f)), TrackThickness);
        }

        var trackX = slot.X + ((slot.Width - TrackThickness) / 2f);
        var verticalInset = ThumbSize / 2f;
        return new LayoutRect(trackX, slot.Y + verticalInset, TrackThickness, MathF.Max(0f, slot.Height - (verticalInset * 2f)));
    }

    private LayoutRect GetThumbRect(LayoutRect trackRect)
    {
        var normalized = GetNormalizedValue();
        if (Orientation == Orientation.Horizontal)
        {
            var centerX = trackRect.X + (trackRect.Width * normalized);
            return new LayoutRect(
                centerX - (ThumbSize / 2f),
                LayoutSlot.Y + ((LayoutSlot.Height - ThumbSize) / 2f),
                ThumbSize,
                ThumbSize);
        }

        var centerY = trackRect.Y + (trackRect.Height * (1f - normalized));
        return new LayoutRect(
            LayoutSlot.X + ((LayoutSlot.Width - ThumbSize) / 2f),
            centerY - (ThumbSize / 2f),
            ThumbSize,
            ThumbSize);
    }

    private float GetPointerAlongOrientation(Vector2 pointerPosition)
    {
        return Orientation == Orientation.Horizontal ? pointerPosition.X : pointerPosition.Y;
    }

    private float GetThumbStartAlongOrientation(LayoutRect thumbRect)
    {
        return Orientation == Orientation.Horizontal ? thumbRect.X : thumbRect.Y;
    }

    private float GetThumbEndAlongOrientation(LayoutRect thumbRect)
    {
        return Orientation == Orientation.Horizontal ? thumbRect.X + thumbRect.Width : thumbRect.Y + thumbRect.Height;
    }

    private float ValueFromPointer(float pointer, LayoutRect trackRect, bool useThumbCenterOffset)
    {
        var range = MathF.Max(0f, Maximum - Minimum);
        if (range <= 0f)
        {
            return Minimum;
        }

        var normalized = 0f;
        if (Orientation == Orientation.Horizontal)
        {
            var trackStart = trackRect.X;
            var travel = MathF.Max(1f, trackRect.Width);
            var centerAdjust = useThumbCenterOffset ? ThumbSize / 2f : 0f;
            normalized = (pointer + centerAdjust - trackStart) / travel;
        }
        else
        {
            var trackStart = trackRect.Y;
            var travel = MathF.Max(1f, trackRect.Height);
            var centerAdjust = useThumbCenterOffset ? ThumbSize / 2f : 0f;
            normalized = 1f - ((pointer + centerAdjust - trackStart) / travel);
        }

        normalized = MathF.Max(0f, MathF.Min(1f, normalized));
        return Minimum + (range * normalized);
    }

    private float GetNormalizedValue()
    {
        var range = Maximum - Minimum;
        if (range <= 0f)
        {
            return 0f;
        }

        var normalized = (Value - Minimum) / range;
        return MathF.Max(0f, MathF.Min(1f, normalized));
    }

    private float SnapValueIfNeeded(float value)
    {
        if (!IsSnapToTickEnabled || TickFrequency <= 0f)
        {
            return value;
        }

        var offset = value - Minimum;
        var snapped = Minimum + (MathF.Round(offset / TickFrequency) * TickFrequency);
        var min = MathF.Min(Minimum, Maximum);
        var max = MathF.Max(Minimum, Maximum);
        return MathF.Max(min, MathF.Min(max, snapped));
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
        IsDraggingThumb = false;
        _ = releaseCapture;
    }
}
