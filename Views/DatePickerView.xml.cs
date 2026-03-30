using System;

namespace InkkSlinger;

public partial class DatePickerView : UserControl
{
    private static readonly DayOfWeek[] InspectorWeekCycle =
    {
        DayOfWeek.Sunday,
        DayOfWeek.Monday,
        DayOfWeek.Saturday,
    };

    private readonly DateTime _rangeStart = new(2026, 4, 7);
    private readonly DateTime _rangeEnd = new(2026, 4, 25);
    private int _inspectorEventCount;
    private string _inspectorLastDelta = "Last delta: none";

    public DatePickerView()
    {
        InitializeComponent();

        SetupBasicPicker();
        SetupTextEntryPicker();
        SetupRangePicker();
        SetupControlledPicker();
        SetupInspectorPicker();
    }

    private void SetupBasicPicker()
    {
        if (FindElement<DatePicker>("BasicPicker") is not { } picker)
        {
            return;
        }

        picker.SelectedDate = new DateTime(2026, 5, 12);
        picker.DisplayDate = picker.SelectedDate.Value;
        picker.SelectedDateChanged += OnBasicPickerSelectedDateChanged;

        AttachClick("BasicTodayButton", OnBasicTodayClick);
        AttachClick("BasicClearButton", OnBasicClearClick);
        AttachClick("BasicMilestoneButton", OnBasicMilestoneClick);

        UpdateBasicStatus();
    }

    private void SetupTextEntryPicker()
    {
        if (FindElement<DatePicker>("TextEntryPicker") is not { } picker)
        {
            return;
        }

        picker.SelectedDate = new DateTime(2026, 6, 18);
        picker.DisplayDate = picker.SelectedDate.Value;
        picker.DependencyPropertyChanged += OnTextEntryPickerPropertyChanged;

        AttachClick("TextEntryLocalButton", OnTextEntryLocalClick);
        AttachClick("TextEntryIsoButton", OnTextEntryIsoClick);
        AttachClick("TextEntryInvalidButton", OnTextEntryInvalidClick);
        AttachClick("TextEntryClearButton", OnTextEntryClearClick);

        UpdateTextEntryStatus();
    }

    private void SetupRangePicker()
    {
        if (FindElement<DatePicker>("RangePicker") is not { } picker)
        {
            return;
        }

        picker.DisplayDateStart = _rangeStart;
        picker.DisplayDateEnd = _rangeEnd;
        picker.DisplayDate = _rangeStart;
        picker.FirstDayOfWeek = DayOfWeek.Monday;
        picker.SelectedDate = _rangeStart.AddDays(3);
        picker.DependencyPropertyChanged += OnRangePickerPropertyChanged;

        AttachClick("RangeStartButton", OnRangeStartClick);
        AttachClick("RangeBeforeButton", OnRangeBeforeClick);
        AttachClick("RangeAfterButton", OnRangeAfterClick);

        UpdateRangeStatus();
    }

    private void SetupControlledPicker()
    {
        if (FindElement<DatePicker>("ControlledPicker") is not { } picker)
        {
            return;
        }

        picker.SelectedDate = DateTime.Today.AddDays(10);
        picker.DisplayDate = picker.SelectedDate.Value;
        picker.DependencyPropertyChanged += OnControlledPickerPropertyChanged;

        AttachClick("ControlledToggleReadOnlyButton", OnControlledToggleReadOnlyClick);
        AttachClick("ControlledOpenButton", OnControlledOpenClick);
        AttachClick("ControlledCloseButton", OnControlledCloseClick);
        AttachClick("ControlledTodayButton", OnControlledTodayClick);

        UpdateControlledStatus();
    }

    private void SetupInspectorPicker()
    {
        if (FindElement<DatePicker>("InspectorPicker") is not { } picker)
        {
            return;
        }

        picker.SelectedDate = DateTime.Today.AddDays(5);
        picker.DisplayDate = picker.SelectedDate.Value;
        picker.FirstDayOfWeek = DayOfWeek.Monday;
        picker.SelectedDateChanged += OnInspectorSelectedDateChanged;
        picker.DependencyPropertyChanged += OnInspectorPickerPropertyChanged;

        AttachClick("InspectorTodayButton", OnInspectorTodayClick);
        AttachClick("InspectorClearButton", OnInspectorClearClick);
        AttachClick("InspectorCycleWeekButton", OnInspectorCycleWeekClick);
        AttachClick("InspectorTogglePopupButton", OnInspectorTogglePopupClick);

        UpdateInspectorStatus();
    }

