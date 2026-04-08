namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// UIElement render timing snapshot.
/// </summary>
internal readonly record struct UIElementRenderTimingSnapshot(
    long RenderSelfElapsedTicks,
    int RenderSelfCallCount,
    string HottestRenderSelfType,
    string HottestRenderSelfName,
    double HottestRenderSelfMilliseconds,
    string HottestRenderSelfTypeSummary);