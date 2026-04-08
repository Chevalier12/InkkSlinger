namespace InkkSlinger;

public sealed class InkkOopsControlDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 30;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        _ = context;

        if (element is not Control target)
        {
            return;
        }

        var runtime = target.GetControlSnapshotForDiagnostics();
        var telemetry = Control.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("controlHasTemplateAssigned", runtime.HasTemplateAssigned);
        builder.Add("controlHasTemplateRoot", runtime.HasTemplateRoot);
        builder.Add("controlTemplateRootType", runtime.TemplateRootType);
        builder.Add("controlHasSubscribedCommand", runtime.HasSubscribedCommand);
        builder.Add("controlIsCommandDisablingIsEnabled", runtime.IsCommandDisablingIsEnabled);
        builder.Add("controlHasStoredIsEnabledLocalValue", runtime.HasStoredIsEnabledLocalValue);
        builder.Add("controlTrackedStyleResourceAncestors", runtime.TrackedStyleResourceAncestorCount);
        builder.Add("controlLayoutSlot", $"{runtime.LayoutSlotWidth:0.##},{runtime.LayoutSlotHeight:0.##}");

        builder.Add("controlRuntimeVisualChildrenCalls", runtime.GetVisualChildrenCallCount);
        builder.Add("controlRuntimeVisualChildrenYieldedTemplateRoot", runtime.GetVisualChildrenYieldedTemplateRootCount);
        builder.Add("controlRuntimeVisualChildrenWithoutTemplateRoot", runtime.GetVisualChildrenWithoutTemplateRootCount);
        builder.Add("controlRuntimeTraversalCountCalls", runtime.GetVisualChildCountForTraversalCallCount);
        builder.Add("controlRuntimeTraversalCountWithTemplateRoot", runtime.GetVisualChildCountForTraversalWithTemplateRootCount);
        builder.Add("controlRuntimeTraversalCountWithoutTemplateRoot", runtime.GetVisualChildCountForTraversalWithoutTemplateRootCount);
        builder.Add("controlRuntimeTraversalIndexCalls", runtime.GetVisualChildAtForTraversalCallCount);
        builder.Add("controlRuntimeTraversalIndexTemplateRootPath", runtime.GetVisualChildAtForTraversalTemplateRootPathCount);
        builder.Add("controlRuntimeTraversalOutOfRange", runtime.GetVisualChildAtForTraversalOutOfRangeCount);
        builder.Add("controlRuntimeApplyTemplateCalls", runtime.ApplyTemplateCallCount);
        builder.Add("controlRuntimeApplyTemplateMs", FormatMilliseconds(runtime.ApplyTemplateMilliseconds));
        builder.Add("controlRuntimeApplyTemplateNull", runtime.ApplyTemplateTemplateNullCount);
        builder.Add("controlRuntimeApplyTemplateTargetMismatch", runtime.ApplyTemplateTargetTypeMismatchCount);
        builder.Add("controlRuntimeApplyTemplateBuildReturnedNull", runtime.ApplyTemplateBuildReturnedNullCount);
        builder.Add("controlRuntimeApplyTemplateSetTemplateTree", runtime.ApplyTemplateSetTemplateTreeCount);
        builder.Add("controlRuntimeApplyTemplateBindingsApplied", runtime.ApplyTemplateBindingsAppliedCount);
        builder.Add("controlRuntimeApplyTemplateTriggersApplied", runtime.ApplyTemplateTriggersAppliedCount);
        builder.Add("controlRuntimeApplyTemplateValidation", runtime.ApplyTemplateValidationCount);
        builder.Add("controlRuntimeApplyTemplateOnApplyTemplate", runtime.ApplyTemplateOnApplyTemplateCount);
        builder.Add("controlRuntimeApplyTemplateReturnedTrue", runtime.ApplyTemplateReturnedTrueCount);
        builder.Add("controlRuntimeApplyTemplateReturnedFalse", runtime.ApplyTemplateReturnedFalseCount);
        builder.Add("controlRuntimeMeasureOverrideCalls", runtime.MeasureOverrideCallCount);
        builder.Add("controlRuntimeMeasureOverrideMs", FormatMilliseconds(runtime.MeasureOverrideMilliseconds));
        builder.Add("controlRuntimeMeasureOverrideImplicitStyleUpdate", runtime.MeasureOverrideImplicitStyleUpdateCount);
        builder.Add("controlRuntimeMeasureOverrideTemplateApplyAttempt", runtime.MeasureOverrideTemplateApplyAttemptCount);
        builder.Add("controlRuntimeMeasureOverrideTemplateRootMeasure", runtime.MeasureOverrideTemplateRootMeasureCount);
        builder.Add("controlRuntimeMeasureOverrideReturnedZero", runtime.MeasureOverrideReturnedZeroCount);
        builder.Add("controlRuntimeCanReuseMeasureCalls", runtime.CanReuseMeasureCallCount);
        builder.Add("controlRuntimeCanReuseMeasureTemplateRootDelegated", runtime.CanReuseMeasureTemplateRootDelegatedCount);
        builder.Add("controlRuntimeCanReuseMeasureNoTemplateRootRejected", runtime.CanReuseMeasureNoTemplateRootRejectedCount);
        builder.Add("controlRuntimeArrangeOverrideCalls", runtime.ArrangeOverrideCallCount);
        builder.Add("controlRuntimeArrangeOverrideMs", FormatMilliseconds(runtime.ArrangeOverrideMilliseconds));
        builder.Add("controlRuntimeArrangeOverrideTemplateRootArrange", runtime.ArrangeOverrideTemplateRootArrangeCount);
        builder.Add("controlRuntimeArrangeOverrideNoTemplateRoot", runtime.ArrangeOverrideNoTemplateRootCount);
        builder.Add("controlRuntimeDependencyPropertyChangedCalls", runtime.DependencyPropertyChangedCallCount);
        builder.Add("controlRuntimeDependencyPropertyChangedMs", FormatMilliseconds(runtime.DependencyPropertyChangedMilliseconds));
        builder.Add("controlRuntimeDependencyPropertyChangedStyle", runtime.DependencyPropertyChangedStylePropertyCount);
        builder.Add("controlRuntimeDependencyPropertyChangedTemplate", runtime.DependencyPropertyChangedTemplatePropertyCount);
        builder.Add("controlRuntimeDependencyPropertyChangedCommand", runtime.DependencyPropertyChangedCommandPropertyCount);
        builder.Add("controlRuntimeDependencyPropertyChangedCommandState", runtime.DependencyPropertyChangedCommandStatePropertyCount);
        builder.Add("controlRuntimeDependencyPropertyChangedIsEnabled", runtime.DependencyPropertyChangedIsEnabledPropertyCount);
        builder.Add("controlRuntimeDependencyPropertyChangedOther", runtime.DependencyPropertyChangedOtherPropertyCount);
        builder.Add("controlRuntimeVisualParentChangedCalls", runtime.VisualParentChangedCallCount);
        builder.Add("controlRuntimeVisualParentChangedMs", FormatMilliseconds(runtime.VisualParentChangedMilliseconds));
        builder.Add("controlRuntimeVisualParentTrackedImplicitStyleScopes", runtime.VisualParentChangedTrackedImplicitStyleScopesCount);
        builder.Add("controlRuntimeVisualParentClearedImplicitStyleScopes", runtime.VisualParentChangedClearedImplicitStyleScopesCount);
        builder.Add("controlRuntimeLogicalParentChangedCalls", runtime.LogicalParentChangedCallCount);
        builder.Add("controlRuntimeLogicalParentChangedMs", FormatMilliseconds(runtime.LogicalParentChangedMilliseconds));
        builder.Add("controlRuntimeLogicalParentSkippedForVisualParent", runtime.LogicalParentChangedSkippedForVisualParentCount);
        builder.Add("controlRuntimeLogicalParentTrackedImplicitStyleScopes", runtime.LogicalParentChangedTrackedImplicitStyleScopesCount);
        builder.Add("controlRuntimeLogicalParentClearedImplicitStyleScopes", runtime.LogicalParentChangedClearedImplicitStyleScopesCount);
        builder.Add("controlRuntimeResourceScopeChangedCalls", runtime.ResourceScopeChangedCallCount);
        builder.Add("controlRuntimeResourceScopeChangedApplicationSkip", runtime.ResourceScopeChangedApplicationSkipCount);
        builder.Add("controlRuntimeUpdateImplicitStyleCalls", runtime.UpdateImplicitStyleCallCount);
        builder.Add("controlRuntimeUpdateImplicitStyleApplied", runtime.UpdateImplicitStyleAppliedCount);
        builder.Add("controlRuntimeUpdateImplicitStyleCleared", runtime.UpdateImplicitStyleClearedCount);
        builder.Add("controlRuntimeUpdateImplicitStyleNoChange", runtime.UpdateImplicitStyleNoChangeCount);
        builder.Add("controlRuntimeUpdateImplicitStyleSkipped", runtime.UpdateImplicitStyleSkippedCount);
        builder.Add("controlRuntimeRefreshCommandSubscriptionsCalls", runtime.RefreshCommandSubscriptionsCallCount);
        builder.Add("controlRuntimeRefreshCommandSubscriptionsDetachedOld", runtime.RefreshCommandSubscriptionsDetachedOldCommandCount);
        builder.Add("controlRuntimeRefreshCommandSubscriptionsAttachedNew", runtime.RefreshCommandSubscriptionsAttachedNewCommandCount);
        builder.Add("controlRuntimeUpdateCommandEnabledStateCalls", runtime.UpdateCommandEnabledStateCallCount);
        builder.Add("controlRuntimeUpdateCommandEnabledStateNoCommandRestore", runtime.UpdateCommandEnabledStateNoCommandRestoreCount);
        builder.Add("controlRuntimeUpdateCommandEnabledStateCanExecuteRestore", runtime.UpdateCommandEnabledStateCanExecuteRestoreCount);
        builder.Add("controlRuntimeUpdateCommandEnabledStateDisableCommand", runtime.UpdateCommandEnabledStateDisableCommandCount);
        builder.Add("controlRuntimeUpdateCommandEnabledStateForceLocalDisable", runtime.UpdateCommandEnabledStateForceLocalDisableCount);
        builder.Add("controlRuntimeRestoreIsEnabledCalls", runtime.RestoreIsEnabledIfCommandDisabledItCallCount);
        builder.Add("controlRuntimeRestoreIsEnabledNoOp", runtime.RestoreIsEnabledIfCommandDisabledItNoOpCount);
        builder.Add("controlRuntimeRestoreIsEnabledClearValue", runtime.RestoreIsEnabledIfCommandDisabledItClearValueCount);
        builder.Add("controlRuntimeRestoreIsEnabledRestoreStoredValue", runtime.RestoreIsEnabledIfCommandDisabledItRestoreStoredValueCount);

        builder.Add("controlVisualChildrenCalls", telemetry.GetVisualChildrenCallCount);
        builder.Add("controlVisualChildrenYieldedTemplateRoot", telemetry.GetVisualChildrenYieldedTemplateRootCount);
        builder.Add("controlVisualChildrenWithoutTemplateRoot", telemetry.GetVisualChildrenWithoutTemplateRootCount);
        builder.Add("controlTraversalCountCalls", telemetry.GetVisualChildCountForTraversalCallCount);
        builder.Add("controlTraversalCountWithTemplateRoot", telemetry.GetVisualChildCountForTraversalWithTemplateRootCount);
        builder.Add("controlTraversalCountWithoutTemplateRoot", telemetry.GetVisualChildCountForTraversalWithoutTemplateRootCount);
        builder.Add("controlTraversalIndexCalls", telemetry.GetVisualChildAtForTraversalCallCount);
        builder.Add("controlTraversalIndexTemplateRootPath", telemetry.GetVisualChildAtForTraversalTemplateRootPathCount);
        builder.Add("controlTraversalOutOfRange", telemetry.GetVisualChildAtForTraversalOutOfRangeCount);
        builder.Add("controlApplyTemplateCalls", telemetry.ApplyTemplateCallCount);
        builder.Add("controlApplyTemplateMs", FormatMilliseconds(telemetry.ApplyTemplateMilliseconds));
        builder.Add("controlApplyTemplateNull", telemetry.ApplyTemplateTemplateNullCount);
        builder.Add("controlApplyTemplateTargetMismatch", telemetry.ApplyTemplateTargetTypeMismatchCount);
        builder.Add("controlApplyTemplateBuildReturnedNull", telemetry.ApplyTemplateBuildReturnedNullCount);
        builder.Add("controlApplyTemplateSetTemplateTree", telemetry.ApplyTemplateSetTemplateTreeCount);
        builder.Add("controlApplyTemplateBindingsApplied", telemetry.ApplyTemplateBindingsAppliedCount);
        builder.Add("controlApplyTemplateTriggersApplied", telemetry.ApplyTemplateTriggersAppliedCount);
        builder.Add("controlApplyTemplateValidation", telemetry.ApplyTemplateValidationCount);
        builder.Add("controlApplyTemplateOnApplyTemplate", telemetry.ApplyTemplateOnApplyTemplateCount);
        builder.Add("controlApplyTemplateReturnedTrue", telemetry.ApplyTemplateReturnedTrueCount);
        builder.Add("controlApplyTemplateReturnedFalse", telemetry.ApplyTemplateReturnedFalseCount);
        builder.Add("controlMeasureOverrideCalls", telemetry.MeasureOverrideCallCount);
        builder.Add("controlMeasureOverrideMs", FormatMilliseconds(telemetry.MeasureOverrideMilliseconds));
        builder.Add("controlMeasureOverrideImplicitStyleUpdate", telemetry.MeasureOverrideImplicitStyleUpdateCount);
        builder.Add("controlMeasureOverrideTemplateApplyAttempt", telemetry.MeasureOverrideTemplateApplyAttemptCount);
        builder.Add("controlMeasureOverrideTemplateRootMeasure", telemetry.MeasureOverrideTemplateRootMeasureCount);
        builder.Add("controlMeasureOverrideReturnedZero", telemetry.MeasureOverrideReturnedZeroCount);
        builder.Add("controlCanReuseMeasureCalls", telemetry.CanReuseMeasureCallCount);
        builder.Add("controlCanReuseMeasureTemplateRootDelegated", telemetry.CanReuseMeasureTemplateRootDelegatedCount);
        builder.Add("controlCanReuseMeasureNoTemplateRootRejected", telemetry.CanReuseMeasureNoTemplateRootRejectedCount);
        builder.Add("controlArrangeOverrideCalls", telemetry.ArrangeOverrideCallCount);
        builder.Add("controlArrangeOverrideMs", FormatMilliseconds(telemetry.ArrangeOverrideMilliseconds));
        builder.Add("controlArrangeOverrideTemplateRootArrange", telemetry.ArrangeOverrideTemplateRootArrangeCount);
        builder.Add("controlArrangeOverrideNoTemplateRoot", telemetry.ArrangeOverrideNoTemplateRootCount);
        builder.Add("controlDependencyPropertyChangedCalls", telemetry.DependencyPropertyChangedCallCount);
        builder.Add("controlDependencyPropertyChangedMs", FormatMilliseconds(telemetry.DependencyPropertyChangedMilliseconds));
        builder.Add("controlDependencyPropertyChangedStyle", telemetry.DependencyPropertyChangedStylePropertyCount);
        builder.Add("controlDependencyPropertyChangedTemplate", telemetry.DependencyPropertyChangedTemplatePropertyCount);
        builder.Add("controlDependencyPropertyChangedCommand", telemetry.DependencyPropertyChangedCommandPropertyCount);
        builder.Add("controlDependencyPropertyChangedCommandState", telemetry.DependencyPropertyChangedCommandStatePropertyCount);
        builder.Add("controlDependencyPropertyChangedIsEnabled", telemetry.DependencyPropertyChangedIsEnabledPropertyCount);
        builder.Add("controlDependencyPropertyChangedOther", telemetry.DependencyPropertyChangedOtherPropertyCount);
        builder.Add("controlVisualParentChangedCalls", telemetry.VisualParentChangedCallCount);
        builder.Add("controlVisualParentChangedMs", FormatMilliseconds(telemetry.VisualParentChangedMilliseconds));
        builder.Add("controlVisualParentTrackedImplicitStyleScopes", telemetry.VisualParentChangedTrackedImplicitStyleScopesCount);
        builder.Add("controlVisualParentClearedImplicitStyleScopes", telemetry.VisualParentChangedClearedImplicitStyleScopesCount);
        builder.Add("controlLogicalParentChangedCalls", telemetry.LogicalParentChangedCallCount);
        builder.Add("controlLogicalParentChangedMs", FormatMilliseconds(telemetry.LogicalParentChangedMilliseconds));
        builder.Add("controlLogicalParentSkippedForVisualParent", telemetry.LogicalParentChangedSkippedForVisualParentCount);
        builder.Add("controlLogicalParentTrackedImplicitStyleScopes", telemetry.LogicalParentChangedTrackedImplicitStyleScopesCount);
        builder.Add("controlLogicalParentClearedImplicitStyleScopes", telemetry.LogicalParentChangedClearedImplicitStyleScopesCount);
        builder.Add("controlResourceScopeChangedCalls", telemetry.ResourceScopeChangedCallCount);
        builder.Add("controlResourceScopeChangedApplicationSkip", telemetry.ResourceScopeChangedApplicationSkipCount);
        builder.Add("controlUpdateImplicitStyleCalls", telemetry.UpdateImplicitStyleCallCount);
        builder.Add("controlUpdateImplicitStyleApplied", telemetry.UpdateImplicitStyleAppliedCount);
        builder.Add("controlUpdateImplicitStyleCleared", telemetry.UpdateImplicitStyleClearedCount);
        builder.Add("controlUpdateImplicitStyleNoChange", telemetry.UpdateImplicitStyleNoChangeCount);
        builder.Add("controlUpdateImplicitStyleSkipped", telemetry.UpdateImplicitStyleSkippedCount);
        builder.Add("controlRefreshCommandSubscriptionsCalls", telemetry.RefreshCommandSubscriptionsCallCount);
        builder.Add("controlRefreshCommandSubscriptionsDetachedOld", telemetry.RefreshCommandSubscriptionsDetachedOldCommandCount);
        builder.Add("controlRefreshCommandSubscriptionsAttachedNew", telemetry.RefreshCommandSubscriptionsAttachedNewCommandCount);
        builder.Add("controlUpdateCommandEnabledStateCalls", telemetry.UpdateCommandEnabledStateCallCount);
        builder.Add("controlUpdateCommandEnabledStateNoCommandRestore", telemetry.UpdateCommandEnabledStateNoCommandRestoreCount);
        builder.Add("controlUpdateCommandEnabledStateCanExecuteRestore", telemetry.UpdateCommandEnabledStateCanExecuteRestoreCount);
        builder.Add("controlUpdateCommandEnabledStateDisableCommand", telemetry.UpdateCommandEnabledStateDisableCommandCount);
        builder.Add("controlUpdateCommandEnabledStateForceLocalDisable", telemetry.UpdateCommandEnabledStateForceLocalDisableCount);
        builder.Add("controlRestoreIsEnabledCalls", telemetry.RestoreIsEnabledIfCommandDisabledItCallCount);
        builder.Add("controlRestoreIsEnabledNoOp", telemetry.RestoreIsEnabledIfCommandDisabledItNoOpCount);
        builder.Add("controlRestoreIsEnabledClearValue", telemetry.RestoreIsEnabledIfCommandDisabledItClearValueCount);
        builder.Add("controlRestoreIsEnabledRestoreStoredValue", telemetry.RestoreIsEnabledIfCommandDisabledItRestoreStoredValueCount);
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }
}
