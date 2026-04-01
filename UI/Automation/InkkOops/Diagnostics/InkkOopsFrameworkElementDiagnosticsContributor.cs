namespace InkkSlinger;

public sealed class InkkOopsFrameworkElementDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 10;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        _ = context;

        if (element is not FrameworkElement frameworkElement)
        {
            return;
        }

        var snapshot = frameworkElement.GetFrameworkElementSnapshotForDiagnostics();
        var invalidation = snapshot.Invalidation;
        var telemetry = FrameworkElement.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("slot", FormatRect(snapshot.Slot));
        builder.Add("desired", FormatSize(snapshot.DesiredSize));
        builder.Add("actual", FormatSize(snapshot.RenderSize));
        builder.Add("renderSize", FormatSize(snapshot.RenderSize));
        builder.Add("previousAvailable", FormatSize(snapshot.PreviousAvailableSize));
        builder.Add("measureCalls", snapshot.MeasureCallCount);
        builder.Add("measureWork", snapshot.MeasureWorkCount);
        builder.Add("arrangeCalls", snapshot.ArrangeCallCount);
        builder.Add("arrangeWork", snapshot.ArrangeWorkCount);
        builder.Add("measureValid", snapshot.IsMeasureValid);
        builder.Add("arrangeValid", snapshot.IsArrangeValid);
        builder.Add("measureMs", FormatMilliseconds(snapshot.MeasureMilliseconds));
        builder.Add("measureExclusiveMs", FormatMilliseconds(snapshot.MeasureExclusiveMilliseconds));
        builder.Add("arrangeMs", FormatMilliseconds(snapshot.ArrangeMilliseconds));
        builder.Add("frameworkMeasureMs", FormatMilliseconds(snapshot.MeasureMilliseconds));
        builder.Add("frameworkMeasureExclusiveMs", FormatMilliseconds(snapshot.MeasureExclusiveMilliseconds));
        builder.Add("frameworkArrangeMs", FormatMilliseconds(snapshot.ArrangeMilliseconds));
        builder.Add("measureInvalidations", frameworkElement.MeasureInvalidationCount);
        builder.Add("measureInvalidationDirect", invalidation.DirectMeasureInvalidationCount);
        builder.Add("measureInvalidationPropagated", invalidation.PropagatedMeasureInvalidationCount);
        builder.Add("measureInvalidationLast", invalidation.LastMeasureInvalidationSummary);
        builder.Add("measureInvalidationTopSources", invalidation.TopMeasureInvalidationSources);
        builder.Add("measureInvalidationLastLayoutFrame", invalidation.LastMeasureInvalidationLayoutFrame);
        builder.Add("measureInvalidationLastDrawFrame", invalidation.LastMeasureInvalidationDrawFrame);
        builder.Add("arrangeInvalidations", frameworkElement.ArrangeInvalidationCount);
        builder.Add("arrangeInvalidationDirect", invalidation.DirectArrangeInvalidationCount);
        builder.Add("arrangeInvalidationPropagated", invalidation.PropagatedArrangeInvalidationCount);
        builder.Add("arrangeInvalidationLast", invalidation.LastArrangeInvalidationSummary);
        builder.Add("arrangeInvalidationTopSources", invalidation.TopArrangeInvalidationSources);
        builder.Add("arrangeInvalidationLastLayoutFrame", invalidation.LastArrangeInvalidationLayoutFrame);
        builder.Add("arrangeInvalidationLastDrawFrame", invalidation.LastArrangeInvalidationDrawFrame);
        builder.Add("renderInvalidations", frameworkElement.RenderInvalidationCount);
        builder.Add("renderInvalidationDirect", invalidation.DirectRenderInvalidationCount);
        builder.Add("renderInvalidationPropagated", invalidation.PropagatedRenderInvalidationCount);
        builder.Add("renderInvalidationLast", invalidation.LastRenderInvalidationSummary);
        builder.Add("renderInvalidationTopSources", invalidation.TopRenderInvalidationSources);
        builder.Add("renderInvalidationLastLayoutFrame", invalidation.LastRenderInvalidationLayoutFrame);
        builder.Add("renderInvalidationLastDrawFrame", invalidation.LastRenderInvalidationDrawFrame);
        builder.Add("visible", snapshot.IsVisible);
        builder.Add("enabled", snapshot.IsEnabled);
        builder.Add("frameworkIsLoaded", snapshot.IsLoaded);
        builder.Add("frameworkUseLayoutRounding", snapshot.UseLayoutRounding);
        builder.Add("frameworkArrangeRect", FormatRect(snapshot.ArrangeRect));
        builder.Add("frameworkLastArrangedDesired", FormatSize(snapshot.LastArrangedDesiredSize));

        builder.Add("frameworkMeasureSkippedInvisible", snapshot.MeasureSkippedInvisibleCount);
        builder.Add("frameworkMeasureCachedReuse", snapshot.MeasureCachedReuseCount);
        builder.Add("frameworkMeasureReusableSizeReuse", snapshot.MeasureReusableSizeReuseCount);
        builder.Add("frameworkMeasureParentInvalidations", snapshot.MeasureParentInvalidationCount);
        builder.Add("frameworkMeasureInvalidatedDuringMeasure", snapshot.MeasureInvalidatedDuringMeasureCount);
        builder.Add("frameworkArrangeCachedReuse", snapshot.ArrangeCachedReuseCount);
        builder.Add("frameworkArrangeSkippedInvisible", snapshot.ArrangeSkippedInvisibleCount);
        builder.Add("frameworkArrangeRemeasure", snapshot.ArrangeRemeasureCount);
        builder.Add("frameworkArrangeParentInvalidations", snapshot.ArrangeParentInvalidationCount);
        builder.Add("frameworkArrangeInvalidatedDuringArrange", snapshot.ArrangeInvalidatedDuringArrangeCount);
        builder.Add("frameworkLayoutUpdatedRaises", snapshot.LayoutUpdatedRaiseCount);
        builder.Add("frameworkUpdateLayoutCalls", snapshot.UpdateLayoutCallCount);
        builder.Add("frameworkUpdateLayoutPasses", snapshot.UpdateLayoutPassCount);
        builder.Add("frameworkUpdateLayoutRecursiveChildren", snapshot.UpdateLayoutRecursiveChildCount);
        builder.Add("frameworkUpdateLayoutStableExits", snapshot.UpdateLayoutStableExitCount);
        builder.Add("frameworkUpdateLayoutMaxPassExits", snapshot.UpdateLayoutMaxPassExitCount);
        builder.Add("frameworkUpdateLayoutMeasureRepairs", snapshot.UpdateLayoutMeasureRepairCount);
        builder.Add("frameworkUpdateLayoutArrangeRepairs", snapshot.UpdateLayoutArrangeRepairCount);
        builder.Add("frameworkInvalidateMeasureCalls", snapshot.InvalidateMeasureCallCount);
        builder.Add("frameworkInvalidateArrangeCalls", snapshot.InvalidateArrangeCallCount);
        builder.Add("frameworkInvalidateVisualCalls", snapshot.InvalidateVisualCallCount);
        builder.Add("frameworkInvalidateArrangeDirectCalls", snapshot.InvalidateArrangeDirectLayoutOnlyCallCount);
        builder.Add("frameworkInvalidateArrangeDirectNoRender", snapshot.InvalidateArrangeDirectLayoutOnlyWithoutRenderCount);
        builder.Add("frameworkSetResourceReferenceCalls", snapshot.SetResourceReferenceCallCount);
        builder.Add("frameworkRefreshResourceBindingsCalls", snapshot.RefreshResourceBindingsCallCount);
        builder.Add("frameworkResourceBindingRefreshEntries", snapshot.ResourceBindingRefreshEntryCount);
        builder.Add("frameworkUpdateResourceBindingCalls", snapshot.UpdateResourceBindingCallCount);
        builder.Add("frameworkResourceBindingHits", snapshot.UpdateResourceBindingHitCount);
        builder.Add("frameworkResourceBindingMisses", snapshot.UpdateResourceBindingMissCount);
        builder.Add("frameworkLocalResourcesChanged", snapshot.LocalResourcesChangedCallCount);
        builder.Add("frameworkParentResourcesChanged", snapshot.ParentResourcesChangedCallCount);
        builder.Add("frameworkApplicationResourcesChanged", snapshot.ApplicationResourcesChangedCallCount);
        builder.Add("frameworkResourceScopeInvalidated", snapshot.ResourceScopeInvalidatedRaiseCount);
        builder.Add("frameworkResourceParentAttach", snapshot.ResourceParentAttachCount);
        builder.Add("frameworkResourceParentDetach", snapshot.ResourceParentDetachCount);
        builder.Add("frameworkDescendantResourceNotifications", snapshot.DescendantResourcesChangedNotifyCallCount);
        builder.Add("frameworkDescendantDirectRefresh", snapshot.DescendantDirectResourceRefreshCount);
        builder.Add("frameworkImplicitStyleUpdates", snapshot.ImplicitStyleUpdateCallCount);
        builder.Add("frameworkImplicitStyleSkipControl", snapshot.ImplicitStyleSkipControlTypeCount);
        builder.Add("frameworkImplicitStyleSkipPolicy", snapshot.ImplicitStyleSkipPolicyCount);
        builder.Add("frameworkImplicitStyleFound", snapshot.ImplicitStyleResourceFoundCount);
        builder.Add("frameworkImplicitStyleApplied", snapshot.ImplicitStyleAppliedCount);
        builder.Add("frameworkImplicitStyleCleared", snapshot.ImplicitStyleClearedCount);
        builder.Add("frameworkImplicitStyleNoChange", snapshot.ImplicitStyleNoChangeCount);
        builder.Add("frameworkVisualParentChanged", snapshot.VisualParentChangedCallCount);
        builder.Add("frameworkVisualParentScopeChanged", snapshot.VisualParentResourceScopeChangedCount);
        builder.Add("frameworkVisualParentTriggeredUnload", snapshot.VisualParentTriggeredUnloadCount);
        builder.Add("frameworkVisualParentTriggeredLoad", snapshot.VisualParentTriggeredLoadCount);
        builder.Add("frameworkLogicalParentChanged", snapshot.LogicalParentChangedCallCount);
        builder.Add("frameworkLogicalParentSkippedForVisualParent", snapshot.LogicalParentSkippedDueToVisualParentCount);
        builder.Add("frameworkLogicalParentScopeChanged", snapshot.LogicalParentResourceScopeChangedCount);
        builder.Add("frameworkRaiseInitialized", snapshot.RaiseInitializedCallCount);
        builder.Add("frameworkRaiseLoaded", snapshot.RaiseLoadedCallCount);
        builder.Add("frameworkRaiseLoadedNoOp", snapshot.RaiseLoadedNoOpCount);
        builder.Add("frameworkRaiseUnloaded", snapshot.RaiseUnloadedCallCount);
        builder.Add("frameworkRaiseUnloadedNoOp", snapshot.RaiseUnloadedNoOpCount);
        builder.Add("frameworkDependencyPropertyChanged", snapshot.DependencyPropertyChangedCallCount);
        builder.Add("frameworkVisibilityPropertyChanged", snapshot.VisibilityPropertyChangedCount);
        builder.Add("frameworkStylePropertyChanged", snapshot.StylePropertyChangedCount);
        builder.Add("frameworkStyleDetach", snapshot.StyleDetachCount);
        builder.Add("frameworkStyleApply", snapshot.StyleApplyCount);

        builder.Add("frameworkGlobalMeasureCalls", telemetry.MeasureCallCount);
        builder.Add("frameworkGlobalMeasureMs", FormatMilliseconds(telemetry.MeasureMilliseconds));
        builder.Add("frameworkGlobalMeasureWork", telemetry.MeasureWorkCount);
        builder.Add("frameworkGlobalMeasureExclusiveMs", FormatMilliseconds(telemetry.MeasureExclusiveMilliseconds));
        builder.Add("frameworkGlobalArrangeCalls", telemetry.ArrangeCallCount);
        builder.Add("frameworkGlobalArrangeMs", FormatMilliseconds(telemetry.ArrangeMilliseconds));
        builder.Add("frameworkGlobalArrangeWork", telemetry.ArrangeWorkCount);
        builder.Add("frameworkGlobalUpdateLayoutCalls", telemetry.UpdateLayoutCallCount);
        builder.Add("frameworkGlobalResourceBindingHits", telemetry.UpdateResourceBindingHitCount);
        builder.Add("frameworkGlobalResourceBindingMisses", telemetry.UpdateResourceBindingMissCount);
        builder.Add("frameworkGlobalImplicitStyleUpdates", telemetry.ImplicitStyleUpdateCallCount);
        builder.Add("frameworkGlobalRaiseLoaded", telemetry.RaiseLoadedCallCount);
        builder.Add("frameworkGlobalRaiseUnloaded", telemetry.RaiseUnloadedCallCount);
        builder.Add("frameworkGlobalDependencyPropertyChanged", telemetry.DependencyPropertyChangedCallCount);
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}";
    }

    private static string FormatSize(Microsoft.Xna.Framework.Vector2 size)
    {
        return $"{size.X:0.##},{size.Y:0.##}";
    }
}
