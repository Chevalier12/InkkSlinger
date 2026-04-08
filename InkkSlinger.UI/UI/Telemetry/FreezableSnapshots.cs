namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Freezable telemetry snapshot.
/// </summary>
internal readonly record struct FreezableTelemetrySnapshot(
    int BatchBeginCount,
    int BatchEndCount,
    int PendingChangedDuringBatchCount,
    int OnChangedCallCount,
    int EndBatchFlushCount,
    double OnChangedMilliseconds,
    double EndBatchMilliseconds,
    string HottestOnChangedType,
    double HottestOnChangedMilliseconds,
    string HottestEndBatchType,
    double HottestEndBatchMilliseconds);
