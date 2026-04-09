namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Calendar aggregate telemetry snapshot.
/// </summary>
internal readonly record struct CalendarTelemetrySnapshot(
    int RequestCalendarRefreshCallCount,
    int RequestCalendarRefreshImmediateUpdateCount,
    int RequestCalendarRefreshDeferredQueuePathCount,
    int EnsureCalendarViewCurrentCallCount,
    int EnsureCalendarViewCurrentForcedUpdateCount,
    int QueueInitialCalendarRefreshIfNeededCallCount,
    int QueueInitialCalendarRefreshIfNeededEnqueuedCount,
    int QueueInitialCalendarRefreshIfNeededSkippedCount,
    int NavigateMonthCallCount,
    int NavigateMonthBlockedCount,
    int UpdateCalendarViewCallCount,
    double UpdateCalendarViewMilliseconds,
    int SetLabelTextCallCount,
    int SetLabelTextChangedCount,
    int SetLabelTextNoOpCount,
    int SetButtonTextCallCount,
    int SetButtonTextChangedCount,
    int SetButtonTextNoOpCount,
    int SetButtonEnabledCallCount,
    int SetButtonEnabledChangedCount,
    int SetButtonEnabledNoOpCount,
    int SetButtonBackgroundCallCount,
    int SetButtonBackgroundChangedCount,
    int SetButtonBackgroundNoOpCount,
    int SetButtonForegroundCallCount,
    int SetButtonForegroundChangedCount,
    int SetButtonForegroundNoOpCount,
    int SetButtonBorderBrushCallCount,
    int SetButtonBorderBrushChangedCount,
    int SetButtonBorderBrushNoOpCount,
    int RefreshCount,
    CalendarRefreshDiagnostics LastRefresh,
    CalendarRefreshDiagnostics TotalRefresh,
    CalendarRefreshTimingDiagnostics LastRefreshTiming,
    CalendarRefreshTimingDiagnostics TotalRefreshTiming);

/// <summary>
/// Calendar per-instance runtime diagnostics snapshot.
/// </summary>
internal readonly record struct CalendarRuntimeDiagnosticsSnapshot(
    bool HasPendingCalendarRefresh,
    bool HasCompletedInitialCalendarRefresh,
    bool HasDeferredInitialCalendarRefreshQueued,
    bool PendingManualRenderDiagnostics,
    bool ManualRenderDiagnosticsLogged,
    int RequestCalendarRefreshCallCount,
    int RequestCalendarRefreshImmediateUpdateCount,
    int RequestCalendarRefreshDeferredQueuePathCount,
    int EnsureCalendarViewCurrentCallCount,
    int EnsureCalendarViewCurrentForcedUpdateCount,
    int QueueInitialCalendarRefreshIfNeededCallCount,
    int QueueInitialCalendarRefreshIfNeededEnqueuedCount,
    int QueueInitialCalendarRefreshIfNeededSkippedCount,
    int NavigateMonthCallCount,
    int NavigateMonthBlockedCount,
    int UpdateCalendarViewCallCount,
    double UpdateCalendarViewMilliseconds,
    int SetLabelTextCallCount,
    int SetLabelTextChangedCount,
    int SetLabelTextNoOpCount,
    int SetButtonTextCallCount,
    int SetButtonTextChangedCount,
    int SetButtonTextNoOpCount,
    int SetButtonEnabledCallCount,
    int SetButtonEnabledChangedCount,
    int SetButtonEnabledNoOpCount,
    int SetButtonBackgroundCallCount,
    int SetButtonBackgroundChangedCount,
    int SetButtonBackgroundNoOpCount,
    int SetButtonForegroundCallCount,
    int SetButtonForegroundChangedCount,
    int SetButtonForegroundNoOpCount,
    int SetButtonBorderBrushCallCount,
    int SetButtonBorderBrushChangedCount,
    int SetButtonBorderBrushNoOpCount,
    int RefreshCount,
    CalendarRefreshDiagnostics LastRefresh,
    CalendarRefreshDiagnostics TotalRefresh,
    CalendarRefreshTimingDiagnostics LastRefreshTiming,
    CalendarRefreshTimingDiagnostics TotalRefreshTiming);

