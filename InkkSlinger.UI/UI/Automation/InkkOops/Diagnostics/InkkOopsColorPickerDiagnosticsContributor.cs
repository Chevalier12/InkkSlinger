namespace InkkSlinger;

using Microsoft.Xna.Framework;

public sealed class InkkOopsColorPickerDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 42;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not ColorPicker picker)
        {
            return;
        }

        var runtime = picker.GetColorPickerSnapshotForDiagnostics();
        var telemetry = ColorPicker.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("colorPickerSpectrumRect", FormatRect(runtime.SpectrumRect));
        builder.Add("colorPickerSelector", $"{runtime.SaturationSelector.X:0.##},{runtime.SaturationSelector.Y:0.##}");
        builder.Add("colorPickerSelectorRadius", $"{runtime.SelectionIndicatorRadius:0.##}");
        builder.Add("colorPickerSelectedColor", FormatColor(runtime.SelectedColor));
        builder.Add("colorPickerHue", FormatFloat(runtime.Hue));
        builder.Add("colorPickerSaturation", FormatFloat(runtime.Saturation));
        builder.Add("colorPickerValue", FormatFloat(runtime.Value));
        builder.Add("colorPickerAlpha", FormatFloat(runtime.Alpha));
        builder.Add("colorPickerDragging", runtime.IsDragging);
        builder.Add("colorPickerMouseOver", runtime.IsMouseOver);
        builder.Add("colorPickerHasPendingSelectedColorSync", runtime.HasPendingSelectedColorSync);
        builder.Add("colorPickerSelectedColorSyncDeferred", runtime.IsSelectedColorSyncDeferred);
        builder.Add("colorPickerSynchronizingSelectedColor", runtime.IsSynchronizingSelectedColor);
        builder.Add("colorPickerSynchronizingComponents", runtime.IsSynchronizingComponents);
        builder.Add("colorPickerRuntimePointerDownCalls", runtime.HandlePointerDownCallCount);
        builder.Add("colorPickerRuntimePointerDownMs", FormatMilliseconds(runtime.HandlePointerDownMilliseconds));
        builder.Add("colorPickerRuntimePointerHits", runtime.HandlePointerDownHitCount);
        builder.Add("colorPickerRuntimePointerMisses", runtime.HandlePointerDownMissCount);
        builder.Add("colorPickerRuntimePointerMoveCalls", runtime.HandlePointerMoveCallCount);
        builder.Add("colorPickerRuntimePointerMoveMs", FormatMilliseconds(runtime.HandlePointerMoveMilliseconds));
        builder.Add("colorPickerRuntimePointerMoveDrag", runtime.HandlePointerMoveDragCount);
        builder.Add("colorPickerRuntimePointerMoveIgnored", runtime.HandlePointerMoveIgnoredCount);
        builder.Add("colorPickerRuntimePointerUpCalls", runtime.HandlePointerUpCallCount);
        builder.Add("colorPickerRuntimePointerUpStateChanges", runtime.HandlePointerUpStateChangeCount);
        builder.Add("colorPickerRuntimeMouseOverCalls", runtime.SetMouseOverCallCount);
        builder.Add("colorPickerRuntimeMouseOverStateChanges", runtime.SetMouseOverStateChangeCount);
        builder.Add("colorPickerRuntimeUpdateFromPointerCalls", runtime.UpdateSpectrumFromPointerCallCount);
        builder.Add("colorPickerRuntimeUpdateFromPointerMs", FormatMilliseconds(runtime.UpdateSpectrumFromPointerMilliseconds));
        builder.Add("colorPickerRuntimeUpdateFromPointerZeroRectSkips", runtime.UpdateSpectrumFromPointerZeroRectSkipCount);
        builder.Add("colorPickerRuntimeRequestSelectedColorSyncCalls", runtime.RequestSelectedColorSyncCallCount);
        builder.Add("colorPickerRuntimeRequestSelectedColorSyncDragDeferred", runtime.RequestSelectedColorSyncDragDeferredCount);
        builder.Add("colorPickerRuntimeQueueDeferredSelectedColorSyncCalls", runtime.QueueDeferredSelectedColorSyncCallCount);
        builder.Add("colorPickerRuntimeQueueDeferredSelectedColorSyncAlreadyQueued", runtime.QueueDeferredSelectedColorSyncAlreadyQueuedCount);
        builder.Add("colorPickerRuntimeFlushDeferredSelectedColorSyncCalls", runtime.FlushDeferredSelectedColorSyncCallCount);
        builder.Add("colorPickerRuntimeFlushDeferredSelectedColorSyncNoPending", runtime.FlushDeferredSelectedColorSyncNoPendingCount);
        builder.Add("colorPickerRuntimeFlushDeferredSelectedColorSyncRequeueWhileDragging", runtime.FlushDeferredSelectedColorSyncRequeueWhileDraggingCount);
        builder.Add("colorPickerRuntimeFlushPendingSelectedColorSyncAfterDragCalls", runtime.FlushPendingSelectedColorSyncAfterDragCallCount);
        builder.Add("colorPickerRuntimeFlushPendingSelectedColorSyncAfterDragNoPending", runtime.FlushPendingSelectedColorSyncAfterDragNoPendingCount);
        builder.Add("colorPickerRuntimeSyncSelectedColorCalls", runtime.SyncSelectedColorFromComponentsCallCount);
        builder.Add("colorPickerRuntimeSyncSelectedColorMs", FormatMilliseconds(runtime.SyncSelectedColorFromComponentsMilliseconds));
        builder.Add("colorPickerRuntimeSyncSelectedColorReentrantSkip", runtime.SyncSelectedColorFromComponentsReentrantSkipCount);
        builder.Add("colorPickerRuntimeSyncSelectedColorNoOp", runtime.SyncSelectedColorFromComponentsNoOpCount);
        builder.Add("colorPickerRuntimeSelectedColorChangedCalls", runtime.SelectedColorChangedCallCount);
        builder.Add("colorPickerRuntimeSelectedColorChangedExternalSync", runtime.SelectedColorChangedExternalSyncCount);
        builder.Add("colorPickerRuntimeSelectedColorChangedComponentWriteback", runtime.SelectedColorChangedComponentWritebackCount);
        builder.Add("colorPickerRuntimeSelectedColorChangedRaised", runtime.SelectedColorChangedRaisedCount);
        builder.Add("colorPickerRuntimeHueChangedCalls", runtime.HueChangedCallCount);
        builder.Add("colorPickerRuntimeSaturationChangedCalls", runtime.SaturationChangedCallCount);
        builder.Add("colorPickerRuntimeValueChangedCalls", runtime.ValueChangedCallCount);
        builder.Add("colorPickerRuntimeAlphaChangedCalls", runtime.AlphaChangedCallCount);
        builder.Add("colorPickerRuntimeRenderCalls", runtime.RenderCallCount);
        builder.Add("colorPickerRuntimeRenderMs", FormatMilliseconds(runtime.RenderMilliseconds));
        builder.Add("colorPickerRuntimeRenderZeroRectSkips", runtime.RenderSkippedZeroRectCount);
        builder.Add("colorPickerRuntimeTextureCacheHits", runtime.SpectrumTextureCacheHitCount);
        builder.Add("colorPickerRuntimeTextureCacheMisses", runtime.SpectrumTextureCacheMissCount);
        builder.Add("colorPickerRuntimeTextureBuilds", runtime.SpectrumTextureBuildCount);
        builder.Add("colorPickerRuntimeTextureBuildMs", FormatMilliseconds(runtime.SpectrumTextureBuildMilliseconds));

        builder.Add("colorPickerPointerDownCalls", telemetry.HandlePointerDownCallCount);
        builder.Add("colorPickerPointerDownMs", FormatMilliseconds(telemetry.HandlePointerDownMilliseconds));
        builder.Add("colorPickerPointerHits", telemetry.HandlePointerDownHitCount);
        builder.Add("colorPickerPointerMisses", telemetry.HandlePointerDownMissCount);
        builder.Add("colorPickerPointerMoveCalls", telemetry.HandlePointerMoveCallCount);
        builder.Add("colorPickerPointerMoveMs", FormatMilliseconds(telemetry.HandlePointerMoveMilliseconds));
        builder.Add("colorPickerPointerMoveDrag", telemetry.HandlePointerMoveDragCount);
        builder.Add("colorPickerPointerMoveIgnored", telemetry.HandlePointerMoveIgnoredCount);
        builder.Add("colorPickerPointerUpCalls", telemetry.HandlePointerUpCallCount);
        builder.Add("colorPickerPointerUpStateChanges", telemetry.HandlePointerUpStateChangeCount);
        builder.Add("colorPickerMouseOverCalls", telemetry.SetMouseOverCallCount);
        builder.Add("colorPickerMouseOverStateChanges", telemetry.SetMouseOverStateChangeCount);
        builder.Add("colorPickerUpdateFromPointerCalls", telemetry.UpdateSpectrumFromPointerCallCount);
        builder.Add("colorPickerUpdateFromPointerMs", FormatMilliseconds(telemetry.UpdateSpectrumFromPointerMilliseconds));
        builder.Add("colorPickerUpdateFromPointerZeroRectSkips", telemetry.UpdateSpectrumFromPointerZeroRectSkipCount);
        builder.Add("colorPickerRequestSelectedColorSyncCalls", telemetry.RequestSelectedColorSyncCallCount);
        builder.Add("colorPickerRequestSelectedColorSyncDragDeferred", telemetry.RequestSelectedColorSyncDragDeferredCount);
        builder.Add("colorPickerQueueDeferredSelectedColorSyncCalls", telemetry.QueueDeferredSelectedColorSyncCallCount);
        builder.Add("colorPickerQueueDeferredSelectedColorSyncAlreadyQueued", telemetry.QueueDeferredSelectedColorSyncAlreadyQueuedCount);
        builder.Add("colorPickerFlushDeferredSelectedColorSyncCalls", telemetry.FlushDeferredSelectedColorSyncCallCount);
        builder.Add("colorPickerFlushDeferredSelectedColorSyncNoPending", telemetry.FlushDeferredSelectedColorSyncNoPendingCount);
        builder.Add("colorPickerFlushDeferredSelectedColorSyncRequeueWhileDragging", telemetry.FlushDeferredSelectedColorSyncRequeueWhileDraggingCount);
        builder.Add("colorPickerFlushPendingSelectedColorSyncAfterDragCalls", telemetry.FlushPendingSelectedColorSyncAfterDragCallCount);
        builder.Add("colorPickerFlushPendingSelectedColorSyncAfterDragNoPending", telemetry.FlushPendingSelectedColorSyncAfterDragNoPendingCount);
        builder.Add("colorPickerSyncSelectedColorCalls", telemetry.SyncSelectedColorFromComponentsCallCount);
        builder.Add("colorPickerSyncSelectedColorMs", FormatMilliseconds(telemetry.SyncSelectedColorFromComponentsMilliseconds));
        builder.Add("colorPickerSyncSelectedColorReentrantSkip", telemetry.SyncSelectedColorFromComponentsReentrantSkipCount);
        builder.Add("colorPickerSyncSelectedColorNoOp", telemetry.SyncSelectedColorFromComponentsNoOpCount);
        builder.Add("colorPickerSelectedColorChangedCalls", telemetry.SelectedColorChangedCallCount);
        builder.Add("colorPickerSelectedColorChangedExternalSync", telemetry.SelectedColorChangedExternalSyncCount);
        builder.Add("colorPickerSelectedColorChangedComponentWriteback", telemetry.SelectedColorChangedComponentWritebackCount);
        builder.Add("colorPickerSelectedColorChangedRaised", telemetry.SelectedColorChangedRaisedCount);
        builder.Add("colorPickerHueChangedCalls", telemetry.HueChangedCallCount);
        builder.Add("colorPickerSaturationChangedCalls", telemetry.SaturationChangedCallCount);
        builder.Add("colorPickerValueChangedCalls", telemetry.ValueChangedCallCount);
        builder.Add("colorPickerAlphaChangedCalls", telemetry.AlphaChangedCallCount);
        builder.Add("colorPickerRenderCalls", telemetry.RenderCallCount);
        builder.Add("colorPickerRenderMs", FormatMilliseconds(telemetry.RenderMilliseconds));
        builder.Add("colorPickerRenderZeroRectSkips", telemetry.RenderSkippedZeroRectCount);
        builder.Add("colorPickerTextureCacheHits", telemetry.SpectrumTextureCacheHitCount);
        builder.Add("colorPickerTextureCacheMisses", telemetry.SpectrumTextureCacheMissCount);
        builder.Add("colorPickerTextureBuilds", telemetry.SpectrumTextureBuildCount);
        builder.Add("colorPickerTextureBuildMs", FormatMilliseconds(telemetry.SpectrumTextureBuildMilliseconds));

        _ = context;
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}";
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.###");
    }

    private static string FormatColor(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2} ({color.PackedValue})";
    }
}