using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class CalendarRangeSelectionTests
{
    [Fact]
    public void SelectionMode_Default_IsSingleDate()
    {
        var (_, calendar) = CreateFixture();
        Assert.Equal(CalendarSelectionMode.SingleDate, calendar.SelectionMode);
    }

    [Fact]
    public void SelectionMode_None_ClearsSelectionAndBlocksPointerSelection()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.SelectedDate = new DateTime(2026, 3, 18);
        calendar.SelectionMode = CalendarSelectionMode.None;
        RunLayout(uiRoot);

        var targetDate = new DateTime(2026, 3, 22);
        Assert.True(calendar.TryGetDayButtonIndexForDateForTesting(targetDate, out var index));
        Click(uiRoot, GetCenter(calendar.DayButtonsForTesting[index]));

        Assert.Null(calendar.SelectedDate);
        Assert.Empty(calendar.SelectedDates);
    }

    [Fact]
    public void SelectedDate_SetProperty_UpdatesSelectedDatesProjection()
    {
        var (_, calendar) = CreateFixture();
        var selected = new DateTime(2026, 3, 14);

        calendar.SelectedDate = selected;

        var only = Assert.Single(calendar.SelectedDates);
        Assert.Equal(selected, only);
    }

    [Fact]
    public void SetSelectedDates_NormalizesSortsAndMergesContiguousValues()
    {
        var (_, calendar) = CreateFixture();
        calendar.SelectionMode = CalendarSelectionMode.MultipleRange;

        calendar.SetSelectedDates(
        [
            new DateTime(2026, 3, 5),
            new DateTime(2026, 3, 4),
            new DateTime(2026, 3, 4),
            new DateTime(2026, 3, 8),
            new DateTime(2026, 3, 7),
            new DateTime(2026, 3, 6)
        ]);

        Assert.Equal(
            [
                new DateTime(2026, 3, 4),
                new DateTime(2026, 3, 5),
                new DateTime(2026, 3, 6),
                new DateTime(2026, 3, 7),
                new DateTime(2026, 3, 8)
            ],
            calendar.SelectedDates);
    }

    [Fact]
    public void SingleRange_ShiftClick_SelectsContiguousRangeFromAnchor()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.SelectionMode = CalendarSelectionMode.SingleRange;
        calendar.DisplayDate = new DateTime(2026, 3, 1);
        RunLayout(uiRoot);

        ClickDate(uiRoot, calendar, new DateTime(2026, 3, 10));
        PressModifier(uiRoot, Keys.LeftShift);
        ClickDate(uiRoot, calendar, new DateTime(2026, 3, 14), heldModifiers: [Keys.LeftShift]);
        ReleaseModifier(uiRoot, Keys.LeftShift);

        Assert.Equal(new DateTime(2026, 3, 14), calendar.SelectedDate);
        Assert.Equal(5, calendar.SelectedDates.Count);
        Assert.Contains(new DateTime(2026, 3, 10), calendar.SelectedDates);
        Assert.Contains(new DateTime(2026, 3, 14), calendar.SelectedDates);
    }

    [Fact]
    public void MultipleRange_CtrlClick_TogglesSingleDateSelection()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.SelectionMode = CalendarSelectionMode.MultipleRange;
        calendar.DisplayDate = new DateTime(2026, 3, 1);
        RunLayout(uiRoot);

        ClickDate(uiRoot, calendar, new DateTime(2026, 3, 12));
        Assert.Single(calendar.SelectedDates);

        PressModifier(uiRoot, Keys.LeftControl);
        ClickDate(uiRoot, calendar, new DateTime(2026, 3, 12), heldModifiers: [Keys.LeftControl]);
        ReleaseModifier(uiRoot, Keys.LeftControl);

        Assert.Null(calendar.SelectedDate);
        Assert.Empty(calendar.SelectedDates);
    }

    [Fact]
    public void KeyboardShiftNavigation_ExtendsRangeAcrossMonthBoundary()
    {
        var (uiRoot, calendar) = CreateFixture();
        calendar.SelectionMode = CalendarSelectionMode.SingleRange;
        calendar.DisplayDate = new DateTime(2026, 3, 1);
        calendar.SelectedDate = new DateTime(2026, 3, 31);
        RunLayout(uiRoot);
        ClickDate(uiRoot, calendar, new DateTime(2026, 3, 31));

        PressModifier(uiRoot, Keys.LeftShift);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right, heldModifiers: [Keys.LeftShift]));
        ReleaseModifier(uiRoot, Keys.LeftShift);

        Assert.Equal(new DateTime(2026, 4, 1), calendar.SelectedDate);
        Assert.Equal(2, calendar.SelectedDates.Count);
        Assert.Contains(new DateTime(2026, 3, 31), calendar.SelectedDates);
        Assert.Contains(new DateTime(2026, 4, 1), calendar.SelectedDates);
    }

    [Fact]
    public void SelectedDatesChanged_RaisesAddedAndRemovedPayloads()
    {
        var (_, calendar) = CreateFixture();
        calendar.SelectionMode = CalendarSelectionMode.MultipleRange;

        SelectionChangedEventArgs? lastArgs = null;
        calendar.SelectedDatesChanged += (_, args) => lastArgs = args;

        calendar.SetSelectedDates([new DateTime(2026, 3, 1), new DateTime(2026, 3, 2)]);
        Assert.NotNull(lastArgs);
        Assert.Equal(2, lastArgs!.AddedItems.Count);
        Assert.Empty(lastArgs.RemovedItems);

        calendar.SetSelectedDates([new DateTime(2026, 3, 2), new DateTime(2026, 3, 3)]);
        Assert.NotNull(lastArgs);
        Assert.Single(lastArgs!.RemovedItems);
        Assert.Single(lastArgs.AddedItems);
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

    private static void ClickDate(UiRoot uiRoot, Calendar calendar, DateTime date, Keys[]? heldModifiers = null)
    {
        Assert.True(calendar.TryGetDayButtonIndexForDateForTesting(date, out var index));
        var pointer = GetCenter(calendar.DayButtonsForTesting[index]);
        Click(uiRoot, pointer, heldModifiers);
    }

    private static Vector2 GetCenter(FrameworkElement element)
    {
        var slot = element.LayoutSlot;
        return new Vector2(slot.X + (slot.Width / 2f), slot.Y + (slot.Height / 2f));
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer, Keys[]? heldModifiers = null)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, heldModifiers, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, heldModifiers, leftReleased: true));
    }

    private static void PressModifier(UiRoot uiRoot, Keys modifier)
    {
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(modifier, heldModifiers: [modifier]));
    }

    private static void ReleaseModifier(UiRoot uiRoot, Keys modifier)
    {
        uiRoot.RunInputDeltaForTests(CreateKeyUpDelta(modifier));
    }

    private static InputDelta CreateKeyDownDelta(Keys key, Keys[]? heldModifiers = null)
    {
        var pointer = new Vector2(16f, 16f);
        var modifiers = heldModifiers ?? [];
        return new InputDelta
        {
            Previous = new InputSnapshot(new KeyboardState(), default, pointer),
            Current = new InputSnapshot(new KeyboardState(modifiers), default, pointer),
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

    private static InputDelta CreateKeyUpDelta(Keys key)
    {
        var pointer = new Vector2(16f, 16f);
        return new InputDelta
        {
            Previous = new InputSnapshot(new KeyboardState(key), default, pointer),
            Current = new InputSnapshot(new KeyboardState(), default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys> { key },
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

    private static InputDelta CreatePointerDelta(Vector2 pointer, Keys[]? heldModifiers, bool leftPressed = false, bool leftReleased = false)
    {
        var modifiers = heldModifiers ?? [];
        return new InputDelta
        {
            Previous = new InputSnapshot(new KeyboardState(modifiers), default, pointer),
            Current = new InputSnapshot(new KeyboardState(modifiers), default, pointer),
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
