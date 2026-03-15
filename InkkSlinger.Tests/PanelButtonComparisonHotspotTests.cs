using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.Tests;

public sealed class PanelButtonComparisonHotspotTests
{
    private readonly ITestOutputHelper _output;

    public PanelButtonComparisonHotspotTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Compare_UniformGridAndGrid_WithFortyButtons_ShouldReportLayoutMetrics()
    {
        var uniformGridFirstMetrics = MeasurePanelScenario(CreateUniformGridWithButtons());
        var gridSecondMetrics = MeasurePanelScenario(CreateGridWithButtons());
        var gridFirstMetrics = MeasurePanelScenario(CreateGridWithButtons());
        var uniformGridSecondMetrics = MeasurePanelScenario(CreateUniformGridWithButtons());

        _output.WriteLine(
            $"uniform grid first: measureWork={uniformGridFirstMetrics.TotalMeasureWork}, arrangeWork={uniformGridFirstMetrics.TotalArrangeWork}, " +
            $"measureTicks={uniformGridFirstMetrics.TotalMeasureTicks}, measureExclusiveTicks={uniformGridFirstMetrics.TotalMeasureExclusiveTicks}, " +
            $"buttonMeasureTicks={uniformGridFirstMetrics.ButtonMeasureTicks}, gridMeasureTicks={uniformGridFirstMetrics.GridMeasureTicks}, uniformGridMeasureTicks={uniformGridFirstMetrics.UniformGridMeasureTicks}");
        _output.WriteLine(
            $"grid second: measureWork={gridSecondMetrics.TotalMeasureWork}, arrangeWork={gridSecondMetrics.TotalArrangeWork}, " +
            $"measureTicks={gridSecondMetrics.TotalMeasureTicks}, measureExclusiveTicks={gridSecondMetrics.TotalMeasureExclusiveTicks}, " +
            $"buttonMeasureTicks={gridSecondMetrics.ButtonMeasureTicks}, gridMeasureTicks={gridSecondMetrics.GridMeasureTicks}, uniformGridMeasureTicks={gridSecondMetrics.UniformGridMeasureTicks}");
        _output.WriteLine(
            $"grid first: measureWork={gridFirstMetrics.TotalMeasureWork}, arrangeWork={gridFirstMetrics.TotalArrangeWork}, " +
            $"measureTicks={gridFirstMetrics.TotalMeasureTicks}, measureExclusiveTicks={gridFirstMetrics.TotalMeasureExclusiveTicks}, " +
            $"buttonMeasureTicks={gridFirstMetrics.ButtonMeasureTicks}, gridMeasureTicks={gridFirstMetrics.GridMeasureTicks}, uniformGridMeasureTicks={gridFirstMetrics.UniformGridMeasureTicks}");
        _output.WriteLine(
            $"uniform grid second: measureWork={uniformGridSecondMetrics.TotalMeasureWork}, arrangeWork={uniformGridSecondMetrics.TotalArrangeWork}, " +
            $"measureTicks={uniformGridSecondMetrics.TotalMeasureTicks}, measureExclusiveTicks={uniformGridSecondMetrics.TotalMeasureExclusiveTicks}, " +
            $"buttonMeasureTicks={uniformGridSecondMetrics.ButtonMeasureTicks}, gridMeasureTicks={uniformGridSecondMetrics.GridMeasureTicks}, uniformGridMeasureTicks={uniformGridSecondMetrics.UniformGridMeasureTicks}");

        Assert.True(uniformGridFirstMetrics.TotalMeasureWork > 0);
        Assert.True(gridSecondMetrics.TotalMeasureWork > 0);
        Assert.True(gridFirstMetrics.TotalMeasureWork > 0);
        Assert.True(uniformGridSecondMetrics.TotalMeasureWork > 0);
    }

    [Fact]
    public void Measure_UniformGrid_WithFortyButtons_ShouldReportLayoutMetrics()
    {
        var metrics = MeasurePanelScenario(CreateUniformGridWithButtons());

        _output.WriteLine(
            $"uniform grid isolated: measureWork={metrics.TotalMeasureWork}, arrangeWork={metrics.TotalArrangeWork}, " +
            $"measureTicks={metrics.TotalMeasureTicks}, measureExclusiveTicks={metrics.TotalMeasureExclusiveTicks}, " +
            $"buttonMeasureTicks={metrics.ButtonMeasureTicks}, gridMeasureTicks={metrics.GridMeasureTicks}, uniformGridMeasureTicks={metrics.UniformGridMeasureTicks}");

        Assert.True(metrics.TotalMeasureWork > 0);
    }

