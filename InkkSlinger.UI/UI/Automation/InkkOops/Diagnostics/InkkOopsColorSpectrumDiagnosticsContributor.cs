namespace InkkSlinger;

using Microsoft.Xna.Framework;

public sealed class InkkOopsColorSpectrumDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 43;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not ColorSpectrum spectrum)
        {
            return;
        }

        var runtime = spectrum.GetColorSpectrumSnapshotForDiagnostics();
        var telemetry = ColorSpectrum.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("colorSpectrumRect", FormatRect(runtime.SpectrumRect));
        builder.Add("colorSpectrumSelectorNormalizedOffset", $"{runtime.SelectorNormalizedOffset:0.###}");
        builder.Add("colorSpectrumSelectorPosition", $"{runtime.SelectorPosition:0.##}");
        builder.Add("colorSpectrumMode", runtime.Mode);
        builder.Add("colorSpectrumSelectedColor", FormatColor(runtime.SelectedColor));
        builder.Add("colorSpectrumHue", FormatFloat(runtime.Hue));
        builder.Add("colorSpectrumSaturation", FormatFloat(runtime.Saturation));
        builder.Add("colorSpectrumValue", FormatFloat(runtime.Value));
        builder.Add("colorSpectrumAlpha", FormatFloat(runtime.Alpha));
        builder.Add("colorSpectrumOrientation", runtime.Orientation);
        builder.Add("colorSpectrumDragging", runtime.IsDragging);
        builder.Add("colorSpectrumMouseOver", runtime.IsMouseOver);
        builder.Add("colorSpectrumHasPendingSelectedColorSync", runtime.HasPendingSelectedColorSync);
        builder.Add("colorSpectrumSelectedColorSyncDeferred", runtime.IsSelectedColorSyncDeferred);
        builder.Add("colorSpectrumSynchronizingSelectedColor", runtime.IsSynchronizingSelectedColor);
        builder.Add("colorSpectrumSynchronizingComponents", runtime.IsSynchronizingComponents);
        builder.Add("colorSpectrumRuntimePointerDownCalls", runtime.HandlePointerDownCallCount);
        builder.Add("colorSpectrumRuntimePointerDownMs", FormatMilliseconds(runtime.HandlePointerDownMilliseconds));
        builder.Add("colorSpectrumRuntimePointerHits", runtime.HandlePointerDownHitCount);
        builder.Add("colorSpectrumRuntimePointerMisses", runtime.HandlePointerDownMissCount);
        builder.Add("colorSpectrumRuntimePointerMoveCalls", runtime.HandlePointerMoveCallCount);
        builder.Add("colorSpectrumRuntimePointerMoveMs", FormatMilliseconds(runtime.HandlePointerMoveMilliseconds));
        builder.Add("colorSpectrumRuntimePointerMoveDrag", runtime.HandlePointerMoveDragCount);
        builder.Add("colorSpectrumRuntimePointerMoveIgnored", runtime.HandlePointerMoveIgnoredCount);
        builder.Add("colorSpectrumRuntimePointerUpCalls", runtime.HandlePointerUpCallCount);
        builder.Add("colorSpectrumRuntimePointerUpStateChanges", runtime.HandlePointerUpStateChangeCount);
        builder.Add("colorSpectrumRuntimeMouseOverCalls", runtime.SetMouseOverCallCount);
        builder.Add("colorSpectrumRuntimeMouseOverStateChanges", runtime.SetMouseOverStateChangeCount);
        builder.Add("colorSpectrumRuntimeUpdateFromPointerCalls", runtime.UpdateFromPointerCallCount);
        builder.Add("colorSpectrumRuntimeUpdateFromPointerMs", FormatMilliseconds(runtime.UpdateFromPointerMilliseconds));
        builder.Add("colorSpectrumRuntimeUpdateFromPointerZeroRectSkips", runtime.UpdateFromPointerZeroRectSkipCount);
        builder.Add("colorSpectrumRuntimeUpdateFromPointerHuePath", runtime.UpdateFromPointerHuePathCount);
        builder.Add("colorSpectrumRuntimeUpdateFromPointerAlphaPath", runtime.UpdateFromPointerAlphaPathCount);
        builder.Add("colorSpectrumRuntimeRequestSelectedColorSyncCalls", runtime.RequestSelectedColorSyncCallCount);
        builder.Add("colorSpectrumRuntimeRequestSelectedColorSyncDragDeferred", runtime.RequestSelectedColorSyncDragDeferredCount);
        builder.Add("colorSpectrumRuntimeQueueDeferredSelectedColorSyncCalls", runtime.QueueDeferredSelectedColorSyncCallCount);
        builder.Add("colorSpectrumRuntimeQueueDeferredSelectedColorSyncAlreadyQueued", runtime.QueueDeferredSelectedColorSyncAlreadyQueuedCount);
        builder.Add("colorSpectrumRuntimeFlushDeferredSelectedColorSyncCalls", runtime.FlushDeferredSelectedColorSyncCallCount);
        builder.Add("colorSpectrumRuntimeFlushDeferredSelectedColorSyncNoPending", runtime.FlushDeferredSelectedColorSyncNoPendingCount);
        builder.Add("colorSpectrumRuntimeFlushDeferredSelectedColorSyncRequeueWhileDragging", runtime.FlushDeferredSelectedColorSyncRequeueWhileDraggingCount);
        builder.Add("colorSpectrumRuntimeFlushPendingSelectedColorSyncAfterDragCalls", runtime.FlushPendingSelectedColorSyncAfterDragCallCount);
        builder.Add("colorSpectrumRuntimeFlushPendingSelectedColorSyncAfterDragNoPending", runtime.FlushPendingSelectedColorSyncAfterDragNoPendingCount);
        builder.Add("colorSpectrumRuntimeSyncSelectedColorCalls", runtime.SyncSelectedColorFromComponentsCallCount);
        builder.Add("colorSpectrumRuntimeSyncSelectedColorMs", FormatMilliseconds(runtime.SyncSelectedColorFromComponentsMilliseconds));
        builder.Add("colorSpectrumRuntimeSyncSelectedColorReentrantSkip", runtime.SyncSelectedColorFromComponentsReentrantSkipCount);
        builder.Add("colorSpectrumRuntimeSyncSelectedColorNoOp", runtime.SyncSelectedColorFromComponentsNoOpCount);
        builder.Add("colorSpectrumRuntimeSelectedColorChangedCalls", runtime.SelectedColorChangedCallCount);
        builder.Add("colorSpectrumRuntimeSelectedColorChangedExternalSync", runtime.SelectedColorChangedExternalSyncCount);
        builder.Add("colorSpectrumRuntimeSelectedColorChangedComponentWriteback", runtime.SelectedColorChangedComponentWritebackCount);
        builder.Add("colorSpectrumRuntimeSelectedColorChangedRaised", runtime.SelectedColorChangedRaisedCount);
        builder.Add("colorSpectrumRuntimeHueChangedCalls", runtime.HueChangedCallCount);
        builder.Add("colorSpectrumRuntimeSaturationChangedCalls", runtime.SaturationChangedCallCount);
        builder.Add("colorSpectrumRuntimeValueChangedCalls", runtime.ValueChangedCallCount);
        builder.Add("colorSpectrumRuntimeAlphaChangedCalls", runtime.AlphaChangedCallCount);
        builder.Add("colorSpectrumRuntimeRenderCalls", runtime.RenderCallCount);
        builder.Add("colorSpectrumRuntimeRenderMs", FormatMilliseconds(runtime.RenderMilliseconds));
        builder.Add("colorSpectrumRuntimeRenderZeroRectSkips", runtime.RenderSkippedZeroRectCount);
        builder.Add("colorSpectrumRuntimeTextureCacheHits", runtime.SpectrumTextureCacheHitCount);
        builder.Add("colorSpectrumRuntimeTextureCacheMisses", runtime.SpectrumTextureCacheMissCount);
        builder.Add("colorSpectrumRuntimeTextureBuilds", runtime.SpectrumTextureBuildCount);
        builder.Add("colorSpectrumRuntimeTextureBuildMs", FormatMilliseconds(runtime.SpectrumTextureBuildMilliseconds));

        builder.Add("colorSpectrumPointerDownCalls", telemetry.HandlePointerDownCallCount);
        builder.Add("colorSpectrumPointerDownMs", FormatMilliseconds(telemetry.HandlePointerDownMilliseconds));
        builder.Add("colorSpectrumPointerHits", telemetry.HandlePointerDownHitCount);
        builder.Add("colorSpectrumPointerMisses", telemetry.HandlePointerDownMissCount);
        builder.Add("colorSpectrumPointerMoveCalls", telemetry.HandlePointerMoveCallCount);
        builder.Add("colorSpectrumPointerMoveMs", FormatMilliseconds(telemetry.HandlePointerMoveMilliseconds));
        builder.Add("colorSpectrumPointerMoveDrag", telemetry.HandlePointerMoveDragCount);
        builder.Add("colorSpectrumPointerMoveIgnored", telemetry.HandlePointerMoveIgnoredCount);
        builder.Add("colorSpectrumPointerUpCalls", telemetry.HandlePointerUpCallCount);
        builder.Add("colorSpectrumPointerUpStateChanges", telemetry.HandlePointerUpStateChangeCount);
        builder.Add("colorSpectrumMouseOverCalls", telemetry.SetMouseOverCallCount);
        builder.Add("colorSpectrumMouseOverStateChanges", telemetry.SetMouseOverStateChangeCount);
        builder.Add("colorSpectrumUpdateFromPointerCalls", telemetry.UpdateFromPointerCallCount);
        builder.Add("colorSpectrumUpdateFromPointerMs", FormatMilliseconds(telemetry.UpdateFromPointerMilliseconds));
        builder.Add("colorSpectrumUpdateFromPointerZeroRectSkips", telemetry.UpdateFromPointerZeroRectSkipCount);
        builder.Add("colorSpectrumUpdateFromPointerHuePath", telemetry.UpdateFromPointerHuePathCount);
        builder.Add("colorSpectrumUpdateFromPointerAlphaPath", telemetry.UpdateFromPointerAlphaPathCount);
        builder.Add("colorSpectrumRequestSelectedColorSyncCalls", telemetry.RequestSelectedColorSyncCallCount);
        builder.Add("colorSpectrumRequestSelectedColorSyncDragDeferred", telemetry.RequestSelectedColorSyncDragDeferredCount);
        builder.Add("colorSpectrumQueueDeferredSelectedColorSyncCalls", telemetry.QueueDeferredSelectedColorSyncCallCount);
        builder.Add("colorSpectrumQueueDeferredSelectedColorSyncAlreadyQueued", telemetry.QueueDeferredSelectedColorSyncAlreadyQueuedCount);
        builder.Add("colorSpectrumFlushDeferredSelectedColorSyncCalls", telemetry.FlushDeferredSelectedColorSyncCallCount);
        builder.Add("colorSpectrumFlushDeferredSelectedColorSyncNoPending", telemetry.FlushDeferredSelectedColorSyncNoPendingCount);
        builder.Add("colorSpectrumFlushDeferredSelectedColorSyncRequeueWhileDragging", telemetry.FlushDeferredSelectedColorSyncRequeueWhileDraggingCount);
        builder.Add("colorSpectrumFlushPendingSelectedColorSyncAfterDragCalls", telemetry.FlushPendingSelectedColorSyncAfterDragCallCount);
        builder.Add("colorSpectrumFlushPendingSelectedColorSyncAfterDragNoPending", telemetry.FlushPendingSelectedColorSyncAfterDragNoPendingCount);
        builder.Add("colorSpectrumSyncSelectedColorCalls", telemetry.SyncSelectedColorFromComponentsCallCount);
        builder.Add("colorSpectrumSyncSelectedColorMs", FormatMilliseconds(telemetry.SyncSelectedColorFromComponentsMilliseconds));
        builder.Add("colorSpectrumSyncSelectedColorReentrantSkip", telemetry.SyncSelectedColorFromComponentsReentrantSkipCount);
        builder.Add("colorSpectrumSyncSelectedColorNoOp", telemetry.SyncSelectedColorFromComponentsNoOpCount);
        builder.Add("colorSpectrumSelectedColorChangedCalls", telemetry.SelectedColorChangedCallCount);
        builder.Add("colorSpectrumSelectedColorChangedExternalSync", telemetry.SelectedColorChangedExternalSyncCount);
        builder.Add("colorSpectrumSelectedColorChangedComponentWriteback", telemetry.SelectedColorChangedComponentWritebackCount);
        builder.Add("colorSpectrumSelectedColorChangedRaised", telemetry.SelectedColorChangedRaisedCount);
        builder.Add("colorSpectrumHueChangedCalls", telemetry.HueChangedCallCount);
        builder.Add("colorSpectrumSaturationChangedCalls", telemetry.SaturationChangedCallCount);
        builder.Add("colorSpectrumValueChangedCalls", telemetry.ValueChangedCallCount);
        builder.Add("colorSpectrumAlphaChangedCalls", telemetry.AlphaChangedCallCount);
        builder.Add("colorSpectrumRenderCalls", telemetry.RenderCallCount);
        builder.Add("colorSpectrumRenderMs", FormatMilliseconds(telemetry.RenderMilliseconds));
        builder.Add("colorSpectrumRenderZeroRectSkips", telemetry.RenderSkippedZeroRectCount);
        builder.Add("colorSpectrumTextureCacheHits", telemetry.SpectrumTextureCacheHitCount);
        builder.Add("colorSpectrumTextureCacheMisses", telemetry.SpectrumTextureCacheMissCount);
        builder.Add("colorSpectrumTextureBuilds", telemetry.SpectrumTextureBuildCount);
        builder.Add("colorSpectrumTextureBuildMs", FormatMilliseconds(telemetry.SpectrumTextureBuildMilliseconds));

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