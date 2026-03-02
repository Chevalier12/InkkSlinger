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

    public DateTime? SelectedDate
    {
        get => GetValue<DateTime?>(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

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

    private void OnSelectedDateChanged(DateTime? oldValue, DateTime? newValue)
    {
        if (newValue.HasValue)
        {
            var selectedDate = newValue.Value.Date;
            if (DisplayDate.Year != selectedDate.Year || DisplayDate.Month != selectedDate.Month)
            {
                DisplayDate = selectedDate;
            }
        }

        UpdateCalendarView();

        var removedItems = oldValue.HasValue
            ? new object[] { oldValue.Value }
            : Array.Empty<object>();
        var addedItems = newValue.HasValue
            ? new object[] { newValue.Value }
            : Array.Empty<object>();
        RaiseRoutedEvent(
            SelectedDateChangedEvent,
            new SelectionChangedEventArgs(SelectedDateChangedEvent, removedItems, addedItems));
    }

    private void OnDisplayRangeChanged()
    {
        if (DisplayDateStart.HasValue && DisplayDateEnd.HasValue && DisplayDateStart.Value > DisplayDateEnd.Value)
        {
            DisplayDateEnd = DisplayDateStart;
            return;
        }

        if (SelectedDate.HasValue)
        {
            var selected = SelectedDate.Value.Date;
            var coerced = CoerceDateWithinRange(selected);
            if (coerced != selected)
            {
                SelectedDate = coerced;
            }
        }

        var coercedDisplayDate = CoerceDisplayDateToRange(DisplayDate);
        if (coercedDisplayDate != DisplayDate)
        {
            DisplayDate = coercedDisplayDate;
        }
        else
        {
            UpdateCalendarView();
        }
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
        if (!IsDateSelectable(date))
        {
            return;
        }

        SelectedDate = date;
    }

    private void OnKeyDown(object? sender, KeyRoutedEventArgs args)
    {
        if (args.Modifiers != ModifierKeys.None)
        {
            return;
        }

        switch (args.Key)
        {
            case Keys.Left:
                MoveSelectionByDays(-1);
                args.Handled = true;
                break;
            case Keys.Right:
                MoveSelectionByDays(1);
                args.Handled = true;
                break;
            case Keys.Up:
                MoveSelectionByDays(-7);
                args.Handled = true;
                break;
            case Keys.Down:
                MoveSelectionByDays(7);
                args.Handled = true;
                break;
            case Keys.PageUp:
                MoveSelectionByMonths(-1);
                args.Handled = true;
                break;
            case Keys.PageDown:
                MoveSelectionByMonths(1);
                args.Handled = true;
                break;
            case Keys.Home:
                SelectDate(new DateTime(DisplayDate.Year, DisplayDate.Month, 1));
                args.Handled = true;
                break;
            case Keys.End:
                SelectDate(new DateTime(DisplayDate.Year, DisplayDate.Month, DateTime.DaysInMonth(DisplayDate.Year, DisplayDate.Month)));
                args.Handled = true;
                break;
        }
    }

    private void MoveSelectionByDays(int dayDelta)
    {
        var anchor = (SelectedDate ?? DisplayDate).Date;
        SelectDate(anchor.AddDays(dayDelta));
    }

    private void MoveSelectionByMonths(int monthDelta)
    {
        var anchor = (SelectedDate ?? DisplayDate).Date;
        SelectDate(anchor.AddMonths(monthDelta));
    }

    private void SelectDate(DateTime date)
    {
        var normalized = CoerceDateWithinRange(date.Date);
        if (SelectedDate != normalized)
        {
            SelectedDate = normalized;
            return;
        }

        if (DisplayDate.Year != normalized.Year || DisplayDate.Month != normalized.Month)
        {
            DisplayDate = normalized;
            return;
        }

        UpdateCalendarView();
    }

    private void UpdateCalendarView()
    {
        var displayMonth = NormalizeToMonthStart(DisplayDate);
        _monthLabel.Text = displayMonth.ToString("Y", CultureInfo.CurrentCulture);

        var abbreviatedDayNames = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
        for (var i = 0; i < _weekDayLabels.Length; i++)
        {
            var index = (((int)FirstDayOfWeek + i) % 7 + 7) % 7;
            _weekDayLabels[i].Text = abbreviatedDayNames[index];
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
            var isSelected = SelectedDate.HasValue && SelectedDate.Value.Date == date;
            var isToday = date == today;
            var isEnabled = IsDateSelectable(date);

            button.IsEnabled = isEnabled;
            button.Background = ResolveDayButtonBackground(inDisplayMonth, isSelected, isToday);
            button.Foreground = ResolveDayButtonForeground(inDisplayMonth, isEnabled, isSelected);
            button.BorderBrush = isSelected
                ? new Color(160, 205, 255)
                : isToday
                ? new Color(122, 122, 140)
                : new Color(78, 78, 78);
        }

        _previousMonthButton.IsEnabled = CanDisplayMonth(displayMonth.AddMonths(-1));
        _nextMonthButton.IsEnabled = CanDisplayMonth(displayMonth.AddMonths(1));
    }

    private static Color ResolveDayButtonBackground(bool inDisplayMonth, bool isSelected, bool isToday)
    {
        if (isSelected)
        {
            return new Color(64, 100, 146);
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

    private static Color ResolveDayButtonForeground(bool inDisplayMonth, bool isEnabled, bool isSelected)
    {
        if (!isEnabled)
        {
            return new Color(96, 96, 96);
        }

        if (isSelected)
        {
            return Color.White;
        }

        return inDisplayMonth
            ? new Color(236, 236, 236)
            : new Color(150, 150, 150);
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
