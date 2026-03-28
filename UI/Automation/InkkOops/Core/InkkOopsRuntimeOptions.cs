namespace InkkSlinger;

public sealed class InkkOopsRuntimeOptions
{
    public string StartupScriptName { get; init; } = string.Empty;

    public string NamedPipeName { get; init; } = "InkkOops";

    public string ArtifactRoot { get; init; } = "artifacts/inkkoops";

    public bool RecordUserSession { get; init; }

    public string RecordingRoot { get; init; } = "artifacts/inkkoops-recordings";

    public string StartupRecordingPath { get; init; } = string.Empty;

    public bool DisableRetainedRenderList { get; init; }

    public bool DisableDirtyRegionRendering { get; init; }
}
