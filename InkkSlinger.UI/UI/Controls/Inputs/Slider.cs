using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

[TemplatePart("PART_Root", typeof(Grid))]
[TemplatePart("PART_Track", typeof(Track))]
[TemplatePart("PART_Thumb", typeof(Thumb))]
[TemplatePart("PART_DecreaseButton", typeof(RepeatButton))]
[TemplatePart("PART_IncreaseButton", typeof(RepeatButton))]
[TemplatePart("PART_SelectionRange", typeof(FrameworkElement))]
[TemplatePart("PART_TopTickBar", typeof(TickBar))]
[TemplatePart("PART_BottomTickBar", typeof(TickBar))]
[TemplatePart("PART_LeftTickBar", typeof(TickBar))]
[TemplatePart("PART_RightTickBar", typeof(TickBar))]
public class Slider : RangeBase
{
    private static readonly Lazy<Style> DefaultSliderStyle = new(BuildDefaultSliderStyle);

    private Grid? _root;
    private Track? _track;
    private Thumb? _thumb;
    private RepeatButton? _decreaseButton;
    private RepeatButton? _increaseButton;
    private FrameworkElement? _selectionRangeElement;
    private TickBar? _topTickBar;
    private TickBar? _bottomTickBar;
    private TickBar? _leftTickBar;
    private TickBar? _rightTickBar;
    private ToolTip? _autoToolTip;
    private TextBlock? _autoToolTipText;
    private Vector2 _lastTrackPressPosition;
    private float _thumbDragOriginTravel;
    private float _thumbDragAccumulatedDelta;

    public static readonly RoutedUICommand DecreaseLarge = new(text: "Decrease Large", name: nameof(DecreaseLarge), ownerType: typeof(Slider));
    public static readonly RoutedUICommand IncreaseLarge = new(text: "Increase Large", name: nameof(IncreaseLarge), ownerType: typeof(Slider));
    public static readonly RoutedUICommand DecreaseSmall = new(text: "Decrease Small", name: nameof(DecreaseSmall), ownerType: typeof(Slider));
    public static readonly RoutedUICommand IncreaseSmall = new(text: "Increase Small", name: nameof(IncreaseSmall), ownerType: typeof(Slider));
    public static readonly RoutedUICommand MinimizeValue = new(text: "Minimize Value", name: nameof(MinimizeValue), ownerType: typeof(Slider));
    public static readonly RoutedUICommand MaximizeValue = new(text: "Maximize Value", name: nameof(MaximizeValue), ownerType: typeof(Slider));

