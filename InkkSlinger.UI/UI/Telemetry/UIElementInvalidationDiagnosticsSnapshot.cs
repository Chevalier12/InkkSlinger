namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// UIElement invalidation diagnostics snapshot.
/// </summary>
internal readonly record struct UIElementInvalidationDiagnosticsSnapshot(
    int DirectMeasureInvalidationCount,
    int PropagatedMeasureInvalidationCount,
    string LastMeasureInvalidationSummary,
    string TopMeasureInvalidationSources,
    int LastMeasureInvalidationLayoutFrame,
    int LastMeasureInvalidationDrawFrame,
    int DirectArrangeInvalidationCount,
    int PropagatedArrangeInvalidationCount,
    string LastArrangeInvalidationSummary,
    string TopArrangeInvalidationSources,
    int LastArrangeInvalidationLayoutFrame,
    int LastArrangeInvalidationDrawFrame,
    int DirectRenderInvalidationCount,
    int PropagatedRenderInvalidationCount,
    string LastRenderInvalidationSummary,
    string TopRenderInvalidationSources,
    int LastRenderInvalidationLayoutFrame,
    int LastRenderInvalidationDrawFrame)
{
    public static UIElementInvalidationDiagnosticsSnapshot Empty => new(
        0,
        0,
        "none",
        "none",
        -1,
        -1,
        0,
        0,
        "none",
        "none",
        -1,
        -1,
        0,
        0,
        "none",
        "none",
        -1,
        -1);
}