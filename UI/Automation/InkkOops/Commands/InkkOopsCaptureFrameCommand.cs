using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsCaptureFrameCommand : IInkkOopsCommand
{
    public InkkOopsCaptureFrameCommand(string artifactName)
    {
        ArtifactName = string.IsNullOrWhiteSpace(artifactName)
            ? throw new ArgumentException("Artifact name is required.", nameof(artifactName))
            : artifactName;
    }

    public string ArtifactName { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return $"CaptureFrame({ArtifactName})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.CaptureFrameAsync(ArtifactName, cancellationToken);
    }
}