    public new static readonly RoutedEvent ValueChangedEvent = RangeBase.ValueChangedEvent;
    public new static readonly DependencyProperty MinimumProperty = RangeBase.MinimumProperty;
    public new static readonly DependencyProperty MaximumProperty = RangeBase.MaximumProperty;
    public new static readonly DependencyProperty ValueProperty = RangeBase.ValueProperty;
    public new static readonly DependencyProperty SmallChangeProperty = RangeBase.SmallChangeProperty;
    public new static readonly DependencyProperty LargeChangeProperty = RangeBase.LargeChangeProperty;
    public new static readonly DependencyProperty BackgroundProperty = Control.BackgroundProperty;
    public new static readonly DependencyProperty ForegroundProperty = Control.ForegroundProperty;
    public new static readonly DependencyProperty BorderBrushProperty = Control.BorderBrushProperty;
    public new static readonly DependencyProperty BorderThicknessProperty = Control.BorderThicknessProperty;

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                Orientation.Horizontal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.OnOrientationOrDirectionChanged();
                    }
                }));

    public static readonly DependencyProperty IsDirectionReversedProperty =
        DependencyProperty.Register(
            nameof(IsDirectionReversed),
            typeof(bool),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.OnOrientationOrDirectionChanged();
                    }
                }));

    public static readonly DependencyProperty IsSnapToTickEnabledProperty =
        DependencyProperty.Register(
            nameof(IsSnapToTickEnabled),
            typeof(bool),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                false,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.CoerceCurrentValue();
                        slider.SyncTemplateState();
                    }
                }));

    public static readonly DependencyProperty TickFrequencyProperty =
        DependencyProperty.Register(
            nameof(TickFrequency),
            typeof(float),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                1f,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.CoerceCurrentValue();
                        slider.SyncTemplateState();
                    }
                },
                coerceValueCallback: static (_, value) => value is float frequency && frequency > 0f ? frequency : 1f));

    public static readonly DependencyProperty TicksProperty =
        DependencyProperty.Register(
            nameof(Ticks),
            typeof(DoubleCollection),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                null,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.OnTicksPropertyChanged(args.OldValue as DoubleCollection, args.NewValue as DoubleCollection);
                    }
                }));

    public static readonly DependencyProperty IsMoveToPointEnabledProperty =
        DependencyProperty.Register(
            nameof(IsMoveToPointEnabled),
            typeof(bool),
            typeof(Slider),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty DelayProperty =
        DependencyProperty.Register(
            nameof(Delay),
            typeof(int),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                250,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.UpdateRepeatButtonState();
                    }
                },
                coerceValueCallback: static (_, value) => value is int delay && delay >= 0 ? delay : 250));

    public static readonly DependencyProperty IntervalProperty =
        DependencyProperty.Register(
            nameof(Interval),
            typeof(int),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                100,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.UpdateRepeatButtonState();
                    }
                },
                coerceValueCallback: static (_, value) => value is int interval && interval > 0 ? interval : 100));

    public static readonly DependencyProperty TickPlacementProperty =
        DependencyProperty.Register(
            nameof(TickPlacement),
            typeof(TickPlacement),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                TickPlacement.None,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.UpdateTemplateLayout();
                        slider.SyncTemplateState();
                    }
                }));

    public static readonly DependencyProperty AutoToolTipPlacementProperty =
        DependencyProperty.Register(
            nameof(AutoToolTipPlacement),
            typeof(AutoToolTipPlacement),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                AutoToolTipPlacement.None,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.UpdateAutoToolTip();
                    }
                }));

    public static readonly DependencyProperty AutoToolTipPrecisionProperty =
        DependencyProperty.Register(
            nameof(AutoToolTipPrecision),
            typeof(int),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                0,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.UpdateAutoToolTip();
                    }
                },
                coerceValueCallback: static (_, value) => value is int precision && precision >= 0 ? precision : 0));

    public static readonly DependencyProperty IsSelectionRangeEnabledProperty =
        DependencyProperty.Register(
            nameof(IsSelectionRangeEnabled),
            typeof(bool),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.UpdateSelectionRangeVisual();
                    }
                }));

    public static readonly DependencyProperty SelectionStartProperty =
        DependencyProperty.Register(
            nameof(SelectionStart),
            typeof(float),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.UpdateSelectionRangeVisual();
                    }
                },
                coerceValueCallback: static (dependencyObject, value) =>
                    dependencyObject is Slider slider && value is float numeric ? slider.ConstrainToRange(numeric) : 0f));

    public static readonly DependencyProperty SelectionEndProperty =
        DependencyProperty.Register(
            nameof(SelectionEnd),
            typeof(float),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                0f,
                FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.UpdateSelectionRangeVisual();
                    }
                },
                coerceValueCallback: static (dependencyObject, value) =>
                    dependencyObject is Slider slider && value is float numeric ? slider.ConstrainToRange(numeric) : 0f));

    public static readonly DependencyProperty TrackThicknessProperty =
        DependencyProperty.Register(
            nameof(TrackThickness),
            typeof(float),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                4f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.SyncTemplateState();
                    }
                },
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 1f ? thickness : 1f));

    public static readonly DependencyProperty ThumbSizeProperty =
        DependencyProperty.Register(
            nameof(ThumbSize),
            typeof(float),
            typeof(Slider),
            new FrameworkPropertyMetadata(
                14f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Slider slider)
                    {
                        slider.SyncTemplateState();
                    }
                },
                coerceValueCallback: static (_, value) => value is float size && size >= 6f ? size : 6f));

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(
            nameof(TrackBrush),
            typeof(Color),
            typeof(Slider),
            new FrameworkPropertyMetadata(new Color(62, 62, 62)));

    public static readonly DependencyProperty ThumbBrushProperty =
        DependencyProperty.Register(
            nameof(ThumbBrush),
            typeof(Color),
            typeof(Slider),
            new FrameworkPropertyMetadata(new Color(140, 140, 140)));

    public static readonly DependencyProperty SelectionRangeBrushProperty =
        DependencyProperty.Register(
            nameof(SelectionRangeBrush),
            typeof(Color),
            typeof(Slider),
            new FrameworkPropertyMetadata(new Color(66, 124, 211)));

    public static readonly DependencyProperty IsDraggingThumbProperty =
        DependencyProperty.Register(
            nameof(IsDraggingThumb),
            typeof(bool),
            typeof(Slider),
            new FrameworkPropertyMetadata(false));

    static Slider()
    {
        MinimumProperty.OverrideMetadata(typeof(Slider), CreateDerivedMetadata(MinimumProperty, 0f, FrameworkPropertyMetadataOptions.AffectsArrange));
        MaximumProperty.OverrideMetadata(typeof(Slider), CreateDerivedMetadata(MaximumProperty, 10f, FrameworkPropertyMetadataOptions.AffectsArrange));
        ValueProperty.OverrideMetadata(typeof(Slider), CreateDerivedMetadata(ValueProperty, 0f, FrameworkPropertyMetadataOptions.AffectsArrange));
        SmallChangeProperty.OverrideMetadata(typeof(Slider), CreateDerivedMetadata(SmallChangeProperty, 1f, FrameworkPropertyMetadataOptions.None));
        LargeChangeProperty.OverrideMetadata(typeof(Slider), CreateDerivedMetadata(LargeChangeProperty, 1f, FrameworkPropertyMetadataOptions.None));
        BackgroundProperty.OverrideMetadata(typeof(Slider), new FrameworkPropertyMetadata(new Color(20, 20, 20)));
        BorderBrushProperty.OverrideMetadata(typeof(Slider), new FrameworkPropertyMetadata(new Color(100, 100, 100)));
    }

    public Slider()
    {
        Focusable = true;
        RegisterCommandBindings();
        RefreshInputBindings();
    }

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public bool IsDirectionReversed
    {
        get => GetValue<bool>(IsDirectionReversedProperty);
        set => SetValue(IsDirectionReversedProperty, value);
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

    public DoubleCollection Ticks
    {
        get
        {
            var ticks = GetValue<DoubleCollection>(TicksProperty);
            if (ticks != null)
            {
                return ticks;
            }

            ticks = new DoubleCollection();
            SetValue(TicksProperty, ticks);
            return ticks;
        }
        set => SetValue(TicksProperty, value);
    }

    public bool IsMoveToPointEnabled
    {
        get => GetValue<bool>(IsMoveToPointEnabledProperty);
        set => SetValue(IsMoveToPointEnabledProperty, value);
    }

    public int Delay
    {
        get => GetValue<int>(DelayProperty);
        set => SetValue(DelayProperty, value);
    }

    public int Interval
    {
        get => GetValue<int>(IntervalProperty);
        set => SetValue(IntervalProperty, value);
    }

    public TickPlacement TickPlacement
    {
        get => GetValue<TickPlacement>(TickPlacementProperty);
        set => SetValue(TickPlacementProperty, value);
    }

    public AutoToolTipPlacement AutoToolTipPlacement
    {
        get => GetValue<AutoToolTipPlacement>(AutoToolTipPlacementProperty);
        set => SetValue(AutoToolTipPlacementProperty, value);
    }

    public int AutoToolTipPrecision
    {
        get => GetValue<int>(AutoToolTipPrecisionProperty);
        set => SetValue(AutoToolTipPrecisionProperty, value);
    }

    public bool IsSelectionRangeEnabled
    {
        get => GetValue<bool>(IsSelectionRangeEnabledProperty);
        set => SetValue(IsSelectionRangeEnabledProperty, value);
    }

    public float SelectionStart
    {
        get => GetValue<float>(SelectionStartProperty);
        set => SetValue(SelectionStartProperty, value);
    }

    public float SelectionEnd
    {
        get => GetValue<float>(SelectionEndProperty);
        set => SetValue(SelectionEndProperty, value);
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

    public Color SelectionRangeBrush
    {
        get => GetValue<Color>(SelectionRangeBrushProperty);
        set => SetValue(SelectionRangeBrushProperty, value);
    }

    public bool IsDraggingThumb
    {
        get => GetValue<bool>(IsDraggingThumbProperty);
        private set => SetValue(IsDraggingThumbProperty, value);
    }

    public override void OnApplyTemplate()
    {
        DetachTemplatePartHandlers();
        base.OnApplyTemplate();

        _root = GetTemplateChild("PART_Root") as Grid;
        _track = GetTemplateChild("PART_Track") as Track;
        _thumb = GetTemplateChild("PART_Thumb") as Thumb;
        _decreaseButton = GetTemplateChild("PART_DecreaseButton") as RepeatButton;
        _increaseButton = GetTemplateChild("PART_IncreaseButton") as RepeatButton;
        _selectionRangeElement = GetTemplateChild("PART_SelectionRange") as FrameworkElement;
        _topTickBar = GetTemplateChild("PART_TopTickBar") as TickBar;
        _bottomTickBar = GetTemplateChild("PART_BottomTickBar") as TickBar;
        _leftTickBar = GetTemplateChild("PART_LeftTickBar") as TickBar;
        _rightTickBar = GetTemplateChild("PART_RightTickBar") as TickBar;

        if (_track == null || _thumb == null || _decreaseButton == null || _increaseButton == null)
        {
            return;
        }

        EnsureTrackDescendant(_thumb, "PART_Thumb");
        EnsureTrackDescendant(_decreaseButton, "PART_DecreaseButton");
        EnsureTrackDescendant(_increaseButton, "PART_IncreaseButton");
        if (_selectionRangeElement != null)
        {
            EnsureTrackDescendant(_selectionRangeElement, "PART_SelectionRange");
        }

        Track.SetPartRole(_decreaseButton, TrackPartRole.DecreaseButton);
        Track.SetPartRole(_thumb, TrackPartRole.Thumb);
        Track.SetPartRole(_increaseButton, TrackPartRole.IncreaseButton);
        Panel.SetZIndex(_thumb, 1);

        _thumb.DragStarted += OnThumbDragStarted;
        _thumb.DragDelta += OnThumbDragDelta;
        _thumb.DragCompleted += OnThumbDragCompleted;
        _track.LayoutUpdated += OnTrackLayoutUpdated;
        _track.AddHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnTrackMouseDown);
        _decreaseButton.Click += OnDecreaseButtonClick;
        _increaseButton.Click += OnIncreaseButtonClick;
        _decreaseButton.AddHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnTrackButtonMouseDown);
        _increaseButton.AddHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnTrackButtonMouseDown);
        _thumb.AddHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnThumbMouseDown);

        UpdateTemplateLayout();
        SyncTemplateState();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        if (Orientation == Orientation.Horizontal)
        {
            desired.X = MathF.Max(desired.X, 120f);
            desired.Y = MathF.Max(desired.Y, MathF.Max(ThumbSize, TrackThickness) + 12f);
        }
        else
        {
            desired.X = MathF.Max(desired.X, MathF.Max(ThumbSize, TrackThickness) + 12f);
            desired.Y = MathF.Max(desired.Y, 120f);
        }

        return desired;
    }

    protected override Style? GetFallbackStyle()
    {
        return DefaultSliderStyle.Value;
    }

    protected override void OnMinimumChanged(float oldMinimum, float newMinimum)
    {
        base.OnMinimumChanged(oldMinimum, newMinimum);
        CoerceAuxiliaryValues();
        SyncTemplateState();
    }

    protected override void OnMaximumChanged(float oldMaximum, float newMaximum)
    {
        base.OnMaximumChanged(oldMaximum, newMaximum);
        CoerceAuxiliaryValues();
        SyncTemplateState();
    }

    protected override void OnValueChanged(float oldValue, float newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        SyncTemplateState();
        UpdateAutoToolTip();
    }

    protected override float CoerceValueCore(float value)
    {
        return SnapValueIfNeeded(ConstrainToRange(value));
    }

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        if (!HitTest(pointerPosition) || _track == null)
        {
            return false;
        }

        FocusManager.SetFocus(this);
        _lastTrackPressPosition = pointerPosition;
        MoveToPoint(pointerPosition);
        return true;
    }

    internal void HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        if (!IsDraggingThumb || _track == null)
        {
            return;
        }

        _ = pointerPosition;
        Value = _track.GetValueFromThumbTravel(_thumbDragOriginTravel + _thumbDragAccumulatedDelta);
    }

    internal void HandlePointerUpFromInput()
    {
        if (!IsDraggingThumb)
        {
            return;
        }

        EndThumbDrag();
    }

    private void RegisterCommandBindings()
    {
        CommandBindings.Add(new CommandBinding(DecreaseSmall, (_, _) => ChangeValueBy(-ResolveSmallChange()), (_, args) => args.CanExecute = IsEnabled));
        CommandBindings.Add(new CommandBinding(IncreaseSmall, (_, _) => ChangeValueBy(ResolveSmallChange()), (_, args) => args.CanExecute = IsEnabled));
        CommandBindings.Add(new CommandBinding(DecreaseLarge, (_, _) => ChangeValueBy(-ResolveLargeChange()), (_, args) => args.CanExecute = IsEnabled));
        CommandBindings.Add(new CommandBinding(IncreaseLarge, (_, _) => ChangeValueBy(ResolveLargeChange()), (_, args) => args.CanExecute = IsEnabled));
        CommandBindings.Add(new CommandBinding(MinimizeValue, (_, _) => Value = Minimum, (_, args) => args.CanExecute = IsEnabled));
        CommandBindings.Add(new CommandBinding(MaximizeValue, (_, _) => Value = Maximum, (_, args) => args.CanExecute = IsEnabled));
    }

    private void RefreshInputBindings()
    {
        InputBindings.Clear();

        var decreaseKey = Orientation == Orientation.Horizontal
            ? (IsDirectionReversed ? Keys.Right : Keys.Left)
            : (IsDirectionReversed ? Keys.Up : Keys.Down);
        var increaseKey = Orientation == Orientation.Horizontal
            ? (IsDirectionReversed ? Keys.Left : Keys.Right)
            : (IsDirectionReversed ? Keys.Down : Keys.Up);

        AddInputBinding(decreaseKey, DecreaseSmall);
        AddInputBinding(increaseKey, IncreaseSmall);
        AddInputBinding(Keys.PageDown, DecreaseLarge);
        AddInputBinding(Keys.PageUp, IncreaseLarge);
        AddInputBinding(Keys.Home, MinimizeValue);
        AddInputBinding(Keys.End, MaximizeValue);
    }

    private void AddInputBinding(Keys key, System.Windows.Input.ICommand command)
    {
        InputBindings.Add(new KeyBinding
        {
            Key = key,
            Modifiers = ModifierKeys.None,
            Command = command
        });
    }

    private void OnOrientationOrDirectionChanged()
    {
        RefreshInputBindings();
        UpdateTemplateLayout();
        SyncTemplateState();
    }

    private void OnTicksPropertyChanged(DoubleCollection? oldTicks, DoubleCollection? newTicks)
    {
        if (oldTicks != null)
        {
            oldTicks.Changed -= OnTicksCollectionChanged;
        }

        if (newTicks != null)
        {
            newTicks.Changed += OnTicksCollectionChanged;
        }

        CoerceCurrentValue();
        SyncTemplateState();
    }

    private void OnTicksCollectionChanged()
    {
        CoerceCurrentValue();
        SyncTemplateState();
    }

    private void CoerceCurrentValue()
    {
        var coercedValue = CoerceValueCore(Value);
        if (!FloatValuesAreClose(coercedValue, Value))
        {
            SetValue(ValueProperty, coercedValue);
        }
    }

    private void CoerceAuxiliaryValues()
    {
        var coercedStart = ConstrainToRange(SelectionStart);
        if (!FloatValuesAreClose(coercedStart, SelectionStart))
        {
            SetValue(SelectionStartProperty, coercedStart);
        }

        var coercedEnd = ConstrainToRange(SelectionEnd);
        if (!FloatValuesAreClose(coercedEnd, SelectionEnd))
        {
            SetValue(SelectionEndProperty, coercedEnd);
        }

        CoerceCurrentValue();
    }

    private void ChangeValueBy(float delta)
    {
        Value += delta;
    }

    private float ResolveSmallChange()
    {
        return SmallChange > 0f ? SmallChange : 1f;
    }

    private float ResolveLargeChange()
    {
        return LargeChange > 0f ? LargeChange : ResolveSmallChange();
    }

    private float SnapValueIfNeeded(float value)
    {
        if (!IsSnapToTickEnabled)
        {
            return value;
        }

        var bestValue = Minimum;
        var bestDistance = float.PositiveInfinity;

        EvaluateCandidate(Minimum, ref bestValue, ref bestDistance, value);
        EvaluateCandidate(Maximum, ref bestValue, ref bestDistance, value);

        var ticks = GetValue<DoubleCollection>(TicksProperty);
        if (ticks != null && ticks.Count > 0)
        {
            for (var index = 0; index < ticks.Count; index++)
            {
                EvaluateCandidate((float)ticks[index], ref bestValue, ref bestDistance, value);
            }
        }
        else if (TickFrequency > 0f)
        {
            var candidate = Minimum;
            while (candidate <= Maximum + 0.0001f)
            {
                EvaluateCandidate(candidate, ref bestValue, ref bestDistance, value);
                candidate += TickFrequency;
            }
        }

        return ConstrainToRange(bestValue);
    }

    private static void EvaluateCandidate(float candidate, ref float bestValue, ref float bestDistance, float target)
    {
        var distance = MathF.Abs(candidate - target);
        if (distance < bestDistance)
        {
            bestDistance = distance;
            bestValue = candidate;
        }
    }

    private void SyncTemplateState()
    {
        if (_track == null)
        {
            return;
        }

        SetIfChanged(Track.OrientationProperty, _track, Orientation);
        SetIfChanged(Track.MinimumProperty, _track, Minimum);
        SetIfChanged(Track.MaximumProperty, _track, Maximum);
        SetIfChanged(Track.ValueProperty, _track, Value);
        SetIfChanged(Track.IsViewportSizedThumbProperty, _track, false);
        SetIfChanged(Track.IsDirectionReversedProperty, _track, GetEffectiveTrackDirectionReversed());
        SetIfChanged(Track.ThumbLengthProperty, _track, ThumbSize);
        SetIfChanged(Track.ThumbMinLengthProperty, _track, ThumbSize);
        SetIfChanged(Track.TrackThicknessProperty, _track, TrackThickness);

        if (_thumb != null)
        {
            _thumb.Width = ThumbSize;
            _thumb.Height = ThumbSize;
        }

        UpdateRepeatButtonState();
        UpdateTickBarState();
        RefreshTrackLayoutIfPossible();
        UpdateSelectionRangeVisual();
    }

    private void UpdateRepeatButtonState()
    {
        UpdateRepeatButton(_decreaseButton);
        UpdateRepeatButton(_increaseButton);
    }

    private void UpdateRepeatButton(RepeatButton? button)
    {
        if (button == null)
        {
            return;
        }

        button.Focusable = false;
        button.RepeatDelay = Delay / 1000f;
        button.RepeatInterval = Interval / 1000f;
    }

    private void UpdateTickBarState()
    {
        UpdateTickBar(_topTickBar, TickBarPlacement.Top);
        UpdateTickBar(_bottomTickBar, TickBarPlacement.Bottom);
        UpdateTickBar(_leftTickBar, TickBarPlacement.Left);
        UpdateTickBar(_rightTickBar, TickBarPlacement.Right);
    }

    private void UpdateTickBar(TickBar? tickBar, TickBarPlacement placement)
    {
        if (tickBar == null)
        {
            return;
        }

        tickBar.Placement = placement;
        tickBar.Minimum = Minimum;
        tickBar.Maximum = Maximum;
        tickBar.TickFrequency = TickFrequency;
        tickBar.Ticks = GetValue<DoubleCollection>(TicksProperty) ?? Ticks;
        tickBar.ReservedSpace = ThumbSize;
        tickBar.IsDirectionReversed = GetEffectiveTrackDirectionReversed();
    }

    private void UpdateTemplateLayout()
    {
        if (_root == null || _track == null)
        {
            return;
        }

        _root.RowDefinitions.Clear();
        _root.ColumnDefinitions.Clear();

        if (Orientation == Orientation.Horizontal)
        {
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1f, GridUnitType.Star) });
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });

            Grid.SetRow(_track, 1);
            Grid.SetColumn(_track, 0);
            _track.Height = MathF.Max(ThumbSize, TrackThickness);
            _track.Width = float.NaN;

            ConfigureHorizontalTickBar(_topTickBar, row: 0, visible: TickPlacement is TickPlacement.TopLeft or TickPlacement.Both);
            ConfigureHorizontalTickBar(_bottomTickBar, row: 2, visible: TickPlacement is TickPlacement.BottomRight or TickPlacement.Both);
            ConfigureCollapsedTickBar(_leftTickBar);
            ConfigureCollapsedTickBar(_rightTickBar);
        }
        else
        {
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1f, GridUnitType.Star) });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetRow(_track, 0);
            Grid.SetColumn(_track, 1);
            _track.Width = MathF.Max(ThumbSize, TrackThickness);
            _track.Height = float.NaN;

            ConfigureVerticalTickBar(_leftTickBar, column: 0, visible: TickPlacement is TickPlacement.TopLeft or TickPlacement.Both);
            ConfigureVerticalTickBar(_rightTickBar, column: 2, visible: TickPlacement is TickPlacement.BottomRight or TickPlacement.Both);
            ConfigureCollapsedTickBar(_topTickBar);
            ConfigureCollapsedTickBar(_bottomTickBar);
        }
    }

    private static void ConfigureHorizontalTickBar(TickBar? tickBar, int row, bool visible)
    {
        if (tickBar == null)
        {
            return;
        }

        Grid.SetRow(tickBar, row);
        Grid.SetColumn(tickBar, 0);
        tickBar.Height = 8f;
        tickBar.Width = float.NaN;
        tickBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void ConfigureVerticalTickBar(TickBar? tickBar, int column, bool visible)
    {
        if (tickBar == null)
        {
            return;
        }

        Grid.SetRow(tickBar, 0);
        Grid.SetColumn(tickBar, column);
        tickBar.Width = 8f;
        tickBar.Height = float.NaN;
        tickBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void ConfigureCollapsedTickBar(TickBar? tickBar)
    {
        if (tickBar != null)
        {
            tickBar.Visibility = Visibility.Collapsed;
        }
    }

    private void RefreshTrackLayoutIfPossible()
    {
        if (_track is not FrameworkElement track)
        {
            return;
        }

        var slot = track.LayoutSlot;
        if (slot.Width <= 0f || slot.Height <= 0f)
        {
            return;
        }

        if (!track.NeedsMeasure && !track.NeedsArrange)
        {
            return;
        }

        track.Arrange(slot);
    }

    private void UpdateSelectionRangeVisual()
    {
        if (_selectionRangeElement == null || _track == null)
        {
            return;
        }

        if (!IsSelectionRangeEnabled)
        {
            _selectionRangeElement.Visibility = Visibility.Collapsed;
            return;
        }

        var slot = _track.LayoutSlot;
        var trackRect = _track.GetTrackRect();
        if (slot.Width <= 0f || slot.Height <= 0f || trackRect.Width <= 0f || trackRect.Height <= 0f)
        {
            return;
        }

        var startPosition = _track.GetValuePosition(SelectionStart);
        var endPosition = _track.GetValuePosition(SelectionEnd);
        var low = MathF.Min(startPosition, endPosition);
        var high = MathF.Max(startPosition, endPosition);

        Thickness margin;
        if (Orientation == Orientation.Horizontal)
        {
            margin = new Thickness(
                low - slot.X,
                trackRect.Y - slot.Y,
                (slot.X + slot.Width) - high,
                (slot.Y + slot.Height) - (trackRect.Y + trackRect.Height));
        }
        else
        {
            margin = new Thickness(
                trackRect.X - slot.X,
                low - slot.Y,
                (slot.X + slot.Width) - (trackRect.X + trackRect.Width),
                (slot.Y + slot.Height) - high);
        }

        _selectionRangeElement.Margin = margin;
        _selectionRangeElement.Visibility = Visibility.Visible;
    }

    private void OnTrackButtonMouseDown(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        _lastTrackPressPosition = args.Position;
        FocusManager.SetFocus(this);
        if (IsMoveToPointEnabled)
        {
            MoveToPoint(args.Position);
        }
    }

    private void OnTrackMouseDown(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (!IsMoveToPointEnabled || args.Button != MouseButton.Left)
        {
            return;
        }

        if (IsSameOrDescendantOf(args.OriginalSource, _thumb))
        {
            return;
        }

        MoveToPoint(args.Position);
    }

    private void OnTrackLayoutUpdated(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateSelectionRangeVisual();
    }

    private void OnThumbMouseDown(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        FocusManager.SetFocus(this);
        _lastTrackPressPosition = args.Position;
    }

    private void OnDecreaseButtonClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        FocusManager.SetFocus(this);
        if (IsMoveToPointEnabled)
        {
            MoveToPoint(_lastTrackPressPosition);
        }
        else
        {
            Value -= ResolveLargeChange();
        }

        args.Handled = true;
    }

    private void OnIncreaseButtonClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        FocusManager.SetFocus(this);
        if (IsMoveToPointEnabled)
        {
            MoveToPoint(_lastTrackPressPosition);
        }
        else
        {
            Value += ResolveLargeChange();
        }

        args.Handled = true;
    }

    private void MoveToPoint(Vector2 point)
    {
        if (_track == null)
        {
            return;
        }

        Value = _track.GetValueFromPoint(point, useThumbCenterOffset: true);
    }

    private void OnThumbDragStarted(object? sender, DragStartedEventArgs args)
    {
        _ = sender;
        FocusManager.SetFocus(this);
        IsDraggingThumb = true;
        _thumbDragOriginTravel = _track?.GetThumbTravel() ?? 0f;
        _thumbDragAccumulatedDelta = 0f;
        UpdateAutoToolTip();
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
        UpdateAutoToolTip();
        args.Handled = true;
    }

    private void OnThumbDragCompleted(object? sender, DragCompletedEventArgs args)
    {
        _ = sender;
        EndThumbDrag();
        args.Handled = true;
    }

    private void EndThumbDrag()
    {
        IsDraggingThumb = false;
        _thumbDragOriginTravel = 0f;
        _thumbDragAccumulatedDelta = 0f;
        CloseAutoToolTip();
    }

    private void UpdateAutoToolTip()
    {
        if (!IsDraggingThumb || AutoToolTipPlacement == AutoToolTipPlacement.None || _thumb == null)
        {
            CloseAutoToolTip();
            return;
        }

        var host = FindHostPanel();
        if (host == null)
        {
            return;
        }

        EnsureAutoToolTip();
        if (_autoToolTip == null || _autoToolTipText == null)
        {
            return;
        }

        _autoToolTipText.Text = FormatAutoToolTipValue();
        ConfigureAutoToolTipPlacement(_autoToolTip);
        if (_autoToolTip.IsOpen)
        {
            _autoToolTip.ShowFor(host, _thumb);
        }
        else
        {
            _autoToolTip.ShowFor(host, _thumb);
        }
    }

    private void EnsureAutoToolTip()
    {
        if (_autoToolTip != null)
        {
            return;
        }

        _autoToolTipText = new TextBlock
        {
            Margin = new Thickness(0f),
            Text = string.Empty
        };

        _autoToolTip = new ToolTip
        {
            Content = _autoToolTipText
        };
    }

    private void ConfigureAutoToolTipPlacement(ToolTip toolTip)
    {
        var placement = AutoToolTipPlacement;
        toolTip.PlacementMode = placement switch
        {
            AutoToolTipPlacement.TopLeft when Orientation == Orientation.Horizontal => PopupPlacementMode.Top,
            AutoToolTipPlacement.TopLeft => PopupPlacementMode.Left,
            AutoToolTipPlacement.BottomRight when Orientation == Orientation.Horizontal => PopupPlacementMode.Bottom,
            _ => PopupPlacementMode.Right
        };
    }

    private string FormatAutoToolTipValue()
    {
        return Value.ToString($"F{AutoToolTipPrecision}", CultureInfo.InvariantCulture);
    }

    private void CloseAutoToolTip()
    {
        if (_autoToolTip?.IsOpen == true)
        {
            _autoToolTip.Close();
        }
    }

    private Panel? FindHostPanel()
    {
        Panel? host = null;
        for (UIElement? current = this; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is Panel panel)
            {
                host = panel;
            }
        }

        return host;
    }

    private bool GetEffectiveTrackDirectionReversed()
    {
        return Orientation == Orientation.Vertical ? !IsDirectionReversed : IsDirectionReversed;
    }

    private void DetachTemplatePartHandlers()
    {
        CloseAutoToolTip();

        if (_track != null)
        {
            _track.LayoutUpdated -= OnTrackLayoutUpdated;
            _track.RemoveHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnTrackMouseDown);
        }

        if (_thumb != null)
        {
            _thumb.DragStarted -= OnThumbDragStarted;
            _thumb.DragDelta -= OnThumbDragDelta;
            _thumb.DragCompleted -= OnThumbDragCompleted;
            _thumb.RemoveHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnThumbMouseDown);
        }

        if (_decreaseButton != null)
        {
            _decreaseButton.Click -= OnDecreaseButtonClick;
            _decreaseButton.RemoveHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnTrackButtonMouseDown);
        }

        if (_increaseButton != null)
        {
            _increaseButton.Click -= OnIncreaseButtonClick;
            _increaseButton.RemoveHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnTrackButtonMouseDown);
        }
    }

    private void EnsureTrackDescendant(UIElement element, string partName)
    {
        if (IsSameOrDescendantOf(element, _track))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Template part '{partName}' for '{nameof(Slider)}' must be within '{nameof(Track)}' part 'PART_Track'.");
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

    private static bool FloatValuesAreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.0001f;
    }

    private static Style BuildDefaultSliderStyle()
    {
        var style = new Style(typeof(Slider));
        style.Setters.Add(new Setter(TemplateProperty, BuildDefaultSliderTemplate()));

        var disabled = new Trigger(IsEnabledProperty, false);
        disabled.Setters.Add(new Setter(ThumbBrushProperty, new Color(96, 96, 96)));
        disabled.Setters.Add(new Setter(TrackBrushProperty, new Color(52, 52, 52)));
        disabled.Setters.Add(new Setter(SelectionRangeBrushProperty, new Color(68, 68, 68)));
        style.Triggers.Add(disabled);

        return style;
    }

    private static ControlTemplate BuildDefaultSliderTemplate()
    {
        var template = new ControlTemplate(static owner =>
        {
            var slider = (Slider)owner;
            var root = new Grid
            {
                Name = "PART_Root"
            };

            var topTickBar = new TickBar
            {
                Name = "PART_TopTickBar",
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            var bottomTickBar = new TickBar
            {
                Name = "PART_BottomTickBar",
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            var leftTickBar = new TickBar
            {
                Name = "PART_LeftTickBar",
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            var rightTickBar = new TickBar
            {
                Name = "PART_RightTickBar",
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };

            var track = new Track
            {
                Name = "PART_Track",
                Focusable = false,
                IsViewportSizedThumb = false,
                ThumbLength = slider.ThumbSize,
                ThumbMinLength = slider.ThumbSize,
                TrackThickness = slider.TrackThickness
            };

            var selectionRange = new Border
            {
                Name = "PART_SelectionRange",
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };

            var decreaseButton = CreateTrackButton("PART_DecreaseButton");
            var thumb = new Thumb
            {
                Name = "PART_Thumb"
            };
            var increaseButton = CreateTrackButton("PART_IncreaseButton");

            Track.SetPartRole(decreaseButton, TrackPartRole.DecreaseButton);
            Track.SetPartRole(thumb, TrackPartRole.Thumb);
            Track.SetPartRole(increaseButton, TrackPartRole.IncreaseButton);

            track.AddChild(selectionRange);
            track.AddChild(decreaseButton);
            track.AddChild(thumb);
            track.AddChild(increaseButton);

            root.AddChild(topTickBar);
            root.AddChild(bottomTickBar);
            root.AddChild(leftTickBar);
            root.AddChild(rightTickBar);
            root.AddChild(track);
            return root;
        })
        {
            TargetType = typeof(Slider)
        };

        template.BindTemplate(string.Empty, Panel.BackgroundProperty, BackgroundProperty);
        template.BindTemplate("PART_Track", Panel.BackgroundProperty, TrackBrushProperty);
        template.BindTemplate("PART_Track", Track.BorderBrushProperty, BorderBrushProperty);
        template.BindTemplate("PART_Track", Track.ThumbLengthProperty, ThumbSizeProperty);
        template.BindTemplate("PART_Track", Track.ThumbMinLengthProperty, ThumbSizeProperty);
        template.BindTemplate("PART_Track", Track.TrackThicknessProperty, TrackThicknessProperty);
        template.BindTemplate("PART_SelectionRange", Border.BackgroundProperty, SelectionRangeBrushProperty);
        template.BindTemplate("PART_Thumb", Thumb.BackgroundProperty, ThumbBrushProperty);
        template.BindTemplate("PART_Thumb", Thumb.BorderBrushProperty, BorderBrushProperty);
        template.BindTemplate("PART_TopTickBar", TickBar.FillProperty, ForegroundProperty);
        template.BindTemplate("PART_BottomTickBar", TickBar.FillProperty, ForegroundProperty);
        template.BindTemplate("PART_LeftTickBar", TickBar.FillProperty, ForegroundProperty);
        template.BindTemplate("PART_RightTickBar", TickBar.FillProperty, ForegroundProperty);
        template.BindTemplate("PART_TopTickBar", TickBar.ReservedSpaceProperty, ThumbSizeProperty);
        template.BindTemplate("PART_BottomTickBar", TickBar.ReservedSpaceProperty, ThumbSizeProperty);
        template.BindTemplate("PART_LeftTickBar", TickBar.ReservedSpaceProperty, ThumbSizeProperty);
        template.BindTemplate("PART_RightTickBar", TickBar.ReservedSpaceProperty, ThumbSizeProperty);

        return template;
    }

    private static RepeatButton CreateTrackButton(string name)
    {
        return new RepeatButton
        {
            Name = name,
            Background = Color.Transparent,
            BorderBrush = Color.Transparent,
            Foreground = Color.Transparent,
            BorderThickness = 0f,
            Padding = Thickness.Empty,
            Focusable = false,
            Content = null
        };
    }
}
