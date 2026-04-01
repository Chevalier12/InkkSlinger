namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Canvas telemetry snapshot.
/// </summary>
internal readonly record struct CanvasTelemetrySnapshot(
    int MeasureCallCount,
    double MeasureMilliseconds,
    int MeasuredChildCount,
    int ArrangeCallCount,
    double ArrangeMilliseconds,
    int ArrangedChildCount);