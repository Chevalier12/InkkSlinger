namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Central catalog of all telemetry snapshot types in the InkkSlinger framework.
/// This folder provides a single organized location for all telemetry-related types.
/// 
/// Snapshot types are organized by category:
/// - Animation: Animation-related telemetry (AnimationSnapshots.cs)
/// - Core: Core framework telemetry for root, rendering, and performance (CoreSnapshots.cs)
/// - Rendering: Rendering and text telemetry (RenderingSnapshots.cs)
/// - Scrolling: ScrollViewer, ScrollBar, Track telemetry (ScrollingSnapshots.cs)
/// - Style: Styling, templating, and visual state telemetry (StyleSnapshots.cs)
/// - Control-specific: Individual control telemetry in per-control snapshot files
/// - Panel: Panel layout telemetry (PanelSnapshots.cs)
/// - VisualTree: Visual tree and hit testing telemetry (VisualTreeSnapshots.cs)
/// - View: View-specific telemetry (ViewSnapshots.cs)
/// - Freezable: Freezable object telemetry (FreezableSnapshots.cs)
/// - Effects: Effect telemetry (EffectsSnapshots.cs)
/// - Automation: UI Automation telemetry (AutomationSnapshots.cs)
/// </summary>
public static class TelemetryCatalog
{
    // Categories and their associated files:
    // 
    // Animation (UI/Telemetry/AnimationSnapshots.cs)
    // - AnimationTelemetrySnapshot
    // - AnimationSinkTelemetrySnapshot
    // 
    // Core (UI/Telemetry/CoreSnapshots.cs)
    // - UiRootMetricsSnapshot
    // - UiVisualTreeMetricsSnapshot
    // - UiVisualTreeWorkMetricsSnapshot
    // - UiInputMetricsSnapshot
    // - UiRenderTelemetrySnapshot
    // - UiRenderInvalidationDebugSnapshot
    // - UiRootPerformanceTelemetrySnapshot
    // - UiPointerMoveTelemetrySnapshot
    // - UiFreezableInvalidationBatchSnapshot
    // 
    // Rendering (UI/Telemetry/RenderingSnapshots.cs)
    // - UiTextRendererTimingSnapshot
    // - UiRuntimeFontBackendTimingSnapshot
    // - TextLayoutMetricsSnapshot
    // - TextClipboardTelemetrySnapshot
    // - TextClipboardReadTelemetrySnapshot
    // 
    // Scrolling (UI/Telemetry/ScrollingSnapshots.cs)
    // - ScrollViewerScrollMetricsSnapshot
    // - ScrollViewerInteractionTelemetrySnapshot
    // - ScrollViewerValueChangedTelemetrySnapshot
    // - ScrollViewerLayoutTelemetrySnapshot
    // - ScrollBarThumbDragTelemetrySnapshot
    // - TrackThumbTravelTelemetrySnapshot
    // 
    // Style (UI/Telemetry/StyleSnapshots.cs)
    // - StyleTelemetrySnapshot
    // - VisualStateTelemetrySnapshot
    // - TemplateTriggerTelemetrySnapshot
    // 
    // Control-specific
    // - Button (UI/Telemetry/ButtonSnapshots.cs)
    // - ButtonTimingSnapshot
    // - Thumb (UI/Telemetry/ThumbSnapshots.cs)
    // - ThumbDragTelemetrySnapshot
    // - Expander (UI/Telemetry/ExpanderSnapshots.cs)
    // - ExpanderTimingSnapshot
    // - ExpanderRuntimeDiagnosticsSnapshot
    // - Calendar (UI/Telemetry/CalendarSnapshots.cs)
    // - CalendarDiagnosticsSnapshot
    // - CalendarRefreshDiagnostics
    // - CalendarRefreshTimingDiagnostics
    // - CalendarDayButton (UI/Telemetry/CalendarDayButtonSnapshots.cs)
    // - CalendarDayButtonTimingSnapshot
    // - TextBlock (UI/Telemetry/TextBlockSnapshots.cs)
    // - TextBlockPerformanceSnapshot
    // - TextBlockRuntimeDiagnosticsSnapshot
    // - TextBox (UI/Telemetry/TextBoxSnapshots.cs)
    // - TextBoxPerformanceSnapshot
    // - PasswordBox (UI/Telemetry/PasswordBoxSnapshots.cs)
    // - PasswordBoxPerformanceSnapshot
    // - RichTextBox (UI/Telemetry/RichTextBoxSnapshots.cs)
    // - RichTextBoxPerformanceSnapshot
    // 
    // Panel (UI/Telemetry/PanelSnapshots.cs)
    // - PanelTelemetrySnapshot
    // - CanvasTelemetrySnapshot
    // - StackPanelTelemetrySnapshot
    // - GridTimingSnapshot
    // - UniformGridTimingSnapshot
    // 
    // VisualTree (UI/Telemetry/VisualTreeSnapshots.cs)
    // - HitTestInstrumentationSnapshot
    // - UIElementRenderTimingSnapshot
    // - UIElementInvalidationDiagnosticsSnapshot
    // - ValueChangedRoutedEventTelemetrySnapshot
    // - FrameworkLayoutTimingSnapshot
    // 
    // View (UI/Telemetry/ViewSnapshots.cs)
    // - CanvasViewDiagnosticsSnapshot
    // - RichTextBoxViewDiagnosticsSnapshot
    // Freezable (UI/Telemetry/FreezableSnapshots.cs)
    // - FreezableTelemetrySnapshot
    // 
    // Effects (UI/Telemetry/EffectsSnapshots.cs)
    // - DropShadowEffectTimingSnapshot
    // 
    // Automation (UI/Telemetry/AutomationSnapshots.cs)
    // - AutomationMetricsSnapshot
}
