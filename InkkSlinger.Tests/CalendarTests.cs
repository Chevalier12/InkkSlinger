using System;
using System.Collections.Generic;
using System.Globalization;
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

    [Fact]
    public void Measure_DoesNotForceInitialCalendarRefresh()
    {
        var calendar = new Calendar
        {
            Width = 280f,
            Height = 260f
        };

        calendar.Measure(new Vector2(280f, 260f));

        Assert.Equal(0, calendar.CalendarViewRefreshCountForTesting);
    }

    [Fact]
    public void DisplayDateStart_PreventsNavigationBeforeStart()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.DisplayDateStart = new DateTime(2026, 3, 10);
        calendar.DisplayDate = new DateTime(2026, 3, 15);
        RunLayout(uiRoot);

        Assert.False(calendar.PreviousMonthButtonForTesting.IsEnabled);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.PageUp));
        Assert.Equal(3, calendar.DisplayDate.Month);
    }

    [Fact]
    public void DisplayDateEnd_PreventsNavigationAfterEnd()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.DisplayDateEnd = new DateTime(2026, 3, 20);
        calendar.DisplayDate = new DateTime(2026, 3, 15);
        RunLayout(uiRoot);

        Assert.False(calendar.NextMonthButtonForTesting.IsEnabled);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.PageDown));
        Assert.Equal(3, calendar.DisplayDate.Month);
    }

    [Fact]
    public void SelectedDate_OutsideRange_IsClampedToRange()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.DisplayDateStart = new DateTime(2026, 3, 10);
        calendar.DisplayDateEnd = new DateTime(2026, 3, 20);

        calendar.SelectedDate = new DateTime(2026, 3, 5);
        Assert.Equal(new DateTime(2026, 3, 10), calendar.SelectedDate);

        calendar.SelectedDate = new DateTime(2026, 3, 25);
        Assert.Equal(new DateTime(2026, 3, 20), calendar.SelectedDate);
    }

    [Fact]
    public void FirstDayOfWeek_Monday_UpdatesHeaderLabels()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.FirstDayOfWeek = DayOfWeek.Monday;
        RunLayout(uiRoot);

        var shortestDayNames = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames;
        Assert.Equal(shortestDayNames[(int)DayOfWeek.Monday], calendar.WeekDayLabelsForTesting[0].Text);
    }

    [Fact]
    public void KeyboardNavigation_PastMonthBoundary_AdvancesDisplayMonth()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.DisplayDate = new DateTime(2026, 3, 1);
        calendar.SelectedDate = new DateTime(2026, 3, 31);
        RunLayout(uiRoot);

        Assert.True(calendar.TryGetDayButtonIndexForDateForTesting(calendar.SelectedDate.Value, out var index));
        Click(uiRoot, GetCenter(calendar.DayButtonsForTesting[index]));

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));

        Assert.Equal(new DateTime(2026, 4, 1), calendar.SelectedDate);
        Assert.Equal(4, calendar.DisplayDate.Month);
    }

    [Fact]
    public void CalendarView_XamlInitialization_CoalescesInitialCalendarRefresh()
    {
        var view = new CalendarView
        {
            Width = 600f,
            Height = 420f
        };

        var calendar = FindFirstVisualChild<Calendar>(view);
        Assert.NotNull(calendar);
        Assert.Equal(0, calendar!.CalendarViewRefreshCountForTesting);

        var host = new Canvas
        {
            Width = 600f,
            Height = 420f
        };
        host.AddChild(view);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        Assert.Equal(1, calendar.CalendarViewRefreshCountForTesting);
        Assert.Equal(DayOfWeek.Monday, calendar.FirstDayOfWeek);
        Assert.Equal(CalendarSelectionMode.SingleRange, calendar.SelectionMode);

        var shortestDayNames = CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames;
        Assert.Equal(shortestDayNames[(int)DayOfWeek.Monday], calendar.WeekDayLabelsForTesting[0].Text);
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

    private static TElement? FindFirstVisualChild<TElement>(UIElement root)
        where TElement : UIElement
    {
        if (root is TElement match)
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild<TElement>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
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
