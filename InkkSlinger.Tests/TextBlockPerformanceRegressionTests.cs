using System.Linq;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TextBlockPerformanceRegressionTests
{
    [Fact]
    public void Measure_NoWrapSingleLineTextBlock_DoesNotInvokeTextLayout()
    {
        TextLayout.ResetMetricsForTests();

        var textBlock = new TextBlock
        {
            Text = "March 2026"
        };

        textBlock.Measure(new Vector2(300f, 40f));

        var expected = new Vector2(
            UiTextRenderer.MeasureWidth(textBlock, textBlock.Text, textBlock.FontSize),
            UiTextRenderer.GetLineHeight(textBlock, textBlock.FontSize));

        Assert.InRange(textBlock.DesiredSize.X, expected.X - 0.01f, expected.X + 0.01f);
        Assert.InRange(textBlock.DesiredSize.Y, expected.Y - 0.01f, expected.Y + 0.01f);
        Assert.Equal(0, TextLayout.GetMetricsSnapshot().BuildCount);
    }

    [Fact]
    public void Measure_NoWrapSingleLineTextBlock_WithExplicitLineHeight_UsesOverrideWithoutTextLayout()
    {
        TextLayout.ResetMetricsForTests();

        var textBlock = new TextBlock
        {
            Text = "March 2026",
            LineHeight = 22f
        };

        textBlock.Measure(new Vector2(300f, 40f));

        var expectedWidth = UiTextRenderer.MeasureWidth(textBlock, textBlock.Text, textBlock.FontSize);
        Assert.InRange(textBlock.DesiredSize.X, expectedWidth - 0.01f, expectedWidth + 0.01f);
        Assert.Equal(22f, textBlock.DesiredSize.Y);
        Assert.Equal(0, TextLayout.GetMetricsSnapshot().BuildCount);
    }

    [Fact]
    public void Measure_NoWrapSingleLineTextBlock_ReusesMeasureAcrossAvailableSizeChanges()
    {
        var textBlock = new TextBlock
        {
            Text = "Wed"
        };

        textBlock.Measure(new Vector2(40f, 20f));
        textBlock.Measure(new Vector2(300f, 60f));

        Assert.Equal(2, textBlock.MeasureCallCount);
        Assert.Equal(1, textBlock.MeasureWorkCount);
    }

    [Fact]
    public void Measure_WrappedSingleLineText_WhenWidthStaysAboveIntrinsicLineWidth_ReusesMeasure()
    {
        TextLayout.ResetMetricsForTests();

        var textBlock = new TextBlock
        {
            Text = "Calendar",
            TextWrapping = TextWrapping.Wrap
        };

        var intrinsicWidth = UiTextRenderer.MeasureWidth(textBlock, textBlock.Text, textBlock.FontSize);
        textBlock.Measure(new Vector2(intrinsicWidth + 10f, 20f));
        textBlock.Measure(new Vector2(intrinsicWidth + 80f, 40f));

        Assert.Equal(2, textBlock.MeasureCallCount);
        Assert.Equal(1, textBlock.MeasureWorkCount);
        Assert.Equal(0, TextLayout.GetMetricsSnapshot().BuildCount);
    }

    [Fact]
    public void Measure_WrappedSingleLineText_WhenWidthDropsBelowIntrinsicLineWidth_StillReMeasures()
    {
        var textBlock = new TextBlock
        {
            Text = "Calendar",
            TextWrapping = TextWrapping.Wrap
        };

        var intrinsicWidth = UiTextRenderer.MeasureWidth(textBlock, textBlock.Text, textBlock.FontSize);
        textBlock.Measure(new Vector2(intrinsicWidth + 10f, 20f));
        textBlock.Measure(new Vector2(MathF.Max(1f, intrinsicWidth - 5f), 40f));

        Assert.Equal(2, textBlock.MeasureCallCount);
        Assert.Equal(2, textBlock.MeasureWorkCount);
    }

    [Fact]
    public void Measure_WrappedText_WhenNarrowingButCurrentWrappedLinesStillFit_ReusesMeasure()
    {
        var textBlock = new TextBlock
        {
            Text = "Wednesday Thursday",
            TextWrapping = TextWrapping.Wrap
        };

        var widestWrappedLine = MathF.Max(
            UiTextRenderer.MeasureWidth(textBlock, "Wednesday", textBlock.FontSize),
            UiTextRenderer.MeasureWidth(textBlock, "Thursday", textBlock.FontSize));
        var initialWidth = widestWrappedLine + 24f;
        var narrowerWidth = widestWrappedLine + 2f;

        textBlock.Measure(new Vector2(initialWidth, 80f));
        var wrappedDesired = textBlock.DesiredSize;

        var initialLayout = TextLayout.LayoutForElement(
            textBlock.Text,
            textBlock,
            textBlock.FontSize,
            initialWidth,
            TextWrapping.Wrap);
        var narrowerLayout = TextLayout.LayoutForElement(
            textBlock.Text,
            textBlock,
            textBlock.FontSize,
            narrowerWidth,
            TextWrapping.Wrap);

        Assert.Equal(
            initialLayout.Lines.Select(static line => line.TrimEnd()).ToArray(),
            narrowerLayout.Lines.Select(static line => line.TrimEnd()).ToArray());

        textBlock.Measure(new Vector2(narrowerWidth, 80f));

        Assert.Equal(2, textBlock.MeasureCallCount);
        Assert.Equal(1, textBlock.MeasureWorkCount);
        Assert.Equal(wrappedDesired, textBlock.DesiredSize);
    }

    [Fact]
    public void Measure_WrappedText_WithZeroAvailableWidth_CollapsesWithoutBuildingLayout()
    {
        TextLayout.ResetMetricsForTests();

        var textBlock = new TextBlock
        {
            Text = "Exact wrapped copy stays expensive when width drops to zero.",
            TextWrapping = TextWrapping.Wrap
        };

        textBlock.Measure(new Vector2(0f, 200f));

        Assert.Equal(Vector2.Zero, textBlock.DesiredSize);
        Assert.Equal(0, TextLayout.GetMetricsSnapshot().BuildCount);
    }

    [Fact]
    public void Measure_MultilineTextBlock_WithExplicitLineHeight_UsesOverrideForDesiredHeight()
    {
        var textBlock = new TextBlock
        {
            Text = "Line one\nLine two",
            LineHeight = 22f
        };

        textBlock.Measure(new Vector2(300f, 200f));

        Assert.Equal(22f * 2f, textBlock.DesiredSize.Y);
    }

    [Fact]
    public void Measure_NoWrapSingleLineTextBlock_WithCharacterSpacing_IncreasesDesiredWidth()
    {
        var normal = new TextBlock
        {
            Text = "AB"
        };

        var tracked = new TextBlock
        {
            Text = "AB",
            CharacterSpacing = 100
        };

        normal.Measure(new Vector2(300f, 40f));
        tracked.Measure(new Vector2(300f, 40f));

        Assert.True(tracked.DesiredSize.X > normal.DesiredSize.X);
        Assert.Equal(tracked.FontSize * 0.1f, tracked.DesiredSize.X - normal.DesiredSize.X, 3);
    }

    [Fact]
    public void LineHeight_NonPositiveValue_ThrowsArgumentException()
    {
        var textBlock = new TextBlock();

        Assert.Throws<ArgumentException>(() => textBlock.LineHeight = 0f);
        Assert.Throws<ArgumentException>(() => textBlock.LineHeight = -1f);
    }
}
