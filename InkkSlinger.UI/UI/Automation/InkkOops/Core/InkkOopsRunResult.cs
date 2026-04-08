using System;

namespace InkkSlinger;

public enum InkkOopsRunStatus
{
    Completed,
    Failed,
    Busy,
    NotFound
}

public sealed class InkkOopsRunResult
{
    public InkkOopsRunResult(
        InkkOopsRunStatus status,
        string scriptName,
        string artifactDirectory,
        int commandCount,
        int? failedCommandIndex = null,
        string? failedCommandDescription = null,
        InkkOopsFailureCategory failureCategory = InkkOopsFailureCategory.None,
        string? failureMessage = null,
        TimeSpan? duration = null)
    {
        Status = status;
        ScriptName = scriptName ?? string.Empty;
        ArtifactDirectory = artifactDirectory ?? string.Empty;
        CommandCount = commandCount;
        FailedCommandIndex = failedCommandIndex;
        FailedCommandDescription = failedCommandDescription ?? string.Empty;
        FailureCategory = failureCategory;
        FailureMessage = failureMessage ?? string.Empty;
        Duration = duration ?? TimeSpan.Zero;
    }

    public InkkOopsRunStatus Status { get; }

    public string ScriptName { get; }

    public string ArtifactDirectory { get; }

    public int CommandCount { get; }

    public int? FailedCommandIndex { get; }

    public string FailedCommandDescription { get; }

    public InkkOopsFailureCategory FailureCategory { get; }

    public string FailureMessage { get; }

    public TimeSpan Duration { get; }
}
