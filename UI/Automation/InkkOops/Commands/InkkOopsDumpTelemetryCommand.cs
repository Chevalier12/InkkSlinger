using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsDumpTelemetryCommand : IInkkOopsCommand
{
    public InkkOopsDumpTelemetryCommand(string artifactName)
    {
        ArtifactName = string.IsNullOrWhiteSpace(artifactName)
            ? throw new ArgumentException("Artifact name is required.", nameof(artifactName))
            : artifactName;
    }

    public string ArtifactName { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return $"DumpTelemetry({ArtifactName})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.WriteTelemetryAsync(ArtifactName, cancellationToken);
    }
}
