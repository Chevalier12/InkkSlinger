namespace InkkSlinger;

public sealed class InkkOopsScrollBarDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 50;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        _ = context;

        if (element is not ScrollBar target)
        {
            return;
        }

        var runtime = target.GetScrollBarSnapshotForDiagnostics();
        var telemetry = ScrollBar.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("scrollBarOrientation", runtime.Orientation);
        builder.Add("scrollBarMinimum", $"{runtime.Minimum:0.###}");
        builder.Add("scrollBarMaximum", $"{runtime.Maximum:0.###}");
        builder.Add("scrollBarValue", $"{runtime.Value:0.###}");
        builder.Add("scrollBarViewportSize", $"{runtime.ViewportSize:0.###}");
        builder.Add("scrollBarSmallChange", $"{runtime.SmallChange:0.###}");
        builder.Add("scrollBarLargeChange", $"{runtime.LargeChange:0.###}");
        builder.Add("scrollBarLayoutSlot", $"{runtime.LayoutSlotWidth:0.##},{runtime.LayoutSlotHeight:0.##}");
        builder.Add("scrollBarHasTrack", runtime.HasTrack);
        builder.Add("scrollBarTrackType", runtime.TrackType);
        builder.Add("scrollBarHasThumb", runtime.HasThumb);
        builder.Add("scrollBarThumbType", runtime.ThumbType);
        builder.Add("scrollBarHasLineUpButton", runtime.HasLineUpButton);
        builder.Add("scrollBarLineUpButtonType", runtime.LineUpButtonType);
        builder.Add("scrollBarHasLineDownButton", runtime.HasLineDownButton);
        builder.Add("scrollBarLineDownButtonType", runtime.LineDownButtonType);
        builder.Add("scrollBarThumbDragOriginTravel", $"{runtime.ThumbDragOriginTravel:0.###}");
        builder.Add("scrollBarThumbDragAccumulatedDelta", $"{runtime.ThumbDragAccumulatedDelta:0.###}");

        builder.Add("scrollBarRuntimeApplyTemplateCalls", runtime.OnApplyTemplateCallCount);
        builder.Add("scrollBarRuntimeApplyTemplateMissingPart", runtime.OnApplyTemplateMissingPartCount);
        builder.Add("scrollBarRuntimeApplyTemplateEnsureTrackDescendant", runtime.OnApplyTemplateEnsureTrackDescendantCount);
        builder.Add("scrollBarRuntimeApplyTemplateHandlerAttach", runtime.OnApplyTemplateHandlerAttachCount);
        builder.Add("scrollBarRuntimeMinimumChangedCalls", runtime.OnMinimumChangedCallCount);
        builder.Add("scrollBarRuntimeMaximumChangedCalls", runtime.OnMaximumChangedCallCount);
        builder.Add("scrollBarRuntimeValueChangedCalls", runtime.OnValueChangedCallCount);
        builder.Add("scrollBarRuntimeValueChangedBaseMs", FormatMilliseconds(runtime.OnValueChangedBaseMilliseconds));
        builder.Add("scrollBarRuntimeValueChangedSyncTrackStateMs", FormatMilliseconds(runtime.OnValueChangedSyncTrackStateMilliseconds));
        builder.Add("scrollBarRuntimeTrackMouseDownCalls", runtime.OnTrackMouseDownCallCount);
        builder.Add("scrollBarRuntimeTrackMouseDownIgnoredDisabledOrNoTrack", runtime.OnTrackMouseDownIgnoredDisabledOrNoTrackCount);
        builder.Add("scrollBarRuntimeTrackMouseDownIgnoredPartTarget", runtime.OnTrackMouseDownIgnoredPartTargetCount);
        builder.Add("scrollBarRuntimeTrackMouseDownDecreaseHit", runtime.OnTrackMouseDownDecreaseHitCount);
        builder.Add("scrollBarRuntimeTrackMouseDownIncreaseHit", runtime.OnTrackMouseDownIncreaseHitCount);
        builder.Add("scrollBarRuntimeTrackMouseDownMiss", runtime.OnTrackMouseDownMissCount);
        builder.Add("scrollBarRuntimeLineUpClickCalls", runtime.OnLineUpButtonClickCallCount);
        builder.Add("scrollBarRuntimeLineDownClickCalls", runtime.OnLineDownButtonClickCallCount);
        builder.Add("scrollBarRuntimeThumbDragStartedCalls", runtime.OnThumbDragStartedCallCount);
        builder.Add("scrollBarRuntimeThumbDragStartedNoTrack", runtime.OnThumbDragStartedNoTrackCount);
        builder.Add("scrollBarRuntimeThumbDragDeltaCalls", runtime.OnThumbDragDeltaCallCount);
        builder.Add("scrollBarRuntimeThumbDragDeltaMs", FormatMilliseconds(runtime.OnThumbDragDeltaMilliseconds));
        builder.Add("scrollBarRuntimeThumbDragDeltaValueSetMs", FormatMilliseconds(runtime.OnThumbDragDeltaValueSetMilliseconds));
        builder.Add("scrollBarRuntimeThumbDragDeltaNoTrack", runtime.OnThumbDragDeltaNoTrackCount);
        builder.Add("scrollBarRuntimeThumbDragDeltaVerticalPath", runtime.OnThumbDragDeltaVerticalPathCount);
        builder.Add("scrollBarRuntimeThumbDragDeltaHorizontalPath", runtime.OnThumbDragDeltaHorizontalPathCount);
        builder.Add("scrollBarRuntimeThumbDragCompletedCalls", runtime.OnThumbDragCompletedCallCount);
        builder.Add("scrollBarRuntimeSyncTrackStateCalls", runtime.SyncTrackStateCallCount);
        builder.Add("scrollBarRuntimeSyncTrackStateMs", FormatMilliseconds(runtime.SyncTrackStateMilliseconds));
        builder.Add("scrollBarRuntimeSyncTrackStateNoTrack", runtime.SyncTrackStateNoTrackCount);
        builder.Add("scrollBarRuntimeSyncTrackStateCoercedValueChange", runtime.SyncTrackStateCoercedValueChangeCount);
        builder.Add("scrollBarRuntimeRefreshTrackLayoutCalls", runtime.RefreshTrackLayoutCallCount);
        builder.Add("scrollBarRuntimeRefreshTrackLayoutMs", FormatMilliseconds(runtime.RefreshTrackLayoutMilliseconds));
        builder.Add("scrollBarRuntimeRefreshTrackLayoutNoTrack", runtime.RefreshTrackLayoutNoTrackCount);
        builder.Add("scrollBarRuntimeRefreshTrackLayoutZeroSlot", runtime.RefreshTrackLayoutZeroSlotCount);
        builder.Add("scrollBarRuntimeRefreshTrackLayoutNoLayoutNeeded", runtime.RefreshTrackLayoutNoLayoutNeededCount);
        builder.Add("scrollBarRuntimeRefreshTrackLayoutArranged", runtime.RefreshTrackLayoutArrangedCount);

        builder.Add("scrollBarThumbDragCalls", telemetry.OnThumbDragDeltaCallCount);
        builder.Add("scrollBarThumbDragMs", FormatMilliseconds(telemetry.OnThumbDragDeltaMilliseconds));
        builder.Add("scrollBarThumbDragValueSetMs", FormatMilliseconds(telemetry.OnThumbDragDeltaValueSetMilliseconds));
        builder.Add("scrollBarValueChangedBaseMs", FormatMilliseconds(telemetry.OnValueChangedBaseMilliseconds));
        builder.Add("scrollBarValueChangedSyncTrackStateMs", FormatMilliseconds(telemetry.OnValueChangedSyncTrackStateMilliseconds));
        builder.Add("scrollBarSyncTrackStateMs", FormatMilliseconds(telemetry.SyncTrackStateMilliseconds));
        builder.Add("scrollBarRefreshTrackLayoutMs", FormatMilliseconds(telemetry.RefreshTrackLayoutMilliseconds));
        builder.Add("scrollBarApplyTemplateCalls", telemetry.OnApplyTemplateCallCount);
        builder.Add("scrollBarApplyTemplateMissingPart", telemetry.OnApplyTemplateMissingPartCount);
        builder.Add("scrollBarApplyTemplateEnsureTrackDescendant", telemetry.OnApplyTemplateEnsureTrackDescendantCount);
        builder.Add("scrollBarApplyTemplateHandlerAttach", telemetry.OnApplyTemplateHandlerAttachCount);
        builder.Add("scrollBarMinimumChangedCalls", telemetry.OnMinimumChangedCallCount);
        builder.Add("scrollBarMaximumChangedCalls", telemetry.OnMaximumChangedCallCount);
        builder.Add("scrollBarValueChangedCalls", telemetry.OnValueChangedCallCount);
        builder.Add("scrollBarTrackMouseDownCalls", telemetry.OnTrackMouseDownCallCount);
        builder.Add("scrollBarTrackMouseDownIgnoredDisabledOrNoTrack", telemetry.OnTrackMouseDownIgnoredDisabledOrNoTrackCount);
        builder.Add("scrollBarTrackMouseDownIgnoredPartTarget", telemetry.OnTrackMouseDownIgnoredPartTargetCount);
        builder.Add("scrollBarTrackMouseDownDecreaseHit", telemetry.OnTrackMouseDownDecreaseHitCount);
        builder.Add("scrollBarTrackMouseDownIncreaseHit", telemetry.OnTrackMouseDownIncreaseHitCount);
        builder.Add("scrollBarTrackMouseDownMiss", telemetry.OnTrackMouseDownMissCount);
        builder.Add("scrollBarLineUpClickCalls", telemetry.OnLineUpButtonClickCallCount);
        builder.Add("scrollBarLineDownClickCalls", telemetry.OnLineDownButtonClickCallCount);
        builder.Add("scrollBarThumbDragStartedCalls", telemetry.OnThumbDragStartedCallCount);
        builder.Add("scrollBarThumbDragStartedNoTrack", telemetry.OnThumbDragStartedNoTrackCount);
        builder.Add("scrollBarThumbDragDeltaNoTrack", telemetry.OnThumbDragDeltaNoTrackCount);
        builder.Add("scrollBarThumbDragDeltaVerticalPath", telemetry.OnThumbDragDeltaVerticalPathCount);
        builder.Add("scrollBarThumbDragDeltaHorizontalPath", telemetry.OnThumbDragDeltaHorizontalPathCount);
        builder.Add("scrollBarThumbDragCompletedCalls", telemetry.OnThumbDragCompletedCallCount);
        builder.Add("scrollBarSyncTrackStateCalls", telemetry.SyncTrackStateCallCount);
        builder.Add("scrollBarSyncTrackStateNoTrack", telemetry.SyncTrackStateNoTrackCount);
        builder.Add("scrollBarSyncTrackStateCoercedValueChange", telemetry.SyncTrackStateCoercedValueChangeCount);
        builder.Add("scrollBarRefreshTrackLayoutCalls", telemetry.RefreshTrackLayoutCallCount);
        builder.Add("scrollBarRefreshTrackLayoutNoTrack", telemetry.RefreshTrackLayoutNoTrackCount);
        builder.Add("scrollBarRefreshTrackLayoutZeroSlot", telemetry.RefreshTrackLayoutZeroSlotCount);
        builder.Add("scrollBarRefreshTrackLayoutNoLayoutNeeded", telemetry.RefreshTrackLayoutNoLayoutNeededCount);
        builder.Add("scrollBarRefreshTrackLayoutArranged", telemetry.RefreshTrackLayoutArrangedCount);
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }
}
