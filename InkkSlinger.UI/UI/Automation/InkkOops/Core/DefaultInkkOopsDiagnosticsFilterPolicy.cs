using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsFilterPolicy : IInkkOopsDiagnosticsFilterPolicy
{
    private static readonly int[] TargetedActionIndexes =
    [
        164,
        166,
        168,
    ];

    private static readonly InkkOopsDiagnosticsFilter TargetedActionFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules = CreateCompletionDropdownScrollRules()
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

    private static InkkOopsDiagnosticsFactRule[] CreateCompletionDropdownScrollRules()
    {
        var rules = new List<InkkOopsDiagnosticsFactRule>();

        AddNamedRules(
            rules,
            "SourceEditorPane",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "userControlHasTemplateRoot",
            "userControlHasContentElement",
            "userControlContentElementType",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "frameworkUpdateLayoutPasses");

        AddNamedRules(
            rules,
            "CompletionPopup",
            "name",
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
            "frameworkInvalidateVisualCalls");

        AddNamedRules(
            rules,
            "CompletionListBox",
            "name",
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
            "controlHasTemplateRoot",
            "controlTemplateRootType",
            "controlRuntimeMeasureOverrideCalls",
            "controlRuntimeArrangeOverrideCalls");

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
            "runtimeMouseWheelCalls",
            "runtimeMouseWheelHandled",
            "runtimeMouseWheelMs",
            "runtimeSetOffsetsCalls",
            "runtimeSetOffsetsMs",
            "runtimeSetOffsetsWork",
            "runtimeSetOffsetsNoOp",
            "runtimeSetOffsetsVirtualizingMeasureInvalidation",
            "runtimeSetOffsetsVirtualizingArrangeOnly",
            "runtimeSetOffsetsTransformInvalidation",
            "runtimeSetOffsetsManualArrange",
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
            "runtimeResolveBarsMeasureHottestTrace");

        AddElementTypeRules(
            rules,
            "VirtualizingStackPanel",
            "orientation",
            "isVirtualizing",
            "virtualizationMode",
            "cacheLength",
            "cacheLengthUnit",
            "virtualizationActive",
            "children",
            "realizedRange",
            "realizedChildren",
            "realizedSpan",
            "extent",
            "viewport",
            "offset",
            "averagePrimarySize",
            "maxSecondarySize",
            "startOffsetsDirty",
            "relayoutQueuedFromOffset",
            "lastMeasuredRange",
            "lastArrangedRange",
            "lastViewportContext",
            "lastOffsetDecision",
            "lastOffsetDecisionWindow",
            "lastOffsetDecisionViewport",
            "lastOffsetDecisionRealizedSpan",
            "lastOffsetDecisionGuardBand",
            "runtimeMeasureOverrideCalls",
            "runtimeMeasureOverrideMs",
            "runtimeMeasureOverrideReusedRange",
            "runtimeMeasureAllChildrenCalls",
            "runtimeMeasureAllChildrenMs",
            "runtimeMeasureRangeCalls",
            "runtimeMeasureRangeMs",
            "runtimeArrangeOverrideCalls",
            "runtimeArrangeOverrideMs",
            "runtimeArrangeOverrideReusedRange",
            "runtimeArrangeRangeCalls",
            "runtimeArrangeRangeMs",
            "runtimeCanReuseMeasureCalls",
            "runtimeCanReuseMeasureTrue",
            "runtimeResolveViewportContextCalls",
            "runtimeViewerOwnedOffsetDecisionCalls",
            "runtimeViewerOwnedOffsetDecisionRequireMeasure",
            "runtimeViewerOwnedOffsetDecisionOrientationMismatch",
            "runtimeViewerOwnedOffsetDecisionInactiveOrNoChildrenOrNoDelta",
            "runtimeViewerOwnedOffsetDecisionNonFiniteViewport",
            "runtimeViewerOwnedOffsetDecisionMissingRealizedRange",
            "runtimeViewerOwnedOffsetDecisionBeforeGuardBand",
            "runtimeViewerOwnedOffsetDecisionAfterGuardBand",
            "runtimeViewerOwnedOffsetDecisionWithinRealizedWindow",
            "runtimeSetVerticalOffsetCalls",
            "runtimeSetVerticalOffsetRelayout",
            "runtimeSetVerticalOffsetVisualOnly",
            "runtimeSetHorizontalOffsetCalls",
            "runtimeSetHorizontalOffsetRelayout",
            "runtimeSetHorizontalOffsetVisualOnly",
            "realizedChildSummary");

        AddElementTypeRules(
            rules,
            "ListBoxItem",
            "hovered",
            "focused",
            "slot",
            "desired",
            "actual",
            "measureCalls",
            "measureWork",
            "arrangeCalls",
            "arrangeWork",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations");

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
