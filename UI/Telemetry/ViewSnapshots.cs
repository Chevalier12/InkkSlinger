namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// CanvasView diagnostics snapshot.
/// </summary>
internal readonly record struct CanvasViewDiagnosticsSnapshot(
    int HandleFocusCardDragDeltaCallCount,
    int HandlePointerMovedCallCount,
    int HandlePointerPressedCallCount,
    int HandlePointerReleasedCallCount,
    int InvalidateForTestsCallCount);

/// <summary>
/// RichTextBoxView diagnostics snapshot.
/// </summary>
internal readonly record struct RichTextBoxViewDiagnosticsSnapshot(
    int RequestUiRefreshCount,
    int HandlePointerMovedCallCount,
    int HandlePointerPressedCallCount,
    int HandlePointerReleasedCallCount);
