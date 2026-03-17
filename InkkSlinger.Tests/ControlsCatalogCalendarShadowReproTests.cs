using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogCalendarShadowReproTests
{
    private readonly ITestOutputHelper _output;

    public ControlsCatalogCalendarShadowReproTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void HoveringSidebarButton_WithCalendarPreview_DoesNotMutateCalendarShadowState()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new ControlsCatalogView
            {
                Width = 1400f,
                Height = 900f
            };

            var host = new Canvas
            {
                Width = 1400f,
                Height = 900f
            };
            host.AddChild(view);

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot, 1400, 900, 16);

            view.ShowControl("Calendar");
            RunLayout(uiRoot, 1400, 900, 32);

            var sidebarButton = FindCatalogButton(view, "Canvas");
            var previewHost = Assert.IsType<ContentControl>(view.FindName("PreviewHost"));
            var previewRoot = Assert.IsAssignableFrom<UIElement>(previewHost.Content);
            var calendar = FindFirstVisualChild<Calendar>(previewRoot);
            Assert.NotNull(calendar);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(GetCenter(sidebarButton.LayoutSlot), pointerMoved: true));
            for (var i = 0; i < 12; i++)
            {
                RunLayout(uiRoot, 1400, 900, 64 + (i * 16));
            }

            var sidebarShadow = GetButtonShadow(sidebarButton);
            var shadowedDays = calendar!.DayButtonsForTesting
                .Select((button, index) => new
                {
                    Index = index,
                    Button = Assert.IsType<CalendarDayButton>(button),
                    Shadow = GetButtonShadow(button),
                    Date = TryResolveDate(calendar, index)
                })
                .Where(static item => item.Shadow.BlurRadius > 0.001f || item.Shadow.Opacity > 0.001f)
                .ToArray();

            _output.WriteLine(
                $"sidebar hover button='{sidebarButton.GetContentText()}', blur={sidebarShadow.BlurRadius:0.###}, opacity={sidebarShadow.Opacity:0.###}, isMouseOver={sidebarButton.IsMouseOver}");
            foreach (var item in shadowedDays)
            {
                _output.WriteLine(
                    $"calendar shadow index={item.Index}, date={item.Date}, text='{item.Button.DayText}', blur={item.Shadow.BlurRadius:0.###}, opacity={item.Shadow.Opacity:0.###}, slot={FormatRect(item.Button.LayoutSlot)}");
            }

            Assert.True(sidebarButton.IsMouseOver, "Expected the hovered sidebar button to own hover state.");
            Assert.True(
                sidebarShadow.BlurRadius > 0.001f || sidebarShadow.Opacity > 0.001f,
                $"Expected sidebar button shadow to animate. blur={sidebarShadow.BlurRadius}, opacity={sidebarShadow.Opacity}.");
            Assert.Empty(shadowedDays);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CalendarPreview_Repro_ManualContentRenderingStillTracksContentInRetainedRenderList()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new ControlsCatalogView
            {
                Width = 1400f,
                Height = 900f
            };

            var host = new Canvas
            {
                Width = 1400f,
                Height = 900f
            };
            host.AddChild(view);

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot, 1400, 900, 16);

            view.ShowControl("Calendar");
            RunLayout(uiRoot, 1400, 900, 32);
            uiRoot.RebuildRenderListForTests();

            var previewHost = Assert.IsType<ContentControl>(view.FindName("PreviewHost"));
            var previewRoot = Assert.IsAssignableFrom<UIElement>(previewHost.Content);
            var calendar = Assert.IsType<Calendar>(FindFirstVisualChild<Calendar>(previewRoot));
            var retainedOrder = uiRoot.GetRetainedVisualOrderForTests();

            _output.WriteLine($"retained node count={retainedOrder.Count}");
            _output.WriteLine($"calendar retained children={string.Join(", ", calendar.GetRetainedRenderChildren().Select(static child => child.GetType().Name))}");

            Assert.Contains(calendar, retainedOrder);
            Assert.Contains(calendar.DayButtonsForTesting[0], retainedOrder);
            Assert.Contains(calendar.DayButtonsForTesting[^1], retainedOrder);

            var dayButtonsHost = calendar.DayButtonsForTesting[0].VisualParent;
            Assert.NotNull(dayButtonsHost);
            Assert.Contains(dayButtonsHost!, retainedOrder);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static string TryResolveDate(Calendar calendar, int dayButtonIndex)
    {
        var displayDate = calendar.DisplayDate;
        var startOfMonth = new DateTime(displayDate.Year, displayDate.Month, 1);
        var offset = ((7 + (startOfMonth.DayOfWeek - calendar.FirstDayOfWeek)) % 7);
        var date = startOfMonth.AddDays(dayButtonIndex - offset);
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static Button FindCatalogButton(ControlsCatalogView view, string buttonText)
    {
        var host = Assert.IsType<StackPanel>(view.FindName("ControlButtonsHost"));
        return Assert.IsType<Button>(
            host.Children
                .OfType<Button>()
                .First(button => string.Equals(button.GetContentText(), buttonText, StringComparison.Ordinal)));
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

    private static DropShadowEffect GetButtonShadow(Button button)
    {
        var chrome = Assert.IsType<Border>(Assert.Single(button.GetVisualChildren()));
        return Assert.IsType<DropShadowEffect>(chrome.Effect);
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
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

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private static void LoadRootAppResources()
    {
        var appPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "App.xml"));
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

    private readonly record struct ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        IReadOnlyList<ResourceDictionary> MergedDictionaries);
}
