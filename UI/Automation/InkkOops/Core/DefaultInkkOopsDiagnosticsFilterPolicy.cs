using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsFilterPolicy : IInkkOopsDiagnosticsFilterPolicy
{
    private const int HighestPriorityActionIndex = 11;

    private static readonly int[] TargetedActionIndexes = [10, 11, 12, 13, 14];

    private static readonly InkkOopsDiagnosticsFilter TargetedActionFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules = CreateTargetedDragRules(includePriorityRules: false)
    };

    private static readonly InkkOopsDiagnosticsFilter HighestPriorityActionFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules = CreateTargetedDragRules(includePriorityRules: true)
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

    private static InkkOopsDiagnosticsFactRule[] CreateTargetedDragRules(bool includePriorityRules)
    {
        var rules = new List<InkkOopsDiagnosticsFactRule>
        {
            new() { Key = "hovered", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true },
            new() { Key = "focused", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true }
        };

        AddNamedRules(
            rules,
            "NavigationSplitter",
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
            "gridSplitterConfiguredDirection",
            "gridSplitterEffectiveDirection",
            "gridSplitterBehavior",
            "gridSplitterEnabled",
            "gridSplitterHover",
            "gridSplitterDragging",
            "gridSplitterSlot",
            "gridSplitterActivePair",
            "gridSplitterStartSizes",
            "gridSplitterLastDeltas",
            "gridSplitterRuntimePointer",
            "gridSplitterRuntimeResize",
            "gridSplitterRuntimeResolveTargets",
            "gridSplitterRuntimeResolveDirection",
            "gridSplitterRuntimeResolvePairs",
            "gridSplitterRuntimeResolveSizes",
            "gridSplitterRuntimeSnap",
            "gridSplitterPointer",
            "gridSplitterResize",
            "gridSplitterResolveTargets",
            "gridSplitterResolveDirection",
            "gridSplitterResolvePairs",
            "gridSplitterResolveSizes",
            "gridSplitterSnap");

        AddNamedRules(
            rules,
            "PrimaryEditorGrid",
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
            "gridRuntimeColumnDefinitions",
            "gridRuntimeChildren",
            "gridRuntimeMeasuredColumns",
            "gridRuntimeMetadataDirty",
            "gridGlobalMeasureMs",
            "gridGlobalArrangeMs",
            "gridGlobalPrepareMetadataMs",
            "gridGlobalMeasureChildMs",
            "gridGlobalResolveDefinitionMs",
            "gridGlobalApplyRequirementMs",
            "gridGlobalRemeasures",
            "gridGlobalMetadataInvalidations",
            "gridGlobalOverflowTriggers",
            "columns",
            "children");

        AddNamedRules(
            rules,
            "GridSplitterWorkbenchScrollViewer",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "verticalOffset",
            "viewport",
            "contentViewport",
            "runtimeContentViewportRect",
            "contentType",
            "contentSummary",
            "contentSlot",
            "contentActual",
            "contentMeasureMs",
            "contentMeasureExclusiveMs",
            "runtimeMeasureOverrideMs",
            "runtimeArrangeOverrideMs",
            "runtimeResolveBarsMeasureMs",
            "runtimeResolveBarsMeasureLastTrace",
            "runtimeResolveBarsMeasureHottestTrace",
            "runtimeResolveBarsMeasureHottestMs",
            "runtimeResolveBarsArrangeMs",
            "runtimeMeasureContentMs",
            "runtimeUpdateScrollBarsMs",
            "runtimeArrangeContentMs");

        AddNamedRules(
            rules,
            "PrimaryNavigationPane",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "arrangeMs",
            "runtimeMeasureOverrideMs",
            "runtimeArrangeOverrideMs",
            "runtimeRenderMs",
            "runtimeTextureCacheHits",
            "runtimeTextureCacheMisses",
            "runtimeRenderStateCacheHits",
            "runtimeRenderStateCacheMisses",
            "runtimeRoundedGeometryCacheHits",
            "runtimeRoundedGeometryCacheMisses");

        AddNamedRules(
            rules,
            "PrimaryCanvasPane",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "arrangeMs",
            "runtimeMeasureOverrideMs",
            "runtimeArrangeOverrideMs",
            "runtimeRenderMs",
            "runtimeTextureCacheHits",
            "runtimeTextureCacheMisses",
            "runtimeRenderStateCacheHits",
            "runtimeRenderStateCacheMisses",
            "runtimeRoundedGeometryCacheHits",
            "runtimeRoundedGeometryCacheMisses");

        AddNamedRules(
            rules,
            "PrimaryCanvasRootGrid",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "gridRuntimeChildren",
            "gridGlobalMeasureMs",
            "gridGlobalArrangeMs",
            "columns",
            "children");

        AddNamedRules(
            rules,
            "PrimaryCanvasHeaderGrid",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "columns",
            "children");

        AddNamedRules(
            rules,
            "PrimaryCanvasHintGrid",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "columns",
            "children");

        AddNamedRules(
            rules,
            "PrimaryCanvasLowerGrid",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "columns",
            "children");

        AddNamedRules(
            rules,
            "PrimaryCanvasLowerLeftPanel",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "measureInvalidationLast",
            "measureInvalidationTopSources");

        AddNamedRules(
            rules,
            "PrimaryCanvasLowerRightPanel",
            "name",
            "slot",
            "actual",
            "renderSize",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "measureInvalidationLast",
            "measureInvalidationTopSources");

        AddElementTypeRules(
            rules,
            "StackPanel",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "orientation",
            "childSummary");

        AddElementTypeRules(
            rules,
            "TextBlock",
            "measureWork",
            "arrangeWork",
            "measureMs",
            "measureExclusiveMs",
            "arrangeMs",
            "text",
            "runtimeMeasureOverrideMs",
            "runtimeResolveLayoutMs",
            "runtimeResolveLayoutUncachedMs",
            "runtimeResolveLayoutCacheMisses");

        if (includePriorityRules)
        {
            AddNamedRules(
                rules,
                "NavigationSplitter",
                "measureInvalidationTopSources",
                "arrangeInvalidationTopSources",
                "renderInvalidationTopSources",
                "gridSplitterRuntimeKeyDownMs",
                "gridSplitterKeyDownMs");

            AddNamedRules(
                rules,
                "PrimaryEditorGrid",
                "measureInvalidationTopSources",
                "arrangeInvalidationTopSources",
                "gridGlobalMeasureCalls",
                "gridGlobalArrangeCalls",
                "gridGlobalPrepareMetadataCalls",
                "gridGlobalMeasureChildCalls",
                "gridGlobalResolveDefinitionCalls",
                "gridGlobalApplyRequirementCalls");

            AddNamedRules(
                rules,
                "GridSplitterWorkbenchScrollViewer",
                "runtimeMeasureOverrideCalls",
                "runtimeArrangeOverrideCalls",
                "runtimeResolveBarsMeasureCalls",
                "runtimeResolveBarsArrangeCalls",
                "runtimeMeasureContentCalls",
                "runtimeUpdateScrollBarsCalls",
                "runtimeArrangeContentCalls");
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
