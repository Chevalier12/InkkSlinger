using System;

namespace InkkSlinger;

public sealed class InkkOopsRichTextBoxDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 40;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        _ = context;
        if (element is not RichTextBox richTextBox)
        {
            return;
        }

        var runtime = richTextBox.GetRichTextBoxSnapshotForDiagnostics();
        var telemetry = RichTextBox.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("richTextBoxViewport", $"{runtime.HorizontalOffset:0.###},{runtime.VerticalOffset:0.###},{runtime.ViewportWidth:0.##},{runtime.ViewportHeight:0.##}");
        builder.Add("richTextBoxExtent", $"{runtime.ExtentWidth:0.##},{runtime.ExtentHeight:0.##}");
        builder.Add("richTextBoxScrollable", $"{MathF.Max(0f, runtime.ExtentWidth - runtime.ViewportWidth):0.##},{MathF.Max(0f, runtime.ExtentHeight - runtime.ViewportHeight):0.##}");
        builder.Add("richTextBoxHasContentHost", runtime.HasContentHost);
        builder.Add("richTextBoxHasUsableContentHostMetrics", runtime.HasUsableContentHostMetrics);
        builder.Add("richTextBoxPendingContentHostScrollOffsets", runtime.HasPendingContentHostScrollOffsets);
        builder.Add("richTextBoxHostedDocumentChildren", runtime.HostedDocumentVisualChildCount);
        builder.Add("runtimeContentHostViewportChangedCalls", runtime.ContentHostViewportChangedCallCount);
        builder.Add("runtimeContentHostViewportChangedMs", FormatMilliseconds(runtime.ContentHostViewportChangedMilliseconds));
        builder.Add("runtimeContentHostViewportChangedApplyPendingMs", FormatMilliseconds(runtime.ContentHostViewportChangedApplyPendingMilliseconds));
        builder.Add("runtimeContentHostViewportChangedEnsureHostedLayoutMs", FormatMilliseconds(runtime.ContentHostViewportChangedEnsureHostedLayoutMilliseconds));
        builder.Add("runtimeContentHostViewportChangedNotifyViewportMs", FormatMilliseconds(runtime.ContentHostViewportChangedNotifyViewportMilliseconds));
        builder.Add("runtimeApplyPendingContentHostOffsetsCalls", runtime.ApplyPendingContentHostScrollOffsetsCallCount);
        builder.Add("runtimeApplyPendingContentHostOffsetsApplied", runtime.ApplyPendingContentHostScrollOffsetsAppliedCount);
        builder.Add("runtimeApplyPendingContentHostOffsetsSkippedNoContentHost", runtime.ApplyPendingContentHostScrollOffsetsSkippedNoContentHostCount);
        builder.Add("runtimeApplyPendingContentHostOffsetsSkippedNoMetrics", runtime.ApplyPendingContentHostScrollOffsetsSkippedNoMetricsCount);
        builder.Add("runtimeApplyPendingContentHostOffsetsSkippedNoPending", runtime.ApplyPendingContentHostScrollOffsetsSkippedNoPendingCount);
        builder.Add("runtimeApplyPendingContentHostOffsetsMs", FormatMilliseconds(runtime.ApplyPendingContentHostScrollOffsetsMilliseconds));
        builder.Add("runtimeEnsureHostedDocumentChildLayoutCalls", runtime.EnsureHostedDocumentChildLayoutCallCount);
        builder.Add("runtimeEnsureHostedDocumentChildLayoutSkippedZeroTextRect", runtime.EnsureHostedDocumentChildLayoutSkippedZeroTextRectCount);
        builder.Add("runtimeEnsureHostedDocumentChildLayoutSkippedNoHostedChildren", runtime.EnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount);
        builder.Add("runtimeEnsureHostedDocumentChildLayoutMs", FormatMilliseconds(runtime.EnsureHostedDocumentChildLayoutMilliseconds));
        builder.Add("runtimeNotifyViewportChangedCalls", runtime.NotifyViewportChangedCallCount);
        builder.Add("runtimeNotifyViewportChangedRaised", runtime.NotifyViewportChangedRaisedCount);
        builder.Add("runtimeNotifyViewportChangedSkippedNoChange", runtime.NotifyViewportChangedSkippedNoChangeCount);
        builder.Add("runtimeNotifyViewportChangedMs", FormatMilliseconds(runtime.NotifyViewportChangedMilliseconds));
        builder.Add("runtimeNotifyViewportChangedSubscriberMs", FormatMilliseconds(runtime.NotifyViewportChangedSubscriberMilliseconds));

        builder.Add("telemetryContentHostViewportChangedCalls", telemetry.ContentHostViewportChangedCallCount);
        builder.Add("telemetryContentHostViewportChangedMs", FormatMilliseconds(telemetry.ContentHostViewportChangedMilliseconds));
        builder.Add("telemetryContentHostViewportChangedApplyPendingMs", FormatMilliseconds(telemetry.ContentHostViewportChangedApplyPendingMilliseconds));
        builder.Add("telemetryContentHostViewportChangedEnsureHostedLayoutMs", FormatMilliseconds(telemetry.ContentHostViewportChangedEnsureHostedLayoutMilliseconds));
        builder.Add("telemetryContentHostViewportChangedNotifyViewportMs", FormatMilliseconds(telemetry.ContentHostViewportChangedNotifyViewportMilliseconds));
        builder.Add("telemetryApplyPendingContentHostOffsetsCalls", telemetry.ApplyPendingContentHostScrollOffsetsCallCount);
        builder.Add("telemetryApplyPendingContentHostOffsetsApplied", telemetry.ApplyPendingContentHostScrollOffsetsAppliedCount);
        builder.Add("telemetryApplyPendingContentHostOffsetsSkippedNoContentHost", telemetry.ApplyPendingContentHostScrollOffsetsSkippedNoContentHostCount);
        builder.Add("telemetryApplyPendingContentHostOffsetsSkippedNoMetrics", telemetry.ApplyPendingContentHostScrollOffsetsSkippedNoMetricsCount);
        builder.Add("telemetryApplyPendingContentHostOffsetsSkippedNoPending", telemetry.ApplyPendingContentHostScrollOffsetsSkippedNoPendingCount);
        builder.Add("telemetryApplyPendingContentHostOffsetsMs", FormatMilliseconds(telemetry.ApplyPendingContentHostScrollOffsetsMilliseconds));
        builder.Add("telemetryEnsureHostedDocumentChildLayoutCalls", telemetry.EnsureHostedDocumentChildLayoutCallCount);
        builder.Add("telemetryEnsureHostedDocumentChildLayoutSkippedZeroTextRect", telemetry.EnsureHostedDocumentChildLayoutSkippedZeroTextRectCount);
        builder.Add("telemetryEnsureHostedDocumentChildLayoutSkippedNoHostedChildren", telemetry.EnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount);
        builder.Add("telemetryEnsureHostedDocumentChildLayoutMs", FormatMilliseconds(telemetry.EnsureHostedDocumentChildLayoutMilliseconds));
        builder.Add("telemetryNotifyViewportChangedCalls", telemetry.NotifyViewportChangedCallCount);
        builder.Add("telemetryNotifyViewportChangedRaised", telemetry.NotifyViewportChangedRaisedCount);
        builder.Add("telemetryNotifyViewportChangedSkippedNoChange", telemetry.NotifyViewportChangedSkippedNoChangeCount);
        builder.Add("telemetryNotifyViewportChangedMs", FormatMilliseconds(telemetry.NotifyViewportChangedMilliseconds));
        builder.Add("telemetryNotifyViewportChangedSubscriberMs", FormatMilliseconds(telemetry.NotifyViewportChangedSubscriberMilliseconds));
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }
}