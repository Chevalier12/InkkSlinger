namespace InkkSlinger.UI.Telemetry;

using InkkSlinger;

/// <summary>
/// Expander timing telemetry snapshot.
/// </summary>
internal readonly record struct ExpanderTimingSnapshot(
    long MeasureOverrideCallCount,
    double MeasureOverrideMilliseconds,
    long HeaderMeasureCount,
    long ContentMeasuredWhenExpandedCount,
    long ContentMeasuredWhenCollapsedCount,
    long ArrangeOverrideCallCount,
    double ArrangeOverrideMilliseconds,
    long ExpandDirectionDownCount,
    long ExpandDirectionUpCount,
    long ExpandDirectionLeftCount,
    long ExpandDirectionRightCount,
    long RenderCallCount,
    double RenderMilliseconds,
    long ExpandCount,
    long CollapseCount,
    long HeaderPointerDownCount,
    long HeaderPointerUpToggleCount,
    long HeaderUpdateCount);

/// <summary>
/// Button timing telemetry snapshot.
/// </summary>
internal readonly record struct ButtonTimingSnapshot(
    long MeasureOverrideElapsedTicks,
    long RenderElapsedTicks,
    long ResolveTextLayoutElapsedTicks,
    long RenderChromeElapsedTicks,
    long RenderTextPreparationElapsedTicks,
    long RenderTextDrawDispatchElapsedTicks,
    int RenderTextPreparationCallCount,
    int RenderTextDrawDispatchCallCount,
    int ContentPropertyChangedCount,
    int TextLayoutCacheHitCount,
    int TextLayoutCacheMissCount,
    int IntrinsicNoWrapMeasureCacheHitCount,
    int IntrinsicNoWrapMeasureCacheMissCount,
    int TextLayoutInvalidationCount,
    int IntrinsicNoWrapMeasureInvalidationCount,
    int PlainTextMeasureFastPathCount,
    int IntrinsicNoWrapMeasurePathCount,
    int TextLayoutMeasurePathCount);

/// <summary>
/// Thumb drag telemetry snapshot.
/// </summary>
internal readonly record struct ThumbDragTelemetrySnapshot(
    int HandlePointerMoveCallCount,
    double HandlePointerMoveMilliseconds,
    double RaiseDragDeltaMilliseconds);

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

/// <summary>
/// Calendar day button timing snapshot.
/// </summary>
internal readonly record struct CalendarDayButtonTimingSnapshot(
    long RenderElapsedTicks,
    int RenderCallCount,
    int NonEmptyRenderCallCount);

/// <summary>
/// TextBlock performance snapshot.
/// </summary>
public readonly record struct TextBlockPerformanceSnapshot(
    int LayoutCacheHitCount,
    int LayoutCacheMissCount,
    int RenderSampleCount,
    double LastRenderMilliseconds,
    double AverageRenderMilliseconds,
    double MaxRenderMilliseconds);

/// <summary>
/// TextBlock runtime diagnostics snapshot.
/// </summary>
internal readonly record struct TextBlockRuntimeDiagnosticsSnapshot(
    int MeasureOverrideCallCount,
    double MeasureOverrideMilliseconds,
    int EmptyMeasureCallCount,
    int SameTextSameWidthMeasureCallCount,
    int IntrinsicMeasurePathCallCount,
    int IntrinsicMeasureCacheHitCount,
    int IntrinsicMeasureCacheMissCount,
    int ResolveLayoutCallCount,
    int ResolveLayoutCacheHitCount,
    int ResolveLayoutCacheMissCount,
    int ResolveLayoutSameTextSameWidthCallCount,
    int TextPropertyChangeCount,
    int LayoutAffectingPropertyChangeCount,
    int LayoutCacheInvalidationCount,
    int IntrinsicMeasureInvalidationCount);

/// <summary>
/// TextBox performance snapshot.
/// </summary>
public readonly record struct TextBoxPerformanceSnapshot(
    int CommitCount,
    int DeferredSyncScheduledCount,
    int DeferredSyncFlushCount,
    int ImmediateSyncCount,
    int IncrementalNoWrapEditAttemptCount,
    int IncrementalNoWrapEditSuccessCount,
    int IncrementalVirtualEditSuccessCount,
    int IncrementalVirtualEditFallbackCount,
    int LayoutCacheHitCount,
    int LayoutCacheMissCount,
    int ViewportLayoutBuildCount,
    int FullLayoutBuildCount,
    int VirtualRangeBuildCount,
    int VirtualLineBuildCount,
    double TextSyncMilliseconds,
    int InputMutationSampleCount,
    double LastInputMutationMilliseconds,
    double LastInputEditMilliseconds,
    double LastInputCommitMilliseconds,
    double LastInputEnsureCaretMilliseconds,
    double AverageInputMutationMilliseconds,
    double MaxInputMutationMilliseconds,
    int RenderSampleCount,
    double LastRenderMilliseconds,
    double LastRenderViewportMilliseconds,
    double LastRenderSelectionMilliseconds,
    double LastRenderTextMilliseconds,
    double LastRenderCaretMilliseconds,
    double AverageRenderMilliseconds,
    double MaxRenderMilliseconds,
    int ViewportStateSampleCount,
    int ViewportStateCacheHitCount,
    int ViewportStateCacheMissCount,
    double LastViewportStateMilliseconds,
    double AverageViewportStateMilliseconds,
    double MaxViewportStateMilliseconds,
    int EnsureCaretSampleCount,
    int EnsureCaretFastPathHitCount,
    int EnsureCaretFastPathMissCount,
    double LastEnsureCaretMilliseconds,
    double LastEnsureCaretViewportMilliseconds,
    double LastEnsureCaretLineLookupMilliseconds,
    double LastEnsureCaretWidthMilliseconds,
    double LastEnsureCaretOffsetAdjustMilliseconds,
    double AverageEnsureCaretMilliseconds,
    double MaxEnsureCaretMilliseconds,
    TextEditingBufferMetrics BufferMetrics);

