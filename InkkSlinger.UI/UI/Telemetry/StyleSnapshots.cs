namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Style telemetry snapshot for style application tracking.
/// </summary>
internal readonly record struct StyleTelemetrySnapshot(
    long ApplyCallCount,
    double ApplyMilliseconds,
    double ApplySettersMilliseconds,
    double ApplyTriggersMilliseconds,
    double CollectTriggeredValuesMilliseconds,
    long TriggerMatchCount,
    long MatchedTriggerCount,
    long SetStyleValueCount,
    long SetStyleTriggerValueCount,
    long ClearStyleTriggerValueCount,
    double ApplyTriggerActionsMilliseconds,
    long InvokeActionsCount,
    double InvokeActionsMilliseconds);

/// <summary>
/// Visual state telemetry snapshot.
/// </summary>
internal readonly record struct VisualStateTelemetrySnapshot(
    long GoToStateCallCount,
    double GoToStateMilliseconds,
    long GoToElementStateCallCount,
    double GoToElementStateMilliseconds,
    long TryGoToStateCallCount,
    double TryGoToStateMilliseconds,
    long MatchedGroupCount,
    long GroupGoToStateCallCount,
    double GroupGoToStateMilliseconds,
    double GroupApplySettersMilliseconds,
    double GroupStoryboardMilliseconds,
    long SetTemplateTriggerValueCount,
    long ClearTemplateTriggerValueCount,
    long ClearStateCallCount);

/// <summary>
/// Template trigger engine telemetry snapshot.
/// </summary>
internal readonly record struct TemplateTriggerTelemetrySnapshot(
    long ApplyCallCount,
    double ApplyMilliseconds,
    long ReapplyCallCount,
    double ReapplyMilliseconds,
    long TriggerMatchCount,
    long MatchedTriggerCount,
    double TriggerMatchMilliseconds,
    long SetterResolveCount,
    double SetterResolveMilliseconds,
    long SetTemplateTriggerValueCount,
    double SetTemplateTriggerValueMilliseconds,
    long ClearTemplateTriggerValueCount,
    double ClearTemplateTriggerValueMilliseconds,
    double ApplyActionsMilliseconds,
    long InvokeActionsCount,
    double InvokeActionsMilliseconds,
    double PrewarmStoryboardMilliseconds,
    double PrewarmSetterMilliseconds);
