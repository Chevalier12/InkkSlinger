using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class InkkOopsHostConfiguration
{
    public required IInkkOopsScriptCatalog ScriptCatalog { get; init; }

    public required IInkkOopsArtifactNamingPolicy ArtifactNamingPolicy { get; init; }

    public required IInkkOopsReplayPostamblePolicy ReplayPostamblePolicy { get; init; }

    public required IInkkOopsDiagnosticsSerializer DiagnosticsSerializer { get; init; }

    public required IInkkOopsDiagnosticsFilterPolicy DiagnosticsFilterPolicy { get; init; }

    public required IReadOnlyList<IInkkOopsDiagnosticsContributor> DiagnosticsContributors { get; init; }

    public required IReadOnlyList<IInkkOopsSemanticLogContributor> SemanticLogContributors { get; init; }

    public string DefaultNamedPipeName { get; init; } = string.Empty;

    public string DefaultArtifactRoot { get; init; } = string.Empty;

    public string DefaultRecordingRoot { get; init; } = string.Empty;

    public static InkkOopsHostConfiguration CreateDefault(System.Reflection.Assembly scriptAssembly)
    {
        ArgumentNullException.ThrowIfNull(scriptAssembly);

        var namingPolicy = new DefaultInkkOopsArtifactNamingPolicy();
        var semanticLogContributors = new InkkOopsSemanticLogContributorRegistry()
            .Register<Expander>(InkkOopsSemanticLogTarget.Owner, static expander => expander.IsExpanded)
            .Register<Expander>(InkkOopsSemanticLogTarget.Owner, static expander => expander.ExpandDirection)
            .Register<TextBlock>(InkkOopsSemanticLogTarget.Hovered, static textBlock => textBlock.Text)
            .Register<Button>(InkkOopsSemanticLogTarget.Hovered, static button => button.Content)
            .Register<Button>(InkkOopsSemanticLogTarget.Hovered, static button => button.IsMouseOver)
            .Build();
        return new InkkOopsHostConfiguration
        {
            ScriptCatalog = new ReflectionInkkOopsScriptCatalog(scriptAssembly),
            ArtifactNamingPolicy = namingPolicy,
            ReplayPostamblePolicy = new DefaultInkkOopsReplayPostamblePolicy(),
            DiagnosticsSerializer = new DefaultInkkOopsDiagnosticsSerializer(),
            DiagnosticsFilterPolicy = new DefaultInkkOopsDiagnosticsFilterPolicy(),
            DiagnosticsContributors =
            [
                new InkkOopsGenericElementDiagnosticsContributor(),
                new InkkOopsFrameworkElementDiagnosticsContributor(),
                new InkkOopsTextBlockDiagnosticsContributor(),
                new InkkOopsButtonDiagnosticsContributor(),
                new InkkOopsExpanderDiagnosticsContributor(),
                new InkkOopsScrollViewerDiagnosticsContributor(),
            ],
            SemanticLogContributors = semanticLogContributors,
            DefaultNamedPipeName = "InkkOops",
            DefaultArtifactRoot = "artifacts/inkkoops",
            DefaultRecordingRoot = "artifacts/inkkoops-recordings"
        };
    }
}
