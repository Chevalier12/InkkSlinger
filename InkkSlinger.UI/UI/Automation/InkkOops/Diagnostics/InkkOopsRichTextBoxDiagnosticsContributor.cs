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
        var performance = richTextBox.GetPerformanceSnapshot();

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
        builder.Add("runtimeContentHostViewportChangedNoMetricChange", runtime.ContentHostViewportChangedNoMetricChangeCount);
        builder.Add("runtimeContentHostViewportChangedVerticalOffsetChanged", runtime.ContentHostViewportChangedVerticalOffsetChangedCount);
        builder.Add("runtimeContentHostViewportChangedViewportHeightChanged", runtime.ContentHostViewportChangedViewportHeightChangedCount);
        builder.Add("runtimeContentHostViewportChangedExtentHeightChanged", runtime.ContentHostViewportChangedExtentHeightChangedCount);
        builder.Add("runtimeContentHostViewportChangedOnlyExtentHeightChanged", runtime.ContentHostViewportChangedOnlyExtentHeightChangedCount);
        builder.Add("runtimeContentHostViewportChangedOnlyViewportHeightChanged", runtime.ContentHostViewportChangedOnlyViewportHeightChangedCount);
        builder.Add("runtimeContentHostViewportChangedViewportAndExtentHeightChanged", runtime.ContentHostViewportChangedViewportAndExtentHeightChangedCount);
        builder.Add("runtimeContentHostViewportChangedMaxVerticalOffsetDelta", runtime.ContentHostViewportChangedMaxVerticalOffsetDelta.ToString("0.###"));
        builder.Add("runtimeContentHostViewportChangedMaxViewportHeightDelta", runtime.ContentHostViewportChangedMaxViewportHeightDelta.ToString("0.###"));
        builder.Add("runtimeContentHostViewportChangedMaxExtentHeightDelta", runtime.ContentHostViewportChangedMaxExtentHeightDelta.ToString("0.###"));
        builder.Add("runtimeLastContentHostViewportChangedMask", runtime.LastContentHostViewportChangedMask);
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
        builder.Add("runtimeQueueViewportChangedCalls", runtime.QueueViewportChangedNotificationCallCount);
        builder.Add("runtimeQueueViewportChangedAlreadyPending", runtime.QueueViewportChangedNotificationAlreadyPendingCount);
        builder.Add("runtimeFlushViewportChangedCalls", runtime.FlushPendingViewportChangedNotificationCallCount);
        builder.Add("runtimeFlushViewportChangedSkippedNoPending", runtime.FlushPendingViewportChangedNotificationSkippedNoPendingCount);
        builder.Add("runtimeFlushViewportChangedMs", FormatMilliseconds(runtime.FlushPendingViewportChangedNotificationMilliseconds));
        builder.Add("runtimeFlushViewportChangedNotifyMs", FormatMilliseconds(runtime.FlushPendingViewportChangedNotificationNotifyMilliseconds));
        builder.Add("runtimeHostedScrollContentMeasureCalls", runtime.HostedScrollContentMeasureCallCount);
        builder.Add("runtimeHostedScrollContentMeasureMs", FormatMilliseconds(runtime.HostedScrollContentMeasureMilliseconds));
        builder.Add("runtimeHostedScrollContentMeasureWidths", $"{runtime.LastHostedScrollContentMeasureAvailableWidth:0.###},{runtime.LastHostedScrollContentMeasureLayoutWidth:0.###},{runtime.LastHostedScrollContentMeasureDesiredWidth:0.###}");
        builder.Add("runtimeHostedScrollContentArrangeCalls", runtime.HostedScrollContentArrangeCallCount);
        builder.Add("runtimeHostedScrollContentArrangeMs", FormatMilliseconds(runtime.HostedScrollContentArrangeMilliseconds));
        builder.Add("runtimeHostedScrollContentArrangeSize", $"{runtime.LastHostedScrollContentArrangeWidth:0.###},{runtime.LastHostedScrollContentArrangeHeight:0.###}");
        builder.Add("runtimeCanReuseHostedContentMeasureCalls", runtime.CanReuseHostedContentMeasureCallCount);
        builder.Add("runtimeCanReuseHostedContentMeasureTrue", runtime.CanReuseHostedContentMeasureTrueCount);
        builder.Add("runtimeCanReuseHostedContentMeasureEquivalentWidthTrue", runtime.CanReuseHostedContentMeasureEquivalentWidthTrueCount);
        builder.Add("runtimeCanReuseHostedContentMeasureLayoutReuseTrue", runtime.CanReuseHostedContentMeasureLayoutReuseTrueCount);
        builder.Add("runtimeLastCanReuseHostedContentMeasureWidths", $"{runtime.LastCanReuseHostedContentMeasurePreviousWidth:0.###},{runtime.LastCanReuseHostedContentMeasureNextWidth:0.###},{runtime.LastCanReuseHostedContentMeasurePreviousLayoutWidth:0.###},{runtime.LastCanReuseHostedContentMeasureNextLayoutWidth:0.###}");
        builder.Add("runtimeLastCanReuseHostedContentMeasureResult", runtime.LastCanReuseHostedContentMeasureResult);
        builder.Add("runtimeLastCanReuseHostedContentMeasureEquivalentWidth", runtime.LastCanReuseHostedContentMeasureEquivalentWidth);
        builder.Add("runtimeNotifyViewportChangedCalls", runtime.NotifyViewportChangedCallCount);
        builder.Add("runtimeNotifyViewportChangedRaised", runtime.NotifyViewportChangedRaisedCount);
        builder.Add("runtimeNotifyViewportChangedSkippedNoChange", runtime.NotifyViewportChangedSkippedNoChangeCount);
        builder.Add("runtimeNotifyViewportChangedFromContentHost", runtime.NotifyViewportChangedFromContentHostCallCount);
        builder.Add("runtimeNotifyViewportChangedFromSetScrollOffsets", runtime.NotifyViewportChangedFromSetScrollOffsetsCallCount);
        builder.Add("runtimeNotifyViewportChangedFromPendingFlush", runtime.NotifyViewportChangedFromPendingFlushCallCount);
        builder.Add("runtimeNotifyViewportChangedRaisedFromContentHost", runtime.NotifyViewportChangedRaisedFromContentHostCount);
        builder.Add("runtimeNotifyViewportChangedRaisedFromSetScrollOffsets", runtime.NotifyViewportChangedRaisedFromSetScrollOffsetsCount);
        builder.Add("runtimeNotifyViewportChangedRaisedFromPendingFlush", runtime.NotifyViewportChangedRaisedFromPendingFlushCount);
        builder.Add("runtimeNotifyViewportChangedRaisedVerticalOffsetChanged", runtime.NotifyViewportChangedRaisedVerticalOffsetChangedCount);
        builder.Add("runtimeNotifyViewportChangedRaisedViewportHeightChanged", runtime.NotifyViewportChangedRaisedViewportHeightChangedCount);
        builder.Add("runtimeNotifyViewportChangedRaisedExtentHeightChanged", runtime.NotifyViewportChangedRaisedExtentHeightChangedCount);
        builder.Add("runtimeNotifyViewportChangedRaisedOnlyExtentHeightChanged", runtime.NotifyViewportChangedRaisedOnlyExtentHeightChangedCount);
        builder.Add("runtimeNotifyViewportChangedRaisedOnlyViewportHeightChanged", runtime.NotifyViewportChangedRaisedOnlyViewportHeightChangedCount);
        builder.Add("runtimeNotifyViewportChangedRaisedViewportAndExtentHeightChanged", runtime.NotifyViewportChangedRaisedViewportAndExtentHeightChangedCount);
        builder.Add("runtimeNotifyViewportChangedMaxVerticalOffsetDelta", runtime.NotifyViewportChangedMaxVerticalOffsetDelta.ToString("0.###"));
        builder.Add("runtimeNotifyViewportChangedMaxViewportHeightDelta", runtime.NotifyViewportChangedMaxViewportHeightDelta.ToString("0.###"));
        builder.Add("runtimeNotifyViewportChangedMaxExtentHeightDelta", runtime.NotifyViewportChangedMaxExtentHeightDelta.ToString("0.###"));
        builder.Add("runtimeLastNotifyViewportChangedSource", runtime.LastNotifyViewportChangedSource);
        builder.Add("runtimeLastNotifyViewportChangedMask", runtime.LastNotifyViewportChangedMask);
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
        builder.Add("telemetryQueueViewportChangedCalls", telemetry.QueueViewportChangedNotificationCallCount);
        builder.Add("telemetryQueueViewportChangedAlreadyPending", telemetry.QueueViewportChangedNotificationAlreadyPendingCount);
        builder.Add("telemetryFlushViewportChangedCalls", telemetry.FlushPendingViewportChangedNotificationCallCount);
        builder.Add("telemetryFlushViewportChangedSkippedNoPending", telemetry.FlushPendingViewportChangedNotificationSkippedNoPendingCount);
        builder.Add("telemetryFlushViewportChangedMs", FormatMilliseconds(telemetry.FlushPendingViewportChangedNotificationMilliseconds));
        builder.Add("telemetryFlushViewportChangedNotifyMs", FormatMilliseconds(telemetry.FlushPendingViewportChangedNotificationNotifyMilliseconds));
        builder.Add("telemetryHostedScrollContentMeasureCalls", telemetry.HostedScrollContentMeasureCallCount);
        builder.Add("telemetryHostedScrollContentMeasureMs", FormatMilliseconds(telemetry.HostedScrollContentMeasureMilliseconds));
        builder.Add("telemetryHostedScrollContentArrangeCalls", telemetry.HostedScrollContentArrangeCallCount);
        builder.Add("telemetryHostedScrollContentArrangeMs", FormatMilliseconds(telemetry.HostedScrollContentArrangeMilliseconds));
        builder.Add("telemetryCanReuseHostedContentMeasureCalls", telemetry.CanReuseHostedContentMeasureCallCount);
        builder.Add("telemetryCanReuseHostedContentMeasureTrue", telemetry.CanReuseHostedContentMeasureTrueCount);
        builder.Add("telemetryCanReuseHostedContentMeasureEquivalentWidthTrue", telemetry.CanReuseHostedContentMeasureEquivalentWidthTrueCount);
        builder.Add("telemetryCanReuseHostedContentMeasureLayoutReuseTrue", telemetry.CanReuseHostedContentMeasureLayoutReuseTrueCount);
        builder.Add("telemetryNotifyViewportChangedCalls", telemetry.NotifyViewportChangedCallCount);
        builder.Add("telemetryNotifyViewportChangedRaised", telemetry.NotifyViewportChangedRaisedCount);
        builder.Add("telemetryNotifyViewportChangedSkippedNoChange", telemetry.NotifyViewportChangedSkippedNoChangeCount);
        builder.Add("telemetryNotifyViewportChangedMs", FormatMilliseconds(telemetry.NotifyViewportChangedMilliseconds));
        builder.Add("telemetryNotifyViewportChangedSubscriberMs", FormatMilliseconds(telemetry.NotifyViewportChangedSubscriberMilliseconds));
        builder.Add("richTextBoxStructuredEnterCount", performance.StructuredEnterSampleCount);
        builder.Add("richTextBoxStructuredEnterParagraphEntriesMs", FormatMilliseconds(performance.LastStructuredEnterParagraphEntryCollectionMilliseconds));
        builder.Add("richTextBoxStructuredEnterCloneMs", FormatMilliseconds(performance.LastStructuredEnterCloneDocumentMilliseconds));
        builder.Add("richTextBoxStructuredEnterEnumerateMs", FormatMilliseconds(performance.LastStructuredEnterParagraphEnumerationMilliseconds));
        builder.Add("richTextBoxStructuredEnterPrepareMs", FormatMilliseconds(performance.LastStructuredEnterPrepareParagraphsMilliseconds));
        builder.Add("richTextBoxStructuredEnterCommitMs", FormatMilliseconds(performance.LastStructuredEnterCommitMilliseconds));
        builder.Add("richTextBoxStructuredEnterTotalMs", FormatMilliseconds(performance.LastStructuredEnterTotalMilliseconds));
        builder.Add("richTextBoxStructuredEnterReplacedDocument", performance.LastStructuredEnterUsedDocumentReplacement);
        builder.Add("richTextBoxStructuredEnterCommitMutationBatchMs", FormatMilliseconds(performance.LastStructuredEnterCommitMutationBatchMilliseconds));
        builder.Add("richTextBoxStructuredEnterCommitApplyOperationMs", FormatMilliseconds(performance.LastStructuredEnterCommitApplyOperationMilliseconds));
        builder.Add("richTextBoxStructuredEnterCommitTransactionMs", FormatMilliseconds(performance.LastStructuredEnterCommitTransactionMilliseconds));
        builder.Add("richTextBoxStructuredEnterCommitSelectionMs", FormatMilliseconds(performance.LastStructuredEnterCommitSelectionMilliseconds));
        builder.Add("richTextBoxStructuredEnterCommitTraceInvariantsMs", FormatMilliseconds(performance.LastStructuredEnterCommitTraceInvariantsMilliseconds));
        builder.Add("richTextBoxStructuredEnterCommitEnsureCaretVisibleMs", FormatMilliseconds(performance.LastStructuredEnterCommitEnsureCaretVisibleMilliseconds));
        builder.Add("richTextBoxStructuredEnterCommitInvalidateAfterMutationMs", FormatMilliseconds(performance.LastStructuredEnterCommitInvalidateAfterMutationMilliseconds));
        builder.Add("richTextBoxStructuredEnterCommitFlushPendingEventsMs", FormatMilliseconds(performance.LastStructuredEnterCommitFlushPendingEventsMilliseconds));
        builder.Add("richTextBoxStructuredEnterCommitFlushMaintenanceMs", FormatMilliseconds(performance.LastStructuredEnterCommitFlushMaintenanceMilliseconds));
        builder.Add("richTextBoxStructuredEnterCommitFlushDocumentChangedEventMs", FormatMilliseconds(performance.LastStructuredEnterCommitFlushDocumentChangedEventMilliseconds));
        builder.Add("richTextBoxStructuredEnterCommitFlushTextChangedEventMs", FormatMilliseconds(performance.LastStructuredEnterCommitFlushTextChangedEventMilliseconds));
        builder.Add("richTextBoxStructuredEnterCommitFlushInvalidateAfterDocumentChangeMs", FormatMilliseconds(performance.LastStructuredEnterCommitFlushInvalidateAfterDocumentChangeMilliseconds));
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }
}