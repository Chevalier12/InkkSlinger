using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

[TemplatePart("PART_Track", typeof(Track))]
[TemplatePart("PART_Thumb", typeof(Thumb))]
[TemplatePart("PART_LineUpButton", typeof(RepeatButton))]
[TemplatePart("PART_LineDownButton", typeof(RepeatButton))]
public class ScrollBar : RangeBase
{
    private const float ValueEpsilon = 0.01f;
    private static readonly Lazy<Style> DefaultScrollBarStyle = new(BuildDefaultScrollBarStyle);
    private static int _diagOnThumbDragDeltaCallCount;
    private static long _diagOnThumbDragDeltaElapsedTicks;
    private static long _diagOnThumbDragDeltaValueSetElapsedTicks;
    private static long _diagOnValueChangedBaseElapsedTicks;
    private static long _diagOnValueChangedSyncTrackStateElapsedTicks;
    private static long _diagSyncTrackStateElapsedTicks;
    private static long _diagRefreshTrackLayoutElapsedTicks;
    private Track? _track;
    private Thumb? _thumb;
    private RepeatButton? _lineUpButton;
    private RepeatButton? _lineDownButton;
    private float _thumbDragOriginTravel;
    private float _thumbDragAccumulatedDelta;

    public new static readonly RoutedEvent ValueChangedEvent = RangeBase.ValueChangedEvent;

    public new static readonly DependencyProperty MinimumProperty = RangeBase.MinimumProperty;

    public new static readonly DependencyProperty MaximumProperty = RangeBase.MaximumProperty;

    public new static readonly DependencyProperty ValueProperty = RangeBase.ValueProperty;

    public new static readonly DependencyProperty SmallChangeProperty = RangeBase.SmallChangeProperty;

