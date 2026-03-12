using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TextBlockPerformanceRegressionTests
{
    [Fact]
    public void Measure_NoWrapSingleLineLabel_DoesNotInvokeTextLayout()
    {
        TextLayout.ResetMetricsForTests();

        var label = new Label
        {
            Text = "March 2026"
        };

        label.Measure(new Vector2(300f, 40f));

        var expected = new Vector2(
            UiTextRenderer.MeasureWidth(label.Font, label.Text, label.FontSize),
            UiTextRenderer.GetLineHeight(label.Font, label.FontSize));

        Assert.InRange(label.DesiredSize.X, expected.X - 0.01f, expected.X + 0.01f);
        Assert.InRange(label.DesiredSize.Y, expected.Y - 0.01f, expected.Y + 0.01f);
        Assert.Equal(0, TextLayout.GetMetricsSnapshot().BuildCount);
    }

    [Fact]
    public void Measure_NoWrapSingleLineLabel_ReusesMeasureAcrossAvailableSizeChanges()
    {
        var label = new Label
        {
            Text = "Wed"
        };

        label.Measure(new Vector2(40f, 20f));
        label.Measure(new Vector2(300f, 60f));

        Assert.Equal(2, label.MeasureCallCount);
        Assert.Equal(1, label.MeasureWorkCount);
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

        var intrinsicWidth = UiTextRenderer.MeasureWidth(textBlock.Font, textBlock.Text, textBlock.FontSize);
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

        var intrinsicWidth = UiTextRenderer.MeasureWidth(textBlock.Font, textBlock.Text, textBlock.FontSize);
        textBlock.Measure(new Vector2(intrinsicWidth + 10f, 20f));
        textBlock.Measure(new Vector2(MathF.Max(1f, intrinsicWidth - 5f), 40f));

        Assert.Equal(2, textBlock.MeasureCallCount);
        Assert.Equal(2, textBlock.MeasureWorkCount);
    }
}
