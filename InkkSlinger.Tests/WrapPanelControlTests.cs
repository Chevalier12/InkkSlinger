using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class WrapPanelControlTests
{
    [Fact]
    public void WrapPanel_AggregateTelemetry_CapturesWrapPathsAndResets()
    {
        _ = WrapPanel.GetTelemetryAndReset();

        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };
        panel.AddChild(CreateSizedBorder(40f, 10f));
        panel.AddChild(new BareElement());
        panel.AddChild(CreateSizedBorder(35f, 12f));
        panel.AddChild(CreateSizedBorder(20f, 8f));

        panel.Measure(new Vector2(60f, 100f));
        panel.Arrange(new LayoutRect(0f, 0f, 60f, 100f));

        panel.Orientation = Orientation.Vertical;
        panel.ItemWidth = 25f;
        panel.ItemHeight = 10f;
        panel.Measure(new Vector2(100f, 18f));
        panel.Arrange(new LayoutRect(0f, 0f, 100f, 18f));

        var diagnostics = WrapPanel.GetTelemetrySnapshotForDiagnostics();

        Assert.Equal(2, diagnostics.MeasureCallCount);
        Assert.Equal(2, diagnostics.ArrangeCallCount);
        Assert.Equal(6, diagnostics.MeasuredChildCount);
        Assert.Equal(6, diagnostics.ArrangedChildCount);
        Assert.Equal(2, diagnostics.MeasureSkippedChildCount);
        Assert.Equal(2, diagnostics.ArrangeSkippedChildCount);
        Assert.Equal(1, diagnostics.MeasureHorizontalCount);
        Assert.Equal(1, diagnostics.MeasureVerticalCount);
        Assert.Equal(1, diagnostics.ArrangeHorizontalCount);
        Assert.Equal(1, diagnostics.ArrangeVerticalCount);
        Assert.Equal(3, diagnostics.MeasureWrapCount);
        Assert.Equal(3, diagnostics.ArrangeWrapCount);
        Assert.Equal(5, diagnostics.MeasureCommittedLineCount);
        Assert.Equal(5, diagnostics.ArrangeCommittedLineCount);
        Assert.Equal(0, diagnostics.MeasureEmptyCount);
        Assert.Equal(0, diagnostics.ArrangeEmptyCount);
        Assert.Equal(3, diagnostics.MeasureExplicitItemWidthCount);
        Assert.Equal(3, diagnostics.MeasureAvailableWidthCount);
        Assert.Equal(3, diagnostics.MeasureExplicitItemHeightCount);
        Assert.Equal(3, diagnostics.MeasureAvailableHeightCount);
        Assert.Equal(12, diagnostics.GetChildSizeCallCount);
        Assert.Equal(6, diagnostics.GetChildSizeFromMeasureCount);
        Assert.Equal(6, diagnostics.GetChildSizeFromArrangeCount);
        Assert.Equal(6, diagnostics.GetChildSizeExplicitWidthCount);
        Assert.Equal(6, diagnostics.GetChildSizeDesiredWidthCount);
        Assert.Equal(6, diagnostics.GetChildSizeExplicitHeightCount);
        Assert.Equal(6, diagnostics.GetChildSizeDesiredHeightCount);
        Assert.True(diagnostics.MeasureMilliseconds >= 0d);
        Assert.True(diagnostics.ArrangeMilliseconds >= 0d);
        Assert.True(diagnostics.GetChildSizeMilliseconds >= 0d);

        var reset = WrapPanel.GetTelemetryAndReset();

        Assert.Equal(diagnostics.MeasureCallCount, reset.MeasureCallCount);
        Assert.Equal(diagnostics.ArrangeCallCount, reset.ArrangeCallCount);
        Assert.Equal(diagnostics.GetChildSizeCallCount, reset.GetChildSizeCallCount);

        var cleared = WrapPanel.GetTelemetryAndReset();

        Assert.Equal(0, cleared.MeasureCallCount);
        Assert.Equal(0, cleared.ArrangeCallCount);
        Assert.Equal(0, cleared.GetChildSizeCallCount);
    }

    [Fact]
    public void WrapPanel_AggregateTelemetry_CapturesUnlimitedAndNonPositiveLineLimits()
    {
        _ = WrapPanel.GetTelemetryAndReset();

        var panel = new WrapPanel
        {
            ItemWidth = 20f,
            ItemHeight = 10f
        };
        panel.AddChild(CreateSizedBorder(5f, 5f));
        panel.AddChild(CreateSizedBorder(5f, 5f));

        panel.Measure(new Vector2(float.PositiveInfinity, 40f));
        panel.Arrange(new LayoutRect(0f, 0f, 0f, 40f));

        var snapshot = WrapPanel.GetTelemetryAndReset();

        Assert.Equal(1, snapshot.MeasureInfiniteLineLimitCount);
        Assert.Equal(0, snapshot.MeasureNaNLineLimitCount);
        Assert.Equal(0, snapshot.MeasureNonPositiveLineLimitCount);
        Assert.Equal(0, snapshot.ArrangeInfiniteLineLimitCount);
        Assert.Equal(0, snapshot.ArrangeNaNLineLimitCount);
        Assert.Equal(1, snapshot.ArrangeNonPositiveLineLimitCount);
        Assert.Equal(1, snapshot.MeasureCallCount);
        Assert.Equal(1, snapshot.ArrangeCallCount);
        Assert.Equal(2, snapshot.MeasuredChildCount);
        Assert.Equal(2, snapshot.ArrangedChildCount);
    }

    private static Border CreateSizedBorder(float width, float height)
    {
        return new Border
        {
            Width = width,
            Height = height
        };
    }

    private sealed class BareElement : UIElement
    {
    }
}