namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Hit test instrumentation snapshot.
/// </summary>
internal readonly record struct HitTestInstrumentationSnapshot(
    int ItemsPresenterNeighborProbes,
    int ItemsPresenterFullFallbackScans,
    int LegacyEnumerableFallbacks,
    int MonotonicPanelFastPathCount,
    int SimpleSlotHitCount,
    int TransformedBoundsHitCount,
    int ClipRejectCount,
    int VisibilityRejectCount,
    int PanelTraversalCount,
    int VisualTraversalZSortCount);