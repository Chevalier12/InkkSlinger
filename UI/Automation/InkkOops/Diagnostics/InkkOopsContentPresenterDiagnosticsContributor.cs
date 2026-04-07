namespace InkkSlinger;

public sealed class InkkOopsContentPresenterDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 40;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        _ = context;

        if (element is not ContentPresenter target)
        {
            return;
        }

        var runtime = target.GetContentPresenterSnapshotForDiagnostics();
        var telemetry = ContentPresenter.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("contentPresenterHasPresentedElement", runtime.HasPresentedElement);
        builder.Add("contentPresenterPresentedElementType", runtime.PresentedElementType);
        builder.Add("contentPresenterHasSourceOwner", runtime.HasSourceOwner);
        builder.Add("contentPresenterSourceOwnerType", runtime.SourceOwnerType);
        builder.Add("contentPresenterContentSource", runtime.ContentSource);
        builder.Add("contentPresenterHasEffectivePresentationState", runtime.HasEffectivePresentationState);
        builder.Add("contentPresenterHasLocalContent", runtime.HasLocalContent);
        builder.Add("contentPresenterHasLocalContentTemplate", runtime.HasLocalContentTemplate);
        builder.Add("contentPresenterHasLocalContentTemplateSelector", runtime.HasLocalContentTemplateSelector);
        builder.Add("contentPresenterLayoutSlot", $"{runtime.LayoutSlotWidth:0.##},{runtime.LayoutSlotHeight:0.##}");

        builder.Add("contentPresenterRuntimeRefreshSourceBindingCalls", runtime.RefreshSourceBindingCallCount);
        builder.Add("contentPresenterRuntimeEnsureSourceBindingCalls", runtime.EnsureSourceBindingCallCount);
        builder.Add("contentPresenterRuntimeEnsureSourceBindingMs", FormatMilliseconds(runtime.EnsureSourceBindingMilliseconds));
        builder.Add("contentPresenterRuntimeEnsureSourceBindingOwnerUnchanged", runtime.EnsureSourceBindingOwnerUnchangedCount);
        builder.Add("contentPresenterRuntimeEnsureSourceBindingDetachedOwner", runtime.EnsureSourceBindingDetachedOwnerCount);
        builder.Add("contentPresenterRuntimeEnsureSourceBindingAttachedOwner", runtime.EnsureSourceBindingAttachedOwnerCount);
        builder.Add("contentPresenterRuntimeEnsureSourceBindingAttachedContentControl", runtime.EnsureSourceBindingAttachedContentControlCount);
        builder.Add("contentPresenterRuntimeSourceOwnerChangedCalls", runtime.OnSourceOwnerPropertyChangedCallCount);
        builder.Add("contentPresenterRuntimeSourceOwnerChangedMs", FormatMilliseconds(runtime.OnSourceOwnerPropertyChangedMilliseconds));
        builder.Add("contentPresenterRuntimeSourceOwnerChangedIrrelevant", runtime.OnSourceOwnerPropertyChangedIrrelevantPropertyCount);
        builder.Add("contentPresenterRuntimeSourceOwnerChangedRebuilt", runtime.OnSourceOwnerPropertyChangedRebuiltPresentedElementCount);
        builder.Add("contentPresenterRuntimeSourceOwnerChangedFallbackRefresh", runtime.OnSourceOwnerPropertyChangedRefreshedFallbackTextCount);
        builder.Add("contentPresenterRuntimeSourceOwnerChangedInvalidateArrange", runtime.OnSourceOwnerPropertyChangedInvalidatedArrangeCount);
        builder.Add("contentPresenterRuntimeRefreshPresentedElementCalls", runtime.RefreshPresentedElementCallCount);
        builder.Add("contentPresenterRuntimeRefreshPresentedElementMs", FormatMilliseconds(runtime.RefreshPresentedElementMilliseconds));
        builder.Add("contentPresenterRuntimeRefreshPresentedElementStateCacheHits", runtime.RefreshPresentedElementStateCacheHitCount);
        builder.Add("contentPresenterRuntimeRefreshPresentedElementSelectedTemplate", runtime.RefreshPresentedElementSelectedTemplateCount);
        builder.Add("contentPresenterRuntimeRefreshPresentedElementBuiltSameInstance", runtime.RefreshPresentedElementBuiltSameInstanceCount);
        builder.Add("contentPresenterRuntimeRefreshPresentedElementDetachedOld", runtime.RefreshPresentedElementDetachedOldElementCount);
        builder.Add("contentPresenterRuntimeRefreshPresentedElementAttachedNew", runtime.RefreshPresentedElementAttachedNewElementCount);
        builder.Add("contentPresenterRuntimeRefreshPresentedElementChanged", runtime.RefreshPresentedElementChangedCount);
        builder.Add("contentPresenterRuntimeBuildContentElementCalls", runtime.BuildContentElementCallCount);
        builder.Add("contentPresenterRuntimeBuildContentElementMs", FormatMilliseconds(runtime.BuildContentElementMilliseconds));
        builder.Add("contentPresenterRuntimeBuildContentElementUiElementPath", runtime.BuildContentElementUiElementPathCount);
        builder.Add("contentPresenterRuntimeBuildContentElementTemplatePath", runtime.BuildContentElementTemplatePathCount);
        builder.Add("contentPresenterRuntimeBuildContentElementAccessTextPath", runtime.BuildContentElementAccessTextPathCount);
        builder.Add("contentPresenterRuntimeBuildContentElementLabelPath", runtime.BuildContentElementLabelPathCount);
        builder.Add("contentPresenterRuntimeBuildContentElementNullPath", runtime.BuildContentElementNullPathCount);
        builder.Add("contentPresenterRuntimeBuildContentElementCycleGuard", runtime.BuildContentElementCycleGuardCount);
        builder.Add("contentPresenterRuntimeFallbackStylingCalls", runtime.TryRefreshFallbackTextStylingCallCount);
        builder.Add("contentPresenterRuntimeFallbackStylingNoContent", runtime.TryRefreshFallbackTextStylingNoContentCount);
        builder.Add("contentPresenterRuntimeFallbackStylingUiElementPath", runtime.TryRefreshFallbackTextStylingUiElementPathCount);
        builder.Add("contentPresenterRuntimeFallbackStylingTemplatePath", runtime.TryRefreshFallbackTextStylingTemplatePathCount);
        builder.Add("contentPresenterRuntimeFallbackStylingLabelPath", runtime.TryRefreshFallbackTextStylingLabelPathCount);
        builder.Add("contentPresenterRuntimeFallbackStylingTextBlockPath", runtime.TryRefreshFallbackTextStylingTextBlockPathCount);
        builder.Add("contentPresenterRuntimeFallbackStylingNoMatch", runtime.TryRefreshFallbackTextStylingNoMatchCount);
        builder.Add("contentPresenterRuntimePresentationCycleChecks", runtime.WouldCreatePresentationCycleCallCount);
        builder.Add("contentPresenterRuntimePresentationCycleReuse", runtime.WouldCreatePresentationCycleCurrentPresentedReuseCount);
        builder.Add("contentPresenterRuntimePresentationCycleSelf", runtime.WouldCreatePresentationCycleSelfCount);
        builder.Add("contentPresenterRuntimePresentationCycleAncestor", runtime.WouldCreatePresentationCycleAncestorMatchCount);
        builder.Add("contentPresenterRuntimePresentationCycleDescendant", runtime.WouldCreatePresentationCycleDescendantMatchCount);
        builder.Add("contentPresenterRuntimePresentationCycleFalse", runtime.WouldCreatePresentationCycleFalseCount);
        builder.Add("contentPresenterRuntimeFindSourceOwnerCalls", runtime.FindSourceOwnerCallCount);
        builder.Add("contentPresenterRuntimeFindSourceOwnerMs", FormatMilliseconds(runtime.FindSourceOwnerMilliseconds));
        builder.Add("contentPresenterRuntimeFindSourceOwnerAncestorProbe", runtime.FindSourceOwnerAncestorProbeCount);
        builder.Add("contentPresenterRuntimeFindSourceOwnerPropertyMatch", runtime.FindSourceOwnerPropertyMatchCount);
        builder.Add("contentPresenterRuntimeFindSourceOwnerSelfOwnerSkip", runtime.FindSourceOwnerSelfOwnerSkipCount);
        builder.Add("contentPresenterRuntimeFindSourceOwnerGetterFailure", runtime.FindSourceOwnerGetterFailureCount);
        builder.Add("contentPresenterRuntimeFindSourceOwnerFound", runtime.FindSourceOwnerFoundCount);
        builder.Add("contentPresenterRuntimeFindSourceOwnerNotFound", runtime.FindSourceOwnerNotFoundCount);
        builder.Add("contentPresenterRuntimeMeasureOverrideCalls", runtime.MeasureOverrideCallCount);
        builder.Add("contentPresenterRuntimeMeasureOverrideMs", FormatMilliseconds(runtime.MeasureOverrideMilliseconds));
        builder.Add("contentPresenterRuntimeMeasureOverridePresentedElementPath", runtime.MeasureOverridePresentedElementPathCount);
        builder.Add("contentPresenterRuntimeMeasureOverrideNoPresentedElement", runtime.MeasureOverrideNoPresentedElementCount);
        builder.Add("contentPresenterRuntimeArrangeOverrideCalls", runtime.ArrangeOverrideCallCount);
        builder.Add("contentPresenterRuntimeArrangeOverrideMs", FormatMilliseconds(runtime.ArrangeOverrideMilliseconds));
        builder.Add("contentPresenterRuntimeArrangeOverridePresentedElementPath", runtime.ArrangeOverridePresentedElementPathCount);
        builder.Add("contentPresenterRuntimeArrangeOverrideNoPresentedElement", runtime.ArrangeOverrideNoPresentedElementCount);

        builder.Add("contentPresenterVisualChildrenCalls", telemetry.GetVisualChildrenCallCount);
        builder.Add("contentPresenterVisualChildrenYielded", telemetry.GetVisualChildrenYieldedChildCount);
        builder.Add("contentPresenterTraversalCountCalls", telemetry.GetVisualChildCountForTraversalCallCount);
        builder.Add("contentPresenterTraversalIndexCalls", telemetry.GetVisualChildAtForTraversalCallCount);
        builder.Add("contentPresenterTraversalOutOfRange", telemetry.GetVisualChildAtForTraversalOutOfRangeCount);
        builder.Add("contentPresenterLogicalChildrenCalls", telemetry.GetLogicalChildrenCallCount);
        builder.Add("contentPresenterLogicalChildrenYielded", telemetry.GetLogicalChildrenYieldedChildCount);
        builder.Add("contentPresenterNotifyOwnerContentChangedCalls", telemetry.NotifyOwnerContentChangedCallCount);
        builder.Add("contentPresenterNotifyOwnerContentChangedInvalidatedMeasure", telemetry.NotifyOwnerContentChangedInvalidatedMeasureCount);
        builder.Add("contentPresenterDependencyPropertyChangedCalls", telemetry.DependencyPropertyChangedCallCount);
        builder.Add("contentPresenterDependencyPropertyChangedRelevant", telemetry.DependencyPropertyChangedRelevantPropertyCount);
        builder.Add("contentPresenterVisualParentChangedCalls", telemetry.VisualParentChangedCallCount);
        builder.Add("contentPresenterLogicalParentChangedCalls", telemetry.LogicalParentChangedCallCount);
        builder.Add("contentPresenterMeasureOverrideCalls", telemetry.MeasureOverrideCallCount);
        builder.Add("contentPresenterMeasureOverrideMs", FormatMilliseconds(telemetry.MeasureOverrideMilliseconds));
        builder.Add("contentPresenterMeasureOverridePresentedElementPath", telemetry.MeasureOverridePresentedElementPathCount);
        builder.Add("contentPresenterMeasureOverrideNoPresentedElement", telemetry.MeasureOverrideNoPresentedElementCount);
        builder.Add("contentPresenterCanReuseMeasureCalls", telemetry.CanReuseMeasureCallCount);
        builder.Add("contentPresenterCanReuseMeasureNoPresentedElement", telemetry.CanReuseMeasureNoPresentedElementCount);
        builder.Add("contentPresenterCanReuseMeasureDelegated", telemetry.CanReuseMeasureDelegatedCount);
        builder.Add("contentPresenterArrangeOverrideCalls", telemetry.ArrangeOverrideCallCount);
        builder.Add("contentPresenterArrangeOverrideMs", FormatMilliseconds(telemetry.ArrangeOverrideMilliseconds));
        builder.Add("contentPresenterArrangeOverridePresentedElementPath", telemetry.ArrangeOverridePresentedElementPathCount);
        builder.Add("contentPresenterArrangeOverrideNoPresentedElement", telemetry.ArrangeOverrideNoPresentedElementCount);
        builder.Add("contentPresenterRefreshSourceBindingCalls", telemetry.RefreshSourceBindingCallCount);
        builder.Add("contentPresenterEnsureSourceBindingCalls", telemetry.EnsureSourceBindingCallCount);
        builder.Add("contentPresenterEnsureSourceBindingMs", FormatMilliseconds(telemetry.EnsureSourceBindingMilliseconds));
        builder.Add("contentPresenterEnsureSourceBindingOwnerUnchanged", telemetry.EnsureSourceBindingOwnerUnchangedCount);
        builder.Add("contentPresenterEnsureSourceBindingDetachedOwner", telemetry.EnsureSourceBindingDetachedOwnerCount);
        builder.Add("contentPresenterEnsureSourceBindingAttachedOwner", telemetry.EnsureSourceBindingAttachedOwnerCount);
        builder.Add("contentPresenterEnsureSourceBindingAttachedContentControl", telemetry.EnsureSourceBindingAttachedContentControlCount);
        builder.Add("contentPresenterSourceOwnerChangedCalls", telemetry.OnSourceOwnerPropertyChangedCallCount);
        builder.Add("contentPresenterSourceOwnerChangedMs", FormatMilliseconds(telemetry.OnSourceOwnerPropertyChangedMilliseconds));
        builder.Add("contentPresenterSourceOwnerChangedIrrelevant", telemetry.OnSourceOwnerPropertyChangedIrrelevantPropertyCount);
        builder.Add("contentPresenterSourceOwnerChangedRebuilt", telemetry.OnSourceOwnerPropertyChangedRebuiltPresentedElementCount);
        builder.Add("contentPresenterSourceOwnerChangedFallbackRefresh", telemetry.OnSourceOwnerPropertyChangedRefreshedFallbackTextCount);
        builder.Add("contentPresenterSourceOwnerChangedInvalidateArrange", telemetry.OnSourceOwnerPropertyChangedInvalidatedArrangeCount);
        builder.Add("contentPresenterRefreshPresentedElementCalls", telemetry.RefreshPresentedElementCallCount);
        builder.Add("contentPresenterRefreshPresentedElementMs", FormatMilliseconds(telemetry.RefreshPresentedElementMilliseconds));
        builder.Add("contentPresenterRefreshPresentedElementStateCacheHits", telemetry.RefreshPresentedElementStateCacheHitCount);
        builder.Add("contentPresenterRefreshPresentedElementSelectedTemplate", telemetry.RefreshPresentedElementSelectedTemplateCount);
        builder.Add("contentPresenterRefreshPresentedElementBuiltSameInstance", telemetry.RefreshPresentedElementBuiltSameInstanceCount);
        builder.Add("contentPresenterRefreshPresentedElementDetachedOld", telemetry.RefreshPresentedElementDetachedOldElementCount);
        builder.Add("contentPresenterRefreshPresentedElementAttachedNew", telemetry.RefreshPresentedElementAttachedNewElementCount);
        builder.Add("contentPresenterRefreshPresentedElementChanged", telemetry.RefreshPresentedElementChangedCount);
        builder.Add("contentPresenterBuildContentElementCalls", telemetry.BuildContentElementCallCount);
        builder.Add("contentPresenterBuildContentElementMs", FormatMilliseconds(telemetry.BuildContentElementMilliseconds));
        builder.Add("contentPresenterBuildContentElementUiElementPath", telemetry.BuildContentElementUiElementPathCount);
        builder.Add("contentPresenterBuildContentElementTemplatePath", telemetry.BuildContentElementTemplatePathCount);
        builder.Add("contentPresenterBuildContentElementAccessTextPath", telemetry.BuildContentElementAccessTextPathCount);
        builder.Add("contentPresenterBuildContentElementLabelPath", telemetry.BuildContentElementLabelPathCount);
        builder.Add("contentPresenterBuildContentElementNullPath", telemetry.BuildContentElementNullPathCount);
        builder.Add("contentPresenterBuildContentElementCycleGuard", telemetry.BuildContentElementCycleGuardCount);
        builder.Add("contentPresenterResolveAccessKeyTargetCalls", telemetry.ResolveAccessKeyTargetCallCount);
        builder.Add("contentPresenterResolveAccessKeyTargetLabelPath", telemetry.ResolveAccessKeyTargetLabelPathCount);
        builder.Add("contentPresenterResolveAccessKeyTargetRecognizesAccessKeyPath", telemetry.ResolveAccessKeyTargetRecognizesAccessKeyPathCount);
        builder.Add("contentPresenterResolveAccessKeyTargetNoTarget", telemetry.ResolveAccessKeyTargetNoTargetCount);
        builder.Add("contentPresenterFallbackStylingCalls", telemetry.TryRefreshFallbackTextStylingCallCount);
        builder.Add("contentPresenterFallbackStylingNoContent", telemetry.TryRefreshFallbackTextStylingNoContentCount);
        builder.Add("contentPresenterFallbackStylingUiElementPath", telemetry.TryRefreshFallbackTextStylingUiElementPathCount);
        builder.Add("contentPresenterFallbackStylingTemplatePath", telemetry.TryRefreshFallbackTextStylingTemplatePathCount);
        builder.Add("contentPresenterFallbackStylingLabelPath", telemetry.TryRefreshFallbackTextStylingLabelPathCount);
        builder.Add("contentPresenterFallbackStylingTextBlockPath", telemetry.TryRefreshFallbackTextStylingTextBlockPathCount);
        builder.Add("contentPresenterFallbackStylingNoMatch", telemetry.TryRefreshFallbackTextStylingNoMatchCount);
        builder.Add("contentPresenterPresentationCycleChecks", telemetry.WouldCreatePresentationCycleCallCount);
        builder.Add("contentPresenterPresentationCycleReuse", telemetry.WouldCreatePresentationCycleCurrentPresentedReuseCount);
        builder.Add("contentPresenterPresentationCycleSelf", telemetry.WouldCreatePresentationCycleSelfCount);
        builder.Add("contentPresenterPresentationCycleAncestor", telemetry.WouldCreatePresentationCycleAncestorMatchCount);
        builder.Add("contentPresenterPresentationCycleDescendant", telemetry.WouldCreatePresentationCycleDescendantMatchCount);
        builder.Add("contentPresenterPresentationCycleFalse", telemetry.WouldCreatePresentationCycleFalseCount);
        builder.Add("contentPresenterFindSourceOwnerCalls", telemetry.FindSourceOwnerCallCount);
        builder.Add("contentPresenterFindSourceOwnerMs", FormatMilliseconds(telemetry.FindSourceOwnerMilliseconds));
        builder.Add("contentPresenterFindSourceOwnerAncestorProbe", telemetry.FindSourceOwnerAncestorProbeCount);
        builder.Add("contentPresenterFindSourceOwnerPropertyMatch", telemetry.FindSourceOwnerPropertyMatchCount);
        builder.Add("contentPresenterFindSourceOwnerSelfOwnerSkip", telemetry.FindSourceOwnerSelfOwnerSkipCount);
        builder.Add("contentPresenterFindSourceOwnerGetterFailure", telemetry.FindSourceOwnerGetterFailureCount);
        builder.Add("contentPresenterFindSourceOwnerFound", telemetry.FindSourceOwnerFoundCount);
        builder.Add("contentPresenterFindSourceOwnerNotFound", telemetry.FindSourceOwnerNotFoundCount);
        builder.Add("contentPresenterFindReadablePropertyCalls", telemetry.FindReadablePropertyCallCount);
        builder.Add("contentPresenterFindReadablePropertyMs", FormatMilliseconds(telemetry.FindReadablePropertyMilliseconds));
        builder.Add("contentPresenterFindReadablePropertyMatched", telemetry.FindReadablePropertyMatchedCount);
        builder.Add("contentPresenterFindReadablePropertyNotFound", telemetry.FindReadablePropertyNotFoundCount);
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }
}
