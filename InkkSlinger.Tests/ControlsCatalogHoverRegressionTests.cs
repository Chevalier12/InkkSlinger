using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using InkkSlinger.Tests.TestDoubles;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogHoverRegressionTests
{
    [Fact]
    public void HoveringFromViewerGutterIntoButton_ShouldActivateButtonHover()
    {
        var view = new ControlsCatalogView();
        var uiRoot = new UiRoot(view);
        RunLayout(uiRoot, 1280, 820, 16);

        var viewer = FindFirstVisualChild<ScrollViewer>(view);
        Assert.NotNull(viewer);

        var host = Assert.IsType<StackPanel>(view.FindName("ControlButtonsHost"));
        var button = host.Children.OfType<Button>().First();

        var verticalBar = viewer!.GetVisualChildren()
            .OfType<ScrollBar>()
            .FirstOrDefault(static bar => bar.Orientation == Orientation.Vertical && bar.IsVisible);
        Assert.NotNull(verticalBar);

        var gutterPoint = new Vector2(
            verticalBar!.LayoutSlot.X - 0.25f,
            button.LayoutSlot.Y + (button.LayoutSlot.Height * 0.5f));

        // Capture instrumentation during the hover sequence
        using var capture = new InstrumentationCapture();
        MovePointer(uiRoot, gutterPoint);
        RunLayout(uiRoot, 1280, 820, 16);

        var buttonPoint = new Vector2(
            button.LayoutSlot.X + (button.LayoutSlot.Width * 0.5f),
            button.LayoutSlot.Y + (button.LayoutSlot.Height * 0.5f));
        MovePointer(uiRoot, buttonPoint);
        RunLayout(uiRoot, 1280, 820, 32);

        var lines = capture.GetInstrumentLines();

        // Parse instrumentation
        var timings = lines.Select(l => InstrumentationCapture.TryParseTiming(l)).Where(t => t.HasValue).Select(t => t!.Value).ToList();
        var counters = lines.Select(l => InstrumentationCapture.TryParseCounter(l)).Where(c => c.HasValue).Select(c => c!.Value).ToList();

        Console.WriteLine($"[METRICS] Raw instrument lines captured: {lines.Count}");

        // Aggregate by method type and show top slow and hot
        foreach (var timing in timings.OrderByDescending(t => t.microseconds).Take(10))
        {
            Console.WriteLine($"[METRICS] Slow: {timing.method} = {timing.microseconds}us");
        }

        var groupedCounters = counters.GroupBy(c => c.method).Select(g => (method: g.Key, totalCalls: g.Sum(x => x.count))).OrderByDescending(x => x.totalCalls).ToList();
        foreach (var counter in groupedCounters.Take(10))
        {
            Console.WriteLine($"[METRICS] Hot: {counter.method} = {counter.totalCalls} calls");
        }

        // Check for MarkFullFrameDirty calls
        var fullFrameDirtyLines = lines.Where(l => l.Contains("MarkFullFrameDirty")).ToList();
        if (fullFrameDirtyLines.Count > 0)
        {
            Console.WriteLine($"[METRICS] MarkFullFrameDirty calls: {fullFrameDirtyLines.Count}");
            foreach (var line in fullFrameDirtyLines.Take(5))
            {
                Console.WriteLine($"  {line}");
            }
        }

        Assert.True(
            button.IsMouseOver,
            $"Expected button hover to recover after moving from viewer gutter. button={button.GetContentText()}, gutter=({gutterPoint.X:0.###},{gutterPoint.Y:0.###}), buttonPoint=({buttonPoint.X:0.###},{buttonPoint.Y:0.###})");
    }

    [Fact]
    public void HoveringFromListBoxIntoSidebarButton_ShouldActivateSidebarHover()
    {
        var root = new Canvas
        {
            Width = 1000f,
            Height = 700f
        };

        var sidebarHost = new StackPanel();
        for (var i = 0; i < 18; i++)
        {
            sidebarHost.AddChild(new Button
            {
                Content = $"Control {i}",
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        var sidebarViewer = new ScrollViewer
        {
            Width = 260f,
            Height = 620f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = sidebarHost
        };
        root.AddChild(sidebarViewer);
        Canvas.SetLeft(sidebarViewer, 12f);
        Canvas.SetTop(sidebarViewer, 12f);

        var listBox = new ListBox
        {
            Width = 340f,
            Height = 260f
        };
        listBox.Items.Add("Alpha");
        listBox.Items.Add("Beta");
        listBox.Items.Add("Gamma");
        listBox.Items.Add("Delta");
        root.AddChild(listBox);
        Canvas.SetLeft(listBox, 340f);
        Canvas.SetTop(listBox, 56f);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 1000, 700, 16);

        var previewItemPoint = new Vector2(
            listBox.LayoutSlot.X + MathF.Max(12f, listBox.LayoutSlot.Width * 0.35f),
            listBox.LayoutSlot.Y + MathF.Max(12f, listBox.LayoutSlot.Height * 0.25f));
        var previewHit = VisualTreeHelper.HitTest(root, previewItemPoint);
        Assert.NotNull(FindAncestor<ListBoxItem>(previewHit));
        MovePointer(uiRoot, previewItemPoint);

        var firstSidebarButton = sidebarHost.Children.OfType<Button>().First();
        var sidebarButtonPoint = new Vector2(
            firstSidebarButton.LayoutSlot.X + (firstSidebarButton.LayoutSlot.Width * 0.5f),
            firstSidebarButton.LayoutSlot.Y + (firstSidebarButton.LayoutSlot.Height * 0.5f));
        var preMoveHit = VisualTreeHelper.HitTest(root, sidebarButtonPoint);
        var preMoveButton = FindAncestor<Button>(preMoveHit);
        MovePointer(uiRoot, sidebarButtonPoint);

        Assert.True(
            firstSidebarButton.IsMouseOver,
            $"Expected sidebar hover to activate after leaving ListBox. sidebar={firstSidebarButton.GetContentText()}, listBoxPoint=({previewItemPoint.X:0.###},{previewItemPoint.Y:0.###}), sidebarPoint=({sidebarButtonPoint.X:0.###},{sidebarButtonPoint.Y:0.###}), preMoveHit={preMoveHit?.GetType().Name ?? "null"}, preMoveButton={preMoveButton?.GetContentText() ?? "null"}");
    }

    private static void MovePointer(UiRoot uiRoot, Vector2 point)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, pointerMoved: true));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
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
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root, Func<TElement, bool>? predicate = null)
        where TElement : UIElement
    {
        if (root is TElement match && (predicate == null || predicate(match)))
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild(child, predicate);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TElement? FindAncestor<TElement>(UIElement? start)
        where TElement : UIElement
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement match)
            {
                return match;
            }
        }

        return null;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}
