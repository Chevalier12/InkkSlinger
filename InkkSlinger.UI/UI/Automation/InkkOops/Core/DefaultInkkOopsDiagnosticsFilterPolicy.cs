using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsFilterPolicy : IInkkOopsDiagnosticsFilterPolicy
{
    private static readonly int[] TargetedActionIndexes =
    [
        3,
        4,
        5,
        6,
        7,
        8,
    ];

    private static readonly InkkOopsDiagnosticsFilter TargetedActionFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules = CreateDesignerRootTemplateComboBoxOpenRules()
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

    private static InkkOopsDiagnosticsFactRule[] CreateDesignerRootTemplateComboBoxOpenRules()
    {
        var rules = new List<InkkOopsDiagnosticsFactRule>();

        AddNamedRules(
            rules,
            "RootTemplateComboBox",
            "hovered",
            "focused",
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
            "measureInvalidationTopSources",
            "arrangeInvalidationTopSources",
            "renderInvalidationTopSources",
            "frameworkUpdateLayoutPasses",
            "controlHasTemplateRoot",
            "controlTemplateRootType",
            "controlRuntimeApplyTemplateCalls",
            "controlRuntimeApplyTemplateMs",
            "controlRuntimeMeasureOverrideCalls",
            "controlRuntimeMeasureOverrideMs",
            "controlRuntimeArrangeOverrideCalls",
            "controlRuntimeArrangeOverrideMs",
            "comboBoxIsDropDownOpen",
            "comboBoxHasDropDownPopup",
            "comboBoxIsDropDownPopupOpen",
            "comboBoxHasDropDownList",
            "comboBoxSelectedIndex",
            "comboBoxSelectedText",
            "comboBoxItemContainerCount",
            "comboBoxDropDownItemCount",
            "comboBoxLayoutSlot",
            "comboBoxRuntimeHandlePointerDownCalls",
            "comboBoxRuntimeHandlePointerDownMs",
            "comboBoxRuntimePointerHits",
            "comboBoxRuntimePointerOpenToggles",
            "comboBoxRuntimeOpenStateChangedCalls",
            "comboBoxRuntimeOpenStateChangedMs",
            "comboBoxRuntimeOpenStateOpenPath",
            "comboBoxRuntimeOpenDropDownCalls",
            "comboBoxRuntimeOpenDropDownMs",
            "comboBoxRuntimeOpenDropDownPopupShow",
            "comboBoxRuntimeEnsureDropDownControlsCalls",
            "comboBoxRuntimeEnsureDropDownControlsMs",
            "comboBoxRuntimeDropDownListCreates",
            "comboBoxRuntimeDropDownListReuses",
            "comboBoxRuntimeDropDownPopupCreates",
            "comboBoxRuntimeDropDownPopupReuses",
            "comboBoxRuntimeRefreshDropDownItemsCalls",
            "comboBoxRuntimeRefreshDropDownItemsMs",
            "comboBoxRuntimeRefreshProjectedItems",
            "comboBoxRuntimeRefreshSelectedIndexSync",
            "comboBoxRuntimeBuildDropDownContainerCalls",
            "comboBoxRuntimePrepareContainerCalls",
            "comboBoxRuntimePrepareConfigured",
            "comboBoxRuntimeSyncTypographyCalls");

        AddElementTypeRules(
            rules,
            nameof(Popup),
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
            "measureInvalidationTopSources",
            "arrangeInvalidationTopSources",
            "renderInvalidationTopSources",
            "frameworkUpdateLayoutPasses",
            "controlHasTemplateRoot",
            "controlTemplateRootType",
            "controlRuntimeApplyTemplateCalls",
            "controlRuntimeApplyTemplateMs",
            "controlRuntimeMeasureOverrideCalls",
            "controlRuntimeMeasureOverrideMs",
            "controlRuntimeArrangeOverrideCalls",
            "controlRuntimeArrangeOverrideMs",
            "contentControlHasContentElement",
            "contentControlContentElementType",
            "contentControlHasActiveContentPresenter",
            "contentControlActiveContentPresenterType",
            "contentControlHasContent",
            "contentControlContentType",
            "contentControlHasContentTemplate",
            "contentControlRuntimeMeasureOverrideCalls",
            "contentControlRuntimeMeasureOverrideMs",
            "contentControlRuntimeArrangeOverrideCalls",
            "contentControlRuntimeArrangeOverrideMs",
            "contentControlRuntimeUpdateContentElementCalls",
            "contentControlRuntimeUpdateContentElementMs",
            "contentControlRuntimeUpdateContentElementTemplateSelected",
            "contentControlRuntimeUpdateContentElementTemplateBuilt");

        AddElementTypeRules(
            rules,
            nameof(ListBox),
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
            "measureInvalidationTopSources",
            "arrangeInvalidationTopSources",
            "renderInvalidationTopSources",
            "frameworkUpdateLayoutPasses",
            "controlHasTemplateRoot",
            "controlTemplateRootType",
            "controlRuntimeApplyTemplateCalls",
            "controlRuntimeApplyTemplateMs",
            "controlRuntimeMeasureOverrideCalls",
            "controlRuntimeMeasureOverrideMs",
            "controlRuntimeArrangeOverrideCalls",
            "controlRuntimeArrangeOverrideMs");

        AddElementTypeRules(
            rules,
            nameof(ScrollViewer),
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
            "measureInvalidationTopSources",
            "arrangeInvalidationTopSources",
            "renderInvalidationTopSources",
            "frameworkUpdateLayoutPasses",
            "horizontalOffset",
            "verticalOffset",
            "extent",
            "viewport",
            "scrollable",
            "contentViewport",
            "contentType",
            "contentSummary",
            "contentMeasureCalls",
            "contentMeasureWork",
            "contentArrangeCalls",
            "contentArrangeWork",
            "contentMeasureMs",
            "contentArrangeMs",
            "runtimeMeasureOverrideCalls",
            "runtimeMeasureOverrideMs",
            "runtimeArrangeOverrideCalls",
            "runtimeArrangeOverrideMs",
            "runtimeResolveBarsMeasureCalls",
            "runtimeResolveBarsMeasureMs",
            "runtimeResolveBarsMeasureIterations",
            "runtimeResolveBarsMeasureRemeasurePathCalls",
            "runtimeResolveBarsMeasureLastTrace",
            "runtimeResolveBarsMeasureHottestTrace",
            "runtimeResolveBarsMeasureHottestMs",
            "runtimeMeasureContentCalls",
            "runtimeMeasureContentMs",
            "runtimeUpdateScrollBarsCalls",
            "runtimeUpdateScrollBarsMs",
            "runtimeSetOffsetsCalls",
            "runtimeSetOffsetsMs",
            "runtimeSetOffsetsWork",
            "runtimeSetOffsetsNoOp",
            "runtimeArrangeContentCalls",
            "runtimeArrangeContentMs",
            "runtimeInvalidateScrollInfoCalls");

        AddElementTypeRules(
            rules,
            nameof(VirtualizingStackPanel),
            "orientation",
            "isVirtualizing",
            "virtualizationMode",
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
            "lastMeasuredRange",
            "lastArrangedRange",
            "lastViewportContext",
            "lastOffsetDecision",
            "lastOffsetDecisionWindow",
            "lastOffsetDecisionRealizedSpan",
            "runtimeMeasureOverrideCalls",
            "runtimeMeasureOverrideMs",
            "runtimeMeasureAllChildrenCalls",
            "runtimeMeasureAllChildrenMs",
            "runtimeMeasureRangeCalls",
            "runtimeMeasureRangeMs",
            "runtimeArrangeOverrideCalls",
            "runtimeArrangeOverrideMs",
            "runtimeArrangeRangeCalls",
            "runtimeArrangeRangeMs",
            "runtimeCanReuseMeasureCalls",
            "runtimeCanReuseMeasureTrue",
            "realizedChildSummary");

        AddElementTypeRules(
            rules,
            nameof(ComboBoxItem),
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
            "measureInvalidationTopSources",
            "arrangeInvalidationTopSources",
            "renderInvalidationTopSources",
            "frameworkUpdateLayoutPasses",
            "controlHasTemplateRoot",
            "controlTemplateRootType",
            "contentControlHasContentElement",
            "contentControlContentElementType",
            "contentControlHasActiveContentPresenter",
            "contentControlActiveContentPresenterType",
            "contentControlHasContent",
            "contentControlContentType",
            "contentControlHasContentTemplate",
            "contentControlRuntimeMeasureOverrideCalls",
            "contentControlRuntimeMeasureOverrideMs",
            "contentControlRuntimeArrangeOverrideCalls",
            "contentControlRuntimeArrangeOverrideMs",
            "contentControlRuntimeAttachContentPresenterCalls",
            "contentControlRuntimeUpdateContentElementCalls",
            "contentControlRuntimeUpdateContentElementMs",
            "contentControlRuntimeUpdateContentElementTemplateSelected",
            "contentControlRuntimeUpdateContentElementTemplateBuilt",
            "contentControlRuntimeUpdateContentElementAttachedNew");

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
