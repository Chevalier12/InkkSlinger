using Microsoft.Xna.Framework;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.Tests;

public sealed class GridResizeHotspotInvestigationTests
{
    private readonly ITestOutputHelper _output;

    public GridResizeHotspotInvestigationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ShrinkingBadgeGrid_AttributesRepeatedLayoutWorkToWrappedTextBlock()
    {
        TextLayout.ResetMetricsForTests();
        _ = TextBlock.GetTelemetryAndReset();
        _ = Grid.GetTelemetryAndReset();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var badge = new Border
        {
            Width = 72f,
            Height = 24f,
            Margin = new Thickness(0f, 0f, 12f, 0f)
        };
        grid.AddChild(badge);

        var text = new TextBlock
        {
            Text = "This badge row behaves like the GridSplitter surface: the badge keeps its fixed width while the wrapped summary keeps being recomputed as the host shrinks.",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(text, 1);
        grid.AddChild(text);

        var beforeGridMeasureWork = grid.MeasureWorkCount;
        var beforeGridMeasureTicks = grid.MeasureExclusiveElapsedTicksForTests;
        var beforeBadgeMeasureWork = badge.MeasureWorkCount;
        var beforeBadgeMeasureTicks = badge.MeasureExclusiveElapsedTicksForTests;
        var beforeTextMeasureWork = text.MeasureWorkCount;
        var beforeTextMeasureTicks = text.MeasureExclusiveElapsedTicksForTests;

        foreach (var width in new[] { 400f, 360f, 320f, 280f, 240f, 220f, 200f })
        {
            MeasureArrangeAndUpdate(grid, width, 140f);
        }

        var textTelemetry = TextBlock.GetTelemetryAndReset();
        var gridTelemetry = Grid.GetTelemetryAndReset();
        var textLayoutMetrics = TextLayout.GetMetricsSnapshot();

        var gridMeasureWork = grid.MeasureWorkCount - beforeGridMeasureWork;
        var gridMeasureTicks = grid.MeasureExclusiveElapsedTicksForTests - beforeGridMeasureTicks;
        var badgeMeasureWork = badge.MeasureWorkCount - beforeBadgeMeasureWork;
        var badgeMeasureTicks = badge.MeasureExclusiveElapsedTicksForTests - beforeBadgeMeasureTicks;
        var textMeasureWork = text.MeasureWorkCount - beforeTextMeasureWork;
        var textMeasureTicks = text.MeasureExclusiveElapsedTicksForTests - beforeTextMeasureTicks;
        var textRuntime = text.GetTextBlockSnapshotForDiagnostics();

        _output.WriteLine(
            $"grid measureWork={gridMeasureWork}, measureTicks={gridMeasureTicks}, remeasureChecks={gridTelemetry.MeasureRemeasureCheckCount}, remeasures={gridTelemetry.MeasureRemeasureCount}");
        _output.WriteLine(
            $"badge measureWork={badgeMeasureWork}, measureTicks={badgeMeasureTicks}, desired={badge.DesiredSize.X:0.##}x{badge.DesiredSize.Y:0.##}");
        _output.WriteLine(
            $"text measureWork={textMeasureWork}, measureTicks={textMeasureTicks}, desired={text.DesiredSize.X:0.##}x{text.DesiredSize.Y:0.##}, actual={text.ActualWidth:0.##}x{text.ActualHeight:0.##}");
        _output.WriteLine(
            $"text telemetry resolveLayoutCalls={textTelemetry.ResolveLayoutCallCount}, cacheHits={textTelemetry.ResolveLayoutCacheHitCount}, cacheMisses={textTelemetry.ResolveLayoutCacheMissCount}, tooNarrowRejects={textTelemetry.CanReuseMeasureTooNarrowRejectCount}, wrappedBuilds={textLayoutMetrics.WrappedBuildCount}");
        _output.WriteLine(
            $"text runtime resolveLayoutMs={textRuntime.ResolveLayoutMilliseconds:0.###}, measureOverrideMs={textRuntime.MeasureOverrideMilliseconds:0.###}, layoutMeasurePathCalls={textRuntime.LayoutMeasurePathCallCount}");

        Assert.True(textMeasureWork >= 7, $"Expected wrapped text to do measure work on each shrink step, actual={textMeasureWork}.");
        Assert.True(textTelemetry.ResolveLayoutCallCount >= 7, $"Expected wrapped text layout resolution on each shrink step, actual={textTelemetry.ResolveLayoutCallCount}.");
        Assert.True(textLayoutMetrics.WrappedBuildCount >= 7, $"Expected TextLayout wrapped builds on each unique width, actual={textLayoutMetrics.WrappedBuildCount}.");
        Assert.True(textTelemetry.CanReuseMeasureTooNarrowRejectCount > 0, "Expected wrapped text reuse rejection when the available width shrinks.");
        Assert.True(textMeasureTicks > 0, "Expected the wrapped TextBlock to accumulate measurable layout time.");
    }

    [Fact]
    public void ShrinkingBadgeGrid_WithNoWrapText_DoesNotShowTheSameTextLayoutChurn()
    {
        TextLayout.ResetMetricsForTests();
        _ = TextBlock.GetTelemetryAndReset();
        _ = Grid.GetTelemetryAndReset();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var badge = new Border
        {
            Width = 72f,
            Height = 24f,
            Margin = new Thickness(0f, 0f, 12f, 0f)
        };
        grid.AddChild(badge);

        var text = new TextBlock
        {
            Text = "This badge row behaves like the GridSplitter surface but the text stays on the no-wrap path while the host shrinks.",
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(text, 1);
        grid.AddChild(text);

        var beforeGridMeasureWork = grid.MeasureWorkCount;
        var beforeGridMeasureTicks = grid.MeasureExclusiveElapsedTicksForTests;
        var beforeBadgeMeasureWork = badge.MeasureWorkCount;
        var beforeBadgeMeasureTicks = badge.MeasureExclusiveElapsedTicksForTests;
        var beforeTextMeasureWork = text.MeasureWorkCount;
        var beforeTextMeasureTicks = text.MeasureExclusiveElapsedTicksForTests;

        foreach (var width in new[] { 400f, 360f, 320f, 280f, 240f, 220f, 200f })
        {
            MeasureArrangeAndUpdate(grid, width, 140f);
        }

        var textTelemetry = TextBlock.GetTelemetryAndReset();
        var gridTelemetry = Grid.GetTelemetryAndReset();
        var textLayoutMetrics = TextLayout.GetMetricsSnapshot();

        var gridMeasureWork = grid.MeasureWorkCount - beforeGridMeasureWork;
        var gridMeasureTicks = grid.MeasureExclusiveElapsedTicksForTests - beforeGridMeasureTicks;
        var badgeMeasureWork = badge.MeasureWorkCount - beforeBadgeMeasureWork;
        var badgeMeasureTicks = badge.MeasureExclusiveElapsedTicksForTests - beforeBadgeMeasureTicks;
        var textMeasureWork = text.MeasureWorkCount - beforeTextMeasureWork;
        var textMeasureTicks = text.MeasureExclusiveElapsedTicksForTests - beforeTextMeasureTicks;
        var textRuntime = text.GetTextBlockSnapshotForDiagnostics();

        _output.WriteLine(
            $"nowrap grid measureWork={gridMeasureWork}, measureTicks={gridMeasureTicks}, remeasureChecks={gridTelemetry.MeasureRemeasureCheckCount}, remeasures={gridTelemetry.MeasureRemeasureCount}");
        _output.WriteLine(
            $"nowrap badge measureWork={badgeMeasureWork}, measureTicks={badgeMeasureTicks}, desired={badge.DesiredSize.X:0.##}x{badge.DesiredSize.Y:0.##}");
        _output.WriteLine(
            $"nowrap text measureWork={textMeasureWork}, measureTicks={textMeasureTicks}, desired={text.DesiredSize.X:0.##}x{text.DesiredSize.Y:0.##}, actual={text.ActualWidth:0.##}x{text.ActualHeight:0.##}");
        _output.WriteLine(
            $"nowrap text telemetry resolveLayoutCalls={textTelemetry.ResolveLayoutCallCount}, cacheHits={textTelemetry.ResolveLayoutCacheHitCount}, cacheMisses={textTelemetry.ResolveLayoutCacheMissCount}, intrinsicMeasurePathCalls={textTelemetry.IntrinsicMeasurePathCallCount}, wrappedBuilds={textLayoutMetrics.WrappedBuildCount}");
        _output.WriteLine(
            $"nowrap text runtime resolveLayoutMs={textRuntime.ResolveLayoutMilliseconds:0.###}, measureOverrideMs={textRuntime.MeasureOverrideMilliseconds:0.###}, canReuseMeasureTrue={textRuntime.CanReuseMeasureTrueCount}");

        Assert.True(textMeasureWork <= 2, $"Expected no-wrap text to avoid repeated measure work during shrink, actual={textMeasureWork}.");
        Assert.Equal(0, textTelemetry.ResolveLayoutCallCount);
        Assert.Equal(0, textLayoutMetrics.WrappedBuildCount);
        Assert.True(textTelemetry.IntrinsicMeasurePathCallCount > 0, "Expected no-wrap text to stay on the intrinsic measure path.");
    }

    private static void MeasureArrangeAndUpdate(FrameworkElement element, float width, float height)
    {
        element.Measure(new Vector2(width, height));
        element.Arrange(new LayoutRect(0f, 0f, width, height));
        element.UpdateLayout();
    }
}
