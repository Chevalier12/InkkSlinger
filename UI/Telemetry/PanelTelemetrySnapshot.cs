namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Panel telemetry snapshot.
/// </summary>
internal readonly record struct PanelTelemetrySnapshot(
    int MeasureCallCount,
    double MeasureMilliseconds,
    int MeasuredChildCount,
    int ArrangeCallCount,
    double ArrangeMilliseconds,
    int ArrangedChildCount);