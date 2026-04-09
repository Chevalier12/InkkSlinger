using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class CalendarShadowPositionDiagnosticsTests
{
    [Fact]
    public void HoveredCalendarDayButton_ShadowRenderBoundsStayOnHoveredCell()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var (uiRoot, calendar) = CreateFixture();
            calendar.DisplayDate = new DateTime(2026, 3, 1);
            calendar.FirstDayOfWeek = DayOfWeek.Monday;
            RunLayout(uiRoot);

            var hoveredDate = new DateTime(2026, 3, 18);
            Assert.True(calendar.TryGetDayButtonIndexForDateForTesting(hoveredDate, out var hoveredIndex));

            var hoveredButton = Assert.IsType<CalendarDayButton>(calendar.DayButtonsForTesting[hoveredIndex]);
            var trailingButton = Assert.IsType<CalendarDayButton>(calendar.DayButtonsForTesting[^1]);

            EnsureTemplateRealized(hoveredButton);
            EnsureTemplateRealized(trailingButton);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(GetCenter(hoveredButton.LayoutSlot), pointerMoved: true));
            for (var i = 0; i < 12; i++)
            {
                RunLayout(uiRoot, 32 + (i * 16));
            }

            var hoveredBorder = Assert.IsType<Border>(Assert.Single(hoveredButton.GetVisualChildren()));
            var trailingBorder = Assert.IsType<Border>(Assert.Single(trailingButton.GetVisualChildren()));
            var hoveredShadow = Assert.IsType<DropShadowEffect>(hoveredBorder.Effect);
            var trailingShadow = Assert.IsType<DropShadowEffect>(trailingBorder.Effect);

            Assert.True(hoveredButton.IsMouseOver);
            Assert.False(trailingButton.IsMouseOver);
            Assert.True(
                hoveredBorder.TryGetRenderBoundsInRootSpace(out var hoveredBounds),
                $"Expected hovered border to report render bounds. hoveredButton={FormatRect(hoveredButton.LayoutSlot)}, hoveredBorder={FormatRect(hoveredBorder.LayoutSlot)}, hoveredShadowBlur={hoveredShadow.BlurRadius:0.###}, hoveredShadowOpacity={hoveredShadow.Opacity:0.###}");
            Assert.True(
                trailingBorder.TryGetRenderBoundsInRootSpace(out var trailingBounds),
                $"Expected trailing border to report render bounds. trailingButton={FormatRect(trailingButton.LayoutSlot)}, trailingBorder={FormatRect(trailingBorder.LayoutSlot)}, trailingShadowBlur={trailingShadow.BlurRadius:0.###}, trailingShadowOpacity={trailingShadow.Opacity:0.###}");
            Assert.True(hoveredShadow.BlurRadius > 0.001f || hoveredShadow.Opacity > 0.001f);
            Assert.True(trailingShadow.BlurRadius <= 0.001f && trailingShadow.Opacity <= 0.001f);

            var hoveredCenter = GetCenter(hoveredButton.LayoutSlot);
            var trailingCenter = GetCenter(trailingButton.LayoutSlot);
            var hoveredBoundsCenter = GetCenter(hoveredBounds);

            Assert.True(
                DistanceSquared(hoveredBoundsCenter, hoveredCenter) < DistanceSquared(hoveredBoundsCenter, trailingCenter),
                $"Expected hovered shadow bounds to stay closest to the hovered cell. hoveredButton={FormatRect(hoveredButton.LayoutSlot)}, hoveredBorder={FormatRect(hoveredBorder.LayoutSlot)}, hoveredBounds={FormatRect(hoveredBounds)}, trailingButton={FormatRect(trailingButton.LayoutSlot)}, trailingBorder={FormatRect(trailingBorder.LayoutSlot)}, trailingBounds={FormatRect(trailingBounds)}");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CalendarDayButtonTemplateShadowMutation_TracksExpandedDirtyRegion()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var (uiRoot, calendar) = CreateFixture();
            calendar.DisplayDate = new DateTime(2026, 3, 1);
            calendar.FirstDayOfWeek = DayOfWeek.Monday;
            RunLayout(uiRoot);
            uiRoot.RebuildRenderListForTests();
            calendar.ClearRenderInvalidationRecursive();
            uiRoot.CompleteDrawStateForTests();
            uiRoot.ResetDirtyStateForTests();

            var hoveredDate = new DateTime(2026, 3, 19);
            Assert.True(calendar.TryGetDayButtonIndexForDateForTesting(hoveredDate, out var hoveredIndex));
            var hoveredButton = Assert.IsType<CalendarDayButton>(calendar.DayButtonsForTesting[hoveredIndex]);
            EnsureTemplateRealized(hoveredButton);
            var hoveredBorder = Assert.IsType<Border>(Assert.Single(hoveredButton.GetVisualChildren()));
            var shadow = Assert.IsType<DropShadowEffect>(hoveredBorder.Effect);

            shadow.BlurRadius = 12f;
            shadow.Opacity = 0.5f;

            var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();
            Assert.NotEmpty(dirtyRegions);

            var slot = hoveredButton.LayoutSlot;
            Assert.Contains(
                dirtyRegions,
                region => region.X < slot.X ||
                          region.Y < slot.Y ||
                          region.X + region.Width > slot.X + slot.Width ||
                          region.Y + region.Height > slot.Y + slot.Height);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CalendarView_HoveringMarch19_OnlyThatDayHasAnimatedShadowState()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new CalendarView
            {
                Width = 600f,
                Height = 420f
            };

            var host = new Canvas
            {
                Width = 600f,
                Height = 420f
            };
            host.AddChild(view);

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot);

            var calendar = FindFirstVisualChild<Calendar>(view);
            Assert.NotNull(calendar);
            calendar!.DisplayDate = new DateTime(2026, 3, 1);
            RunLayout(uiRoot);

            var hoveredDate = new DateTime(2026, 3, 19);
            Assert.True(calendar.TryGetDayButtonIndexForDateForTesting(hoveredDate, out var hoveredIndex));
            var hoveredButton = Assert.IsType<CalendarDayButton>(calendar.DayButtonsForTesting[hoveredIndex]);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(GetCenter(hoveredButton.LayoutSlot), pointerMoved: true));
            for (var i = 0; i < 12; i++)
            {
                RunLayout(uiRoot, 32 + (i * 16));
            }

            var animatedIndices = new List<int>();
            for (var i = 0; i < calendar.DayButtonsForTesting.Count; i++)
            {
                var button = Assert.IsType<CalendarDayButton>(calendar.DayButtonsForTesting[i]);
                EnsureTemplateRealized(button);
                var border = Assert.IsType<Border>(Assert.Single(button.GetVisualChildren()));
                var shadow = Assert.IsType<DropShadowEffect>(border.Effect);
                if (shadow.BlurRadius > 0.001f || shadow.Opacity > 0.001f)
                {
                    animatedIndices.Add(i);
                }
            }

            Assert.Equal(new[] { hoveredIndex }, animatedIndices);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CalendarView_TemplateNameLookup_ForShadow_ResolvesToHoveredButtonInstance()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new CalendarView
            {
                Width = 600f,
                Height = 420f
            };

            var host = new Canvas
            {
                Width = 600f,
                Height = 420f
            };
            host.AddChild(view);

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot);

            var calendar = FindFirstVisualChild<Calendar>(view);
            Assert.NotNull(calendar);
            calendar!.DisplayDate = new DateTime(2026, 3, 1);
            RunLayout(uiRoot);

            Assert.True(calendar.TryGetDayButtonIndexForDateForTesting(new DateTime(2026, 3, 19), out var hoveredIndex));
            var hoveredButton = Assert.IsType<CalendarDayButton>(calendar.DayButtonsForTesting[hoveredIndex]);
            var trailingButton = Assert.IsType<CalendarDayButton>(calendar.DayButtonsForTesting[^1]);

            EnsureTemplateRealized(hoveredButton);
            EnsureTemplateRealized(trailingButton);

            var hoveredBorder = Assert.IsType<Border>(Assert.Single(hoveredButton.GetVisualChildren()));
            var trailingBorder = Assert.IsType<Border>(Assert.Single(trailingButton.GetVisualChildren()));

            var hoveredShadowFromLookup = hoveredButton.FindTemplateNamedObject("shadow");
            var trailingShadowFromLookup = trailingButton.FindTemplateNamedObject("shadow");
            var hoveredShadowFromScope = hoveredBorder.GetLocalNameScope()?.FindName("shadow");
            var trailingShadowFromScope = trailingBorder.GetLocalNameScope()?.FindName("shadow");

            Assert.Same(hoveredBorder.Effect, hoveredShadowFromScope);
            Assert.Same(trailingBorder.Effect, trailingShadowFromScope);
            Assert.Same(hoveredBorder.Effect, hoveredShadowFromLookup);
            Assert.Same(trailingBorder.Effect, trailingShadowFromLookup);
            Assert.NotSame(hoveredShadowFromLookup, trailingShadowFromLookup);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void PlainCalendar_TemplateNameLookup_ForShadow_ResolvesToButtonInstance()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var (uiRoot, calendar) = CreateFixture();
            calendar.DisplayDate = new DateTime(2026, 3, 1);
            calendar.FirstDayOfWeek = DayOfWeek.Monday;
            RunLayout(uiRoot);

            Assert.True(calendar.TryGetDayButtonIndexForDateForTesting(new DateTime(2026, 3, 19), out var hoveredIndex));
            var hoveredButton = Assert.IsType<CalendarDayButton>(calendar.DayButtonsForTesting[hoveredIndex]);
            EnsureTemplateRealized(hoveredButton);
            var hoveredBorder = Assert.IsType<Border>(Assert.Single(hoveredButton.GetVisualChildren()));

            Assert.Same(hoveredBorder.Effect, hoveredButton.FindTemplateNamedObject("shadow"));
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

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
    }

    private static float DistanceSquared(Vector2 left, Vector2 right)
    {
        return Vector2.DistanceSquared(left, right);
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"({rect.X:0.###},{rect.Y:0.###},{rect.Width:0.###},{rect.Height:0.###})";
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool pointerMoved)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void EnsureTemplateRealized(CalendarDayButton button)
    {
        if (button.GetVisualChildren().Any())
        {
            return;
        }

        Assert.True(button.ApplyTemplate());
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

    private static void RunLayout(UiRoot uiRoot)
    {
        RunLayout(uiRoot, 16);
    }

    private static void RunLayout(UiRoot uiRoot, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(16)),
            new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 600, 420));
    }

    private static void LoadRootAppResources()
    {
        TestApplicationResources.LoadDemoAppResources();
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

    private readonly record struct ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        IReadOnlyList<ResourceDictionary> MergedDictionaries);
}
