namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// TextBlock performance snapshot.
/// </summary>
public readonly record struct TextBlockPerformanceSnapshot(
    int LayoutCacheHitCount,
    int LayoutCacheMissCount,
    int RenderSampleCount,
    double LastRenderMilliseconds,
    double AverageRenderMilliseconds,
    double MaxRenderMilliseconds);

/// <summary>
/// TextBlock runtime diagnostics snapshot.
/// </summary>
internal readonly record struct TextBlockRuntimeDiagnosticsSnapshot(
    int MeasureOverrideCallCount,
    double MeasureOverrideMilliseconds,
    int EmptyMeasureCallCount,
    int SameTextSameWidthMeasureCallCount,
    int IntrinsicMeasurePathCallCount,
    int IntrinsicMeasureCacheHitCount,
    int IntrinsicMeasureCacheMissCount,
    int ResolveLayoutCallCount,
    int ResolveLayoutCacheHitCount,
    int ResolveLayoutCacheMissCount,
    int ResolveLayoutSameTextSameWidthCallCount,
    int TextPropertyChangeCount,
    int LayoutAffectingPropertyChangeCount,
    int LayoutCacheInvalidationCount,
    int IntrinsicMeasureInvalidationCount);