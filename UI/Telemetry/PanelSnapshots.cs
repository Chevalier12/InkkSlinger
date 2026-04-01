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
/// WrapPanel telemetry snapshot.
/// </summary>
internal readonly record struct WrapPanelTelemetrySnapshot(
    long MeasureCallCount,
    double MeasureMilliseconds,
    long MeasuredChildCount,
    long MeasureSkippedChildCount,
    long MeasureHorizontalCount,
    long MeasureVerticalCount,
    long MeasureInfiniteLineLimitCount,
    long MeasureNaNLineLimitCount,
    long MeasureNonPositiveLineLimitCount,
    long MeasureWrapCount,
    long MeasureCommittedLineCount,
    long MeasureEmptyCount,
    long MeasureExplicitItemWidthCount,
    long MeasureAvailableWidthCount,
    long MeasureExplicitItemHeightCount,
    long MeasureAvailableHeightCount,
    long ArrangeCallCount,
    double ArrangeMilliseconds,
    long ArrangedChildCount,
    long ArrangeSkippedChildCount,
    long ArrangeHorizontalCount,
    long ArrangeVerticalCount,
    long ArrangeInfiniteLineLimitCount,
    long ArrangeNaNLineLimitCount,
    long ArrangeNonPositiveLineLimitCount,
    long ArrangeWrapCount,
    long ArrangeCommittedLineCount,
    long ArrangeEmptyCount,
    long GetChildSizeCallCount,
    double GetChildSizeMilliseconds,
    long GetChildSizeFromMeasureCount,
    long GetChildSizeFromArrangeCount,
    long GetChildSizeExplicitWidthCount,
    long GetChildSizeDesiredWidthCount,
    long GetChildSizeExplicitHeightCount,
    long GetChildSizeDesiredHeightCount);

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
