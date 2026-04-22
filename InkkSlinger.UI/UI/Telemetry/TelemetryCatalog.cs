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
/// - Panel: Panel layout telemetry (individual *TelemetrySnapshot.cs and *TimingSnapshot.cs files)
/// - VisualTree: Visual tree and hit testing telemetry (split across dedicated snapshot files)
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
    // - ScrollViewerTelemetrySnapshot
    // - ScrollViewerRuntimeDiagnosticsSnapshot
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
    // - ButtonTelemetrySnapshot
    // - ButtonRuntimeDiagnosticsSnapshot
    // - ButtonTimingSnapshot
    // - CheckBox (UI/Telemetry/CheckBoxSnapshots.cs)
    // - CheckBoxTelemetrySnapshot
    // - CheckBoxRuntimeDiagnosticsSnapshot
    // - ComboBox (UI/Telemetry/ComboBoxSnapshots.cs)
    // - ComboBoxTelemetrySnapshot
    // - ComboBoxRuntimeDiagnosticsSnapshot
    // - Thumb (UI/Telemetry/ThumbSnapshots.cs)
    // - ThumbDragTelemetrySnapshot
    // - Expander (UI/Telemetry/ExpanderSnapshots.cs)
    // - ExpanderTimingSnapshot
    // - ExpanderRuntimeDiagnosticsSnapshot
    // - Calendar (UI/Telemetry/CalendarSnapshots.cs)
    // - CalendarTelemetrySnapshot
    // - CalendarRuntimeDiagnosticsSnapshot
    // - CalendarDiagnosticsSnapshot
    // - CalendarRefreshDiagnostics
    // - CalendarRefreshTimingDiagnostics
    // - CalendarDayButton (UI/Telemetry/CalendarDayButtonSnapshots.cs)
    // - CalendarDayButtonTelemetrySnapshot
    // - CalendarDayButtonRuntimeDiagnosticsSnapshot
    // - CalendarDayButtonTimingSnapshot
    // - Border (UI/Telemetry/BorderSnapshots.cs)
    // - BorderTelemetrySnapshot
    // - BorderRuntimeDiagnosticsSnapshot
    // - Label (UI/Telemetry/LabelSnapshots.cs)
    // - LabelTelemetrySnapshot
    // - LabelRuntimeDiagnosticsSnapshot
    // - TextBlock (UI/Telemetry/TextBlockSnapshots.cs)
    // - TextBlockTelemetrySnapshot
    // - TextBlockPerformanceSnapshot
    // - TextBlockRuntimeDiagnosticsSnapshot
    // - TextBox (UI/Telemetry/TextBoxSnapshots.cs)
    // - TextBoxPerformanceSnapshot
    // - PasswordBox (UI/Telemetry/PasswordBoxSnapshots.cs)
    // - PasswordBoxPerformanceSnapshot
    // - RichTextBox (UI/Telemetry/RichTextBoxSnapshots.cs)
    // - RichTextBoxTelemetrySnapshot
    // - RichTextBoxRuntimeDiagnosticsSnapshot
    // - RichTextBoxPerformanceSnapshot
    // - IDE_Editor (UI/Telemetry/IDEEditorSnapshots.cs)
    // - IDEEditorTelemetrySnapshot
    // - IDEEditorRuntimeDiagnosticsSnapshot
    // - IDEEditorLineNumberPresenterTelemetrySnapshot
    // - IDEEditorLineNumberPresenterRuntimeDiagnosticsSnapshot
    // - IDEEditorIndentGuideOverlayTelemetrySnapshot
    // - IDEEditorIndentGuideOverlayRuntimeDiagnosticsSnapshot
    // - GridSplitter (UI/Telemetry/GridSplitterSnapshots.cs)
    // - GridSplitterTelemetrySnapshot
    // - GridSplitterRuntimeDiagnosticsSnapshot
    // - ContentPresenter (UI/Telemetry/ContentPresenterSnapshots.cs)
    // - ContentPresenterTelemetrySnapshot
    // - ContentPresenterRuntimeDiagnosticsSnapshot
    // - Control (UI/Telemetry/ControlSnapshots.cs)
    // - ControlTelemetrySnapshot
    // - ControlRuntimeDiagnosticsSnapshot
    // - ContentControl (UI/Telemetry/ContentControlSnapshots.cs)
    // - ContentControlTelemetrySnapshot
    // - ContentControlRuntimeDiagnosticsSnapshot
    // 
    // Panel
    // - PanelTelemetrySnapshot (UI/Telemetry/PanelTelemetrySnapshot.cs)
    // - CanvasTelemetrySnapshot (UI/Telemetry/CanvasTelemetrySnapshot.cs)
    // - StackPanelTelemetrySnapshot (UI/Telemetry/StackPanelTelemetrySnapshot.cs)
    // - StackPanelRuntimeDiagnosticsSnapshot (UI/Telemetry/StackPanelTelemetrySnapshot.cs)
    // - VirtualizingStackPanelTelemetrySnapshot (UI/Telemetry/VirtualizingStackPanelSnapshots.cs)
    // - VirtualizingStackPanelRuntimeDiagnosticsSnapshot (UI/Telemetry/VirtualizingStackPanelSnapshots.cs)
    // - WrapPanelTelemetrySnapshot (UI/Telemetry/WrapPanelTelemetrySnapshot.cs)
    // - GridTelemetrySnapshot (UI/Telemetry/GridTelemetrySnapshot.cs)
    // - GridRuntimeDiagnosticsSnapshot (UI/Telemetry/GridTelemetrySnapshot.cs)
    // - GridTimingSnapshot (UI/Telemetry/GridTimingSnapshot.cs)
    // - UniformGridTimingSnapshot (UI/Telemetry/UniformGridTimingSnapshot.cs)
    // 
    // VisualTree
    // - HitTestInstrumentationSnapshot (UI/Telemetry/HitTestInstrumentationSnapshot.cs)
    // - UIElementRenderTimingSnapshot (UI/Telemetry/UIElementRenderTimingSnapshot.cs)
    // - UIElementInvalidationDiagnosticsSnapshot (UI/Telemetry/UIElementInvalidationDiagnosticsSnapshot.cs)
    // - ValueChangedRoutedEventTelemetrySnapshot (UI/Telemetry/ValueChangedRoutedEventTelemetrySnapshot.cs)
    // - FrameworkLayoutTimingSnapshot (UI/Telemetry/FrameworkLayoutTimingSnapshot.cs)
    // - FrameworkElement (UI/Telemetry/FrameworkElementSnapshot.cs)
    // - FrameworkElementTelemetrySnapshot
    // - FrameworkElementDiagnosticsSnapshot
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
