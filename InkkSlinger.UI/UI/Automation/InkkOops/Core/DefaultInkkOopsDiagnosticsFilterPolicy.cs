using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsFilterPolicy : IInkkOopsDiagnosticsFilterPolicy
{
    private static readonly int[] TargetedActionIndexes =
    [
        117,
        122,
        123,
    ];

    private static readonly InkkOopsDiagnosticsFilter TargetedActionFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules = CreateEditorEnterRules()
    };

    public InkkOopsDiagnosticsFilter CreateFilter(string artifactName)
    {
        if (!TryGetActionIndex(artifactName, out var actionIndex))
        {
            return InkkOopsDiagnosticsFilter.None;
        }

        return Array.IndexOf(TargetedActionIndexes, actionIndex) >= 0
            ? TargetedActionFilter
            : InkkOopsDiagnosticsFilter.None;
    }

    private static InkkOopsDiagnosticsFactRule[] CreateEditorEnterRules()
    {
        var rules = new List<InkkOopsDiagnosticsFactRule>();

        AddNamedRules(
            rules,
            "SourceEditorPane",
            "name",
            "focused",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "userControlHasTemplateRoot",
            "userControlHasContentElement",
            "userControlContentElementType",
            "userControlRuntimeMeasureOverrideCalls",
            "userControlRuntimeMeasureOverrideMs",
            "userControlRuntimeArrangeOverrideCalls",
            "userControlRuntimeArrangeOverrideMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkUpdateLayoutPasses");

        AddNamedRules(
            rules,
            "SourceEditor",
            "name",
            "focused",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureValid",
            "arrangeValid",
            "controlHasTemplateRoot",
            "controlTemplateRootType",
            "controlRuntimeMeasureOverrideCalls",
            "controlRuntimeMeasureOverrideMs",
            "controlRuntimeArrangeOverrideCalls",
            "controlRuntimeArrangeOverrideMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateMeasureCalls",
            "frameworkInvalidateArrangeCalls",
            "frameworkInvalidateVisualCalls",
            "frameworkUpdateLayoutPasses",
            "ideEditorViewport",
            "ideEditorExtent",
            "ideEditorLastRenderedRange",
            "runtimeIdeEditorGutterCalls",
            "runtimeIdeEditorGutterForced",
            "runtimeIdeEditorGutterApplied",
            "runtimeIdeEditorGutterNoOp",
            "runtimeIdeEditorGutterVisibleLines",
            "runtimeIdeEditorGutterMs",
            "runtimeIdeEditorTextChanged",
            "runtimeIdeEditorTextChangedMs",
            "runtimeIdeEditorTextChangedCachedLineCountMs",
            "runtimeIdeEditorTextChangedGutterMs",
            "runtimeIdeEditorTextChangedLineNumberUpdateMs",
            "runtimeIdeEditorTextChangedLineNumberMeasureMs",
            "runtimeIdeEditorTextChangedLineNumberArrangeMs",
            "runtimeIdeEditorTextChangedIndentInvalidateMs",
            "runtimeIdeEditorTextChangedSubscriberMs",
            "runtimeIdeEditorLastTextChangedMs",
            "runtimeIdeEditorLastTextChangedCachedLineCountMs",
            "runtimeIdeEditorLastTextChangedGutterMs",
            "runtimeIdeEditorLastTextChangedLineNumberUpdateMs",
            "runtimeIdeEditorLastTextChangedLineNumberMeasureMs",
            "runtimeIdeEditorLastTextChangedLineNumberArrangeMs",
            "runtimeIdeEditorLastTextChangedIndentInvalidateMs",
            "runtimeIdeEditorLastTextChangedSubscriberMs",
            "runtimeIdeEditorDocumentChanged",
            "runtimeIdeEditorViewportChanged",
            "runtimeIdeEditorLayoutUpdated",
            "runtimeIdeEditorIndentInvalidate",
            "runtimeIdeEditorIndentSnapshotCalls",
            "runtimeIdeEditorIndentSnapshotSuccess",
            "runtimeIdeEditorIndentSnapshotSegments",
            "runtimeIdeEditorIndentSnapshotMs",
            "telemetryIdeEditorGutterCalls",
            "telemetryIdeEditorGutterForced",
            "telemetryIdeEditorGutterApplied",
            "telemetryIdeEditorGutterNoOp",
            "telemetryIdeEditorGutterVisibleLines",
            "telemetryIdeEditorGutterMs",
            "telemetryIdeEditorTextChanged",
            "telemetryIdeEditorTextChangedMs",
            "telemetryIdeEditorTextChangedCachedLineCountMs",
            "telemetryIdeEditorTextChangedGutterMs",
            "telemetryIdeEditorTextChangedLineNumberUpdateMs",
            "telemetryIdeEditorTextChangedLineNumberMeasureMs",
            "telemetryIdeEditorTextChangedLineNumberArrangeMs",
            "telemetryIdeEditorTextChangedIndentInvalidateMs",
            "telemetryIdeEditorTextChangedSubscriberMs",
            "telemetryIdeEditorDocumentChanged",
            "telemetryIdeEditorViewportChanged",
            "telemetryIdeEditorLayoutUpdated",
            "telemetryIdeEditorIndentInvalidate",
            "telemetryIdeEditorIndentSnapshotCalls",
            "telemetryIdeEditorIndentSnapshotSuccess",
            "telemetryIdeEditorIndentSnapshotSegments",
            "telemetryIdeEditorIndentSnapshotMs");

        AddNamedRules(
            rules,
            "PART_Editor",
            "name",
            "focused",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateMeasureCalls",
            "frameworkInvalidateArrangeCalls",
            "frameworkInvalidateVisualCalls",
            "frameworkUpdateLayoutPasses",
            "richTextBoxViewport",
            "richTextBoxExtent",
            "richTextBoxScrollable",
            "richTextBoxHasContentHost",
            "richTextBoxHasUsableContentHostMetrics",
            "richTextBoxPendingContentHostScrollOffsets",
            "richTextBoxHostedDocumentChildren",
            "runtimeContentHostViewportChangedCalls",
            "runtimeContentHostViewportChangedMs",
            "runtimeContentHostViewportChangedApplyPendingMs",
            "runtimeContentHostViewportChangedEnsureHostedLayoutMs",
            "runtimeContentHostViewportChangedNotifyViewportMs",
            "runtimeContentHostViewportChangedNoMetricChange",
            "runtimeContentHostViewportChangedVerticalOffsetChanged",
            "runtimeContentHostViewportChangedViewportHeightChanged",
            "runtimeContentHostViewportChangedExtentHeightChanged",
            "runtimeContentHostViewportChangedOnlyExtentHeightChanged",
            "runtimeContentHostViewportChangedOnlyViewportHeightChanged",
            "runtimeContentHostViewportChangedViewportAndExtentHeightChanged",
            "runtimeContentHostViewportChangedMaxVerticalOffsetDelta",
            "runtimeContentHostViewportChangedMaxViewportHeightDelta",
            "runtimeContentHostViewportChangedMaxExtentHeightDelta",
            "runtimeLastContentHostViewportChangedMask",
            "runtimeApplyPendingContentHostOffsetsCalls",
            "runtimeApplyPendingContentHostOffsetsApplied",
            "runtimeApplyPendingContentHostOffsetsSkippedNoContentHost",
            "runtimeApplyPendingContentHostOffsetsSkippedNoMetrics",
            "runtimeApplyPendingContentHostOffsetsSkippedNoPending",
            "runtimeApplyPendingContentHostOffsetsMs",
            "runtimeEnsureHostedDocumentChildLayoutCalls",
            "runtimeEnsureHostedDocumentChildLayoutSkippedZeroTextRect",
            "runtimeEnsureHostedDocumentChildLayoutSkippedNoHostedChildren",
            "runtimeEnsureHostedDocumentChildLayoutMs",
            "runtimeQueueViewportChangedCalls",
            "runtimeQueueViewportChangedAlreadyPending",
            "runtimeFlushViewportChangedCalls",
            "runtimeFlushViewportChangedSkippedNoPending",
            "runtimeFlushViewportChangedMs",
            "runtimeFlushViewportChangedNotifyMs",
            "runtimeHostedScrollContentMeasureCalls",
            "runtimeHostedScrollContentMeasureMs",
            "runtimeHostedScrollContentMeasureWidths",
            "runtimeHostedScrollContentArrangeCalls",
            "runtimeHostedScrollContentArrangeMs",
            "runtimeHostedScrollContentArrangeSize",
            "runtimeCanReuseHostedContentMeasureCalls",
            "runtimeCanReuseHostedContentMeasureTrue",
            "runtimeCanReuseHostedContentMeasureEquivalentWidthTrue",
            "runtimeCanReuseHostedContentMeasureLayoutReuseTrue",
            "runtimeLastCanReuseHostedContentMeasureWidths",
            "runtimeLastCanReuseHostedContentMeasureResult",
            "runtimeLastCanReuseHostedContentMeasureEquivalentWidth",
            "runtimeNotifyViewportChangedCalls",
            "runtimeNotifyViewportChangedRaised",
            "runtimeNotifyViewportChangedSkippedNoChange",
            "runtimeNotifyViewportChangedFromContentHost",
            "runtimeNotifyViewportChangedFromSetScrollOffsets",
            "runtimeNotifyViewportChangedFromPendingFlush",
            "runtimeNotifyViewportChangedRaisedFromContentHost",
            "runtimeNotifyViewportChangedRaisedFromSetScrollOffsets",
            "runtimeNotifyViewportChangedRaisedFromPendingFlush",
            "runtimeNotifyViewportChangedRaisedVerticalOffsetChanged",
            "runtimeNotifyViewportChangedRaisedViewportHeightChanged",
            "runtimeNotifyViewportChangedRaisedExtentHeightChanged",
            "runtimeNotifyViewportChangedRaisedOnlyExtentHeightChanged",
            "runtimeNotifyViewportChangedRaisedOnlyViewportHeightChanged",
            "runtimeNotifyViewportChangedRaisedViewportAndExtentHeightChanged",
            "runtimeNotifyViewportChangedMaxVerticalOffsetDelta",
            "runtimeNotifyViewportChangedMaxViewportHeightDelta",
            "runtimeNotifyViewportChangedMaxExtentHeightDelta",
            "runtimeLastNotifyViewportChangedSource",
            "runtimeLastNotifyViewportChangedMask",
            "runtimeNotifyViewportChangedMs",
            "runtimeNotifyViewportChangedSubscriberMs",
            "telemetryContentHostViewportChangedCalls",
            "telemetryContentHostViewportChangedMs",
            "telemetryApplyPendingContentHostOffsetsCalls",
            "telemetryApplyPendingContentHostOffsetsMs",
            "telemetryEnsureHostedDocumentChildLayoutCalls",
            "telemetryEnsureHostedDocumentChildLayoutMs",
            "telemetryQueueViewportChangedCalls",
            "telemetryQueueViewportChangedAlreadyPending",
            "telemetryFlushViewportChangedCalls",
            "telemetryFlushViewportChangedSkippedNoPending",
            "telemetryFlushViewportChangedMs",
            "telemetryFlushViewportChangedNotifyMs",
            "telemetryHostedScrollContentMeasureCalls",
            "telemetryHostedScrollContentMeasureMs",
            "telemetryHostedScrollContentArrangeCalls",
            "telemetryHostedScrollContentArrangeMs",
            "telemetryCanReuseHostedContentMeasureCalls",
            "telemetryCanReuseHostedContentMeasureTrue",
            "telemetryCanReuseHostedContentMeasureEquivalentWidthTrue",
            "telemetryCanReuseHostedContentMeasureLayoutReuseTrue",
            "telemetryNotifyViewportChangedCalls",
            "telemetryNotifyViewportChangedMs",
            "telemetryNotifyViewportChangedSubscriberMs",
            "richTextBoxStructuredEnterCount",
            "richTextBoxStructuredEnterParagraphEntriesMs",
            "richTextBoxStructuredEnterCloneMs",
            "richTextBoxStructuredEnterEnumerateMs",
            "richTextBoxStructuredEnterPrepareMs",
            "richTextBoxStructuredEnterCommitMs",
            "richTextBoxStructuredEnterTotalMs",
            "richTextBoxStructuredEnterReplacedDocument",
            "richTextBoxStructuredEnterCommitMutationBatchMs",
            "richTextBoxStructuredEnterCommitApplyOperationMs",
            "richTextBoxStructuredEnterCommitTransactionMs",
            "richTextBoxStructuredEnterCommitSelectionMs",
            "richTextBoxStructuredEnterCommitTraceInvariantsMs",
            "richTextBoxStructuredEnterCommitEnsureCaretVisibleMs",
            "richTextBoxStructuredEnterCommitInvalidateAfterMutationMs",
            "richTextBoxStructuredEnterCommitFlushPendingEventsMs",
            "richTextBoxStructuredEnterCommitFlushMaintenanceMs",
            "richTextBoxStructuredEnterCommitFlushDocumentChangedEventMs",
            "richTextBoxStructuredEnterCommitFlushTextChangedEventMs",
            "richTextBoxStructuredEnterCommitFlushInvalidateAfterDocumentChangeMs");

        AddNamedRules(
            rules,
            "PART_LineNumberPresenter",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureCalls",
            "measureWork",
            "arrangeCalls",
            "arrangeWork",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "ideLineNumberRange",
            "ideLineNumberMetrics",
            "runtimeIdeLineNumberUpdateCalls",
            "runtimeIdeLineNumberUpdateChanged",
            "runtimeIdeLineNumberTextUpdates",
            "runtimeIdeLineNumberVisibleLines",
            "runtimeIdeLineNumberUpdateMs",
            "runtimeIdeLineNumberMeasureCalls",
            "runtimeIdeLineNumberMeasureMs",
            "runtimeIdeLineNumberArrangeCalls",
            "runtimeIdeLineNumberArrangeMs",
            "telemetryIdeLineNumberUpdateCalls",
            "telemetryIdeLineNumberUpdateChanged",
            "telemetryIdeLineNumberTextUpdates",
            "telemetryIdeLineNumberVisibleLines",
            "telemetryIdeLineNumberUpdateMs",
            "telemetryIdeLineNumberMeasureCalls",
            "telemetryIdeLineNumberMeasureMs",
            "telemetryIdeLineNumberArrangeCalls",
            "telemetryIdeLineNumberArrangeMs");

        AddNamedRules(
            rules,
            "PART_IndentGuideOverlay",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureCalls",
            "measureWork",
            "arrangeCalls",
            "arrangeWork",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "ideIndentGuideHasOwner",
            "runtimeIdeIndentRenderCalls",
            "runtimeIdeIndentRenderSkippedNoOwner",
            "runtimeIdeIndentRenderSkippedEmpty",
            "runtimeIdeIndentRenderSegments",
            "runtimeIdeIndentRenderMs",
            "telemetryIdeIndentRenderCalls",
            "telemetryIdeIndentRenderSkippedNoOwner",
            "telemetryIdeIndentRenderSkippedEmpty",
            "telemetryIdeIndentRenderSegments",
            "telemetryIdeIndentRenderMs");

        AddElementTypeRules(
            rules,
            "ScrollViewer",
            "contentType",
            "contentSummary",
            "horizontalOffset",
            "verticalOffset",
            "extent",
            "viewport",
            "scrollable",
            "contentMeasureCalls",
            "contentMeasureWork",
            "contentArrangeCalls",
            "contentArrangeWork",
            "contentMeasureMs",
            "contentArrangeMs",
            "contentMeasureValid",
            "contentArrangeValid",
            "runtimeMeasureOverrideCalls",
            "runtimeMeasureOverrideMs",
            "runtimeArrangeOverrideCalls",
            "runtimeArrangeOverrideMs",
            "runtimeMeasureContentCalls",
            "runtimeMeasureContentMs",
            "runtimeResolveBarsMeasureCalls",
            "runtimeResolveBarsMeasureMs",
            "runtimeResolveBarsMeasureIterations",
            "runtimeResolveBarsMeasureRemeasurePathCalls",
            "runtimeResolveBarsMeasureHorizontalFlips",
            "runtimeResolveBarsMeasureVerticalFlips",
            "runtimeResolveBarsMeasureLastTrace",
            "runtimeResolveBarsMeasureHottestTrace",
            "telemetryMeasureOverrideCalls",
            "telemetryMeasureOverrideMs",
            "telemetryArrangeOverrideCalls",
            "telemetryArrangeOverrideMs",
            "telemetryResolveBarsMeasureCalls",
            "telemetryResolveBarsMeasureMs",
            "telemetryResolveBarsMeasureIterations",
            "telemetryResolveBarsMeasureRemeasurePathCalls",
            "telemetryMeasureContentCalls",
            "telemetryMeasureContentMs");

        return [.. rules];
    }

    private static void AddNamedRules(List<InkkOopsDiagnosticsFactRule> rules, string displayNameContains, params string[] keys)
    {
        for (var i = 0; i < keys.Length; i++)
        {
            rules.Add(new InkkOopsDiagnosticsFactRule
            {
                DisplayNameContains = displayNameContains,
                Key = keys[i],
                Comparison = InkkOopsDiagnosticsComparison.Exists
            });
        }
    }

    private static void AddElementTypeRules(List<InkkOopsDiagnosticsFactRule> rules, string elementTypeName, params string[] keys)
    {
        for (var i = 0; i < keys.Length; i++)
        {
            rules.Add(new InkkOopsDiagnosticsFactRule
            {
                ElementTypeName = elementTypeName,
                Key = keys[i],
                Comparison = InkkOopsDiagnosticsComparison.Exists
            });
        }
    }

    private static bool TryGetActionIndex(string artifactName, out int actionIndex)
    {
        const string prefix = "action[";
        actionIndex = -1;

        if (!artifactName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var endIndex = artifactName.IndexOf(']', prefix.Length);
        return endIndex > prefix.Length
            && int.TryParse(artifactName[prefix.Length..endIndex], out actionIndex);
    }
}
