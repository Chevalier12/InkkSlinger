namespace InkkSlinger;

public sealed class InkkOopsContentControlDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 40;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        _ = context;

        if (element is not ContentControl target)
        {
            return;
        }

        var runtime = target.GetContentControlSnapshotForDiagnostics();
        var telemetry = ContentControl.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("contentControlHasContentElement", runtime.HasContentElement);
        builder.Add("contentControlContentElementType", runtime.ContentElementType);
        builder.Add("contentControlHasActiveContentPresenter", runtime.HasActiveContentPresenter);
        builder.Add("contentControlActiveContentPresenterType", runtime.ActiveContentPresenterType);
        builder.Add("contentControlHasContent", runtime.HasContent);
        builder.Add("contentControlContentType", runtime.ContentType);
        builder.Add("contentControlHasContentTemplate", runtime.HasContentTemplate);
        builder.Add("contentControlHasContentTemplateSelector", runtime.HasContentTemplateSelector);
        builder.Add("contentControlIsLabelInstance", runtime.IsLabelInstance);
        builder.Add("contentControlLayoutSlot", $"{runtime.LayoutSlotWidth:0.##},{runtime.LayoutSlotHeight:0.##}");

        builder.Add("contentControlRuntimeDependencyPropertyChangedCalls", runtime.DependencyPropertyChangedCallCount);
        builder.Add("contentControlRuntimeDependencyPropertyChangedMs", FormatMilliseconds(runtime.DependencyPropertyChangedMilliseconds));
        builder.Add("contentControlRuntimeDependencyPropertyChangedContent", runtime.DependencyPropertyChangedContentPropertyCount);
        builder.Add("contentControlRuntimeDependencyPropertyChangedTemplate", runtime.DependencyPropertyChangedTemplatePropertyCount);
        builder.Add("contentControlRuntimeDependencyPropertyChangedOther", runtime.DependencyPropertyChangedOtherPropertyCount);
        builder.Add("contentControlRuntimeVisualParentChangedCalls", runtime.VisualParentChangedCallCount);
        builder.Add("contentControlRuntimeLogicalParentChangedCalls", runtime.LogicalParentChangedCallCount);
        builder.Add("contentControlRuntimeVisualChildrenCalls", runtime.GetVisualChildrenCallCount);
        builder.Add("contentControlRuntimeVisualChildrenYieldedBase", runtime.GetVisualChildrenYieldedBaseChildCount);
        builder.Add("contentControlRuntimeVisualChildrenYieldedContent", runtime.GetVisualChildrenYieldedContentChildCount);
        builder.Add("contentControlRuntimeTraversalCountCalls", runtime.GetVisualChildCountForTraversalCallCount);
        builder.Add("contentControlRuntimeTraversalCountWithContent", runtime.GetVisualChildCountForTraversalWithContentElementCount);
        builder.Add("contentControlRuntimeTraversalCountWithoutContent", runtime.GetVisualChildCountForTraversalWithoutContentElementCount);
        builder.Add("contentControlRuntimeTraversalIndexCalls", runtime.GetVisualChildAtForTraversalCallCount);
        builder.Add("contentControlRuntimeTraversalIndexBasePath", runtime.GetVisualChildAtForTraversalBasePathCount);
        builder.Add("contentControlRuntimeTraversalIndexContentPath", runtime.GetVisualChildAtForTraversalContentPathCount);
        builder.Add("contentControlRuntimeTraversalOutOfRange", runtime.GetVisualChildAtForTraversalOutOfRangeCount);
        builder.Add("contentControlRuntimeLogicalChildrenCalls", runtime.GetLogicalChildrenCallCount);
        builder.Add("contentControlRuntimeLogicalChildrenYieldedBase", runtime.GetLogicalChildrenYieldedBaseChildCount);
        builder.Add("contentControlRuntimeLogicalChildrenYieldedContent", runtime.GetLogicalChildrenYieldedContentChildCount);
        builder.Add("contentControlRuntimeMeasureOverrideCalls", runtime.MeasureOverrideCallCount);
        builder.Add("contentControlRuntimeMeasureOverrideMs", FormatMilliseconds(runtime.MeasureOverrideMilliseconds));
        builder.Add("contentControlRuntimeMeasureOverrideActivePresenterSkip", runtime.MeasureOverrideActivePresenterSkipCount);
        builder.Add("contentControlRuntimeMeasureOverrideContentMeasure", runtime.MeasureOverrideContentMeasureCount);
        builder.Add("contentControlRuntimeMeasureOverrideNoContent", runtime.MeasureOverrideNoContentCount);
        builder.Add("contentControlRuntimeCanReuseMeasureCalls", runtime.CanReuseMeasureCallCount);
        builder.Add("contentControlRuntimeCanReuseMeasureBaseRejected", runtime.CanReuseMeasureBaseRejectedCount);
        builder.Add("contentControlRuntimeCanReuseMeasureDelegated", runtime.CanReuseMeasureDelegatedCount);
        builder.Add("contentControlRuntimeCanReuseMeasureActivePresenterOrNoContentTrue", runtime.CanReuseMeasureActivePresenterOrNoContentTrueCount);
        builder.Add("contentControlRuntimeArrangeOverrideCalls", runtime.ArrangeOverrideCallCount);
        builder.Add("contentControlRuntimeArrangeOverrideMs", FormatMilliseconds(runtime.ArrangeOverrideMilliseconds));
        builder.Add("contentControlRuntimeArrangeOverrideActivePresenterSkip", runtime.ArrangeOverrideActivePresenterSkipCount);
        builder.Add("contentControlRuntimeArrangeOverrideContentArrange", runtime.ArrangeOverrideContentArrangeCount);
        builder.Add("contentControlRuntimeArrangeOverrideNoContent", runtime.ArrangeOverrideNoContentCount);
        builder.Add("contentControlRuntimeAttachContentPresenterCalls", runtime.AttachContentPresenterCallCount);
        builder.Add("contentControlRuntimeAttachContentPresenterNoOp", runtime.AttachContentPresenterNoOpCount);
        builder.Add("contentControlRuntimeAttachContentPresenterInvalidateMeasure", runtime.AttachContentPresenterInvalidateMeasureCount);
        builder.Add("contentControlRuntimeDetachContentPresenterCalls", runtime.DetachContentPresenterCallCount);
        builder.Add("contentControlRuntimeDetachContentPresenterIgnored", runtime.DetachContentPresenterIgnoredCount);
        builder.Add("contentControlRuntimeDetachContentPresenterInvalidateMeasure", runtime.DetachContentPresenterInvalidateMeasureCount);
        builder.Add("contentControlRuntimeUpdateContentElementCalls", runtime.UpdateContentElementCallCount);
        builder.Add("contentControlRuntimeUpdateContentElementMs", FormatMilliseconds(runtime.UpdateContentElementMilliseconds));
        builder.Add("contentControlRuntimeUpdateContentElementReusedExisting", runtime.UpdateContentElementReusedExistingElementCount);
        builder.Add("contentControlRuntimeUpdateContentElementNullNoOp", runtime.UpdateContentElementNullNoOpCount);
        builder.Add("contentControlRuntimeUpdateContentElementDetachedOld", runtime.UpdateContentElementDetachedOldElementCount);
        builder.Add("contentControlRuntimeUpdateContentElementPresenterNotify", runtime.UpdateContentElementPresenterNotifyCount);
        builder.Add("contentControlRuntimeUpdateContentElementLabelBypass", runtime.UpdateContentElementLabelBypassCount);
        builder.Add("contentControlRuntimeUpdateContentElementUiElementPath", runtime.UpdateContentElementUiElementPathCount);
        builder.Add("contentControlRuntimeUpdateContentElementTemplateSelected", runtime.UpdateContentElementTemplateSelectedCount);
        builder.Add("contentControlRuntimeUpdateContentElementTemplateBuilt", runtime.UpdateContentElementTemplateBuiltElementCount);
        builder.Add("contentControlRuntimeUpdateContentElementTemplateReturnedNull", runtime.UpdateContentElementTemplateReturnedNullCount);
        builder.Add("contentControlRuntimeUpdateContentElementImplicitSuppressed", runtime.UpdateContentElementImplicitCreationSuppressedCount);
        builder.Add("contentControlRuntimeUpdateContentElementImplicitLabelCreated", runtime.UpdateContentElementImplicitLabelCreatedCount);
        builder.Add("contentControlRuntimeUpdateContentElementNullContentTerminal", runtime.UpdateContentElementNullContentTerminalCount);
        builder.Add("contentControlRuntimeUpdateContentElementAttachedNew", runtime.UpdateContentElementAttachedNewElementCount);

        builder.Add("contentControlDependencyPropertyChangedCalls", telemetry.DependencyPropertyChangedCallCount);
        builder.Add("contentControlDependencyPropertyChangedMs", FormatMilliseconds(telemetry.DependencyPropertyChangedMilliseconds));
        builder.Add("contentControlDependencyPropertyChangedContent", telemetry.DependencyPropertyChangedContentPropertyCount);
        builder.Add("contentControlDependencyPropertyChangedTemplate", telemetry.DependencyPropertyChangedTemplatePropertyCount);
        builder.Add("contentControlDependencyPropertyChangedOther", telemetry.DependencyPropertyChangedOtherPropertyCount);
        builder.Add("contentControlVisualParentChangedCalls", telemetry.VisualParentChangedCallCount);
        builder.Add("contentControlLogicalParentChangedCalls", telemetry.LogicalParentChangedCallCount);
        builder.Add("contentControlVisualChildrenCalls", telemetry.GetVisualChildrenCallCount);
        builder.Add("contentControlVisualChildrenYieldedBase", telemetry.GetVisualChildrenYieldedBaseChildCount);
        builder.Add("contentControlVisualChildrenYieldedContent", telemetry.GetVisualChildrenYieldedContentChildCount);
        builder.Add("contentControlTraversalCountCalls", telemetry.GetVisualChildCountForTraversalCallCount);
        builder.Add("contentControlTraversalCountWithContent", telemetry.GetVisualChildCountForTraversalWithContentElementCount);
        builder.Add("contentControlTraversalCountWithoutContent", telemetry.GetVisualChildCountForTraversalWithoutContentElementCount);
        builder.Add("contentControlTraversalIndexCalls", telemetry.GetVisualChildAtForTraversalCallCount);
        builder.Add("contentControlTraversalIndexBasePath", telemetry.GetVisualChildAtForTraversalBasePathCount);
        builder.Add("contentControlTraversalIndexContentPath", telemetry.GetVisualChildAtForTraversalContentPathCount);
        builder.Add("contentControlTraversalOutOfRange", telemetry.GetVisualChildAtForTraversalOutOfRangeCount);
        builder.Add("contentControlLogicalChildrenCalls", telemetry.GetLogicalChildrenCallCount);
        builder.Add("contentControlLogicalChildrenYieldedBase", telemetry.GetLogicalChildrenYieldedBaseChildCount);
        builder.Add("contentControlLogicalChildrenYieldedContent", telemetry.GetLogicalChildrenYieldedContentChildCount);
        builder.Add("contentControlMeasureOverrideCalls", telemetry.MeasureOverrideCallCount);
        builder.Add("contentControlMeasureOverrideMs", FormatMilliseconds(telemetry.MeasureOverrideMilliseconds));
        builder.Add("contentControlMeasureOverrideActivePresenterSkip", telemetry.MeasureOverrideActivePresenterSkipCount);
        builder.Add("contentControlMeasureOverrideContentMeasure", telemetry.MeasureOverrideContentMeasureCount);
        builder.Add("contentControlMeasureOverrideNoContent", telemetry.MeasureOverrideNoContentCount);
        builder.Add("contentControlCanReuseMeasureCalls", telemetry.CanReuseMeasureCallCount);
        builder.Add("contentControlCanReuseMeasureBaseRejected", telemetry.CanReuseMeasureBaseRejectedCount);
        builder.Add("contentControlCanReuseMeasureDelegated", telemetry.CanReuseMeasureDelegatedCount);
        builder.Add("contentControlCanReuseMeasureActivePresenterOrNoContentTrue", telemetry.CanReuseMeasureActivePresenterOrNoContentTrueCount);
        builder.Add("contentControlArrangeOverrideCalls", telemetry.ArrangeOverrideCallCount);
        builder.Add("contentControlArrangeOverrideMs", FormatMilliseconds(telemetry.ArrangeOverrideMilliseconds));
        builder.Add("contentControlArrangeOverrideActivePresenterSkip", telemetry.ArrangeOverrideActivePresenterSkipCount);
        builder.Add("contentControlArrangeOverrideContentArrange", telemetry.ArrangeOverrideContentArrangeCount);
        builder.Add("contentControlArrangeOverrideNoContent", telemetry.ArrangeOverrideNoContentCount);
        builder.Add("contentControlAttachContentPresenterCalls", telemetry.AttachContentPresenterCallCount);
        builder.Add("contentControlAttachContentPresenterNoOp", telemetry.AttachContentPresenterNoOpCount);
        builder.Add("contentControlAttachContentPresenterInvalidateMeasure", telemetry.AttachContentPresenterInvalidateMeasureCount);
        builder.Add("contentControlDetachContentPresenterCalls", telemetry.DetachContentPresenterCallCount);
        builder.Add("contentControlDetachContentPresenterIgnored", telemetry.DetachContentPresenterIgnoredCount);
        builder.Add("contentControlDetachContentPresenterInvalidateMeasure", telemetry.DetachContentPresenterInvalidateMeasureCount);
        builder.Add("contentControlUpdateContentElementCalls", telemetry.UpdateContentElementCallCount);
        builder.Add("contentControlUpdateContentElementMs", FormatMilliseconds(telemetry.UpdateContentElementMilliseconds));
        builder.Add("contentControlUpdateContentElementReusedExisting", telemetry.UpdateContentElementReusedExistingElementCount);
        builder.Add("contentControlUpdateContentElementNullNoOp", telemetry.UpdateContentElementNullNoOpCount);
        builder.Add("contentControlUpdateContentElementDetachedOld", telemetry.UpdateContentElementDetachedOldElementCount);
        builder.Add("contentControlUpdateContentElementPresenterNotify", telemetry.UpdateContentElementPresenterNotifyCount);
        builder.Add("contentControlUpdateContentElementLabelBypass", telemetry.UpdateContentElementLabelBypassCount);
        builder.Add("contentControlUpdateContentElementUiElementPath", telemetry.UpdateContentElementUiElementPathCount);
        builder.Add("contentControlUpdateContentElementTemplateSelected", telemetry.UpdateContentElementTemplateSelectedCount);
        builder.Add("contentControlUpdateContentElementTemplateBuilt", telemetry.UpdateContentElementTemplateBuiltElementCount);
        builder.Add("contentControlUpdateContentElementTemplateReturnedNull", telemetry.UpdateContentElementTemplateReturnedNullCount);
        builder.Add("contentControlUpdateContentElementImplicitSuppressed", telemetry.UpdateContentElementImplicitCreationSuppressedCount);
        builder.Add("contentControlUpdateContentElementImplicitLabelCreated", telemetry.UpdateContentElementImplicitLabelCreatedCount);
        builder.Add("contentControlUpdateContentElementNullContentTerminal", telemetry.UpdateContentElementNullContentTerminalCount);
        builder.Add("contentControlUpdateContentElementAttachedNew", telemetry.UpdateContentElementAttachedNewElementCount);
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }
}
