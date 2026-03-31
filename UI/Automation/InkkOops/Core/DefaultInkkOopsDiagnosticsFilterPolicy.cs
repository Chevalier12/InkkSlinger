using System;

namespace InkkSlinger;

public sealed class DefaultInkkOopsDiagnosticsFilterPolicy : IInkkOopsDiagnosticsFilterPolicy
{
    private static readonly InkkOopsDiagnosticsFilter RecordingFinalFilter = new()
    {
        NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
        Rules =
        [
            new InkkOopsDiagnosticsFactRule { Key = "hovered", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true },
            new InkkOopsDiagnosticsFactRule { Key = "focused", Comparison = InkkOopsDiagnosticsComparison.Equal, Value = true },
            new InkkOopsDiagnosticsFactRule { Key = "measureWork", Comparison = InkkOopsDiagnosticsComparison.GreaterThan, Value = 1 },
            new InkkOopsDiagnosticsFactRule { Key = "arrangeWork", Comparison = InkkOopsDiagnosticsComparison.GreaterThan, Value = 1 },
            new InkkOopsDiagnosticsFactRule { Key = "measureInvalidations", Comparison = InkkOopsDiagnosticsComparison.GreaterThan, Value = 0 },
            new InkkOopsDiagnosticsFactRule { Key = "arrangeInvalidations", Comparison = InkkOopsDiagnosticsComparison.GreaterThan, Value = 0 },
            new InkkOopsDiagnosticsFactRule { Key = "renderInvalidations", Comparison = InkkOopsDiagnosticsComparison.GreaterThan, Value = 0 },
            new InkkOopsDiagnosticsFactRule { Key = "measureMs", Comparison = InkkOopsDiagnosticsComparison.GreaterThanOrEqual, Value = 0.1d },
            new InkkOopsDiagnosticsFactRule { Key = "measureExclusiveMs", Comparison = InkkOopsDiagnosticsComparison.GreaterThanOrEqual, Value = 0.1d },
            new InkkOopsDiagnosticsFactRule { Key = "arrangeMs", Comparison = InkkOopsDiagnosticsComparison.GreaterThanOrEqual, Value = 0.1d },
            new InkkOopsDiagnosticsFactRule { Key = "runtimeMeasureOverrideMs", Comparison = InkkOopsDiagnosticsComparison.GreaterThanOrEqual, Value = 0.1d },
            new InkkOopsDiagnosticsFactRule { Key = "runtimeTextPropertyChanges", Comparison = InkkOopsDiagnosticsComparison.GreaterThan, Value = 0 },
            new InkkOopsDiagnosticsFactRule { Key = "runtimeResolveLayoutCacheMisses", Comparison = InkkOopsDiagnosticsComparison.GreaterThan, Value = 0 },
            new InkkOopsDiagnosticsFactRule { Key = "canvasViewUpdateTelemetryCalls", Comparison = InkkOopsDiagnosticsComparison.GreaterThan, Value = 0 },
            new InkkOopsDiagnosticsFactRule { Key = "canvasViewSetTextChanges", Comparison = InkkOopsDiagnosticsComparison.GreaterThan, Value = 0 },
            new InkkOopsDiagnosticsFactRule { Key = "measureInvalidationTopSources", Comparison = InkkOopsDiagnosticsComparison.Contains, Value = "property:" },
            new InkkOopsDiagnosticsFactRule { Key = "renderText", Comparison = InkkOopsDiagnosticsComparison.Exists },
            new InkkOopsDiagnosticsFactRule { Key = "text", DisplayNameContains = "TextBlock#PositionValueText", Comparison = InkkOopsDiagnosticsComparison.Exists }
        ]
    };

    public InkkOopsDiagnosticsFilter CreateFilter(string artifactName)
    {
        return string.Equals(artifactName, "recording-final", StringComparison.OrdinalIgnoreCase)
            ? RecordingFinalFilter
            : InkkOopsDiagnosticsFilter.None;
    }
}