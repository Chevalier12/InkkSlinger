namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Button timing telemetry snapshot.
/// </summary>
internal readonly record struct ButtonTimingSnapshot(
    long MeasureOverrideElapsedTicks,
    long RenderElapsedTicks,
    long ResolveTextLayoutElapsedTicks,
    long RenderChromeElapsedTicks,
    long RenderTextPreparationElapsedTicks,
    long RenderTextDrawDispatchElapsedTicks,
    int RenderTextPreparationCallCount,
    int RenderTextDrawDispatchCallCount,
    int ContentPropertyChangedCount,
    int TextLayoutCacheHitCount,
    int TextLayoutCacheMissCount,
    int IntrinsicNoWrapMeasureCacheHitCount,
    int IntrinsicNoWrapMeasureCacheMissCount,
    int TextLayoutInvalidationCount,
    int IntrinsicNoWrapMeasureInvalidationCount,
    int PlainTextMeasureFastPathCount,
    int IntrinsicNoWrapMeasurePathCount,
    int TextLayoutMeasurePathCount);