using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsFilterPolicy : IInkkOopsDiagnosticsFilterPolicy
{
    private static readonly int[] TargetedActionIndexes = [16, 17, 18, 19];

    private static readonly InkkOopsDiagnosticsFilter TargetedActionFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules = CreateSourceEditorCompletionRules()
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

    private static InkkOopsDiagnosticsFactRule[] CreateSourceEditorCompletionRules()
    {
        var rules = new List<InkkOopsDiagnosticsFactRule>
        {
            new() { Key = "hovered", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true },
            new() { Key = "focused", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true }
        };

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
            "Popup",
            "slot",
            "actual",
            "renderSize",
            "visible",
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
            "contentControlHasContentElement",
            "contentControlContentElementType",
            "contentControlHasActiveContentPresenter",
            "contentControlActiveContentPresenterType",
            "contentControlHasContent",
            "contentControlContentType",
            "contentControlRuntimeMeasureOverrideCalls",
            "contentControlRuntimeMeasureOverrideMs",
            "contentControlRuntimeArrangeOverrideCalls",
            "contentControlRuntimeArrangeOverrideMs",
            "contentControlRuntimeUpdateContentElementCalls",
            "contentControlRuntimeUpdateContentElementMs",
            "contentControlRuntimeAttachContentPresenterCalls",
            "contentControlRuntimeDetachContentPresenterCalls");

        AddElementTypeRules(
            rules,
            "ListBox",
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
            "controlTemplateRootType",
            "controlRuntimeApplyTemplateCalls",
            "controlRuntimeApplyTemplateMs",
            "controlRuntimeMeasureOverrideCalls",
            "controlRuntimeMeasureOverrideMs",
            "controlRuntimeArrangeOverrideCalls",
            "controlRuntimeArrangeOverrideMs",
            "controlRuntimeTraversalCountCalls",
            "controlRuntimeTraversalIndexCalls");

        AddElementTypeRules(
            rules,
            "ContentPresenter",
            "slot",
            "actual",
            "renderSize",
            "contentPresenterPresentedElementType",
            "contentPresenterSourceOwnerType",
            "contentPresenterContentSource",
            "contentPresenterHasEffectivePresentationState",
            "contentPresenterRuntimeRefreshPresentedElementCalls",
            "contentPresenterRuntimeRefreshPresentedElementMs",
            "contentPresenterRuntimeRefreshPresentedElementChanged",
            "contentPresenterRuntimeBuildContentElementCalls",
            "contentPresenterRuntimeBuildContentElementMs",
            "contentPresenterRuntimeBuildContentElementTemplatePath",
            "contentPresenterRuntimeBuildContentElementAccessTextPath",
            "contentPresenterRuntimeBuildContentElementLabelPath",
            "contentPresenterRuntimeMeasureOverrideCalls",
            "contentPresenterRuntimeMeasureOverrideMs",
            "contentPresenterRuntimeArrangeOverrideCalls",
            "contentPresenterRuntimeArrangeOverrideMs");

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
