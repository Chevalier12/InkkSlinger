using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class InkkOopsVirtualizingStackPanelDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 45;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        _ = context;

        if (element is not VirtualizingStackPanel panel)
        {
            return;
        }

        var runtime = panel.GetVirtualizingStackPanelSnapshotForDiagnostics();
        var telemetry = VirtualizingStackPanel.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("type", panel.GetType().Name);
        builder.Add("orientation", runtime.Orientation);
        builder.Add("isVirtualizing", runtime.IsVirtualizing);
        builder.Add("virtualizationMode", runtime.VirtualizationMode);
        builder.Add("cacheLength", $"{runtime.CacheLength:0.###}");
        builder.Add("cacheLengthUnit", runtime.CacheLengthUnit);
        builder.Add("virtualizationActive", runtime.IsVirtualizationActive);
        builder.Add("children", runtime.ChildCount);
        builder.Add("realizedRange", $"{runtime.FirstRealizedIndex}..{runtime.LastRealizedIndex}");
        builder.Add("realizedChildren", runtime.RealizedChildrenCount);
        builder.Add("realizedSpan", $"{runtime.RealizedStart:0.###},{runtime.RealizedEnd:0.###}");
        builder.Add("extent", $"{runtime.ExtentWidth:0.##},{runtime.ExtentHeight:0.##}");
        builder.Add("viewport", $"{runtime.ViewportWidth:0.##},{runtime.ViewportHeight:0.##}");
        builder.Add("offset", $"{runtime.HorizontalOffset:0.###},{runtime.VerticalOffset:0.###}");
        builder.Add("averagePrimarySize", $"{runtime.AveragePrimarySize:0.###}");
        builder.Add("maxSecondarySize", $"{runtime.MaxSecondarySize:0.###}");
        builder.Add("startOffsetsDirty", runtime.StartOffsetsDirty);
        builder.Add("relayoutQueuedFromOffset", runtime.RelayoutQueuedFromOffset);
        builder.Add("lastMeasuredRange", $"{runtime.LastMeasuredFirst}..{runtime.LastMeasuredLast}");
        builder.Add("lastArrangedRange", $"{runtime.LastArrangedFirst}..{runtime.LastArrangedLast}");
        builder.Add("hasArrangedRange", runtime.HasArrangedRange);
        builder.Add("lastViewportContext", $"viewport={runtime.LastViewportContextViewportPrimary:0.###} offset={runtime.LastViewportContextOffsetPrimary:0.###} start={runtime.LastViewportContextStartOffset:0.###} end={runtime.LastViewportContextEndOffset:0.###}");
        builder.Add("lastOffsetDecision", runtime.LastOffsetDecisionReason);
        builder.Add("lastOffsetDecisionWindow", $"{runtime.LastOffsetDecisionOldOffset:0.###}->{runtime.LastOffsetDecisionNewOffset:0.###}");
        builder.Add("lastOffsetDecisionViewport", $"{runtime.LastOffsetDecisionViewportPrimary:0.###}");
        builder.Add("lastOffsetDecisionRealizedSpan", $"{runtime.LastOffsetDecisionRealizedStart:0.###},{runtime.LastOffsetDecisionRealizedEnd:0.###}");
        builder.Add("lastOffsetDecisionGuardBand", $"{runtime.LastOffsetDecisionGuardBand:0.###}");
        builder.Add("runtimeMeasureOverrideCalls", runtime.MeasureOverrideCallCount);
        builder.Add("runtimeMeasureOverrideMs", $"{runtime.MeasureOverrideMilliseconds:0.###}");
        builder.Add("runtimeMeasureOverrideReusedRange", runtime.MeasureOverrideReusedRangeCount);
        builder.Add("runtimeMeasureAllChildrenCalls", runtime.MeasureAllChildrenCallCount);
        builder.Add("runtimeMeasureAllChildrenMs", $"{runtime.MeasureAllChildrenMilliseconds:0.###}");
        builder.Add("runtimeMeasureRangeCalls", runtime.MeasureRangeCallCount);
        builder.Add("runtimeMeasureRangeMs", $"{runtime.MeasureRangeMilliseconds:0.###}");
        builder.Add("runtimeArrangeOverrideCalls", runtime.ArrangeOverrideCallCount);
        builder.Add("runtimeArrangeOverrideMs", $"{runtime.ArrangeOverrideMilliseconds:0.###}");
        builder.Add("runtimeArrangeOverrideReusedRange", runtime.ArrangeOverrideReusedRangeCount);
        builder.Add("runtimeArrangeRangeCalls", runtime.ArrangeRangeCallCount);
        builder.Add("runtimeArrangeRangeMs", $"{runtime.ArrangeRangeMilliseconds:0.###}");
        builder.Add("runtimeCanReuseMeasureCalls", runtime.CanReuseMeasureForAvailableSizeChangeCallCount);
        builder.Add("runtimeCanReuseMeasureTrue", runtime.CanReuseMeasureForAvailableSizeChangeTrueCount);
        builder.Add("runtimeResolveViewportContextCalls", runtime.ResolveViewportContextCallCount);
        builder.Add("runtimeViewerOwnedOffsetDecisionCalls", runtime.ViewerOwnedOffsetDecisionCallCount);
        builder.Add("runtimeViewerOwnedOffsetDecisionRequireMeasure", runtime.ViewerOwnedOffsetDecisionRequireMeasureCount);
        builder.Add("runtimeViewerOwnedOffsetDecisionOrientationMismatch", runtime.ViewerOwnedOffsetDecisionOrientationMismatchCount);
        builder.Add("runtimeViewerOwnedOffsetDecisionInactiveOrNoChildrenOrNoDelta", runtime.ViewerOwnedOffsetDecisionInactiveOrNoChildrenOrNoDeltaCount);
        builder.Add("runtimeViewerOwnedOffsetDecisionNonFiniteViewport", runtime.ViewerOwnedOffsetDecisionNonFiniteViewportCount);
        builder.Add("runtimeViewerOwnedOffsetDecisionMissingRealizedRange", runtime.ViewerOwnedOffsetDecisionMissingRealizedRangeCount);
        builder.Add("runtimeViewerOwnedOffsetDecisionBeforeGuardBand", runtime.ViewerOwnedOffsetDecisionBeforeGuardBandCount);
        builder.Add("runtimeViewerOwnedOffsetDecisionAfterGuardBand", runtime.ViewerOwnedOffsetDecisionAfterGuardBandCount);
        builder.Add("runtimeViewerOwnedOffsetDecisionWithinRealizedWindow", runtime.ViewerOwnedOffsetDecisionWithinRealizedWindowCount);
        builder.Add("runtimeSetHorizontalOffsetCalls", runtime.SetHorizontalOffsetCallCount);
        builder.Add("runtimeSetHorizontalOffsetNoOp", runtime.SetHorizontalOffsetNoOpCount);
        builder.Add("runtimeSetHorizontalOffsetRelayout", runtime.SetHorizontalOffsetRelayoutCount);
        builder.Add("runtimeSetHorizontalOffsetVisualOnly", runtime.SetHorizontalOffsetVisualOnlyCount);
        builder.Add("runtimeSetVerticalOffsetCalls", runtime.SetVerticalOffsetCallCount);
        builder.Add("runtimeSetVerticalOffsetNoOp", runtime.SetVerticalOffsetNoOpCount);
        builder.Add("runtimeSetVerticalOffsetRelayout", runtime.SetVerticalOffsetRelayoutCount);
        builder.Add("runtimeSetVerticalOffsetVisualOnly", runtime.SetVerticalOffsetVisualOnlyCount);
        builder.Add("realizedChildSummary", BuildRealizedChildSummary(panel));

        builder.Add("telemetryMeasureOverrideCalls", telemetry.MeasureOverrideCallCount);
        builder.Add("telemetryMeasureOverrideMs", $"{telemetry.MeasureOverrideMilliseconds:0.###}");
        builder.Add("telemetryMeasureOverrideReusedRange", telemetry.MeasureOverrideReusedRangeCount);
        builder.Add("telemetryMeasureAllChildrenCalls", telemetry.MeasureAllChildrenCallCount);
        builder.Add("telemetryMeasureAllChildrenMs", $"{telemetry.MeasureAllChildrenMilliseconds:0.###}");
        builder.Add("telemetryMeasureRangeCalls", telemetry.MeasureRangeCallCount);
        builder.Add("telemetryMeasureRangeMs", $"{telemetry.MeasureRangeMilliseconds:0.###}");
        builder.Add("telemetryArrangeOverrideCalls", telemetry.ArrangeOverrideCallCount);
        builder.Add("telemetryArrangeOverrideMs", $"{telemetry.ArrangeOverrideMilliseconds:0.###}");
        builder.Add("telemetryArrangeOverrideReusedRange", telemetry.ArrangeOverrideReusedRangeCount);
        builder.Add("telemetryArrangeRangeCalls", telemetry.ArrangeRangeCallCount);
        builder.Add("telemetryArrangeRangeMs", $"{telemetry.ArrangeRangeMilliseconds:0.###}");
        builder.Add("telemetryCanReuseMeasureCalls", telemetry.CanReuseMeasureForAvailableSizeChangeCallCount);
        builder.Add("telemetryCanReuseMeasureTrue", telemetry.CanReuseMeasureForAvailableSizeChangeTrueCount);
        builder.Add("telemetryResolveViewportContextCalls", telemetry.ResolveViewportContextCallCount);
        builder.Add("telemetryViewerOwnedOffsetDecisionCalls", telemetry.ViewerOwnedOffsetDecisionCallCount);
        builder.Add("telemetryViewerOwnedOffsetDecisionRequireMeasure", telemetry.ViewerOwnedOffsetDecisionRequireMeasureCount);
        builder.Add("telemetryViewerOwnedOffsetDecisionOrientationMismatch", telemetry.ViewerOwnedOffsetDecisionOrientationMismatchCount);
        builder.Add("telemetryViewerOwnedOffsetDecisionInactiveOrNoChildrenOrNoDelta", telemetry.ViewerOwnedOffsetDecisionInactiveOrNoChildrenOrNoDeltaCount);
        builder.Add("telemetryViewerOwnedOffsetDecisionNonFiniteViewport", telemetry.ViewerOwnedOffsetDecisionNonFiniteViewportCount);
        builder.Add("telemetryViewerOwnedOffsetDecisionMissingRealizedRange", telemetry.ViewerOwnedOffsetDecisionMissingRealizedRangeCount);
        builder.Add("telemetryViewerOwnedOffsetDecisionBeforeGuardBand", telemetry.ViewerOwnedOffsetDecisionBeforeGuardBandCount);
        builder.Add("telemetryViewerOwnedOffsetDecisionAfterGuardBand", telemetry.ViewerOwnedOffsetDecisionAfterGuardBandCount);
        builder.Add("telemetryViewerOwnedOffsetDecisionWithinRealizedWindow", telemetry.ViewerOwnedOffsetDecisionWithinRealizedWindowCount);
        builder.Add("telemetrySetHorizontalOffsetCalls", telemetry.SetHorizontalOffsetCallCount);
        builder.Add("telemetrySetHorizontalOffsetNoOp", telemetry.SetHorizontalOffsetNoOpCount);
        builder.Add("telemetrySetHorizontalOffsetRelayout", telemetry.SetHorizontalOffsetRelayoutCount);
        builder.Add("telemetrySetHorizontalOffsetVisualOnly", telemetry.SetHorizontalOffsetVisualOnlyCount);
        builder.Add("telemetrySetVerticalOffsetCalls", telemetry.SetVerticalOffsetCallCount);
        builder.Add("telemetrySetVerticalOffsetNoOp", telemetry.SetVerticalOffsetNoOpCount);
        builder.Add("telemetrySetVerticalOffsetRelayout", telemetry.SetVerticalOffsetRelayoutCount);
        builder.Add("telemetrySetVerticalOffsetVisualOnly", telemetry.SetVerticalOffsetVisualOnlyCount);
    }

    private static string BuildRealizedChildSummary(VirtualizingStackPanel panel)
    {
        if (panel.Children.Count == 0 || panel.FirstRealizedIndex < 0 || panel.LastRealizedIndex < panel.FirstRealizedIndex)
        {
            return "none";
        }

        var parts = new List<string>();
        var first = Math.Max(0, panel.FirstRealizedIndex);
        var last = Math.Min(panel.Children.Count - 1, panel.LastRealizedIndex);
        for (var index = first; index <= last; index++)
        {
            var child = panel.Children[index];
            if (child is FrameworkElement frameworkChild)
            {
                parts.Add($"{index}:{DescribeElement(child)} slot={FormatRect(frameworkChild.LayoutSlot)} desired={frameworkChild.DesiredSize.X:0.##},{frameworkChild.DesiredSize.Y:0.##} actual={frameworkChild.ActualWidth:0.##},{frameworkChild.ActualHeight:0.##} mc={frameworkChild.MeasureCallCount}/{frameworkChild.MeasureWorkCount} ac={frameworkChild.ArrangeCallCount}/{frameworkChild.ArrangeWorkCount}");
            }
            else
            {
                parts.Add($"{index}:{DescribeElement(child)}");
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
}