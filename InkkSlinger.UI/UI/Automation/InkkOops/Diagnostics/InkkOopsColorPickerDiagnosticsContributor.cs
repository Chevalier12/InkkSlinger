namespace InkkSlinger;

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
        builder.Add("colorPickerDragging", runtime.IsDragging);
        builder.Add("colorPickerMouseOver", runtime.IsMouseOver);
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
        builder.Add("colorPickerRuntimeSyncSelectedColorCalls", runtime.SyncSelectedColorFromComponentsCallCount);
        builder.Add("colorPickerRuntimeSyncSelectedColorMs", FormatMilliseconds(runtime.SyncSelectedColorFromComponentsMilliseconds));
        builder.Add("colorPickerRuntimeSyncSelectedColorNoOp", runtime.SyncSelectedColorFromComponentsNoOpCount);
        builder.Add("colorPickerRuntimeSelectedColorChangedCalls", runtime.SelectedColorChangedCallCount);
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
        builder.Add("colorPickerSyncSelectedColorCalls", telemetry.SyncSelectedColorFromComponentsCallCount);
        builder.Add("colorPickerSyncSelectedColorMs", FormatMilliseconds(telemetry.SyncSelectedColorFromComponentsMilliseconds));
        builder.Add("colorPickerSyncSelectedColorNoOp", telemetry.SyncSelectedColorFromComponentsNoOpCount);
        builder.Add("colorPickerSelectedColorChangedCalls", telemetry.SelectedColorChangedCallCount);
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
}