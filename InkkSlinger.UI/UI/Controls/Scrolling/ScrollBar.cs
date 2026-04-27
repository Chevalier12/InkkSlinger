using System;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
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
    private static int _diagOnApplyTemplateCallCount;
    private static int _diagOnApplyTemplateMissingPartCount;
    private static int _diagOnApplyTemplateEnsureTrackDescendantCount;
    private static int _diagOnApplyTemplateHandlerAttachCount;
    private static int _diagOnMinimumChangedCallCount;
    private static int _diagOnMaximumChangedCallCount;
    private static int _diagOnValueChangedCallCount;
    private static long _diagOnValueChangedBaseElapsedTicks;
    private static long _diagOnValueChangedSyncTrackStateElapsedTicks;
    private static int _diagOnTrackMouseDownCallCount;
    private static int _diagOnTrackMouseDownIgnoredDisabledOrNoTrackCount;
    private static int _diagOnTrackMouseDownIgnoredPartTargetCount;
    private static int _diagOnTrackMouseDownDecreaseHitCount;
    private static int _diagOnTrackMouseDownIncreaseHitCount;
    private static int _diagOnTrackMouseDownMissCount;
    private static int _diagOnLineUpButtonClickCallCount;
    private static int _diagOnLineDownButtonClickCallCount;
    private static int _diagOnThumbDragStartedCallCount;
    private static int _diagOnThumbDragStartedNoTrackCount;
    private static int _diagOnThumbDragDeltaNoTrackCount;
    private static int _diagOnThumbDragDeltaVerticalPathCount;
    private static int _diagOnThumbDragDeltaHorizontalPathCount;
    private static int _diagOnThumbDragCompletedCallCount;
    private static int _diagSyncTrackStateCallCount;
    private static int _diagSyncTrackStateNoTrackCount;
    private static int _diagSyncTrackStateCoercedValueChangeCount;
    private static long _diagSyncTrackStateElapsedTicks;
    private static int _diagRefreshTrackLayoutCallCount;
    private static int _diagRefreshTrackLayoutNoTrackCount;
    private static int _diagRefreshTrackLayoutZeroSlotCount;
    private static int _diagRefreshTrackLayoutNoLayoutNeededCount;
    private static int _diagRefreshTrackLayoutArrangedCount;
    private static long _diagRefreshTrackLayoutElapsedTicks;
    private int _runtimeOnApplyTemplateCallCount;
    private int _runtimeOnApplyTemplateMissingPartCount;
    private int _runtimeOnApplyTemplateEnsureTrackDescendantCount;
    private int _runtimeOnApplyTemplateHandlerAttachCount;
    private int _runtimeOnMinimumChangedCallCount;
    private int _runtimeOnMaximumChangedCallCount;
    private int _runtimeOnValueChangedCallCount;
    private long _runtimeOnValueChangedBaseElapsedTicks;
    private long _runtimeOnValueChangedSyncTrackStateElapsedTicks;
    private int _runtimeOnTrackMouseDownCallCount;
    private int _runtimeOnTrackMouseDownIgnoredDisabledOrNoTrackCount;
    private int _runtimeOnTrackMouseDownIgnoredPartTargetCount;
    private int _runtimeOnTrackMouseDownDecreaseHitCount;
    private int _runtimeOnTrackMouseDownIncreaseHitCount;
    private int _runtimeOnTrackMouseDownMissCount;
    private int _runtimeOnLineUpButtonClickCallCount;
    private int _runtimeOnLineDownButtonClickCallCount;
    private int _runtimeOnThumbDragStartedCallCount;
    private int _runtimeOnThumbDragStartedNoTrackCount;
    private int _runtimeOnThumbDragDeltaCallCount;
    private long _runtimeOnThumbDragDeltaElapsedTicks;
    private long _runtimeOnThumbDragDeltaValueSetElapsedTicks;
    private int _runtimeOnThumbDragDeltaNoTrackCount;
    private int _runtimeOnThumbDragDeltaVerticalPathCount;
    private int _runtimeOnThumbDragDeltaHorizontalPathCount;
    private int _runtimeOnThumbDragCompletedCallCount;
    private int _runtimeSyncTrackStateCallCount;
    private long _runtimeSyncTrackStateElapsedTicks;
    private int _runtimeSyncTrackStateNoTrackCount;
    private int _runtimeSyncTrackStateCoercedValueChangeCount;
    private int _runtimeRefreshTrackLayoutCallCount;
    private long _runtimeRefreshTrackLayoutElapsedTicks;
    private int _runtimeRefreshTrackLayoutNoTrackCount;
    private int _runtimeRefreshTrackLayoutZeroSlotCount;
    private int _runtimeRefreshTrackLayoutNoLayoutNeededCount;
    private int _runtimeRefreshTrackLayoutArrangedCount;
    private Track? _track;
    private Thumb? _thumb;
    private RepeatButton? _lineUpButton;
    private RepeatButton? _lineDownButton;
    private float _thumbDragOriginTravel;
    private float _thumbDragAccumulatedDelta;
    private bool _isThumbDragInProgress;
    private bool _suppressImmediateTrackLayoutRefresh;

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
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is ScrollBar scrollBar)
                    {
                        scrollBar.OnViewportSizeChanged();
                    }
                },
                coerceValueCallback: static (_, value) => value is float numeric && float.IsFinite(numeric) && numeric >= 0f ? numeric : 0f));

    public static readonly DependencyProperty ShowLineButtonsProperty =
        DependencyProperty.Register(
            nameof(ShowLineButtons),
            typeof(bool),
            typeof(ScrollBar),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is ScrollBar scrollBar)
                    {
                        scrollBar.SyncTrackState();
                    }
                }));

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
            CreateDerivedMetadata(MinimumProperty, 0f, FrameworkPropertyMetadataOptions.None));
        MaximumProperty.OverrideMetadata(
            typeof(ScrollBar),
            CreateDerivedMetadata(MaximumProperty, 0f, FrameworkPropertyMetadataOptions.None));
        ValueProperty.OverrideMetadata(
            typeof(ScrollBar),
            CreateDerivedMetadata(ValueProperty, 0f, FrameworkPropertyMetadataOptions.None));
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

    public bool ShowLineButtons
    {
        get => GetValue<bool>(ShowLineButtonsProperty);
        set => SetValue(ShowLineButtonsProperty, value);
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
        _runtimeOnApplyTemplateCallCount++;
        _diagOnApplyTemplateCallCount++;
        DetachTemplatePartHandlers();
        base.OnApplyTemplate();

        _track = GetTemplateChild("PART_Track") as Track;
        _thumb = GetTemplateChild("PART_Thumb") as Thumb;
        _lineUpButton = GetTemplateChild("PART_LineUpButton") as RepeatButton;
        _lineDownButton = GetTemplateChild("PART_LineDownButton") as RepeatButton;

        if (_track == null || _thumb == null || _lineUpButton == null || _lineDownButton == null)
        {
            _runtimeOnApplyTemplateMissingPartCount++;
            _diagOnApplyTemplateMissingPartCount++;
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
        _runtimeOnApplyTemplateHandlerAttachCount += 5;
        _diagOnApplyTemplateHandlerAttachCount += 5;

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

    internal bool IsThumbDragInProgress => _isThumbDragInProgress;

    internal float GetActiveThumbDragValue()
    {
        if (!_isThumbDragInProgress || _track == null)
        {
            return Value;
        }

        return _track.GetValueFromThumbTravel(_thumbDragOriginTravel + _thumbDragAccumulatedDelta);
    }

    protected override void OnMinimumChanged(float oldMinimum, float newMinimum)
    {
        _runtimeOnMinimumChangedCallCount++;
        _diagOnMinimumChangedCallCount++;
        base.OnMinimumChanged(oldMinimum, newMinimum);
        SyncTrackState();
    }

    protected override void OnMaximumChanged(float oldMaximum, float newMaximum)
    {
        _runtimeOnMaximumChangedCallCount++;
        _diagOnMaximumChangedCallCount++;
        base.OnMaximumChanged(oldMaximum, newMaximum);
        SyncTrackState();
    }

    protected override void OnValueChanged(float oldValue, float newValue)
    {
        _runtimeOnValueChangedCallCount++;
        _diagOnValueChangedCallCount++;
        var baseStartTicks = Stopwatch.GetTimestamp();
        base.OnValueChanged(oldValue, newValue);
        var baseElapsedTicks = Stopwatch.GetTimestamp() - baseStartTicks;
        _runtimeOnValueChangedBaseElapsedTicks += baseElapsedTicks;
        _diagOnValueChangedBaseElapsedTicks += baseElapsedTicks;

        var syncStartTicks = Stopwatch.GetTimestamp();
        SyncTrackState(refreshTrackLayout: !_suppressImmediateTrackLayoutRefresh);
        var syncElapsedTicks = Stopwatch.GetTimestamp() - syncStartTicks;
        _runtimeOnValueChangedSyncTrackStateElapsedTicks += syncElapsedTicks;
        _diagOnValueChangedSyncTrackStateElapsedTicks += syncElapsedTicks;
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
        _runtimeOnLineUpButtonClickCallCount++;
        _diagOnLineUpButtonClickCallCount++;
        Value -= ResolveSmallChange();
        args.Handled = true;
    }

    private void OnLineDownButtonClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _runtimeOnLineDownButtonClickCallCount++;
        _diagOnLineDownButtonClickCallCount++;
        Value += ResolveSmallChange();
        args.Handled = true;
    }

    private void OnTrackMouseDown(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        _runtimeOnTrackMouseDownCallCount++;
        _diagOnTrackMouseDownCallCount++;
        if (!IsEnabled || args.Button != MouseButton.Left || _track == null)
        {
            _runtimeOnTrackMouseDownIgnoredDisabledOrNoTrackCount++;
            _diagOnTrackMouseDownIgnoredDisabledOrNoTrackCount++;
            return;
        }

        if (IsSameOrDescendantOf(args.OriginalSource, _thumb) ||
            IsSameOrDescendantOf(args.OriginalSource, _lineUpButton) ||
            IsSameOrDescendantOf(args.OriginalSource, _lineDownButton))
        {
            _runtimeOnTrackMouseDownIgnoredPartTargetCount++;
            _diagOnTrackMouseDownIgnoredPartTargetCount++;
            return;
        }

        if (_track.HitTestDecreaseRegion(args.Position))
        {
            _runtimeOnTrackMouseDownDecreaseHitCount++;
            _diagOnTrackMouseDownDecreaseHitCount++;
            Value -= ResolveLargeChange();
            args.Handled = true;
        }
        else if (_track.HitTestIncreaseRegion(args.Position))
        {
            _runtimeOnTrackMouseDownIncreaseHitCount++;
            _diagOnTrackMouseDownIncreaseHitCount++;
            Value += ResolveLargeChange();
            args.Handled = true;
        }
        else
        {
            _runtimeOnTrackMouseDownMissCount++;
            _diagOnTrackMouseDownMissCount++;
        }
    }

    private void OnThumbDragStarted(object? sender, DragStartedEventArgs args)
    {
        _ = sender;
        _runtimeOnThumbDragStartedCallCount++;
        _diagOnThumbDragStartedCallCount++;
        if (_track == null)
        {
            _runtimeOnThumbDragStartedNoTrackCount++;
            _diagOnThumbDragStartedNoTrackCount++;
        }

        _isThumbDragInProgress = true;
        _thumbDragOriginTravel = _track?.GetThumbTravel() ?? 0f;
        _thumbDragAccumulatedDelta = 0f;
        args.Handled = true;
    }

    private void OnThumbDragDelta(object? sender, DragDeltaEventArgs args)
    {
        _ = sender;
        if (_track == null)
        {
            _runtimeOnThumbDragDeltaNoTrackCount++;
            _diagOnThumbDragDeltaNoTrackCount++;
            return;
        }

        var startTicks = Stopwatch.GetTimestamp();
        _runtimeOnThumbDragDeltaCallCount++;
        _thumbDragAccumulatedDelta += Orientation == Orientation.Vertical
            ? args.VerticalChange
            : args.HorizontalChange;
        if (Orientation == Orientation.Vertical)
        {
            _runtimeOnThumbDragDeltaVerticalPathCount++;
            _diagOnThumbDragDeltaVerticalPathCount++;
        }
        else
        {
            _runtimeOnThumbDragDeltaHorizontalPathCount++;
            _diagOnThumbDragDeltaHorizontalPathCount++;
        }

        var valueSetStartTicks = Stopwatch.GetTimestamp();
        Value = GetActiveThumbDragValue();
        var valueSetElapsedTicks = Stopwatch.GetTimestamp() - valueSetStartTicks;
        _runtimeOnThumbDragDeltaValueSetElapsedTicks += valueSetElapsedTicks;
        _diagOnThumbDragDeltaValueSetElapsedTicks += valueSetElapsedTicks;
        _diagOnThumbDragDeltaCallCount++;
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _runtimeOnThumbDragDeltaElapsedTicks += elapsedTicks;
        _diagOnThumbDragDeltaElapsedTicks += elapsedTicks;
        args.Handled = true;
    }

    private void OnThumbDragCompleted(object? sender, DragCompletedEventArgs args)
    {
        _ = sender;
        _runtimeOnThumbDragCompletedCallCount++;
        _diagOnThumbDragCompletedCallCount++;
        _isThumbDragInProgress = false;
        _thumbDragOriginTravel = 0f;
        _thumbDragAccumulatedDelta = 0f;
        args.Handled = true;
    }

    private void OnViewportSizeChanged()
    {
        _suppressImmediateTrackLayoutRefresh = true;
        try
        {
            SetValue(ValueProperty, Value);
            SyncTrackState(refreshTrackLayout: false);
        }
        finally
        {
            _suppressImmediateTrackLayoutRefresh = false;
        }
    }

    private void SyncTrackState(bool refreshTrackLayout = true)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeSyncTrackStateCallCount++;
        _diagSyncTrackStateCallCount++;
        if (_track == null)
        {
            _runtimeSyncTrackStateNoTrackCount++;
            _diagSyncTrackStateNoTrackCount++;
            var noTrackElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _runtimeSyncTrackStateElapsedTicks += noTrackElapsedTicks;
            _diagSyncTrackStateElapsedTicks += noTrackElapsedTicks;
            return;
        }

        var coercedValue = CoerceValueCore(Value);
        if (!AreClose(Value, coercedValue))
        {
            _runtimeSyncTrackStateCoercedValueChangeCount++;
            _diagSyncTrackStateCoercedValueChangeCount++;
            SetValue(ValueProperty, coercedValue);
        }

        SetIfChanged(Track.OrientationProperty, _track, Orientation);
        SetIfChanged(Track.MinimumProperty, _track, Minimum);
        SetIfChanged(Track.MaximumProperty, _track, Maximum);
        SetIfChanged(Track.ValueProperty, _track, coercedValue);
        SetIfChanged(Track.ViewportSizeProperty, _track, ViewportSize);
        SetIfChanged(Track.ShowLineButtonsProperty, _track, ShowLineButtons);
        if (refreshTrackLayout)
        {
            RefreshTrackLayoutIfPossible();
        }
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _runtimeSyncTrackStateElapsedTicks += elapsedTicks;
        _diagSyncTrackStateElapsedTicks += elapsedTicks;
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
        _runtimeOnApplyTemplateEnsureTrackDescendantCount++;
        _diagOnApplyTemplateEnsureTrackDescendantCount++;
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
        _runtimeRefreshTrackLayoutCallCount++;
        _diagRefreshTrackLayoutCallCount++;
        if (_track is not FrameworkElement track)
        {
            _runtimeRefreshTrackLayoutNoTrackCount++;
            _diagRefreshTrackLayoutNoTrackCount++;
            var noTrackElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _runtimeRefreshTrackLayoutElapsedTicks += noTrackElapsedTicks;
            _diagRefreshTrackLayoutElapsedTicks += noTrackElapsedTicks;
            return;
        }

        var slot = track.LayoutSlot;
        if (slot.Width <= 0f || slot.Height <= 0f)
        {
            _runtimeRefreshTrackLayoutZeroSlotCount++;
            _diagRefreshTrackLayoutZeroSlotCount++;
            var zeroSlotElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _runtimeRefreshTrackLayoutElapsedTicks += zeroSlotElapsedTicks;
            _diagRefreshTrackLayoutElapsedTicks += zeroSlotElapsedTicks;
            return;
        }

        if (ShouldDeferTrackLayoutRefresh())
        {
            var deferredElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _runtimeRefreshTrackLayoutElapsedTicks += deferredElapsedTicks;
            _diagRefreshTrackLayoutElapsedTicks += deferredElapsedTicks;
            return;
        }

        if (!track.NeedsMeasure && !track.NeedsArrange)
        {
            _runtimeRefreshTrackLayoutNoLayoutNeededCount++;
            _diagRefreshTrackLayoutNoLayoutNeededCount++;
            var noLayoutElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _runtimeRefreshTrackLayoutElapsedTicks += noLayoutElapsedTicks;
            _diagRefreshTrackLayoutElapsedTicks += noLayoutElapsedTicks;
            return;
        }

        _runtimeRefreshTrackLayoutArrangedCount++;
        _diagRefreshTrackLayoutArrangedCount++;
        track.Arrange(slot);
        UiRoot.Current?.NotifyDirectRenderInvalidation(track, requireDeepSync: true);
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _runtimeRefreshTrackLayoutElapsedTicks += elapsedTicks;
        _diagRefreshTrackLayoutElapsedTicks += elapsedTicks;
    }

    private bool ShouldDeferTrackLayoutRefresh()
    {
        if (IsMeasuring || IsArrangingOverride)
        {
            return true;
        }

        for (var current = VisualParent as FrameworkElement ?? LogicalParent as FrameworkElement;
             current != null;
             current = current.VisualParent as FrameworkElement ?? current.LogicalParent as FrameworkElement)
        {
            if (current.IsMeasuring || current.IsArrangingOverride)
            {
                return true;
            }
        }

        return false;
    }

    internal ScrollBarRuntimeDiagnosticsSnapshot GetScrollBarSnapshotForDiagnostics()
    {
        return new ScrollBarRuntimeDiagnosticsSnapshot(
            _track is not null,
            _track?.GetType().Name ?? string.Empty,
            _thumb is not null,
            _thumb?.GetType().Name ?? string.Empty,
            _lineUpButton is not null,
            _lineUpButton?.GetType().Name ?? string.Empty,
            _lineDownButton is not null,
            _lineDownButton?.GetType().Name ?? string.Empty,
            Orientation.ToString(),
            Minimum,
            Maximum,
            Value,
            ViewportSize,
            SmallChange,
            LargeChange,
            LayoutSlot.Width,
            LayoutSlot.Height,
            _thumbDragOriginTravel,
            _thumbDragAccumulatedDelta,
            _runtimeOnApplyTemplateCallCount,
            _runtimeOnApplyTemplateMissingPartCount,
            _runtimeOnApplyTemplateEnsureTrackDescendantCount,
            _runtimeOnApplyTemplateHandlerAttachCount,
            _runtimeOnMinimumChangedCallCount,
            _runtimeOnMaximumChangedCallCount,
            _runtimeOnValueChangedCallCount,
            TicksToMilliseconds(_runtimeOnValueChangedBaseElapsedTicks),
            TicksToMilliseconds(_runtimeOnValueChangedSyncTrackStateElapsedTicks),
            _runtimeOnTrackMouseDownCallCount,
            _runtimeOnTrackMouseDownIgnoredDisabledOrNoTrackCount,
            _runtimeOnTrackMouseDownIgnoredPartTargetCount,
            _runtimeOnTrackMouseDownDecreaseHitCount,
            _runtimeOnTrackMouseDownIncreaseHitCount,
            _runtimeOnTrackMouseDownMissCount,
            _runtimeOnLineUpButtonClickCallCount,
            _runtimeOnLineDownButtonClickCallCount,
            _runtimeOnThumbDragStartedCallCount,
            _runtimeOnThumbDragStartedNoTrackCount,
            _runtimeOnThumbDragDeltaCallCount,
            TicksToMilliseconds(_runtimeOnThumbDragDeltaElapsedTicks),
            TicksToMilliseconds(_runtimeOnThumbDragDeltaValueSetElapsedTicks),
            _runtimeOnThumbDragDeltaNoTrackCount,
            _runtimeOnThumbDragDeltaVerticalPathCount,
            _runtimeOnThumbDragDeltaHorizontalPathCount,
            _runtimeOnThumbDragCompletedCallCount,
            _runtimeSyncTrackStateCallCount,
            TicksToMilliseconds(_runtimeSyncTrackStateElapsedTicks),
            _runtimeSyncTrackStateNoTrackCount,
            _runtimeSyncTrackStateCoercedValueChangeCount,
            _runtimeRefreshTrackLayoutCallCount,
            TicksToMilliseconds(_runtimeRefreshTrackLayoutElapsedTicks),
            _runtimeRefreshTrackLayoutNoTrackCount,
            _runtimeRefreshTrackLayoutZeroSlotCount,
            _runtimeRefreshTrackLayoutNoLayoutNeededCount,
            _runtimeRefreshTrackLayoutArrangedCount);
    }

    internal new static ScrollBarThumbDragTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot(reset: false);
    }

    internal new static ScrollBarThumbDragTelemetrySnapshot GetTelemetrySnapshotForDiagnostics()
    {
        return GetAggregateTelemetrySnapshotForDiagnostics();
    }

    internal new static ScrollBarThumbDragTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateTelemetrySnapshot(reset: true);
    }

    internal static ScrollBarThumbDragTelemetrySnapshot GetThumbDragTelemetryAndReset()
    {
        return GetTelemetryAndReset();
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    private static ScrollBarThumbDragTelemetrySnapshot CreateTelemetrySnapshot(bool reset)
    {
        return new ScrollBarThumbDragTelemetrySnapshot(
            ReadOrReset(ref _diagOnThumbDragDeltaCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagOnThumbDragDeltaElapsedTicks, reset)),
            TicksToMilliseconds(ReadOrReset(ref _diagOnThumbDragDeltaValueSetElapsedTicks, reset)),
            TicksToMilliseconds(ReadOrReset(ref _diagOnValueChangedBaseElapsedTicks, reset)),
            TicksToMilliseconds(ReadOrReset(ref _diagOnValueChangedSyncTrackStateElapsedTicks, reset)),
            TicksToMilliseconds(ReadOrReset(ref _diagSyncTrackStateElapsedTicks, reset)),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshTrackLayoutElapsedTicks, reset)),
            ReadOrReset(ref _diagOnApplyTemplateCallCount, reset),
            ReadOrReset(ref _diagOnApplyTemplateMissingPartCount, reset),
            ReadOrReset(ref _diagOnApplyTemplateEnsureTrackDescendantCount, reset),
            ReadOrReset(ref _diagOnApplyTemplateHandlerAttachCount, reset),
            ReadOrReset(ref _diagOnMinimumChangedCallCount, reset),
            ReadOrReset(ref _diagOnMaximumChangedCallCount, reset),
            ReadOrReset(ref _diagOnValueChangedCallCount, reset),
            ReadOrReset(ref _diagOnTrackMouseDownCallCount, reset),
            ReadOrReset(ref _diagOnTrackMouseDownIgnoredDisabledOrNoTrackCount, reset),
            ReadOrReset(ref _diagOnTrackMouseDownIgnoredPartTargetCount, reset),
            ReadOrReset(ref _diagOnTrackMouseDownDecreaseHitCount, reset),
            ReadOrReset(ref _diagOnTrackMouseDownIncreaseHitCount, reset),
            ReadOrReset(ref _diagOnTrackMouseDownMissCount, reset),
            ReadOrReset(ref _diagOnLineUpButtonClickCallCount, reset),
            ReadOrReset(ref _diagOnLineDownButtonClickCallCount, reset),
            ReadOrReset(ref _diagOnThumbDragStartedCallCount, reset),
            ReadOrReset(ref _diagOnThumbDragStartedNoTrackCount, reset),
            ReadOrReset(ref _diagOnThumbDragDeltaNoTrackCount, reset),
            ReadOrReset(ref _diagOnThumbDragDeltaVerticalPathCount, reset),
            ReadOrReset(ref _diagOnThumbDragDeltaHorizontalPathCount, reset),
            ReadOrReset(ref _diagOnThumbDragCompletedCallCount, reset),
            ReadOrReset(ref _diagSyncTrackStateCallCount, reset),
            ReadOrReset(ref _diagSyncTrackStateNoTrackCount, reset),
            ReadOrReset(ref _diagSyncTrackStateCoercedValueChangeCount, reset),
            ReadOrReset(ref _diagRefreshTrackLayoutCallCount, reset),
            ReadOrReset(ref _diagRefreshTrackLayoutNoTrackCount, reset),
            ReadOrReset(ref _diagRefreshTrackLayoutZeroSlotCount, reset),
            ReadOrReset(ref _diagRefreshTrackLayoutNoLayoutNeededCount, reset),
            ReadOrReset(ref _diagRefreshTrackLayoutArrangedCount, reset));
    }

    private static int ReadOrReset(ref int value, bool reset)
    {
        var result = value;
        if (reset)
        {
            value = 0;
        }

        return result;
    }

    private static long ReadOrReset(ref long value, bool reset)
    {
        var result = value;
        if (reset)
        {
            value = 0;
        }

        return result;
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


