using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class DatePicker : UserControl
{
    public static readonly RoutedEvent SelectedDateChangedEvent =
        new(nameof(SelectedDateChanged), RoutingStrategy.Bubble);

    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(
            nameof(SelectedDate),
            typeof(DateTime?),
            typeof(DatePicker),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is DatePicker datePicker)
                    {
                        datePicker.OnSelectedDateChanged((DateTime?)args.OldValue, (DateTime?)args.NewValue);
                    }
                },
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    if (dependencyObject is not DatePicker datePicker)
                    {
                        return value;
                    }

                    if (value is null)
                    {
                        return null;
                    }

                    if (value is DateTime date)
                    {
                        return datePicker.CoerceDateWithinRange(date.Date);
                    }

                    return null;
                }));

    public static readonly DependencyProperty DisplayDateProperty =
        DependencyProperty.Register(
            nameof(DisplayDate),
            typeof(DateTime),
            typeof(DatePicker),
            new FrameworkPropertyMetadata(
                DateTime.Today,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is DatePicker datePicker && args.NewValue is DateTime displayDate)
                    {
                        datePicker.OnDisplayDateChanged(displayDate);
                    }
                },
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    if (dependencyObject is not DatePicker datePicker)
                    {
                        return value;
                    }

                    var typed = value is DateTime date ? date : DateTime.Today;
                    return datePicker.CoerceDisplayDateMonthStart(typed);
                }));

    public static readonly DependencyProperty DisplayDateStartProperty =
        DependencyProperty.Register(
            nameof(DisplayDateStart),
            typeof(DateTime?),
            typeof(DatePicker),
            new FrameworkPropertyMetadata(
                null,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is DatePicker datePicker)
                    {
                        datePicker.OnDisplayRangeChanged();
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
            typeof(DatePicker),
            new FrameworkPropertyMetadata(
                null,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is DatePicker datePicker)
                    {
                        datePicker.OnDisplayRangeChanged();
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
            typeof(DatePicker),
            new FrameworkPropertyMetadata(
                DayOfWeek.Sunday,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is DatePicker datePicker && args.NewValue is DayOfWeek firstDayOfWeek)
                    {
                        datePicker.OnFirstDayOfWeekChanged(firstDayOfWeek);
                    }
                }));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(DatePicker),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is DatePicker datePicker)
                    {
                        datePicker.OnTextPropertyChanged(args.NewValue as string ?? string.Empty);
                    }
                }));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(DatePicker),
            new FrameworkPropertyMetadata(
                false,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is DatePicker datePicker && args.NewValue is bool isReadOnly)
                    {
                        datePicker.OnIsReadOnlyChanged(isReadOnly);
                    }
                }));

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(
            nameof(IsDropDownOpen),
            typeof(bool),
            typeof(DatePicker),
            new FrameworkPropertyMetadata(
                false,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is DatePicker datePicker && args.NewValue is bool isOpen)
                    {
                        datePicker.OnIsDropDownOpenChanged(isOpen);
                    }
                }));

    private readonly Grid _rootGrid;
    private readonly TextBox _textBox;
    private readonly Button _dropDownButton;
    private readonly Calendar _calendar;
    private Popup? _popup;
    private bool _isSynchronizingDropDown;
    private bool _isSynchronizingSelection;
    private bool _isSynchronizingText;

    public DatePicker()
    {
        BorderBrush = new Color(102, 102, 102);
        BorderThickness = new Thickness(0f);
        Padding = new Thickness(0f);

        _rootGrid = new Grid();
        _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
        _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _textBox = new TextBox
        {
            Margin = new Thickness(0f),
            Padding = new Thickness(8f, 2f, 8f, 2f),
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        _textBox.TextChanged += OnTextBoxTextChanged;
        _textBox.AddHandler<KeyRoutedEventArgs>(UIElement.KeyDownEvent, OnTextBoxKeyDown, handledEventsToo: true);
        _rootGrid.AddChild(_textBox);
        Grid.SetColumn(_textBox, 0);

        _dropDownButton = new Button
        {
            Content = "v",
            Width = 28f,
            Padding = new Thickness(0f),
            Margin = new Thickness(2f, 0f, 0f, 0f)
        };
        _dropDownButton.Click += OnDropDownButtonClick;
        _rootGrid.AddChild(_dropDownButton);
        Grid.SetColumn(_dropDownButton, 1);

        _calendar = new Calendar
        {
            Width = 244f,
            Height = 232f,
            SelectionMode = CalendarSelectionMode.SingleDate
        };
        _calendar.SelectedDateChanged += OnCalendarSelectedDateChanged;

        Content = _rootGrid;

        AddHandler<KeyRoutedEventArgs>(UIElement.KeyDownEvent, OnDatePickerKeyDown, handledEventsToo: true);

        Height = 32f;
        FirstDayOfWeek = DayOfWeek.Sunday;
        DisplayDate = DateTime.Today;
        SynchronizeTextWithSelection();
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

    public string Text
    {
        get => GetValue<string>(TextProperty) ?? string.Empty;
        set => SetValue(TextProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue<bool>(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => GetValue<bool>(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    protected internal TextBox TextBoxForTesting => _textBox;
    protected internal Button DropDownButtonForTesting => _dropDownButton;
    protected internal Calendar DropDownCalendarForTesting => _calendar;
    protected internal bool IsDropDownPopupOpenForTesting => _popup?.IsOpen ?? false;

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnVisualParentChanged(oldParent, newParent);
        if (newParent != null)
        {
            return;
        }

        _popup?.Close();
        if (IsDropDownOpen)
        {
            _isSynchronizingDropDown = true;
            try
            {
                IsDropDownOpen = false;
            }
            finally
            {
                _isSynchronizingDropDown = false;
            }
        }
    }

    private void OnSelectedDateChanged(DateTime? oldValue, DateTime? newValue)
    {
        if (newValue.HasValue)
        {
            var normalized = newValue.Value.Date;
            if (DisplayDate != normalized)
            {
                DisplayDate = normalized;
            }
        }

        SyncCalendarSelection();
        SynchronizeTextWithSelection();

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

    private void OnDisplayDateChanged(DateTime displayDate)
    {
        var normalized = displayDate.Date;
        SyncCalendarSelection();
        if (_calendar.DisplayDate != normalized)
        {
            _calendar.DisplayDate = normalized;
        }
    }

    private void OnDisplayRangeChanged()
    {
        if (DisplayDateStart.HasValue && DisplayDateEnd.HasValue && DisplayDateStart.Value > DisplayDateEnd.Value)
        {
            DisplayDateEnd = DisplayDateStart;
            return;
        }

        var normalizedDisplayDate = CoerceDisplayDateMonthStart(DisplayDate);
        if (normalizedDisplayDate != DisplayDate)
        {
            DisplayDate = normalizedDisplayDate;
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

        SyncCalendarSelection();
    }

    private void OnFirstDayOfWeekChanged(DayOfWeek firstDayOfWeek)
    {
        _calendar.FirstDayOfWeek = firstDayOfWeek;
    }

    private void OnTextPropertyChanged(string text)
    {
        if (_isSynchronizingText)
        {
            return;
        }

        if (!string.Equals(_textBox.Text, text, StringComparison.Ordinal))
        {
            _isSynchronizingText = true;
            try
            {
                _textBox.Text = text;
            }
            finally
            {
                _isSynchronizingText = false;
            }
        }

        TryApplyTextToSelection(text, commitInvalidText: false);
    }

    private void OnIsReadOnlyChanged(bool isReadOnly)
    {
        _textBox.IsReadOnly = isReadOnly;
        _dropDownButton.IsEnabled = !isReadOnly;
    }

    private void OnIsDropDownOpenChanged(bool isOpen)
    {
        if (_isSynchronizingDropDown)
        {
            return;
        }

        if (isOpen)
        {
            OpenDropDown();
            return;
        }

        CloseDropDown();
    }

    private void OnDropDownButtonClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (IsReadOnly)
        {
            return;
        }

        IsDropDownOpen = !IsDropDownOpen;
    }

    private void OnTextBoxTextChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_isSynchronizingText)
        {
            return;
        }

        Text = _textBox.Text;
    }

    private void OnTextBoxKeyDown(object? sender, KeyRoutedEventArgs args)
    {
        _ = sender;
        if (args.Key == Keys.F4 || ((args.Modifiers & ModifierKeys.Alt) != 0 && args.Key == Keys.Down))
        {
            if (!IsReadOnly)
            {
                IsDropDownOpen = !IsDropDownOpen;
            }

            args.Handled = true;
            return;
        }

        if (args.Key == Keys.Escape && IsDropDownOpen)
        {
            IsDropDownOpen = false;
            args.Handled = true;
            return;
        }

        if (args.Key == Keys.Enter)
        {
            TryApplyTextToSelection(_textBox.Text, commitInvalidText: true);
            args.Handled = true;
        }
    }

    private void OnCalendarSelectedDateChanged(object? sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_isSynchronizingSelection)
        {
            return;
        }

        _isSynchronizingSelection = true;
        try
        {
            SelectedDate = _calendar.SelectedDate;
        }
        finally
        {
            _isSynchronizingSelection = false;
        }

        IsDropDownOpen = false;
    }

    private void OnDatePickerKeyDown(object? sender, KeyRoutedEventArgs args)
    {
        _ = sender;
        if (args.Key == Keys.Escape && IsDropDownOpen)
        {
            IsDropDownOpen = false;
            args.Handled = true;
        }
    }

    private void OpenDropDown()
    {
        var host = FindHostPanel();
        if (host == null)
        {
            _isSynchronizingDropDown = true;
            try
            {
                IsDropDownOpen = false;
            }
            finally
            {
                _isSynchronizingDropDown = false;
            }

            return;
        }

        EnsurePopup();
        SyncCalendarSelection();
        if (_popup == null)
        {
            return;
        }

        _popup.PlacementTarget = this;
        _popup.PlacementMode = PopupPlacementMode.Bottom;
        _popup.HorizontalOffset = 0f;
        _popup.VerticalOffset = 2f;
        _popup.Width = float.NaN;
        _popup.Height = float.NaN;
        _popup.Show(host);
    }

    private void CloseDropDown()
    {
        _popup?.Close();
    }

    private void EnsurePopup()
    {
        if (_popup != null)
        {
            return;
        }

        _popup = new Popup
        {
            Title = string.Empty,
            TitleBarHeight = 0f,
            CanClose = false,
            CanDragMove = false,
            DismissOnOutsideClick = true,
            Content = _calendar,
            BorderThickness = 1f,
            Padding = new Thickness(0f)
        };

        _popup.Closed += (_, _) =>
        {
            if (!IsDropDownOpen)
            {
                return;
            }

            _isSynchronizingDropDown = true;
            try
            {
                IsDropDownOpen = false;
            }
            finally
            {
                _isSynchronizingDropDown = false;
            }
        };
    }

    private void SyncCalendarSelection()
    {
        _isSynchronizingSelection = true;
        try
        {
            // DatePicker remains single-date by design (WPF parity).
            _calendar.SelectionMode = CalendarSelectionMode.SingleDate;
            _calendar.DisplayDateStart = DisplayDateStart;
            _calendar.DisplayDateEnd = DisplayDateEnd;
            _calendar.FirstDayOfWeek = FirstDayOfWeek;
            _calendar.DisplayDate = DisplayDate;
            _calendar.SelectedDate = SelectedDate;
        }
        finally
        {
            _isSynchronizingSelection = false;
        }
    }

    private void SynchronizeTextWithSelection()
    {
        var nextText = SelectedDate.HasValue
            ? SelectedDate.Value.ToString("d", CultureInfo.CurrentCulture)
            : string.Empty;
        if (string.Equals(Text, nextText, StringComparison.Ordinal) &&
            string.Equals(_textBox.Text, nextText, StringComparison.Ordinal))
        {
            return;
        }

        _isSynchronizingText = true;
        try
        {
            Text = nextText;
            _textBox.Text = nextText;
        }
        finally
        {
            _isSynchronizingText = false;
        }
    }

    private void TryApplyTextToSelection(string text, bool commitInvalidText)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (SelectedDate != null)
            {
                SelectedDate = null;
            }

            return;
        }

        if (TryParseDateText(text, out var parsedDate))
        {
            if (SelectedDate != parsedDate)
            {
                SelectedDate = parsedDate;
            }

            if (commitInvalidText)
            {
                SynchronizeTextWithSelection();
            }

            return;
        }

        if (commitInvalidText)
        {
            SynchronizeTextWithSelection();
        }
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

    private DateTime CoerceDisplayDateMonthStart(DateTime date)
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

    private static bool TryParseDateText(string text, out DateTime date)
    {
        var trimmed = text.Trim();
        if (DateTime.TryParse(
                trimmed,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var currentCultureDate))
        {
            date = currentCultureDate.Date;
            return true;
        }

        if (DateTime.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                out var invariantDate))
        {
            date = invariantDate.Date;
            return true;
        }

        date = default;
        return false;
    }

    private Panel? FindHostPanel()
    {
        return Popup.ResolveOverlayHost(this);
    }
}
