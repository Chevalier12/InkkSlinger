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

/// <summary>
/// StackPanel telemetry snapshot.
/// </summary>
internal readonly record struct StackPanelTelemetrySnapshot(
    int MeasureCallCount,
    double MeasureMilliseconds,
    int MeasuredChildCount,
    int ArrangeCallCount,
    double ArrangeMilliseconds,
    int ArrangedChildCount);

/// <summary>
/// Grid timing snapshot.
/// </summary>
internal readonly record struct GridTimingSnapshot(
    long MeasureOverrideElapsedTicks,
    long ArrangeOverrideElapsedTicks);

/// <summary>
/// UniformGrid timing snapshot.
/// </summary>
internal readonly record struct UniformGridTimingSnapshot(
    long MeasureOverrideElapsedTicks,
    long ArrangeOverrideElapsedTicks,
    long MeasureChildrenCacheElapsedTicks,
    long MeasureDimensionResolutionElapsedTicks,
    long MeasureAggregateCheckElapsedTicks,
    long MeasureChildLoopElapsedTicks,
    long MeasureChildMeasureElapsedTicks,
    int MeasureChildrenCacheRefreshCount,
    int MeasureAggregateReuseHitCount,
    int MeasureAggregateReuseMissCount,
    int MeasureChildReuseCount,
    int MeasureChildMeasureCount);
