namespace InkkSlinger;

public sealed class InkkSlingerOptions
{
    public string WindowTitle { get; init; } = "InkkSlinger";

    public bool FpsEnabled { get; init; }

    public bool LoadApplicationResources { get; init; } = true;

    public string? ApplicationResourcesPath { get; init; }

    public bool IsMouseVisible { get; init; } = true;

    public bool AllowUserResizing { get; init; } = true;

    public int InitialWindowWidth { get; init; } = 1280;

    public int InitialWindowHeight { get; init; } = 820;

    public InkkOopsRuntimeOptions InkkOopsRuntimeOptions { get; init; } = new();
}