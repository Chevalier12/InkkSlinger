namespace InkkSlinger;

public sealed class InkkOopsTextBlockDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 30;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not TextBlock textBlock)
        {
            return;
        }

        var runtime = textBlock.GetTextBlockSnapshotForDiagnostics();
        var telemetry = TextBlock.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("text", Escape(textBlock.Text));
        var typography = UiTextRenderer.ResolveTypography(textBlock, textBlock.FontSize);
        var lineHeight = UiTextRenderer.GetLineHeight(typography);
        var inkBounds = string.IsNullOrWhiteSpace(textBlock.LastRenderedLayoutTextForTests)
            ? new LayoutRect(0f, 0f, 0f, 0f)
            : UiTextRenderer.GetInkBoundsForTests(typography, textBlock.LastRenderedLayoutTextForTests);
        builder.Add("renderLines", textBlock.LastRenderedLineCountForTests);
        builder.Add("renderWidth", $"{textBlock.LastRenderedLayoutWidthForTests:0.##}");
        builder.Add("desired", $"{textBlock.DesiredSize.X:0.##},{textBlock.DesiredSize.Y:0.##}");
        builder.Add("previousAvailable", $"{textBlock.PreviousAvailableSizeForTests.X:0.##},{textBlock.PreviousAvailableSizeForTests.Y:0.##}");
        builder.Add("measureCalls", textBlock.MeasureCallCount);
        builder.Add("measureWork", textBlock.MeasureWorkCount);
        builder.Add("arrangeCalls", textBlock.ArrangeCallCount);
        builder.Add("arrangeWork", textBlock.ArrangeWorkCount);
        builder.Add("measureValid", textBlock.IsMeasureValidForTests);
        builder.Add("arrangeValid", textBlock.IsArrangeValidForTests);
        builder.Add("lineHeight", $"{lineHeight:0.###}");
        builder.Add("inkBounds", $"{inkBounds.X:0.##},{inkBounds.Y:0.##},{inkBounds.Width:0.##},{inkBounds.Height:0.##}");
        builder.Add("hasLayoutCache", runtime.HasLayoutCache);
        builder.Add("hasSecondaryLayoutCache", runtime.HasSecondaryLayoutCache);
        builder.Add("hasIntrinsicMeasureCache", runtime.HasIntrinsicNoWrapMeasureCache);
        builder.Add("textVersion", runtime.TextVersion);
        builder.Add("layoutCacheWidth", $"{runtime.LayoutCacheWidth:0.##}");
        builder.Add("secondaryLayoutCacheWidth", $"{runtime.SecondaryLayoutCacheWidth:0.##}");
        builder.Add("runtimeMeasureOverrideCalls", runtime.MeasureOverrideCallCount);
        builder.Add("runtimeMeasureOverrideMs", FormatMilliseconds(runtime.MeasureOverrideMilliseconds));
        builder.Add("runtimeEmptyMeasureCalls", runtime.EmptyMeasureCallCount);
        builder.Add("runtimeSameTextSameWidthMeasureCalls", runtime.SameTextSameWidthMeasureCallCount);
        builder.Add("runtimeIntrinsicMeasurePathCalls", runtime.IntrinsicMeasurePathCallCount);
        builder.Add("runtimeLayoutMeasurePathCalls", runtime.LayoutMeasurePathCallCount);
        builder.Add("runtimeCanReuseMeasureCalls", runtime.CanReuseMeasureCallCount);
        builder.Add("runtimeCanReuseMeasureTrue", runtime.CanReuseMeasureTrueCount);
        builder.Add("runtimeCanReuseMeasureEmptyText", runtime.CanReuseMeasureEmptyTextCount);
        builder.Add("runtimeCanReuseMeasureNoWrap", runtime.CanReuseMeasureNoWrapCount);
        builder.Add("runtimeCanReuseMeasureMultilineRejects", runtime.CanReuseMeasureMultilineRejectCount);
        builder.Add("runtimeCanReuseMeasureIntrinsicFit", runtime.CanReuseMeasureIntrinsicFitCount);
        builder.Add("runtimeCanReuseMeasureTooNarrowRejects", runtime.CanReuseMeasureTooNarrowRejectCount);
        builder.Add("runtimeShouldInvalidateMeasureCalls", runtime.ShouldInvalidateMeasureCallCount);
        builder.Add("runtimeShouldInvalidateMeasureTextPropertyCalls", runtime.ShouldInvalidateMeasureTextPropertyCallCount);
        builder.Add("runtimeShouldInvalidateMeasureBaseFallbacks", runtime.ShouldInvalidateMeasureBaseFallbackCount);
        builder.Add("runtimeShouldInvalidateMeasureReusedDesired", runtime.ShouldInvalidateMeasureReusedDesiredSizeCount);
        builder.Add("runtimeShouldInvalidateMeasureChangedDesired", runtime.ShouldInvalidateMeasureChangedDesiredSizeCount);
        builder.Add("runtimeTryMeasureDesiredSizeCalls", runtime.TryMeasureDesiredSizeForTextChangeCallCount);
        builder.Add("runtimeTryMeasureDesiredSizeSuccess", runtime.TryMeasureDesiredSizeForTextChangeSuccessCount);
        builder.Add("runtimeTryMeasureDesiredSizeNoPreviousAvailable", runtime.TryMeasureDesiredSizeForTextChangeNoPreviousAvailableCount);
        builder.Add("runtimeTryMeasureDesiredSizeEmptyText", runtime.TryMeasureDesiredSizeForTextChangeEmptyTextCount);
        builder.Add("runtimeTryMeasureDesiredSizeIntrinsicPath", runtime.TryMeasureDesiredSizeForTextChangeIntrinsicPathCount);
        builder.Add("runtimeTryMeasureDesiredSizeLayoutPath", runtime.TryMeasureDesiredSizeForTextChangeLayoutPathCount);
        builder.Add("runtimeTryMeasureDesiredSizeLayoutRounding", runtime.TryMeasureDesiredSizeForTextChangeLayoutRoundingCount);
        builder.Add("runtimeUniformGridCalls", runtime.HasAvailableIndependentDesiredSizeForUniformGridCallCount);
        builder.Add("runtimeUniformGridTrue", runtime.HasAvailableIndependentDesiredSizeForUniformGridTrueCount);
        builder.Add("runtimeUniformGridFalse", runtime.HasAvailableIndependentDesiredSizeForUniformGridFalseCount);
        builder.Add("runtimeCanUseIntrinsicNoWrapCalls", runtime.CanUseIntrinsicNoWrapMeasureCallCount);
        builder.Add("runtimeCanUseIntrinsicNoWrapAllowed", runtime.CanUseIntrinsicNoWrapMeasureAllowedCount);
        builder.Add("runtimeCanUseIntrinsicNoWrapRejectedWrap", runtime.CanUseIntrinsicNoWrapMeasureRejectedWrapCount);
        builder.Add("runtimeCanUseIntrinsicNoWrapRejectedEmpty", runtime.CanUseIntrinsicNoWrapMeasureRejectedEmptyCount);
        builder.Add("runtimeCanUseIntrinsicNoWrapRejectedMultiline", runtime.CanUseIntrinsicNoWrapMeasureRejectedMultilineCount);
        builder.Add("runtimeCanUseIntrinsicMeasureCalls", runtime.CanUseIntrinsicMeasureCallCount);
        builder.Add("runtimeCanUseIntrinsicMeasureAllowedNoWrap", runtime.CanUseIntrinsicMeasureAllowedNoWrapCount);
        builder.Add("runtimeCanUseIntrinsicMeasureAllowedWrappedWidth", runtime.CanUseIntrinsicMeasureAllowedWrappedWidthCount);
        builder.Add("runtimeCanUseIntrinsicMeasureRejectedNoWrapUnavailable", runtime.CanUseIntrinsicMeasureRejectedNoWrapUnavailableCount);
        builder.Add("runtimeCanUseIntrinsicMeasureRejectedEmpty", runtime.CanUseIntrinsicMeasureRejectedEmptyTextCount);
        builder.Add("runtimeCanUseIntrinsicMeasureRejectedMultiline", runtime.CanUseIntrinsicMeasureRejectedMultilineTextCount);
        builder.Add("runtimeCanUseIntrinsicMeasureRejectedWidthTooNarrow", runtime.CanUseIntrinsicMeasureRejectedWidthTooNarrowCount);
        builder.Add("runtimeResolveIntrinsicMeasureCalls", runtime.ResolveIntrinsicNoWrapTextSizeCallCount);
        builder.Add("runtimeResolveIntrinsicMeasureMs", FormatMilliseconds(runtime.ResolveIntrinsicNoWrapTextSizeMilliseconds));
        builder.Add("runtimeIntrinsicMeasureCacheHits", runtime.IntrinsicMeasureCacheHitCount);
        builder.Add("runtimeIntrinsicMeasureCacheMisses", runtime.IntrinsicMeasureCacheMissCount);
        builder.Add("runtimeResolveLayoutCalls", runtime.ResolveLayoutCallCount);
        builder.Add("runtimeResolveLayoutMs", FormatMilliseconds(runtime.ResolveLayoutMilliseconds));
        builder.Add("runtimeResolveLayoutEmptyText", runtime.ResolveLayoutEmptyTextCount);
        builder.Add("runtimeResolveLayoutCacheHits", runtime.ResolveLayoutCacheHitCount);
        builder.Add("runtimeResolveLayoutPrimaryCacheHits", runtime.ResolveLayoutPrimaryCacheHitCount);
        builder.Add("runtimeResolveLayoutSecondaryCacheHits", runtime.ResolveLayoutSecondaryCacheHitCount);
        builder.Add("runtimeResolveLayoutCacheMisses", runtime.ResolveLayoutCacheMissCount);
        builder.Add("runtimeResolveLayoutSameTextSameWidthCalls", runtime.ResolveLayoutSameTextSameWidthCallCount);
        builder.Add("runtimeResolveLayoutUncachedCalls", runtime.ResolveLayoutUncachedCallCount);
        builder.Add("runtimeResolveLayoutUncachedMs", FormatMilliseconds(runtime.ResolveLayoutUncachedMilliseconds));
        builder.Add("runtimeTextPropertyChanges", runtime.TextPropertyChangeCount);
        builder.Add("runtimeLayoutAffectingPropertyChanges", runtime.LayoutAffectingPropertyChangeCount);
        builder.Add("runtimeOtherPropertyChanges", runtime.OtherPropertyChangeCount);
        builder.Add("runtimeLayoutCacheInvalidations", runtime.LayoutCacheInvalidationCount);
        builder.Add("runtimeLayoutCacheInvalidationNoOps", runtime.LayoutCacheInvalidationNoOpCount);
        builder.Add("runtimeIntrinsicMeasureInvalidations", runtime.IntrinsicMeasureInvalidationCount);
        builder.Add("runtimeIntrinsicMeasureInvalidationNoOps", runtime.IntrinsicMeasureInvalidationNoOpCount);
        builder.Add("runtimeRenderCalls", runtime.RenderCallCount);
        builder.Add("runtimeRenderMs", FormatMilliseconds(runtime.RenderMilliseconds));
        builder.Add("runtimeRenderEmptyTextSkips", runtime.RenderEmptyTextSkipCount);
        builder.Add("runtimeRenderLineIterations", runtime.RenderLineIterationCount);
        builder.Add("runtimeRenderEmptyLineSkips", runtime.RenderEmptyLineSkipCount);
        builder.Add("runtimeRenderAxisAlignedClipFastPath", runtime.RenderAxisAlignedClipFastPathCount);
        builder.Add("runtimeRenderTransformedClipPath", runtime.RenderTransformedClipPathCount);
        builder.Add("runtimeRenderClipSkips", runtime.RenderClipSkipCount);
        builder.Add("runtimeRenderClipBreaks", runtime.RenderClipBreakCount);
        builder.Add("runtimeRenderDrawLineCalls", runtime.RenderDrawLineCount);
        builder.Add("runtimeRenderTextDecorationsCalls", runtime.RenderTextDecorationsCallCount);

        builder.Add("telemetryMeasureOverrideCalls", telemetry.MeasureOverrideCallCount);
        builder.Add("telemetryMeasureOverrideMs", FormatMilliseconds(telemetry.MeasureOverrideMilliseconds));
        builder.Add("telemetryEmptyMeasureCalls", telemetry.EmptyMeasureCallCount);
        builder.Add("telemetryIntrinsicMeasurePathCalls", telemetry.IntrinsicMeasurePathCallCount);
        builder.Add("telemetryLayoutMeasurePathCalls", telemetry.LayoutMeasurePathCallCount);
        builder.Add("telemetryCanReuseMeasureCalls", telemetry.CanReuseMeasureCallCount);
        builder.Add("telemetryShouldInvalidateMeasureCalls", telemetry.ShouldInvalidateMeasureCallCount);
        builder.Add("telemetryTryMeasureDesiredSizeCalls", telemetry.TryMeasureDesiredSizeForTextChangeCallCount);
        builder.Add("telemetryResolveIntrinsicMeasureCalls", telemetry.ResolveIntrinsicNoWrapTextSizeCallCount);
        builder.Add("telemetryResolveIntrinsicMeasureMs", FormatMilliseconds(telemetry.ResolveIntrinsicNoWrapTextSizeMilliseconds));
        builder.Add("telemetryIntrinsicMeasureCacheHits", telemetry.IntrinsicMeasureCacheHitCount);
        builder.Add("telemetryIntrinsicMeasureCacheMisses", telemetry.IntrinsicMeasureCacheMissCount);
        builder.Add("telemetryResolveLayoutCalls", telemetry.ResolveLayoutCallCount);
        builder.Add("telemetryResolveLayoutMs", FormatMilliseconds(telemetry.ResolveLayoutMilliseconds));
        builder.Add("telemetryResolveLayoutCacheHits", telemetry.ResolveLayoutCacheHitCount);
        builder.Add("telemetryResolveLayoutPrimaryCacheHits", telemetry.ResolveLayoutPrimaryCacheHitCount);
        builder.Add("telemetryResolveLayoutSecondaryCacheHits", telemetry.ResolveLayoutSecondaryCacheHitCount);
        builder.Add("telemetryResolveLayoutCacheMisses", telemetry.ResolveLayoutCacheMissCount);
        builder.Add("telemetryResolveLayoutUncachedCalls", telemetry.ResolveLayoutUncachedCallCount);
        builder.Add("telemetryResolveLayoutUncachedMs", FormatMilliseconds(telemetry.ResolveLayoutUncachedMilliseconds));
        builder.Add("telemetryTextPropertyChanges", telemetry.TextPropertyChangeCount);
        builder.Add("telemetryLayoutAffectingPropertyChanges", telemetry.LayoutAffectingPropertyChangeCount);
        builder.Add("telemetryOtherPropertyChanges", telemetry.OtherPropertyChangeCount);
        builder.Add("telemetryLayoutCacheInvalidations", telemetry.LayoutCacheInvalidationCount);
        builder.Add("telemetryLayoutCacheInvalidationNoOps", telemetry.LayoutCacheInvalidationNoOpCount);
        builder.Add("telemetryIntrinsicMeasureInvalidations", telemetry.IntrinsicMeasureInvalidationCount);
        builder.Add("telemetryIntrinsicMeasureInvalidationNoOps", telemetry.IntrinsicMeasureInvalidationNoOpCount);
        builder.Add("telemetryRenderCalls", telemetry.RenderCallCount);
        builder.Add("telemetryRenderMs", FormatMilliseconds(telemetry.RenderMilliseconds));
        builder.Add("telemetryRenderClipSkips", telemetry.RenderClipSkipCount);
        builder.Add("telemetryRenderClipBreaks", telemetry.RenderClipBreakCount);
        builder.Add("telemetryRenderDrawLineCalls", telemetry.RenderDrawLineCount);
        builder.Add("telemetryRenderTextDecorationsCalls", telemetry.RenderTextDecorationsCallCount);
        if (!string.IsNullOrWhiteSpace(textBlock.LastRenderedLayoutTextForTests))
        {
            builder.Add("renderText", Escape(textBlock.LastRenderedLayoutTextForTests));
        }
    }

    private static string Escape(string text)
    {
        return text.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }
}
