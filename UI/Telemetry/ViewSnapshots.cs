namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// CanvasView diagnostics snapshot.
/// </summary>
internal readonly record struct CanvasViewDiagnosticsSnapshot(
    int HandleFocusCardDragDeltaCallCount,
    double HandleFocusCardDragDeltaMilliseconds,
    int MoveFocusByCallCount,
    double MoveFocusByMilliseconds,
    int ApplySceneStateCallCount,
    double ApplySceneStateMilliseconds,
    int ApplyFocusAnchorsCallCount,
    double ApplyFocusAnchorsMilliseconds,
    int ApplyBadgeLayerCallCount,
    double ApplyBadgeLayerMilliseconds,
    int ApplyGuideVisibilityCallCount,
    double ApplyGuideVisibilityMilliseconds,
    int UpdateLiveTextCallCount,
    double UpdateLiveTextMilliseconds,
    int UpdateTelemetryCallCount,
    double UpdateTelemetryMilliseconds,
    int SyncOverlayLayoutCallCount,
    double SyncOverlayLayoutMilliseconds,
    int SetTextChangeCount,
    double SetTextMilliseconds,
    string SetTextTargetSummary,
    int SetCanvasLeftChangeCount,
    double SetCanvasLeftMilliseconds,
    int SetCanvasTopChangeCount,
    double SetCanvasTopMilliseconds);

/// <summary>
/// RichTextBoxView diagnostics snapshot.
/// </summary>
internal readonly record struct RichTextBoxViewDiagnosticsSnapshot(
    int RequestUiRefreshCount,
    int QueuedEditorRefreshCount,
    int RefreshUiStateCount,
    double RefreshUiStateTotalMilliseconds,
    int RefreshEditorUiStateCount,
    double RefreshEditorUiStateTotalMilliseconds,
    int DocumentStatsCount,
    double DocumentStatsTotalMilliseconds,
    int UpdateCommandStatesCount,
    double UpdateCommandStatesTotalMilliseconds,
    int UpdateEditorCommandStatesCount,
    double UpdateEditorCommandStatesTotalMilliseconds,
    int UpdateStatusLabelsCount,
    double UpdateStatusLabelsTotalMilliseconds,
    int UpdatePayloadMetaCount,
    double UpdatePayloadMetaTotalMilliseconds,
    int UpdateHeroSummaryCount,
    double UpdateHeroSummaryTotalMilliseconds,
    int UpdatePresetHintsCount,
    double UpdatePresetHintsTotalMilliseconds);