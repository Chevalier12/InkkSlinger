using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsFilterPolicy : IInkkOopsDiagnosticsFilterPolicy
{
    private static readonly int[] TargetedActionIndexes =
    [
        97, 98, 99, 100, 101, 102, 103, 104
    ];

    private static readonly InkkOopsDiagnosticsFilter TargetedActionFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules = CreateSourcePropertyInspectorMaximizeRules()
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

    private static InkkOopsDiagnosticsFactRule[] CreateSourcePropertyInspectorMaximizeRules()
    {
        var rules = new List<InkkOopsDiagnosticsFactRule>
        {
            new() { DisplayNameContains = "SourcePropertyInspectorScrollViewer", Key = "verticalBarVisible", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new() { DisplayNameContains = "SourcePropertyInspectorScrollViewer", Key = "runtimeContentViewportRect", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new() { DisplayNameContains = "SourcePropertyInspectorScrollViewer", Key = "contentViewport", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new() { DisplayNameContains = "SourcePropertyInspectorScrollViewer", Key = "vbar_present", Comparison = InkkOopsDiagnosticsComparison.Exists }
        };

        AddNamedRules(
            rules,
            "EditorTabControl",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureWork",
            "arrangeWork",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls");

        AddNamedRules(
            rules,
            "SourceTab",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureWork",
            "arrangeWork",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls");

        AddNamedRules(
            rules,
            "SourceEditorPane",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureWork",
            "arrangeWork",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls",
            "userControlHasTemplateRoot",
            "userControlHasContentElement",
            "userControlContentElementType",
            "userControlRuntimeArrangeOverrideCalls",
            "userControlRuntimeRenderCalls");

        AddNamedRules(
            rules,
            "SourcePropertyInspectorBorder",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureWork",
            "arrangeWork",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls",
            "hasVisibleBackground",
            "hasVisibleBorder",
            "runtimeMeasureOverrideCalls",
            "runtimeArrangeOverrideCalls",
            "runtimeRenderCalls");

        AddNamedRules(
            rules,
            "SourcePropertyInspectorFilterBorder",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureWork",
            "arrangeWork",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls",
            "hasVisibleBackground",
            "hasVisibleBorder",
            "runtimeMeasureOverrideCalls",
            "runtimeArrangeOverrideCalls",
            "runtimeRenderCalls");

        AddNamedRules(
            rules,
            "SourcePropertyInspectorScrollViewer",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "measureWork",
            "arrangeWork",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls",
            "clipToBounds",
            "horizontalScrollBarVisibility",
            "verticalScrollBarVisibility",
            "horizontalOffset",
            "verticalOffset",
            "extent",
            "viewport",
            "contentViewport",
            "runtimeContentViewportRect",
            "contentType",
            "contentSlot",
            "contentActual",
            "contentArrangeWork",
            "contentArrangeValid",
            "verticalBarVisible",
            "runtimeShowVerticalBar",
            "runtimePreviousVerticalBar",
            "runtimeArrangeContentCalls",
            "runtimeArrangeContentTransformPath",
            "runtimeArrangeContentOffsetPath",
            "vbar_present",
            "vbar_actual",
            "vbar_viewportSize");

        AddNamedRules(
            rules,
            "SourcePropertyInspectorPropertiesHost",
            "name",
            "slot",
            "actual",
            "renderSize",
            "desired",
            "previousAvailable",
            "orientation",
            "children",
            "childSummary",
            "measureWork",
            "arrangeWork",
            "measureValid",
            "arrangeValid",
            "measureInvalidations",
            "arrangeInvalidations",
            "renderInvalidations",
            "measureInvalidationLast",
            "arrangeInvalidationLast",
            "renderInvalidationLast",
            "frameworkInvalidateVisualCalls");

        AddElementTypeRules(
            rules,
            "Grid",
            "rows",
            "columns",
            "children",
            "measureWork",
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