    [Fact]
    public void Measure_Grid_WithFortyButtons_ShouldReportLayoutMetrics()
    {
        var metrics = MeasurePanelScenario(CreateGridWithButtons());

        _output.WriteLine(
            $"grid isolated: measureWork={metrics.TotalMeasureWork}, arrangeWork={metrics.TotalArrangeWork}, " +
            $"measureTicks={metrics.TotalMeasureTicks}, measureExclusiveTicks={metrics.TotalMeasureExclusiveTicks}, " +
            $"buttonMeasureTicks={metrics.ButtonMeasureTicks}, gridMeasureTicks={metrics.GridMeasureTicks}, uniformGridMeasureTicks={metrics.UniformGridMeasureTicks}");

        Assert.True(metrics.TotalMeasureWork > 0);
    }

    [Fact]
    public void Compare_UniformGrid_WithFortyTwoButtons_WithAndWithoutText_ShouldReportLayoutMetrics()
    {
        var withTextMetrics = MeasurePanelScenario(CreateUniformGridWithButtonText(buttonCount: 42, includeText: true));
        var withoutTextMetrics = MeasurePanelScenario(CreateUniformGridWithButtonText(buttonCount: 42, includeText: false));

        _output.WriteLine(
            $"uniform grid 42 buttons with text: measureWork={withTextMetrics.TotalMeasureWork}, arrangeWork={withTextMetrics.TotalArrangeWork}, " +
            $"measureTicks={withTextMetrics.TotalMeasureTicks}, measureExclusiveTicks={withTextMetrics.TotalMeasureExclusiveTicks}, " +
            $"buttonMeasureTicks={withTextMetrics.ButtonMeasureTicks}, uniformGridMeasureTicks={withTextMetrics.UniformGridMeasureTicks}, " +
            $"fontMeasureWidthTicks={withTextMetrics.FontMeasureWidthTicks}, fontMeasureWidthCalls={withTextMetrics.FontMeasureWidthCallCount}, " +
            $"fontLineHeightTicks={withTextMetrics.FontGetLineHeightTicks}, fontLineHeightCalls={withTextMetrics.FontGetLineHeightCallCount}, " +
            $"fontDrawTicks={withTextMetrics.FontDrawTicks}, fontDrawCalls={withTextMetrics.FontDrawCallCount}");
        _output.WriteLine(
            $"uniform grid 42 buttons without text: measureWork={withoutTextMetrics.TotalMeasureWork}, arrangeWork={withoutTextMetrics.TotalArrangeWork}, " +
            $"measureTicks={withoutTextMetrics.TotalMeasureTicks}, measureExclusiveTicks={withoutTextMetrics.TotalMeasureExclusiveTicks}, " +
            $"buttonMeasureTicks={withoutTextMetrics.ButtonMeasureTicks}, uniformGridMeasureTicks={withoutTextMetrics.UniformGridMeasureTicks}, " +
            $"fontMeasureWidthTicks={withoutTextMetrics.FontMeasureWidthTicks}, fontMeasureWidthCalls={withoutTextMetrics.FontMeasureWidthCallCount}, " +
            $"fontLineHeightTicks={withoutTextMetrics.FontGetLineHeightTicks}, fontLineHeightCalls={withoutTextMetrics.FontGetLineHeightCallCount}, " +
            $"fontDrawTicks={withoutTextMetrics.FontDrawTicks}, fontDrawCalls={withoutTextMetrics.FontDrawCallCount}");

        Assert.True(withTextMetrics.TotalMeasureWork > 0);
        Assert.True(withoutTextMetrics.TotalMeasureWork > 0);
        Assert.True(withTextMetrics.FontMeasureWidthCallCount > withoutTextMetrics.FontMeasureWidthCallCount);
    }

