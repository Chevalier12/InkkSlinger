namespace InkkSlinger;

public sealed class InkkOopsRuntimeOptions
{
    public string StartupScriptName { get; init; } = string.Empty;

    public string[] AdditionalScriptAssemblyPaths { get; init; } = [];

    public InkkOopsObjectObserver[] ObjectObservers { get; init; } = [];

    public int[] ActionDiagnosticsIndexes { get; init; } = [];

    public string NamedPipeName { get; init; } = string.Empty;

    public string ArtifactRoot { get; init; } = string.Empty;

    public bool RecordUserSession { get; init; }

    public string LaunchProjectPath { get; init; } = string.Empty;

    public string RecordingRoot { get; init; } = string.Empty;

    public string StartupRecordingPath { get; init; } = string.Empty;

    public bool DisableRetainedRenderList { get; init; }

    public bool DisableDirtyRegionRendering { get; init; }
}
