using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class InkkOopsHostConfiguration
{
    public required IInkkOopsScriptCatalog ScriptCatalog { get; init; }

    public required IInkkOopsArtifactNamingPolicy ArtifactNamingPolicy { get; init; }

    public required IInkkOopsDiagnosticsSerializer DiagnosticsSerializer { get; init; }

    public required IInkkOopsDiagnosticsFilterPolicy DiagnosticsFilterPolicy { get; init; }

    public required IReadOnlyList<IInkkOopsDiagnosticsContributor> DiagnosticsContributors { get; init; }

    public string DefaultNamedPipeName { get; init; } = string.Empty;

    public string DefaultArtifactRoot { get; init; } = string.Empty;

    public string DefaultRecordingRoot { get; init; } = string.Empty;

    public static InkkOopsHostConfiguration CreateDefault(System.Reflection.Assembly scriptAssembly)
    {
        ArgumentNullException.ThrowIfNull(scriptAssembly);

        var namingPolicy = new DefaultInkkOopsArtifactNamingPolicy();
        return new InkkOopsHostConfiguration
        {
            ScriptCatalog = new ReflectionInkkOopsScriptCatalog(scriptAssembly),
            ArtifactNamingPolicy = namingPolicy,
            DiagnosticsSerializer = new DefaultInkkOopsDiagnosticsSerializer(),
            DiagnosticsFilterPolicy = new DefaultInkkOopsDiagnosticsFilterPolicy(),
            DiagnosticsContributors =
            [
                new InkkOopsGenericElementDiagnosticsContributor(),
                new InkkOopsFrameworkElementDiagnosticsContributor(),
                new InkkOopsTextBlockDiagnosticsContributor(),
                new InkkOopsButtonDiagnosticsContributor(),
                new InkkOopsGridSplitterDiagnosticsContributor(),
                new InkkOopsExpanderDiagnosticsContributor(),
                new InkkOopsScrollViewerDiagnosticsContributor(),
            ],
            DefaultNamedPipeName = "InkkOops",
            DefaultArtifactRoot = "artifacts/inkkoops",
            DefaultRecordingRoot = "artifacts/inkkoops-recordings"
        };
    }
}
