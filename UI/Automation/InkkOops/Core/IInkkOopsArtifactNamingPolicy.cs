using System;

namespace InkkSlinger;

public interface IInkkOopsArtifactNamingPolicy
{
    string CreateRunDirectoryName(string scriptName, DateTime timestampUtc);

    string CreateRecordingDirectoryName(DateTime timestampUtc);

    string GetActionLogFileName();

    string GetResultFileName();

    string GetRecordingJsonFileName();

    string GetRecordingInkkrFileName();

    string GetFrameCaptureFileName(string artifactName);

    string GetTelemetryFileName(string artifactName);

    string SanitizePathSegment(string value, string fallbackValue);
}
