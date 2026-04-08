namespace InkkSlinger;

public sealed class InkkOopsPipeRequest
{
    public string ScriptName { get; set; } = string.Empty;

    public int[] ActionDiagnosticsIndexes { get; set; } = [];

    public int TimeoutMilliseconds { get; set; }

    public string ArtifactRootOverride { get; set; } = string.Empty;
}

public sealed class InkkOopsPipeResponse
{
    public string Status { get; set; } = string.Empty;

    public string ScriptName { get; set; } = string.Empty;

    public string ArtifactDirectory { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
