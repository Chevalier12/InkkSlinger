namespace InkkSlinger;

public sealed class InkkOopsUserControlDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 40;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not UserControl target)
        {
            return;
        }

        var runtime = target.GetUserControlSnapshotForDiagnostics();
        var telemetry = UserControl.GetAggregateTelemetrySnapshotForDiagnostics();

        builder.Add("userControlHasTemplateAssigned", runtime.HasTemplateAssigned);
        builder.Add("userControlHasTemplateRoot", runtime.HasTemplateRoot);
        builder.Add("userControlHasCachedTemplateRoot", runtime.HasCachedTemplateRoot);
        builder.Add("userControlCachedTemplateRootType", runtime.CachedTemplateRootType);
        builder.Add("userControlHasContentElement", runtime.HasContentElement);
        builder.Add("userControlContentElementType", runtime.ContentElementType);
        builder.Add("userControlLayoutSlot", $"{runtime.LayoutSlotWidth:0.##},{runtime.LayoutSlotHeight:0.##}");
        builder.Add("userControlBorderThickness", FormatThickness(runtime.BorderLeft, runtime.BorderTop, runtime.BorderRight, runtime.BorderBottom));
        builder.Add("userControlPadding", FormatThickness(runtime.PaddingLeft, runtime.PaddingTop, runtime.PaddingRight, runtime.PaddingBottom));

        builder.Add("userControlRuntimeDependencyPropertyChangedCalls", runtime.DependencyPropertyChangedCallCount);
        builder.Add("userControlRuntimeDependencyPropertyChangedMs", FormatMilliseconds(runtime.DependencyPropertyChangedMilliseconds));
        builder.Add("userControlRuntimeRejectedNonUiElement", runtime.DependencyPropertyChangedRejectedNonUiElementCount);
        builder.Add("userControlRuntimeTemplatePropertyChanges", runtime.DependencyPropertyChangedTemplatePropertyCount);
        builder.Add("userControlRuntimeTemplateDetachCalls", runtime.DependencyPropertyChangedTemplateDetachCount);
        builder.Add("userControlRuntimeTemplateCacheClears", runtime.DependencyPropertyChangedTemplateCacheClearCount);
        builder.Add("userControlRuntimeTemplateRefreshes", runtime.DependencyPropertyChangedTemplateRefreshCount);
        builder.Add("userControlRuntimeOtherPropertyChanges", runtime.DependencyPropertyChangedOtherPropertyCount);
        builder.Add("userControlRuntimeVisualChildrenCalls", runtime.GetVisualChildrenCallCount);
        builder.Add("userControlRuntimeVisualChildrenTemplatePath", runtime.GetVisualChildrenTemplatePathCount);
        builder.Add("userControlRuntimeVisualChildrenNonTemplatePath", runtime.GetVisualChildrenNonTemplatePathCount);
        builder.Add("userControlRuntimeVisualChildrenFilteredContent", runtime.GetVisualChildrenFilteredContentCount);
        builder.Add("userControlRuntimeVisualChildrenYielded", runtime.GetVisualChildrenYieldedChildCount);
        builder.Add("userControlRuntimeLogicalChildrenCalls", runtime.GetLogicalChildrenCallCount);
        builder.Add("userControlRuntimeLogicalChildrenTemplatePath", runtime.GetLogicalChildrenTemplatePathCount);
        builder.Add("userControlRuntimeLogicalChildrenNonTemplatePath", runtime.GetLogicalChildrenNonTemplatePathCount);
        builder.Add("userControlRuntimeLogicalChildrenFilteredContent", runtime.GetLogicalChildrenFilteredContentCount);
        builder.Add("userControlRuntimeLogicalChildrenYielded", runtime.GetLogicalChildrenYieldedChildCount);
        builder.Add("userControlRuntimeTraversalCountCalls", runtime.GetVisualChildCountForTraversalCallCount);
        builder.Add("userControlRuntimeTraversalCountTemplatePath", runtime.GetVisualChildCountForTraversalTemplatePathCount);
        builder.Add("userControlRuntimeTraversalCountNonTemplatePath", runtime.GetVisualChildCountForTraversalNonTemplatePathCount);
        builder.Add("userControlRuntimeTraversalCountFilteredContent", runtime.GetVisualChildCountForTraversalFilteredContentCount);
        builder.Add("userControlRuntimeTraversalIndexCalls", runtime.GetVisualChildAtForTraversalCallCount);
        builder.Add("userControlRuntimeTraversalIndexTemplatePath", runtime.GetVisualChildAtForTraversalTemplatePathCount);
        builder.Add("userControlRuntimeTraversalIndexNonTemplatePath", runtime.GetVisualChildAtForTraversalNonTemplatePathCount);
        builder.Add("userControlRuntimeTraversalFilteredContent", runtime.GetVisualChildAtForTraversalFilteredContentCount);
        builder.Add("userControlRuntimeTraversalOutOfRange", runtime.GetVisualChildAtForTraversalOutOfRangeCount);
        builder.Add("userControlRuntimeMeasureOverrideCalls", runtime.MeasureOverrideCallCount);
        builder.Add("userControlRuntimeMeasureOverrideMs", FormatMilliseconds(runtime.MeasureOverrideMilliseconds));
        builder.Add("userControlRuntimeMeasureTemplatePath", runtime.MeasureOverrideTemplatePathCount);
        builder.Add("userControlRuntimeMeasureNonTemplatePath", runtime.MeasureOverrideNonTemplatePathCount);
        builder.Add("userControlRuntimeMeasureTemplateRoot", runtime.MeasureOverrideTemplateRootMeasureCount);
        builder.Add("userControlRuntimeMeasureTemplateReturnedZero", runtime.MeasureOverrideTemplateReturnedZeroCount);
        builder.Add("userControlRuntimeMeasureNoContent", runtime.MeasureOverrideNoContentCount);
        builder.Add("userControlRuntimeMeasureContent", runtime.MeasureOverrideContentMeasureCount);
        builder.Add("userControlRuntimeArrangeOverrideCalls", runtime.ArrangeOverrideCallCount);
        builder.Add("userControlRuntimeArrangeOverrideMs", FormatMilliseconds(runtime.ArrangeOverrideMilliseconds));
        builder.Add("userControlRuntimeArrangeTemplatePath", runtime.ArrangeOverrideTemplatePathCount);
        builder.Add("userControlRuntimeArrangeNonTemplatePath", runtime.ArrangeOverrideNonTemplatePathCount);
        builder.Add("userControlRuntimeArrangeTemplateRoot", runtime.ArrangeOverrideTemplateRootArrangeCount);
        builder.Add("userControlRuntimeArrangeNoContent", runtime.ArrangeOverrideNoContentCount);
        builder.Add("userControlRuntimeArrangeContent", runtime.ArrangeOverrideContentArrangeCount);
        builder.Add("userControlRuntimeRenderCalls", runtime.RenderCallCount);
        builder.Add("userControlRuntimeRenderMs", FormatMilliseconds(runtime.RenderMilliseconds));
        builder.Add("userControlRuntimeRenderTemplateSkip", runtime.RenderTemplateSkipCount);
        builder.Add("userControlRuntimeRenderBackground", runtime.RenderBackgroundDrawCount);
        builder.Add("userControlRuntimeRenderBorderEdges", runtime.RenderBorderEdgeDrawCount);
        builder.Add("userControlRuntimeGetChromeThicknessCalls", runtime.GetChromeThicknessCallCount);
        builder.Add("userControlRuntimeEnsureTemplateCalls", runtime.EnsureTemplateAppliedIfNeededCallCount);
        builder.Add("userControlRuntimeEnsureTemplateMs", FormatMilliseconds(runtime.EnsureTemplateAppliedIfNeededMilliseconds));
        builder.Add("userControlRuntimeEnsureTemplateApply", runtime.EnsureTemplateAppliedApplyTemplateCount);
        builder.Add("userControlRuntimeEnsureTemplateRefresh", runtime.EnsureTemplateAppliedRefreshCachedRootCount);
        builder.Add("userControlRuntimeEnsureTemplateNoOp", runtime.EnsureTemplateAppliedNoOpCount);
        builder.Add("userControlRuntimeRefreshCachedRootCalls", runtime.RefreshCachedTemplateRootCallCount);
        builder.Add("userControlRuntimeRefreshCachedRootMs", FormatMilliseconds(runtime.RefreshCachedTemplateRootMilliseconds));
        builder.Add("userControlRuntimeRefreshCachedRootHits", runtime.RefreshCachedTemplateRootHitCount);
        builder.Add("userControlRuntimeRefreshCachedRootMisses", runtime.RefreshCachedTemplateRootMissCount);
        builder.Add("userControlRuntimeRefreshCachedRootEnumerated", runtime.RefreshCachedTemplateRootEnumeratedChildCount);
        builder.Add("userControlRuntimeDetachPresenterCalls", runtime.DetachTemplateContentPresentersCallCount);
        builder.Add("userControlRuntimeDetachPresenterMs", FormatMilliseconds(runtime.DetachTemplateContentPresentersMilliseconds));
        builder.Add("userControlRuntimeDetachPresenterFallbackSearch", runtime.DetachTemplateContentPresentersFallbackSearchCount);
        builder.Add("userControlRuntimeDetachPresenterRootNotFound", runtime.DetachTemplateContentPresentersRootNotFoundCount);
        builder.Add("userControlRuntimeDetachPresenterVisited", runtime.DetachTemplateContentPresentersVisitedElementCount);
        builder.Add("userControlRuntimeDetachPresenterDetachCount", runtime.DetachTemplateContentPresentersPresenterDetachCount);
        builder.Add("userControlRuntimeDetachPresenterTraversed", runtime.DetachTemplateContentPresentersTraversedChildCount);

        builder.Add("userControlDependencyPropertyChangedCalls", telemetry.DependencyPropertyChangedCallCount);
        builder.Add("userControlDependencyPropertyChangedMs", FormatMilliseconds(telemetry.DependencyPropertyChangedMilliseconds));
        builder.Add("userControlRejectedNonUiElement", telemetry.DependencyPropertyChangedRejectedNonUiElementCount);
        builder.Add("userControlTemplatePropertyChanges", telemetry.DependencyPropertyChangedTemplatePropertyCount);
        builder.Add("userControlTemplateDetachCalls", telemetry.DependencyPropertyChangedTemplateDetachCount);
        builder.Add("userControlTemplateCacheClears", telemetry.DependencyPropertyChangedTemplateCacheClearCount);
        builder.Add("userControlTemplateRefreshes", telemetry.DependencyPropertyChangedTemplateRefreshCount);
        builder.Add("userControlOtherPropertyChanges", telemetry.DependencyPropertyChangedOtherPropertyCount);
        builder.Add("userControlVisualChildrenCalls", telemetry.GetVisualChildrenCallCount);
        builder.Add("userControlVisualChildrenTemplatePath", telemetry.GetVisualChildrenTemplatePathCount);
        builder.Add("userControlVisualChildrenNonTemplatePath", telemetry.GetVisualChildrenNonTemplatePathCount);
        builder.Add("userControlVisualChildrenFilteredContent", telemetry.GetVisualChildrenFilteredContentCount);
        builder.Add("userControlVisualChildrenYielded", telemetry.GetVisualChildrenYieldedChildCount);
        builder.Add("userControlLogicalChildrenCalls", telemetry.GetLogicalChildrenCallCount);
        builder.Add("userControlLogicalChildrenTemplatePath", telemetry.GetLogicalChildrenTemplatePathCount);
        builder.Add("userControlLogicalChildrenNonTemplatePath", telemetry.GetLogicalChildrenNonTemplatePathCount);
        builder.Add("userControlLogicalChildrenFilteredContent", telemetry.GetLogicalChildrenFilteredContentCount);
        builder.Add("userControlLogicalChildrenYielded", telemetry.GetLogicalChildrenYieldedChildCount);
        builder.Add("userControlTraversalCountCalls", telemetry.GetVisualChildCountForTraversalCallCount);
        builder.Add("userControlTraversalCountTemplatePath", telemetry.GetVisualChildCountForTraversalTemplatePathCount);
        builder.Add("userControlTraversalCountNonTemplatePath", telemetry.GetVisualChildCountForTraversalNonTemplatePathCount);
        builder.Add("userControlTraversalCountFilteredContent", telemetry.GetVisualChildCountForTraversalFilteredContentCount);
        builder.Add("userControlTraversalIndexCalls", telemetry.GetVisualChildAtForTraversalCallCount);
        builder.Add("userControlTraversalIndexTemplatePath", telemetry.GetVisualChildAtForTraversalTemplatePathCount);
        builder.Add("userControlTraversalIndexNonTemplatePath", telemetry.GetVisualChildAtForTraversalNonTemplatePathCount);
        builder.Add("userControlTraversalIndexFilteredContent", telemetry.GetVisualChildAtForTraversalFilteredContentCount);
        builder.Add("userControlTraversalOutOfRange", telemetry.GetVisualChildAtForTraversalOutOfRangeCount);
        builder.Add("userControlMeasureOverrideCalls", telemetry.MeasureOverrideCallCount);
        builder.Add("userControlMeasureOverrideMs", FormatMilliseconds(telemetry.MeasureOverrideMilliseconds));
        builder.Add("userControlMeasureTemplatePath", telemetry.MeasureOverrideTemplatePathCount);
        builder.Add("userControlMeasureNonTemplatePath", telemetry.MeasureOverrideNonTemplatePathCount);
        builder.Add("userControlMeasureTemplateRoot", telemetry.MeasureOverrideTemplateRootMeasureCount);
        builder.Add("userControlMeasureTemplateReturnedZero", telemetry.MeasureOverrideTemplateReturnedZeroCount);
        builder.Add("userControlMeasureNoContent", telemetry.MeasureOverrideNoContentCount);
        builder.Add("userControlMeasureContent", telemetry.MeasureOverrideContentMeasureCount);
        builder.Add("userControlArrangeOverrideCalls", telemetry.ArrangeOverrideCallCount);
        builder.Add("userControlArrangeOverrideMs", FormatMilliseconds(telemetry.ArrangeOverrideMilliseconds));
        builder.Add("userControlArrangeTemplatePath", telemetry.ArrangeOverrideTemplatePathCount);
        builder.Add("userControlArrangeNonTemplatePath", telemetry.ArrangeOverrideNonTemplatePathCount);
        builder.Add("userControlArrangeTemplateRoot", telemetry.ArrangeOverrideTemplateRootArrangeCount);
        builder.Add("userControlArrangeNoContent", telemetry.ArrangeOverrideNoContentCount);
        builder.Add("userControlArrangeContent", telemetry.ArrangeOverrideContentArrangeCount);
        builder.Add("userControlRenderCalls", telemetry.RenderCallCount);
        builder.Add("userControlRenderMs", FormatMilliseconds(telemetry.RenderMilliseconds));
        builder.Add("userControlRenderTemplateSkip", telemetry.RenderTemplateSkipCount);
        builder.Add("userControlRenderBackground", telemetry.RenderBackgroundDrawCount);
        builder.Add("userControlRenderBorderEdges", telemetry.RenderBorderEdgeDrawCount);
        builder.Add("userControlGetChromeThicknessCalls", telemetry.GetChromeThicknessCallCount);
        builder.Add("userControlEnsureTemplateCalls", telemetry.EnsureTemplateAppliedIfNeededCallCount);
        builder.Add("userControlEnsureTemplateMs", FormatMilliseconds(telemetry.EnsureTemplateAppliedIfNeededMilliseconds));
        builder.Add("userControlEnsureTemplateApply", telemetry.EnsureTemplateAppliedApplyTemplateCount);
        builder.Add("userControlEnsureTemplateRefresh", telemetry.EnsureTemplateAppliedRefreshCachedRootCount);
        builder.Add("userControlEnsureTemplateNoOp", telemetry.EnsureTemplateAppliedNoOpCount);
        builder.Add("userControlRefreshCachedRootCalls", telemetry.RefreshCachedTemplateRootCallCount);
        builder.Add("userControlRefreshCachedRootMs", FormatMilliseconds(telemetry.RefreshCachedTemplateRootMilliseconds));
        builder.Add("userControlRefreshCachedRootHits", telemetry.RefreshCachedTemplateRootHitCount);
        builder.Add("userControlRefreshCachedRootMisses", telemetry.RefreshCachedTemplateRootMissCount);
        builder.Add("userControlRefreshCachedRootEnumerated", telemetry.RefreshCachedTemplateRootEnumeratedChildCount);
        builder.Add("userControlDetachPresenterCalls", telemetry.DetachTemplateContentPresentersCallCount);
        builder.Add("userControlDetachPresenterMs", FormatMilliseconds(telemetry.DetachTemplateContentPresentersMilliseconds));
        builder.Add("userControlDetachPresenterFallbackSearch", telemetry.DetachTemplateContentPresentersFallbackSearchCount);
        builder.Add("userControlDetachPresenterRootNotFound", telemetry.DetachTemplateContentPresentersRootNotFoundCount);
        builder.Add("userControlDetachPresenterVisited", telemetry.DetachTemplateContentPresentersVisitedElementCount);
        builder.Add("userControlDetachPresenterDetachCount", telemetry.DetachTemplateContentPresentersPresenterDetachCount);
        builder.Add("userControlDetachPresenterTraversed", telemetry.DetachTemplateContentPresentersTraversedChildCount);

        if (target is IInkkOopsCustomDiagnosticsSource customDiagnosticsSource)
        {
            customDiagnosticsSource.ContributeInkkOopsDiagnostics(builder);
        }
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###");
    }

    private static string FormatThickness(float left, float top, float right, float bottom)
    {
        return $"{left:0.##},{top:0.##},{right:0.##},{bottom:0.##}";
    }
}