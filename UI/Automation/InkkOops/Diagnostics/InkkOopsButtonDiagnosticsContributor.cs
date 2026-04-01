namespace InkkSlinger;

public sealed class InkkOopsButtonDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 40;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not Button button)
        {
            return;
        }

        var runtime = button.GetButtonSnapshotForDiagnostics();
        var telemetry = Button.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("buttonContentType", runtime.ContentType);
        builder.Add("buttonDisplayText", Escape(runtime.DisplayText));
        builder.Add("buttonHasTemplateRoot", runtime.HasTemplateRoot);
        builder.Add("buttonHasContentElement", runtime.HasContentElement);
        builder.Add("buttonHasTextLayoutCache", runtime.HasTextLayoutCache);
        builder.Add("buttonHasIntrinsicMeasureCache", runtime.HasIntrinsicNoWrapMeasureCache);
        builder.Add("buttonHasTextRenderPlanCache", runtime.HasTextRenderPlanCache);
        builder.Add("buttonIsMouseOver", runtime.IsMouseOver);
        builder.Add("buttonIsPressed", runtime.IsPressed);
        builder.Add("buttonContentVersion", runtime.ContentVersion);
        builder.Add("buttonLayoutSlot", $"{runtime.LayoutSlotWidth:0.##},{runtime.LayoutSlotHeight:0.##}");

        builder.Add("buttonRuntimeMeasureOverrideCalls", runtime.MeasureOverrideCallCount);
        builder.Add("buttonRuntimeMeasureOverrideMs", FormatMilliseconds(runtime.MeasureOverrideMilliseconds));
        builder.Add("buttonRuntimePlainTextFastPath", runtime.MeasureOverridePlainTextFastPathCount);
        builder.Add("buttonRuntimeBaseMeasurePath", runtime.MeasureOverrideBaseMeasurePathCount);
        builder.Add("buttonRuntimeChromeOnlyMeasure", runtime.MeasureOverrideChromeOnlyCount);
        builder.Add("buttonRuntimeCanReuseMeasureCalls", runtime.CanReuseMeasureCallCount);
        builder.Add("buttonRuntimeCanReuseMeasureAllowed", runtime.CanReuseMeasureAllowedCount);
        builder.Add("buttonRuntimeCanReuseMeasureRejected", runtime.CanReuseMeasureRejectedCount);
        builder.Add("buttonRuntimeResolveTextLayoutCalls", runtime.ResolveTextLayoutCallCount);
        builder.Add("buttonRuntimeResolveTextLayoutMs", FormatMilliseconds(runtime.ResolveTextLayoutMilliseconds));
        builder.Add("buttonRuntimeTextLayoutCacheHits", runtime.TextLayoutCacheHitCount);
        builder.Add("buttonRuntimeTextLayoutCacheMisses", runtime.TextLayoutCacheMissCount);
        builder.Add("buttonRuntimeTextLayoutInvalidations", runtime.TextLayoutInvalidationCount);
        builder.Add("buttonRuntimeTextRenderPlanCacheHits", runtime.TextRenderPlanCacheHitCount);
        builder.Add("buttonRuntimeTextRenderPlanCacheMisses", runtime.TextRenderPlanCacheMissCount);
        builder.Add("buttonRuntimeTextRenderPlanInvalidations", runtime.TextRenderPlanInvalidationCount);
        builder.Add("buttonRuntimeIntrinsicMeasureCacheHits", runtime.IntrinsicNoWrapMeasureCacheHitCount);
        builder.Add("buttonRuntimeIntrinsicMeasureCacheMisses", runtime.IntrinsicNoWrapMeasureCacheMissCount);
        builder.Add("buttonRuntimeRenderTextPreparationCalls", runtime.RenderTextPreparationCallCount);
        builder.Add("buttonRuntimeRenderTextPreparationMs", FormatMilliseconds(runtime.RenderTextPreparationMilliseconds));
        builder.Add("buttonRuntimePreparedLines", runtime.TextRenderPlanPreparedLineCount);
        builder.Add("buttonRuntimeSkippedEmptyLines", runtime.TextRenderPlanSkippedEmptyLineCount);
        builder.Add("buttonRuntimeTextDrawDispatchCalls", runtime.RenderTextDrawDispatchCallCount);
        builder.Add("buttonRuntimeRenderChromeCalls", runtime.RenderChromeCallCount);
        builder.Add("buttonRuntimeRenderChromeMs", FormatMilliseconds(runtime.RenderChromeMilliseconds));
        builder.Add("buttonRuntimeRenderChromeSkippedBorder", runtime.RenderChromeSkippedBorderCount);
        builder.Add("buttonRuntimeRenderChromeDrewBorder", runtime.RenderChromeDrewBorderCount);
        builder.Add("buttonRuntimeRenderCalls", runtime.RenderCallCount);
        builder.Add("buttonRuntimeRenderMs", FormatMilliseconds(runtime.RenderMilliseconds));
        builder.Add("buttonRuntimeRenderSkippedTemplateRoot", runtime.RenderSkippedTemplateRootCount);
        builder.Add("buttonRuntimeRenderPreparedTextPlan", runtime.RenderPreparedTextPlanCount);
        builder.Add("buttonRuntimeRenderSkippedNoTextPlan", runtime.RenderSkippedNoTextPlanCount);
        builder.Add("buttonRuntimeOnClickCalls", runtime.OnClickCallCount);
        builder.Add("buttonRuntimeOnClickMs", FormatMilliseconds(runtime.OnClickMilliseconds));
        builder.Add("buttonRuntimeAutomationNotify", runtime.OnClickAutomationNotifyCount);
        builder.Add("buttonRuntimeAutomationSkip", runtime.OnClickAutomationSkipCount);
        builder.Add("buttonRuntimeExecuteCommand", runtime.OnClickExecuteCommandCount);
        builder.Add("buttonRuntimeRaiseClickEvent", runtime.RaiseClickEventCallCount);
        builder.Add("buttonRuntimeSetMouseOverCalls", runtime.SetMouseOverFromInputCallCount);
        builder.Add("buttonRuntimeSetMouseOverNoOp", runtime.SetMouseOverFromInputNoOpCount);
        builder.Add("buttonRuntimeSetMouseOverChanged", runtime.SetMouseOverFromInputChangedCount);
        builder.Add("buttonRuntimeSetPressedCalls", runtime.SetPressedFromInputCallCount);
        builder.Add("buttonRuntimeSetPressedNoOp", runtime.SetPressedFromInputNoOpCount);
        builder.Add("buttonRuntimeSetPressedChanged", runtime.SetPressedFromInputChangedCount);
        builder.Add("buttonRuntimeInvokeFromInput", runtime.InvokeFromInputCallCount);
        builder.Add("buttonRuntimeUniformGridCalls", runtime.HasAvailableIndependentDesiredSizeForUniformGridCallCount);
        builder.Add("buttonRuntimeUniformGridTrue", runtime.HasAvailableIndependentDesiredSizeForUniformGridTrueCount);
        builder.Add("buttonRuntimeUniformGridFalse", runtime.HasAvailableIndependentDesiredSizeForUniformGridFalseCount);

        builder.Add("buttonMeasureOverrideCalls", telemetry.MeasureOverrideCallCount);
        builder.Add("buttonMeasureOverrideMs", FormatMilliseconds(telemetry.MeasureOverrideMilliseconds));
        builder.Add("buttonPlainTextFastPath", telemetry.MeasureOverridePlainTextFastPathCount);
        builder.Add("buttonBaseMeasurePath", telemetry.MeasureOverrideBaseMeasurePathCount);
        builder.Add("buttonChromeOnlyMeasure", telemetry.MeasureOverrideChromeOnlyCount);
        builder.Add("buttonResolveTextLayoutCalls", telemetry.ResolveTextLayoutCallCount);
        builder.Add("buttonResolveTextLayoutMs", FormatMilliseconds(telemetry.ResolveTextLayoutMilliseconds));
        builder.Add("buttonTextLayoutCacheHits", telemetry.TextLayoutCacheHitCount);
        builder.Add("buttonTextLayoutCacheMisses", telemetry.TextLayoutCacheMissCount);
        builder.Add("buttonTextLayoutInvalidations", telemetry.TextLayoutInvalidationCount);
        builder.Add("buttonTextRenderPlanCacheHits", telemetry.TextRenderPlanCacheHitCount);
        builder.Add("buttonTextRenderPlanCacheMisses", telemetry.TextRenderPlanCacheMissCount);
        builder.Add("buttonTextRenderPlanInvalidations", telemetry.TextRenderPlanInvalidationCount);
        builder.Add("buttonIntrinsicMeasureCacheHits", telemetry.IntrinsicNoWrapMeasureCacheHitCount);
        builder.Add("buttonIntrinsicMeasureCacheMisses", telemetry.IntrinsicNoWrapMeasureCacheMissCount);
        builder.Add("buttonRenderTextPreparationCalls", telemetry.RenderTextPreparationCallCount);
        builder.Add("buttonRenderTextPreparationMs", FormatMilliseconds(telemetry.RenderTextPreparationMilliseconds));
        builder.Add("buttonPreparedLines", telemetry.TextRenderPlanPreparedLineCount);
        builder.Add("buttonTextDrawDispatchCalls", telemetry.RenderTextDrawDispatchCallCount);
        builder.Add("buttonRenderChromeCalls", telemetry.RenderChromeCallCount);
        builder.Add("buttonRenderChromeMs", FormatMilliseconds(telemetry.RenderChromeMilliseconds));
        builder.Add("buttonRenderCalls", telemetry.RenderCallCount);
        builder.Add("buttonRenderMs", FormatMilliseconds(telemetry.RenderMilliseconds));
        builder.Add("buttonOnClickCalls", telemetry.OnClickCallCount);
        builder.Add("buttonOnClickMs", FormatMilliseconds(telemetry.OnClickMilliseconds));
        builder.Add("buttonDependencyPropertyChangedCalls", telemetry.DependencyPropertyChangedCallCount);
        builder.Add("buttonContentPropertyChanged", telemetry.ContentPropertyChangedCount);
        builder.Add("buttonTextMetricPropertyChanged", telemetry.TextMetricPropertyChangedCount);
        builder.Add("buttonGetFallbackStyleCalls", telemetry.GetFallbackStyleCallCount);
        builder.Add("buttonGetFallbackStyleMs", FormatMilliseconds(telemetry.GetFallbackStyleMilliseconds));
        builder.Add("buttonGetFallbackStyleCacheHits", telemetry.GetFallbackStyleCacheHitCount);
        builder.Add("buttonGetFallbackStyleCacheMisses", telemetry.GetFallbackStyleCacheMissCount);
        builder.Add("buttonSetMouseOverChanged", telemetry.SetMouseOverFromInputChangedCount);
        builder.Add("buttonSetPressedChanged", telemetry.SetPressedFromInputChangedCount);
        builder.Add("buttonInvokeFromInput", telemetry.InvokeFromInputCallCount);
    }

    private static string Escape(string value)
    {
        return value.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }
}