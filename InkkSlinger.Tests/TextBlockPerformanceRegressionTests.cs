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
}
