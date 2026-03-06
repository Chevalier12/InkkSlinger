using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DatePickerTests
{
    [Fact]
    public void DropDownButtonClick_OpensPopup_AndOutsideClickClosesIt()
    {
        var (uiRoot, datePicker) = CreateFixture();

        Click(uiRoot, GetCenter(datePicker.DropDownButtonForTesting));

        Assert.True(datePicker.IsDropDownOpen);
        Assert.True(datePicker.IsDropDownPopupOpenForTesting);

        Click(uiRoot, new Vector2(6f, 6f));

        Assert.False(datePicker.IsDropDownOpen);
        Assert.False(datePicker.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void SelectingDateFromDropDownCalendar_UpdatesSelectedDateAndText_ThenClosesPopup()
    {
        var (uiRoot, datePicker) = CreateFixture();
        datePicker.DisplayDate = new DateTime(2026, 3, 1);
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(datePicker.DropDownButtonForTesting));
        RunLayout(uiRoot);

        var selectedDate = new DateTime(2026, 3, 18);
        var calendar = datePicker.DropDownCalendarForTesting;
        Assert.True(calendar.TryGetDayButtonIndexForDateForTesting(selectedDate, out var buttonIndex));
        var dayButton = calendar.DayButtonsForTesting[buttonIndex];

        Click(uiRoot, GetCenter(dayButton));

        Assert.Equal(selectedDate, datePicker.SelectedDate?.Date);
        Assert.Equal(selectedDate.ToString("d", CultureInfo.CurrentCulture), datePicker.Text);
        Assert.False(datePicker.IsDropDownOpen);
        Assert.False(datePicker.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void TextEntry_ValidDateUpdatesSelection_InvalidDateKeepsPreviousSelection()
    {
        var (_, datePicker) = CreateFixture();
        var expected = new DateTime(2026, 4, 2);

        datePicker.TextBoxForTesting.Text = expected.ToString("d", CultureInfo.CurrentCulture);

        Assert.Equal(expected, datePicker.SelectedDate?.Date);

        datePicker.TextBoxForTesting.Text = "not-a-date";

        Assert.Equal(expected, datePicker.SelectedDate?.Date);
        Assert.Equal("not-a-date", datePicker.Text);
    }

    [Fact]
    public void IsReadOnly_True_DisablesDropDownButtonAndTextEditing()
    {
        var (uiRoot, datePicker) = CreateFixture();
        datePicker.IsReadOnly = true;
        RunLayout(uiRoot);

        Assert.False(datePicker.DropDownButtonForTesting.IsEnabled);
        Assert.True(datePicker.TextBoxForTesting.IsReadOnly);

        Click(uiRoot, GetCenter(datePicker.DropDownButtonForTesting));
        Assert.False(datePicker.IsDropDownOpen);
    }

    [Fact]
    public void TextEntry_OutOfRangeDate_IsNotCommittedWhenBoundarySet()
    {
        var (uiRoot, datePicker) = CreateFixture();
        datePicker.DisplayDateStart = new DateTime(2026, 3, 10);
        datePicker.DisplayDateEnd = new DateTime(2026, 3, 20);
        RunLayout(uiRoot);

        datePicker.TextBoxForTesting.Text = "2026-03-05";
        Click(uiRoot, GetCenter(datePicker.TextBoxForTesting));
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter));

        Assert.Equal(new DateTime(2026, 3, 10), datePicker.SelectedDate);
    }

    [Fact]
    public void EnterKey_CommitsTextAndNormalizesDisplay()
    {
        var (uiRoot, datePicker) = CreateFixture();
        RunLayout(uiRoot);

        datePicker.TextBoxForTesting.Text = "2026-04-10";
        Click(uiRoot, GetCenter(datePicker.TextBoxForTesting));
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter));

        Assert.Equal(new DateTime(2026, 4, 10), datePicker.SelectedDate);
        var expectedText = new DateTime(2026, 4, 10).ToString("d", CultureInfo.CurrentCulture);
        Assert.Equal(expectedText, datePicker.Text);
    }

    [Fact]
    public void EscapeKey_ClosesDropDown()
    {
        var (uiRoot, datePicker) = CreateFixture();
        RunLayout(uiRoot);

        Click(uiRoot, GetCenter(datePicker.DropDownButtonForTesting));
        RunLayout(uiRoot);
        Assert.True(datePicker.IsDropDownOpen);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape));
        Assert.False(datePicker.IsDropDownOpen);
    }

    [Fact]
    public void DropDownCalendar_RemainsSingleDateMode()
    {
        var (_, datePicker) = CreateFixture();
        Assert.Equal(CalendarSelectionMode.SingleDate, datePicker.DropDownCalendarForTesting.SelectionMode);
    }

    [Fact]
    public void InternalTextBox_UsesSingleLineHiddenScrollConfiguration()
    {
        var (_, datePicker) = CreateFixture();

        Assert.Equal(TextWrapping.NoWrap, datePicker.TextBoxForTesting.TextWrapping);
        Assert.Equal(ScrollBarVisibility.Hidden, datePicker.TextBoxForTesting.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Disabled, datePicker.TextBoxForTesting.VerticalScrollBarVisibility);
        Assert.Equal(2f, datePicker.TextBoxForTesting.Padding.Top);
        Assert.Equal(2f, datePicker.TextBoxForTesting.Padding.Bottom);
    }

    [Fact]
    public void TypingShortText_DoesNotScrollDatePickerTextBoxVertically()
    {
        var (uiRoot, datePicker) = CreateFixture();
        var pointer = GetCenter(datePicker.TextBoxForTesting);

        Click(uiRoot, pointer);
        datePicker.TextBoxForTesting.Text = string.Empty;
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreateTextInputDelta('a', pointer));
        RunLayout(uiRoot);
        Assert.Equal(0f, datePicker.TextBoxForTesting.VerticalOffsetForTesting);

        uiRoot.RunInputDeltaForTests(CreateTextInputDelta('s', pointer));
        RunLayout(uiRoot);
        Assert.Equal(0f, datePicker.TextBoxForTesting.VerticalOffsetForTesting);
        Assert.Equal("as", datePicker.TextBoxForTesting.Text);
    }

    private static (UiRoot UiRoot, DatePicker DatePicker) CreateFixture()
    {
        var host = new Canvas
        {
            Width = 600f,
            Height = 360f
        };

        var datePicker = new DatePicker
        {
            Width = 260f,
            Height = 32f
        };
        host.AddChild(datePicker);
        Canvas.SetLeft(datePicker, 60f);
        Canvas.SetTop(datePicker, 60f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        return (uiRoot, datePicker);
    }

    private static Vector2 GetCenter(FrameworkElement element)
    {
        var slot = element.LayoutSlot;
        return new Vector2(slot.X + (slot.Width / 2f), slot.Y + (slot.Height / 2f));
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static InputDelta CreateKeyDownDelta(Keys key)
    {
        var pointer = new Vector2(16f, 16f);
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys> { key },
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool leftPressed = false, bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = true,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreateTextInputDelta(char character, Vector2 pointer)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char> { character },
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 600, 360));
    }
}
