namespace InkkSlinger;

public sealed class InkkOopsCheckBoxDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 40;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not CheckBox checkBox)
        {
            return;
        }

        var runtime = checkBox.GetCheckBoxSnapshotForDiagnostics();
        var telemetry = CheckBox.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("checkBoxContentType", runtime.ContentType);
        builder.Add("checkBoxDisplayText", Escape(runtime.DisplayText));
        builder.Add("checkBoxHasTemplateRoot", runtime.HasTemplateRoot);
        builder.Add("checkBoxIsEnabled", runtime.IsEnabled);
        builder.Add("checkBoxIsChecked", FormatNullableBoolean(runtime.IsChecked));
        builder.Add("checkBoxIsThreeState", runtime.IsThreeState);
        builder.Add("checkBoxLayoutSlot", $"{runtime.LayoutSlotWidth:0.##},{runtime.LayoutSlotHeight:0.##}");

        builder.Add("checkBoxRuntimeMeasureOverrideCalls", runtime.MeasureOverrideCallCount);
        builder.Add("checkBoxRuntimeMeasureOverrideMs", FormatMilliseconds(runtime.MeasureOverrideMilliseconds));
        builder.Add("checkBoxRuntimeTemplateRootMeasurePath", runtime.MeasureOverrideTemplateRootPathCount);
        builder.Add("checkBoxRuntimeSelfLayoutMeasurePath", runtime.MeasureOverrideSelfLayoutPathCount);
        builder.Add("checkBoxRuntimeRenderCalls", runtime.RenderCallCount);
        builder.Add("checkBoxRuntimeRenderMs", FormatMilliseconds(runtime.RenderMilliseconds));
        builder.Add("checkBoxRuntimeRenderSkippedTemplateRoot", runtime.RenderTemplateRootSkipCount);
        builder.Add("checkBoxRuntimeRenderEnabled", runtime.RenderEnabledStateCount);
        builder.Add("checkBoxRuntimeRenderDisabled", runtime.RenderDisabledStateCount);
        builder.Add("checkBoxRuntimeRenderChecked", runtime.RenderCheckedStateCount);
        builder.Add("checkBoxRuntimeRenderUnchecked", runtime.RenderUncheckedStateCount);
        builder.Add("checkBoxRuntimeRenderIndeterminate", runtime.RenderIndeterminateStateCount);
        builder.Add("checkBoxRuntimeGetFallbackStyleCalls", runtime.GetFallbackStyleCallCount);
        builder.Add("checkBoxRuntimeGetFallbackStyleMs", FormatMilliseconds(runtime.GetFallbackStyleMilliseconds));
        builder.Add("checkBoxRuntimeGetFallbackStyleCacheHits", runtime.GetFallbackStyleCacheHitCount);
        builder.Add("checkBoxRuntimeGetFallbackStyleCacheMisses", runtime.GetFallbackStyleCacheMissCount);
        builder.Add("checkBoxRuntimeGetGlyphSizeCalls", runtime.GetGlyphSizeCallCount);
        builder.Add("checkBoxRuntimeGetGlyphSpacingCalls", runtime.GetGlyphSpacingCallCount);
        builder.Add("checkBoxRuntimeMeasureTextCalls", runtime.MeasureTextCallCount);
        builder.Add("checkBoxRuntimeMeasureTextMs", FormatMilliseconds(runtime.MeasureTextMilliseconds));
        builder.Add("checkBoxRuntimeMeasureTextEmpty", runtime.MeasureTextEmptyTextCount);
        builder.Add("checkBoxRuntimeMeasureTextLayoutCalls", runtime.MeasureTextLayoutCallCount);
        builder.Add("checkBoxRuntimeDrawTextCalls", runtime.DrawTextCallCount);
        builder.Add("checkBoxRuntimeDrawTextMs", FormatMilliseconds(runtime.DrawTextMilliseconds));
        builder.Add("checkBoxRuntimeDrawTextEmpty", runtime.DrawTextEmptyTextCount);
        builder.Add("checkBoxRuntimeDrawTextNoSpace", runtime.DrawTextNoSpaceCount);
        builder.Add("checkBoxRuntimeDrawTextLayoutCalls", runtime.DrawTextLayoutCallCount);
        builder.Add("checkBoxRuntimeDrawTextLines", runtime.DrawTextLineDrawCount);
        builder.Add("checkBoxRuntimeDrawTextSkippedEmptyLines", runtime.DrawTextSkippedEmptyLineCount);

        builder.Add("checkBoxMeasureOverrideCalls", telemetry.MeasureOverrideCallCount);
        builder.Add("checkBoxMeasureOverrideMs", FormatMilliseconds(telemetry.MeasureOverrideMilliseconds));
        builder.Add("checkBoxTemplateRootMeasurePath", telemetry.MeasureOverrideTemplateRootPathCount);
        builder.Add("checkBoxSelfLayoutMeasurePath", telemetry.MeasureOverrideSelfLayoutPathCount);
        builder.Add("checkBoxRenderCalls", telemetry.RenderCallCount);
        builder.Add("checkBoxRenderMs", FormatMilliseconds(telemetry.RenderMilliseconds));
        builder.Add("checkBoxRenderSkippedTemplateRoot", telemetry.RenderTemplateRootSkipCount);
        builder.Add("checkBoxRenderEnabled", telemetry.RenderEnabledStateCount);
        builder.Add("checkBoxRenderDisabled", telemetry.RenderDisabledStateCount);
        builder.Add("checkBoxRenderChecked", telemetry.RenderCheckedStateCount);
        builder.Add("checkBoxRenderUnchecked", telemetry.RenderUncheckedStateCount);
        builder.Add("checkBoxRenderIndeterminate", telemetry.RenderIndeterminateStateCount);
        builder.Add("checkBoxGetFallbackStyleCalls", telemetry.GetFallbackStyleCallCount);
        builder.Add("checkBoxGetFallbackStyleMs", FormatMilliseconds(telemetry.GetFallbackStyleMilliseconds));
        builder.Add("checkBoxGetFallbackStyleCacheHits", telemetry.GetFallbackStyleCacheHitCount);
        builder.Add("checkBoxGetFallbackStyleCacheMisses", telemetry.GetFallbackStyleCacheMissCount);
        builder.Add("checkBoxGetGlyphSizeCalls", telemetry.GetGlyphSizeCallCount);
        builder.Add("checkBoxGetGlyphSpacingCalls", telemetry.GetGlyphSpacingCallCount);
        builder.Add("checkBoxMeasureTextCalls", telemetry.MeasureTextCallCount);
        builder.Add("checkBoxMeasureTextMs", FormatMilliseconds(telemetry.MeasureTextMilliseconds));
        builder.Add("checkBoxMeasureTextEmpty", telemetry.MeasureTextEmptyTextCount);
        builder.Add("checkBoxMeasureTextLayoutCalls", telemetry.MeasureTextLayoutCallCount);
        builder.Add("checkBoxDrawTextCalls", telemetry.DrawTextCallCount);
        builder.Add("checkBoxDrawTextMs", FormatMilliseconds(telemetry.DrawTextMilliseconds));
        builder.Add("checkBoxDrawTextEmpty", telemetry.DrawTextEmptyTextCount);
        builder.Add("checkBoxDrawTextNoSpace", telemetry.DrawTextNoSpaceCount);
        builder.Add("checkBoxDrawTextLayoutCalls", telemetry.DrawTextLayoutCallCount);
        builder.Add("checkBoxDrawTextLines", telemetry.DrawTextLineDrawCount);
        builder.Add("checkBoxDrawTextSkippedEmptyLines", telemetry.DrawTextSkippedEmptyLineCount);
        builder.Add("checkBoxBuildDefaultStyleCalls", telemetry.BuildDefaultCheckBoxStyleCallCount);
        builder.Add("checkBoxBuildDefaultStyleMs", FormatMilliseconds(telemetry.BuildDefaultCheckBoxStyleMilliseconds));
    }

    private static string Escape(string value)
    {
        return value.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }

    private static string FormatNullableBoolean(bool? value)
    {
        return value switch
        {
            true => "True",
            false => "False",
            null => "null"
        };
    }
}