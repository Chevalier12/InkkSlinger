namespace InkkSlinger;

public sealed class InkkOopsTrackDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 50;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        _ = context;

        if (element is not Track target)
        {
            return;
        }

        var runtime = target.GetTrackSnapshotForDiagnostics();
        var telemetry = Track.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("trackOrientation", runtime.Orientation);
        builder.Add("trackIsViewportSizedThumb", runtime.IsViewportSizedThumb);
        builder.Add("trackIsDirectionReversed", runtime.IsDirectionReversed);
        builder.Add("trackMinimum", $"{runtime.Minimum:0.###}");
        builder.Add("trackMaximum", $"{runtime.Maximum:0.###}");
        builder.Add("trackValue", $"{runtime.Value:0.###}");
        builder.Add("trackViewportSize", $"{runtime.ViewportSize:0.###}");
        builder.Add("trackThumbLength", $"{runtime.ThumbLength:0.###}");
        builder.Add("trackThumbMinLength", $"{runtime.ThumbMinLength:0.###}");
        builder.Add("trackTrackThickness", $"{runtime.TrackThickness:0.###}");
        builder.Add("trackChildrenCount", runtime.ChildrenCount);
        builder.Add("trackHasPendingRenderDirtyBoundsHint", runtime.HasPendingRenderDirtyBoundsHint);
        builder.Add("trackPreserveRenderDirtyBoundsHint", runtime.PreserveRenderDirtyBoundsHint);
        builder.Add("trackTrackRect", FormatRect(runtime.TrackRectX, runtime.TrackRectY, runtime.TrackRectWidth, runtime.TrackRectHeight));
        builder.Add("trackThumbRect", FormatRect(runtime.ThumbRectX, runtime.ThumbRectY, runtime.ThumbRectWidth, runtime.ThumbRectHeight));
        builder.Add("trackDecreaseRegionRect", FormatRect(runtime.DecreaseRegionRectX, runtime.DecreaseRegionRectY, runtime.DecreaseRegionRectWidth, runtime.DecreaseRegionRectHeight));
        builder.Add("trackIncreaseRegionRect", FormatRect(runtime.IncreaseRegionRectX, runtime.IncreaseRegionRectY, runtime.IncreaseRegionRectWidth, runtime.IncreaseRegionRectHeight));

        builder.Add("trackRuntimeGetThumbRectCalls", runtime.GetThumbRectCallCount);
        builder.Add("trackRuntimeGetTrackRectCalls", runtime.GetTrackRectCallCount);
        builder.Add("trackRuntimeGetThumbTravelCalls", runtime.GetThumbTravelCallCount);
        builder.Add("trackRuntimeGetValueFromThumbTravelCalls", runtime.GetValueFromThumbTravelCallCount);
        builder.Add("trackRuntimeGetValueFromThumbTravelMs", FormatMilliseconds(runtime.GetValueFromThumbTravelMilliseconds));
        builder.Add("trackRuntimeGetValueFromThumbTravelScrollableRangeZero", runtime.GetValueFromThumbTravelScrollableRangeZeroCount);
        builder.Add("trackRuntimeGetValueFromThumbTravelMaxTravelZero", runtime.GetValueFromThumbTravelMaxTravelZeroCount);
        builder.Add("trackRuntimeGetValueFromThumbTravelDirectionReversed", runtime.GetValueFromThumbTravelDirectionReversedCount);
        builder.Add("trackRuntimeGetValueFromThumbTravelClamped", runtime.GetValueFromThumbTravelClampedCount);
        builder.Add("trackRuntimeGetValueFromPointCalls", runtime.GetValueFromPointCallCount);
        builder.Add("trackRuntimeGetValueFromPointMs", FormatMilliseconds(runtime.GetValueFromPointMilliseconds));
        builder.Add("trackRuntimeGetValuePositionCalls", runtime.GetValuePositionCallCount);
        builder.Add("trackRuntimeGetValuePositionMs", FormatMilliseconds(runtime.GetValuePositionMilliseconds));
        builder.Add("trackRuntimeHitTestDecreaseRegionCalls", runtime.HitTestDecreaseRegionCallCount);
        builder.Add("trackRuntimeHitTestDecreaseRegionHits", runtime.HitTestDecreaseRegionHitCount);
        builder.Add("trackRuntimeHitTestIncreaseRegionCalls", runtime.HitTestIncreaseRegionCallCount);
        builder.Add("trackRuntimeHitTestIncreaseRegionHits", runtime.HitTestIncreaseRegionHitCount);
        builder.Add("trackRuntimeInvalidateVisualCalls", runtime.InvalidateVisualCallCount);
        builder.Add("trackRuntimeInvalidateVisualClearedPendingHint", runtime.InvalidateVisualClearedPendingHintCount);
        builder.Add("trackRuntimeTryConsumeRenderDirtyBoundsHintCalls", runtime.TryConsumeRenderDirtyBoundsHintCallCount);
        builder.Add("trackRuntimeTryConsumeRenderDirtyBoundsHintHits", runtime.TryConsumeRenderDirtyBoundsHintHitCount);
        builder.Add("trackRuntimeMeasureOverrideCalls", runtime.MeasureOverrideCallCount);
        builder.Add("trackRuntimeMeasureOverrideMs", FormatMilliseconds(runtime.MeasureOverrideMilliseconds));
        builder.Add("trackRuntimeArrangeOverrideCalls", runtime.ArrangeOverrideCallCount);
        builder.Add("trackRuntimeArrangeOverrideMs", FormatMilliseconds(runtime.ArrangeOverrideMilliseconds));
        builder.Add("trackRuntimeArrangeVerticalCalls", runtime.ArrangeVerticalCallCount);
        builder.Add("trackRuntimeArrangeVerticalMs", FormatMilliseconds(runtime.ArrangeVerticalMilliseconds));
        builder.Add("trackRuntimeArrangeHorizontalCalls", runtime.ArrangeHorizontalCallCount);
        builder.Add("trackRuntimeArrangeHorizontalMs", FormatMilliseconds(runtime.ArrangeHorizontalMilliseconds));
        builder.Add("trackRuntimeRenderCalls", runtime.RenderCallCount);
        builder.Add("trackRuntimeRenderMs", FormatMilliseconds(runtime.RenderMilliseconds));
        builder.Add("trackRuntimeDrawBorderCalls", runtime.DrawBorderCallCount);
        builder.Add("trackRuntimeDrawBorderMs", FormatMilliseconds(runtime.DrawBorderMilliseconds));
        builder.Add("trackRuntimeResolvePartsCalls", runtime.ResolvePartsCallCount);
        builder.Add("trackRuntimeResolvePartsMs", FormatMilliseconds(runtime.ResolvePartsMilliseconds));
        builder.Add("trackRuntimeResolvePartsDuplicateRole", runtime.ResolvePartsDuplicateRoleCount);
        builder.Add("trackRuntimeComputeThumbRectCalls", runtime.ComputeThumbRectCallCount);
        builder.Add("trackRuntimeComputeThumbRectMs", FormatMilliseconds(runtime.ComputeThumbRectMilliseconds));
        builder.Add("trackRuntimeComputeSliderThumbRectCalls", runtime.ComputeSliderThumbRectCallCount);
        builder.Add("trackRuntimeComputeSliderThumbRectMs", FormatMilliseconds(runtime.ComputeSliderThumbRectMilliseconds));
        builder.Add("trackRuntimeResolveThumbAxisLengthCalls", runtime.ResolveThumbAxisLengthCallCount);
        builder.Add("trackRuntimeResolveThumbAxisLengthMs", FormatMilliseconds(runtime.ResolveThumbAxisLengthMilliseconds));
        builder.Add("trackRuntimeGetScrollableRangeCalls", runtime.GetScrollableRangeCallCount);
        builder.Add("trackRuntimeClampValueCalls", runtime.ClampValueCallCount);
        builder.Add("trackRuntimeOnStateMutationChangedCalls", runtime.OnStateMutationChangedCallCount);
        builder.Add("trackRuntimeRefreshLayoutCalls", runtime.RefreshLayoutForStateMutationCallCount);
        builder.Add("trackRuntimeRefreshLayoutMs", FormatMilliseconds(runtime.RefreshLayoutForStateMutationMilliseconds));
        builder.Add("trackRuntimeRefreshLayoutNeedsMeasureFallback", runtime.RefreshLayoutNeedsMeasureFallbackCount);
        builder.Add("trackRuntimeRefreshLayoutDirtyBoundsHint", runtime.RefreshLayoutDirtyBoundsHintCount);
        builder.Add("trackRuntimeRefreshLayoutVisualFallback", runtime.RefreshLayoutVisualFallbackCount);
        builder.Add("trackRuntimeInvalidateVisualWithDirtyBoundsHintCalls", runtime.InvalidateVisualWithDirtyBoundsHintCallCount);
        builder.Add("trackRuntimeInvalidateVisualWithDirtyBoundsHintMs", FormatMilliseconds(runtime.InvalidateVisualWithDirtyBoundsHintMilliseconds));
        builder.Add("trackRuntimeCaptureRenderMutationSnapshotCalls", runtime.CaptureRenderMutationSnapshotCallCount);
        builder.Add("trackRuntimeCaptureRenderMutationSnapshotMs", FormatMilliseconds(runtime.CaptureRenderMutationSnapshotMilliseconds));
        builder.Add("trackRuntimeTryBuildRenderMutationDirtyBoundsCalls", runtime.TryBuildRenderMutationDirtyBoundsCallCount);
        builder.Add("trackRuntimeTryBuildRenderMutationDirtyBoundsBuilt", runtime.TryBuildRenderMutationDirtyBoundsBuiltCount);
        builder.Add("trackRuntimeTryBuildRenderMutationDirtyBoundsTrackChanged", runtime.TryBuildRenderMutationDirtyBoundsTrackChangedCount);
        builder.Add("trackRuntimeTryBuildRenderMutationDirtyBoundsPartChanged", runtime.TryBuildRenderMutationDirtyBoundsPartChangedCount);

        builder.Add("trackGetThumbRectCalls", telemetry.GetThumbRectCallCount);
        builder.Add("trackGetTrackRectCalls", telemetry.GetTrackRectCallCount);
        builder.Add("trackGetThumbTravelCalls", telemetry.GetThumbTravelCallCount);
        builder.Add("trackGetValueFromThumbTravelCalls", telemetry.GetValueFromThumbTravelCallCount);
        builder.Add("trackGetValueFromThumbTravelMs", FormatMilliseconds(telemetry.GetValueFromThumbTravelMilliseconds));
        builder.Add("trackGetValueFromThumbTravelScrollableRangeZero", telemetry.GetValueFromThumbTravelScrollableRangeZeroCount);
        builder.Add("trackGetValueFromThumbTravelMaxTravelZero", telemetry.GetValueFromThumbTravelMaxTravelZeroCount);
        builder.Add("trackGetValueFromThumbTravelDirectionReversed", telemetry.GetValueFromThumbTravelDirectionReversedCount);
        builder.Add("trackGetValueFromThumbTravelClamped", telemetry.GetValueFromThumbTravelClampedCount);
        builder.Add("trackGetValueFromPointCalls", telemetry.GetValueFromPointCallCount);
        builder.Add("trackGetValueFromPointMs", FormatMilliseconds(telemetry.GetValueFromPointMilliseconds));
        builder.Add("trackGetValueFromPointThumbCenterOffset", telemetry.GetValueFromPointThumbCenterOffsetCount);
        builder.Add("trackGetValuePositionCalls", telemetry.GetValuePositionCallCount);
        builder.Add("trackGetValuePositionMs", FormatMilliseconds(telemetry.GetValuePositionMilliseconds));
        builder.Add("trackHitTestDecreaseRegionCalls", telemetry.HitTestDecreaseRegionCallCount);
        builder.Add("trackHitTestDecreaseRegionHits", telemetry.HitTestDecreaseRegionHitCount);
        builder.Add("trackHitTestIncreaseRegionCalls", telemetry.HitTestIncreaseRegionCallCount);
        builder.Add("trackHitTestIncreaseRegionHits", telemetry.HitTestIncreaseRegionHitCount);
        builder.Add("trackTryConsumeRenderDirtyBoundsHintCalls", telemetry.TryConsumeRenderDirtyBoundsHintCallCount);
        builder.Add("trackTryConsumeRenderDirtyBoundsHintHits", telemetry.TryConsumeRenderDirtyBoundsHintHitCount);
        builder.Add("trackMeasureOverrideCalls", telemetry.MeasureOverrideCallCount);
        builder.Add("trackMeasureOverrideMs", FormatMilliseconds(telemetry.MeasureOverrideMilliseconds));
        builder.Add("trackArrangeOverrideCalls", telemetry.ArrangeOverrideCallCount);
        builder.Add("trackArrangeOverrideMs", FormatMilliseconds(telemetry.ArrangeOverrideMilliseconds));
        builder.Add("trackArrangeVerticalCalls", telemetry.ArrangeVerticalCallCount);
        builder.Add("trackArrangeVerticalMs", FormatMilliseconds(telemetry.ArrangeVerticalMilliseconds));
        builder.Add("trackArrangeHorizontalCalls", telemetry.ArrangeHorizontalCallCount);
        builder.Add("trackArrangeHorizontalMs", FormatMilliseconds(telemetry.ArrangeHorizontalMilliseconds));
        builder.Add("trackRenderCalls", telemetry.RenderCallCount);
        builder.Add("trackRenderMs", FormatMilliseconds(telemetry.RenderMilliseconds));
        builder.Add("trackResolvePartsCalls", telemetry.ResolvePartsCallCount);
        builder.Add("trackResolvePartsMs", FormatMilliseconds(telemetry.ResolvePartsMilliseconds));
        builder.Add("trackResolvePartsDuplicateRole", telemetry.ResolvePartsDuplicateRoleCount);
        builder.Add("trackComputeThumbRectCalls", telemetry.ComputeThumbRectCallCount);
        builder.Add("trackComputeThumbRectMs", FormatMilliseconds(telemetry.ComputeThumbRectMilliseconds));
        builder.Add("trackComputeSliderThumbRectCalls", telemetry.ComputeSliderThumbRectCallCount);
        builder.Add("trackComputeSliderThumbRectMs", FormatMilliseconds(telemetry.ComputeSliderThumbRectMilliseconds));
        builder.Add("trackResolveThumbAxisLengthCalls", telemetry.ResolveThumbAxisLengthCallCount);
        builder.Add("trackResolveThumbAxisLengthMs", FormatMilliseconds(telemetry.ResolveThumbAxisLengthMilliseconds));
        builder.Add("trackGetScrollableRangeCalls", telemetry.GetScrollableRangeCallCount);
        builder.Add("trackClampValueCalls", telemetry.ClampValueCallCount);
        builder.Add("trackOnStateMutationChangedCalls", telemetry.OnStateMutationChangedCallCount);
        builder.Add("trackRefreshLayoutCalls", telemetry.RefreshLayoutForStateMutationCallCount);
        builder.Add("trackRefreshLayoutMs", FormatMilliseconds(telemetry.RefreshLayoutForStateMutationMilliseconds));
        builder.Add("trackRefreshLayoutValueMutation", telemetry.RefreshLayoutValueMutationCallCount);
        builder.Add("trackRefreshLayoutViewportMutation", telemetry.RefreshLayoutViewportMutationCallCount);
        builder.Add("trackRefreshLayoutMinimumMutation", telemetry.RefreshLayoutMinimumMutationCallCount);
        builder.Add("trackRefreshLayoutMaximumMutation", telemetry.RefreshLayoutMaximumMutationCallCount);
        builder.Add("trackRefreshLayoutDirectionMutation", telemetry.RefreshLayoutDirectionMutationCallCount);
        builder.Add("trackRefreshLayoutNeedsMeasureFallback", telemetry.RefreshLayoutNeedsMeasureFallbackCount);
        builder.Add("trackRefreshLayoutDirtyBoundsHint", telemetry.RefreshLayoutDirtyBoundsHintCount);
        builder.Add("trackRefreshLayoutVisualFallback", telemetry.RefreshLayoutVisualFallbackCount);
        builder.Add("trackInvalidateVisualWithDirtyBoundsHintCalls", telemetry.InvalidateVisualWithDirtyBoundsHintCallCount);
        builder.Add("trackInvalidateVisualWithDirtyBoundsHintMs", FormatMilliseconds(telemetry.InvalidateVisualWithDirtyBoundsHintMilliseconds));
        builder.Add("trackCaptureRenderMutationSnapshotCalls", telemetry.CaptureRenderMutationSnapshotCallCount);
        builder.Add("trackCaptureRenderMutationSnapshotMs", FormatMilliseconds(telemetry.CaptureRenderMutationSnapshotMilliseconds));
        builder.Add("trackTryBuildRenderMutationDirtyBoundsCalls", telemetry.TryBuildRenderMutationDirtyBoundsCallCount);
        builder.Add("trackTryBuildRenderMutationDirtyBoundsBuilt", telemetry.TryBuildRenderMutationDirtyBoundsBuiltCount);
        builder.Add("trackTryBuildRenderMutationDirtyBoundsTrackChanged", telemetry.TryBuildRenderMutationDirtyBoundsTrackChangedCount);
        builder.Add("trackTryBuildRenderMutationDirtyBoundsPartChanged", telemetry.TryBuildRenderMutationDirtyBoundsPartChangedCount);
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }

    private static string FormatRect(float x, float y, float width, float height)
    {
        return $"{x:0.##},{y:0.##},{width:0.##},{height:0.##}";
    }
}
