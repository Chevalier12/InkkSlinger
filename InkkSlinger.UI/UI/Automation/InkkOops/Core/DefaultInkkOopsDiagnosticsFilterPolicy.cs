using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsFilterPolicy : IInkkOopsDiagnosticsFilterPolicy
{
    private const int HighestPriorityActionIndex = 12;

    private static readonly int[] TargetedActionIndexes = [7, 8, 10, 11, 12];

    private static readonly InkkOopsDiagnosticsFilter TargetedActionFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules = CreateTargetedCalendarRules(includePriorityRules: false)
    };

    private static readonly InkkOopsDiagnosticsFilter HighestPriorityActionFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules = CreateTargetedCalendarRules(includePriorityRules: true)
    };

    public InkkOopsDiagnosticsFilter CreateFilter(string artifactName)
    {
        if (!TryGetActionIndex(artifactName, out var actionIndex))
        {
            return InkkOopsDiagnosticsFilter.None;
        }

        if (actionIndex == HighestPriorityActionIndex)
        {
            return HighestPriorityActionFilter;
        }

        return Array.IndexOf(TargetedActionIndexes, actionIndex) >= 0
            ? TargetedActionFilter
            : InkkOopsDiagnosticsFilter.None;
    }

    private static InkkOopsDiagnosticsFactRule[] CreateTargetedCalendarRules(bool includePriorityRules)
    {
        var rules = new List<InkkOopsDiagnosticsFactRule>
        {
            new() { Key = "hovered", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true },
            new() { Key = "focused", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true }
        };

        AddNamedRules(
            rules,
            "CalendarPreviousMonthButton",
            "name",
            "slot",
            "actual",
            "renderSize",
            "previousAvailable",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "arrangeMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "controlTemplateRootType",
            "controlRuntimeApplyTemplateCalls",
            "controlRuntimeMeasureOverrideMs",
            "controlRuntimeArrangeOverrideMs",
            "buttonContentType",
            "buttonDisplayText",
            "buttonHasTextLayoutCache",
            "buttonHasIntrinsicMeasureCache",
            "buttonHasTextRenderPlanCache",
            "buttonIsMouseOver",
            "buttonIsPressed",
            "buttonContentVersion",
            "buttonLayoutSlot",
            "buttonRuntimeMeasureOverrideCalls",
            "buttonRuntimeMeasureOverrideMs",
            "buttonRuntimeResolveTextLayoutCalls",
            "buttonRuntimeResolveTextLayoutMs",
            "buttonRuntimeTextLayoutCacheHits",
            "buttonRuntimeTextLayoutCacheMisses",
            "buttonRuntimeTextLayoutInvalidations",
            "buttonRuntimeTextRenderPlanCacheHits",
            "buttonRuntimeTextRenderPlanCacheMisses",
            "buttonRuntimeTextRenderPlanInvalidations",
            "buttonRuntimeIntrinsicMeasureCacheHits",
            "buttonRuntimeIntrinsicMeasureCacheMisses",
            "buttonRuntimeRenderTextPreparationCalls",
            "buttonRuntimeRenderTextPreparationMs",
            "buttonRuntimeRenderCalls",
            "buttonRuntimeRenderMs",
            "buttonRuntimeOnClickCalls",
            "buttonRuntimeOnClickMs",
            "buttonRuntimeSetMouseOverChanged",
            "buttonRuntimeSetPressedChanged",
            "buttonRuntimeInvokeFromInput");

        AddNamedRules(
            rules,
            "CalendarNextMonthButton",
            "name",
            "slot",
            "actual",
            "renderSize",
            "previousAvailable",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "arrangeMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "controlTemplateRootType",
            "controlRuntimeApplyTemplateCalls",
            "controlRuntimeMeasureOverrideMs",
            "controlRuntimeArrangeOverrideMs",
            "buttonContentType",
            "buttonDisplayText",
            "buttonHasTextLayoutCache",
            "buttonHasIntrinsicMeasureCache",
            "buttonHasTextRenderPlanCache",
            "buttonIsMouseOver",
            "buttonIsPressed",
            "buttonContentVersion",
            "buttonLayoutSlot",
            "buttonRuntimeMeasureOverrideCalls",
            "buttonRuntimeMeasureOverrideMs",
            "buttonRuntimeResolveTextLayoutCalls",
            "buttonRuntimeResolveTextLayoutMs",
            "buttonRuntimeTextLayoutCacheHits",
            "buttonRuntimeTextLayoutCacheMisses",
            "buttonRuntimeTextLayoutInvalidations",
            "buttonRuntimeTextRenderPlanCacheHits",
            "buttonRuntimeTextRenderPlanCacheMisses",
            "buttonRuntimeTextRenderPlanInvalidations",
            "buttonRuntimeIntrinsicMeasureCacheHits",
            "buttonRuntimeIntrinsicMeasureCacheMisses",
            "buttonRuntimeRenderTextPreparationCalls",
            "buttonRuntimeRenderTextPreparationMs",
            "buttonRuntimeRenderCalls",
            "buttonRuntimeRenderMs",
            "buttonRuntimeOnClickCalls",
            "buttonRuntimeOnClickMs",
            "buttonRuntimeSetMouseOverChanged",
            "buttonRuntimeSetPressedChanged",
            "buttonRuntimeInvokeFromInput");

        AddElementTypeRules(
            rules,
            "Calendar",
            "slot",
            "actual",
            "renderSize",
            "previousAvailable",
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
            "renderInvalidationLast",
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
            "Grid",
            "slot",
            "actual",
            "renderSize",
            "previousAvailable",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "gridRuntimeColumnDefinitions",
            "gridRuntimeRowDefinitions",
            "gridRuntimeChildren",
            "gridRuntimeMeasuredColumns",
            "gridRuntimeMeasuredRows",
            "gridRuntimeMetadataDirty",
            "rows",
            "columns",
            "children");

        AddElementTypeRules(
            rules,
            "UniformGrid",
            "slot",
            "actual",
            "renderSize",
            "previousAvailable",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast");

        AddElementTypeRules(
            rules,
            "CalendarDayButton",
            "slot",
            "actual",
            "renderSize",
            "previousAvailable",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "arrangeMs",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "controlTemplateRootType",
            "controlRuntimeApplyTemplateCalls",
            "controlRuntimeMeasureOverrideMs",
            "controlRuntimeArrangeOverrideMs",
            "buttonContentType",
            "buttonDisplayText",
            "buttonHasTextLayoutCache",
            "buttonHasIntrinsicMeasureCache",
            "buttonHasTextRenderPlanCache",
            "buttonIsPressed",
            "buttonContentVersion",
            "buttonLayoutSlot",
            "buttonRuntimeMeasureOverrideCalls",
            "buttonRuntimeMeasureOverrideMs",
            "buttonRuntimeResolveTextLayoutCalls",
            "buttonRuntimeResolveTextLayoutMs",
            "buttonRuntimeTextLayoutCacheHits",
            "buttonRuntimeTextLayoutCacheMisses",
            "buttonRuntimeTextLayoutInvalidations",
            "buttonRuntimeTextRenderPlanCacheHits",
            "buttonRuntimeTextRenderPlanCacheMisses",
            "buttonRuntimeTextRenderPlanInvalidations",
            "buttonRuntimeIntrinsicMeasureCacheHits",
            "buttonRuntimeIntrinsicMeasureCacheMisses",
            "buttonRuntimeRenderTextPreparationCalls",
            "buttonRuntimeRenderTextPreparationMs",
            "buttonRuntimeRenderCalls",
            "buttonRuntimeRenderMs");

        AddElementTypeRules(
            rules,
            "ContentPresenter",
            "contentPresenterPresentedElementType",
            "contentPresenterSourceOwnerType",
            "contentPresenterContentSource",
            "contentPresenterHasEffectivePresentationState",
            "contentPresenterRuntimeSourceOwnerChangedCalls",
            "contentPresenterRuntimeSourceOwnerChangedMs",
            "contentPresenterRuntimeSourceOwnerChangedRebuilt",
            "contentPresenterRuntimeSourceOwnerChangedFallbackRefresh",
            "contentPresenterRuntimeRefreshPresentedElementCalls",
            "contentPresenterRuntimeRefreshPresentedElementMs",
            "contentPresenterRuntimeRefreshPresentedElementChanged",
            "contentPresenterRuntimeBuildContentElementCalls",
            "contentPresenterRuntimeBuildContentElementMs",
            "contentPresenterRuntimeBuildContentElementTemplatePath",
            "contentPresenterRuntimeBuildContentElementAccessTextPath",
            "contentPresenterRuntimeMeasureOverrideCalls",
            "contentPresenterRuntimeMeasureOverrideMs",
            "contentPresenterRuntimeArrangeOverrideCalls",
            "contentPresenterRuntimeArrangeOverrideMs");

        AddElementTypeRules(
            rules,
            "TextBlock",
            "text",
            "renderText",
            "renderLines",
            "renderWidth",
            "desired",
            "previousAvailable",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "lineHeight",
            "inkBounds",
            "textVersion",
            "layoutCacheWidth",
            "secondaryLayoutCacheWidth",
            "runtimeMeasureOverrideCalls",
            "runtimeMeasureOverrideMs",
            "runtimeResolveLayoutCalls",
            "runtimeResolveLayoutMs",
            "runtimeResolveLayoutCacheHits",
            "runtimeResolveLayoutCacheMisses",
            "runtimeResolveLayoutUncachedCalls",
            "runtimeResolveLayoutUncachedMs",
            "runtimeTextPropertyChanges",
            "runtimeLayoutCacheInvalidations",
            "runtimeIntrinsicMeasureInvalidations",
            "runtimeRenderCalls",
            "runtimeRenderMs");

        if (includePriorityRules)
        {
            AddNamedRules(
                rules,
                "CalendarPreviousMonthButton",
                "measureInvalidationTopSources",
                "arrangeInvalidationTopSources",
                "renderInvalidationTopSources",
                "buttonMeasureOverrideCalls",
                "buttonMeasureOverrideMs",
                "buttonResolveTextLayoutCalls",
                "buttonResolveTextLayoutMs",
                "buttonTextLayoutCacheHits",
                "buttonTextLayoutCacheMisses",
                "buttonTextLayoutInvalidations",
                "buttonTextRenderPlanCacheHits",
                "buttonTextRenderPlanCacheMisses",
                "buttonTextRenderPlanInvalidations",
                "buttonIntrinsicMeasureCacheHits",
                "buttonIntrinsicMeasureCacheMisses",
                "buttonRenderCalls",
                "buttonRenderMs",
                "buttonOnClickCalls",
                "buttonOnClickMs",
                "buttonDependencyPropertyChangedCalls",
                "buttonContentPropertyChanged",
                "buttonTextMetricPropertyChanged");

            AddNamedRules(
                rules,
                "CalendarNextMonthButton",
                "measureInvalidationTopSources",
                "arrangeInvalidationTopSources",
                "renderInvalidationTopSources",
                "buttonMeasureOverrideCalls",
                "buttonMeasureOverrideMs",
                "buttonResolveTextLayoutCalls",
                "buttonResolveTextLayoutMs",
                "buttonTextLayoutCacheHits",
                "buttonTextLayoutCacheMisses",
                "buttonTextLayoutInvalidations",
                "buttonTextRenderPlanCacheHits",
                "buttonTextRenderPlanCacheMisses",
                "buttonTextRenderPlanInvalidations",
                "buttonIntrinsicMeasureCacheHits",
                "buttonIntrinsicMeasureCacheMisses",
                "buttonRenderCalls",
                "buttonRenderMs",
                "buttonOnClickCalls",
                "buttonOnClickMs",
                "buttonDependencyPropertyChangedCalls",
                "buttonContentPropertyChanged",
                "buttonTextMetricPropertyChanged");

            AddElementTypeRules(
                rules,
                "Calendar",
                "measureInvalidationTopSources",
                "arrangeInvalidationTopSources",
                "renderInvalidationTopSources",
                "frameworkUpdateLayoutCalls",
                "frameworkUpdateLayoutPasses",
                "frameworkUpdateLayoutMeasureRepairs",
                "frameworkUpdateLayoutArrangeRepairs");

            AddElementTypeRules(
                rules,
                "Grid",
                "measureInvalidationTopSources",
                "arrangeInvalidationTopSources",
                "renderInvalidationTopSources",
                "gridGlobalMeasureCalls",
                "gridGlobalMeasureMs",
                "gridGlobalArrangeCalls",
                "gridGlobalArrangeMs",
                "gridGlobalMeasureChildCalls",
                "gridGlobalMeasureChildMs",
                "gridGlobalMeasureChildCacheHits",
                "gridGlobalMeasureChildCacheMisses",
                "gridGlobalPrepareMetadataCalls",
                "gridGlobalPrepareMetadataMs",
                "gridGlobalMetadataInvalidations",
                "gridGlobalResolveDefinitionCalls",
                "gridGlobalResolveDefinitionMs",
                "gridGlobalApplyRequirementCalls",
                "gridGlobalApplyRequirementMs",
                "gridGlobalRemeasures",
                "gridGlobalOverflowTriggers");

            AddElementTypeRules(
                rules,
                "UniformGrid",
                "measureInvalidationTopSources",
                "arrangeInvalidationTopSources",
                "renderInvalidationTopSources",
                "frameworkUpdateLayoutPasses");

            AddElementTypeRules(
                rules,
                "CalendarDayButton",
                "measureInvalidationTopSources",
                "arrangeInvalidationTopSources",
                "renderInvalidationTopSources",
                "buttonDependencyPropertyChangedCalls",
                "buttonContentPropertyChanged",
                "buttonTextMetricPropertyChanged");

            AddElementTypeRules(
                rules,
                "ContentPresenter",
                "contentPresenterRefreshPresentedElementStateCacheHits",
                "contentPresenterSourceOwnerChangedRebuilt",
                "contentPresenterFindSourceOwnerCalls",
                "contentPresenterFindSourceOwnerMs",
                "contentPresenterFindReadablePropertyCalls",
                "contentPresenterFindReadablePropertyMs");

            AddElementTypeRules(
                rules,
                "TextBlock",
                "measureInvalidationTopSources",
                "arrangeInvalidationTopSources",
                "renderInvalidationTopSources",
                "telemetryResolveLayoutCalls",
                "telemetryResolveLayoutMs",
                "telemetryResolveLayoutCacheHits",
                "telemetryResolveLayoutCacheMisses",
                "telemetryResolveLayoutUncachedCalls",
                "telemetryResolveLayoutUncachedMs",
                "telemetryLayoutCacheInvalidations",
                "telemetryIntrinsicMeasureInvalidations",
                "telemetryRenderCalls",
                "telemetryRenderMs");
        }

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
