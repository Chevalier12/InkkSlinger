using System;
using System.IO;

namespace InkkSlinger;

public sealed class DefaultInkkOopsArtifactNamingPolicy : IInkkOopsArtifactNamingPolicy
{
    public string CreateRunDirectoryName(string scriptName, DateTime timestampUtc)
    {
        return $"{timestampUtc:yyyyMMdd-HHmmssfff}-{SanitizePathSegment(scriptName, "script")}";
    }

    public string CreateRecordingDirectoryName(DateTime timestampUtc)
    {
        return $"{timestampUtc:yyyyMMdd-HHmmssfff}-recorded-session";
    }

    public string GetActionLogFileName() => "action.log";

    public string GetResultFileName() => "result.json";

    public string GetRecordingJsonFileName() => "recording.json";

    public string GetRecordedScriptFileName() => "recorded-script.txt";

    public string GetFrameCaptureFileName(string artifactName)
    {
        return EnsureExtension(artifactName, ".png", "artifact");
    }

    public string GetTelemetryFileName(string artifactName)
    {
        return EnsureExtension(artifactName, ".txt", "artifact");
    }

    public string CreateReplayScriptName(string recordingPath)
    {
        var scriptName = Path.GetFileNameWithoutExtension(recordingPath);
        return $"recording-replay-{SanitizePathSegment(scriptName, "recording")}";
    }

    public string SanitizePathSegment(string value, string fallbackValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallbackValue;
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return value;
    }

    private string EnsureExtension(string artifactName, string extension, string fallbackValue)
    {
        artifactName = SanitizePathSegment(artifactName, fallbackValue);
        return artifactName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? artifactName
            : artifactName + extension;
    }
}
