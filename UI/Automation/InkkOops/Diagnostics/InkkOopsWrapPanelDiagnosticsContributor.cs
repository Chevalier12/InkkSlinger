using System.Collections.Generic;

namespace InkkSlinger;

public sealed class InkkOopsWrapPanelDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 36;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not WrapPanel wrapPanel)
        {
            return;
        }

        builder.Add("type", wrapPanel.GetType().Name);
        builder.Add("orientation", wrapPanel.Orientation);
        builder.Add("itemWidth", FormatFloat(wrapPanel.ItemWidth));
        builder.Add("itemHeight", FormatFloat(wrapPanel.ItemHeight));
        builder.Add("children", wrapPanel.Children.Count);
        builder.Add("desired", $"{wrapPanel.DesiredSize.X:0.##},{wrapPanel.DesiredSize.Y:0.##}");
        builder.Add("renderSize", $"{wrapPanel.RenderSize.X:0.##},{wrapPanel.RenderSize.Y:0.##}");
        builder.Add("actual", $"{wrapPanel.ActualWidth:0.##},{wrapPanel.ActualHeight:0.##}");
        builder.Add("previousAvailable", $"{wrapPanel.PreviousAvailableSizeForTests.X:0.##},{wrapPanel.PreviousAvailableSizeForTests.Y:0.##}");
        builder.Add("measureCalls", wrapPanel.MeasureCallCount);
        builder.Add("measureWork", wrapPanel.MeasureWorkCount);
        builder.Add("arrangeCalls", wrapPanel.ArrangeCallCount);
        builder.Add("arrangeWork", wrapPanel.ArrangeWorkCount);
        builder.Add("measureMs", FormatMilliseconds(wrapPanel.MeasureElapsedTicksForTests));
        builder.Add("measureExclusiveMs", FormatMilliseconds(wrapPanel.MeasureExclusiveElapsedTicksForTests));
        builder.Add("arrangeMs", FormatMilliseconds(wrapPanel.ArrangeElapsedTicksForTests));
        builder.Add("measureValid", wrapPanel.IsMeasureValidForTests);
        builder.Add("arrangeValid", wrapPanel.IsArrangeValidForTests);
        builder.Add("childSummary", BuildChildSummary(wrapPanel));

        var telemetry = WrapPanel.GetTelemetrySnapshotForDiagnostics();
        builder.Add("wrapPanelGlobalMeasureCalls", telemetry.MeasureCallCount);
        builder.Add("wrapPanelGlobalMeasureMs", $"{telemetry.MeasureMilliseconds:0.###}");
        builder.Add("wrapPanelGlobalMeasuredChildren", telemetry.MeasuredChildCount);
        builder.Add("wrapPanelGlobalMeasureWraps", telemetry.MeasureWrapCount);
        builder.Add("wrapPanelGlobalMeasureLines", telemetry.MeasureCommittedLineCount);
        builder.Add("wrapPanelGlobalArrangeCalls", telemetry.ArrangeCallCount);
        builder.Add("wrapPanelGlobalArrangeMs", $"{telemetry.ArrangeMilliseconds:0.###}");
        builder.Add("wrapPanelGlobalArrangedChildren", telemetry.ArrangedChildCount);
        builder.Add("wrapPanelGlobalArrangeWraps", telemetry.ArrangeWrapCount);
        builder.Add("wrapPanelGlobalArrangeLines", telemetry.ArrangeCommittedLineCount);
        builder.Add("wrapPanelGlobalMeasureUnlimitedLimits", telemetry.MeasureInfiniteLineLimitCount + telemetry.MeasureNaNLineLimitCount + telemetry.MeasureNonPositiveLineLimitCount);
        builder.Add("wrapPanelGlobalArrangeInvalidLimits", telemetry.ArrangeInfiniteLineLimitCount + telemetry.ArrangeNaNLineLimitCount + telemetry.ArrangeNonPositiveLineLimitCount);
        builder.Add("wrapPanelGlobalGetChildSizeCalls", telemetry.GetChildSizeCallCount);
        builder.Add("wrapPanelGlobalGetChildSizeMs", $"{telemetry.GetChildSizeMilliseconds:0.###}");
        builder.Add("wrapPanelGlobalExplicitWidthResolutions", telemetry.GetChildSizeExplicitWidthCount);
        builder.Add("wrapPanelGlobalDesiredWidthResolutions", telemetry.GetChildSizeDesiredWidthCount);
        builder.Add("wrapPanelGlobalExplicitHeightResolutions", telemetry.GetChildSizeExplicitHeightCount);
        builder.Add("wrapPanelGlobalDesiredHeightResolutions", telemetry.GetChildSizeDesiredHeightCount);
    }

    private static string BuildChildSummary(WrapPanel wrapPanel)
    {
        if (wrapPanel.Children.Count == 0)
        {
            return "none";
        }

        var parts = new List<string>(wrapPanel.Children.Count);
        for (var i = 0; i < wrapPanel.Children.Count; i++)
        {
            var child = wrapPanel.Children[i];
            if (child is FrameworkElement frameworkChild)
            {
                parts.Add($"{i}:{DescribeElement(child)}@slot={FormatRect(frameworkChild.LayoutSlot)} desired={frameworkChild.DesiredSize.X:0.##},{frameworkChild.DesiredSize.Y:0.##} actual={frameworkChild.ActualWidth:0.##},{frameworkChild.ActualHeight:0.##} mc={frameworkChild.MeasureCallCount}/{frameworkChild.MeasureWorkCount} ac={frameworkChild.ArrangeCallCount}/{frameworkChild.ArrangeWorkCount}");
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

    private static string FormatFloat(float value)
    {
        if (float.IsNaN(value))
        {
            return "NaN";
        }

        if (float.IsPositiveInfinity(value))
        {
            return "+Infinity";
        }

        if (float.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        return value.ToString("0.##");
    }
}