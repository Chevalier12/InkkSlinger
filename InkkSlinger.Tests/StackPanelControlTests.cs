using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class StackPanelControlTests
{
    [Fact]
    public void StackPanel_AggregateTelemetry_CapturesOrientationBranchesAndResets()
    {
        _ = StackPanel.GetTelemetryAndReset();

        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical
        };
        panel.AddChild(CreateSizedBorder(40f, 10f));
        panel.AddChild(new BareElement());
        panel.AddChild(CreateSizedBorder(50f, 12f));
        panel.AddChild(CreateSizedBorder(30f, 8f));

        panel.Measure(new Vector2(80f, 100f));
        panel.Arrange(new LayoutRect(0f, 0f, 80f, 100f));

        panel.Orientation = Orientation.Horizontal;
        panel.Measure(new Vector2(90f, 20f));
        panel.Arrange(new LayoutRect(0f, 0f, 90f, 20f));

        var runtime = panel.GetStackPanelSnapshotForDiagnostics();
        var diagnostics = StackPanel.GetAggregateTelemetrySnapshotForDiagnostics();

        Assert.Equal(Orientation.Horizontal, runtime.Orientation);
        Assert.Equal(4, runtime.ChildCount);
        Assert.Equal(120f, runtime.DesiredWidth, 3);
        Assert.Equal(12f, runtime.DesiredHeight, 3);
        Assert.Equal(90f, runtime.RenderWidth, 3);
        Assert.Equal(20f, runtime.RenderHeight, 3);
        Assert.Equal(90f, runtime.ActualWidth, 3);
        Assert.Equal(20f, runtime.ActualHeight, 3);
        Assert.Equal(90f, runtime.PreviousAvailableWidth, 3);
        Assert.Equal(20f, runtime.PreviousAvailableHeight, 3);
        Assert.Equal(2, runtime.MeasureCallCount);
        Assert.Equal(2, runtime.MeasureWorkCount);
        Assert.Equal(2, runtime.ArrangeCallCount);
        Assert.Equal(2, runtime.ArrangeWorkCount);
        Assert.True(runtime.MeasureMilliseconds >= 0d);
        Assert.True(runtime.MeasureExclusiveMilliseconds >= 0d);
        Assert.True(runtime.ArrangeMilliseconds >= 0d);
        Assert.True(runtime.IsMeasureValid);
        Assert.True(runtime.IsArrangeValid);

        Assert.Equal(2, diagnostics.MeasureCallCount);
        Assert.Equal(2, diagnostics.ArrangeCallCount);
        Assert.Equal(6, diagnostics.MeasuredChildCount);
        Assert.Equal(6, diagnostics.ArrangedChildCount);
        Assert.Equal(2, diagnostics.MeasureSkippedChildCount);
        Assert.Equal(2, diagnostics.ArrangeSkippedChildCount);
        Assert.Equal(1, diagnostics.MeasureVerticalCount);
        Assert.Equal(1, diagnostics.MeasureHorizontalCount);
        Assert.Equal(1, diagnostics.ArrangeVerticalCount);
        Assert.Equal(1, diagnostics.ArrangeHorizontalCount);
        Assert.Equal(0, diagnostics.MeasureEmptyCount);
        Assert.Equal(0, diagnostics.ArrangeEmptyCount);
        Assert.Equal(0, diagnostics.MeasureInfiniteCrossAxisCount);
        Assert.Equal(0, diagnostics.MeasureNaNCrossAxisCount);
        Assert.Equal(0, diagnostics.MeasureNonPositiveCrossAxisCount);
        Assert.Equal(0, diagnostics.ArrangeInfinitePrimarySizeCount);
        Assert.Equal(0, diagnostics.ArrangeNaNPrimarySizeCount);
        Assert.Equal(0, diagnostics.ArrangeNonPositivePrimarySizeCount);
        Assert.Equal(150d, diagnostics.MeasureTotalPrimaryDesired, 3);
        Assert.Equal(62d, diagnostics.MeasureTotalCrossDesired, 3);
        Assert.Equal(150d, diagnostics.ArrangeTotalPrimarySpan, 3);
        Assert.Equal(100d, diagnostics.ArrangeTotalCrossSpan, 3);
        Assert.True(diagnostics.MeasureMilliseconds >= 0d);
        Assert.True(diagnostics.ArrangeMilliseconds >= 0d);

        var reset = StackPanel.GetTelemetryAndReset();

        Assert.Equal(diagnostics.MeasureCallCount, reset.MeasureCallCount);
        Assert.Equal(diagnostics.ArrangeCallCount, reset.ArrangeCallCount);
        Assert.Equal(diagnostics.MeasureSkippedChildCount, reset.MeasureSkippedChildCount);
        Assert.Equal(diagnostics.ArrangeSkippedChildCount, reset.ArrangeSkippedChildCount);
        Assert.Equal(diagnostics.ArrangeTotalPrimarySpan, reset.ArrangeTotalPrimarySpan, 3);

        var cleared = StackPanel.GetTelemetryAndReset();

        Assert.Equal(0, cleared.MeasureCallCount);
        Assert.Equal(0, cleared.ArrangeCallCount);
        Assert.Equal(0, cleared.MeasureSkippedChildCount);
        Assert.Equal(0, cleared.ArrangeSkippedChildCount);
        Assert.Equal(0d, cleared.ArrangeTotalPrimarySpan, 3);
    }

    [Fact]
    public void StackPanel_AggregateTelemetry_CapturesConstraintShapesAndEmptyPaths()
    {
        _ = StackPanel.GetTelemetryAndReset();

        var panel = new TestStackPanel
        {
            Orientation = Orientation.Vertical
        };

        _ = panel.InvokeMeasureOverride(new Vector2(float.PositiveInfinity, 50f));
        _ = panel.InvokeArrangeOverride(new Vector2(40f, 0f));

        panel.Orientation = Orientation.Horizontal;
        _ = panel.InvokeMeasureOverride(new Vector2(50f, float.NaN));
        _ = panel.InvokeArrangeOverride(new Vector2(float.PositiveInfinity, 20f));

        panel.Orientation = Orientation.Vertical;
        _ = panel.InvokeMeasureOverride(new Vector2(0f, 25f));
        _ = panel.InvokeArrangeOverride(new Vector2(10f, float.NaN));

        var snapshot = StackPanel.GetTelemetryAndReset();

        Assert.Equal(3, snapshot.MeasureCallCount);
        Assert.Equal(3, snapshot.ArrangeCallCount);
        Assert.Equal(0, snapshot.MeasuredChildCount);
        Assert.Equal(0, snapshot.ArrangedChildCount);
        Assert.Equal(0, snapshot.MeasureSkippedChildCount);
        Assert.Equal(0, snapshot.ArrangeSkippedChildCount);
        Assert.Equal(2, snapshot.MeasureVerticalCount);
        Assert.Equal(1, snapshot.MeasureHorizontalCount);
        Assert.Equal(2, snapshot.ArrangeVerticalCount);
        Assert.Equal(1, snapshot.ArrangeHorizontalCount);
        Assert.Equal(3, snapshot.MeasureEmptyCount);
        Assert.Equal(3, snapshot.ArrangeEmptyCount);
        Assert.Equal(1, snapshot.MeasureInfiniteCrossAxisCount);
        Assert.Equal(1, snapshot.MeasureNaNCrossAxisCount);
        Assert.Equal(1, snapshot.MeasureNonPositiveCrossAxisCount);
        Assert.Equal(1, snapshot.ArrangeInfinitePrimarySizeCount);
        Assert.Equal(1, snapshot.ArrangeNaNPrimarySizeCount);
        Assert.Equal(1, snapshot.ArrangeNonPositivePrimarySizeCount);
        Assert.Equal(0d, snapshot.MeasureTotalPrimaryDesired, 3);
        Assert.Equal(0d, snapshot.MeasureTotalCrossDesired, 3);
        Assert.Equal(0d, snapshot.ArrangeTotalPrimarySpan, 3);
        Assert.Equal(0d, snapshot.ArrangeTotalCrossSpan, 3);
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

    private sealed class TestStackPanel : StackPanel
    {
        public Vector2 InvokeMeasureOverride(Vector2 availableSize)
        {
            return base.MeasureOverride(availableSize);
        }

        public Vector2 InvokeArrangeOverride(Vector2 finalSize)
        {
            return base.ArrangeOverride(finalSize);
        }
    }
}