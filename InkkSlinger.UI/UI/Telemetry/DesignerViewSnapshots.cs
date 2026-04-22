namespace InkkSlinger.UI.Telemetry;

public readonly record struct DesignerSourceEditorViewTelemetrySnapshot(
    long SourceEditorTextChangedCallCount,
    double SourceEditorTextChangedMilliseconds,
    long SourceEditorTextChangedRefreshHighlightedCallCount,
    double SourceEditorTextChangedRefreshHighlightedMilliseconds,
    long SourceEditorTextChangedRefreshPropertyInspectorCallCount,
    double SourceEditorTextChangedRefreshPropertyInspectorMilliseconds,
    long SourceEditorTextChangedRefreshCompletionCallCount,
    double SourceEditorTextChangedRefreshCompletionMilliseconds);

public readonly record struct DesignerSourceEditorViewRuntimeDiagnosticsSnapshot(
    long SourceEditorTextChangedCallCount,
    double SourceEditorTextChangedMilliseconds,
    long SourceEditorTextChangedRefreshHighlightedCallCount,
    double SourceEditorTextChangedRefreshHighlightedMilliseconds,
    long SourceEditorTextChangedRefreshPropertyInspectorCallCount,
    double SourceEditorTextChangedRefreshPropertyInspectorMilliseconds,
    long SourceEditorTextChangedRefreshCompletionCallCount,
    double SourceEditorTextChangedRefreshCompletionMilliseconds,
    double LastSourceEditorTextChangedMilliseconds,
    bool LastSourceEditorTextChangedRefreshedHighlighted,
    double LastSourceEditorTextChangedRefreshHighlightedMilliseconds,
    double LastSourceEditorTextChangedRefreshPropertyInspectorMilliseconds,
    bool LastSourceEditorTextChangedRefreshedCompletion,
    double LastSourceEditorTextChangedRefreshCompletionMilliseconds);