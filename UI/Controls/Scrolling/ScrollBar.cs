using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

[TemplatePart("PART_Track", typeof(Track))]
[TemplatePart("PART_Thumb", typeof(Thumb))]
[TemplatePart("PART_LineUpButton", typeof(RepeatButton))]
[TemplatePart("PART_LineDownButton", typeof(RepeatButton))]
public class ScrollBar : Control
{
    private const float ValueEpsilon = 0.01f;
    private static readonly Lazy<Style> DefaultScrollBarStyle = new(BuildDefaultScrollBarStyle);
    private Track? _track;
    private Thumb? _thumb;
    private RepeatButton? _lineUpButton;
    private RepeatButton? _lineDownButton;
    private float _thumbDragOriginTravel;
    private float _thumbDragAccumulatedDelta;

    public static readonly RoutedEvent ValueChangedEvent =
        new(nameof(ValueChanged), RoutingStrategy.Bubble);

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                Orientation.Vertical,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is ScrollBar scrollBar)
                    {
                        scrollBar.SyncTrackState();
                        scrollBar.UpdateDefaultLineButtonText();
                    }
                }));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is ScrollBar scrollBar)
                    {
                        scrollBar.CoerceValueWithinRange();
                        scrollBar.SyncTrackState();
                    }
                },
                coerceValueCallback: static (_, value) => value is float numeric && float.IsFinite(numeric) ? numeric : 0f));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is ScrollBar scrollBar)
                    {
                        scrollBar.CoerceValueWithinRange();
                        scrollBar.SyncTrackState();
                    }
                },
                coerceValueCallback: static (_, value) => value is float numeric && float.IsFinite(numeric) ? numeric : 0f));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is not ScrollBar scrollBar ||
                        args.OldValue is not float oldValue ||
                        args.NewValue is not float newValue)
                    {
                        return;
                    }

                    scrollBar.SyncTrackState();
                    if (!AreClose(oldValue, newValue))
                    {
                        scrollBar.RaiseRoutedEvent(ValueChangedEvent, new RoutedSimpleEventArgs(ValueChangedEvent));
                    }
                },
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    var numeric = value is float v && float.IsFinite(v) ? v : 0f;
                    return dependencyObject is ScrollBar scrollBar
                        ? scrollBar.CoerceValue(numeric)
                        : numeric;
                }));

    public static readonly DependencyProperty ViewportSizeProperty =
        DependencyProperty.Register(
            nameof(ViewportSize),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is ScrollBar scrollBar)
                    {
                        scrollBar.CoerceValueWithinRange();
                        scrollBar.SyncTrackState();
                    }
                },
                coerceValueCallback: static (_, value) => value is float numeric && float.IsFinite(numeric) && numeric >= 0f ? numeric : 0f));

    public static readonly DependencyProperty SmallChangeProperty =
        DependencyProperty.Register(
            nameof(SmallChange),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                16f,
                coerceValueCallback: static (_, value) => value is float change && float.IsFinite(change) && change > 0f ? change : 16f));

    public static readonly DependencyProperty LargeChangeProperty =
        DependencyProperty.Register(
            nameof(LargeChange),
            typeof(float),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                32f,
                coerceValueCallback: static (_, value) => value is float change && float.IsFinite(change) && change > 0f ? change : 32f));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(new Color(42, 42, 42)));

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(new Color(112, 112, 112)));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(Color.Transparent));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(Thickness.Empty));

    public ScrollBar()
    {
        Focusable = false;
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

    public new Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public new Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public new Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public new Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public override void OnApplyTemplate()
    {
        DetachTemplatePartHandlers();
        base.OnApplyTemplate();

        _track = GetTemplateChild("PART_Track") as Track;
        _thumb = GetTemplateChild("PART_Thumb") as Thumb;
        _lineUpButton = GetTemplateChild("PART_LineUpButton") as RepeatButton;
        _lineDownButton = GetTemplateChild("PART_LineDownButton") as RepeatButton;

        if (_track == null || _thumb == null || _lineUpButton == null || _lineDownButton == null)
        {
            return;
        }

        EnsureTrackDescendant(_thumb, "PART_Thumb");
        EnsureTrackDescendant(_lineUpButton, "PART_LineUpButton");
        EnsureTrackDescendant(_lineDownButton, "PART_LineDownButton");

        Track.SetPartRole(_lineUpButton, TrackPartRole.DecreaseButton);
        Track.SetPartRole(_thumb, TrackPartRole.Thumb);
        Track.SetPartRole(_lineDownButton, TrackPartRole.IncreaseButton);
        Panel.SetZIndex(_thumb, 1);

        _track.AddHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnTrackMouseDown);
        _thumb.DragStarted += OnThumbDragStarted;
        _thumb.DragDelta += OnThumbDragDelta;
        _thumb.DragCompleted += OnThumbDragCompleted;
        _lineUpButton.Click += OnLineUpButtonClick;
        _lineDownButton.Click += OnLineDownButtonClick;

        UpdateDefaultLineButtonText();
        SyncTrackState();
    }

    protected override Style? GetFallbackStyle()
    {
        return DefaultScrollBarStyle.Value;
    }

    internal LayoutRect GetThumbRectForInput()
    {
        return _track?.GetThumbRect() ?? LayoutSlot;
    }

    private void OnLineUpButtonClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        Value -= ResolveSmallChange();
        args.Handled = true;
    }

    private void OnLineDownButtonClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        Value += ResolveSmallChange();
        args.Handled = true;
    }

    private void OnTrackMouseDown(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (!IsEnabled || args.Button != MouseButton.Left || _track == null)
        {
            return;
        }

        if (IsSameOrDescendantOf(args.OriginalSource, _thumb) ||
            IsSameOrDescendantOf(args.OriginalSource, _lineUpButton) ||
            IsSameOrDescendantOf(args.OriginalSource, _lineDownButton))
        {
            return;
        }

        if (_track.HitTestDecreaseRegion(args.Position))
        {
            Value -= ResolveLargeChange();
            args.Handled = true;
        }
        else if (_track.HitTestIncreaseRegion(args.Position))
        {
            Value += ResolveLargeChange();
            args.Handled = true;
        }
    }

    private void OnThumbDragStarted(object? sender, DragStartedEventArgs args)
    {
        _ = sender;
        _thumbDragOriginTravel = _track?.GetThumbTravel() ?? 0f;
        _thumbDragAccumulatedDelta = 0f;
        args.Handled = true;
    }

    private void OnThumbDragDelta(object? sender, DragDeltaEventArgs args)
    {
        _ = sender;
        if (_track == null)
        {
            return;
        }

        _thumbDragAccumulatedDelta += Orientation == Orientation.Vertical
            ? args.VerticalChange
            : args.HorizontalChange;
        Value = _track.GetValueFromThumbTravel(_thumbDragOriginTravel + _thumbDragAccumulatedDelta);
        args.Handled = true;
    }

    private void OnThumbDragCompleted(object? sender, DragCompletedEventArgs args)
    {
        _ = sender;
        _thumbDragOriginTravel = 0f;
        _thumbDragAccumulatedDelta = 0f;
        args.Handled = true;
    }

    private void SyncTrackState()
    {
        if (_track == null)
        {
            return;
        }

        var coercedValue = CoerceValue(Value);
        if (!AreClose(Value, coercedValue))
        {
            SetValue(ValueProperty, coercedValue);
        }

        SetIfChanged(Track.OrientationProperty, _track, Orientation);
        SetIfChanged(Track.MinimumProperty, _track, Minimum);
        SetIfChanged(Track.MaximumProperty, _track, Maximum);
        SetIfChanged(Track.ValueProperty, _track, coercedValue);
        SetIfChanged(Track.ViewportSizeProperty, _track, ViewportSize);
    }

    private void UpdateDefaultLineButtonText()
    {
        UpdateLineButtonText(_lineUpButton, Orientation == Orientation.Vertical ? "^" : "<");
        UpdateLineButtonText(_lineDownButton, Orientation == Orientation.Vertical ? "v" : ">");
    }

    private static void UpdateLineButtonText(RepeatButton? button, string nextText)
    {
        if (button == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(button.Text) &&
            button.Text is not "^" and not "v" and not "<" and not ">")
        {
            return;
        }

        if (button.Text == nextText)
        {
            return;
        }

        button.Text = nextText;
    }

    private void CoerceValueWithinRange()
    {
        var coerced = CoerceValue(Value);
        if (AreClose(Value, coerced))
        {
            return;
        }

        SetValue(ValueProperty, coerced);
    }

    private float CoerceValue(float value)
    {
        var maxValue = Minimum + GetScrollableRange();
        if (maxValue < Minimum)
        {
            maxValue = Minimum;
        }

        return MathF.Max(Minimum, MathF.Min(maxValue, value));
    }

    private float GetScrollableRange()
    {
        var extent = MathF.Max(0f, Maximum - Minimum);
        return MathF.Max(0f, extent - MathF.Max(0f, ViewportSize));
    }

    private float ResolveSmallChange()
    {
        return MathF.Max(ValueEpsilon, SmallChange);
    }

    private float ResolveLargeChange()
    {
        return MathF.Max(ValueEpsilon, LargeChange);
    }

    private void DetachTemplatePartHandlers()
    {
        if (_track != null)
        {
            _track.RemoveHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnTrackMouseDown);
        }

        if (_thumb != null)
        {
            _thumb.DragStarted -= OnThumbDragStarted;
            _thumb.DragDelta -= OnThumbDragDelta;
            _thumb.DragCompleted -= OnThumbDragCompleted;
        }

        if (_lineUpButton != null)
        {
            _lineUpButton.Click -= OnLineUpButtonClick;
        }

        if (_lineDownButton != null)
        {
            _lineDownButton.Click -= OnLineDownButtonClick;
        }
    }

    private void EnsureTrackDescendant(UIElement element, string partName)
    {
        if (IsSameOrDescendantOf(element, _track))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Template part '{partName}' for '{nameof(ScrollBar)}' must be within '{nameof(Track)}' part 'PART_Track'.");
    }

    private static bool IsSameOrDescendantOf(UIElement? element, UIElement? ancestor)
    {
        if (element == null || ancestor == null)
        {
            return false;
        }

        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static void SetIfChanged<T>(DependencyProperty property, DependencyObject target, T value)
    {
        if (Equals(target.GetValue(property), value))
        {
            return;
        }

        target.SetValue(property, value);
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= ValueEpsilon;
    }

    private static Style BuildDefaultScrollBarStyle()
    {
        var style = new Style(typeof(ScrollBar));
        style.Setters.Add(new Setter(TemplateProperty, BuildDefaultScrollBarTemplate()));
        return style;
    }

    private static ControlTemplate BuildDefaultScrollBarTemplate()
    {
        var template = new ControlTemplate(static owner =>
        {
            var scrollBar = (ScrollBar)owner;
            var track = new Track
            {
                Name = "PART_Track"
            };

            var lineUpButton = CreateDefaultLineButton(
                "PART_LineUpButton",
                scrollBar.Orientation == Orientation.Vertical ? "^" : "<");
            var thumb = new Thumb
            {
                Name = "PART_Thumb"
            };
            var lineDownButton = CreateDefaultLineButton(
                "PART_LineDownButton",
                scrollBar.Orientation == Orientation.Vertical ? "v" : ">");

            Track.SetPartRole(lineUpButton, TrackPartRole.DecreaseButton);
            Track.SetPartRole(thumb, TrackPartRole.Thumb);
            Track.SetPartRole(lineDownButton, TrackPartRole.IncreaseButton);

            track.AddChild(lineUpButton);
            track.AddChild(thumb);
            track.AddChild(lineDownButton);
            return track;
        })
        {
            TargetType = typeof(ScrollBar)
        };

        template.BindTemplate("PART_Track", Panel.BackgroundProperty, BackgroundProperty);
        template.BindTemplate("PART_Track", Track.BorderBrushProperty, BorderBrushProperty);
        template.BindTemplate("PART_Track", Track.BorderThicknessProperty, BorderThicknessProperty);
        template.BindTemplate("PART_Thumb", Thumb.BackgroundProperty, ForegroundProperty);
        template.BindTemplate("PART_Thumb", Thumb.BorderBrushProperty, BorderBrushProperty);
        template.BindTemplate("PART_LineUpButton", Button.ForegroundProperty, ForegroundProperty);
        template.BindTemplate("PART_LineDownButton", Button.ForegroundProperty, ForegroundProperty);

        return template;
    }

    private static RepeatButton CreateDefaultLineButton(string name, string text)
    {
        return new RepeatButton
        {
            Name = name,
            Text = text,
            FontSize = 8f,
            Padding = Thickness.Empty,
            BorderThickness = 0f,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
    }
}
