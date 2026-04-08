namespace InkkSlinger.UI.Telemetry;

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