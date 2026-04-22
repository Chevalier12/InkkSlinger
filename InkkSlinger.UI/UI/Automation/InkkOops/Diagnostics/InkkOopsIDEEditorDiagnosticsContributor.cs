using System;

namespace InkkSlinger;

public sealed class InkkOopsIDEEditorDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 42;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        _ = context;

        if (element is IDE_Editor editor)
        {
            var runtime = editor.GetIDEEditorSnapshotForDiagnostics();
            var telemetry = IDE_Editor.GetAggregateTelemetrySnapshotForDiagnostics();
            builder.Add("ideEditorViewport", $"{runtime.HorizontalOffset:0.###},{runtime.VerticalOffset:0.###},{runtime.ViewportWidth:0.##},{runtime.ViewportHeight:0.##}");
            builder.Add("ideEditorExtent", $"{runtime.ExtentWidth:0.##},{runtime.ExtentHeight:0.##}");
            builder.Add("ideEditorLastRenderedRange", $"{runtime.LastRenderedFirstVisibleLine},{runtime.LastRenderedVisibleLineCount},{runtime.LastRenderedLineHeight:0.###},{runtime.LastRenderedLineOffset:0.###}");
            builder.Add("runtimeIdeEditorGutterCalls", runtime.UpdateLineNumberGutterCallCount);
            builder.Add("runtimeIdeEditorGutterForced", runtime.UpdateLineNumberGutterForcedCount);
            builder.Add("runtimeIdeEditorGutterApplied", runtime.UpdateLineNumberGutterAppliedCount);
            builder.Add("runtimeIdeEditorGutterNoOp", runtime.UpdateLineNumberGutterNoOpCount);
            builder.Add("runtimeIdeEditorGutterVisibleLines", runtime.UpdateLineNumberGutterVisibleLineTotal);
            builder.Add("runtimeIdeEditorGutterMs", FormatMilliseconds(runtime.UpdateLineNumberGutterMilliseconds));
            builder.Add("runtimeIdeEditorTextChanged", runtime.EditorTextChangedCallCount);
            builder.Add("runtimeIdeEditorTextChangedMs", FormatMilliseconds(runtime.EditorTextChangedMilliseconds));
            builder.Add("runtimeIdeEditorTextChangedCachedLineCountMs", FormatMilliseconds(runtime.EditorTextChangedUpdateCachedLineCountMilliseconds));
            builder.Add("runtimeIdeEditorTextChangedGutterMs", FormatMilliseconds(runtime.EditorTextChangedUpdateLineNumberGutterMilliseconds));
            builder.Add("runtimeIdeEditorTextChangedLineNumberUpdateMs", FormatMilliseconds(runtime.EditorTextChangedLineNumberPresenterUpdateMilliseconds));
            builder.Add("runtimeIdeEditorTextChangedLineNumberMeasureMs", FormatMilliseconds(runtime.EditorTextChangedLineNumberPresenterMeasureMilliseconds));
            builder.Add("runtimeIdeEditorTextChangedLineNumberArrangeMs", FormatMilliseconds(runtime.EditorTextChangedLineNumberPresenterArrangeMilliseconds));
            builder.Add("runtimeIdeEditorTextChangedIndentInvalidateMs", FormatMilliseconds(runtime.EditorTextChangedIndentInvalidateMilliseconds));
            builder.Add("runtimeIdeEditorTextChangedSubscriberMs", FormatMilliseconds(runtime.EditorTextChangedSubscriberMilliseconds));
            builder.Add("runtimeIdeEditorLastTextChangedMs", FormatMilliseconds(runtime.LastEditorTextChangedMilliseconds));
            builder.Add("runtimeIdeEditorLastTextChangedCachedLineCountMs", FormatMilliseconds(runtime.LastEditorTextChangedUpdateCachedLineCountMilliseconds));
            builder.Add("runtimeIdeEditorLastTextChangedGutterMs", FormatMilliseconds(runtime.LastEditorTextChangedUpdateLineNumberGutterMilliseconds));
            builder.Add("runtimeIdeEditorLastTextChangedLineNumberUpdateMs", FormatMilliseconds(runtime.LastEditorTextChangedLineNumberPresenterUpdateMilliseconds));
            builder.Add("runtimeIdeEditorLastTextChangedLineNumberMeasureMs", FormatMilliseconds(runtime.LastEditorTextChangedLineNumberPresenterMeasureMilliseconds));
            builder.Add("runtimeIdeEditorLastTextChangedLineNumberArrangeMs", FormatMilliseconds(runtime.LastEditorTextChangedLineNumberPresenterArrangeMilliseconds));
            builder.Add("runtimeIdeEditorLastTextChangedIndentInvalidateMs", FormatMilliseconds(runtime.LastEditorTextChangedIndentInvalidateMilliseconds));
            builder.Add("runtimeIdeEditorLastTextChangedSubscriberMs", FormatMilliseconds(runtime.LastEditorTextChangedSubscriberMilliseconds));
            builder.Add("runtimeIdeEditorDocumentChanged", runtime.EditorDocumentChangedCallCount);
            builder.Add("runtimeIdeEditorViewportChanged", runtime.EditorViewportChangedCallCount);
            builder.Add("runtimeIdeEditorLayoutUpdated", runtime.EditorLayoutUpdatedCallCount);
            builder.Add("runtimeIdeEditorIndentInvalidate", runtime.IndentGuideInvalidateVisualCallCount);
            builder.Add("runtimeIdeEditorIndentSnapshotCalls", runtime.BuildIndentGuideSnapshotCallCount);
            builder.Add("runtimeIdeEditorIndentSnapshotSuccess", runtime.BuildIndentGuideSnapshotSuccessCount);
            builder.Add("runtimeIdeEditorIndentSnapshotSegments", runtime.BuildIndentGuideSnapshotSegmentTotal);
            builder.Add("runtimeIdeEditorIndentSnapshotMs", FormatMilliseconds(runtime.BuildIndentGuideSnapshotMilliseconds));
            builder.Add("telemetryIdeEditorGutterCalls", telemetry.UpdateLineNumberGutterCallCount);
            builder.Add("telemetryIdeEditorGutterForced", telemetry.UpdateLineNumberGutterForcedCount);
            builder.Add("telemetryIdeEditorGutterApplied", telemetry.UpdateLineNumberGutterAppliedCount);
            builder.Add("telemetryIdeEditorGutterNoOp", telemetry.UpdateLineNumberGutterNoOpCount);
            builder.Add("telemetryIdeEditorGutterVisibleLines", telemetry.UpdateLineNumberGutterVisibleLineTotal);
            builder.Add("telemetryIdeEditorGutterMs", FormatMilliseconds(telemetry.UpdateLineNumberGutterMilliseconds));
            builder.Add("telemetryIdeEditorTextChanged", telemetry.EditorTextChangedCallCount);
            builder.Add("telemetryIdeEditorTextChangedMs", FormatMilliseconds(telemetry.EditorTextChangedMilliseconds));
            builder.Add("telemetryIdeEditorTextChangedCachedLineCountMs", FormatMilliseconds(telemetry.EditorTextChangedUpdateCachedLineCountMilliseconds));
            builder.Add("telemetryIdeEditorTextChangedGutterMs", FormatMilliseconds(telemetry.EditorTextChangedUpdateLineNumberGutterMilliseconds));
            builder.Add("telemetryIdeEditorTextChangedLineNumberUpdateMs", FormatMilliseconds(telemetry.EditorTextChangedLineNumberPresenterUpdateMilliseconds));
            builder.Add("telemetryIdeEditorTextChangedLineNumberMeasureMs", FormatMilliseconds(telemetry.EditorTextChangedLineNumberPresenterMeasureMilliseconds));
            builder.Add("telemetryIdeEditorTextChangedLineNumberArrangeMs", FormatMilliseconds(telemetry.EditorTextChangedLineNumberPresenterArrangeMilliseconds));
            builder.Add("telemetryIdeEditorTextChangedIndentInvalidateMs", FormatMilliseconds(telemetry.EditorTextChangedIndentInvalidateMilliseconds));
            builder.Add("telemetryIdeEditorTextChangedSubscriberMs", FormatMilliseconds(telemetry.EditorTextChangedSubscriberMilliseconds));
            builder.Add("telemetryIdeEditorDocumentChanged", telemetry.EditorDocumentChangedCallCount);
            builder.Add("telemetryIdeEditorViewportChanged", telemetry.EditorViewportChangedCallCount);
            builder.Add("telemetryIdeEditorLayoutUpdated", telemetry.EditorLayoutUpdatedCallCount);
            builder.Add("telemetryIdeEditorIndentInvalidate", telemetry.IndentGuideInvalidateVisualCallCount);
            builder.Add("telemetryIdeEditorIndentSnapshotCalls", telemetry.BuildIndentGuideSnapshotCallCount);
            builder.Add("telemetryIdeEditorIndentSnapshotSuccess", telemetry.BuildIndentGuideSnapshotSuccessCount);
            builder.Add("telemetryIdeEditorIndentSnapshotSegments", telemetry.BuildIndentGuideSnapshotSegmentTotal);
            builder.Add("telemetryIdeEditorIndentSnapshotMs", FormatMilliseconds(telemetry.BuildIndentGuideSnapshotMilliseconds));
            return;
        }

        if (element is IDEEditorLineNumberPresenter lineNumberPresenter)
        {
            var runtime = lineNumberPresenter.GetIDEEditorLineNumberPresenterSnapshotForDiagnostics();
            var telemetry = IDEEditorLineNumberPresenter.GetAggregateTelemetrySnapshotForDiagnostics();
            builder.Add("ideLineNumberRange", $"{runtime.FirstVisibleLine},{runtime.VisibleLineCount}");
            builder.Add("ideLineNumberMetrics", $"{runtime.LineHeight:0.###},{runtime.VerticalLineOffset:0.###}");
            builder.Add("runtimeIdeLineNumberUpdateCalls", runtime.UpdateVisibleRangeCallCount);
            builder.Add("runtimeIdeLineNumberUpdateChanged", runtime.UpdateVisibleRangeChangedCount);
            builder.Add("runtimeIdeLineNumberTextUpdates", runtime.UpdateVisibleRangeTextUpdateCount);
            builder.Add("runtimeIdeLineNumberVisibleLines", runtime.UpdateVisibleRangeVisibleLineTotal);
            builder.Add("runtimeIdeLineNumberUpdateMs", FormatMilliseconds(runtime.UpdateVisibleRangeMilliseconds));
            builder.Add("runtimeIdeLineNumberMeasureCalls", runtime.MeasureOverrideCallCount);
            builder.Add("runtimeIdeLineNumberMeasureMs", FormatMilliseconds(runtime.MeasureOverrideMilliseconds));
            builder.Add("runtimeIdeLineNumberArrangeCalls", runtime.ArrangeOverrideCallCount);
            builder.Add("runtimeIdeLineNumberArrangeMs", FormatMilliseconds(runtime.ArrangeOverrideMilliseconds));
            builder.Add("telemetryIdeLineNumberUpdateCalls", telemetry.UpdateVisibleRangeCallCount);
            builder.Add("telemetryIdeLineNumberUpdateChanged", telemetry.UpdateVisibleRangeChangedCount);
            builder.Add("telemetryIdeLineNumberTextUpdates", telemetry.UpdateVisibleRangeTextUpdateCount);
            builder.Add("telemetryIdeLineNumberVisibleLines", telemetry.UpdateVisibleRangeVisibleLineTotal);
            builder.Add("telemetryIdeLineNumberUpdateMs", FormatMilliseconds(telemetry.UpdateVisibleRangeMilliseconds));
            builder.Add("telemetryIdeLineNumberMeasureCalls", telemetry.MeasureOverrideCallCount);
            builder.Add("telemetryIdeLineNumberMeasureMs", FormatMilliseconds(telemetry.MeasureOverrideMilliseconds));
            builder.Add("telemetryIdeLineNumberArrangeCalls", telemetry.ArrangeOverrideCallCount);
            builder.Add("telemetryIdeLineNumberArrangeMs", FormatMilliseconds(telemetry.ArrangeOverrideMilliseconds));
            return;
        }

        if (element is IDEEditorIndentGuideOverlay overlay)
        {
            var runtime = overlay.GetIDEEditorIndentGuideOverlaySnapshotForDiagnostics();
            var telemetry = IDEEditorIndentGuideOverlay.GetAggregateTelemetrySnapshotForDiagnostics();
            builder.Add("ideIndentGuideHasOwner", runtime.HasOwner);
            builder.Add("runtimeIdeIndentRenderCalls", runtime.RenderCallCount);
            builder.Add("runtimeIdeIndentRenderSkippedNoOwner", runtime.RenderSkippedNoOwnerCount);
            builder.Add("runtimeIdeIndentRenderSkippedEmpty", runtime.RenderSkippedEmptySnapshotCount);
            builder.Add("runtimeIdeIndentRenderSegments", runtime.RenderSegmentTotal);
            builder.Add("runtimeIdeIndentRenderMs", FormatMilliseconds(runtime.RenderMilliseconds));
            builder.Add("telemetryIdeIndentRenderCalls", telemetry.RenderCallCount);
            builder.Add("telemetryIdeIndentRenderSkippedNoOwner", telemetry.RenderSkippedNoOwnerCount);
            builder.Add("telemetryIdeIndentRenderSkippedEmpty", telemetry.RenderSkippedEmptySnapshotCount);
            builder.Add("telemetryIdeIndentRenderSegments", telemetry.RenderSegmentTotal);
            builder.Add("telemetryIdeIndentRenderMs", FormatMilliseconds(telemetry.RenderMilliseconds));
        }
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }
}