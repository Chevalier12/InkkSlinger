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

/// <summary>
/// UIElement render timing snapshot.
/// </summary>
internal readonly record struct UIElementRenderTimingSnapshot(
    long RenderSelfElapsedTicks,
    int RenderSelfCallCount,
    string HottestRenderSelfType,
    string HottestRenderSelfName,
    double HottestRenderSelfMilliseconds,
    string HottestRenderSelfTypeSummary);

/// <summary>
/// Value changed routed event telemetry snapshot.
/// </summary>
internal readonly record struct ValueChangedRoutedEventTelemetrySnapshot(
    int RaiseCount,
    double RaiseMilliseconds,
    double RouteBuildMilliseconds,
    double RouteTraverseMilliseconds,
    double ClassHandlerMilliseconds,
    double InstanceDispatchMilliseconds,
    double InstancePrepareMilliseconds,
    double InstanceInvokeMilliseconds,
    int MaxRouteLength);

/// <summary>
/// UIElement invalidation diagnostics snapshot.
/// </summary>
internal readonly record struct UIElementInvalidationDiagnosticsSnapshot(
    int DirectMeasureInvalidationCount,
    int PropagatedMeasureInvalidationCount,
    string LastMeasureInvalidationSummary,
    string TopMeasureInvalidationSources,
    int LastMeasureInvalidationLayoutFrame,
    int LastMeasureInvalidationDrawFrame,
    int DirectArrangeInvalidationCount,
    int PropagatedArrangeInvalidationCount,
    string LastArrangeInvalidationSummary,
    string TopArrangeInvalidationSources,
    int LastArrangeInvalidationLayoutFrame,
    int LastArrangeInvalidationDrawFrame,
    int DirectRenderInvalidationCount,
    int PropagatedRenderInvalidationCount,
    string LastRenderInvalidationSummary,
    string TopRenderInvalidationSources,
    int LastRenderInvalidationLayoutFrame,
    int LastRenderInvalidationDrawFrame)
{
    public static UIElementInvalidationDiagnosticsSnapshot Empty => new(
        0,
        0,
        "none",
        "none",
        -1,
        -1,
        0,
        0,
        "none",
        "none",
        -1,
        -1,
        0,
        0,
        "none",
        "none",
        -1,
        -1);
}

/// <summary>
/// Framework layout timing snapshot.
/// </summary>
internal readonly record struct FrameworkLayoutTimingSnapshot(
    long MeasureElapsedTicks,
    long MeasureExclusiveElapsedTicks,
    long ArrangeElapsedTicks,
    string HottestMeasureElementType,
    string HottestMeasureElementName,
    long HottestMeasureElapsedTicks,
    string HottestArrangeElementType,
    string HottestArrangeElementName,
    long HottestArrangeElapsedTicks);
