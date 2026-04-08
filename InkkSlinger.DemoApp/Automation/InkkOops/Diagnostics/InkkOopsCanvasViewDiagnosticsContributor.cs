using System.Diagnostics;

namespace InkkSlinger;

public sealed class InkkOopsCanvasViewDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 45;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not CanvasView)
        {
            return;
        }

        var canvasView = CanvasView.GetDiagnosticsSnapshotForDiagnostics();
        var textLayout = TextLayout.GetMetricsSnapshot();
        var textRenderer = UiTextRenderer.GetTimingSnapshotForTests();

        builder.Add("canvasViewDragCalls", canvasView.HandleFocusCardDragDeltaCallCount);
        builder.Add("canvasViewDragMs", FormatMilliseconds(canvasView.HandleFocusCardDragDeltaMilliseconds));
        builder.Add("canvasViewMoveFocusCalls", canvasView.MoveFocusByCallCount);
        builder.Add("canvasViewMoveFocusMs", FormatMilliseconds(canvasView.MoveFocusByMilliseconds));
        builder.Add("canvasViewApplySceneCalls", canvasView.ApplySceneStateCallCount);
        builder.Add("canvasViewApplySceneMs", FormatMilliseconds(canvasView.ApplySceneStateMilliseconds));
        builder.Add("canvasViewApplyAnchorsCalls", canvasView.ApplyFocusAnchorsCallCount);
        builder.Add("canvasViewApplyAnchorsMs", FormatMilliseconds(canvasView.ApplyFocusAnchorsMilliseconds));
        builder.Add("canvasViewApplyBadgeLayerCalls", canvasView.ApplyBadgeLayerCallCount);
        builder.Add("canvasViewApplyBadgeLayerMs", FormatMilliseconds(canvasView.ApplyBadgeLayerMilliseconds));
        builder.Add("canvasViewApplyGuideVisibilityCalls", canvasView.ApplyGuideVisibilityCallCount);
        builder.Add("canvasViewApplyGuideVisibilityMs", FormatMilliseconds(canvasView.ApplyGuideVisibilityMilliseconds));
        builder.Add("canvasViewUpdateLiveTextCalls", canvasView.UpdateLiveTextCallCount);
        builder.Add("canvasViewUpdateLiveTextMs", FormatMilliseconds(canvasView.UpdateLiveTextMilliseconds));
        builder.Add("canvasViewUpdateTelemetryCalls", canvasView.UpdateTelemetryCallCount);
        builder.Add("canvasViewUpdateTelemetryMs", FormatMilliseconds(canvasView.UpdateTelemetryMilliseconds));
        builder.Add("canvasViewSyncOverlayLayoutCalls", canvasView.SyncOverlayLayoutCallCount);
        builder.Add("canvasViewSyncOverlayLayoutMs", FormatMilliseconds(canvasView.SyncOverlayLayoutMilliseconds));
        builder.Add("canvasViewSetTextChanges", canvasView.SetTextChangeCount);
        builder.Add("canvasViewSetTextMs", FormatMilliseconds(canvasView.SetTextMilliseconds));
        builder.Add("canvasViewSetTextTargets", canvasView.SetTextTargetSummary);
        builder.Add("canvasViewSetCanvasLeftChanges", canvasView.SetCanvasLeftChangeCount);
        builder.Add("canvasViewSetCanvasLeftMs", FormatMilliseconds(canvasView.SetCanvasLeftMilliseconds));
        builder.Add("canvasViewSetCanvasTopChanges", canvasView.SetCanvasTopChangeCount);
        builder.Add("canvasViewSetCanvasTopMs", FormatMilliseconds(canvasView.SetCanvasTopMilliseconds));

        builder.Add("textLayoutRequests", textLayout.LayoutRequestCount);
        builder.Add("textLayoutCacheHits", textLayout.CacheHitCount);
        builder.Add("textLayoutCacheMisses", textLayout.CacheMissCount);
        builder.Add("textLayoutBuilds", textLayout.BuildCount);
        builder.Add("textLayoutWrappedBuilds", textLayout.WrappedBuildCount);
        builder.Add("textLayoutNoWrapBuilds", textLayout.NoWrapBuildCount);
        builder.Add("textLayoutMeasuredChars", textLayout.TotalMeasuredTextLength);
        builder.Add("textLayoutProducedLines", textLayout.TotalProducedLineCount);
        builder.Add("textLayoutCacheEntries", textLayout.CacheEntryCount);
        builder.Add("textLayoutMs", FormatStopwatchTicks(textLayout.LayoutElapsedTicks));
        builder.Add("textLayoutBuildMs", FormatStopwatchTicks(textLayout.BuildElapsedTicks));

        builder.Add("textRendererMeasureWidthCalls", textRenderer.MeasureWidthCallCount);
        builder.Add("textRendererMeasureWidthMs", FormatStopwatchTicks(textRenderer.MeasureWidthElapsedTicks));
        builder.Add("textRendererLineHeightCalls", textRenderer.GetLineHeightCallCount);
        builder.Add("textRendererLineHeightMs", FormatStopwatchTicks(textRenderer.GetLineHeightElapsedTicks));
        builder.Add("textRendererDrawStringCalls", textRenderer.DrawStringCallCount);
        builder.Add("textRendererDrawStringMs", FormatStopwatchTicks(textRenderer.DrawStringElapsedTicks));
        builder.Add("textRendererTypefaceCacheHits", textRenderer.TypefaceCacheHitCount);
        builder.Add("textRendererTypefaceCacheMisses", textRenderer.TypefaceCacheMissCount);
        builder.Add("textRendererMetricsCacheHits", textRenderer.MetricsCacheHitCount);
        builder.Add("textRendererMetricsCacheMisses", textRenderer.MetricsCacheMissCount);
        builder.Add("textRendererLineHeightCacheHits", textRenderer.LineHeightCacheHitCount);
        builder.Add("textRendererLineHeightCacheMisses", textRenderer.LineHeightCacheMissCount);
        builder.Add("textRendererHottestDrawStringText", Escape(textRenderer.HottestDrawStringText));
        builder.Add("textRendererHottestDrawStringTypography", Escape(textRenderer.HottestDrawStringTypography));
        builder.Add("textRendererHottestDrawStringMs", FormatMilliseconds(textRenderer.HottestDrawStringMilliseconds));
        builder.Add("textRendererHottestMeasureWidthText", Escape(textRenderer.HottestMeasureWidthText));
        builder.Add("textRendererHottestMeasureWidthTypography", Escape(textRenderer.HottestMeasureWidthTypography));
        builder.Add("textRendererHottestMeasureWidthMs", FormatMilliseconds(textRenderer.HottestMeasureWidthMilliseconds));
        builder.Add("textRendererHottestLineHeightTypography", Escape(textRenderer.HottestLineHeightTypography));
        builder.Add("textRendererHottestLineHeightMs", FormatMilliseconds(textRenderer.HottestLineHeightMilliseconds));
    }

    private static string Escape(string value)
    {
        return value.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string FormatStopwatchTicks(long ticks)
    {
        return ((double)ticks * 1000d / Stopwatch.Frequency).ToString("0.###");
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }
}