using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class SliderView : UserControl
{
    private readonly Slider _workbenchSlider;
    private readonly Slider _snappedSelectionSlider;
    private readonly Slider _frequencySlider;
    private readonly Slider _transportSlider;
    private readonly Slider _disabledSlider;
    private readonly Slider _verticalRangeSlider;
    private readonly Slider _verticalReverseSlider;
    private readonly Slider _verticalThemeSlider;
    private readonly ComboBox _tickPlacementComboBox;
    private readonly ComboBox _toolTipPlacementComboBox;
    private readonly CheckBox _snapCheckBox;
    private readonly CheckBox _moveToPointCheckBox;
    private readonly CheckBox _reversedCheckBox;
    private readonly CheckBox _selectionRangeCheckBox;
    private readonly Slider _trackThicknessSlider;
    private readonly Slider _thumbSizeSlider;
    private readonly Slider _precisionSlider;
    private readonly Slider _delaySlider;
    private readonly Slider _intervalSlider;
    private readonly Slider _selectionStartSlider;
    private readonly Slider _selectionEndSlider;
    private readonly TextBlock _presetNarrativeText;
    private readonly TextBlock _workbenchValueText;
    private readonly TextBlock _workbenchRangeText;
    private readonly TextBlock _workbenchGestureText;
    private readonly TextBlock _snappedSelectionValueText;
    private readonly TextBlock _frequencyValueText;
    private readonly TextBlock _transportValueText;
    private readonly TextBlock _disabledValueText;
    private readonly TextBlock _verticalStateText;
    private readonly TextBlock _trackThicknessValueText;
    private readonly TextBlock _thumbSizeValueText;
    private readonly TextBlock _precisionValueText;
    private readonly TextBlock _delayValueText;
    private readonly TextBlock _intervalValueText;
    private readonly TextBlock _selectionStartValueText;
    private readonly TextBlock _selectionEndValueText;
    private readonly TextBlock _workbenchSummaryText;
    private readonly TextBlock _workbenchConfigurationText;
    private readonly TextBlock _gallerySummaryText;

    private bool _suppressEvents;

    public SliderView()
    {
        InitializeComponent();

        _workbenchSlider = RequireElement<Slider>("WorkbenchSlider");
        _snappedSelectionSlider = RequireElement<Slider>("SnappedSelectionSlider");
        _frequencySlider = RequireElement<Slider>("FrequencySlider");
        _transportSlider = RequireElement<Slider>("TransportSlider");
        _disabledSlider = RequireElement<Slider>("DisabledSlider");
        _verticalRangeSlider = RequireElement<Slider>("VerticalRangeSlider");
        _verticalReverseSlider = RequireElement<Slider>("VerticalReverseSlider");
        _verticalThemeSlider = RequireElement<Slider>("VerticalThemeSlider");
        _tickPlacementComboBox = RequireElement<ComboBox>("TickPlacementComboBox");
        _toolTipPlacementComboBox = RequireElement<ComboBox>("ToolTipPlacementComboBox");
        _snapCheckBox = RequireElement<CheckBox>("SnapCheckBox");
        _moveToPointCheckBox = RequireElement<CheckBox>("MoveToPointCheckBox");
        _reversedCheckBox = RequireElement<CheckBox>("ReversedCheckBox");
        _selectionRangeCheckBox = RequireElement<CheckBox>("SelectionRangeCheckBox");
        _trackThicknessSlider = RequireElement<Slider>("TrackThicknessSlider");
        _thumbSizeSlider = RequireElement<Slider>("ThumbSizeSlider");
        _precisionSlider = RequireElement<Slider>("PrecisionSlider");
        _delaySlider = RequireElement<Slider>("DelaySlider");
        _intervalSlider = RequireElement<Slider>("IntervalSlider");
        _selectionStartSlider = RequireElement<Slider>("SelectionStartSlider");
        _selectionEndSlider = RequireElement<Slider>("SelectionEndSlider");
        _presetNarrativeText = RequireElement<TextBlock>("PresetNarrativeText");
        _workbenchValueText = RequireElement<TextBlock>("WorkbenchValueText");
        _workbenchRangeText = RequireElement<TextBlock>("WorkbenchRangeText");
        _workbenchGestureText = RequireElement<TextBlock>("WorkbenchGestureText");
        _snappedSelectionValueText = RequireElement<TextBlock>("SnappedSelectionValueText");
        _frequencyValueText = RequireElement<TextBlock>("FrequencyValueText");
        _transportValueText = RequireElement<TextBlock>("TransportValueText");
        _disabledValueText = RequireElement<TextBlock>("DisabledValueText");
        _verticalStateText = RequireElement<TextBlock>("VerticalStateText");
        _trackThicknessValueText = RequireElement<TextBlock>("TrackThicknessValueText");
        _thumbSizeValueText = RequireElement<TextBlock>("ThumbSizeValueText");
        _precisionValueText = RequireElement<TextBlock>("PrecisionValueText");
        _delayValueText = RequireElement<TextBlock>("DelayValueText");
        _intervalValueText = RequireElement<TextBlock>("IntervalValueText");
        _selectionStartValueText = RequireElement<TextBlock>("SelectionStartValueText");
        _selectionEndValueText = RequireElement<TextBlock>("SelectionEndValueText");
        _workbenchSummaryText = RequireElement<TextBlock>("WorkbenchSummaryText");
        _workbenchConfigurationText = RequireElement<TextBlock>("WorkbenchConfigurationText");
        _gallerySummaryText = RequireElement<TextBlock>("GallerySummaryText");

        PopulateComboBoxes();
        WireEvents();
        ApplyStudioPreset();
        UpdateAllReadouts();
    }

    private void PopulateComboBoxes()
    {
        if (_tickPlacementComboBox.Items.Count == 0)
        {
            _tickPlacementComboBox.Items.Add(nameof(TickPlacement.None));
            _tickPlacementComboBox.Items.Add(nameof(TickPlacement.TopLeft));
            _tickPlacementComboBox.Items.Add(nameof(TickPlacement.BottomRight));
            _tickPlacementComboBox.Items.Add(nameof(TickPlacement.Both));
        }

        if (_toolTipPlacementComboBox.Items.Count == 0)
        {
            _toolTipPlacementComboBox.Items.Add(nameof(AutoToolTipPlacement.None));
            _toolTipPlacementComboBox.Items.Add(nameof(AutoToolTipPlacement.TopLeft));
            _toolTipPlacementComboBox.Items.Add(nameof(AutoToolTipPlacement.BottomRight));
        }
    }

    private void WireEvents()
    {
        _workbenchSlider.ValueChanged += OnWorkbenchValueChanged;
        _snappedSelectionSlider.ValueChanged += OnGallerySliderValueChanged;
        _frequencySlider.ValueChanged += OnGallerySliderValueChanged;
        _transportSlider.ValueChanged += OnGallerySliderValueChanged;
        _verticalRangeSlider.ValueChanged += OnGallerySliderValueChanged;
        _verticalReverseSlider.ValueChanged += OnGallerySliderValueChanged;
        _verticalThemeSlider.ValueChanged += OnGallerySliderValueChanged;
        _trackThicknessSlider.ValueChanged += OnWorkbenchOptionChanged;
        _thumbSizeSlider.ValueChanged += OnWorkbenchOptionChanged;
        _precisionSlider.ValueChanged += OnWorkbenchOptionChanged;
        _delaySlider.ValueChanged += OnWorkbenchOptionChanged;
        _intervalSlider.ValueChanged += OnWorkbenchOptionChanged;
        _selectionStartSlider.ValueChanged += OnSelectionStartChanged;
        _selectionEndSlider.ValueChanged += OnSelectionEndChanged;
        _tickPlacementComboBox.SelectionChanged += OnComboSelectionChanged;
        _toolTipPlacementComboBox.SelectionChanged += OnComboSelectionChanged;

        WireToggle(_snapCheckBox);
        WireToggle(_moveToPointCheckBox);
        WireToggle(_reversedCheckBox);
        WireToggle(_selectionRangeCheckBox);

        AttachButton("StudioPresetButton", (_, _) => ApplyStudioPreset());
        AttachButton("PrecisionPresetButton", (_, _) => ApplyPrecisionPreset());
        AttachButton("ScrubberPresetButton", (_, _) => ApplyScrubberPreset());
        AttachButton("RangePresetButton", (_, _) => ApplyRangePreset());
        AttachButton("WorkbenchMinButton", (_, _) => _workbenchSlider.Value = _workbenchSlider.Minimum);
        AttachButton("WorkbenchStepBackButton", (_, _) => _workbenchSlider.Value -= _workbenchSlider.LargeChange);
        AttachButton("WorkbenchStepForwardButton", (_, _) => _workbenchSlider.Value += _workbenchSlider.LargeChange);
        AttachButton("WorkbenchMaxButton", (_, _) => _workbenchSlider.Value = _workbenchSlider.Maximum);
    }

    private void WireToggle(CheckBox checkBox)
    {
        checkBox.Checked += OnWorkbenchToggleChanged;
        checkBox.Unchecked += OnWorkbenchToggleChanged;
    }

    private void AttachButton(string name, EventHandler<RoutedSimpleEventArgs> handler)
    {
        if (this.FindName(name) is Button button)
        {
            button.Click += handler;
        }
    }

    private void OnWorkbenchValueChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateAllReadouts();
    }

    private void OnGallerySliderValueChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateGalleryReadouts();
        UpdateLiveStateText();
    }

    private void OnWorkbenchOptionChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_suppressEvents)
        {
            return;
        }

        ApplyWorkbenchConfiguration();
    }

    private void OnWorkbenchToggleChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_suppressEvents)
        {
            return;
        }

        ApplyWorkbenchConfiguration();
    }

    private void OnComboSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_suppressEvents)
        {
            return;
        }

        ApplyWorkbenchConfiguration();
    }

    private void OnSelectionStartChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_suppressEvents)
        {
            return;
        }

        ApplySelectionBounds(startWasEdited: true);
        UpdateAllReadouts();
    }

    private void OnSelectionEndChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_suppressEvents)
        {
            return;
        }

        ApplySelectionBounds(startWasEdited: false);
        UpdateAllReadouts();
    }

    private void ApplyWorkbenchConfiguration()
    {
        _workbenchSlider.TickPlacement = ParseTickPlacement(_tickPlacementComboBox.SelectedItem as string);
        _workbenchSlider.AutoToolTipPlacement = ParseAutoToolTipPlacement(_toolTipPlacementComboBox.SelectedItem as string);
        _workbenchSlider.IsSnapToTickEnabled = _snapCheckBox.IsChecked == true;
        _workbenchSlider.IsMoveToPointEnabled = _moveToPointCheckBox.IsChecked == true;
        _workbenchSlider.IsDirectionReversed = _reversedCheckBox.IsChecked == true;
        _workbenchSlider.IsSelectionRangeEnabled = _selectionRangeCheckBox.IsChecked == true;
        _workbenchSlider.TrackThickness = _trackThicknessSlider.Value;
        _workbenchSlider.ThumbSize = _thumbSizeSlider.Value;
        _workbenchSlider.AutoToolTipPrecision = Math.Max(0, (int)MathF.Round(_precisionSlider.Value));
        _workbenchSlider.Delay = Math.Max(0, (int)MathF.Round(_delaySlider.Value));
        _workbenchSlider.Interval = Math.Max(1, (int)MathF.Round(_intervalSlider.Value));

        ApplySelectionBounds(startWasEdited: null);
        UpdateSelectionControlState();
        UpdateAllReadouts();
    }

    private void ApplySelectionBounds(bool? startWasEdited)
    {
        var start = _selectionStartSlider.Value;
        var end = _selectionEndSlider.Value;

        if (start > end)
        {
            if (startWasEdited == false)
            {
                start = end;
            }
            else
            {
                end = start;
            }

            _suppressEvents = true;
            _selectionStartSlider.Value = start;
            _selectionEndSlider.Value = end;
            _suppressEvents = false;
        }

        _workbenchSlider.SelectionStart = start;
        _workbenchSlider.SelectionEnd = end;
    }

    private void UpdateSelectionControlState()
    {
        var selectionEnabled = _selectionRangeCheckBox.IsChecked == true;
        _selectionStartSlider.IsEnabled = selectionEnabled;
        _selectionEndSlider.IsEnabled = selectionEnabled;
    }

    private void ApplyStudioPreset()
    {
        ApplyPreset(
            narrative: "Balanced demo: explicit named stops, top tooltip placement, a visible selection span, and roomy chrome for keyboard and pointer testing.",
            minimum: 0f,
            maximum: 100f,
            value: 42f,
            smallChange: 1f,
            largeChange: 10f,
            ticks: "0,10,20,35,50,65,80,90,100",
            tickFrequency: 10f,
            tickPlacement: TickPlacement.Both,
            toolTipPlacement: AutoToolTipPlacement.TopLeft,
            snapToTick: false,
            moveToPoint: false,
            reversed: false,
            selectionEnabled: true,
            selectionStart: 20f,
            selectionEnd: 80f,
            trackThickness: 6f,
            thumbSize: 18f,
            precision: 0,
            delay: 180,
            interval: 90,
            trackBrush: new Color(51, 71, 93),
            thumbBrush: new Color(224, 140, 67),
            selectionBrush: new Color(79, 166, 232));
    }

    private void ApplyPrecisionPreset()
    {
        ApplyPreset(
            narrative: "Precision demo: fine-grained decimal range, snapping against frequency ticks, and higher tooltip precision for exact value inspection.",
            minimum: -10f,
            maximum: 10f,
            value: 3.5f,
            smallChange: 0.5f,
            largeChange: 2f,
            ticks: null,
            tickFrequency: 0.5f,
            tickPlacement: TickPlacement.TopLeft,
            toolTipPlacement: AutoToolTipPlacement.TopLeft,
            snapToTick: true,
            moveToPoint: false,
            reversed: false,
            selectionEnabled: false,
            selectionStart: -2f,
            selectionEnd: 6f,
            trackThickness: 5f,
            thumbSize: 16f,
            precision: 1,
            delay: 120,
            interval: 60,
            trackBrush: new Color(56, 61, 87),
            thumbBrush: new Color(142, 231, 255),
            selectionBrush: new Color(90, 122, 255));
    }

    private void ApplyScrubberPreset()
    {
        ApplyPreset(
            narrative: "Scrubber demo: move-to-point interaction, aggressive paging, snap-to-frequency behavior, and heavier visuals that feel closer to a media timeline.",
            minimum: 0f,
            maximum: 300f,
            value: 96f,
            smallChange: 5f,
            largeChange: 30f,
            ticks: null,
            tickFrequency: 15f,
            tickPlacement: TickPlacement.BottomRight,
            toolTipPlacement: AutoToolTipPlacement.BottomRight,
            snapToTick: true,
            moveToPoint: true,
            reversed: false,
            selectionEnabled: false,
            selectionStart: 30f,
            selectionEnd: 150f,
            trackThickness: 8f,
            thumbSize: 20f,
            precision: 0,
            delay: 60,
            interval: 35,
            trackBrush: new Color(38, 67, 79),
            thumbBrush: new Color(241, 195, 90),
            selectionBrush: new Color(92, 196, 163));
    }

    private void ApplyRangePreset()
    {
        ApplyPreset(
            narrative: "Range selection demo: emphasized highlighted span, custom palette, snapping against explicit stops, and wider handles for range editing passes.",
            minimum: 0f,
            maximum: 100f,
            value: 64f,
            smallChange: 2f,
            largeChange: 8f,
            ticks: "0,10,25,40,55,70,85,100",
            tickFrequency: 5f,
            tickPlacement: TickPlacement.Both,
            toolTipPlacement: AutoToolTipPlacement.TopLeft,
            snapToTick: true,
            moveToPoint: false,
            reversed: false,
            selectionEnabled: true,
            selectionStart: 25f,
            selectionEnd: 85f,
            trackThickness: 7f,
            thumbSize: 21f,
            precision: 0,
            delay: 180,
            interval: 90,
            trackBrush: new Color(75, 50, 91),
            thumbBrush: new Color(248, 157, 116),
            selectionBrush: new Color(212, 106, 106));
    }

    private void ApplyPreset(
        string narrative,
        float minimum,
        float maximum,
        float value,
        float smallChange,
        float largeChange,
        string? ticks,
        float tickFrequency,
        TickPlacement tickPlacement,
        AutoToolTipPlacement toolTipPlacement,
        bool snapToTick,
        bool moveToPoint,
        bool reversed,
        bool selectionEnabled,
        float selectionStart,
        float selectionEnd,
        float trackThickness,
        float thumbSize,
        int precision,
        int delay,
        int interval,
        Color trackBrush,
        Color thumbBrush,
        Color selectionBrush)
    {
        _suppressEvents = true;

        _presetNarrativeText.Text = narrative;
        _workbenchSlider.Minimum = minimum;
        _workbenchSlider.Maximum = maximum;
        _workbenchSlider.SmallChange = smallChange;
        _workbenchSlider.LargeChange = largeChange;
        _workbenchSlider.TickFrequency = tickFrequency;
        _workbenchSlider.Ticks = string.IsNullOrWhiteSpace(ticks)
            ? new DoubleCollection()
            : DoubleCollection.Parse(ticks);
        _workbenchSlider.TrackBrush = trackBrush;
        _workbenchSlider.ThumbBrush = thumbBrush;
        _workbenchSlider.SelectionRangeBrush = selectionBrush;

        _tickPlacementComboBox.SelectedItem = tickPlacement.ToString();
        _toolTipPlacementComboBox.SelectedItem = toolTipPlacement.ToString();
        _snapCheckBox.IsChecked = snapToTick;
        _moveToPointCheckBox.IsChecked = moveToPoint;
        _reversedCheckBox.IsChecked = reversed;
        _selectionRangeCheckBox.IsChecked = selectionEnabled;
        _trackThicknessSlider.Value = trackThickness;
        _thumbSizeSlider.Value = thumbSize;
        _precisionSlider.Value = precision;
        _delaySlider.Value = delay;
        _intervalSlider.Value = interval;

        _selectionStartSlider.Minimum = minimum;
        _selectionStartSlider.Maximum = maximum;
        _selectionEndSlider.Minimum = minimum;
        _selectionEndSlider.Maximum = maximum;

        var range = MathF.Max(1f, maximum - minimum);
        var selectionTick = MathF.Max(1f, MathF.Round(range / 20f));
        _selectionStartSlider.TickFrequency = selectionTick;
        _selectionEndSlider.TickFrequency = selectionTick;
        _selectionStartSlider.Value = Math.Clamp(selectionStart, minimum, maximum);
        _selectionEndSlider.Value = Math.Clamp(selectionEnd, minimum, maximum);

        _workbenchSlider.Value = Math.Clamp(value, minimum, maximum);

        _suppressEvents = false;
        ApplyWorkbenchConfiguration();
    }

    private void UpdateAllReadouts()
    {
        _workbenchValueText.Text = FormatValue(_workbenchSlider.Value);
        _workbenchRangeText.Text = $"{FormatValue(_workbenchSlider.Minimum)} to {FormatValue(_workbenchSlider.Maximum)}";
        _workbenchGestureText.Text = BuildGestureSummary();
        _trackThicknessValueText.Text = $"{FormatValue(_workbenchSlider.TrackThickness)} px";
        _thumbSizeValueText.Text = $"{FormatValue(_workbenchSlider.ThumbSize)} px";
        _precisionValueText.Text = $"{_workbenchSlider.AutoToolTipPrecision} decimal place(s)";
        _delayValueText.Text = $"{_workbenchSlider.Delay} ms before repeat starts";
        _intervalValueText.Text = $"{_workbenchSlider.Interval} ms between repeats";
        _selectionStartValueText.Text = _selectionRangeCheckBox.IsChecked == true
            ? FormatValue(_workbenchSlider.SelectionStart)
            : "Selection range disabled";
        _selectionEndValueText.Text = _selectionRangeCheckBox.IsChecked == true
            ? FormatValue(_workbenchSlider.SelectionEnd)
            : "Selection range disabled";

        UpdateGalleryReadouts();
        UpdateLiveStateText();
    }

    private void UpdateGalleryReadouts()
    {
        _snappedSelectionValueText.Text =
            $"Value {FormatValue(_snappedSelectionSlider.Value)} on explicit stops. Highlighted span: {FormatValue(_snappedSelectionSlider.SelectionStart)} to {FormatValue(_snappedSelectionSlider.SelectionEnd)}.";
        _frequencyValueText.Text =
            $"Value {FormatValue(_frequencySlider.Value)} with frequency {FormatValue(_frequencySlider.TickFrequency)} and free in-between movement.";
        _transportValueText.Text =
            $"Position {FormatValue(_transportSlider.Value)} sec. Small change {FormatValue(_transportSlider.SmallChange)}, large change {FormatValue(_transportSlider.LargeChange)}.";
        _disabledValueText.Text =
            $"Disabled at {FormatValue(_disabledSlider.Value)} with both-side ticks still visible for read-only workflows.";
        _verticalStateText.Text =
            $"Vertical values: range {FormatValue(_verticalRangeSlider.Value)}, reversed {FormatValue(_verticalReverseSlider.Value)}, themed {FormatValue(_verticalThemeSlider.Value)}.";
    }

    private void UpdateLiveStateText()
    {
        _workbenchSummaryText.Text =
            $"Value {FormatValue(_workbenchSlider.Value)} inside {FormatValue(_workbenchSlider.Minimum)} to {FormatValue(_workbenchSlider.Maximum)}. Dragging thumb: {(_workbenchSlider.IsDraggingThumb ? "Yes" : "No")}. Selection range: {BuildSelectionStateSummary()}.";

        _workbenchConfigurationText.Text =
            $"Ticks: {BuildTickSummary()}. Tick placement: {_workbenchSlider.TickPlacement}. Tooltip: {_workbenchSlider.AutoToolTipPlacement} with precision {_workbenchSlider.AutoToolTipPrecision}. Snap: {OnOff(_workbenchSlider.IsSnapToTickEnabled)}. Move-to-point: {OnOff(_workbenchSlider.IsMoveToPointEnabled)}. Reversed: {OnOff(_workbenchSlider.IsDirectionReversed)}. Track {FormatValue(_workbenchSlider.TrackThickness)} px, thumb {FormatValue(_workbenchSlider.ThumbSize)} px, repeat {_workbenchSlider.Delay}/{_workbenchSlider.Interval} ms.";

        _gallerySummaryText.Text =
            $"Gallery coverage includes explicit ticks, tick frequency, snap-to-tick, selection highlighting, move-to-point transport behavior, disabled rendering, vertical orientation, reversed direction, and custom slider chrome. Transport is at {FormatValue(_transportSlider.Value)} sec while the vertical set reads {FormatValue(_verticalRangeSlider.Value)}, {FormatValue(_verticalReverseSlider.Value)}, and {FormatValue(_verticalThemeSlider.Value)}.";
    }

    private string BuildGestureSummary()
    {
        var trackClick = _workbenchSlider.IsMoveToPointEnabled
            ? "Track clicks jump directly to the pointer position"
            : $"Track clicks page by {FormatValue(_workbenchSlider.LargeChange)}";
        var arrowKeys = _workbenchSlider.IsDirectionReversed
            ? "Left increases and Right decreases"
            : "Left decreases and Right increases";
        return $"{trackClick}. {arrowKeys}. PageUp or PageDown use LargeChange; Home and End jump to bounds.";
    }

    private string BuildSelectionStateSummary()
    {
        if (!_workbenchSlider.IsSelectionRangeEnabled)
        {
            return "Off";
        }

        return $"{FormatValue(_workbenchSlider.SelectionStart)} to {FormatValue(_workbenchSlider.SelectionEnd)}";
    }

    private string BuildTickSummary()
    {
        return _workbenchSlider.Ticks.Count > 0
            ? $"explicit stops ({_workbenchSlider.Ticks.Count})"
            : $"frequency {FormatValue(_workbenchSlider.TickFrequency)}";
    }

    private static string OnOff(bool enabled)
    {
        return enabled ? "On" : "Off";
    }

    private static string FormatValue(float value)
    {
        return value.ToString("0.##");
    }

    private static TickPlacement ParseTickPlacement(string? value)
    {
        return Enum.TryParse<TickPlacement>(value, ignoreCase: false, out var placement)
            ? placement
            : TickPlacement.None;
    }

    private static AutoToolTipPlacement ParseAutoToolTipPlacement(string? value)
    {
        return Enum.TryParse<AutoToolTipPlacement>(value, ignoreCase: false, out var placement)
            ? placement
            : AutoToolTipPlacement.None;
    }

    private T RequireElement<T>(string name)
        where T : class
    {
        if (this.FindName(name) is T element)
        {
            return element;
        }

        throw new InvalidOperationException($"Could not find element '{name}'.");
    }
}




