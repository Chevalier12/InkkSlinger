
using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class Calendar : UserControl
{
    public static readonly RoutedEvent SelectedDateChangedEvent =
        new(nameof(SelectedDateChanged), RoutingStrategy.Bubble);

    public static readonly RoutedEvent SelectedDatesChangedEvent =
        new(nameof(SelectedDatesChanged), RoutingStrategy.Bubble);

    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(
            nameof(SelectedDate),
            typeof(DateTime?),
            typeof(Calendar),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is Calendar calendar)
                    {
                        calendar.OnSelectedDateChanged((DateTime?)args.OldValue, (DateTime?)args.NewValue);
                    }
                },
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    if (dependencyObject is not Calendar calendar)
                    {
                        return value;
                    }

                    if (calendar.SelectionMode == CalendarSelectionMode.None)
                    {
                        return null;
                    }

                    if (value is null)
                    {
                        return null;
                    }

                    if (value is DateTime date)
                    {
                        return calendar.CoerceDateWithinRange(date.Date);
                    }

                    return null;
                }));

    public static readonly DependencyProperty SelectionModeProperty =
        DependencyProperty.Register(
            nameof(SelectionMode),
            typeof(CalendarSelectionMode),
            typeof(Calendar),
            new FrameworkPropertyMetadata(
                CalendarSelectionMode.SingleDate,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is Calendar calendar && args.NewValue is CalendarSelectionMode selectionMode)
                    {
                        calendar.OnSelectionModeChanged(selectionMode);
                    }
                }));

    public static readonly DependencyProperty DisplayDateProperty =
        DependencyProperty.Register(
            nameof(DisplayDate),
            typeof(DateTime),
            typeof(Calendar),
            new FrameworkPropertyMetadata(
                DateTime.Today,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Calendar calendar)
                    {
                        calendar.UpdateCalendarView();
                    }
                },
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    if (dependencyObject is not Calendar calendar)
                    {
                        return value;
                    }

                    var date = value is DateTime typed ? typed : DateTime.Today;
                    return calendar.CoerceDisplayDateToRange(date);
                }));

    public static readonly DependencyProperty DisplayDateStartProperty =
        DependencyProperty.Register(
            nameof(DisplayDateStart),
            typeof(DateTime?),
            typeof(Calendar),
            new FrameworkPropertyMetadata(
                null,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Calendar calendar)
                    {
                        calendar.OnDisplayRangeChanged();
                    }
                },
                coerceValueCallback: static (_, value) =>
                {
                    if (value is null)
                    {
                        return null;
                    }

                    return value is DateTime date ? date.Date : null;
                }));

    public static readonly DependencyProperty DisplayDateEndProperty =
        DependencyProperty.Register(
            nameof(DisplayDateEnd),
            typeof(DateTime?),
            typeof(Calendar),
            new FrameworkPropertyMetadata(
                null,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Calendar calendar)
                    {
                        calendar.OnDisplayRangeChanged();
                    }
                },
                coerceValueCallback: static (_, value) =>
                {
                    if (value is null)
                    {
                        return null;
                    }

                    return value is DateTime date ? date.Date : null;
                }));

    public static readonly DependencyProperty FirstDayOfWeekProperty =
        DependencyProperty.Register(
            nameof(FirstDayOfWeek),
            typeof(DayOfWeek),
            typeof(Calendar),
            new FrameworkPropertyMetadata(
                DayOfWeek.Sunday,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Calendar calendar)
                    {
                        calendar.UpdateCalendarView();
                    }
                }));

    private readonly Grid _rootGrid;
    private readonly Grid _weekDaysGrid;
    private readonly Grid _daysGrid;
    private readonly Button _previousMonthButton;
    private readonly Button _nextMonthButton;
    private readonly Label _monthLabel;
    private readonly Label[] _weekDayLabels = new Label[7];
    private readonly Button[] _dayButtons = new Button[42];
    private readonly DateTime[] _dayButtonDates = new DateTime[42];
    private readonly List<CalendarDateRange> _selectedRanges = new();
    private readonly List<DateTime> _selectedDates = new();
    private readonly HashSet<DateTime> _selectedDateLookup = new();

    private DateTime? _rangeAnchorDate;
    private DateTime? _lastActiveDate;
    private ModifierKeys _clickModifiers;
    private bool _isSynchronizingSelectedDate;

    public Calendar()
    {
        Background = new Color(24, 24, 24);
        BorderBrush = new Color(92, 92, 92);
        BorderThickness = new Thickness(1f);
        Padding = new Thickness(6f);

        _rootGrid = new Grid
        {
            Background = Color.Transparent
        };
        _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1f, GridUnitType.Star) });

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _previousMonthButton = new Button
        {
            Text = "<",
            Width = 28f,
            Margin = new Thickness(0f, 0f, 6f, 4f),
            Padding = new Thickness(0f)
        };
        _previousMonthButton.Click += (_, _) => NavigateMonth(-1);
        headerGrid.AddChild(_previousMonthButton);
        Grid.SetColumn(_previousMonthButton, 0);

        _monthLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0f, 2f, 0f, 4f),
            Foreground = new Color(236, 236, 236)
        };
        headerGrid.AddChild(_monthLabel);
        Grid.SetColumn(_monthLabel, 1);

        _nextMonthButton = new Button
        {
            Text = ">",
            Width = 28f,
            Margin = new Thickness(6f, 0f, 0f, 4f),
            Padding = new Thickness(0f)
        };
        _nextMonthButton.Click += (_, _) => NavigateMonth(1);
        headerGrid.AddChild(_nextMonthButton);
        Grid.SetColumn(_nextMonthButton, 2);

        _rootGrid.AddChild(headerGrid);
        Grid.SetRow(headerGrid, 0);

        _weekDaysGrid = new Grid
        {
            Margin = new Thickness(0f, 0f, 0f, 2f)
        };
        for (var i = 0; i < 7; i++)
        {
            _weekDaysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
            var label = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new Color(178, 178, 178),
                Margin = new Thickness(0f, 0f, 0f, 2f)
            };
            _weekDayLabels[i] = label;
            _weekDaysGrid.AddChild(label);
            Grid.SetColumn(label, i);
        }

        _rootGrid.AddChild(_weekDaysGrid);
        Grid.SetRow(_weekDaysGrid, 1);

        _daysGrid = new Grid();
        for (var i = 0; i < 7; i++)
        {
            _daysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
        }

        for (var i = 0; i < 6; i++)
        {
            _daysGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1f, GridUnitType.Star) });
        }

        for (var i = 0; i < _dayButtons.Length; i++)
        {
            var buttonIndex = i;
            var button = new Button
            {
                Margin = new Thickness(1f),
                Padding = new Thickness(0f),
                BorderThickness = 1f
            };
            button.AddHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, (_, args) => _clickModifiers = args.Modifiers);
            button.Click += (_, _) => OnDayButtonClicked(buttonIndex);
            _dayButtons[i] = button;
            _daysGrid.AddChild(button);
            Grid.SetRow(button, i / 7);
            Grid.SetColumn(button, i % 7);
        }

        _rootGrid.AddChild(_daysGrid);
        Grid.SetRow(_daysGrid, 2);

        Content = _rootGrid;

        AddHandler<KeyRoutedEventArgs>(UIElement.KeyDownEvent, OnKeyDown, handledEventsToo: true);
        UpdateCalendarView();
    }

    public event EventHandler<SelectionChangedEventArgs> SelectedDateChanged
    {
        add => AddHandler(SelectedDateChangedEvent, value);
        remove => RemoveHandler(SelectedDateChangedEvent, value);
    }

    public event EventHandler<SelectionChangedEventArgs> SelectedDatesChanged
    {
        add => AddHandler(SelectedDatesChangedEvent, value);
        remove => RemoveHandler(SelectedDatesChangedEvent, value);
    }

    public DateTime? SelectedDate
    {
        get => GetValue<DateTime?>(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public CalendarSelectionMode SelectionMode
    {
        get => GetValue<CalendarSelectionMode>(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public IReadOnlyList<DateTime> SelectedDates => _selectedDates;

    public DateTime DisplayDate
    {
        get => GetValue<DateTime>(DisplayDateProperty);
        set => SetValue(DisplayDateProperty, value);
    }

    public DateTime? DisplayDateStart
    {
        get => GetValue<DateTime?>(DisplayDateStartProperty);
        set => SetValue(DisplayDateStartProperty, value);
    }

    public DateTime? DisplayDateEnd
    {
        get => GetValue<DateTime?>(DisplayDateEndProperty);
        set => SetValue(DisplayDateEndProperty, value);
    }

    public DayOfWeek FirstDayOfWeek
    {
        get => GetValue<DayOfWeek>(FirstDayOfWeekProperty);
        set => SetValue(FirstDayOfWeekProperty, value);
    }

    protected internal IReadOnlyList<Button> DayButtonsForTesting => _dayButtons;
    protected internal string MonthLabelTextForTesting => _monthLabel.Text;
    protected internal IReadOnlyList<Label> WeekDayLabelsForTesting => _weekDayLabels;
    protected internal Button PreviousMonthButtonForTesting => _previousMonthButton;
    protected internal Button NextMonthButtonForTesting => _nextMonthButton;

    protected internal bool TryGetDayButtonIndexForDateForTesting(DateTime date, out int index)
    {
        var normalized = date.Date;
        for (var i = 0; i < _dayButtonDates.Length; i++)
        {
            if (_dayButtonDates[i].Date == normalized)
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    public void SetSelectedDates(IEnumerable<DateTime> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);

        if (SelectionMode == CalendarSelectionMode.None)
        {
            ApplySelectionChange([], null, null);
            return;
        }

        var ranges = BuildRangesFromDates(dates);
        if (SelectionMode == CalendarSelectionMode.SingleDate)
        {
            var projected = ProjectSingleDateFromRanges(ranges);
            if (projected.HasValue)
            {
                ApplySelectionChange([new CalendarDateRange(projected.Value, projected.Value)], projected.Value, projected.Value);
            }
            else
            {
                ApplySelectionChange([], null, null);
            }

            return;
        }

        if (SelectionMode == CalendarSelectionMode.SingleRange)
        {
            if (ranges.Count == 0)
            {
                ApplySelectionChange([], null, null);
                return;
            }

            var minDate = ranges[0].Start;
            var maxDate = ranges[0].End;
            for (var i = 1; i < ranges.Count; i++)
            {
                if (ranges[i].Start < minDate)
                {
                    minDate = ranges[i].Start;
                }

                if (ranges[i].End > maxDate)
                {
                    maxDate = ranges[i].End;
                }
            }

            var singleRange = new CalendarDateRange(minDate, maxDate);
            ApplySelectionChange([singleRange], singleRange.End, singleRange.End);
            return;
        }

        DateTime? active = ranges.Count > 0 ? ranges[^1].End : null;
        ApplySelectionChange(ranges, active, active);
    }

    private void OnSelectedDateChanged(DateTime? oldValue, DateTime? newValue)
    {
        if (_isSynchronizingSelectedDate)
        {
            return;
        }

        if (!newValue.HasValue)
        {
            ApplySelectionChange([], null, null);
            return;
        }

        var normalized = newValue.Value.Date;
        if (SelectionMode == CalendarSelectionMode.None)
        {
            ApplySelectionChange([], null, null);
            return;
        }

        normalized = CoerceDateWithinRange(normalized);
        ApplySelectionChange([new CalendarDateRange(normalized, normalized)], normalized, normalized);
    }

    private void OnSelectionModeChanged(CalendarSelectionMode selectionMode)
    {
        if (selectionMode == CalendarSelectionMode.None)
        {
            ApplySelectionChange([], null, null);
            return;
        }

        if (_selectedRanges.Count == 0)
        {
            UpdateCalendarView();
            return;
        }

        if (selectionMode == CalendarSelectionMode.SingleDate)
        {
            var projected = _lastActiveDate;
            if (!projected.HasValue || !_selectedDateLookup.Contains(projected.Value.Date))
            {
                projected = _selectedRanges[0].Start;
            }

            ApplySelectionChange([new CalendarDateRange(projected.Value, projected.Value)], projected, projected);
            return;
        }

        if (selectionMode == CalendarSelectionMode.SingleRange)
        {
            var minDate = _selectedRanges[0].Start;
            var maxDate = _selectedRanges[0].End;
            for (var i = 1; i < _selectedRanges.Count; i++)
            {
                if (_selectedRanges[i].Start < minDate)
                {
                    minDate = _selectedRanges[i].Start;
                }

                if (_selectedRanges[i].End > maxDate)
                {
                    maxDate = _selectedRanges[i].End;
                }
            }

            var normalized = new CalendarDateRange(minDate, maxDate);
            var active = _lastActiveDate;
            if (!active.HasValue || !normalized.Contains(active.Value))
            {
                active = normalized.End;
            }

            ApplySelectionChange([normalized], active, active);
            return;
        }

        UpdateCalendarView();
    }

    private void OnDisplayRangeChanged()
    {
        if (DisplayDateStart.HasValue && DisplayDateEnd.HasValue && DisplayDateStart.Value > DisplayDateEnd.Value)
        {
            DisplayDateEnd = DisplayDateStart;
            return;
        }

        var coercedDisplayDate = CoerceDisplayDateToRange(DisplayDate);
        if (coercedDisplayDate != DisplayDate)
        {
            DisplayDate = coercedDisplayDate;
        }

        var clampedRanges = ClampRangesToDisplayBounds(_selectedRanges);
        if (!AreRangesEqual(_selectedRanges, clampedRanges))
        {
            DateTime? active = _lastActiveDate.HasValue ? ClampDateInsideDisplayBounds(_lastActiveDate.Value) : null;
            DateTime? anchor = _rangeAnchorDate.HasValue ? ClampDateInsideDisplayBounds(_rangeAnchorDate.Value) : active;
            ApplySelectionChange(clampedRanges, active, anchor);
            return;
        }

        UpdateCalendarView();
    }

    private void NavigateMonth(int monthDelta)
    {
        var targetMonth = NormalizeToMonthStart(DisplayDate).AddMonths(monthDelta);
        if (!CanDisplayMonth(targetMonth))
        {
            return;
        }

        DisplayDate = targetMonth;
    }

    private void OnDayButtonClicked(int buttonIndex)
    {
        if ((uint)buttonIndex >= (uint)_dayButtonDates.Length)
        {
            return;
        }

        var date = _dayButtonDates[buttonIndex].Date;
        if (!IsDateSelectable(date) || SelectionMode == CalendarSelectionMode.None)
        {
            return;
        }

        var modifiers = _clickModifiers;
        _clickModifiers = ModifierKeys.None;
        var ctrl = (modifiers & ModifierKeys.Control) != 0;
        var shift = (modifiers & ModifierKeys.Shift) != 0;

        if (shift && SelectionMode is CalendarSelectionMode.SingleRange or CalendarSelectionMode.MultipleRange)
        {
            var anchor = ResolveRangeAnchor(date);
            ApplyRangeSelection(anchor, date, keepExisting: SelectionMode == CalendarSelectionMode.MultipleRange);
            return;
        }

        if (ctrl && SelectionMode == CalendarSelectionMode.MultipleRange)
        {
            ToggleDateSelection(date);
            return;
        }

        SelectSingleDate(date);
    }

    private void OnKeyDown(object? sender, KeyRoutedEventArgs args)
    {
        _ = sender;

        if (!TryGetNavigatedDate(args.Key, out var targetDate))
        {
            return;
        }

        args.Handled = true;

        if (SelectionMode == CalendarSelectionMode.None)
        {
            DisplayDate = targetDate;
            return;
        }

        var ctrl = (args.Modifiers & ModifierKeys.Control) != 0;
        var shift = (args.Modifiers & ModifierKeys.Shift) != 0;

        if (ctrl && SelectionMode == CalendarSelectionMode.MultipleRange)
        {
            MoveActiveDateWithoutSelectionMutation(targetDate);
            return;
        }

        if (shift && SelectionMode is CalendarSelectionMode.SingleRange or CalendarSelectionMode.MultipleRange)
        {
            var anchor = ResolveRangeAnchor(targetDate);
            ApplyRangeSelection(anchor, targetDate, keepExisting: SelectionMode == CalendarSelectionMode.MultipleRange);
            return;
        }

        SelectSingleDate(targetDate);
    }

    private bool TryGetNavigatedDate(Keys key, out DateTime targetDate)
    {
        var anchor = (_lastActiveDate ?? SelectedDate ?? DisplayDate).Date;
        switch (key)
        {
            case Keys.Left:
                targetDate = CoerceDateWithinRange(anchor.AddDays(-1));
                return true;
            case Keys.Right:
                targetDate = CoerceDateWithinRange(anchor.AddDays(1));
                return true;
            case Keys.Up:
                targetDate = CoerceDateWithinRange(anchor.AddDays(-7));
                return true;
            case Keys.Down:
                targetDate = CoerceDateWithinRange(anchor.AddDays(7));
                return true;
            case Keys.PageUp:
                targetDate = CoerceDateWithinRange(anchor.AddMonths(-1));
                return true;
            case Keys.PageDown:
                targetDate = CoerceDateWithinRange(anchor.AddMonths(1));
                return true;
            case Keys.Home:
                targetDate = CoerceDateWithinRange(new DateTime(DisplayDate.Year, DisplayDate.Month, 1));
                return true;
            case Keys.End:
                targetDate = CoerceDateWithinRange(new DateTime(DisplayDate.Year, DisplayDate.Month, DateTime.DaysInMonth(DisplayDate.Year, DisplayDate.Month)));
                return true;
            default:
                targetDate = default;
                return false;
        }
    }

    private void MoveActiveDateWithoutSelectionMutation(DateTime date)
    {
        if (!IsDateSelectable(date))
        {
            return;
        }

        _lastActiveDate = date.Date;
        if (!_rangeAnchorDate.HasValue)
        {
            _rangeAnchorDate = _lastActiveDate;
        }

        if (DisplayDate.Year != date.Year || DisplayDate.Month != date.Month)
        {
            DisplayDate = date;
            return;
        }

        UpdateCalendarView();
    }

    private void SelectSingleDate(DateTime date)
    {
        var normalized = CoerceDateWithinRange(date.Date);
        if (SelectionMode == CalendarSelectionMode.None)
        {
            ApplySelectionChange([], null, null);
            return;
        }

        ApplySelectionChange([new CalendarDateRange(normalized, normalized)], normalized, normalized);
    }

    private void ApplyRangeSelection(DateTime anchorDate, DateTime targetDate, bool keepExisting)
    {
        var normalizedAnchor = CoerceDateWithinRange(anchorDate.Date);
        var normalizedTarget = CoerceDateWithinRange(targetDate.Date);
        var range = new CalendarDateRange(normalizedAnchor, normalizedTarget);

        List<CalendarDateRange> nextRanges;
        if (!keepExisting || SelectionMode == CalendarSelectionMode.SingleRange)
        {
            nextRanges = [range];
        }
        else
        {
            nextRanges = new List<CalendarDateRange>(_selectedRanges);
            AddRangeAndMerge(nextRanges, range);
        }

        ApplySelectionChange(nextRanges, normalizedTarget, normalizedAnchor);
    }

    private void ToggleDateSelection(DateTime date)
    {
        var normalized = CoerceDateWithinRange(date.Date);
        var nextRanges = new List<CalendarDateRange>(_selectedRanges);

        var removed = TryRemoveDateFromRanges(nextRanges, normalized);
        if (!removed)
        {
            AddRangeAndMerge(nextRanges, new CalendarDateRange(normalized, normalized));
        }

        DateTime? active = nextRanges.Count > 0 ? normalized : null;
        DateTime? anchor = nextRanges.Count > 0 ? normalized : null;
        ApplySelectionChange(nextRanges, active, anchor);
    }

    private DateTime ResolveRangeAnchor(DateTime fallback)
    {
        if (_rangeAnchorDate.HasValue)
        {
            return _rangeAnchorDate.Value.Date;
        }

        if (_lastActiveDate.HasValue)
        {
            return _lastActiveDate.Value.Date;
        }

        if (SelectedDate.HasValue)
        {
            return SelectedDate.Value.Date;
        }

        return fallback.Date;
    }

    private void ApplySelectionChange(List<CalendarDateRange> nextRanges, DateTime? activeDate, DateTime? anchorDate)
    {
        NormalizeAndMergeRanges(nextRanges);

        var previousSelectedDate = SelectedDate;
        var previousSelectedDates = new List<DateTime>(_selectedDates);

        _selectedRanges.Clear();
        _selectedRanges.AddRange(nextRanges);

        RebuildSelectedDateCaches();

        if (activeDate.HasValue)
        {
            var normalizedActive = CoerceDateWithinRange(activeDate.Value.Date);
            _lastActiveDate = _selectedDateLookup.Contains(normalizedActive)
                ? normalizedActive
                : _selectedDates.Count > 0
                    ? _selectedDates[^1]
                    : null;
        }
        else
        {
            _lastActiveDate = _selectedDates.Count > 0 ? _selectedDates[^1] : null;
        }

        if (anchorDate.HasValue)
        {
            var normalizedAnchor = CoerceDateWithinRange(anchorDate.Value.Date);
            _rangeAnchorDate = _selectedDateLookup.Contains(normalizedAnchor)
                ? normalizedAnchor
                : _lastActiveDate;
        }
        else
        {
            _rangeAnchorDate = _lastActiveDate;
        }

        var projectedSelectedDate = ProjectSelectedDateFromSelection();
        if (projectedSelectedDate.HasValue &&
            (DisplayDate.Year != projectedSelectedDate.Value.Year || DisplayDate.Month != projectedSelectedDate.Value.Month))
        {
            DisplayDate = projectedSelectedDate.Value;
        }

        if (previousSelectedDate != projectedSelectedDate)
        {
            _isSynchronizingSelectedDate = true;
            try
            {
                SelectedDate = projectedSelectedDate;
            }
            finally
            {
                _isSynchronizingSelectedDate = false;
            }
        }

        UpdateCalendarView();

        if (!AreDateListsEqual(previousSelectedDates, _selectedDates))
        {
            var removedDates = BuildSelectionDelta(previousSelectedDates, _selectedDates);
            var addedDates = BuildSelectionDelta(_selectedDates, previousSelectedDates);
            RaiseRoutedEvent(
                SelectedDatesChangedEvent,
                new SelectionChangedEventArgs(SelectedDatesChangedEvent, removedDates, addedDates));
        }

        if (previousSelectedDate != projectedSelectedDate)
        {
            var removedItems = previousSelectedDate.HasValue
                ? new object[] { previousSelectedDate.Value }
                : Array.Empty<object>();
            var addedItems = projectedSelectedDate.HasValue
                ? new object[] { projectedSelectedDate.Value }
                : Array.Empty<object>();
            RaiseRoutedEvent(
                SelectedDateChangedEvent,
                new SelectionChangedEventArgs(SelectedDateChangedEvent, removedItems, addedItems));
        }
    }

    private DateTime? ProjectSelectedDateFromSelection()
    {
        if (SelectionMode == CalendarSelectionMode.None || _selectedDates.Count == 0)
        {
            return null;
        }

        if (_lastActiveDate.HasValue && _selectedDateLookup.Contains(_lastActiveDate.Value.Date))
        {
            return _lastActiveDate.Value.Date;
        }

        return _selectedDates[^1];
    }

    private void UpdateCalendarView()
    {
        var displayMonth = NormalizeToMonthStart(DisplayDate);
        _monthLabel.Text = displayMonth.ToString("Y", CultureInfo.CurrentCulture);

        var shortestDayNames = CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames;
        for (var i = 0; i < _weekDayLabels.Length; i++)
        {
            var index = (((int)FirstDayOfWeek + i) % 7 + 7) % 7;
            _weekDayLabels[i].Text = shortestDayNames[index];
        }

        var offsetFromFirstDay = ((int)displayMonth.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
        var firstVisibleDate = displayMonth.AddDays(-offsetFromFirstDay);
        var today = DateTime.Today;

        for (var i = 0; i < _dayButtons.Length; i++)
        {
            var date = firstVisibleDate.AddDays(i).Date;
            _dayButtonDates[i] = date;

            var button = _dayButtons[i];
            button.Text = date.Day.ToString(CultureInfo.InvariantCulture);

            var inDisplayMonth = date.Month == displayMonth.Month && date.Year == displayMonth.Year;
            var isPrimarySelected = _lastActiveDate.HasValue && _lastActiveDate.Value.Date == date;
            var isSelectedInRange = _selectedDateLookup.Contains(date);
            var isToday = date == today;
            var isEnabled = IsDateSelectable(date);

            button.IsEnabled = isEnabled;
            button.Background = ResolveDayButtonBackground(inDisplayMonth, isSelectedInRange, isPrimarySelected, isToday);
            button.Foreground = ResolveDayButtonForeground(inDisplayMonth, isEnabled, isSelectedInRange, isPrimarySelected);
            button.BorderBrush = ResolveDayButtonBorderBrush(isSelectedInRange, isPrimarySelected, isToday);
        }

        _previousMonthButton.IsEnabled = CanDisplayMonth(displayMonth.AddMonths(-1));
        _nextMonthButton.IsEnabled = CanDisplayMonth(displayMonth.AddMonths(1));
    }

    private static Color ResolveDayButtonBackground(bool inDisplayMonth, bool isSelectedInRange, bool isPrimarySelected, bool isToday)
    {
        if (isPrimarySelected)
        {
            return new Color(64, 100, 146);
        }

        if (isSelectedInRange)
        {
            return inDisplayMonth
                ? new Color(50, 72, 98)
                : new Color(42, 60, 82);
        }

        if (isToday)
        {
            return inDisplayMonth
                ? new Color(56, 56, 68)
                : new Color(44, 44, 56);
        }

        return inDisplayMonth
            ? new Color(38, 38, 38)
            : new Color(28, 28, 28);
    }

    private static Color ResolveDayButtonForeground(bool inDisplayMonth, bool isEnabled, bool isSelectedInRange, bool isPrimarySelected)
    {
        if (!isEnabled)
        {
            return new Color(96, 96, 96);
        }

        if (isPrimarySelected)
        {
            return Color.White;
        }

        if (isSelectedInRange)
        {
            return new Color(228, 238, 248);
        }

        return inDisplayMonth
            ? new Color(236, 236, 236)
            : new Color(150, 150, 150);
    }

    private static Color ResolveDayButtonBorderBrush(bool isSelectedInRange, bool isPrimarySelected, bool isToday)
    {
        if (isPrimarySelected)
        {
            return new Color(160, 205, 255);
        }

        if (isSelectedInRange)
        {
            return new Color(120, 155, 196);
        }

        if (isToday)
        {
            return new Color(122, 122, 140);
        }

        return new Color(78, 78, 78);
    }

    private bool CanDisplayMonth(DateTime monthStart)
    {
        var normalizedMonthStart = NormalizeToMonthStart(monthStart);
        var normalizedMonthEnd = normalizedMonthStart.AddMonths(1).AddDays(-1);

        if (DisplayDateStart.HasValue && normalizedMonthEnd < DisplayDateStart.Value.Date)
        {
            return false;
        }

        if (DisplayDateEnd.HasValue && normalizedMonthStart > DisplayDateEnd.Value.Date)
        {
            return false;
        }

        return true;
    }

    private bool IsDateSelectable(DateTime date)
    {
        var normalized = date.Date;
        if (DisplayDateStart.HasValue && normalized < DisplayDateStart.Value.Date)
        {
            return false;
        }

        if (DisplayDateEnd.HasValue && normalized > DisplayDateEnd.Value.Date)
        {
            return false;
        }

        return true;
    }

    private DateTime CoerceDateWithinRange(DateTime date)
    {
        var normalized = date.Date;
        if (DisplayDateStart.HasValue && normalized < DisplayDateStart.Value.Date)
        {
            normalized = DisplayDateStart.Value.Date;
        }

        if (DisplayDateEnd.HasValue && normalized > DisplayDateEnd.Value.Date)
        {
            normalized = DisplayDateEnd.Value.Date;
        }

        return normalized;
    }

    private DateTime ClampDateInsideDisplayBounds(DateTime date)
    {
        if (!DisplayDateStart.HasValue && !DisplayDateEnd.HasValue)
        {
            return date.Date;
        }

        return CoerceDateWithinRange(date.Date);
    }

    private List<CalendarDateRange> ClampRangesToDisplayBounds(IEnumerable<CalendarDateRange> ranges)
    {
        var minDate = DisplayDateStart?.Date;
        var maxDate = DisplayDateEnd?.Date;
        var clamped = new List<CalendarDateRange>();

        foreach (var range in ranges)
        {
            var nextStart = range.Start;
            var nextEnd = range.End;

            if (minDate.HasValue && nextEnd < minDate.Value)
            {
                continue;
            }

            if (maxDate.HasValue && nextStart > maxDate.Value)
            {
                continue;
            }

            if (minDate.HasValue && nextStart < minDate.Value)
            {
                nextStart = minDate.Value;
            }

            if (maxDate.HasValue && nextEnd > maxDate.Value)
            {
                nextEnd = maxDate.Value;
            }

            if (nextStart <= nextEnd)
            {
                clamped.Add(new CalendarDateRange(nextStart, nextEnd));
            }
        }

        NormalizeAndMergeRanges(clamped);
        return clamped;
    }

    private List<CalendarDateRange> BuildRangesFromDates(IEnumerable<DateTime> dates)
    {
        var normalizedDates = new List<DateTime>();
        foreach (var date in dates)
        {
            var coerced = CoerceDateWithinRange(date.Date);
            if (IsDateSelectable(coerced))
            {
                normalizedDates.Add(coerced);
            }
        }

        if (normalizedDates.Count == 0)
        {
            return [];
        }

        normalizedDates.Sort();

        var uniqueDates = new List<DateTime>(normalizedDates.Count);
        DateTime? last = null;
        foreach (var normalizedDate in normalizedDates)
        {
            if (!last.HasValue || normalizedDate != last.Value)
            {
                uniqueDates.Add(normalizedDate);
                last = normalizedDate;
            }
        }

        var ranges = new List<CalendarDateRange>();
        var rangeStart = uniqueDates[0];
        var rangeEnd = uniqueDates[0];
        for (var i = 1; i < uniqueDates.Count; i++)
        {
            var next = uniqueDates[i];
            if (next == rangeEnd.AddDays(1))
            {
                rangeEnd = next;
                continue;
            }

            ranges.Add(new CalendarDateRange(rangeStart, rangeEnd));
            rangeStart = next;
            rangeEnd = next;
        }

        ranges.Add(new CalendarDateRange(rangeStart, rangeEnd));
        NormalizeAndMergeRanges(ranges);
        return ranges;
    }

    private static DateTime? ProjectSingleDateFromRanges(IReadOnlyList<CalendarDateRange> ranges)
    {
        if (ranges.Count == 0)
        {
            return null;
        }

        return ranges[^1].End;
    }

    private void RebuildSelectedDateCaches()
    {
        _selectedDates.Clear();
        _selectedDateLookup.Clear();

        foreach (var range in _selectedRanges)
        {
            var cursor = range.Start;
            while (cursor <= range.End)
            {
                if (_selectedDateLookup.Add(cursor))
                {
                    _selectedDates.Add(cursor);
                }

                cursor = cursor.AddDays(1);
            }
        }
    }

    private static void NormalizeAndMergeRanges(List<CalendarDateRange> ranges)
    {
        if (ranges.Count <= 1)
        {
            return;
        }

        ranges.Sort(static (left, right) => left.Start.CompareTo(right.Start));

        var merged = new List<CalendarDateRange>(ranges.Count)
        {
            ranges[0]
        };

        for (var i = 1; i < ranges.Count; i++)
        {
            var current = ranges[i];
            var previous = merged[^1];
            if (current.Start <= previous.End.AddDays(1))
            {
                var mergedEnd = current.End > previous.End ? current.End : previous.End;
                merged[^1] = new CalendarDateRange(previous.Start, mergedEnd);
            }
            else
            {
                merged.Add(current);
            }
        }

        ranges.Clear();
        ranges.AddRange(merged);
    }

    private static void AddRangeAndMerge(List<CalendarDateRange> ranges, CalendarDateRange range)
    {
        ranges.Add(range);
        NormalizeAndMergeRanges(ranges);
    }

    private static bool TryRemoveDateFromRanges(List<CalendarDateRange> ranges, DateTime date)
    {
        var normalized = date.Date;
        for (var i = 0; i < ranges.Count; i++)
        {
            var range = ranges[i];
            if (!range.Contains(normalized))
            {
                continue;
            }

            ranges.RemoveAt(i);
            if (range.Start < normalized)
            {
                ranges.Insert(i, new CalendarDateRange(range.Start, normalized.AddDays(-1)));
                i++;
            }

            if (range.End > normalized)
            {
                ranges.Insert(i, new CalendarDateRange(normalized.AddDays(1), range.End));
            }

            NormalizeAndMergeRanges(ranges);
            return true;
        }

        return false;
    }

    private static bool AreDateListsEqual(IReadOnlyList<DateTime> left, IReadOnlyList<DateTime> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreRangesEqual(IReadOnlyList<CalendarDateRange> left, IReadOnlyList<CalendarDateRange> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (left[i].Start != right[i].Start || left[i].End != right[i].End)
            {
                return false;
            }
        }

        return true;
    }

    private static object[] BuildSelectionDelta(IReadOnlyList<DateTime> left, IReadOnlyList<DateTime> right)
    {
        var rightLookup = new HashSet<DateTime>(right);
        var delta = new List<object>();
        foreach (var date in left)
        {
            if (!rightLookup.Contains(date))
            {
                delta.Add(date);
            }
        }

        return delta.ToArray();
    }

    private DateTime CoerceDisplayDateToRange(DateTime date)
    {
        var normalized = NormalizeToMonthStart(date);
        if (DisplayDateStart.HasValue)
        {
            var monthStart = NormalizeToMonthStart(DisplayDateStart.Value);
            if (normalized < monthStart)
            {
                normalized = monthStart;
            }
        }

        if (DisplayDateEnd.HasValue)
        {
            var monthStart = NormalizeToMonthStart(DisplayDateEnd.Value);
            if (normalized > monthStart)
            {
                normalized = monthStart;
            }
        }

        return normalized;
    }

    private static DateTime NormalizeToMonthStart(DateTime date)
    {
        var normalized = date.Date;
        return new DateTime(normalized.Year, normalized.Month, 1);
    }
}
