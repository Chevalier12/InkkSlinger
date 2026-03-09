using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogCalendarPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public ControlsCatalogCalendarPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ClickingCalendarPreview_ShouldExposeMeasuredWarmSwitchCost()
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
        Assert.NotNull(previewHost.Content);

        ClickCatalogButton(uiRoot, view, "Button");
        var buttonMetrics = CaptureFrameMetrics(uiRoot, host, 1400, 900, 32);
        Assert.IsType<ButtonView>(previewHost.Content);

        TextLayout.ResetMetricsForTests();
        var beforeCalendarRootMetrics = uiRoot.GetMetricsSnapshot();
        var beforeCalendarTreeMetrics = uiRoot.GetVisualTreeMetricsSnapshot();
        var beforeCalendarInvalidations = SnapshotInvalidations(host);
        var calendarClickMetrics = ClickCatalogButton(uiRoot, view, "Calendar");
        var calendarMetrics = CaptureFrameMetrics(uiRoot, host, 1400, 900, 48);
        var afterCalendarRootMetrics = uiRoot.GetMetricsSnapshot();
        var afterCalendarTreeMetrics = uiRoot.GetVisualTreeMetricsSnapshot();
        var afterCalendarInvalidations = SnapshotInvalidations(host);
        var calendarTextLayoutMetrics = uiRoot.GetTextLayoutMetricsSnapshot();

        ClickCatalogButton(uiRoot, view, "Button");
        var betweenMetrics = CaptureFrameMetrics(uiRoot, host, 1400, 900, 64);
        TextLayout.ResetMetricsForTests();
        var beforeWarmCalendarRootMetrics = uiRoot.GetMetricsSnapshot();
        var beforeWarmCalendarTreeMetrics = uiRoot.GetVisualTreeMetricsSnapshot();
        var warmCalendarClickMetrics = ClickCatalogButton(uiRoot, view, "Calendar");
        var warmCalendarMetrics = CaptureFrameMetrics(uiRoot, host, 1400, 900, 80);
        var afterWarmCalendarRootMetrics = uiRoot.GetMetricsSnapshot();
        var afterWarmCalendarTreeMetrics = uiRoot.GetVisualTreeMetricsSnapshot();
        var warmCalendarTextLayoutMetrics = uiRoot.GetTextLayoutMetricsSnapshot();

        var calendar = FindFirstVisualChild<Calendar>(view);
        Assert.NotNull(calendar);
        Assert.IsType<CalendarView>(previewHost.Content);
        Assert.Equal(42, calendar!.DayButtonsForTesting.Count);

        _output.WriteLine($"calendar click: {calendarClickMetrics}");
        _output.WriteLine($"button frame: {buttonMetrics}");
        _output.WriteLine($"calendar frame: {calendarMetrics}");
        _output.WriteLine($"calendar root metrics delta: {DescribeRootMetricsDelta(beforeCalendarRootMetrics, afterCalendarRootMetrics)}");
        _output.WriteLine($"calendar tree metrics delta: {DescribeVisualTreeMetricsDelta(beforeCalendarTreeMetrics, afterCalendarTreeMetrics)}");
        _output.WriteLine($"calendar text layout: {calendarTextLayoutMetrics}");
        foreach (var line in DescribeTopLayoutCallCounts(previewHost.Content as UIElement, count: 16))
        {
            _output.WriteLine(line);
        }
        _output.WriteLine($"between frame: {betweenMetrics}");
        _output.WriteLine($"warm calendar click: {warmCalendarClickMetrics}");
        _output.WriteLine($"warm calendar frame: {warmCalendarMetrics}");
        _output.WriteLine($"warm calendar root metrics delta: {DescribeRootMetricsDelta(beforeWarmCalendarRootMetrics, afterWarmCalendarRootMetrics)}");
        _output.WriteLine($"warm calendar tree metrics delta: {DescribeVisualTreeMetricsDelta(beforeWarmCalendarTreeMetrics, afterWarmCalendarTreeMetrics)}");
        _output.WriteLine($"warm calendar text layout: {warmCalendarTextLayoutMetrics}");
        foreach (var line in DescribeTopMeasureInvalidations(host, count: 16))
        {
            _output.WriteLine(line);
        }

        foreach (var line in DescribeMeasureInvalidationDeltaByType(beforeCalendarInvalidations, afterCalendarInvalidations, count: 16))
        {
            _output.WriteLine(line);
        }

        Assert.True(buttonMetrics.LastLayoutPhaseMs < 80d);
        Assert.True(calendarMetrics.LastLayoutPhaseMs < 160d);
        Assert.True(calendarMetrics.LastUpdateMs < 180d);
        Assert.True(warmCalendarClickMetrics.ReleaseDispatchMs < 30d);
        Assert.True(warmCalendarMetrics.LastLayoutPhaseMs < 60d);
        Assert.True(warmCalendarMetrics.LastUpdateMs < 70d);
        Assert.True(warmCalendarTextLayoutMetrics.LayoutRequestCount < 80);
    }

    private static InputInteractionMetrics ClickCatalogButton(UiRoot uiRoot, ControlsCatalogView view, string buttonText)
    {
        var button = FindCatalogButton(view, buttonText);
        Assert.NotNull(button);
        var center = GetCenter(button!.LayoutSlot);

        var stopwatch = Stopwatch.StartNew();
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(center, pointerMoved: true));
        var moveDispatchMs = uiRoot.GetInputMetricsSnapshot().LastInputDispatchMilliseconds;
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(center, leftPressed: true));
        var pressDispatchMs = uiRoot.GetInputMetricsSnapshot().LastInputDispatchMilliseconds;
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(center, leftReleased: true));
        stopwatch.Stop();
        var releaseInputMetrics = uiRoot.GetInputMetricsSnapshot();

        return new InputInteractionMetrics(
            stopwatch.Elapsed.TotalMilliseconds,
            moveDispatchMs,
            pressDispatchMs,
            releaseInputMetrics.LastInputDispatchMilliseconds,
            releaseInputMetrics.LastInputPointerTargetResolveMilliseconds,
            releaseInputMetrics.LastInputPointerDispatchMilliseconds,
            releaseInputMetrics.HitTestCount,
            releaseInputMetrics.RoutedEventCount,
            releaseInputMetrics.PointerEventCount);
    }

    private static Button? FindCatalogButton(UIElement root, string text)
    {
        if (root is Button button && string.Equals(button.Text, text, StringComparison.Ordinal))
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

    private static PerformanceFrameMetrics CaptureFrameMetrics(UiRoot uiRoot, UIElement host, int width, int height, int elapsedMs)
    {
        RunLayout(uiRoot, width, height, elapsedMs);
        return new PerformanceFrameMetrics(
            uiRoot.LastUpdateMs,
            uiRoot.LastBindingPhaseMs,
            uiRoot.LastLayoutPhaseMs,
            uiRoot.LastInputPhaseMs,
            uiRoot.LastDeferredOperationCount,
            uiRoot.LayoutPasses,
            uiRoot.MeasureInvalidationCount,
            uiRoot.ArrangeInvalidationCount,
            uiRoot.RenderInvalidationCount,
            EnumerateVisualTree(host).Count(),
            EnumerateVisualTree(host).OfType<Button>().Count());
    }

    private static IReadOnlyList<string> DescribeTopMeasureInvalidations(UIElement root, int count)
    {
        return EnumerateVisualTree(root)
            .Where(static element => element.MeasureInvalidationCount > 0)
            .OrderByDescending(static element => element.MeasureInvalidationCount)
            .ThenBy(static element => element.GetType().Name, StringComparer.Ordinal)
            .Take(count)
            .Select(static element =>
                $"{element.GetType().Name}: measure={element.MeasureInvalidationCount}, arrange={element.ArrangeInvalidationCount}, render={element.RenderInvalidationCount}")
            .ToArray();
    }

    private static IReadOnlyList<string> DescribeTopLayoutCallCounts(UIElement? root, int count)
    {
        if (root == null)
        {
            return Array.Empty<string>();
        }

        return EnumerateVisualTree(root)
            .OfType<FrameworkElement>()
            .OrderByDescending(static element => element.ArrangeCallCount)
            .ThenByDescending(static element => element.MeasureCallCount)
            .ThenBy(static element => element.GetType().Name, StringComparer.Ordinal)
            .Take(count)
            .Select(static element =>
                $"{element.GetType().Name}: measureCalls={element.MeasureCallCount}, arrangeCalls={element.ArrangeCallCount}, desired={element.DesiredSize}, slot={element.LayoutSlot}")
            .ToArray();
    }

    private static Dictionary<UIElement, (int Measure, int Arrange, int Render)> SnapshotInvalidations(UIElement root)
    {
        return EnumerateVisualTree(root).ToDictionary(
            static element => element,
            static element => (element.MeasureInvalidationCount, element.ArrangeInvalidationCount, element.RenderInvalidationCount));
    }

    private static IReadOnlyList<string> DescribeMeasureInvalidationDeltaByType(
        IReadOnlyDictionary<UIElement, (int Measure, int Arrange, int Render)> before,
        IReadOnlyDictionary<UIElement, (int Measure, int Arrange, int Render)> after,
        int count)
    {
        return after
            .Select(entry =>
            {
                before.TryGetValue(entry.Key, out var previous);
                return new
                {
                    TypeName = entry.Key.GetType().Name,
                    Measure = entry.Value.Measure - previous.Measure,
                    Arrange = entry.Value.Arrange - previous.Arrange,
                    Render = entry.Value.Render - previous.Render
                };
            })
            .Where(static entry => entry.Measure > 0 || entry.Arrange > 0 || entry.Render > 0)
            .GroupBy(static entry => entry.TypeName, StringComparer.Ordinal)
            .Select(static group => new
            {
                TypeName = group.Key,
                ElementCount = group.Count(),
                Measure = group.Sum(static entry => entry.Measure),
                Arrange = group.Sum(static entry => entry.Arrange),
                Render = group.Sum(static entry => entry.Render)
            })
            .OrderByDescending(static group => group.Measure)
            .ThenByDescending(static group => group.Arrange)
            .ThenBy(static group => group.TypeName, StringComparer.Ordinal)
            .Take(count)
            .Select(static group =>
                $"{group.TypeName}: elements={group.ElementCount}, measure={group.Measure}, arrange={group.Arrange}, render={group.Render}")
            .ToArray();
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

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private readonly record struct PerformanceFrameMetrics(
        double LastUpdateMs,
        double LastBindingPhaseMs,
        double LastLayoutPhaseMs,
        double LastInputPhaseMs,
        int LastDeferredOperationCount,
        int LayoutPasses,
        int MeasureInvalidationCount,
        int ArrangeInvalidationCount,
        int RenderInvalidationCount,
        int VisualCount,
        int ButtonCount)
    {
        public override string ToString()
        {
            return string.Join(
                ", ",
                new[]
                {
                    $"update={LastUpdateMs:0.000}ms",
                    $"binding={LastBindingPhaseMs:0.000}ms",
                    $"layout={LastLayoutPhaseMs:0.000}ms",
                    $"input={LastInputPhaseMs:0.000}ms",
                    $"deferred={LastDeferredOperationCount}",
                    $"layoutPasses={LayoutPasses}",
                    $"measureInvalidations={MeasureInvalidationCount}",
                    $"arrangeInvalidations={ArrangeInvalidationCount}",
                    $"renderInvalidations={RenderInvalidationCount}",
                    $"visuals={VisualCount}",
                    $"buttons={ButtonCount}"
                });
        }
    }

    private readonly record struct InputInteractionMetrics(
        double TotalElapsedMs,
        double MoveDispatchMs,
        double PressDispatchMs,
        double ReleaseDispatchMs,
        double ReleaseTargetResolveMs,
        double ReleasePointerDispatchMs,
        int ReleaseHitTestCount,
        int ReleaseRoutedEventCount,
        int ReleasePointerEventCount)
    {
        public override string ToString()
        {
            return string.Join(
                ", ",
                new[]
                {
                    $"total={TotalElapsedMs:0.000}ms",
                    $"moveDispatch={MoveDispatchMs:0.000}ms",
                    $"pressDispatch={PressDispatchMs:0.000}ms",
                    $"releaseDispatch={ReleaseDispatchMs:0.000}ms",
                    $"releaseResolve={ReleaseTargetResolveMs:0.000}ms",
                    $"releasePointer={ReleasePointerDispatchMs:0.000}ms",
                    $"releaseHitTests={ReleaseHitTestCount}",
                    $"releaseRoutedEvents={ReleaseRoutedEventCount}",
                    $"releasePointerEvents={ReleasePointerEventCount}"
                });
        }
    }

    private static string DescribeRootMetricsDelta(UiRootMetricsSnapshot before, UiRootMetricsSnapshot after)
    {
        return string.Join(
            ", ",
            new[]
            {
                $"retainedNodes={after.RetainedRenderNodeCount - before.RetainedRenderNodeCount}",
                $"visualStructureChanges={after.VisualStructureChangeCount - before.VisualStructureChangeCount}",
                $"retainedFullRebuilds={after.RetainedFullRebuildCount - before.RetainedFullRebuildCount}",
                $"retainedSubtreeSyncs={after.RetainedSubtreeSyncCount - before.RetainedSubtreeSyncCount}",
                $"lastDirtyVisuals={after.LastRetainedDirtyVisualCount - before.LastRetainedDirtyVisualCount}"
            });
    }

    private static string DescribeVisualTreeMetricsDelta(UiVisualTreeMetricsSnapshot before, UiVisualTreeMetricsSnapshot after)
    {
        return string.Join(
            ", ",
            new[]
            {
                $"visuals={after.VisualCount - before.VisualCount}",
                $"frameworkElements={after.FrameworkElementCount - before.FrameworkElementCount}",
                $"measureCalls={after.MeasureCallCount - before.MeasureCallCount}",
                $"arrangeCalls={after.ArrangeCallCount - before.ArrangeCallCount}",
                $"updateCalls={after.UpdateCallCount - before.UpdateCallCount}",
                $"measureInvalidations={after.MeasureInvalidationCount - before.MeasureInvalidationCount}",
                $"arrangeInvalidations={after.ArrangeInvalidationCount - before.ArrangeInvalidationCount}",
                $"renderInvalidations={after.RenderInvalidationCount - before.RenderInvalidationCount}"
            });
    }

}
