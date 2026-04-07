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

    [Fact]
    public void WrappedTextBlock_ReusesResolvedLayoutAcrossDistinctWidthsInsideReusableRange()
    {
        var text = new TextBlock
        {
            Text = "The framework should not rebuild wrapped line layout for every nearby drag width when the previous line breaks remain valid across the same reusable width interval.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16f
        };

        var widths = FindReusableWidths(text, 220f);

        TextLayout.ResetMetricsForTests();
        _ = TextBlock.GetTelemetryAndReset();

        foreach (var width in widths)
        {
            text.InvalidateMeasure();
            text.Measure(new Vector2(width, 200f));
        }

        var telemetry = TextBlock.GetTelemetryAndReset();
        var metrics = TextLayout.GetMetricsSnapshot();

        Assert.Equal(widths.Length, telemetry.ResolveLayoutCallCount);
        Assert.Equal(1, telemetry.ResolveLayoutCacheMissCount);
        Assert.True(telemetry.ResolveLayoutCacheHitCount >= widths.Length - 1, $"Expected interval-based wrapped layout reuse for nearby widths. hits={telemetry.ResolveLayoutCacheHitCount}, widths={widths.Length}");
        Assert.Equal(1, metrics.WrappedBuildCount);
    }

    [Fact]
    public void WrappedTextBlock_RebuildsWhenWidthLeavesReusableRange()
    {
        var text = new TextBlock
        {
            Text = "Leaving the reusable width interval should still trigger a fresh wrapped layout build so the framework does not reuse stale line breaks.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16f
        };

        var widths = FindReusableWidths(text, 220f);
        var typography = UiTextRenderer.ResolveTypography(text, text.FontSize);
        var layout = TextLayout.Layout(text.Text, typography, text.FontSize, widths[0], TextWrapping.Wrap);
        var outsideWidth = ResolveOutsideReusableWidth(layout, widths[0]);

        TextLayout.ResetMetricsForTests();
        _ = TextBlock.GetTelemetryAndReset();

        text.InvalidateMeasure();
        text.Measure(new Vector2(widths[0], 200f));
        text.InvalidateMeasure();
        text.Measure(new Vector2(outsideWidth, 200f));

        var telemetry = TextBlock.GetTelemetryAndReset();
        var metrics = TextLayout.GetMetricsSnapshot();

        Assert.Equal(2, telemetry.ResolveLayoutCallCount);
        Assert.Equal(2, telemetry.ResolveLayoutCacheMissCount);
        Assert.Equal(2, metrics.WrappedBuildCount);
    }

    private static void MeasureArrangeAndUpdate(FrameworkElement element, float width, float height)
    {
        element.Measure(new Vector2(width, height));
        element.Arrange(new LayoutRect(0f, 0f, width, height));
        element.UpdateLayout();
    }

    private static float[] FindReusableWidths(TextBlock textBlock, float seedWidth)
    {
        var typography = UiTextRenderer.ResolveTypography(textBlock, textBlock.FontSize);
        foreach (var candidateWidth in EnumerateCandidateWidths(seedWidth))
        {
            var layout = TextLayout.Layout(textBlock.Text, typography, textBlock.FontSize, candidateWidth, TextWrapping.Wrap);
            var minimum = MathF.Max(1f, layout.ReusableMinimumWidth + 0.75f);
            var maximum = float.IsFinite(layout.ReusableMaximumWidth)
                ? layout.ReusableMaximumWidth - 0.75f
                : minimum + 0.4f;
            if (maximum - minimum >= 0.2f)
            {
                var widths = new[]
                {
                    minimum,
                    minimum + 0.1f,
                    minimum + 0.2f
                };

                if (widths[2] <= maximum &&
                    HaveMatchingWrappedLines(textBlock.Text, typography, textBlock.FontSize, widths))
                {
                    return widths;
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Could not find a reusable wrapped width interval wide enough for the regression test.");
    }

    private static float ResolveOutsideReusableWidth(TextLayout.TextLayoutResult layout, float insideWidth)
    {
        var belowMinimum = MathF.Floor(layout.ReusableMinimumWidth - 2f);
        if (belowMinimum > 0.01f && !ApproximatelyEqual(belowMinimum, insideWidth))
        {
            return belowMinimum;
        }

        if (float.IsFinite(layout.ReusableMaximumWidth))
        {
            var aboveMaximum = MathF.Ceiling(layout.ReusableMaximumWidth + 2f);
            if (!ApproximatelyEqual(aboveMaximum, insideWidth))
            {
                return aboveMaximum;
            }
        }

        throw new Xunit.Sdk.XunitException("Could not resolve a width outside the reusable wrapped layout interval.");
    }

    private static bool ApproximatelyEqual(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }

    private static bool HaveMatchingWrappedLines(string text, UiTypography typography, float fontSize, float[] widths)
    {
        var baseline = TextLayout.Layout(text, typography, fontSize, widths[0], TextWrapping.Wrap);
        for (var i = 1; i < widths.Length; i++)
        {
            var candidate = TextLayout.Layout(text, typography, fontSize, widths[i], TextWrapping.Wrap);
            if (candidate.Lines.Count != baseline.Lines.Count)
            {
                return false;
            }

            for (var lineIndex = 0; lineIndex < baseline.Lines.Count; lineIndex++)
            {
                if (!string.Equals(candidate.Lines[lineIndex], baseline.Lines[lineIndex], StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static IEnumerable<float> EnumerateCandidateWidths(float seedWidth)
    {
        yield return seedWidth;

        for (var width = 120f; width <= 320f; width += 5f)
        {
            if (!ApproximatelyEqual(width, seedWidth))
            {
                yield return width;
            }
        }
    }
}
