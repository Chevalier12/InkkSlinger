using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogRichTextBoxTypingHotspotDiagnosticsTests
{
    private const int ViewportWidth = 1400;
    private const int ViewportHeight = 900;

    [Fact]
    public void ControlsCatalog_RichTextBoxTyping_WritesHotspotDiagnosticsLog()
    {
        RunTypingDiagnostics(
            presetButtonLabel: null,
            typedText: "lagprobe",
            logFileNameWithoutExtension: "controls-catalog-richtextbox-typing-hotspot",
            scenario: "Controls Catalog RichTextBox typing hotspot diagnostics");
    }

    [Fact]
    public void ControlsCatalog_RichTextBoxEmbeddedUiTyping_WritesHotspotDiagnosticsLog()
    {
        RunTypingDiagnostics(
            presetButtonLabel: "Embedded UI",
            typedText: "hostedaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaasdfasdfasdfasdfasdfasdfasdfasdf",
            logFileNameWithoutExtension: "controls-catalog-richtextbox-embedded-ui-typing-hotspot",
            scenario: "Controls Catalog RichTextBox embedded UI typing hotspot diagnostics");
    }

    [Fact]
    public void ControlsCatalog_RichTextBoxEmbeddedUiEnter_WritesHotspotDiagnosticsLog()
    {
        Dispatcher.ResetForTests();
        AnimationManager.Current.ResetForTests();
        FocusManager.ClearFocus();
        FocusManager.ClearPointerCapture();

        var catalog = new ControlsCatalogView();
        catalog.ShowControl("RichTextBox");
        var uiRoot = CreateUiRoot(catalog);
        RunFrame(uiRoot, 16);
        RunFrame(uiRoot, 16);
        RunFrame(uiRoot, 16);

        Assert.True(TryGetSelectedRichTextBoxView(catalog, out var view), "Expected Controls Catalog to present RichTextBox view.");
        Assert.NotNull(view);

        var embeddedUiButton = FindFirstVisualChild<Button>(
                                   view,
                                   static button => string.Equals(button.GetContentText(), "Embedded UI", StringComparison.Ordinal))
                               ?? throw new InvalidOperationException("Could not find Embedded UI preset button.");
        InvokeButtonClick(embeddedUiButton);
        RunFrame(uiRoot, 16);
        RunFrame(uiRoot, 16);

        var editor = Assert.IsType<RichTextBox>(view.FindName("Editor"));
        var beforeText = DocumentEditing.GetText(editor.Document);
        var inlineObjectIndex = beforeText.IndexOf('\uFFFC');
        Assert.True(inlineObjectIndex >= 0, $"Expected embedded UI preset to contain an inline object. text={beforeText}");

        Click(uiRoot, GetCenter(editor));
        RunFrame(uiRoot, 16);
        RunFrame(uiRoot, 16);

        uiRoot.SetFocusedElementForTests(editor);
        editor.SetFocusedFromInput(true);
        editor.Select(inlineObjectIndex + 1, 0);

        SimulatePostDrawCleanup(uiRoot);

        editor.ResetPerformanceSnapshot();
        view.ResetDiagnosticsForTests();

        var lines = new List<string>
        {
            "scenario=Controls Catalog RichTextBox embedded UI enter hotspot diagnostics",
            $"timestamp_utc={DateTime.UtcNow:O}",
            $"log_path={GetDiagnosticsLogPath("controls-catalog-richtextbox-embedded-ui-enter-hotspot")}",
            "step_1=open Controls Catalog",
            "step_2=open RichTextBox view",
            "step_3=click Embedded UI preset",
            "step_4=place caret immediately after the inline hosted button",
            "step_5=press Enter through RichTextBox key input path",
            $"editor_slot={FormatRect(editor.LayoutSlot)}",
            $"caret_before={editor.CaretIndex}",
            $"document_text_before={beforeText.Replace(Environment.NewLine, "\\n", StringComparison.Ordinal)}"
        };

        uiRoot.ClearDirtyBoundsEventTraceForTests();
        var beforeView = view.GetDiagnosticsSnapshotForTests();
        var beforeEditor = editor.GetPerformanceSnapshot();
        var beforeLayout = SnapshotElementTimings(catalog);
        var beforeRender = uiRoot.GetRenderTelemetrySnapshotForTests();

        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        RunFrame(uiRoot, 16);

        var afterView = view.GetDiagnosticsSnapshotForTests();
        var afterEditor = editor.GetPerformanceSnapshot();
        var rootTelemetry = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        var renderTelemetry = uiRoot.GetRenderTelemetrySnapshotForTests();
        var invalidationDebug = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();
        var dirtyRegionCount = dirtyRegions.Count;
        var dirtyCoverage = uiRoot.GetDirtyCoverageForTests();
        var wouldUsePartialDirtyRedraw = uiRoot.WouldUsePartialDirtyRedrawForTests();
        var isFullDirty = uiRoot.IsFullDirtyForTests();
        var dirtyQueue = uiRoot.GetDirtyRenderQueueSnapshotForTests();
        var dirtyBoundsTrace = uiRoot.GetDirtyBoundsEventTraceForTests();
        var largestDirtyRegion = dirtyRegions.OrderByDescending(static region => region.Width * region.Height).FirstOrDefault();
        var traversalClip = dirtyRegionCount > 0
            ? largestDirtyRegion
            : new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight);
        var traversalMetrics = uiRoot.GetRetainedTraversalMetricsForClipForTests(traversalClip);
        var drawOrder = uiRoot.GetRetainedDrawOrderForClipForTests(traversalClip);
        var editorSubtreeDrawnCount = drawOrder.Count(visual => IsDescendantOrSelf(editor, visual));
        var nonEditorSubtreeDrawnCount = drawOrder.Count - editorSubtreeDrawnCount;
        var dirtyQueueSummary = string.Join(" | ", dirtyQueue.Take(6).Select(DescribeElement));
        var nonEditorDrawSample = string.Join(
            " | ",
            drawOrder.Where(visual => !IsDescendantOrSelf(editor, visual))
                .Take(6)
                .Select(visual => DescribeVisualPath(visual, catalog)));
        var hottestElement = CaptureHottestElementTiming(catalog, beforeLayout);
        var recentOperations = string.Join(" | ", GetRecentOperations(editor));

        lines.Add(string.Empty);
        lines.Add("summary:");
        lines.Add($"update_ms={uiRoot.LastUpdateMs:0.###}");
        lines.Add($"input_ms={uiRoot.LastInputPhaseMs:0.###}");
        lines.Add($"binding_ms={uiRoot.LastBindingPhaseMs:0.###}");
        lines.Add($"layout_ms={uiRoot.LastLayoutPhaseMs:0.###}");
        lines.Add($"animation_ms={uiRoot.LastAnimationPhaseMs:0.###}");
        lines.Add($"render_schedule_ms={uiRoot.LastRenderSchedulingPhaseMs:0.###}");
        lines.Add($"layout_measure_work_ms={rootTelemetry.LayoutMeasureWorkMilliseconds:0.###}");
        lines.Add($"layout_measure_exclusive_work_ms={rootTelemetry.LayoutMeasureExclusiveWorkMilliseconds:0.###}");
        lines.Add($"layout_arrange_work_ms={rootTelemetry.LayoutArrangeWorkMilliseconds:0.###}");
        lines.Add($"refresh_editor_ui_ms={afterView.RefreshEditorUiStateTotalMilliseconds - beforeView.RefreshEditorUiStateTotalMilliseconds:0.###}");
        lines.Add($"document_stats_ms={afterView.DocumentStatsTotalMilliseconds - beforeView.DocumentStatsTotalMilliseconds:0.###}");
        lines.Add($"update_editor_command_states_ms={afterView.UpdateEditorCommandStatesTotalMilliseconds - beforeView.UpdateEditorCommandStatesTotalMilliseconds:0.###}");
        lines.Add($"editor_last_edit_ms={afterEditor.LastEditMilliseconds:0.###}");
        lines.Add($"structured_enter_samples={afterEditor.StructuredEnterSampleCount - beforeEditor.StructuredEnterSampleCount}");
        lines.Add($"structured_enter_collect_entries_ms={afterEditor.LastStructuredEnterParagraphEntryCollectionMilliseconds:0.###}");
        lines.Add($"structured_enter_clone_document_ms={afterEditor.LastStructuredEnterCloneDocumentMilliseconds:0.###}");
        lines.Add($"structured_enter_enumerate_paragraphs_ms={afterEditor.LastStructuredEnterParagraphEnumerationMilliseconds:0.###}");
        lines.Add($"structured_enter_prepare_paragraphs_ms={afterEditor.LastStructuredEnterPrepareParagraphsMilliseconds:0.###}");
        lines.Add($"structured_enter_commit_ms={afterEditor.LastStructuredEnterCommitMilliseconds:0.###}");
        lines.Add($"structured_enter_total_ms={afterEditor.LastStructuredEnterTotalMilliseconds:0.###}");
        lines.Add($"structured_enter_used_document_replacement={afterEditor.LastStructuredEnterUsedDocumentReplacement}");
        lines.Add($"dirty_regions={dirtyRegionCount}");
        lines.Add($"dirty_coverage={dirtyCoverage:0.###}");
        lines.Add($"partial_dirty={wouldUsePartialDirtyRedraw}");
        lines.Add($"full_dirty={isFullDirty}");
        lines.Add($"dirty_roots={Math.Max(0, renderTelemetry.DirtyRootCount - beforeRender.DirtyRootCount)}");
        lines.Add($"retained_traversals={Math.Max(0, renderTelemetry.RetainedTraversalCount - beforeRender.RetainedTraversalCount)}");
        lines.Add($"dirty_region_traversals={Math.Max(0, renderTelemetry.DirtyRegionTraversalCount - beforeRender.DirtyRegionTraversalCount)}");
        lines.Add($"dirty_region_fallbacks={Math.Max(0, renderTelemetry.DirtyRegionThresholdFallbackCount - beforeRender.DirtyRegionThresholdFallbackCount)}");
        lines.Add($"full_dirty_structure={Math.Max(0, renderTelemetry.FullDirtyVisualStructureChangeCount - beforeRender.FullDirtyVisualStructureChangeCount)}");
        lines.Add($"full_dirty_retained_rebuild={Math.Max(0, renderTelemetry.FullDirtyRetainedRebuildCount - beforeRender.FullDirtyRetainedRebuildCount)}");
        lines.Add($"full_dirty_detached={Math.Max(0, renderTelemetry.FullDirtyDetachedVisualCount - beforeRender.FullDirtyDetachedVisualCount)}");
        lines.Add($"requested_source={invalidationDebug.RequestedSourceType}#{invalidationDebug.RequestedSourceName}");
        lines.Add($"effective_source={invalidationDebug.EffectiveSourceType}#{invalidationDebug.EffectiveSourceName}");
        lines.Add($"dirty_bounds_visual={invalidationDebug.DirtyBoundsVisualType}#{invalidationDebug.DirtyBoundsVisualName}");
        lines.Add($"dirty_bounds_hint={invalidationDebug.DirtyBoundsUsedHint}");
        lines.Add($"dirty_bounds={(invalidationDebug.HasDirtyBounds ? FormatRect(invalidationDebug.DirtyBounds) : "none")}");
        lines.Add($"traversal_clip={FormatRect(traversalClip)}");
        lines.Add($"traversal_visited={traversalMetrics.NodesVisited}");
        lines.Add($"traversal_drawn={traversalMetrics.NodesDrawn}");
        lines.Add($"editor_subtree_drawn={editorSubtreeDrawnCount}");
        lines.Add($"non_editor_subtree_drawn={nonEditorSubtreeDrawnCount}");
        lines.Add($"hottest_element_path={hottestElement.Path}");
        lines.Add($"hottest_element_measure_ms={hottestElement.MeasureMilliseconds:0.###}");
        lines.Add($"hottest_element_measure_exclusive_ms={hottestElement.MeasureExclusiveMilliseconds:0.###}");
        lines.Add($"hottest_element_arrange_ms={hottestElement.ArrangeMilliseconds:0.###}");
        lines.Add($"document_text_after={DocumentEditing.GetText(editor.Document).Replace(Environment.NewLine, "\\n", StringComparison.Ordinal)}");
        lines.Add($"recent_operations={recentOperations}");
        lines.Add($"dirty_queue={dirtyQueueSummary}");
        lines.Add($"non_editor_draw_sample={nonEditorDrawSample}");
        lines.Add($"dirty_bounds_trace={string.Join(" | ", dirtyBoundsTrace)}");
        lines.Add(
            afterEditor.LastStructuredEnterUsedDocumentReplacement &&
            afterEditor.LastStructuredEnterCloneDocumentMilliseconds + afterEditor.LastStructuredEnterCommitMilliseconds >
            Math.Max(uiRoot.LastLayoutPhaseMs, afterView.RefreshEditorUiStateTotalMilliseconds - beforeView.RefreshEditorUiStateTotalMilliseconds)
                ? "hotspot_inference=Exact hotspot candidate: RichTextBox structured Enter path is dominated by whole-document clone/replace work."
                : !afterEditor.LastStructuredEnterUsedDocumentReplacement &&
                  afterEditor.LastStructuredEnterTotalMilliseconds < 30d &&
                  !isFullDirty
                    ? "hotspot_inference=The old structured Enter hotspot is gone; Enter now stays on the paragraph-local split path with partial dirty redraw."
                    : "hotspot_inference=Logs did not isolate the embedded UI Enter hotspot yet; add narrower instrumentation.");

        var logPath = GetDiagnosticsLogPath("controls-catalog-richtextbox-embedded-ui-enter-hotspot");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.WriteAllLines(logPath, lines);

        Assert.True(File.Exists(logPath));
    }

    private static void RunTypingDiagnostics(
        string? presetButtonLabel,
        string typedText,
        string logFileNameWithoutExtension,
        string scenario)
    {
        Dispatcher.ResetForTests();
        AnimationManager.Current.ResetForTests();
        FocusManager.ClearFocus();
        FocusManager.ClearPointerCapture();

        var catalog = new ControlsCatalogView();
        catalog.ShowControl("RichTextBox");
        var uiRoot = CreateUiRoot(catalog);
        RunFrame(uiRoot, 16);
        RunFrame(uiRoot, 16);
        RunFrame(uiRoot, 16);

        var controlButtonsHost = Assert.IsType<StackPanel>(catalog.FindName("ControlButtonsHost"));
        var richTextButton = FindFirstVisualChild<Button>(
                                 controlButtonsHost,
                                 static button => string.Equals(button.GetContentText(), "RichTextBox", StringComparison.Ordinal))
                             ?? throw new InvalidOperationException("Could not find RichTextBox button in Controls Catalog.");

        Click(uiRoot, GetCenter(richTextButton));
        RunFrame(uiRoot, 16);
        RunFrame(uiRoot, 16);

        if (!TryGetSelectedRichTextBoxView(catalog, out var view))
        {
            catalog.ShowControl("RichTextBox");
            RunFrame(uiRoot, 16);
            RunFrame(uiRoot, 16);
            Assert.True(TryGetSelectedRichTextBoxView(catalog, out view), "Expected Controls Catalog to present RichTextBox view.");
        }

        Assert.NotNull(view);
        var editor = Assert.IsType<RichTextBox>(view.FindName("Editor"));

        if (!string.IsNullOrEmpty(presetButtonLabel))
        {
            var presetButton = FindFirstVisualChild<Button>(
                                   view,
                                   button => string.Equals(button.GetContentText(), presetButtonLabel, StringComparison.Ordinal))
                               ?? throw new InvalidOperationException($"Could not find preset button '{presetButtonLabel}'.");
            InvokeButtonClick(presetButton);
            RunFrame(uiRoot, 16);
            RunFrame(uiRoot, 16);

            if (string.Equals(presetButtonLabel, "Embedded UI", StringComparison.Ordinal))
            {
                Assert.True(
                    DocumentEditing.GetText(editor.Document).Contains('\uFFFC'),
                    "Expected Embedded UI preset to populate hosted document children before typing.");
            }
        }

        Click(uiRoot, GetCenter(editor));
        RunFrame(uiRoot, 16);
        RunFrame(uiRoot, 16);

        uiRoot.SetFocusedElementForTests(editor);
        editor.SetFocusedFromInput(true);
        if (string.Equals(presetButtonLabel, "Embedded UI", StringComparison.Ordinal))
        {
            editor.Select(0, 0);
        }

        SimulatePostDrawCleanup(uiRoot);

        editor.ResetPerformanceSnapshot();
        view.ResetDiagnosticsForTests();

        var lines = new List<string>
        {
            $"scenario={scenario}",
            $"timestamp_utc={DateTime.UtcNow:O}",
            $"log_path={GetDiagnosticsLogPath(logFileNameWithoutExtension)}",
            "step_1=open Controls Catalog",
            "step_2=click RichTextBox button in sidebar",
            !string.IsNullOrEmpty(presetButtonLabel)
                ? $"step_3=click {presetButtonLabel} preset button"
                : "step_3=click inside RichTextBox editor",
            !string.IsNullOrEmpty(presetButtonLabel)
                ? "step_4=click inside RichTextBox editor"
                : $"step_4=type \"{typedText}\" through UiRoot text input pipeline",
            !string.IsNullOrEmpty(presetButtonLabel)
                ? $"step_5=type \"{typedText}\" through UiRoot text input pipeline"
                : null,
            $"editor_slot={FormatRect(editor.LayoutSlot)}"
        }.Where(static line => line != null).Cast<string>().ToList();

        var stepMetrics = new List<TypingStepMetrics>();
        foreach (var character in typedText)
        {
            uiRoot.ClearDirtyBoundsEventTraceForTests();
            var beforeView = view.GetDiagnosticsSnapshotForTests();
            var beforeEditor = editor.GetPerformanceSnapshot();
            var beforeLayout = SnapshotElementTimings(catalog);
            var beforeRender = uiRoot.GetRenderTelemetrySnapshotForTests();

            Assert.True(editor.HandleTextInputFromInput(character));
            RunFrame(uiRoot, 16);

            var afterView = view.GetDiagnosticsSnapshotForTests();
            var afterEditor = editor.GetPerformanceSnapshot();
            var rootTelemetry = uiRoot.GetPerformanceTelemetrySnapshotForTests();
            var renderTelemetry = uiRoot.GetRenderTelemetrySnapshotForTests();
            var invalidationDebug = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
            var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();
            var dirtyRegionCount = dirtyRegions.Count;
            var dirtyCoverage = uiRoot.GetDirtyCoverageForTests();
            var wouldUsePartialDirtyRedraw = uiRoot.WouldUsePartialDirtyRedrawForTests();
            var isFullDirty = uiRoot.IsFullDirtyForTests();
            var dirtyQueue = uiRoot.GetDirtyRenderQueueSnapshotForTests();
            var dirtyBoundsTrace = uiRoot.GetDirtyBoundsEventTraceForTests();
            var largestDirtyRegion = dirtyRegions.OrderByDescending(static region => region.Width * region.Height).FirstOrDefault();
            var traversalClip = dirtyRegionCount > 0
                ? largestDirtyRegion
                : new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight);
            var traversalMetrics = uiRoot.GetRetainedTraversalMetricsForClipForTests(traversalClip);
            var drawOrder = uiRoot.GetRetainedDrawOrderForClipForTests(traversalClip);
            var editorSubtreeDrawnCount = drawOrder.Count(visual => IsDescendantOrSelf(editor, visual));
            var nonEditorSubtreeDrawnCount = drawOrder.Count - editorSubtreeDrawnCount;
            var dirtyQueueSummary = string.Join(" | ", dirtyQueue.Take(6).Select(DescribeElement));
            var nonEditorDrawSample = string.Join(
                " | ",
                drawOrder.Where(visual => !IsDescendantOrSelf(editor, visual))
                    .Take(6)
                    .Select(visual => DescribeVisualPath(visual, catalog)));
            var hottestElement = CaptureHottestElementTiming(catalog, beforeLayout);
            stepMetrics.Add(new TypingStepMetrics(
                character,
                uiRoot.LastUpdateMs,
                uiRoot.LastInputPhaseMs,
                uiRoot.LastBindingPhaseMs,
                uiRoot.LastLayoutPhaseMs,
                uiRoot.LastAnimationPhaseMs,
                uiRoot.LastRenderSchedulingPhaseMs,
                uiRoot.LastDeferredOperationCount,
                uiRoot.LayoutPasses,
                rootTelemetry.LayoutMeasureWorkMilliseconds,
                rootTelemetry.LayoutMeasureExclusiveWorkMilliseconds,
                rootTelemetry.LayoutArrangeWorkMilliseconds,
                rootTelemetry.HottestLayoutMeasureElementType,
                rootTelemetry.HottestLayoutMeasureElementName,
                rootTelemetry.HottestLayoutMeasureElementMilliseconds,
                rootTelemetry.HottestLayoutArrangeElementType,
                rootTelemetry.HottestLayoutArrangeElementName,
                rootTelemetry.HottestLayoutArrangeElementMilliseconds,
                hottestElement.Path,
                hottestElement.MeasureMilliseconds,
                hottestElement.MeasureExclusiveMilliseconds,
                hottestElement.ArrangeMilliseconds,
                afterView.RequestUiRefreshCount - beforeView.RequestUiRefreshCount,
                afterView.QueuedEditorRefreshCount - beforeView.QueuedEditorRefreshCount,
                afterView.RefreshUiStateCount - beforeView.RefreshUiStateCount,
                afterView.RefreshUiStateTotalMilliseconds - beforeView.RefreshUiStateTotalMilliseconds,
                afterView.RefreshEditorUiStateCount - beforeView.RefreshEditorUiStateCount,
                afterView.RefreshEditorUiStateTotalMilliseconds - beforeView.RefreshEditorUiStateTotalMilliseconds,
                afterView.DocumentStatsCount - beforeView.DocumentStatsCount,
                afterView.DocumentStatsTotalMilliseconds - beforeView.DocumentStatsTotalMilliseconds,
                afterView.UpdateCommandStatesCount - beforeView.UpdateCommandStatesCount,
                afterView.UpdateCommandStatesTotalMilliseconds - beforeView.UpdateCommandStatesTotalMilliseconds,
                afterView.UpdateEditorCommandStatesCount - beforeView.UpdateEditorCommandStatesCount,
                afterView.UpdateEditorCommandStatesTotalMilliseconds - beforeView.UpdateEditorCommandStatesTotalMilliseconds,
                afterView.UpdateStatusLabelsCount - beforeView.UpdateStatusLabelsCount,
                afterView.UpdateStatusLabelsTotalMilliseconds - beforeView.UpdateStatusLabelsTotalMilliseconds,
                afterView.UpdatePayloadMetaCount - beforeView.UpdatePayloadMetaCount,
                afterView.UpdatePayloadMetaTotalMilliseconds - beforeView.UpdatePayloadMetaTotalMilliseconds,
                afterView.UpdateHeroSummaryCount - beforeView.UpdateHeroSummaryCount,
                afterView.UpdateHeroSummaryTotalMilliseconds - beforeView.UpdateHeroSummaryTotalMilliseconds,
                afterView.UpdatePresetHintsCount - beforeView.UpdatePresetHintsCount,
                afterView.UpdatePresetHintsTotalMilliseconds - beforeView.UpdatePresetHintsTotalMilliseconds,
                afterEditor.EditSampleCount - beforeEditor.EditSampleCount,
                afterEditor.LastEditMilliseconds,
                afterEditor.LayoutCacheHitCount - beforeEditor.LayoutCacheHitCount,
                afterEditor.LayoutCacheMissCount - beforeEditor.LayoutCacheMissCount,
                afterEditor.LayoutBuildSampleCount - beforeEditor.LayoutBuildSampleCount,
                afterEditor.MaxLayoutBuildMilliseconds,
                afterEditor.SelectionGeometrySampleCount - beforeEditor.SelectionGeometrySampleCount,
                afterEditor.LastSelectionGeometryMilliseconds,
                dirtyRegionCount,
                dirtyCoverage,
                wouldUsePartialDirtyRedraw,
                isFullDirty,
                Math.Max(0, renderTelemetry.DirtyRootCount - beforeRender.DirtyRootCount),
                Math.Max(0, renderTelemetry.RetainedTraversalCount - beforeRender.RetainedTraversalCount),
                Math.Max(0, renderTelemetry.DirtyRegionTraversalCount - beforeRender.DirtyRegionTraversalCount),
                Math.Max(0, renderTelemetry.DirtyRegionThresholdFallbackCount - beforeRender.DirtyRegionThresholdFallbackCount),
                Math.Max(0, renderTelemetry.FullDirtyVisualStructureChangeCount - beforeRender.FullDirtyVisualStructureChangeCount),
                Math.Max(0, renderTelemetry.FullDirtyRetainedRebuildCount - beforeRender.FullDirtyRetainedRebuildCount),
                Math.Max(0, renderTelemetry.FullDirtyDetachedVisualCount - beforeRender.FullDirtyDetachedVisualCount),
                invalidationDebug.RequestedSourceType,
                invalidationDebug.RequestedSourceName,
                invalidationDebug.EffectiveSourceType,
                invalidationDebug.EffectiveSourceName,
                invalidationDebug.DirtyBoundsVisualType,
                invalidationDebug.DirtyBoundsVisualName,
                invalidationDebug.DirtyBoundsUsedHint,
                invalidationDebug.HasDirtyBounds ? FormatRect(invalidationDebug.DirtyBounds) : "none",
                FormatRect(traversalClip),
                traversalMetrics.NodesVisited,
                traversalMetrics.NodesDrawn,
                editorSubtreeDrawnCount,
                nonEditorSubtreeDrawnCount,
                dirtyQueueSummary,
                nonEditorDrawSample,
                string.Join(" | ", dirtyBoundsTrace)));

            SimulatePostDrawCleanup(uiRoot);
        }

        var summary = Summarize(stepMetrics);
        lines.Add(string.Empty);
        lines.Add("summary:");
        lines.Add($"typed_character_count={stepMetrics.Count}");
        lines.Add($"total_update_ms={summary.TotalUpdateMilliseconds:0.###}");
        lines.Add($"total_input_ms={summary.TotalInputMilliseconds:0.###}");
        lines.Add($"total_binding_ms={summary.TotalBindingMilliseconds:0.###}");
        lines.Add($"total_layout_ms={summary.TotalLayoutMilliseconds:0.###}");
        lines.Add($"total_layout_measure_work_ms={summary.TotalLayoutMeasureWorkMilliseconds:0.###}");
        lines.Add($"total_layout_measure_exclusive_work_ms={summary.TotalLayoutMeasureExclusiveWorkMilliseconds:0.###}");
        lines.Add($"total_layout_arrange_work_ms={summary.TotalLayoutArrangeWorkMilliseconds:0.###}");
        lines.Add($"total_refresh_ui_ms={summary.TotalRefreshUiStateMilliseconds:0.###}");
        lines.Add($"total_refresh_editor_ui_ms={summary.TotalRefreshEditorUiStateMilliseconds:0.###}");
        lines.Add($"total_document_stats_ms={summary.TotalDocumentStatsMilliseconds:0.###}");
        lines.Add($"total_update_command_states_ms={summary.TotalUpdateCommandStatesMilliseconds:0.###}");
        lines.Add($"total_update_editor_command_states_ms={summary.TotalUpdateEditorCommandStatesMilliseconds:0.###}");
        lines.Add($"total_update_status_labels_ms={summary.TotalUpdateStatusLabelsMilliseconds:0.###}");
        lines.Add($"total_update_payload_meta_ms={summary.TotalUpdatePayloadMetaMilliseconds:0.###}");
        lines.Add($"total_update_hero_summary_ms={summary.TotalUpdateHeroSummaryMilliseconds:0.###}");
        lines.Add($"total_update_preset_hints_ms={summary.TotalUpdatePresetHintsMilliseconds:0.###}");
        lines.Add($"total_deferred_operations={summary.TotalDeferredOperations}");
        lines.Add($"total_layout_passes={summary.TotalLayoutPasses}");
        lines.Add($"total_editor_edits={summary.TotalEditorEditSamples}");
        lines.Add($"total_layout_cache_hits={summary.TotalLayoutCacheHits}");
        lines.Add($"total_layout_cache_misses={summary.TotalLayoutCacheMisses}");
        lines.Add($"total_layout_build_samples={summary.TotalLayoutBuildSamples}");
        lines.Add($"total_partial_dirty_steps={summary.TotalPartialDirtySteps}");
        lines.Add($"total_full_dirty_steps={summary.TotalFullDirtySteps}");
        lines.Add($"total_dirty_region_fallbacks={summary.TotalDirtyRegionFallbacks}");
        lines.Add($"max_dirty_region_count={summary.MaxDirtyRegionCount}");
        lines.Add($"max_dirty_region_coverage={summary.MaxDirtyRegionCoverage:0.###}");
        lines.Add($"hotspot_inference={summary.HotspotInference}");
        lines.Add($"hottest_step={summary.HottestStepLabel}");
        lines.Add($"hottest_step_detail={summary.HottestStepDetail}");

        lines.Add(string.Empty);
        lines.Add("steps:");
        lines.AddRange(stepMetrics.Select(FormatStep));

        var logPath = GetDiagnosticsLogPath(logFileNameWithoutExtension);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.WriteAllLines(logPath, lines);

        Assert.True(File.Exists(logPath));
    }

    private static TypingSummary Summarize(IReadOnlyList<TypingStepMetrics> steps)
    {
        var hottest = steps
            .OrderByDescending(static step => step.RefreshEditorUiStateMilliseconds + step.DocumentStatsMilliseconds + step.UpdateCommandStatesMilliseconds + step.UpdateEditorCommandStatesMilliseconds)
            .ThenByDescending(static step => step.LayoutMilliseconds)
            .First();

        var totalRefreshEditorUiMs = steps.Sum(static step => step.RefreshEditorUiStateMilliseconds);
        var totalDocumentStatsMs = steps.Sum(static step => step.DocumentStatsMilliseconds);
        var totalUpdateCommandStatesMs = steps.Sum(static step => step.UpdateCommandStatesMilliseconds);
        var totalUpdateEditorCommandStatesMs = steps.Sum(static step => step.UpdateEditorCommandStatesMilliseconds);

        var totalLayoutMs = steps.Sum(static step => step.LayoutMilliseconds);
        var totalLayoutMeasureExclusiveMs = steps.Sum(static step => step.LayoutMeasureExclusiveWorkMilliseconds);
        var totalPartialDirtySteps = steps.Count(static step => step.WouldUsePartialDirtyRedraw);
        var totalFullDirtySteps = steps.Count(static step => step.IsFullDirty);
        var totalDirtyRegionFallbacks = steps.Sum(static step => step.DirtyRegionThresholdFallbackCount);

        var hotspotInference =
            totalRefreshEditorUiMs > totalLayoutMs &&
            totalDocumentStatsMs >= totalUpdateCommandStatesMs &&
            totalDocumentStatsMs >= totalUpdateEditorCommandStatesMs
                ? "Exact hotspot candidate: RichTextBoxView.RefreshEditorUiState -> DocumentStats.FromDocument, which recomputes full document stats after each typed character."
                : totalRefreshEditorUiMs > totalLayoutMs &&
                  totalUpdateCommandStatesMs + totalUpdateEditorCommandStatesMs > totalDocumentStatsMs
                    ? "Exact hotspot candidate: RichTextBoxView command-state refresh after each typed character, not RichTextBox edit/layout work."
                    : totalLayoutMs > totalRefreshEditorUiMs &&
                      totalLayoutMeasureExclusiveMs > totalDocumentStatsMs &&
                      steps.Select(static step => step.HottestElementPath)
                          .Any(static path => path.Contains(nameof(RichTextBox), StringComparison.Ordinal))
                        ? "Exact hotspot candidate: RichTextBox measure work in the layout phase, not catalog sidebar refresh work."
                    : steps.Any(static step => step.WouldUsePartialDirtyRedraw &&
                                               step.NonEditorSubtreeTraversalCount > step.EditorSubtreeTraversalCount)
                        ? "Exact hotspot candidate: retained dirty-region traversal still walks large non-editor subtrees for the RichTextBox dirty clip, so the remaining cost is below RichTextBox in retained bounds/clip culling."
                    : steps.All(static step => !step.WouldUsePartialDirtyRedraw) &&
                      steps.Sum(static step => step.DirtyRegionThresholdFallbackCount) > 0
                        ? "Exact hotspot candidate: dirty-region rendering falls back before draw, so the remaining cost is in root dirty-region promotion rather than RichTextBox edit/render work."
                    : steps.All(static step => !step.WouldUsePartialDirtyRedraw) &&
                      steps.Any(static step => !string.Equals(step.EffectiveInvalidationSourceType, nameof(RichTextBox), StringComparison.Ordinal))
                        ? "Exact hotspot candidate: render invalidation resolves to a retained ancestor outside RichTextBox, so editor-local typing is being promoted to a viewport-sized dirty region before draw."
                    : "Logs did not isolate the typing hotspot yet; add narrower instrumentation.";

        return new TypingSummary(
            steps.Sum(static step => step.UpdateMilliseconds),
            steps.Sum(static step => step.InputMilliseconds),
            steps.Sum(static step => step.BindingMilliseconds),
            steps.Sum(static step => step.LayoutMilliseconds),
            steps.Sum(static step => step.LayoutMeasureWorkMilliseconds),
            totalLayoutMeasureExclusiveMs,
            steps.Sum(static step => step.LayoutArrangeWorkMilliseconds),
            steps.Sum(static step => step.RefreshUiStateMilliseconds),
            totalRefreshEditorUiMs,
            totalDocumentStatsMs,
            totalUpdateCommandStatesMs,
            totalUpdateEditorCommandStatesMs,
            steps.Sum(static step => step.UpdateStatusLabelsMilliseconds),
            steps.Sum(static step => step.UpdatePayloadMetaMilliseconds),
            steps.Sum(static step => step.UpdateHeroSummaryMilliseconds),
            steps.Sum(static step => step.UpdatePresetHintsMilliseconds),
            steps.Sum(static step => step.DeferredOperations),
            steps.Sum(static step => step.LayoutPasses),
            steps.Sum(static step => step.EditorEditSamples),
            steps.Sum(static step => step.LayoutCacheHits),
            steps.Sum(static step => step.LayoutCacheMisses),
            steps.Sum(static step => step.LayoutBuildSamples),
            totalPartialDirtySteps,
            totalFullDirtySteps,
            totalDirtyRegionFallbacks,
            steps.Max(static step => step.DirtyRegionCount),
            steps.Max(static step => step.DirtyCoverage),
            hotspotInference,
            hottest.Character.ToString(),
            $"updateMs={hottest.UpdateMilliseconds:0.###}, bindingMs={hottest.BindingMilliseconds:0.###}, layoutMs={hottest.LayoutMilliseconds:0.###}, layoutMeasureExclusiveMs={hottest.LayoutMeasureExclusiveWorkMilliseconds:0.###}, hottestMeasure={hottest.HottestLayoutMeasureElementType}({hottest.HottestLayoutMeasureElementName})/{hottest.HottestLayoutMeasureElementMilliseconds:0.###}, hottestArrange={hottest.HottestLayoutArrangeElementType}({hottest.HottestLayoutArrangeElementName})/{hottest.HottestLayoutArrangeElementMilliseconds:0.###}, hottestElementPath={hottest.HottestElementPath}, hottestElementMeasureMs={hottest.HottestElementMeasureMilliseconds:0.###}, hottestElementMeasureExclusiveMs={hottest.HottestElementMeasureExclusiveMilliseconds:0.###}, refreshEditorUiMs={hottest.RefreshEditorUiStateMilliseconds:0.###}, documentStatsMs={hottest.DocumentStatsMilliseconds:0.###}, editorLastEditMs={hottest.EditorLastEditMilliseconds:0.###}, layoutBuildMaxMs={hottest.EditorMaxLayoutBuildMilliseconds:0.###}, dirtyRegions={hottest.DirtyRegionCount}, dirtyCoverage={hottest.DirtyCoverage:0.###}, partialDirty={hottest.WouldUsePartialDirtyRedraw}, requestedSource={hottest.RequestedInvalidationSourceType}#{hottest.RequestedInvalidationSourceName}, effectiveSource={hottest.EffectiveInvalidationSourceType}#{hottest.EffectiveInvalidationSourceName}, dirtyBoundsVisual={hottest.DirtyBoundsVisualType}#{hottest.DirtyBoundsVisualName}, dirtyBoundsHint={hottest.DirtyBoundsUsedHint}, dirtyBounds={hottest.DirtyBoundsRect}, traversalClip={hottest.TraversalClip}, traversalNodesDrawn={hottest.TraversalNodesDrawn}, nonEditorSubtreeTraversal={hottest.NonEditorSubtreeTraversalCount}");
    }

    private static string FormatStep(TypingStepMetrics step)
    {
        return
            $"char={step.Character} updateMs={step.UpdateMilliseconds:0.###} inputMs={step.InputMilliseconds:0.###} bindingMs={step.BindingMilliseconds:0.###} layoutMs={step.LayoutMilliseconds:0.###} animationMs={step.AnimationMilliseconds:0.###} renderScheduleMs={step.RenderSchedulingMilliseconds:0.###} " +
            $"deferredOps={step.DeferredOperations} layoutPasses={step.LayoutPasses} layoutMeasureWorkMs={step.LayoutMeasureWorkMilliseconds:0.###} layoutMeasureExclusiveMs={step.LayoutMeasureExclusiveWorkMilliseconds:0.###} layoutArrangeWorkMs={step.LayoutArrangeWorkMilliseconds:0.###} hottestMeasure={step.HottestLayoutMeasureElementType}({step.HottestLayoutMeasureElementName})/{step.HottestLayoutMeasureElementMilliseconds:0.###} hottestArrange={step.HottestLayoutArrangeElementType}({step.HottestLayoutArrangeElementName})/{step.HottestLayoutArrangeElementMilliseconds:0.###} hottestElementPath={step.HottestElementPath} hottestElementMeasureMs={step.HottestElementMeasureMilliseconds:0.###} hottestElementMeasureExclusiveMs={step.HottestElementMeasureExclusiveMilliseconds:0.###} hottestElementArrangeMs={step.HottestElementArrangeMilliseconds:0.###} requestUiRefresh={step.RequestUiRefreshCount} queuedEditorRefresh={step.QueuedEditorRefreshCount} refreshUiCount={step.RefreshUiStateCount} refreshUiMs={step.RefreshUiStateMilliseconds:0.###} " +
            $"refreshEditorUiCount={step.RefreshEditorUiStateCount} refreshEditorUiMs={step.RefreshEditorUiStateMilliseconds:0.###} documentStatsCount={step.DocumentStatsCount} documentStatsMs={step.DocumentStatsMilliseconds:0.###} " +
            $"updateCommandStatesCount={step.UpdateCommandStatesCount} updateCommandStatesMs={step.UpdateCommandStatesMilliseconds:0.###} updateEditorCommandStatesCount={step.UpdateEditorCommandStatesCount} updateEditorCommandStatesMs={step.UpdateEditorCommandStatesMilliseconds:0.###} " +
            $"updateStatusLabelsCount={step.UpdateStatusLabelsCount} updateStatusLabelsMs={step.UpdateStatusLabelsMilliseconds:0.###} updatePayloadMetaCount={step.UpdatePayloadMetaCount} updatePayloadMetaMs={step.UpdatePayloadMetaMilliseconds:0.###} " +
            $"updateHeroSummaryCount={step.UpdateHeroSummaryCount} updateHeroSummaryMs={step.UpdateHeroSummaryMilliseconds:0.###} updatePresetHintsCount={step.UpdatePresetHintsCount} updatePresetHintsMs={step.UpdatePresetHintsMilliseconds:0.###} " +
            $"editorEditSamples={step.EditorEditSamples} editorLastEditMs={step.EditorLastEditMilliseconds:0.###} layoutCacheHits={step.LayoutCacheHits} layoutCacheMisses={step.LayoutCacheMisses} layoutBuildSamples={step.LayoutBuildSamples} layoutBuildMaxMs={step.EditorMaxLayoutBuildMilliseconds:0.###} selectionGeometrySamples={step.SelectionGeometrySamples} selectionGeometryLastMs={step.SelectionGeometryLastMilliseconds:0.###} " +
            $"dirtyRegions={step.DirtyRegionCount} dirtyCoverage={step.DirtyCoverage:0.###} partialDirty={step.WouldUsePartialDirtyRedraw} fullDirty={step.IsFullDirty} dirtyRoots={step.DirtyRootCount} retainedTraversals={step.RetainedTraversalCount} dirtyRegionTraversals={step.DirtyRegionTraversalCount} dirtyFallbacks={step.DirtyRegionThresholdFallbackCount} fullDirtyStructure={step.FullDirtyVisualStructureChangeCount} fullDirtyRetainedRebuild={step.FullDirtyRetainedRebuildCount} fullDirtyDetached={step.FullDirtyDetachedVisualCount} requestedSource={step.RequestedInvalidationSourceType}#{step.RequestedInvalidationSourceName} effectiveSource={step.EffectiveInvalidationSourceType}#{step.EffectiveInvalidationSourceName} dirtyBoundsVisual={step.DirtyBoundsVisualType}#{step.DirtyBoundsVisualName} dirtyBoundsHint={step.DirtyBoundsUsedHint} dirtyBounds={step.DirtyBoundsRect} traversalClip={step.TraversalClip} traversalVisited={step.TraversalNodesVisited} traversalDrawn={step.TraversalNodesDrawn} editorSubtreeDrawn={step.EditorSubtreeTraversalCount} nonEditorSubtreeDrawn={step.NonEditorSubtreeTraversalCount} dirtyQueue={step.DirtyQueueSummary} nonEditorDrawSample={step.NonEditorDrawSample} dirtyBoundsTrace={step.DirtyBoundsTrace}";
    }

    private static UiRoot CreateUiRoot(UIElement content)
    {
        var host = new Grid
        {
            Width = ViewportWidth,
            Height = ViewportHeight
        };

        host.AddChild(content);
        if (content is FrameworkElement frameworkElement)
        {
            frameworkElement.Width = ViewportWidth;
            frameworkElement.Height = ViewportHeight;
        }

        return new UiRoot(host);
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool pointerMoved = false, bool leftPressed = false, bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = [],
            ReleasedKeys = [],
            TextInput = [],
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void InvokeButtonClick(Button button)
    {
        var onClick = typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(onClick);
        onClick!.Invoke(button, null);
    }

    private static void RunFrame(UiRoot uiRoot, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, ViewportWidth, ViewportHeight));
    }

    private static void SimulatePostDrawCleanup(UiRoot uiRoot)
    {
        uiRoot.ApplyRenderInvalidationCleanupForTests();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
    }

    private static bool TryGetSelectedRichTextBoxView(ControlsCatalogView catalog, out RichTextBoxView? view)
    {
        var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
        view = previewHost.Content as RichTextBoxView;
        return view != null;
    }

    private static IReadOnlyList<string> GetRecentOperations(RichTextBox editor)
    {
        var build = typeof(RichTextBox).GetMethod("BuildRecentOperationLines", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(build);
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(build!.Invoke(editor, null));
    }

    private static Dictionary<FrameworkElement, ElementTiming> SnapshotElementTimings(UIElement root)
    {
        return EnumerateVisualTree(root)
            .OfType<FrameworkElement>()
            .ToDictionary(
                static element => element,
                static element => new ElementTiming(
                    element.MeasureElapsedTicksForTests,
                    element.MeasureExclusiveElapsedTicksForTests,
                    element.ArrangeElapsedTicksForTests));
    }

    private static ElementTimingDelta CaptureHottestElementTiming(
        UIElement root,
        IReadOnlyDictionary<FrameworkElement, ElementTiming> beforeSnapshot)
    {
        ElementTimingDelta hottest = default;
        foreach (var element in EnumerateVisualTree(root).OfType<FrameworkElement>())
        {
            var before = beforeSnapshot.TryGetValue(element, out var snapshot)
                ? snapshot
                : default;
            var measureTicks = Math.Max(0L, element.MeasureElapsedTicksForTests - before.MeasureElapsedTicks);
            var measureExclusiveTicks = Math.Max(0L, element.MeasureExclusiveElapsedTicksForTests - before.MeasureExclusiveElapsedTicks);
            var arrangeTicks = Math.Max(0L, element.ArrangeElapsedTicksForTests - before.ArrangeElapsedTicks);
            if (measureExclusiveTicks <= hottest.MeasureExclusiveTicks)
            {
                continue;
            }

            hottest = new ElementTimingDelta(
                DescribeVisualPath(element, root),
                measureTicks,
                measureExclusiveTicks,
                arrangeTicks);
        }

        return hottest;
    }

    private static IEnumerable<UIElement> EnumerateVisualTree(UIElement root)
    {
        var stack = new Stack<UIElement>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            foreach (var child in current.GetVisualChildren())
            {
                stack.Push(child);
            }
        }
    }

    private static string DescribeVisualPath(UIElement element, UIElement root)
    {
        var segments = new Stack<string>();
        UIElement? current = element;
        while (current != null)
        {
            segments.Push(DescribeElement(current));
            if (ReferenceEquals(current, root))
            {
                break;
            }

            current = current.VisualParent;
        }

        return string.Join(" > ", segments);
    }

    private static string DescribeElement(UIElement element)
    {
        return element switch
        {
            Button button when !string.IsNullOrEmpty(button.GetContentText()) => $"{nameof(Button)}(\"{button.GetContentText()}\")",
            Label label when !string.IsNullOrEmpty(label.GetContentText()) => $"{nameof(Label)}(\"{label.GetContentText()}\")",
            TextBlock textBlock when !string.IsNullOrEmpty(textBlock.Text) => $"{nameof(TextBlock)}(\"{Truncate(textBlock.Text, 24)}\")",
            ContentControl { Name.Length: > 0 } contentControl => $"{nameof(ContentControl)}#{contentControl.Name}",
            FrameworkElement { Name.Length: > 0 } frameworkElement => $"{frameworkElement.GetType().Name}#{frameworkElement.Name}",
            _ => element.GetType().Name
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private static bool IsDescendantOrSelf(UIElement ancestor, UIElement candidate)
    {
        for (var current = candidate; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root, Func<TElement, bool>? predicate = null)
        where TElement : UIElement
    {
        if (root is TElement match && (predicate == null || predicate(match)))
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild(child, predicate);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Vector2 GetCenter(UIElement element)
    {
        return new Vector2(
            element.LayoutSlot.X + (element.LayoutSlot.Width * 0.5f),
            element.LayoutSlot.Y + (element.LayoutSlot.Height * 0.5f));
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"({rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##})";
    }

    private static string GetDiagnosticsLogPath(string fileNameWithoutExtension)
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "artifacts", "diagnostics", $"{fileNameWithoutExtension}.txt");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (current.EnumerateFiles("InkkSlinger.sln").Any())
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test base directory.");
    }

    private readonly record struct TypingStepMetrics(
        char Character,
        double UpdateMilliseconds,
        double InputMilliseconds,
        double BindingMilliseconds,
        double LayoutMilliseconds,
        double AnimationMilliseconds,
        double RenderSchedulingMilliseconds,
        int DeferredOperations,
        int LayoutPasses,
        double LayoutMeasureWorkMilliseconds,
        double LayoutMeasureExclusiveWorkMilliseconds,
        double LayoutArrangeWorkMilliseconds,
        string HottestLayoutMeasureElementType,
        string HottestLayoutMeasureElementName,
        double HottestLayoutMeasureElementMilliseconds,
        string HottestLayoutArrangeElementType,
        string HottestLayoutArrangeElementName,
        double HottestLayoutArrangeElementMilliseconds,
        string HottestElementPath,
        double HottestElementMeasureMilliseconds,
        double HottestElementMeasureExclusiveMilliseconds,
        double HottestElementArrangeMilliseconds,
        int RequestUiRefreshCount,
        int QueuedEditorRefreshCount,
        int RefreshUiStateCount,
        double RefreshUiStateMilliseconds,
        int RefreshEditorUiStateCount,
        double RefreshEditorUiStateMilliseconds,
        int DocumentStatsCount,
        double DocumentStatsMilliseconds,
        int UpdateCommandStatesCount,
        double UpdateCommandStatesMilliseconds,
        int UpdateEditorCommandStatesCount,
        double UpdateEditorCommandStatesMilliseconds,
        int UpdateStatusLabelsCount,
        double UpdateStatusLabelsMilliseconds,
        int UpdatePayloadMetaCount,
        double UpdatePayloadMetaMilliseconds,
        int UpdateHeroSummaryCount,
        double UpdateHeroSummaryMilliseconds,
        int UpdatePresetHintsCount,
        double UpdatePresetHintsMilliseconds,
        int EditorEditSamples,
        double EditorLastEditMilliseconds,
        int LayoutCacheHits,
        int LayoutCacheMisses,
        int LayoutBuildSamples,
        double EditorMaxLayoutBuildMilliseconds,
        int SelectionGeometrySamples,
        double SelectionGeometryLastMilliseconds,
        int DirtyRegionCount,
        double DirtyCoverage,
        bool WouldUsePartialDirtyRedraw,
        bool IsFullDirty,
        int DirtyRootCount,
        int RetainedTraversalCount,
        int DirtyRegionTraversalCount,
        int DirtyRegionThresholdFallbackCount,
        int FullDirtyVisualStructureChangeCount,
        int FullDirtyRetainedRebuildCount,
        int FullDirtyDetachedVisualCount,
        string RequestedInvalidationSourceType,
        string RequestedInvalidationSourceName,
        string EffectiveInvalidationSourceType,
        string EffectiveInvalidationSourceName,
        string DirtyBoundsVisualType,
        string DirtyBoundsVisualName,
        bool DirtyBoundsUsedHint,
        string DirtyBoundsRect,
        string TraversalClip,
        int TraversalNodesVisited,
        int TraversalNodesDrawn,
        int EditorSubtreeTraversalCount,
        int NonEditorSubtreeTraversalCount,
        string DirtyQueueSummary,
        string NonEditorDrawSample,
        string DirtyBoundsTrace);

    private readonly record struct TypingSummary(
        double TotalUpdateMilliseconds,
        double TotalInputMilliseconds,
        double TotalBindingMilliseconds,
        double TotalLayoutMilliseconds,
        double TotalLayoutMeasureWorkMilliseconds,
        double TotalLayoutMeasureExclusiveWorkMilliseconds,
        double TotalLayoutArrangeWorkMilliseconds,
        double TotalRefreshUiStateMilliseconds,
        double TotalRefreshEditorUiStateMilliseconds,
        double TotalDocumentStatsMilliseconds,
        double TotalUpdateCommandStatesMilliseconds,
        double TotalUpdateEditorCommandStatesMilliseconds,
        double TotalUpdateStatusLabelsMilliseconds,
        double TotalUpdatePayloadMetaMilliseconds,
        double TotalUpdateHeroSummaryMilliseconds,
        double TotalUpdatePresetHintsMilliseconds,
        int TotalDeferredOperations,
        int TotalLayoutPasses,
        int TotalEditorEditSamples,
        int TotalLayoutCacheHits,
        int TotalLayoutCacheMisses,
        int TotalLayoutBuildSamples,
        int TotalPartialDirtySteps,
        int TotalFullDirtySteps,
        int TotalDirtyRegionFallbacks,
        int MaxDirtyRegionCount,
        double MaxDirtyRegionCoverage,
        string HotspotInference,
        string HottestStepLabel,
        string HottestStepDetail);

    private readonly record struct ElementTiming(
        long MeasureElapsedTicks,
        long MeasureExclusiveElapsedTicks,
        long ArrangeElapsedTicks);

    private readonly record struct ElementTimingDelta(
        string Path,
        long MeasureTicks,
        long MeasureExclusiveTicks,
        long ArrangeTicks)
    {
        public double MeasureMilliseconds => MeasureTicks * 1000d / StopwatchFrequency;
        public double MeasureExclusiveMilliseconds => MeasureExclusiveTicks * 1000d / StopwatchFrequency;
        public double ArrangeMilliseconds => ArrangeTicks * 1000d / StopwatchFrequency;

        private static readonly double StopwatchFrequency = System.Diagnostics.Stopwatch.Frequency;
    }
}
