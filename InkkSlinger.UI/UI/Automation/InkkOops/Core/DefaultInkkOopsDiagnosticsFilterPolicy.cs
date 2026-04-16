using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsFilterPolicy : IInkkOopsDiagnosticsFilterPolicy
{
    private static readonly int[] TargetedActionIndexes = [56, 66, 154, 156, 160];

    private static readonly InkkOopsDiagnosticsFilter TargetedActionFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules = CreateDesignerSourceSplitterDragRules()
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

    private static InkkOopsDiagnosticsFactRule[] CreateDesignerSourceSplitterDragRules()
    {
        var rules = new List<InkkOopsDiagnosticsFactRule>
        {
            new() { Key = "hovered", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true },
            new() { Key = "focused", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true }
        };

        AddNamedRules(
            rules,
            "PreviewSourceSplitter",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "measureInvalidationTopSources",
            "arrangeInvalidationLast",
            "arrangeInvalidationTopSources",
            "renderInvalidationLast",
            "renderInvalidationTopSources",
            "gridSplitterConfiguredDirection",
            "gridSplitterEffectiveDirection",
            "gridSplitterBehavior",
            "gridSplitterEnabled",
            "gridSplitterHover",
            "gridSplitterDragging",
            "gridSplitterActiveGrid",
            "gridSplitterVisualParent",
            "gridSplitterSlot",
            "gridSplitterIncrements",
            "gridSplitterActivePair",
            "gridSplitterStartSizes",
            "gridSplitterLastDeltas",
            "gridSplitterRuntimePointer",
            "gridSplitterRuntimeKeyDown",
            "gridSplitterRuntimeKeyDownMs",
            "gridSplitterRuntimeResize",
            "gridSplitterRuntimeResolveTargets",
            "gridSplitterRuntimeResolveDirection",
            "gridSplitterRuntimeResolvePairs",
            "gridSplitterRuntimeResolveSizes",
            "gridSplitterRuntimeSnap",
            "gridSplitterPointer",
            "gridSplitterKeyDown",
            "gridSplitterKeyDownMs",
            "gridSplitterResize",
            "gridSplitterResolveTargets",
            "gridSplitterResolveDirection",
            "gridSplitterResolvePairs",
            "gridSplitterResolveSizes",
            "gridSplitterSnap");

        AddNamedRules(
            rules,
            "SourceEditor",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "measureInvalidationTopSources",
            "arrangeInvalidationLast",
            "arrangeInvalidationTopSources",
            "renderInvalidationLast",
            "renderInvalidationTopSources",
            "controlTemplateRootType",
            "controlRuntimeApplyTemplateCalls",
            "controlRuntimeApplyTemplateMs",
            "controlRuntimeMeasureOverrideCalls",
            "controlRuntimeMeasureOverrideMs",
            "controlRuntimeArrangeOverrideCalls",
            "controlRuntimeArrangeOverrideMs",
            "frameworkUpdateLayoutCalls",
            "frameworkUpdateLayoutPasses",
            "frameworkUpdateLayoutMeasureRepairs",
            "frameworkUpdateLayoutArrangeRepairs");

        AddNamedRules(
            rules,
            "SourceEditorPane",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "arrangeMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "measureInvalidationLast",
            "measureInvalidationTopSources",
            "arrangeInvalidationLast",
            "arrangeInvalidationTopSources",
            "userControlHasContentElement",
            "userControlContentElementType",
            "userControlRuntimeMeasureOverrideCalls",
            "userControlRuntimeMeasureOverrideMs",
            "userControlRuntimeArrangeOverrideCalls",
            "userControlRuntimeArrangeOverrideMs",
            "userControlRuntimeRenderCalls",
            "userControlRuntimeRenderMs",
            "userControlRuntimeEnsureTemplateCalls",
            "userControlRuntimeEnsureTemplateMs");

        AddElementTypeRules(
            rules,
            "GridSplitter",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "measureInvalidationTopSources",
            "arrangeInvalidationLast",
            "arrangeInvalidationTopSources",
            "renderInvalidationLast",
            "renderInvalidationTopSources",
            "gridSplitterConfiguredDirection",
            "gridSplitterEffectiveDirection",
            "gridSplitterBehavior",
            "gridSplitterEnabled",
            "gridSplitterHover",
            "gridSplitterDragging",
            "gridSplitterActiveGrid",
            "gridSplitterVisualParent",
            "gridSplitterSlot",
            "gridSplitterIncrements",
            "gridSplitterActivePair",
            "gridSplitterStartSizes",
            "gridSplitterLastDeltas",
            "gridSplitterRuntimePointer",
            "gridSplitterRuntimeKeyDown",
            "gridSplitterRuntimeKeyDownMs",
            "gridSplitterRuntimeResize",
            "gridSplitterRuntimeResolveTargets",
            "gridSplitterRuntimeResolveDirection",
            "gridSplitterRuntimeResolvePairs",
            "gridSplitterRuntimeResolveSizes",
            "gridSplitterRuntimeSnap",
            "gridSplitterPointer",
            "gridSplitterKeyDown",
            "gridSplitterKeyDownMs",
            "gridSplitterResize",
            "gridSplitterResolveTargets",
            "gridSplitterResolveDirection",
            "gridSplitterResolvePairs",
            "gridSplitterResolveSizes",
            "gridSplitterSnap");

        AddElementTypeRules(
            rules,
            "SourceEditor",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "measureInvalidationTopSources",
            "arrangeInvalidationLast",
            "arrangeInvalidationTopSources",
            "renderInvalidationLast",
            "controlTemplateRootType",
            "controlRuntimeApplyTemplateCalls",
            "controlRuntimeApplyTemplateMs",
            "controlRuntimeMeasureOverrideCalls",
            "controlRuntimeMeasureOverrideMs",
            "controlRuntimeArrangeOverrideCalls",
            "controlRuntimeArrangeOverrideMs",
            "frameworkUpdateLayoutCalls",
            "frameworkUpdateLayoutPasses",
            "frameworkUpdateLayoutMeasureRepairs",
            "frameworkUpdateLayoutArrangeRepairs");

        AddElementTypeRules(
            rules,
            "DesignerSourceEditorView",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "arrangeMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "measureInvalidationLast",
            "measureInvalidationTopSources",
            "arrangeInvalidationLast",
            "arrangeInvalidationTopSources",
            "userControlHasContentElement",
            "userControlContentElementType",
            "userControlHasContentElement",
            "userControlRuntimeMeasureOverrideCalls",
            "userControlRuntimeMeasureOverrideMs",
            "userControlRuntimeArrangeOverrideCalls",
            "userControlRuntimeArrangeOverrideMs",
            "userControlRuntimeRenderCalls",
            "userControlRuntimeRenderMs",
            "userControlRuntimeEnsureTemplateCalls",
            "userControlRuntimeEnsureTemplateMs");

        AddElementTypeRules(
            rules,
            "RichTextBox",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "measureInvalidationTopSources",
            "arrangeInvalidationLast",
            "arrangeInvalidationTopSources",
            "renderInvalidationLast",
            "renderInvalidationTopSources",
            "controlTemplateRootType",
            "controlRuntimeApplyTemplateCalls",
            "controlRuntimeApplyTemplateMs",
            "controlRuntimeMeasureOverrideCalls",
            "controlRuntimeMeasureOverrideMs",
            "controlRuntimeArrangeOverrideCalls",
            "controlRuntimeArrangeOverrideMs",
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
            "runtimeNotifyViewportChangedCalls",
            "runtimeNotifyViewportChangedRaised",
            "runtimeNotifyViewportChangedSkippedNoChange",
            "runtimeNotifyViewportChangedMs",
            "runtimeNotifyViewportChangedSubscriberMs",
            "telemetryContentHostViewportChangedCalls",
            "telemetryContentHostViewportChangedMs",
            "telemetryContentHostViewportChangedApplyPendingMs",
            "telemetryContentHostViewportChangedEnsureHostedLayoutMs",
            "telemetryContentHostViewportChangedNotifyViewportMs",
            "telemetryApplyPendingContentHostOffsetsCalls",
            "telemetryApplyPendingContentHostOffsetsApplied",
            "telemetryApplyPendingContentHostOffsetsSkippedNoContentHost",
            "telemetryApplyPendingContentHostOffsetsSkippedNoMetrics",
            "telemetryApplyPendingContentHostOffsetsSkippedNoPending",
            "telemetryApplyPendingContentHostOffsetsMs",
            "telemetryEnsureHostedDocumentChildLayoutCalls",
            "telemetryEnsureHostedDocumentChildLayoutSkippedZeroTextRect",
            "telemetryEnsureHostedDocumentChildLayoutSkippedNoHostedChildren",
            "telemetryEnsureHostedDocumentChildLayoutMs",
            "telemetryNotifyViewportChangedCalls",
            "telemetryNotifyViewportChangedRaised",
            "telemetryNotifyViewportChangedSkippedNoChange",
            "telemetryNotifyViewportChangedMs",
            "telemetryNotifyViewportChangedSubscriberMs",
            "frameworkUpdateLayoutCalls",
            "frameworkUpdateLayoutPasses",
            "frameworkUpdateLayoutMeasureRepairs",
            "frameworkUpdateLayoutArrangeRepairs");

        AddElementTypeRules(
            rules,
            "Grid",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "arrangeMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "measureInvalidationLast",
            "measureInvalidationTopSources",
            "arrangeInvalidationLast",
            "arrangeInvalidationTopSources",
            "gridRuntimeColumnDefinitions",
            "gridRuntimeRowDefinitions",
            "gridRuntimeChildren",
            "gridRuntimeMeasuredColumns",
            "gridRuntimeMeasuredRows",
            "gridRuntimeMetadataDirty",
            "gridGlobalMeasureCalls",
            "gridGlobalMeasureMs",
            "gridGlobalArrangeCalls",
            "gridGlobalArrangeMs",
            "gridGlobalMeasureChildCalls",
            "gridGlobalMeasureChildMs",
            "gridGlobalMetadataInvalidations",
            "gridGlobalResolveDefinitionCalls",
            "gridGlobalResolveDefinitionMs",
            "gridGlobalApplyRequirementCalls",
            "gridGlobalApplyRequirementMs",
            "gridGlobalRemeasures",
            "gridGlobalOverflowTriggers",
            "rows",
            "columns",
            "children");

        AddElementTypeRules(
            rules,
            "ScrollViewer",
            "slot",
            "actual",
            "renderSize",
            "previousAvailable",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "measureInvalidationLast",
            "measureInvalidationTopSources",
            "arrangeInvalidationLast",
            "arrangeInvalidationTopSources",
            "horizontalOffset",
            "verticalOffset",
            "extent",
            "viewport",
            "scrollable",
            "horizontalBarVisible",
            "verticalBarVisible",
            "contentType",
            "contentSummary",
            "contentSlot",
            "contentActual",
            "contentMeasureWork",
            "contentArrangeWork",
            "runtimeResolveBarsMeasureCalls",
            "runtimeResolveBarsMeasureMs",
            "runtimeResolveBarsMeasureIterations",
            "runtimeResolveBarsMeasureLastTrace",
            "runtimeResolveBarsMeasureHottestTrace",
            "runtimeResolveBarsMeasureHottestMs",
            "runtimeResolveBarsArrangeCalls",
            "runtimeResolveBarsArrangeMs",
            "runtimeMeasureContentCalls",
            "runtimeMeasureContentMs",
            "runtimeUpdateScrollBarsCalls",
            "runtimeUpdateScrollBarsMs",
            "runtimeSetOffsetsCalls",
            "runtimeSetOffsetsMs",
            "runtimeSetOffsetsWork",
            "runtimeSetOffsetsNoOp",
            "runtimeSetOffsetsDeferredLayout",
            "runtimeSetOffsetsTransformInvalidation",
            "runtimeArrangeContentCalls",
            "runtimeArrangeContentMs",
            "runtimeUpdateScrollBarValuesCalls",
            "runtimeUpdateScrollBarValuesMs",
            "runtimeContentViewportRect",
            "hbar_present",
            "vbar_present");

        AddElementTypeRules(
            rules,
            "ScrollBar",
            "scrollBarOrientation",
            "scrollBarValue",
            "scrollBarViewportSize",
            "scrollBarHasTrack",
            "scrollBarTrackType",
            "scrollBarHasThumb",
            "scrollBarThumbType",
            "scrollBarRuntimeSyncTrackStateCalls",
            "scrollBarRuntimeSyncTrackStateMs",
            "scrollBarRuntimeRefreshTrackLayoutCalls",
            "scrollBarRuntimeRefreshTrackLayoutMs",
            "scrollBarRuntimeRefreshTrackLayoutArranged");

        AddElementTypeRules(
            rules,
            "Track",
            "trackOrientation",
            "trackViewportSize",
            "trackTrackRect",
            "trackThumbRect",
            "trackRuntimeMeasureOverrideCalls",
            "trackRuntimeMeasureOverrideMs",
            "trackRuntimeArrangeOverrideCalls",
            "trackRuntimeArrangeOverrideMs",
            "trackRuntimeArrangeVerticalCalls",
            "trackRuntimeArrangeVerticalMs",
            "trackRuntimeArrangeHorizontalCalls",
            "trackRuntimeArrangeHorizontalMs",
            "trackRuntimeRefreshLayoutCalls",
            "trackRuntimeRefreshLayoutMs",
            "trackRuntimeComputeThumbRectCalls",
            "trackRuntimeComputeThumbRectMs");

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
