using System;

namespace InkkSlinger;

public sealed class DefaultInkkOopsReplayPostamblePolicy : IInkkOopsReplayPostamblePolicy
{
    public void Apply(InkkOopsScriptBuilder builder, string recordingPath, IInkkOopsArtifactNamingPolicy namingPolicy)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(namingPolicy);

        var artifactBaseName = namingPolicy.CreateReplayFinalArtifactBaseName(recordingPath);
        builder
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .CaptureFrame(artifactBaseName)
            .DumpTelemetry(artifactBaseName);
    }
}
