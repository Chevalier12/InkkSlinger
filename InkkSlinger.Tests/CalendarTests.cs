using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

    [Fact]
    public void CalendarDayButtons_WithAppButtonStyle_KeepDayTextOnTemplatedButtons()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var (uiRoot, calendar) = CreateFixture();
            calendar.DisplayDate = new DateTime(2026, 3, 1);
            RunLayout(uiRoot);

            var dayButton = Assert.IsType<CalendarDayButton>(calendar.DayButtonsForTesting[0]);
            Assert.True(dayButton.ApplyTemplate());
            Assert.NotEmpty(dayButton.GetVisualChildren());
            Assert.NotEmpty(dayButton.DayText);
            Assert.Equal(dayButton.DayText, Assert.IsType<string>(dayButton.Content));
            Assert.Null(FindFirstVisualChild<CalendarDayTextPresenter>(dayButton));
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CalendarDayButtons_DayTextAndContentStayInSync()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.DisplayDate = new DateTime(2026, 3, 1);
        RunLayout(uiRoot);

        var dayButton = Assert.IsType<CalendarDayButton>(calendar.DayButtonsForTesting[0]);
        dayButton.DayText = "27";
        var dayTextPresenter = Assert.IsType<CalendarDayTextPresenter>(FindFirstVisualChild<CalendarDayTextPresenter>(dayButton));
        Assert.Equal("27", dayButton.DayText);
        Assert.Equal("27", dayTextPresenter.Text);

        dayButton.Content = "31";
        Assert.Equal("31", dayButton.DayText);
        Assert.Equal("31", dayTextPresenter.Text);
    }

    [Fact]
    public void Calendar_PublishesRetainedRenderChildren_ForRetainedTraversal()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.DisplayDate = new DateTime(2026, 3, 1);
        RunLayout(uiRoot);

        Assert.NotEmpty(calendar.GetRetainedRenderChildren());
    }

    [Fact]
    public void CalendarDayButtons_HoverMovesShadowToCurrentDate()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var (uiRoot, calendar) = CreateFixture();
            calendar.DisplayDate = new DateTime(2026, 3, 1);
            RunLayout(uiRoot);

            var firstDate = new DateTime(2026, 4, 5);
            var secondDate = new DateTime(2026, 3, 12);
            Assert.True(calendar.TryGetDayButtonIndexForDateForTesting(firstDate, out var firstIndex));
            Assert.True(calendar.TryGetDayButtonIndexForDateForTesting(secondDate, out var secondIndex));

            var firstButton = Assert.IsType<CalendarDayButton>(calendar.DayButtonsForTesting[firstIndex]);
            var secondButton = Assert.IsType<CalendarDayButton>(calendar.DayButtonsForTesting[secondIndex]);
            Assert.True(firstButton.ApplyTemplate());
            Assert.True(secondButton.ApplyTemplate());

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(GetCenter(firstButton)));
            for (var i = 0; i < 12; i++)
            {
                RunLayout(uiRoot, 32 + (i * 16));
            }

            var firstShadow = GetDayButtonShadow(firstButton);
            var secondShadow = GetDayButtonShadow(secondButton);

            Assert.True(firstButton.IsMouseOver);
            Assert.False(secondButton.IsMouseOver);
            Assert.True(firstShadow.BlurRadius > 0f, $"Expected first hovered shadow blur > 0, actual {firstShadow.BlurRadius}.");
            Assert.True(firstShadow.Opacity > 0f, $"Expected first hovered shadow opacity > 0, actual {firstShadow.Opacity}.");
            Assert.True(secondShadow.BlurRadius <= 0.001f, $"Expected other shadow blur ~0, actual {secondShadow.BlurRadius}.");
            Assert.True(secondShadow.Opacity <= 0.001f, $"Expected other shadow opacity ~0, actual {secondShadow.Opacity}.");

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(GetCenter(secondButton)));
            for (var i = 0; i < 12; i++)
            {
                RunLayout(uiRoot, 256 + (i * 16));
            }

            firstShadow = GetDayButtonShadow(firstButton);
            secondShadow = GetDayButtonShadow(secondButton);

            Assert.False(firstButton.IsMouseOver);
            Assert.True(secondButton.IsMouseOver);
            Assert.True(firstShadow.BlurRadius <= 0.001f, $"Expected previous shadow blur ~0 after leave, actual {firstShadow.BlurRadius}.");
            Assert.True(firstShadow.Opacity <= 0.001f, $"Expected previous shadow opacity ~0 after leave, actual {firstShadow.Opacity}.");
            Assert.True(secondShadow.BlurRadius > 0f, $"Expected current hovered shadow blur > 0, actual {secondShadow.BlurRadius}.");
            Assert.True(secondShadow.Opacity > 0f, $"Expected current hovered shadow opacity > 0, actual {secondShadow.Opacity}.");
        }
        finally
        {
            RestoreApplicationResources(backup);
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

    private static DropShadowEffect GetDayButtonShadow(CalendarDayButton button)
    {
        var chrome = Assert.IsType<Border>(Assert.Single(button.GetVisualChildren()));
        return Assert.IsType<DropShadowEffect>(chrome.Effect);
    }

    private static void RunLayout(UiRoot uiRoot)
    {
        RunLayout(uiRoot, 16);
    }

    private static void RunLayout(UiRoot uiRoot, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 600, 420));
    }

    private static void LoadRootAppResources()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        string? appPath = null;
        while (currentDirectory != null)
        {
            var candidate = Path.Combine(currentDirectory.FullName, "App.xml");
            if (File.Exists(candidate))
            {
                appPath = candidate;
                break;
            }

            currentDirectory = currentDirectory.Parent;
        }

        Assert.True(File.Exists(appPath), $"Expected App.xml to exist at '{appPath}'.");
        XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);
    }

    private static ResourceSnapshot CaptureApplicationResources()
    {
        var resources = UiApplication.Current.Resources;
        return new ResourceSnapshot(
            resources.ToList(),
            resources.MergedDictionaries.ToList());
    }

    private static void RestoreApplicationResources(ResourceSnapshot snapshot)
    {
        TestApplicationResources.Restore(snapshot.Entries, snapshot.MergedDictionaries);
    }

    private sealed record ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        List<ResourceDictionary> MergedDictionaries);
}
