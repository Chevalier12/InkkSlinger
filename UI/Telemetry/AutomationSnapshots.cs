namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Automation metrics snapshot.
/// </summary>
public readonly record struct AutomationMetricsSnapshot(
    int PeerCount,
    int TreeRebuildCount,
    int EmittedEventCountLastFrame,
    int CoalescedEventDiscardCountLastFrame);