    public new static readonly DependencyProperty LargeChangeProperty = RangeBase.LargeChangeProperty;

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
                        scrollBar.SetValue(ValueProperty, scrollBar.Value);
                        scrollBar.SyncTrackState();
                    }
                },
                coerceValueCallback: static (_, value) => value is float numeric && float.IsFinite(numeric) && numeric >= 0f ? numeric : 0f));

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

    static ScrollBar()
    {
        MinimumProperty.OverrideMetadata(
            typeof(ScrollBar),
            CreateDerivedMetadata(MinimumProperty, 0f, FrameworkPropertyMetadataOptions.AffectsArrange));
        MaximumProperty.OverrideMetadata(
            typeof(ScrollBar),
            CreateDerivedMetadata(MaximumProperty, 0f, FrameworkPropertyMetadataOptions.AffectsArrange));
        ValueProperty.OverrideMetadata(
            typeof(ScrollBar),
            CreateDerivedMetadata(ValueProperty, 0f, FrameworkPropertyMetadataOptions.AffectsArrange));
        SmallChangeProperty.OverrideMetadata(
            typeof(ScrollBar),
            CreateDerivedMetadata(SmallChangeProperty, 16f, FrameworkPropertyMetadataOptions.None));
        LargeChangeProperty.OverrideMetadata(
            typeof(ScrollBar),
            CreateDerivedMetadata(LargeChangeProperty, 32f, FrameworkPropertyMetadataOptions.None));
    }

    public ScrollBar()
    {
        Focusable = false;
    }

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public float ViewportSize
    {
        get => GetValue<float>(ViewportSizeProperty);
        set => SetValue(ViewportSizeProperty, value);
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

    protected override void OnMinimumChanged(float oldMinimum, float newMinimum)
    {
        base.OnMinimumChanged(oldMinimum, newMinimum);
        SyncTrackState();
    }

    protected override void OnMaximumChanged(float oldMaximum, float newMaximum)
    {
        base.OnMaximumChanged(oldMaximum, newMaximum);
        SyncTrackState();
    }

    protected override void OnValueChanged(float oldValue, float newValue)
    {
        var baseStartTicks = Stopwatch.GetTimestamp();
        base.OnValueChanged(oldValue, newValue);
        _diagOnValueChangedBaseElapsedTicks += Stopwatch.GetTimestamp() - baseStartTicks;

        var syncStartTicks = Stopwatch.GetTimestamp();
        SyncTrackState();
        _diagOnValueChangedSyncTrackStateElapsedTicks += Stopwatch.GetTimestamp() - syncStartTicks;
    }

    protected override float CoerceValueCore(float value)
    {
        var maxValue = Minimum + GetScrollableRange();
        if (maxValue < Minimum)
        {
            maxValue = Minimum;
        }

        return MathF.Max(Minimum, MathF.Min(maxValue, value));
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

        var startTicks = Stopwatch.GetTimestamp();
        _thumbDragAccumulatedDelta += Orientation == Orientation.Vertical
            ? args.VerticalChange
            : args.HorizontalChange;

        var valueSetStartTicks = Stopwatch.GetTimestamp();
        Value = _track.GetValueFromThumbTravel(_thumbDragOriginTravel + _thumbDragAccumulatedDelta);
        _diagOnThumbDragDeltaValueSetElapsedTicks += Stopwatch.GetTimestamp() - valueSetStartTicks;
        _diagOnThumbDragDeltaCallCount++;
        _diagOnThumbDragDeltaElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
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
        var startTicks = Stopwatch.GetTimestamp();
        if (_track == null)
        {
            _diagSyncTrackStateElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        var coercedValue = CoerceValueCore(Value);
        if (!AreClose(Value, coercedValue))
        {
            SetValue(ValueProperty, coercedValue);
        }

        SetIfChanged(Track.OrientationProperty, _track, Orientation);
        SetIfChanged(Track.MinimumProperty, _track, Minimum);
        SetIfChanged(Track.MaximumProperty, _track, Maximum);
        SetIfChanged(Track.ValueProperty, _track, coercedValue);
        SetIfChanged(Track.ViewportSizeProperty, _track, ViewportSize);
        RefreshTrackLayoutIfPossible();
        _diagSyncTrackStateElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
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

        var currentText = Label.ExtractAutomationText(button.Content);

        if (!string.IsNullOrEmpty(currentText) &&
            currentText is not "^" and not "v" and not "<" and not ">")
        {
            return;
        }

        if (currentText == nextText)
        {
            return;
        }

        button.Content = nextText;
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

    private void RefreshTrackLayoutIfPossible()
    {
        var startTicks = Stopwatch.GetTimestamp();
        if (_track is not FrameworkElement track)
        {
            _diagRefreshTrackLayoutElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        var slot = track.LayoutSlot;
        if (slot.Width <= 0f || slot.Height <= 0f)
        {
            _diagRefreshTrackLayoutElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        if (!track.NeedsMeasure && !track.NeedsArrange)
        {
            _diagRefreshTrackLayoutElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        track.Arrange(slot);
        UiRoot.Current?.NotifyDirectRenderInvalidation(track, requireDeepSync: true);
        _diagRefreshTrackLayoutElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    internal static ScrollBarThumbDragTelemetrySnapshot GetThumbDragTelemetryAndReset()
    {
        var snapshot = new ScrollBarThumbDragTelemetrySnapshot(
            _diagOnThumbDragDeltaCallCount,
            TicksToMilliseconds(_diagOnThumbDragDeltaElapsedTicks),
            TicksToMilliseconds(_diagOnThumbDragDeltaValueSetElapsedTicks),
            TicksToMilliseconds(_diagOnValueChangedBaseElapsedTicks),
            TicksToMilliseconds(_diagOnValueChangedSyncTrackStateElapsedTicks),
            TicksToMilliseconds(_diagSyncTrackStateElapsedTicks),
            TicksToMilliseconds(_diagRefreshTrackLayoutElapsedTicks));
        _diagOnThumbDragDeltaCallCount = 0;
        _diagOnThumbDragDeltaElapsedTicks = 0L;
        _diagOnThumbDragDeltaValueSetElapsedTicks = 0L;
        _diagOnValueChangedBaseElapsedTicks = 0L;
        _diagOnValueChangedSyncTrackStateElapsedTicks = 0L;
        _diagSyncTrackStateElapsedTicks = 0L;
        _diagRefreshTrackLayoutElapsedTicks = 0L;
        return snapshot;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
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
            Content = text,
            FontSize = 8f,
            Padding = Thickness.Empty,
            BorderThickness = 0f,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
    }
}

internal readonly record struct ScrollBarThumbDragTelemetrySnapshot(
    int OnThumbDragDeltaCallCount,
    double OnThumbDragDeltaMilliseconds,
    double OnThumbDragDeltaValueSetMilliseconds,
    double OnValueChangedBaseMilliseconds,
    double OnValueChangedSyncTrackStateMilliseconds,
    double SyncTrackStateMilliseconds,
    double RefreshTrackLayoutMilliseconds);