/// <summary>
/// PasswordBox performance snapshot.
/// </summary>
public readonly record struct PasswordBoxPerformanceSnapshot(
    int CommitCount,
    int DeferredSyncScheduledCount,
    int DeferredSyncFlushCount,
    int ImmediateSyncCount,
    int IncrementalNoWrapEditAttemptCount,
    int IncrementalNoWrapEditSuccessCount,
    int IncrementalVirtualEditSuccessCount,
    int IncrementalVirtualEditFallbackCount,
    int LayoutCacheHitCount,
    int LayoutCacheMissCount,
    int ViewportLayoutBuildCount,
    int FullLayoutBuildCount,
    int VirtualRangeBuildCount,
    int VirtualLineBuildCount,
    double TextSyncMilliseconds,
    int InputMutationSampleCount,
    double LastInputMutationMilliseconds,
    double LastInputEditMilliseconds,
    double LastInputCommitMilliseconds,
    double LastInputEnsureCaretMilliseconds,
    double AverageInputMutationMilliseconds,
    double MaxInputMutationMilliseconds,
    int RenderSampleCount,
    double LastRenderMilliseconds,
    double LastRenderViewportMilliseconds,
    double LastRenderSelectionMilliseconds,
    double LastRenderTextMilliseconds,
    double LastRenderCaretMilliseconds,
    double AverageRenderMilliseconds,
    double MaxRenderMilliseconds,
    int ViewportStateSampleCount,
    int ViewportStateCacheHitCount,
    int ViewportStateCacheMissCount,
    double LastViewportStateMilliseconds,
    double AverageViewportStateMilliseconds,
    double MaxViewportStateMilliseconds,
    int EnsureCaretSampleCount,
    int EnsureCaretFastPathHitCount,
    int EnsureCaretFastPathMissCount,
    double LastEnsureCaretMilliseconds,
    double LastEnsureCaretViewportMilliseconds,
    double LastEnsureCaretLineLookupMilliseconds,
    double LastEnsureCaretWidthMilliseconds,
    double LastEnsureCaretOffsetAdjustMilliseconds,
    double AverageEnsureCaretMilliseconds,
    double MaxEnsureCaretMilliseconds,
    TextEditingBufferMetrics BufferMetrics);

/// <summary>
/// RichTextBox performance snapshot.
/// </summary>
public readonly record struct RichTextBoxPerformanceSnapshot(
    int LayoutCacheHitCount,
    int LayoutCacheMissCount,
    int LayoutBuildSampleCount,
    double AverageLayoutBuildMilliseconds,
    double P95LayoutBuildMilliseconds,
    double P99LayoutBuildMilliseconds,
    double MaxLayoutBuildMilliseconds,
    int RenderSampleCount,
    double LastRenderMilliseconds,
    double AverageRenderMilliseconds,
    double MaxRenderMilliseconds,
    double LastRenderLayoutResolveMilliseconds,
    double LastRenderSelectionMilliseconds,
    double LastRenderRunsMilliseconds,
    int LastRenderRunCount,
    int LastRenderRunCharacterCount,
    double LastRenderTableBordersMilliseconds,
    double LastRenderCaretMilliseconds,
    double LastRenderHostedLayoutMilliseconds,
    double LastRenderHostedChildrenDrawMilliseconds,
    int LastRenderHostedChildrenDrawCount,
    int SelectionGeometrySampleCount,
    double LastSelectionGeometryMilliseconds,
    double AverageSelectionGeometryMilliseconds,
    double MaxSelectionGeometryMilliseconds,
    int ClipboardSerializeSampleCount,
    double LastClipboardSerializeMilliseconds,
    double AverageClipboardSerializeMilliseconds,
    double MaxClipboardSerializeMilliseconds,
    int ClipboardDeserializeSampleCount,
    double LastClipboardDeserializeMilliseconds,
    double AverageClipboardDeserializeMilliseconds,
    double MaxClipboardDeserializeMilliseconds,
    int EditSampleCount,
    double LastEditMilliseconds,
    double AverageEditMilliseconds,
    double MaxEditMilliseconds,
    int StructuredEnterSampleCount,
    double LastStructuredEnterParagraphEntryCollectionMilliseconds,
    double LastStructuredEnterCloneDocumentMilliseconds,
    double LastStructuredEnterParagraphEnumerationMilliseconds,
    double LastStructuredEnterPrepareParagraphsMilliseconds,
    double LastStructuredEnterCommitMilliseconds,
    double LastStructuredEnterTotalMilliseconds,
    bool LastStructuredEnterUsedDocumentReplacement,
    int UndoDepth,
    int RedoDepth,
    int UndoOperationCount,
    int RedoOperationCount);
