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

        builder.Add("type", stackPanel.GetType().Name);
        builder.Add("orientation", stackPanel.Orientation);
        builder.Add("children", stackPanel.Children.Count);
        builder.Add("desired", $"{stackPanel.DesiredSize.X:0.##},{stackPanel.DesiredSize.Y:0.##}");
        builder.Add("renderSize", $"{stackPanel.RenderSize.X:0.##},{stackPanel.RenderSize.Y:0.##}");
        builder.Add("actual", $"{stackPanel.ActualWidth:0.##},{stackPanel.ActualHeight:0.##}");
        builder.Add("previousAvailable", $"{stackPanel.PreviousAvailableSizeForTests.X:0.##},{stackPanel.PreviousAvailableSizeForTests.Y:0.##}");
        builder.Add("measureCalls", stackPanel.MeasureCallCount);
        builder.Add("measureWork", stackPanel.MeasureWorkCount);
        builder.Add("arrangeCalls", stackPanel.ArrangeCallCount);
        builder.Add("arrangeWork", stackPanel.ArrangeWorkCount);
        builder.Add("measureMs", FormatMilliseconds(stackPanel.MeasureElapsedTicksForTests));
        builder.Add("measureExclusiveMs", FormatMilliseconds(stackPanel.MeasureExclusiveElapsedTicksForTests));
        builder.Add("arrangeMs", FormatMilliseconds(stackPanel.ArrangeElapsedTicksForTests));
        builder.Add("measureValid", stackPanel.IsMeasureValidForTests);
        builder.Add("arrangeValid", stackPanel.IsArrangeValidForTests);
        builder.Add("childSummary", BuildChildSummary(stackPanel));

        var telemetry = StackPanel.GetTelemetrySnapshotForDiagnostics();
        builder.Add("stackPanelGlobalMeasureCalls", telemetry.MeasureCallCount);
        builder.Add("stackPanelGlobalMeasureMs", $"{telemetry.MeasureMilliseconds:0.###}");
        builder.Add("stackPanelGlobalMeasuredChildren", telemetry.MeasuredChildCount);
        builder.Add("stackPanelGlobalArrangeCalls", telemetry.ArrangeCallCount);
        builder.Add("stackPanelGlobalArrangeMs", $"{telemetry.ArrangeMilliseconds:0.###}");
        builder.Add("stackPanelGlobalArrangedChildren", telemetry.ArrangedChildCount);
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