    [Fact]
    public void Compare_UniformGrid_WithFortyTwoButtons_WithRepeatedAndUniqueText_ShouldReportFontMetrics()
    {
        var repeatedTextMetrics = MeasurePanelScenario(CreateUniformGridWithButtonText(buttonCount: 42, includeText: true, useRepeatedText: true));
        var uniqueTextMetrics = MeasurePanelScenario(CreateUniformGridWithButtonText(buttonCount: 42, includeText: true, useRepeatedText: false));

        _output.WriteLine(
            $"uniform grid 42 buttons repeated text: measureTicks={repeatedTextMetrics.TotalMeasureTicks}, measureExclusiveTicks={repeatedTextMetrics.TotalMeasureExclusiveTicks}, " +
            $"buttonMeasureTicks={repeatedTextMetrics.ButtonMeasureTicks}, uniformGridMeasureTicks={repeatedTextMetrics.UniformGridMeasureTicks}, " +
            $"fontMeasureWidthTicks={repeatedTextMetrics.FontMeasureWidthTicks}, fontMeasureWidthCalls={repeatedTextMetrics.FontMeasureWidthCallCount}, " +
            $"fontLineHeightTicks={repeatedTextMetrics.FontGetLineHeightTicks}, fontLineHeightCalls={repeatedTextMetrics.FontGetLineHeightCallCount}, " +
            $"fontDrawTicks={repeatedTextMetrics.FontDrawTicks}, fontDrawCalls={repeatedTextMetrics.FontDrawCallCount}");
        _output.WriteLine(
            $"uniform grid 42 buttons unique text: measureTicks={uniqueTextMetrics.TotalMeasureTicks}, measureExclusiveTicks={uniqueTextMetrics.TotalMeasureExclusiveTicks}, " +
            $"buttonMeasureTicks={uniqueTextMetrics.ButtonMeasureTicks}, uniformGridMeasureTicks={uniqueTextMetrics.UniformGridMeasureTicks}, " +
            $"fontMeasureWidthTicks={uniqueTextMetrics.FontMeasureWidthTicks}, fontMeasureWidthCalls={uniqueTextMetrics.FontMeasureWidthCallCount}, " +
            $"fontLineHeightTicks={uniqueTextMetrics.FontGetLineHeightTicks}, fontLineHeightCalls={uniqueTextMetrics.FontGetLineHeightCallCount}, " +
            $"fontDrawTicks={uniqueTextMetrics.FontDrawTicks}, fontDrawCalls={uniqueTextMetrics.FontDrawCallCount}");

        Assert.True(repeatedTextMetrics.TotalMeasureWork > 0);
        Assert.True(uniqueTextMetrics.TotalMeasureWork > 0);
    }

    private static PanelScenarioMetrics MeasurePanelScenario(FrameworkElement panel)
    {
        var host = new Canvas
        {
            Width = 800f,
            Height = 500f
        };
        host.AddChild(panel);

        var root = new UiRoot(host);
        var beforeSnapshot = SnapshotElementTimings(host);
        Button.ResetTimingForTests();
        Grid.ResetTimingForTests();
        UniformGrid.ResetTimingForTests();
        UiTextRenderer.ResetTimingForTests();

        RunLayout(root, 800, 500, 16);

        var elementDeltas = CaptureElementTimingDeltas(host, beforeSnapshot);
        var totalTiming = elementDeltas
            .Select(static delta => delta.Timing)
            .Aggregate(LayoutTiming.Zero, Sum);
        var buttonTiming = Button.GetTimingSnapshotForTests();
        var gridTiming = Grid.GetTimingSnapshotForTests();
        var uniformGridTiming = UniformGrid.GetTimingSnapshotForTests();
        var fontTiming = UiTextRenderer.GetTimingSnapshotForTests();

        return new PanelScenarioMetrics(
            totalTiming.MeasureWork,
            totalTiming.ArrangeWork,
            totalTiming.MeasureElapsedTicks,
            totalTiming.MeasureExclusiveElapsedTicks,
            buttonTiming.MeasureOverrideElapsedTicks,
            gridTiming.MeasureOverrideElapsedTicks,
            uniformGridTiming.MeasureOverrideElapsedTicks,
            fontTiming.MeasureWidthElapsedTicks,
            fontTiming.GetLineHeightElapsedTicks,
            fontTiming.DrawStringElapsedTicks,
            fontTiming.MeasureWidthCallCount,
            fontTiming.GetLineHeightCallCount,
            fontTiming.DrawStringCallCount);
    }

