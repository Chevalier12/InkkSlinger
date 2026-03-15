using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogCalendarHotspotTests
{
    private readonly ITestOutputHelper _output;

    public ControlsCatalogCalendarHotspotTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ClickingCalendarPreview_ShouldAttributeCountsAndElapsedTimeByTypeSubtreeAndSubsystem()
    {
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

        var previewHost = Assert.IsType<ContentControl>(view.FindName("PreviewHost"));
        ClickCatalogButton(uiRoot, view, "Button");
        RunLayout(uiRoot, 1400, 900, 32);

        Button.ResetTimingForTests();
        Grid.ResetTimingForTests();
        UniformGrid.ResetTimingForTests();
        TextLayout.ResetMetricsForTests();
        UiTextRenderer.ResetTimingForTests();

        var beforeElementTimings = SnapshotElementTimings(host);
        ClickCatalogButton(uiRoot, view, "Calendar");
        RunLayout(uiRoot, 1400, 900, 48);

        var previewRoot = Assert.IsAssignableFrom<UIElement>(previewHost.Content);
        var previewRootFramework = Assert.IsAssignableFrom<FrameworkElement>(previewRoot);
        var calendar = FindFirstVisualChild<Calendar>(previewRoot);
        Assert.NotNull(calendar);

        var elementDeltas = CaptureElementTimingDeltas(previewRoot, beforeElementTimings);
        var totalDelta = elementDeltas
            .Select(static delta => delta.Timing)
            .Aggregate(LayoutTiming.Zero, Sum);
        var subtreeHotspots = BuildSubtreeHotspots(previewRootFramework, beforeElementTimings);
        var typeHotspots = elementDeltas
            .GroupBy(static delta => delta.Element.GetType().Name, StringComparer.Ordinal)
            .Select(static group => new TypeHotspot(
                group.Key,
                group.Count(),
                group.Sum(static delta => delta.Timing.MeasureWork),
                group.Sum(static delta => delta.Timing.ArrangeWork),
                group.Sum(static delta => delta.Timing.MeasureElapsedTicks),
                group.Sum(static delta => delta.Timing.MeasureExclusiveElapsedTicks),
                group.Sum(static delta => delta.Timing.ArrangeElapsedTicks)))
            .OrderByDescending(static hotspot => hotspot.MeasureExclusiveElapsedTicks)
            .ThenByDescending(static hotspot => hotspot.MeasureElapsedTicks)
            .ThenByDescending(static hotspot => hotspot.MeasureWork)
            .ThenBy(static hotspot => hotspot.TypeName, StringComparer.Ordinal)
            .ToArray();
        var individualHotspots = elementDeltas
            .OrderByDescending(static delta => delta.Timing.MeasureExclusiveElapsedTicks)
            .ThenByDescending(static delta => delta.Timing.MeasureElapsedTicks)
            .ThenByDescending(static delta => delta.Timing.MeasureWork)
            .ThenBy(static delta => delta.Path, StringComparer.Ordinal)
            .ToArray();

        var buttonTiming = Button.GetTimingSnapshotForTests();
        var gridTiming = Grid.GetTimingSnapshotForTests();
        var uniformGridTiming = UniformGrid.GetTimingSnapshotForTests();
        var textLayoutMetrics = TextLayout.GetMetricsSnapshot();
        var fontTiming = UiTextRenderer.GetTimingSnapshotForTests();
        var uniformGridFrameworkExclusiveTicks = typeHotspots
            .Where(static hotspot => hotspot.TypeName == nameof(UniformGrid))
            .Select(static hotspot => hotspot.MeasureExclusiveElapsedTicks)
            .DefaultIfEmpty(0L)
            .Single();
        var uniformGridFrameworkOverheadTicks = Math.Max(
            0L,
            uniformGridFrameworkExclusiveTicks - uniformGridTiming.MeasureOverrideElapsedTicks);

        _output.WriteLine(
            $"calendar root hotspot: measureWork={totalDelta.MeasureWork}, arrangeWork={totalDelta.ArrangeWork}, " +
            $"measureTicks={totalDelta.MeasureElapsedTicks}, measureExclusiveTicks={totalDelta.MeasureExclusiveElapsedTicks}, arrangeTicks={totalDelta.ArrangeElapsedTicks}");
        _output.WriteLine(
            $"subsystem timing: Button.Measure={buttonTiming.MeasureOverrideElapsedTicks}, Button.TextLayout={buttonTiming.ResolveTextLayoutElapsedTicks}, " +
            $"Grid.Measure={gridTiming.MeasureOverrideElapsedTicks}, UniformGrid.Measure={uniformGridTiming.MeasureOverrideElapsedTicks}, " +
            $"TextLayout.Layout={textLayoutMetrics.LayoutElapsedTicks}, TextLayout.Build={textLayoutMetrics.BuildElapsedTicks}, " +
            $"TextLayout.BuildCount={textLayoutMetrics.BuildCount}, TextLayout.CacheMisses={textLayoutMetrics.CacheMissCount}, " +
            $"Font.MeasureWidth={fontTiming.MeasureWidthElapsedTicks}, Font.MeasureWidthCalls={fontTiming.MeasureWidthCallCount}, " +
            $"Font.GetLineHeight={fontTiming.GetLineHeightElapsedTicks}, Font.GetLineHeightCalls={fontTiming.GetLineHeightCallCount}, " +
            $"Font.Draw={fontTiming.DrawStringElapsedTicks}, Font.DrawCalls={fontTiming.DrawStringCallCount}");
        _output.WriteLine(
            $"uniformGrid measure phases: cache={uniformGridTiming.MeasureChildrenCacheElapsedTicks}, dimensions={uniformGridTiming.MeasureDimensionResolutionElapsedTicks}, " +
            $"aggregateCheck={uniformGridTiming.MeasureAggregateCheckElapsedTicks}, childLoop={uniformGridTiming.MeasureChildLoopElapsedTicks}, " +
            $"childMeasureCalls={uniformGridTiming.MeasureChildMeasureElapsedTicks}, cacheRefreshes={uniformGridTiming.MeasureChildrenCacheRefreshCount}, " +
            $"aggregateHits={uniformGridTiming.MeasureAggregateReuseHitCount}, aggregateMisses={uniformGridTiming.MeasureAggregateReuseMissCount}, " +
            $"childReuses={uniformGridTiming.MeasureChildReuseCount}, childMeasures={uniformGridTiming.MeasureChildMeasureCount}, " +
            $"frameworkExclusiveOverhead={uniformGridFrameworkOverheadTicks}");

        foreach (var hotspot in typeHotspots.Take(12))
        {
            _output.WriteLine(
                $"type hotspot: {hotspot.TypeName}, elements={hotspot.ElementCount}, measureWork={hotspot.MeasureWork}, arrangeWork={hotspot.ArrangeWork}, " +
                $"measureTicks={hotspot.MeasureElapsedTicks}, measureExclusiveTicks={hotspot.MeasureExclusiveElapsedTicks}, arrangeTicks={hotspot.ArrangeElapsedTicks}");
        }

        foreach (var hotspot in subtreeHotspots.Take(12))
        {
            _output.WriteLine(
                $"subtree hotspot: {hotspot.Path}, frameworkElements={hotspot.FrameworkElementCount}, measureWork={hotspot.Timing.MeasureWork}, " +
                $"arrangeWork={hotspot.Timing.ArrangeWork}, measureTicks={hotspot.Timing.MeasureElapsedTicks}, measureExclusiveTicks={hotspot.Timing.MeasureExclusiveElapsedTicks}, arrangeTicks={hotspot.Timing.ArrangeElapsedTicks}");
        }

        foreach (var hotspot in individualHotspots.Take(12))
        {
            _output.WriteLine(
                $"element hotspot: {hotspot.Path}, measureWork={hotspot.Timing.MeasureWork}, arrangeWork={hotspot.Timing.ArrangeWork}, " +
                $"measureTicks={hotspot.Timing.MeasureElapsedTicks}, measureExclusiveTicks={hotspot.Timing.MeasureExclusiveElapsedTicks}, arrangeTicks={hotspot.Timing.ArrangeElapsedTicks}, desired={hotspot.Element.DesiredSize}");
        }

        Assert.NotEmpty(elementDeltas);
        Assert.True(totalDelta.MeasureWork > 0);
        Assert.True(totalDelta.MeasureElapsedTicks > 0);
        Assert.True(totalDelta.MeasureExclusiveElapsedTicks > 0);
        Assert.Contains(typeHotspots, static hotspot => hotspot.TypeName == nameof(Button) && hotspot.MeasureExclusiveElapsedTicks > 0);
        Assert.Contains(typeHotspots, static hotspot => hotspot.TypeName == nameof(UniformGrid) && hotspot.MeasureExclusiveElapsedTicks > 0);
        Assert.True(buttonTiming.MeasureOverrideElapsedTicks > 0);
        Assert.True(gridTiming.MeasureOverrideElapsedTicks > 0);
        Assert.True(uniformGridTiming.MeasureOverrideElapsedTicks > 0);
        Assert.True(uniformGridTiming.MeasureChildLoopElapsedTicks > 0 || uniformGridTiming.MeasureAggregateReuseHitCount > 0);
    }

    private static void ClickCatalogButton(UiRoot uiRoot, ControlsCatalogView view, string buttonText)
    {
        var button = FindCatalogButton(view, buttonText);
        Assert.NotNull(button);
        var center = GetCenter(button!.LayoutSlot);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(center, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(center, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(center, leftReleased: true));
    }

    private static Dictionary<FrameworkElement, LayoutTiming> SnapshotElementTimings(UIElement root)
    {
        return EnumerateVisualTree(root)
            .OfType<FrameworkElement>()
            .ToDictionary(
                static element => element,
                static element => new LayoutTiming(
                    element.MeasureWorkCount,
                    element.ArrangeWorkCount,
                    element.MeasureElapsedTicksForTests,
                    element.MeasureExclusiveElapsedTicksForTests,
                    element.ArrangeElapsedTicksForTests));
    }

    private static IReadOnlyList<ElementHotspot> CaptureElementTimingDeltas(
        UIElement root,
        IReadOnlyDictionary<FrameworkElement, LayoutTiming> beforeSnapshot)
    {
        return EnumerateVisualTree(root)
            .OfType<FrameworkElement>()
            .Select(element => new ElementHotspot(
                element,
                DescribeVisualPath(element, root),
                GetLayoutTimingDelta(element, beforeSnapshot)))
            .Where(static hotspot =>
                hotspot.Timing.MeasureWork > 0 ||
                hotspot.Timing.ArrangeWork > 0 ||
                hotspot.Timing.MeasureElapsedTicks > 0 ||
                hotspot.Timing.MeasureExclusiveElapsedTicks > 0 ||
                hotspot.Timing.ArrangeElapsedTicks > 0)
            .ToArray();
    }

    private static IReadOnlyList<SubtreeHotspot> BuildSubtreeHotspots(
        FrameworkElement root,
        IReadOnlyDictionary<FrameworkElement, LayoutTiming> beforeSnapshot)
    {
        var hotspots = new List<SubtreeHotspot>();
        BuildSubtreeHotspotsRecursive(root, beforeSnapshot, DescribeElement(root), hotspots);
        hotspots.Sort(static (left, right) =>
        {
            var measureTickComparison = right.Timing.MeasureExclusiveElapsedTicks.CompareTo(left.Timing.MeasureExclusiveElapsedTicks);
            if (measureTickComparison != 0)
            {
                return measureTickComparison;
            }

            var inclusiveMeasureTickComparison = right.Timing.MeasureElapsedTicks.CompareTo(left.Timing.MeasureElapsedTicks);
            if (inclusiveMeasureTickComparison != 0)
            {
                return inclusiveMeasureTickComparison;
            }

            var measureWorkComparison = right.Timing.MeasureWork.CompareTo(left.Timing.MeasureWork);
            if (measureWorkComparison != 0)
            {
                return measureWorkComparison;
            }

            return string.Compare(left.Path, right.Path, StringComparison.Ordinal);
        });

        return hotspots;
    }

    private static void BuildSubtreeHotspotsRecursive(
        FrameworkElement root,
        IReadOnlyDictionary<FrameworkElement, LayoutTiming> beforeSnapshot,
        string path,
        ICollection<SubtreeHotspot> hotspots)
    {
        var timing = GetLayoutTimingDelta(root, beforeSnapshot);
        var frameworkElementCount = 1;
        foreach (var child in root.GetVisualChildren())
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            var childPath = $"{path} > {DescribeElement(frameworkChild)}";
            BuildSubtreeHotspotsRecursive(frameworkChild, beforeSnapshot, childPath, hotspots);
            timing = Sum(timing, SumSubtreeTiming(frameworkChild, beforeSnapshot));
            frameworkElementCount += CountFrameworkElements(frameworkChild);
        }

        if (timing.MeasureWork > 0 ||
            timing.ArrangeWork > 0 ||
            timing.MeasureElapsedTicks > 0 ||
            timing.MeasureExclusiveElapsedTicks > 0 ||
            timing.ArrangeElapsedTicks > 0)
        {
            hotspots.Add(new SubtreeHotspot(path, timing, frameworkElementCount));
        }
    }

    private static LayoutTiming SumSubtreeTiming(
        FrameworkElement root,
        IReadOnlyDictionary<FrameworkElement, LayoutTiming> beforeSnapshot)
    {
        var total = GetLayoutTimingDelta(root, beforeSnapshot);
        foreach (var child in root.GetVisualChildren())
        {
            if (child is FrameworkElement frameworkChild)
            {
                total = Sum(total, SumSubtreeTiming(frameworkChild, beforeSnapshot));
            }
        }

        return total;
    }

    private static LayoutTiming GetLayoutTimingDelta(
        FrameworkElement element,
        IReadOnlyDictionary<FrameworkElement, LayoutTiming> beforeSnapshot)
    {
        beforeSnapshot.TryGetValue(element, out var before);
        return new LayoutTiming(
            element.MeasureWorkCount - before.MeasureWork,
            element.ArrangeWorkCount - before.ArrangeWork,
            element.MeasureElapsedTicksForTests - before.MeasureElapsedTicks,
            element.MeasureExclusiveElapsedTicksForTests - before.MeasureExclusiveElapsedTicks,
            element.ArrangeElapsedTicksForTests - before.ArrangeElapsedTicks);
    }

    private static LayoutTiming Sum(LayoutTiming left, LayoutTiming right)
    {
        return new LayoutTiming(
            left.MeasureWork + right.MeasureWork,
            left.ArrangeWork + right.ArrangeWork,
            left.MeasureElapsedTicks + right.MeasureElapsedTicks,
            left.MeasureExclusiveElapsedTicks + right.MeasureExclusiveElapsedTicks,
            left.ArrangeElapsedTicks + right.ArrangeElapsedTicks);
    }

    private static int CountFrameworkElements(UIElement root)
    {
        return EnumerateVisualTree(root).OfType<FrameworkElement>().Count();
    }

    private static string DescribeVisualPath(FrameworkElement element, UIElement root)
    {
        var segments = new Stack<string>();
        UIElement? current = element;
        while (current != null)
        {
            segments.Push(DescribeElement(current));
            if (ReferenceEquals(current, root))
            {
                break;
            }

            current = current.VisualParent;
        }

        return string.Join(" > ", segments);
    }

    private static string DescribeElement(UIElement element)
    {
        return element switch
        {
            Button button when !string.IsNullOrEmpty(button.GetContentText()) => $"{nameof(Button)}(\"{button.GetContentText()}\")",
            Label label when !string.IsNullOrEmpty(label.GetContentText()) => $"{nameof(Label)}(\"{label.GetContentText()}\")",
            UniformGrid uniformGrid => $"{nameof(UniformGrid)}(Rows={uniformGrid.Rows}, Columns={uniformGrid.Columns})",
            Grid grid => $"{nameof(Grid)}(Rows={grid.RowDefinitions.Count}, Columns={grid.ColumnDefinitions.Count})",
            _ => element.GetType().Name
        };
    }

    private static Button? FindCatalogButton(UIElement root, string text)
    {
        if (root is Button button && string.Equals(button.GetContentText(), text, StringComparison.Ordinal))
        {
            return button;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindCatalogButton(child, text);
            if (found != null)
            {
                return found;
            }
        }

        return null;
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

    private static IEnumerable<UIElement> EnumerateVisualTree(UIElement root)
    {
        yield return root;
        foreach (var child in root.GetVisualChildren())
        {
            foreach (var nested in EnumerateVisualTree(child))
            {
                yield return nested;
            }
        }
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
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
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
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

    private readonly record struct LayoutTiming(
        long MeasureWork,
        long ArrangeWork,
        long MeasureElapsedTicks,
        long MeasureExclusiveElapsedTicks,
        long ArrangeElapsedTicks)
    {
        public static LayoutTiming Zero => new(0, 0, 0, 0, 0);
    }

    private readonly record struct ElementHotspot(
        FrameworkElement Element,
        string Path,
        LayoutTiming Timing);

    private readonly record struct TypeHotspot(
        string TypeName,
        int ElementCount,
        long MeasureWork,
        long ArrangeWork,
        long MeasureElapsedTicks,
        long MeasureExclusiveElapsedTicks,
        long ArrangeElapsedTicks);

    private readonly record struct SubtreeHotspot(
        string Path,
        LayoutTiming Timing,
        int FrameworkElementCount);
}
