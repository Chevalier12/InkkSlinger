namespace InkkSlinger.UI.Telemetry;

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