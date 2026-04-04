using System;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsFilterPolicy : IInkkOopsDiagnosticsFilterPolicy
{
    private static readonly InkkOopsDiagnosticsFilter ActionFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules =
        [
            new InkkOopsDiagnosticsFactRule { Key = "hovered", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true },
            new InkkOopsDiagnosticsFactRule { Key = "name", Comparison = InkkOopsDiagnosticsComparison.Contains, Value = "Playground" },
            new InkkOopsDiagnosticsFactRule { Key = "slot", DisplayNameContains = "Playground", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { Key = "actual", DisplayNameContains = "Playground", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { Key = "renderSize", DisplayNameContains = "Playground", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "Button", Key = "buttonDisplayText", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "Button", Key = "buttonLayoutSlot", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "Button", Key = "buttonIsMouseOver", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "ScrollViewer", Key = "slot", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "ScrollViewer", Key = "actual", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "ScrollViewer", Key = "renderSize", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "ScrollViewer", Key = "verticalOffset", Comparison = InkkOopsDiagnosticsComparison.GreaterThan, Value = 0 },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "ScrollViewer", Key = "viewport", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "ScrollViewer", Key = "contentViewport", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "ScrollViewer", Key = "runtimeContentViewportRect", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "ScrollViewer", Key = "contentSlot", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "ScrollViewer", Key = "contentActual", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { Key = "slot", DisplayNameContains = "PlaygroundExpander", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { Key = "actual", DisplayNameContains = "PlaygroundExpander", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { Key = "renderSize", DisplayNameContains = "PlaygroundExpander", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { Key = "isExpanded", DisplayNameContains = "PlaygroundExpander", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { Key = "expandDirection", DisplayNameContains = "PlaygroundExpander", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { Key = "hasHeaderElement", DisplayNameContains = "PlaygroundExpander", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { Key = "hasContent", DisplayNameContains = "PlaygroundExpander", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { Key = "headerRect", DisplayNameContains = "PlaygroundExpander", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { Key = "contentRect", DisplayNameContains = "PlaygroundExpander", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { Key = "measuredHeaderSize", DisplayNameContains = "PlaygroundExpander", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "TextBlock", Key = "text", Comparison = InkkOopsDiagnosticsComparison.Contains, Value = "Release checklist" },
            new InkkOopsDiagnosticsFactRule { ElementTypeName = "TextBlock", Key = "text", Comparison = InkkOopsDiagnosticsComparison.Contains, Value = "Composed header content" }
        ]
    };

    public InkkOopsDiagnosticsFilter CreateFilter(string artifactName)
    {
        return artifactName.StartsWith("action[", StringComparison.OrdinalIgnoreCase)
            ? ActionFilter
            : InkkOopsDiagnosticsFilter.None;
    }
}
