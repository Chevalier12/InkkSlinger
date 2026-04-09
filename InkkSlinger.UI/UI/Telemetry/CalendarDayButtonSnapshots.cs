namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Calendar day button aggregate telemetry snapshot.
/// </summary>
internal readonly record struct CalendarDayButtonTelemetrySnapshot(
    int DependencyPropertyChangedCallCount,
    int DayTextPropertyChangedCount,
    int ContentPropertyChangedCount,
    int OnApplyTemplateCallCount,
    int OnApplyTemplateHasPresenterCount,
    int OnApplyTemplateFallbackContentSyncCount,
    int OnDayTextChangedCallCount,
    double OnDayTextChangedMilliseconds,
    int UpdateTemplateDayTextCallCount,
    double UpdateTemplateDayTextMilliseconds,
    int UpdateTemplateDayTextPresenterAttachedCount,
    int UpdateTemplateDayTextPresenterMissingCount,
    int SyncContentFromDayTextCallCount,
    double SyncContentFromDayTextMilliseconds,
    int SyncContentFromDayTextNoOpCount,
    int SyncContentFromDayTextEmptyNoOpCount,
    int SyncContentFromDayTextWriteCount,
    int SyncDayTextFromContentCallCount,
    double SyncDayTextFromContentMilliseconds,
    int SyncDayTextFromContentIgnoredNonStringCount,
    int SyncDayTextFromContentNoOpCount,
    int SyncDayTextFromContentWriteCount,
    int RenderCallCount,
    double RenderMilliseconds,
    int NonEmptyRenderCallCount);

/// <summary>
/// Calendar day button per-instance runtime diagnostics snapshot.
/// </summary>
internal readonly record struct CalendarDayButtonRuntimeDiagnosticsSnapshot(
    string DayText,
    string ContentType,
    bool HasDayTextPresenter,
    int DependencyPropertyChangedCallCount,
    int DayTextPropertyChangedCount,
    int ContentPropertyChangedCount,
    int OnApplyTemplateCallCount,
    int OnApplyTemplateHasPresenterCount,
    int OnApplyTemplateFallbackContentSyncCount,
    int OnDayTextChangedCallCount,
    double OnDayTextChangedMilliseconds,
    int UpdateTemplateDayTextCallCount,
    double UpdateTemplateDayTextMilliseconds,
    int UpdateTemplateDayTextPresenterAttachedCount,
    int UpdateTemplateDayTextPresenterMissingCount,
    int SyncContentFromDayTextCallCount,
    double SyncContentFromDayTextMilliseconds,
    int SyncContentFromDayTextNoOpCount,
    int SyncContentFromDayTextEmptyNoOpCount,
    int SyncContentFromDayTextWriteCount,
    int SyncDayTextFromContentCallCount,
    double SyncDayTextFromContentMilliseconds,
    int SyncDayTextFromContentIgnoredNonStringCount,
    int SyncDayTextFromContentNoOpCount,
    int SyncDayTextFromContentWriteCount,
    int RenderCallCount,
    double RenderMilliseconds,
    int NonEmptyRenderCallCount);

/// <summary>
/// Calendar day button timing snapshot.
/// </summary>
internal readonly record struct CalendarDayButtonTimingSnapshot(
    long RenderElapsedTicks,
    int RenderCallCount,
    int NonEmptyRenderCallCount);