    private void OnBasicPickerSelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateBasicStatus();
    }

    private void OnTextEntryPickerPropertyChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.Property == DatePicker.TextProperty ||
            e.Property == DatePicker.SelectedDateProperty)
        {
            UpdateTextEntryStatus();
        }
    }

    private void OnRangePickerPropertyChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.Property == DatePicker.SelectedDateProperty ||
            e.Property == DatePicker.DisplayDateProperty ||
            e.Property == DatePicker.DisplayDateStartProperty ||
            e.Property == DatePicker.DisplayDateEndProperty)
        {
            UpdateRangeStatus();
        }
    }

    private void OnControlledPickerPropertyChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.Property == DatePicker.SelectedDateProperty ||
            e.Property == DatePicker.IsReadOnlyProperty ||
            e.Property == DatePicker.IsDropDownOpenProperty)
        {
            UpdateControlledStatus();
        }
    }

    private void OnInspectorPickerPropertyChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.Property == DatePicker.SelectedDateProperty ||
            e.Property == DatePicker.TextProperty ||
            e.Property == DatePicker.DisplayDateProperty ||
            e.Property == DatePicker.FirstDayOfWeekProperty ||
            e.Property == DatePicker.IsDropDownOpenProperty ||
            e.Property == DatePicker.IsReadOnlyProperty)
        {
            UpdateInspectorStatus();
        }
    }

    private void OnInspectorSelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _inspectorEventCount++;
        var removed = e.RemovedItems.Count > 0 ? FormatObjectDate(e.RemovedItems[0]) : "none";
        var added = e.AddedItems.Count > 0 ? FormatObjectDate(e.AddedItems[0]) : "none";
        _inspectorLastDelta = $"Last delta: removed {removed}, added {added}";
        UpdateInspectorStatus();
    }

    private void OnBasicTodayClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("BasicPicker") is { } picker)
        {
            picker.SelectedDate = DateTime.Today;
        }
    }

    private void OnBasicClearClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("BasicPicker") is { } picker)
        {
            picker.SelectedDate = null;
        }
    }

    private void OnBasicMilestoneClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("BasicPicker") is { } picker)
        {
            picker.SelectedDate = new DateTime(2026, 11, 12);
        }
    }

    private void OnTextEntryLocalClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("TextEntryPicker") is { } picker)
        {
            picker.Text = new DateTime(2026, 7, 4).ToString("d");
        }
    }

    private void OnTextEntryIsoClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("TextEntryPicker") is { } picker)
        {
            picker.Text = "2026-09-22";
        }
    }

    private void OnTextEntryInvalidClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("TextEntryPicker") is { } picker)
        {
            picker.Text = "tomorrowish";
        }
    }

    private void OnTextEntryClearClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("TextEntryPicker") is { } picker)
        {
            picker.Text = string.Empty;
        }
    }

    private void OnRangeStartClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("RangePicker") is { } picker)
        {
            picker.SelectedDate = _rangeStart;
        }
    }

    private void OnRangeBeforeClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("RangePicker") is { } picker)
        {
            picker.SelectedDate = _rangeStart.AddDays(-10);
        }
    }

    private void OnRangeAfterClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("RangePicker") is { } picker)
        {
            picker.SelectedDate = _rangeEnd.AddDays(10);
        }
    }

    private void OnControlledToggleReadOnlyClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("ControlledPicker") is { } picker)
        {
            picker.IsReadOnly = !picker.IsReadOnly;
        }
    }

    private void OnControlledOpenClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("ControlledPicker") is { } picker && !picker.IsReadOnly)
        {
            picker.IsDropDownOpen = true;
        }
    }

    private void OnControlledCloseClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("ControlledPicker") is { } picker)
        {
            picker.IsDropDownOpen = false;
        }
    }

    private void OnControlledTodayClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("ControlledPicker") is { } picker)
        {
            picker.SelectedDate = DateTime.Today;
        }
    }

    private void OnInspectorTodayClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("InspectorPicker") is { } picker)
        {
            picker.SelectedDate = DateTime.Today;
        }
    }

    private void OnInspectorClearClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("InspectorPicker") is { } picker)
        {
            picker.SelectedDate = null;
        }
    }

    private void OnInspectorCycleWeekClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("InspectorPicker") is not { } picker)
        {
            return;
        }

        var currentIndex = Array.IndexOf(InspectorWeekCycle, picker.FirstDayOfWeek);
        var nextIndex = currentIndex >= 0
            ? (currentIndex + 1) % InspectorWeekCycle.Length
            : 0;
        picker.FirstDayOfWeek = InspectorWeekCycle[nextIndex];
    }

    private void OnInspectorTogglePopupClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<DatePicker>("InspectorPicker") is { } picker)
        {
            picker.IsDropDownOpen = !picker.IsDropDownOpen;
        }
    }

    private void UpdateBasicStatus()
    {
        if (FindElement<DatePicker>("BasicPicker") is not { } picker)
        {
            return;
        }

        SetText(
            "BasicStatusLabel",
            $"SelectedDate: {FormatDate(picker.SelectedDate)} | Text: {FormatText(picker.Text)}");
    }

    private void UpdateTextEntryStatus()
    {
        if (FindElement<DatePicker>("TextEntryPicker") is not { } picker)
        {
            return;
        }

        SetText(
            "TextEntryStatusLabel",
            $"Text: {FormatText(picker.Text)} | SelectedDate: {FormatDate(picker.SelectedDate)}");
    }

    private void UpdateRangeStatus()
    {
        if (FindElement<DatePicker>("RangePicker") is not { } picker)
        {
            return;
        }

        SetText(
            "RangeWindowLabel",
            $"Allowed window: {FormatDate(picker.DisplayDateStart)} to {FormatDate(picker.DisplayDateEnd)} | First day: {picker.FirstDayOfWeek}");
        SetText(
            "RangeStatusLabel",
            $"SelectedDate: {FormatDate(picker.SelectedDate)} | Display month: {picker.DisplayDate:MMMM yyyy}");
    }

    private void UpdateControlledStatus()
    {
        if (FindElement<DatePicker>("ControlledPicker") is not { } picker)
        {
            return;
        }

        SetText(
            "ControlledStatusLabel",
            $"Readonly: {picker.IsReadOnly} | Popup: {(picker.IsDropDownOpen ? "open" : "closed")} | SelectedDate: {FormatDate(picker.SelectedDate)}");
    }

    private void UpdateInspectorStatus()
    {
        if (FindElement<DatePicker>("InspectorPicker") is not { } picker)
        {
            return;
        }

        SetText("InspectorSelectedDateLabel", $"SelectedDate: {FormatDate(picker.SelectedDate)}");
        SetText("InspectorTextLabel", $"Text: {FormatText(picker.Text)}");
        SetText("InspectorDisplayDateLabel", $"Display month: {picker.DisplayDate:MMMM yyyy}");
        SetText("InspectorFirstDayLabel", $"First day of week: {picker.FirstDayOfWeek}");
        SetText(
            "InspectorPopupLabel",
            $"Popup: {(picker.IsDropDownOpen ? "open" : "closed")} | Readonly: {picker.IsReadOnly}");
        SetText("InspectorEventCountLabel", $"SelectedDateChanged fired: {_inspectorEventCount}");
        SetText("InspectorLastChangeLabel", _inspectorLastDelta);
    }

    private void AttachClick(string elementName, EventHandler<RoutedSimpleEventArgs> handler)
    {
        if (FindElement<Button>(elementName) is { } button)
        {
            button.Click += handler;
        }
    }

    private T? FindElement<T>(string name)
        where T : class
    {
        return this.FindName(name) as T;
    }

    private void SetText(string elementName, string text)
    {
        if (FindElement<TextBlock>(elementName) is { } textBlock)
        {
            textBlock.Text = text;
        }
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? value.Value.ToLongDateString() : "none";
    }

    private static string FormatObjectDate(object? value)
    {
        return value is DateTime date ? date.ToLongDateString() : "none";
    }

    private static string FormatText(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? "empty" : text;
    }
}




