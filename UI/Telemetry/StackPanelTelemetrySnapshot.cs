namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// StackPanel telemetry snapshot.
/// </summary>
internal readonly record struct StackPanelTelemetrySnapshot(
    long MeasureCallCount,
    double MeasureMilliseconds,
    long MeasuredChildCount,
    long MeasureSkippedChildCount,
    long MeasureVerticalCount,
    long MeasureHorizontalCount,
    long MeasureEmptyCount,
    long MeasureInfiniteCrossAxisCount,
    long MeasureNaNCrossAxisCount,
    long MeasureNonPositiveCrossAxisCount,
    double MeasureTotalPrimaryDesired,
    double MeasureTotalCrossDesired,
    long ArrangeCallCount,
    double ArrangeMilliseconds,
    long ArrangedChildCount,
    long ArrangeSkippedChildCount,
    long ArrangeVerticalCount,
    long ArrangeHorizontalCount,
    long ArrangeEmptyCount,
    long ArrangeInfinitePrimarySizeCount,
    long ArrangeNaNPrimarySizeCount,
    long ArrangeNonPositivePrimarySizeCount,
    double ArrangeTotalPrimarySpan,
    double ArrangeTotalCrossSpan);

/// <summary>
/// StackPanel per-instance runtime diagnostics snapshot.
/// </summary>
internal readonly record struct StackPanelRuntimeDiagnosticsSnapshot(
    Orientation Orientation,
    int ChildCount,
    float DesiredWidth,
    float DesiredHeight,
    float RenderWidth,
    float RenderHeight,
    float ActualWidth,
    float ActualHeight,
    float PreviousAvailableWidth,
    float PreviousAvailableHeight,
    int MeasureCallCount,
    int MeasureWorkCount,
    int ArrangeCallCount,
    int ArrangeWorkCount,
    double MeasureMilliseconds,
    double MeasureExclusiveMilliseconds,
    double ArrangeMilliseconds,
    bool IsMeasureValid,
    bool IsArrangeValid);