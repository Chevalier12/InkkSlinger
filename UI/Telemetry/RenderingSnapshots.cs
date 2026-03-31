namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Text rendering telemetry snapshots.
/// </summary>
internal readonly record struct UiTextRendererTimingSnapshot(
    long MeasureWidthElapsedTicks,
    long RenderTextElapsedTicks,
    long RenderTextDrawDispatchCallCount,
    long RenderTextDrawDispatchElapsedTicks,
    long RenderTextMeasureCallCount,
    long RenderTextMeasureElapsedTicks,
    long RenderTextPrepareCallCount,
    long RenderTextPrepareElapsedTicks,
    int CachedGlyphsCount,
    int RenderedGlyphsCount);

/// <summary>
/// Runtime font backend timing snapshot.
/// </summary>
internal readonly record struct UiRuntimeFontBackendTimingSnapshot(
    long MeasureElapsedTicks,
    long GetAdvanceWidthElapsedTicks,
    int MeasureCallCount,
    int GetAdvanceWidthCallCount);

/// <summary>
/// Text layout metrics snapshot.
/// </summary>
public readonly record struct TextLayoutMetricsSnapshot(
    int LayoutRequestCount,
    int LayoutCacheHitCount,
    int LayoutCacheMissCount,
    int LineCount,
    int GlyphCount);

/// <summary>
/// Text clipboard telemetry snapshot (simplified for TelemetryCatalog).
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
