namespace InkkSlinger.UI.Telemetry;

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