namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Aggregate Label telemetry snapshot.
/// </summary>
internal readonly record struct LabelTelemetrySnapshot(
    long ConstructorCallCount,
    long ResolveAccessKeyTargetCallCount,
    double ResolveAccessKeyTargetMilliseconds,
    long ResolveAccessKeyTargetReturnedTargetCount,
    long ResolveAccessKeyTargetReturnedSelfCount,
    long GetAutomationContentTextCallCount,
    double GetAutomationContentTextMilliseconds,
    long GetFallbackStyleCallCount,
    double GetFallbackStyleMilliseconds,
    long GetFallbackStyleCacheHitCount,
    long GetFallbackStyleCacheMissCount,
    long OnTargetChangedCallCount,
    double OnTargetChangedMilliseconds,
    long OnTargetChangedClearedOldTargetCount,
    long OnTargetChangedSkippedClearOldTargetCount,
    long OnTargetChangedAttachedNewTargetCount,
    long OnTargetChangedNoNewTargetCount,
    long BuildDefaultLabelStyleCallCount,
    double BuildDefaultLabelStyleMilliseconds,
    long BuildDefaultLabelTemplateCallCount,
    double BuildDefaultLabelTemplateMilliseconds,
    long BuildDefaultLabelTemplateBindCount,
    long ExtractAutomationTextCallCount,
    double ExtractAutomationTextMilliseconds,
    long ExtractAutomationTextNullPathCount,
    long ExtractAutomationTextStringPathCount,
    long ExtractAutomationTextAccessTextPathCount,
    long ExtractAutomationTextLabelPathCount,
    long ExtractAutomationTextTextBlockPathCount,
    long ExtractAutomationTextContentControlPathCount,
    long ExtractAutomationTextFallbackPathCount);

/// <summary>
/// Label per-instance runtime diagnostics snapshot.
/// </summary>
internal readonly record struct LabelRuntimeDiagnosticsSnapshot(
    bool HasTarget,
    string TargetType,
    bool HasContent,
    string ContentType,
    bool Focusable,
    string CurrentAutomationText,
    long ResolveAccessKeyTargetCallCount,
    double ResolveAccessKeyTargetMilliseconds,
    long ResolveAccessKeyTargetReturnedTargetCount,
    long ResolveAccessKeyTargetReturnedSelfCount,
    long GetAutomationContentTextCallCount,
    double GetAutomationContentTextMilliseconds,
    long GetFallbackStyleCallCount,
    double GetFallbackStyleMilliseconds,
    long GetFallbackStyleCacheHitCount,
    long GetFallbackStyleCacheMissCount,
    long OnTargetChangedCallCount,
    double OnTargetChangedMilliseconds,
    long OnTargetChangedClearedOldTargetCount,
    long OnTargetChangedSkippedClearOldTargetCount,
    long OnTargetChangedAttachedNewTargetCount,
    long OnTargetChangedNoNewTargetCount);