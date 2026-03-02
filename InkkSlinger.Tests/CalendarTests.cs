using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class CalendarTests
{
    [Fact]
    public void SelectedDate_SetProperty_UpdatesDisplayedMonth()
    {
        var (_, calendar) = CreateFixture();

        calendar.SelectedDate = new DateTime(2026, 12, 24);

        Assert.Equal(2026, calendar.DisplayDate.Year);
        Assert.Equal(12, calendar.DisplayDate.Month);
        Assert.Contains("2026", calendar.MonthLabelTextForTesting, StringComparison.Ordinal);
    }

    [Fact]
    public void ClickingVisibleDayButton_UpdatesSelectedDate()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.DisplayDate = new DateTime(2026, 3, 1);
        RunLayout(uiRoot);

        var expectedDate = new DateTime(2026, 3, 18);
        Assert.True(calendar.TryGetDayButtonIndexForDateForTesting(expectedDate, out var buttonIndex));
        var dayButton = calendar.DayButtonsForTesting[buttonIndex];

        Click(uiRoot, GetCenter(dayButton));

        Assert.Equal(expectedDate, calendar.SelectedDate?.Date);
    }

    [Fact]
    public void RightArrow_WhenCalendarFocused_MovesSelectionByOneDay()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.DisplayDate = new DateTime(2026, 3, 1);
        RunLayout(uiRoot);

        var initialDate = new DateTime(2026, 3, 18);
        Assert.True(calendar.TryGetDayButtonIndexForDateForTesting(initialDate, out var buttonIndex));
        var dayButton = calendar.DayButtonsForTesting[buttonIndex];
        Click(uiRoot, GetCenter(dayButton));

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));

        Assert.Equal(new DateTime(2026, 3, 19), calendar.SelectedDate?.Date);
    }

    [Fact]
    public void Calendar_WithConstrainedHeight_KeepsDayButtonsInsideBounds()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.Height = 180f;
        calendar.DisplayDate = new DateTime(2026, 8, 1);
        RunLayout(uiRoot);

        var calendarBottom = calendar.LayoutSlot.Y + calendar.LayoutSlot.Height;
        foreach (var dayButton in calendar.DayButtonsForTesting)
        {
            var dayButtonBottom = dayButton.LayoutSlot.Y + dayButton.LayoutSlot.Height;
            Assert.True(
                dayButtonBottom <= calendarBottom + 0.001f,
                $"Day button overflowed calendar bounds. ButtonBottom={dayButtonBottom}, CalendarBottom={calendarBottom}");
        }
    }

    private static (UiRoot UiRoot, Calendar Calendar) CreateFixture()
    {
        var host = new Canvas
        {
            Width = 600f,
            Height = 420f
        };

        var calendar = new Calendar
        {
            Width = 280f,
            Height = 260f
        };
        host.AddChild(calendar);
        Canvas.SetLeft(calendar, 40f);
        Canvas.SetTop(calendar, 30f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        return (uiRoot, calendar);
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

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 600, 420));
    }
}
