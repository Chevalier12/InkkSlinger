namespace InkkSlinger;

public sealed class InkkOopsExpanderDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 40;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not Expander expander)
        {
            return;
        }

        builder.Add("type", expander.GetType().Name);
        builder.Add("slot", FormatRect(expander.LayoutSlot));
        builder.Add("desired", $"{expander.DesiredSize.X:0.##},{expander.DesiredSize.Y:0.##}");
        builder.Add("actual", $"{expander.ActualWidth:0.##},{expander.ActualHeight:0.##}");
        builder.Add("renderSize", $"{expander.RenderSize.X:0.##},{expander.RenderSize.Y:0.##}");
        builder.Add("previousAvailable", $"{expander.PreviousAvailableSizeForTests.X:0.##},{expander.PreviousAvailableSizeForTests.Y:0.##}");
        builder.Add("measureCalls", expander.MeasureCallCount);
        builder.Add("measureWork", expander.MeasureWorkCount);
        builder.Add("arrangeCalls", expander.ArrangeCallCount);
        builder.Add("arrangeWork", expander.ArrangeWorkCount);
        builder.Add("measureMs", FormatMilliseconds(expander.MeasureElapsedTicksForTests));
        builder.Add("measureExclusiveMs", FormatMilliseconds(expander.MeasureExclusiveElapsedTicksForTests));
        builder.Add("arrangeMs", FormatMilliseconds(expander.ArrangeElapsedTicksForTests));
        builder.Add("measureValid", expander.IsMeasureValidForTests);
        builder.Add("arrangeValid", expander.IsArrangeValidForTests);
        builder.Add("visible", expander.Visibility == Visibility.Visible);
        builder.Add("enabled", expander.IsEnabled);
        builder.Add("loaded", expander.IsLoaded);
        builder.Add("isExpanded", expander.IsExpanded);
        builder.Add("expandDirection", expander.ExpandDirection);
        builder.Add("header", Escape(expander.Header?.ToString() ?? string.Empty));

        var runtime = expander.GetExpanderSnapshotForDiagnostics();
        builder.Add("headerPressed", runtime.IsHeaderPressed);
        builder.Add("hasHeaderElement", runtime.HasHeaderElement);
        builder.Add("hasContent", runtime.HasContentElement);
        builder.Add("measuredHeaderSize", $"{runtime.LastMeasuredHeaderWidth:0.##},{runtime.LastMeasuredHeaderHeight:0.##}");
        builder.Add("headerRect", $"{runtime.HeaderRectX:0.##},{runtime.HeaderRectY:0.##},{runtime.HeaderRectWidth:0.##},{runtime.HeaderRectHeight:0.##}");
        builder.Add("contentRect", $"{runtime.ContentRectX:0.##},{runtime.ContentRectY:0.##},{runtime.ContentRectWidth:0.##},{runtime.ContentRectHeight:0.##}");
        builder.Add("runtimeMeasureOverrideCalls", runtime.MeasureOverrideCallCount);
        builder.Add("runtimeMeasureOverrideMs", $"{runtime.MeasureOverrideMilliseconds:0.###}");
        builder.Add("runtimeHeaderMeasureCount", runtime.HeaderMeasureCount);
        builder.Add("runtimeHeaderMeasureMs", $"{runtime.HeaderMeasureMilliseconds:0.###}");
        builder.Add("runtimeHeaderMeasureElementPath", runtime.HeaderMeasureElementPathCount);
        builder.Add("runtimeHeaderMeasureTextPath", runtime.HeaderMeasureTextPathCount);
        builder.Add("runtimeHeaderMeasureEmptyText", runtime.HeaderMeasureEmptyTextCount);
        builder.Add("runtimeContentMeasuredWhenExpanded", runtime.ContentMeasuredWhenExpandedCount);
        builder.Add("runtimeContentMeasuredWhenCollapsed", runtime.ContentMeasuredWhenCollapsedCount);
        builder.Add("runtimeContentMeasureSkippedWithoutContent", runtime.ContentMeasureSkippedWithoutContentCount);
        builder.Add("runtimeArrangeOverrideCalls", runtime.ArrangeOverrideCallCount);
        builder.Add("runtimeArrangeOverrideMs", $"{runtime.ArrangeOverrideMilliseconds:0.###}");
        builder.Add("runtimeArrangeHeaderMeasureCacheHits", runtime.ArrangeHeaderMeasureCacheHitCount);
        builder.Add("runtimeArrangeHeaderMeasureCacheMisses", runtime.ArrangeHeaderMeasureCacheMissCount);
        builder.Add("runtimeArrangeExpandedContent", runtime.ArrangeExpandedContentCount);
        builder.Add("runtimeArrangeCollapsedContent", runtime.ArrangeCollapsedContentCount);
        builder.Add("runtimeArrangeNoContent", runtime.ArrangeNoContentCount);
        builder.Add("runtimeDirectionDown", runtime.ExpandDirectionDownCount);
        builder.Add("runtimeDirectionUp", runtime.ExpandDirectionUpCount);
        builder.Add("runtimeDirectionLeft", runtime.ExpandDirectionLeftCount);
        builder.Add("runtimeDirectionRight", runtime.ExpandDirectionRightCount);
        builder.Add("runtimeRenderCalls", runtime.RenderCallCount);
        builder.Add("runtimeRenderMs", $"{runtime.RenderMilliseconds:0.###}");
        builder.Add("runtimeHeaderBackgroundStyleOverride", runtime.HeaderBackgroundStyleOverrideCount);
        builder.Add("runtimeHeaderBackgroundInherited", runtime.HeaderBackgroundInheritedCount);
        builder.Add("runtimeRenderHeaderText", runtime.RenderHeaderTextCount);
        builder.Add("runtimeRenderHeaderTextSkippedEmpty", runtime.RenderHeaderTextSkippedEmptyCount);
        builder.Add("runtimeRenderHeaderElement", runtime.RenderHeaderElementCount);
        builder.Add("runtimeExpandCount", runtime.ExpandCount);
        builder.Add("runtimeCollapseCount", runtime.CollapseCount);
        builder.Add("runtimeHeaderPointerDown", runtime.HeaderPointerDownCount);
        builder.Add("runtimeHeaderPointerDownMiss", runtime.HeaderPointerDownMissCount);
        builder.Add("runtimeHeaderPointerUpToggle", runtime.HeaderPointerUpToggleCount);
        builder.Add("runtimeHeaderPointerUpMiss", runtime.HeaderPointerUpMissCount);
        builder.Add("runtimeHeaderPointerUpReleaseOutside", runtime.HeaderPointerUpReleaseOutsideCount);
        builder.Add("runtimeHeaderUpdateCount", runtime.HeaderUpdateCount);
        builder.Add("runtimeHeaderUpdateAttachElement", runtime.HeaderUpdateAttachElementCount);
        builder.Add("runtimeHeaderUpdateDetachElement", runtime.HeaderUpdateDetachElementCount);
        builder.Add("runtimeHeaderUpdateTextHeader", runtime.HeaderUpdateTextHeaderCount);

        var telemetry = Expander.GetAggregateTelemetrySnapshotForDiagnostics();
        builder.Add("measureOverrideCalls", telemetry.MeasureOverrideCallCount);
        builder.Add("measureOverrideMs", $"{telemetry.MeasureOverrideMilliseconds:0.###}");
        builder.Add("headerMeasureCount", telemetry.HeaderMeasureCount);
        builder.Add("headerMeasureMs", $"{telemetry.HeaderMeasureMilliseconds:0.###}");
        builder.Add("headerMeasureElementPath", telemetry.HeaderMeasureElementPathCount);
        builder.Add("headerMeasureTextPath", telemetry.HeaderMeasureTextPathCount);
        builder.Add("headerMeasureEmptyText", telemetry.HeaderMeasureEmptyTextCount);
        builder.Add("contentMeasuredWhenExpanded", telemetry.ContentMeasuredWhenExpandedCount);
        builder.Add("contentMeasuredWhenCollapsed", telemetry.ContentMeasuredWhenCollapsedCount);
        builder.Add("contentMeasureSkippedWithoutContent", telemetry.ContentMeasureSkippedWithoutContentCount);
        builder.Add("arrangeOverrideCalls", telemetry.ArrangeOverrideCallCount);
        builder.Add("arrangeOverrideMs", $"{telemetry.ArrangeOverrideMilliseconds:0.###}");
        builder.Add("arrangeHeaderMeasureCacheHits", telemetry.ArrangeHeaderMeasureCacheHitCount);
        builder.Add("arrangeHeaderMeasureCacheMisses", telemetry.ArrangeHeaderMeasureCacheMissCount);
        builder.Add("arrangeExpandedContent", telemetry.ArrangeExpandedContentCount);
        builder.Add("arrangeCollapsedContent", telemetry.ArrangeCollapsedContentCount);
        builder.Add("arrangeNoContent", telemetry.ArrangeNoContentCount);
        builder.Add("expandDirectionDown", telemetry.ExpandDirectionDownCount);
        builder.Add("expandDirectionUp", telemetry.ExpandDirectionUpCount);
        builder.Add("expandDirectionLeft", telemetry.ExpandDirectionLeftCount);
        builder.Add("expandDirectionRight", telemetry.ExpandDirectionRightCount);
        builder.Add("renderCalls", telemetry.RenderCallCount);
        builder.Add("renderMs", $"{telemetry.RenderMilliseconds:0.###}");
        builder.Add("headerBackgroundStyleOverride", telemetry.HeaderBackgroundStyleOverrideCount);
        builder.Add("headerBackgroundInherited", telemetry.HeaderBackgroundInheritedCount);
        builder.Add("renderHeaderText", telemetry.RenderHeaderTextCount);
        builder.Add("renderHeaderTextSkippedEmpty", telemetry.RenderHeaderTextSkippedEmptyCount);
        builder.Add("renderHeaderElement", telemetry.RenderHeaderElementCount);
        builder.Add("expandCount", telemetry.ExpandCount);
        builder.Add("collapseCount", telemetry.CollapseCount);
        builder.Add("headerPointerDownCount", telemetry.HeaderPointerDownCount);
        builder.Add("headerPointerDownMissCount", telemetry.HeaderPointerDownMissCount);
        builder.Add("headerPointerUpToggleCount", telemetry.HeaderPointerUpToggleCount);
        builder.Add("headerPointerUpMissCount", telemetry.HeaderPointerUpMissCount);
        builder.Add("headerPointerUpReleaseOutsideCount", telemetry.HeaderPointerUpReleaseOutsideCount);
        builder.Add("headerUpdateCount", telemetry.HeaderUpdateCount);
    }

    private static string Escape(string text)
    {
        return text.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string FormatMilliseconds(long elapsedTicks)
    {
        return $"{(double)elapsedTicks * 1000d / System.Diagnostics.Stopwatch.Frequency:0.###}";
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}";
    }
}