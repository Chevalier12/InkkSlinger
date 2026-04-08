namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Text renderer timing snapshot.
/// </summary>
internal readonly record struct UiTextRendererTimingSnapshot(
    long MeasureWidthElapsedTicks,
    int MeasureWidthCallCount,
    long GetLineHeightElapsedTicks,
    int GetLineHeightCallCount,
    long DrawStringElapsedTicks,
    int DrawStringCallCount,
    int TypefaceCacheHitCount,
    int TypefaceCacheMissCount,
    int MetricsCacheHitCount,
    int MetricsCacheMissCount,
    int LineHeightCacheHitCount,
    int LineHeightCacheMissCount,
    string HottestDrawStringText,
    string HottestDrawStringTypography,
    double HottestDrawStringMilliseconds,
    string HottestMeasureWidthText,
    string HottestMeasureWidthTypography,
    double HottestMeasureWidthMilliseconds,
    string HottestLineHeightTypography,
    double HottestLineHeightMilliseconds);

/// <summary>
/// Runtime font backend timing snapshot.
/// </summary>
internal readonly record struct UiRuntimeFontBackendTimingSnapshot(
    long MeasureElapsedTicks,
    int MeasureCallCount,
    long RasterizeElapsedTicks,
    int RasterizeCallCount,
    int FaceCacheHitCount,
    int FaceCacheMissCount,
    int FaceSizeReuseHitCount,
    int FaceSizeChangeCount,
    int GlyphAdvanceCacheHitCount,
    int GlyphAdvanceCacheMissCount,
    int KerningCacheHitCount,
    int KerningCacheMissCount,
    int VerticalMetricsCacheHitCount,
    int VerticalMetricsCacheMissCount);

/// <summary>
/// Text layout metrics snapshot.
/// </summary>
public readonly record struct TextLayoutMetricsSnapshot(
    int LayoutRequestCount,
    int CacheHitCount,
    int CacheMissCount,
    int BuildCount,
    int NoWrapBuildCount,
    int WrappedBuildCount,
    int TotalMeasuredTextLength,
    int TotalProducedLineCount,
    int CacheEntryCount,
    long LayoutElapsedTicks,
    long BuildElapsedTicks);

/// <summary>
/// Clipboard telemetry snapshots.
/// </summary>
public readonly record struct TextClipboardTelemetrySnapshot(
    long SyncCallCount,
    long AsyncCallCount,
    int LastSyncTextLength);

/// <summary>
/// Text clipboard read telemetry snapshot (simplified for TelemetryCatalog).
/// </summary>
public readonly record struct TextClipboardReadTelemetrySnapshot(
    string Text,
    int TextLength);
