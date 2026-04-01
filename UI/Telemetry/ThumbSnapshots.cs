namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Thumb drag telemetry snapshot.
/// </summary>
internal readonly record struct ThumbDragTelemetrySnapshot(
    int HandlePointerMoveCallCount,
    double HandlePointerMoveMilliseconds,
    double RaiseDragDeltaMilliseconds);