/// <summary>
/// Calendar diagnostics snapshot.
/// </summary>
internal readonly record struct CalendarDiagnosticsSnapshot(
    int RefreshCount,
    CalendarRefreshDiagnostics LastRefresh,
    CalendarRefreshDiagnostics Total,
    CalendarRefreshTimingDiagnostics LastRefreshTiming,
    CalendarRefreshTimingDiagnostics TotalRefreshTiming);

/// <summary>
/// Calendar refresh diagnostics.
/// </summary>
internal readonly record struct CalendarRefreshDiagnostics(
    int DayButtonTextChangeCount,
    int DayButtonEnabledChangeCount,
    int DayButtonBackgroundChangeCount,
    int DayButtonForegroundChangeCount,
    int DayButtonBorderBrushChangeCount,
    int WeekDayLabelTextChangeCount,
    int MonthLabelTextChangeCount,
    int NavigationEnabledChangeCount)
{
    public CalendarRefreshDiagnostics Add(CalendarRefreshDiagnostics other)
    {
        return new CalendarRefreshDiagnostics(
            DayButtonTextChangeCount + other.DayButtonTextChangeCount,
            DayButtonEnabledChangeCount + other.DayButtonEnabledChangeCount,
            DayButtonBackgroundChangeCount + other.DayButtonBackgroundChangeCount,
            DayButtonForegroundChangeCount + other.DayButtonForegroundChangeCount,
            DayButtonBorderBrushChangeCount + other.DayButtonBorderBrushChangeCount,
            WeekDayLabelTextChangeCount + other.WeekDayLabelTextChangeCount,
            MonthLabelTextChangeCount + other.MonthLabelTextChangeCount,
            NavigationEnabledChangeCount + other.NavigationEnabledChangeCount);
    }
}

/// <summary>
/// Calendar refresh timing diagnostics.
/// </summary>
internal readonly record struct CalendarRefreshTimingDiagnostics(
    long TotalElapsedTicks,
    long MonthLabelElapsedTicks,
    long WeekDayLabelsElapsedTicks,
    long DayLoopElapsedTicks,
    long DayButtonDateSetupElapsedTicks,
    long DayButtonTextElapsedTicks,
    long DayButtonEnabledElapsedTicks,
    long DayButtonBackgroundElapsedTicks,
    long DayButtonForegroundElapsedTicks,
    long DayButtonBorderBrushElapsedTicks,
    long NavigationButtonsElapsedTicks)
{
    public CalendarRefreshTimingDiagnostics Add(CalendarRefreshTimingDiagnostics other)
    {
        return new CalendarRefreshTimingDiagnostics(
            TotalElapsedTicks + other.TotalElapsedTicks,
            MonthLabelElapsedTicks + other.MonthLabelElapsedTicks,
            WeekDayLabelsElapsedTicks + other.WeekDayLabelsElapsedTicks,
            DayLoopElapsedTicks + other.DayLoopElapsedTicks,
            DayButtonDateSetupElapsedTicks + other.DayButtonDateSetupElapsedTicks,
            DayButtonTextElapsedTicks + other.DayButtonTextElapsedTicks,
            DayButtonEnabledElapsedTicks + other.DayButtonEnabledElapsedTicks,
            DayButtonBackgroundElapsedTicks + other.DayButtonBackgroundElapsedTicks,
            DayButtonForegroundElapsedTicks + other.DayButtonForegroundElapsedTicks,
            DayButtonBorderBrushElapsedTicks + other.DayButtonBorderBrushElapsedTicks,
            NavigationButtonsElapsedTicks + other.NavigationButtonsElapsedTicks);
    }
}