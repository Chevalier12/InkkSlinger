using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

public sealed class InkkOopsCalendarDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 47;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        _ = context;

        if (element is not Calendar calendar)
        {
            return;
        }

        var runtime = calendar.GetCalendarSnapshotForDiagnostics();
        var telemetry = Calendar.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("calendarHasPendingRefresh", runtime.HasPendingCalendarRefresh);
        builder.Add("calendarHasCompletedInitialRefresh", runtime.HasCompletedInitialCalendarRefresh);
        builder.Add("calendarHasDeferredInitialRefreshQueued", runtime.HasDeferredInitialCalendarRefreshQueued);
        builder.Add("calendarPendingManualRenderDiagnostics", runtime.PendingManualRenderDiagnostics);
        builder.Add("calendarManualRenderDiagnosticsLogged", runtime.ManualRenderDiagnosticsLogged);
        builder.Add("calendarRuntimeRequestRefreshCalls", runtime.RequestCalendarRefreshCallCount);
        builder.Add("calendarRuntimeRequestRefreshImmediateUpdate", runtime.RequestCalendarRefreshImmediateUpdateCount);
        builder.Add("calendarRuntimeRequestRefreshDeferredQueuePath", runtime.RequestCalendarRefreshDeferredQueuePathCount);
        builder.Add("calendarRuntimeEnsureCurrentCalls", runtime.EnsureCalendarViewCurrentCallCount);
        builder.Add("calendarRuntimeEnsureCurrentForcedUpdate", runtime.EnsureCalendarViewCurrentForcedUpdateCount);
        builder.Add("calendarRuntimeQueueInitialRefreshCalls", runtime.QueueInitialCalendarRefreshIfNeededCallCount);
        builder.Add("calendarRuntimeQueueInitialRefreshEnqueued", runtime.QueueInitialCalendarRefreshIfNeededEnqueuedCount);
        builder.Add("calendarRuntimeQueueInitialRefreshSkipped", runtime.QueueInitialCalendarRefreshIfNeededSkippedCount);
        builder.Add("calendarRuntimeNavigateMonthCalls", runtime.NavigateMonthCallCount);
        builder.Add("calendarRuntimeNavigateMonthBlocked", runtime.NavigateMonthBlockedCount);
        builder.Add("calendarRuntimeUpdateCalendarViewCalls", runtime.UpdateCalendarViewCallCount);
        builder.Add("calendarRuntimeUpdateCalendarViewMs", FormatMilliseconds(runtime.UpdateCalendarViewMilliseconds));
        builder.Add("calendarRuntimeRefreshCount", runtime.RefreshCount);
        builder.Add("calendarRuntimeSetLabelTextCalls", runtime.SetLabelTextCallCount);
        builder.Add("calendarRuntimeSetLabelTextChanged", runtime.SetLabelTextChangedCount);
        builder.Add("calendarRuntimeSetLabelTextNoOp", runtime.SetLabelTextNoOpCount);
        builder.Add("calendarRuntimeSetButtonTextCalls", runtime.SetButtonTextCallCount);
        builder.Add("calendarRuntimeSetButtonTextChanged", runtime.SetButtonTextChangedCount);
        builder.Add("calendarRuntimeSetButtonTextNoOp", runtime.SetButtonTextNoOpCount);
        builder.Add("calendarRuntimeSetButtonEnabledCalls", runtime.SetButtonEnabledCallCount);
        builder.Add("calendarRuntimeSetButtonEnabledChanged", runtime.SetButtonEnabledChangedCount);
        builder.Add("calendarRuntimeSetButtonEnabledNoOp", runtime.SetButtonEnabledNoOpCount);
        builder.Add("calendarRuntimeSetButtonBackgroundCalls", runtime.SetButtonBackgroundCallCount);
        builder.Add("calendarRuntimeSetButtonBackgroundChanged", runtime.SetButtonBackgroundChangedCount);
        builder.Add("calendarRuntimeSetButtonBackgroundNoOp", runtime.SetButtonBackgroundNoOpCount);
        builder.Add("calendarRuntimeSetButtonForegroundCalls", runtime.SetButtonForegroundCallCount);
        builder.Add("calendarRuntimeSetButtonForegroundChanged", runtime.SetButtonForegroundChangedCount);
        builder.Add("calendarRuntimeSetButtonForegroundNoOp", runtime.SetButtonForegroundNoOpCount);
        builder.Add("calendarRuntimeSetButtonBorderBrushCalls", runtime.SetButtonBorderBrushCallCount);
        builder.Add("calendarRuntimeSetButtonBorderBrushChanged", runtime.SetButtonBorderBrushChangedCount);
        builder.Add("calendarRuntimeSetButtonBorderBrushNoOp", runtime.SetButtonBorderBrushNoOpCount);
        AddRefreshFacts(builder, "calendarRuntimeLastRefresh", runtime.LastRefresh, runtime.LastRefreshTiming);
        AddRefreshFacts(builder, "calendarRuntimeTotalRefresh", runtime.TotalRefresh, runtime.TotalRefreshTiming);

        builder.Add("calendarRequestRefreshCalls", telemetry.RequestCalendarRefreshCallCount);
        builder.Add("calendarRequestRefreshImmediateUpdate", telemetry.RequestCalendarRefreshImmediateUpdateCount);
        builder.Add("calendarRequestRefreshDeferredQueuePath", telemetry.RequestCalendarRefreshDeferredQueuePathCount);
        builder.Add("calendarEnsureCurrentCalls", telemetry.EnsureCalendarViewCurrentCallCount);
        builder.Add("calendarEnsureCurrentForcedUpdate", telemetry.EnsureCalendarViewCurrentForcedUpdateCount);
        builder.Add("calendarQueueInitialRefreshCalls", telemetry.QueueInitialCalendarRefreshIfNeededCallCount);
        builder.Add("calendarQueueInitialRefreshEnqueued", telemetry.QueueInitialCalendarRefreshIfNeededEnqueuedCount);
        builder.Add("calendarQueueInitialRefreshSkipped", telemetry.QueueInitialCalendarRefreshIfNeededSkippedCount);
        builder.Add("calendarNavigateMonthCalls", telemetry.NavigateMonthCallCount);
        builder.Add("calendarNavigateMonthBlocked", telemetry.NavigateMonthBlockedCount);
        builder.Add("calendarUpdateCalendarViewCalls", telemetry.UpdateCalendarViewCallCount);
        builder.Add("calendarUpdateCalendarViewMs", FormatMilliseconds(telemetry.UpdateCalendarViewMilliseconds));
        builder.Add("calendarRefreshCount", telemetry.RefreshCount);
        builder.Add("calendarSetLabelTextCalls", telemetry.SetLabelTextCallCount);
        builder.Add("calendarSetLabelTextChanged", telemetry.SetLabelTextChangedCount);
        builder.Add("calendarSetLabelTextNoOp", telemetry.SetLabelTextNoOpCount);
        builder.Add("calendarSetButtonTextCalls", telemetry.SetButtonTextCallCount);
        builder.Add("calendarSetButtonTextChanged", telemetry.SetButtonTextChangedCount);
        builder.Add("calendarSetButtonTextNoOp", telemetry.SetButtonTextNoOpCount);
        builder.Add("calendarSetButtonEnabledCalls", telemetry.SetButtonEnabledCallCount);
        builder.Add("calendarSetButtonEnabledChanged", telemetry.SetButtonEnabledChangedCount);
        builder.Add("calendarSetButtonEnabledNoOp", telemetry.SetButtonEnabledNoOpCount);
        builder.Add("calendarSetButtonBackgroundCalls", telemetry.SetButtonBackgroundCallCount);
        builder.Add("calendarSetButtonBackgroundChanged", telemetry.SetButtonBackgroundChangedCount);
        builder.Add("calendarSetButtonBackgroundNoOp", telemetry.SetButtonBackgroundNoOpCount);
        builder.Add("calendarSetButtonForegroundCalls", telemetry.SetButtonForegroundCallCount);
        builder.Add("calendarSetButtonForegroundChanged", telemetry.SetButtonForegroundChangedCount);
        builder.Add("calendarSetButtonForegroundNoOp", telemetry.SetButtonForegroundNoOpCount);
        builder.Add("calendarSetButtonBorderBrushCalls", telemetry.SetButtonBorderBrushCallCount);
        builder.Add("calendarSetButtonBorderBrushChanged", telemetry.SetButtonBorderBrushChangedCount);
        builder.Add("calendarSetButtonBorderBrushNoOp", telemetry.SetButtonBorderBrushNoOpCount);
        AddRefreshFacts(builder, "calendarLastRefresh", telemetry.LastRefresh, telemetry.LastRefreshTiming);
        AddRefreshFacts(builder, "calendarTotalRefresh", telemetry.TotalRefresh, telemetry.TotalRefreshTiming);
    }

    private static void AddRefreshFacts(
        InkkOopsElementDiagnosticsBuilder builder,
        string prefix,
        CalendarRefreshDiagnostics refresh,
        CalendarRefreshTimingDiagnostics timing)
    {
        builder.Add($"{prefix}DayButtonTextChanged", refresh.DayButtonTextChangeCount);
        builder.Add($"{prefix}DayButtonEnabledChanged", refresh.DayButtonEnabledChangeCount);
        builder.Add($"{prefix}DayButtonBackgroundChanged", refresh.DayButtonBackgroundChangeCount);
        builder.Add($"{prefix}DayButtonForegroundChanged", refresh.DayButtonForegroundChangeCount);
        builder.Add($"{prefix}DayButtonBorderBrushChanged", refresh.DayButtonBorderBrushChangeCount);
        builder.Add($"{prefix}WeekDayLabelTextChanged", refresh.WeekDayLabelTextChangeCount);
        builder.Add($"{prefix}MonthLabelTextChanged", refresh.MonthLabelTextChangeCount);
        builder.Add($"{prefix}NavigationEnabledChanged", refresh.NavigationEnabledChangeCount);
        builder.Add($"{prefix}TotalMs", FormatTicks(timing.TotalElapsedTicks));
        builder.Add($"{prefix}MonthLabelMs", FormatTicks(timing.MonthLabelElapsedTicks));
        builder.Add($"{prefix}WeekDayLabelsMs", FormatTicks(timing.WeekDayLabelsElapsedTicks));
        builder.Add($"{prefix}DayLoopMs", FormatTicks(timing.DayLoopElapsedTicks));
        builder.Add($"{prefix}DayButtonDateSetupMs", FormatTicks(timing.DayButtonDateSetupElapsedTicks));
        builder.Add($"{prefix}DayButtonTextMs", FormatTicks(timing.DayButtonTextElapsedTicks));
        builder.Add($"{prefix}DayButtonEnabledMs", FormatTicks(timing.DayButtonEnabledElapsedTicks));
        builder.Add($"{prefix}DayButtonBackgroundMs", FormatTicks(timing.DayButtonBackgroundElapsedTicks));
        builder.Add($"{prefix}DayButtonForegroundMs", FormatTicks(timing.DayButtonForegroundElapsedTicks));
        builder.Add($"{prefix}DayButtonBorderBrushMs", FormatTicks(timing.DayButtonBorderBrushElapsedTicks));
        builder.Add($"{prefix}NavigationButtonsMs", FormatTicks(timing.NavigationButtonsElapsedTicks));
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }

    private static string FormatTicks(long ticks)
    {
        return (ticks * 1000d / System.Diagnostics.Stopwatch.Frequency).ToString("0.###");
    }
}