using System;
using System.Collections.Generic;
using System.Globalization;
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

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 600, 360));
    }
}
