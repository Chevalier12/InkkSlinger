namespace InkkSlinger;

public sealed class InkkOopsGridSplitterDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 45;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        _ = context;
        if (element is not GridSplitter splitter)
        {
            return;
        }

        var runtime = splitter.GetGridSplitterSnapshotForDiagnostics();
        var telemetry = GridSplitter.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("gridSplitterConfiguredDirection", runtime.ConfiguredResizeDirection);
        builder.Add("gridSplitterEffectiveDirection", runtime.EffectiveResizeDirection);
        builder.Add("gridSplitterBehavior", runtime.ResizeBehavior);
        builder.Add("gridSplitterEnabled", runtime.IsEnabled);
        builder.Add("gridSplitterHover", runtime.IsMouseOver);
        builder.Add("gridSplitterDragging", runtime.IsDragging);
        builder.Add("gridSplitterActiveGrid", runtime.HasActiveGrid);
        builder.Add("gridSplitterVisualParent", runtime.VisualParentType);
        builder.Add("gridSplitterSlot", $"{runtime.LayoutSlotX:0.##},{runtime.LayoutSlotY:0.##},{runtime.LayoutSlotWidth:0.##},{runtime.LayoutSlotHeight:0.##}");
        builder.Add("gridSplitterIncrements", $"drag={runtime.DragIncrement:0.##},key={runtime.KeyboardIncrement:0.##}");
        builder.Add("gridSplitterActivePair", $"{runtime.ActiveDefinitionIndexA},{runtime.ActiveDefinitionIndexB}");
        builder.Add("gridSplitterStartSizes", $"ptr={runtime.StartPointer:0.##},a={runtime.StartSizeA:0.##},b={runtime.StartSizeB:0.##}");
        builder.Add("gridSplitterLastDeltas", $"requested={runtime.LastRequestedDelta:0.##},snapped={runtime.LastSnappedDelta:0.##},applied={runtime.LastAppliedDelta:0.##}");
        builder.Add("gridSplitterRuntimePointer", $"down={runtime.PointerDownCallCount},move={runtime.PointerMoveCallCount},up={runtime.PointerUpCallCount}");
        builder.Add("gridSplitterRuntimeKeyDown", runtime.KeyDownCallCount);
        builder.Add("gridSplitterRuntimeKeyDownMs", FormatMilliseconds(runtime.KeyDownMilliseconds));
        builder.Add("gridSplitterRuntimeResize", $"calls={runtime.ApplyResizeCallCount},ms={FormatMilliseconds(runtime.ApplyResizeMilliseconds)},changed={runtime.ApplyResizeProducedChangeCount},noop={runtime.ApplyResizeNoOpCount},clamped={runtime.ApplyResizeDeltaClampedCount}");
        builder.Add("gridSplitterRuntimeResolveTargets", $"ok={runtime.TryResolveResizeTargetsSuccessCount},fail={runtime.TryResolveResizeTargetsFailureCount},small={runtime.TryResolveResizeTargetsRejectedInsufficientDefinitionsCount}");
        builder.Add("gridSplitterRuntimeResolveDirection", $"explicit={runtime.ResolveEffectiveResizeDirectionExplicitCount},autoCols={runtime.ResolveEffectiveResizeDirectionAutoColumnsCount},autoRows={runtime.ResolveEffectiveResizeDirectionAutoRowsCount}");
        builder.Add("gridSplitterRuntimeResolvePairs", $"calls={runtime.ResolveDefinitionPairCallCount},ok={runtime.ResolveDefinitionPairSuccessCount},bad={runtime.ResolveDefinitionPairInvalidPairCount}");
        builder.Add("gridSplitterRuntimeResolveSizes", $"cols={runtime.ResolveColumnSizeCallCount},rows={runtime.ResolveRowSizeCallCount}");
        builder.Add("gridSplitterRuntimeSnap", $"calls={runtime.SnapCallCount},changed={runtime.SnapRoundedChangeCount},same={runtime.SnapRoundedNoChangeCount}");
        builder.Add("gridSplitterPointer", $"down={telemetry.PointerDownCallCount},move={telemetry.PointerMoveCallCount},up={telemetry.PointerUpCallCount}");
        builder.Add("gridSplitterKeyDown", telemetry.KeyDownCallCount);
        builder.Add("gridSplitterKeyDownMs", FormatMilliseconds(telemetry.KeyDownMilliseconds));
        builder.Add("gridSplitterResize", $"calls={telemetry.ApplyResizeCallCount},ms={FormatMilliseconds(telemetry.ApplyResizeMilliseconds)},changed={telemetry.ApplyResizeProducedChangeCount},noop={telemetry.ApplyResizeNoOpCount},clamped={telemetry.ApplyResizeDeltaClampedCount},corrected={telemetry.ApplyResizeTotalCorrectionCount}");
        builder.Add("gridSplitterResolveTargets", $"ok={telemetry.TryResolveResizeTargetsSuccessCount},fail={telemetry.TryResolveResizeTargetsFailureCount},small={telemetry.TryResolveResizeTargetsRejectedInsufficientDefinitionsCount}");
        builder.Add("gridSplitterResolveDirection", $"explicit={telemetry.ResolveEffectiveResizeDirectionExplicitCount},autoCols={telemetry.ResolveEffectiveResizeDirectionAutoColumnsCount},autoRows={telemetry.ResolveEffectiveResizeDirectionAutoRowsCount}");
        builder.Add("gridSplitterResolvePairs", $"calls={telemetry.ResolveDefinitionPairCallCount},ok={telemetry.ResolveDefinitionPairSuccessCount},bad={telemetry.ResolveDefinitionPairInvalidPairCount}");
        builder.Add("gridSplitterResolveSizes", $"cols={telemetry.ResolveColumnSizeCallCount},rows={telemetry.ResolveRowSizeCallCount}");
        builder.Add("gridSplitterSnap", $"calls={telemetry.SnapCallCount},changed={telemetry.SnapRoundedChangeCount},same={telemetry.SnapRoundedNoChangeCount}");
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }
}
