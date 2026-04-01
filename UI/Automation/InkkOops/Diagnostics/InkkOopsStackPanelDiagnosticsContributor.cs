using System.Collections.Generic;

namespace InkkSlinger;

public sealed class InkkOopsStackPanelDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 35;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not StackPanel stackPanel)
        {
            return;
        }

        var runtime = stackPanel.GetStackPanelSnapshotForDiagnostics();
        var telemetry = StackPanel.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("type", stackPanel.GetType().Name);
        builder.Add("orientation", runtime.Orientation);
        builder.Add("children", runtime.ChildCount);
        builder.Add("desired", $"{runtime.DesiredWidth:0.##},{runtime.DesiredHeight:0.##}");
        builder.Add("renderSize", $"{runtime.RenderWidth:0.##},{runtime.RenderHeight:0.##}");
        builder.Add("actual", $"{runtime.ActualWidth:0.##},{runtime.ActualHeight:0.##}");
        builder.Add("previousAvailable", $"{runtime.PreviousAvailableWidth:0.##},{runtime.PreviousAvailableHeight:0.##}");
        builder.Add("measureCalls", runtime.MeasureCallCount);
        builder.Add("measureWork", runtime.MeasureWorkCount);
        builder.Add("arrangeCalls", runtime.ArrangeCallCount);
        builder.Add("arrangeWork", runtime.ArrangeWorkCount);
        builder.Add("measureMs", $"{runtime.MeasureMilliseconds:0.###}");
        builder.Add("measureExclusiveMs", $"{runtime.MeasureExclusiveMilliseconds:0.###}");
        builder.Add("arrangeMs", $"{runtime.ArrangeMilliseconds:0.###}");
        builder.Add("measureValid", runtime.IsMeasureValid);
        builder.Add("arrangeValid", runtime.IsArrangeValid);
        builder.Add("childSummary", BuildChildSummary(stackPanel));
        builder.Add("stackPanelGlobalMeasureCalls", telemetry.MeasureCallCount);
        builder.Add("stackPanelGlobalMeasureMs", $"{telemetry.MeasureMilliseconds:0.###}");
        builder.Add("stackPanelGlobalMeasuredChildren", telemetry.MeasuredChildCount);
        builder.Add("stackPanelGlobalMeasureSkippedChildren", telemetry.MeasureSkippedChildCount);
        builder.Add("stackPanelGlobalMeasureVertical", telemetry.MeasureVerticalCount);
        builder.Add("stackPanelGlobalMeasureHorizontal", telemetry.MeasureHorizontalCount);
        builder.Add("stackPanelGlobalMeasureEmpty", telemetry.MeasureEmptyCount);
        builder.Add("stackPanelGlobalMeasureInfiniteCrossAxis", telemetry.MeasureInfiniteCrossAxisCount);
        builder.Add("stackPanelGlobalMeasureNaNCrossAxis", telemetry.MeasureNaNCrossAxisCount);
        builder.Add("stackPanelGlobalMeasureNonPositiveCrossAxis", telemetry.MeasureNonPositiveCrossAxisCount);
        builder.Add("stackPanelGlobalMeasurePrimaryDesired", $"{telemetry.MeasureTotalPrimaryDesired:0.###}");
        builder.Add("stackPanelGlobalMeasureCrossDesired", $"{telemetry.MeasureTotalCrossDesired:0.###}");
        builder.Add("stackPanelGlobalArrangeCalls", telemetry.ArrangeCallCount);
        builder.Add("stackPanelGlobalArrangeMs", $"{telemetry.ArrangeMilliseconds:0.###}");
        builder.Add("stackPanelGlobalArrangedChildren", telemetry.ArrangedChildCount);
        builder.Add("stackPanelGlobalArrangeSkippedChildren", telemetry.ArrangeSkippedChildCount);
        builder.Add("stackPanelGlobalArrangeVertical", telemetry.ArrangeVerticalCount);
        builder.Add("stackPanelGlobalArrangeHorizontal", telemetry.ArrangeHorizontalCount);
        builder.Add("stackPanelGlobalArrangeEmpty", telemetry.ArrangeEmptyCount);
        builder.Add("stackPanelGlobalArrangeInfinitePrimary", telemetry.ArrangeInfinitePrimarySizeCount);
        builder.Add("stackPanelGlobalArrangeNaNPrimary", telemetry.ArrangeNaNPrimarySizeCount);
        builder.Add("stackPanelGlobalArrangeNonPositivePrimary", telemetry.ArrangeNonPositivePrimarySizeCount);
        builder.Add("stackPanelGlobalArrangePrimarySpan", $"{telemetry.ArrangeTotalPrimarySpan:0.###}");
        builder.Add("stackPanelGlobalArrangeCrossSpan", $"{telemetry.ArrangeTotalCrossSpan:0.###}");
    }

    private static string BuildChildSummary(StackPanel stackPanel)
    {
        if (stackPanel.Children.Count == 0)
        {
            return "none";
        }

        var parts = new List<string>(stackPanel.Children.Count);
        for (var i = 0; i < stackPanel.Children.Count; i++)
        {
            var child = stackPanel.Children[i];
            if (child is FrameworkElement frameworkChild)
            {
                parts.Add($"{i}:{DescribeElement(child)}@slot={FormatRect(frameworkChild.LayoutSlot)} desired={frameworkChild.DesiredSize.X:0.##},{frameworkChild.DesiredSize.Y:0.##} actual={frameworkChild.ActualWidth:0.##},{frameworkChild.ActualHeight:0.##} prevAvail={frameworkChild.PreviousAvailableSizeForTests.X:0.##},{frameworkChild.PreviousAvailableSizeForTests.Y:0.##} mc={frameworkChild.MeasureCallCount}/{frameworkChild.MeasureWorkCount} ac={frameworkChild.ArrangeCallCount}/{frameworkChild.ArrangeWorkCount}");
            }
            else
            {
                parts.Add($"{i}:{DescribeElement(child)}");
            }
        }

        return string.Join(" | ", parts);
    }

    private static string DescribeElement(UIElement element)
    {
        return element is FrameworkElement frameworkElement && !string.IsNullOrWhiteSpace(frameworkElement.Name)
            ? $"{element.GetType().Name}#{frameworkElement.Name}"
            : element.GetType().Name;
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}";
    }

    private static string FormatMilliseconds(long ticks)
    {
        return ((double)ticks * 1000d / System.Diagnostics.Stopwatch.Frequency).ToString("0.###");
    }
}