namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Drop shadow effect timing snapshot.
/// </summary>
internal readonly record struct DropShadowEffectTimingSnapshot(
    long RenderElapsedTicks,
    long BlurPathElapsedTicks,
    long DrawBlurSlicesElapsedTicks,
    int RenderCallCount,
    int BlurPathCallCount,
    int CalendarDayRenderCallCount,
    int CalendarDayBlurPathCallCount);
