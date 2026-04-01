namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Calendar day button timing snapshot.
/// </summary>
internal readonly record struct CalendarDayButtonTimingSnapshot(
    long RenderElapsedTicks,
    int RenderCallCount,
    int NonEmptyRenderCallCount);