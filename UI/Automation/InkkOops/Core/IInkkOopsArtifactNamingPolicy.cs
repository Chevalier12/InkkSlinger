using System;

namespace InkkSlinger;

public interface IInkkOopsArtifactNamingPolicy
{
    string CreateRunDirectoryName(string scriptName, DateTime timestampUtc);

    string CreateRecordingDirectoryName(DateTime timestampUtc);

    string GetCommandLogFileName();

    string GetResultFileName();

    string GetCommandDiagnosticsFileName(int commandIndex);

    string GetRecordingJsonFileName();

    string GetRecordedScriptFileName();

    string GetFrameCaptureFileName(string artifactName);

    string GetTelemetryFileName(string artifactName);

    string CreateReplayScriptName(string recordingPath);

    string CreateReplayFinalArtifactBaseName(string recordingPath);

    string SanitizePathSegment(string value, string fallbackValue);
}
