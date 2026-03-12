using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogDataGridPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public ControlsCatalogDataGridPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Manual performance investigation only. Excluded from the default test suite.")]
    public void ClickingDataGridPreview_ShouldExposeMeasuredWarmSwitchCost()
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

        var buttonPreviewHost = Assert.IsType<ContentControl>(view.FindName("PreviewHost"));
        Assert.NotNull(buttonPreviewHost.Content);

        ClickCatalogButton(uiRoot, view, "Button");
        var buttonMetrics = CaptureFrameMetrics(uiRoot, 1400, 900, 32);
        Assert.IsType<ButtonView>(buttonPreviewHost.Content);

        var beforeDataGridInvalidations = SnapshotInvalidations(host);
        ClickCatalogButton(uiRoot, view, "DataGrid");
        var dataGridMetrics = CaptureFrameMetrics(uiRoot, 1400, 900, 48);
        var afterDataGridInvalidations = SnapshotInvalidations(host);

        var dataGrid = FindFirstVisualChild<DataGrid>(view);
        Assert.NotNull(dataGrid);
        Assert.IsType<DataGridView>(buttonPreviewHost.Content);
        Assert.NotEmpty(dataGrid!.RowsForTesting);
        Assert.True(dataGrid.ScrollViewerForTesting.ViewportHeight > 0f);

        _output.WriteLine($"button frame: {buttonMetrics}");
        _output.WriteLine($"datagrid frame: {dataGridMetrics}");
        foreach (var line in DescribeTopMeasureInvalidations(host, count: 12))
        {
            _output.WriteLine(line);
        }
        foreach (var line in DescribeMeasureInvalidationDeltaByType(beforeDataGridInvalidations, afterDataGridInvalidations, count: 12))
        {
            _output.WriteLine(line);
        }

        Assert.True(dataGridMetrics.LastDeferredOperationCount <= buttonMetrics.LastDeferredOperationCount + 2);
        Assert.True(dataGridMetrics.LayoutPasses <= buttonMetrics.LayoutPasses + 1);
        Assert.True(buttonMetrics.LastLayoutPhaseMs < 60d);
        Assert.True(dataGridMetrics.LastLayoutPhaseMs < 100d);
        Assert.True(dataGridMetrics.LastUpdateMs < 120d);
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

    private static PerformanceFrameMetrics CaptureFrameMetrics(UiRoot uiRoot, int width, int height, int elapsedMs)
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
            uiRoot.RenderInvalidationCount);
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
        int RenderInvalidationCount)
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
                    $"renderInvalidations={RenderInvalidationCount}"
                });
        }
    }
}
