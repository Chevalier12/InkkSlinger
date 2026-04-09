namespace InkkSlinger;

public sealed class InkkOopsCalendarDayButtonDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 46;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        _ = context;

        if (element is not CalendarDayButton button)
        {
            return;
        }

        var runtime = button.GetCalendarDayButtonSnapshotForDiagnostics();
        var telemetry = CalendarDayButton.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("calendarDayButtonRuntimeDayText", Escape(runtime.DayText));
        builder.Add("calendarDayButtonRuntimeContentType", runtime.ContentType);
        builder.Add("calendarDayButtonRuntimeHasDayTextPresenter", runtime.HasDayTextPresenter);
        builder.Add("calendarDayButtonRuntimeDependencyPropertyChangedCalls", runtime.DependencyPropertyChangedCallCount);
        builder.Add("calendarDayButtonRuntimeDayTextPropertyChanged", runtime.DayTextPropertyChangedCount);
        builder.Add("calendarDayButtonRuntimeContentPropertyChanged", runtime.ContentPropertyChangedCount);
        builder.Add("calendarDayButtonRuntimeOnApplyTemplateCalls", runtime.OnApplyTemplateCallCount);
        builder.Add("calendarDayButtonRuntimeOnApplyTemplateHasPresenter", runtime.OnApplyTemplateHasPresenterCount);
        builder.Add("calendarDayButtonRuntimeOnApplyTemplateFallbackContentSync", runtime.OnApplyTemplateFallbackContentSyncCount);
        builder.Add("calendarDayButtonRuntimeOnDayTextChangedCalls", runtime.OnDayTextChangedCallCount);
        builder.Add("calendarDayButtonRuntimeOnDayTextChangedMs", FormatMilliseconds(runtime.OnDayTextChangedMilliseconds));
        builder.Add("calendarDayButtonRuntimeUpdateTemplateDayTextCalls", runtime.UpdateTemplateDayTextCallCount);
        builder.Add("calendarDayButtonRuntimeUpdateTemplateDayTextMs", FormatMilliseconds(runtime.UpdateTemplateDayTextMilliseconds));
        builder.Add("calendarDayButtonRuntimeUpdateTemplatePresenterAttached", runtime.UpdateTemplateDayTextPresenterAttachedCount);
        builder.Add("calendarDayButtonRuntimeUpdateTemplatePresenterMissing", runtime.UpdateTemplateDayTextPresenterMissingCount);
        builder.Add("calendarDayButtonRuntimeSyncContentFromDayTextCalls", runtime.SyncContentFromDayTextCallCount);
        builder.Add("calendarDayButtonRuntimeSyncContentFromDayTextMs", FormatMilliseconds(runtime.SyncContentFromDayTextMilliseconds));
        builder.Add("calendarDayButtonRuntimeSyncContentFromDayTextNoOp", runtime.SyncContentFromDayTextNoOpCount);
        builder.Add("calendarDayButtonRuntimeSyncContentFromDayTextEmptyNoOp", runtime.SyncContentFromDayTextEmptyNoOpCount);
        builder.Add("calendarDayButtonRuntimeSyncContentFromDayTextWrite", runtime.SyncContentFromDayTextWriteCount);
        builder.Add("calendarDayButtonRuntimeSyncDayTextFromContentCalls", runtime.SyncDayTextFromContentCallCount);
        builder.Add("calendarDayButtonRuntimeSyncDayTextFromContentMs", FormatMilliseconds(runtime.SyncDayTextFromContentMilliseconds));
        builder.Add("calendarDayButtonRuntimeSyncDayTextFromContentIgnoredNonString", runtime.SyncDayTextFromContentIgnoredNonStringCount);
        builder.Add("calendarDayButtonRuntimeSyncDayTextFromContentNoOp", runtime.SyncDayTextFromContentNoOpCount);
        builder.Add("calendarDayButtonRuntimeSyncDayTextFromContentWrite", runtime.SyncDayTextFromContentWriteCount);
        builder.Add("calendarDayButtonRuntimeRenderCalls", runtime.RenderCallCount);
        builder.Add("calendarDayButtonRuntimeRenderMs", FormatMilliseconds(runtime.RenderMilliseconds));
        builder.Add("calendarDayButtonRuntimeNonEmptyRenderCalls", runtime.NonEmptyRenderCallCount);

        builder.Add("calendarDayButtonDependencyPropertyChangedCalls", telemetry.DependencyPropertyChangedCallCount);
        builder.Add("calendarDayButtonDayTextPropertyChanged", telemetry.DayTextPropertyChangedCount);
        builder.Add("calendarDayButtonContentPropertyChanged", telemetry.ContentPropertyChangedCount);
        builder.Add("calendarDayButtonOnApplyTemplateCalls", telemetry.OnApplyTemplateCallCount);
        builder.Add("calendarDayButtonOnApplyTemplateHasPresenter", telemetry.OnApplyTemplateHasPresenterCount);
        builder.Add("calendarDayButtonOnApplyTemplateFallbackContentSync", telemetry.OnApplyTemplateFallbackContentSyncCount);
        builder.Add("calendarDayButtonOnDayTextChangedCalls", telemetry.OnDayTextChangedCallCount);
        builder.Add("calendarDayButtonOnDayTextChangedMs", FormatMilliseconds(telemetry.OnDayTextChangedMilliseconds));
        builder.Add("calendarDayButtonUpdateTemplateDayTextCalls", telemetry.UpdateTemplateDayTextCallCount);
        builder.Add("calendarDayButtonUpdateTemplateDayTextMs", FormatMilliseconds(telemetry.UpdateTemplateDayTextMilliseconds));
        builder.Add("calendarDayButtonUpdateTemplatePresenterAttached", telemetry.UpdateTemplateDayTextPresenterAttachedCount);
        builder.Add("calendarDayButtonUpdateTemplatePresenterMissing", telemetry.UpdateTemplateDayTextPresenterMissingCount);
        builder.Add("calendarDayButtonSyncContentFromDayTextCalls", telemetry.SyncContentFromDayTextCallCount);
        builder.Add("calendarDayButtonSyncContentFromDayTextMs", FormatMilliseconds(telemetry.SyncContentFromDayTextMilliseconds));
        builder.Add("calendarDayButtonSyncContentFromDayTextNoOp", telemetry.SyncContentFromDayTextNoOpCount);
        builder.Add("calendarDayButtonSyncContentFromDayTextEmptyNoOp", telemetry.SyncContentFromDayTextEmptyNoOpCount);
        builder.Add("calendarDayButtonSyncContentFromDayTextWrite", telemetry.SyncContentFromDayTextWriteCount);
        builder.Add("calendarDayButtonSyncDayTextFromContentCalls", telemetry.SyncDayTextFromContentCallCount);
        builder.Add("calendarDayButtonSyncDayTextFromContentMs", FormatMilliseconds(telemetry.SyncDayTextFromContentMilliseconds));
        builder.Add("calendarDayButtonSyncDayTextFromContentIgnoredNonString", telemetry.SyncDayTextFromContentIgnoredNonStringCount);
        builder.Add("calendarDayButtonSyncDayTextFromContentNoOp", telemetry.SyncDayTextFromContentNoOpCount);
        builder.Add("calendarDayButtonSyncDayTextFromContentWrite", telemetry.SyncDayTextFromContentWriteCount);
        builder.Add("calendarDayButtonRenderCalls", telemetry.RenderCallCount);
        builder.Add("calendarDayButtonRenderMs", FormatMilliseconds(telemetry.RenderMilliseconds));
        builder.Add("calendarDayButtonNonEmptyRenderCalls", telemetry.NonEmptyRenderCallCount);
    }

    private static string Escape(string value)
    {
        return value.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }
}