    private static UniformGrid CreateUniformGridWithButtons()
    {
        var panel = new UniformGrid
        {
            Rows = 5,
            Columns = 8,
            Width = 800f,
            Height = 500f
        };

        for (var i = 0; i < 40; i++)
        {
            panel.AddChild(new Button { Content = (i + 1).ToString() });
        }

        return panel;
    }

    private static UniformGrid CreateUniformGridWithButtonText(int buttonCount, bool includeText, bool useRepeatedText = false)
    {
        var columns = 7;
        var rows = (int)Math.Ceiling(buttonCount / (double)columns);
        var panel = new UniformGrid
        {
            Rows = rows,
            Columns = columns,
            Width = 800f,
            Height = 500f
        };

        for (var i = 0; i < buttonCount; i++)
        {
            panel.AddChild(new Button
            {
                Content = includeText
                    ? (useRepeatedText ? "1" : (i + 1).ToString())
                    : string.Empty
            });
        }

        return panel;
    }

    private static Grid CreateGridWithButtons()
    {
        var panel = new Grid
        {
            Width = 800f,
            Height = 500f
        };

        for (var row = 0; row < 5; row++)
        {
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
        }

        for (var column = 0; column < 8; column++)
        {
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        }

        for (var i = 0; i < 40; i++)
        {
            var button = new Button { Content = (i + 1).ToString() };
            Grid.SetRow(button, i / 8);
            Grid.SetColumn(button, i % 8);
            panel.AddChild(button);
        }

        return panel;
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
                    element.MeasureExclusiveElapsedTicksForTests));
    }

    private static IReadOnlyList<ElementTimingDelta> CaptureElementTimingDeltas(
        UIElement root,
        IReadOnlyDictionary<FrameworkElement, LayoutTiming> beforeSnapshot)
    {
        return EnumerateVisualTree(root)
            .OfType<FrameworkElement>()
            .Select(element =>
            {
                beforeSnapshot.TryGetValue(element, out var before);
                return new ElementTimingDelta(
                    element,
                    new LayoutTiming(
                        element.MeasureWorkCount - before.MeasureWork,
                        element.ArrangeWorkCount - before.ArrangeWork,
                        element.MeasureElapsedTicksForTests - before.MeasureElapsedTicks,
                        element.MeasureExclusiveElapsedTicksForTests - before.MeasureExclusiveElapsedTicks));
            })
            .Where(static delta =>
                delta.Timing.MeasureWork > 0 ||
                delta.Timing.ArrangeWork > 0 ||
                delta.Timing.MeasureElapsedTicks > 0 ||
                delta.Timing.MeasureExclusiveElapsedTicks > 0)
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

    private static LayoutTiming Sum(LayoutTiming left, LayoutTiming right)
    {
        return new LayoutTiming(
            left.MeasureWork + right.MeasureWork,
            left.ArrangeWork + right.ArrangeWork,
            left.MeasureElapsedTicks + right.MeasureElapsedTicks,
            left.MeasureExclusiveElapsedTicks + right.MeasureExclusiveElapsedTicks);
    }

    private readonly record struct LayoutTiming(
        long MeasureWork,
        long ArrangeWork,
        long MeasureElapsedTicks,
        long MeasureExclusiveElapsedTicks)
    {
        public static LayoutTiming Zero => new(0, 0, 0, 0);
    }

    private readonly record struct ElementTimingDelta(
        FrameworkElement Element,
        LayoutTiming Timing);

    private readonly record struct PanelScenarioMetrics(
        long TotalMeasureWork,
        long TotalArrangeWork,
        long TotalMeasureTicks,
        long TotalMeasureExclusiveTicks,
        long ButtonMeasureTicks,
        long GridMeasureTicks,
        long UniformGridMeasureTicks,
        long FontMeasureWidthTicks,
        long FontGetLineHeightTicks,
        long FontDrawTicks,
        int FontMeasureWidthCallCount,
        int FontGetLineHeightCallCount,
        int FontDrawCallCount);
}
