using System;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

public partial class TreeView
{
    private static int _diagOnApplyTemplateCallCount;
    private static int _diagOnApplyTemplateTemplatedPathCount;
    private static int _diagOnApplyTemplateFallbackPathCount;
    private static int _diagInvalidateMeasureCallCount;
    private static int _diagInvalidateMeasureConvertedToArrangeCount;
    private static int _diagInvalidateMeasureBasePathCount;
    private static int _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static int _diagMeasureOverrideTemplatePathCount;
    private static int _diagMeasureOverrideFallbackPathCount;
    private static int _diagArrangeOverrideCallCount;
    private static long _diagArrangeOverrideElapsedTicks;
    private static int _diagArrangeOverrideTemplatePathCount;
    private static int _diagArrangeOverrideFallbackPathCount;
    private static int _diagOnRenderCallCount;
    private static int _diagOnRenderTemplateSkippedCount;
    private static int _diagTryHandleMeasureInvalidationCallCount;
    private static int _diagTryHandleMeasureInvalidationAntiLoopCount;
    private static int _diagTryHandleMeasureInvalidationArrangeOnlyCount;
    private static int _diagTryHandleMeasureInvalidationBasePathCount;
    private static int _diagShouldSuppressMeasureInvalidationCallCount;
    private static int _diagShouldSuppressMeasureInvalidationTrueCount;
    private static int _diagHandleKeyDownCallCount;
    private static int _diagHandleKeyDownHierarchicalPathCount;
    private static int _diagHandleKeyDownTreeItemPathCount;
    private static int _diagHandleKeyDownDisabledSkippedCount;
    private static int _diagSelectHierarchicalItemCallCount;
    private static int _diagSelectHierarchicalItemNotHierarchicalCount;
    private static int _diagSelectHierarchicalItemFoundPathCount;
    private static int _diagSelectHierarchicalItemRefreshThenFoundCount;
    private static int _diagSelectHierarchicalItemNotFoundCount;
    private static int _diagSetHierarchicalItemExpandedCallCount;
    private static int _diagScrollHierarchicalItemIntoViewCallCount;
    private static int _diagScrollHierarchicalItemIntoViewNotHierarchicalCount;
    private static int _diagScrollHierarchicalItemIntoViewFoundCount;
    private static int _diagScrollHierarchicalItemIntoViewRefreshThenFoundCount;
    private static int _diagScrollHierarchicalItemIntoViewNotFoundCount;
    private static int _diagContainerFromHierarchicalItemCallCount;
    private static int _diagOnItemsChangedCallCount;
    private static int _diagOnItemsChangedHierarchicalPathCount;
    private static int _diagOnItemsChangedTreeItemPathCount;
    private static int _diagCreateItemsHostCallCount;
    private static int _diagCreateItemsHostHierarchicalPathCount;
    private static int _diagCreateItemsHostItemsPanelPathCount;
    private static int _diagCreateItemsHostVirtualizingPathCount;
    private static int _diagOnActiveScrollViewerViewportChangedCallCount;
    private static int _diagOnActiveScrollViewerViewportChangedSizeChangedCount;
    private static int _diagOnActiveScrollViewerViewportChangedOffsetChangedCount;
    private static int _diagUpdateItemsHostCallCount;
    private static int _diagAttachItemsHostToActiveScrollViewerCallCount;
    private static int _diagRefreshTreeItemTrackingCallCount;
    private static int _diagRefreshTreeItemTrackingAddedCount;
    private static int _diagRefreshTreeItemTrackingRemovedCount;
    private static int _diagRefreshVirtualizedItemsHostCallCount;
    private static int _diagRefreshVirtualizedItemsHostVirtualizingPathCount;
    private static int _diagRefreshVirtualizedItemsHostNonVirtualizingPathCount;
    private static int _diagGetVisibleItemEntriesCallCount;
    private static int _diagPropagateTypographyFromTreeCallCount;
    private static int _diagHandleHierarchicalKeyDownCallCount;
    private static int _diagHandleHierarchicalKeyDownUpCount;
    private static int _diagHandleHierarchicalKeyDownDownCount;
    private static int _diagHandleHierarchicalKeyDownHomeCount;
    private static int _diagHandleHierarchicalKeyDownEndCount;
    private static int _diagHandleHierarchicalKeyDownPageUpCount;
    private static int _diagHandleHierarchicalKeyDownPageDownCount;
    private static int _diagHandleHierarchicalKeyDownRightCount;
    private static int _diagHandleHierarchicalKeyDownLeftCount;
    private static int _diagHandleTreeItemKeyDownCallCount;
    private static int _diagHandleTreeItemKeyDownUpCount;
    private static int _diagHandleTreeItemKeyDownDownCount;
    private static int _diagHandleTreeItemKeyDownHomeCount;
    private static int _diagHandleTreeItemKeyDownEndCount;
    private static int _diagHandleTreeItemKeyDownRightCount;
    private static int _diagHandleTreeItemKeyDownLeftCount;
    private static int _diagOnMouseLeftButtonDownSelectItemCallCount;
    private static int _diagOnMouseLeftButtonDownExpanderHitCount;
    private static int _diagOnMouseLeftButtonDownSelectOnlyCount;
    private static int _diagApplySelectedItemCallCount;
    private static int _diagApplySelectedItemNoOpCount;
    private static int _diagApplySelectedItemChangedCount;
    private static int _diagSelectHierarchicalRowCallCount;
    private static int _diagExpandOrEnterHierarchicalRowCallCount;
    private static int _diagCollapseOrSelectHierarchicalParentCallCount;
    private static int _diagEstimateHierarchicalPageStepCallCount;
    private static int _diagScrollHierarchicalRowIntoViewCallCount;
    private static int _diagScrollHierarchicalRowIntoViewNoHostCount;
    private static int _diagScrollHierarchicalRowIntoViewScrollUpCount;
    private static int _diagScrollHierarchicalRowIntoViewScrollDownCount;
    private static int _diagScrollHierarchicalRowIntoViewAlreadyVisibleCount;
    private static int _diagHierarchicalRefreshRowsCallCount;
    private static int _diagHierarchicalFindRowIndexCallCount;
    private static int _diagHierarchicalSetExpandedCallCount;
    private static int _diagHierarchicalToggleExpandedCallCount;
    private static int _diagHierarchicalRealizeContainerCallCount;
    private static int _diagHierarchicalRealizeContainerRecycledCount;
    private static int _diagHierarchicalRealizeContainerNewCount;
    private static int _diagHierarchicalRecycleContainerCallCount;
    private static int _diagHierarchicalRecycleContainerKeptSelectedCount;
    private static int _diagHierarchicalRecycleContainerRecycledCount;
    private static int _diagHierarchicalApplyContainerCallCount;
    private static int _diagIsStableHierarchicalDataViewportCallCount;
    private static int _diagIsStableHierarchicalDataViewportTrueCount;
    private static int _diagShouldTreatActiveScrollViewerMeasureAsArrangeOnlyCallCount;
    private static int _diagShouldTreatActiveScrollViewerMeasureAsArrangeOnlyTrueCount;

    private int _runtimeOnApplyTemplateCallCount;
    private int _runtimeInvalidateMeasureCallCount;
    private int _runtimeMeasureOverrideCallCount;
    private int _runtimeArrangeOverrideCallCount;
    private int _runtimeOnRenderCallCount;
    private int _runtimeTryHandleMeasureInvalidationCallCount;
    private int _runtimeHandleKeyDownCallCount;
    private int _runtimeSelectHierarchicalItemCallCount;
    private int _runtimeOnItemsChangedCallCount;
    private int _runtimeOnActiveScrollViewerViewportChangedCallCount;
    private int _runtimeRefreshTreeItemTrackingCallCount;
    private int _runtimeRefreshVirtualizedItemsHostCallCount;
    private int _runtimeGetVisibleItemEntriesCallCount;
    private int _runtimeHandleHierarchicalKeyDownCallCount;
    private int _runtimeHandleTreeItemKeyDownCallCount;
    private int _runtimeOnMouseLeftButtonDownSelectItemCallCount;
    private int _runtimeApplySelectedItemCallCount;
    private int _runtimeHierarchicalRefreshRowsCallCount;
    private int _runtimeHierarchicalRealizeContainerCallCount;
    private int _runtimeHierarchicalRecycleContainerCallCount;

    internal new static TreeViewTelemetrySnapshot GetTelemetryAndReset()
    {
        var snapshot = CreateAggregateTelemetrySnapshot();
        _diagOnApplyTemplateCallCount = 0;
        _diagOnApplyTemplateTemplatedPathCount = 0;
        _diagOnApplyTemplateFallbackPathCount = 0;
        _diagInvalidateMeasureCallCount = 0;
        _diagInvalidateMeasureConvertedToArrangeCount = 0;
        _diagInvalidateMeasureBasePathCount = 0;
        _diagMeasureOverrideCallCount = 0;
        _diagMeasureOverrideElapsedTicks = 0L;
        _diagMeasureOverrideTemplatePathCount = 0;
        _diagMeasureOverrideFallbackPathCount = 0;
        _diagArrangeOverrideCallCount = 0;
        _diagArrangeOverrideElapsedTicks = 0L;
        _diagArrangeOverrideTemplatePathCount = 0;
        _diagArrangeOverrideFallbackPathCount = 0;
        _diagOnRenderCallCount = 0;
        _diagOnRenderTemplateSkippedCount = 0;
        _diagTryHandleMeasureInvalidationCallCount = 0;
        _diagTryHandleMeasureInvalidationAntiLoopCount = 0;
        _diagTryHandleMeasureInvalidationArrangeOnlyCount = 0;
        _diagTryHandleMeasureInvalidationBasePathCount = 0;
        _diagShouldSuppressMeasureInvalidationCallCount = 0;
        _diagShouldSuppressMeasureInvalidationTrueCount = 0;
        _diagHandleKeyDownCallCount = 0;
        _diagHandleKeyDownHierarchicalPathCount = 0;
        _diagHandleKeyDownTreeItemPathCount = 0;
        _diagHandleKeyDownDisabledSkippedCount = 0;
        _diagSelectHierarchicalItemCallCount = 0;
        _diagSelectHierarchicalItemNotHierarchicalCount = 0;
        _diagSelectHierarchicalItemFoundPathCount = 0;
        _diagSelectHierarchicalItemRefreshThenFoundCount = 0;
        _diagSelectHierarchicalItemNotFoundCount = 0;
        _diagSetHierarchicalItemExpandedCallCount = 0;
        _diagScrollHierarchicalItemIntoViewCallCount = 0;
        _diagScrollHierarchicalItemIntoViewNotHierarchicalCount = 0;
        _diagScrollHierarchicalItemIntoViewFoundCount = 0;
        _diagScrollHierarchicalItemIntoViewRefreshThenFoundCount = 0;
        _diagScrollHierarchicalItemIntoViewNotFoundCount = 0;
        _diagContainerFromHierarchicalItemCallCount = 0;
        _diagOnItemsChangedCallCount = 0;
        _diagOnItemsChangedHierarchicalPathCount = 0;
        _diagOnItemsChangedTreeItemPathCount = 0;
        _diagCreateItemsHostCallCount = 0;
        _diagCreateItemsHostHierarchicalPathCount = 0;
        _diagCreateItemsHostItemsPanelPathCount = 0;
        _diagCreateItemsHostVirtualizingPathCount = 0;
        _diagOnActiveScrollViewerViewportChangedCallCount = 0;
        _diagOnActiveScrollViewerViewportChangedSizeChangedCount = 0;
        _diagOnActiveScrollViewerViewportChangedOffsetChangedCount = 0;
        _diagUpdateItemsHostCallCount = 0;
        _diagAttachItemsHostToActiveScrollViewerCallCount = 0;
        _diagRefreshTreeItemTrackingCallCount = 0;
        _diagRefreshTreeItemTrackingAddedCount = 0;
        _diagRefreshTreeItemTrackingRemovedCount = 0;
        _diagRefreshVirtualizedItemsHostCallCount = 0;
        _diagRefreshVirtualizedItemsHostVirtualizingPathCount = 0;
        _diagRefreshVirtualizedItemsHostNonVirtualizingPathCount = 0;
        _diagGetVisibleItemEntriesCallCount = 0;
        _diagPropagateTypographyFromTreeCallCount = 0;
        _diagHandleHierarchicalKeyDownCallCount = 0;
        _diagHandleHierarchicalKeyDownUpCount = 0;
        _diagHandleHierarchicalKeyDownDownCount = 0;
        _diagHandleHierarchicalKeyDownHomeCount = 0;
        _diagHandleHierarchicalKeyDownEndCount = 0;
        _diagHandleHierarchicalKeyDownPageUpCount = 0;
        _diagHandleHierarchicalKeyDownPageDownCount = 0;
        _diagHandleHierarchicalKeyDownRightCount = 0;
        _diagHandleHierarchicalKeyDownLeftCount = 0;
        _diagHandleTreeItemKeyDownCallCount = 0;
        _diagHandleTreeItemKeyDownUpCount = 0;
        _diagHandleTreeItemKeyDownDownCount = 0;
        _diagHandleTreeItemKeyDownHomeCount = 0;
        _diagHandleTreeItemKeyDownEndCount = 0;
        _diagHandleTreeItemKeyDownRightCount = 0;
        _diagHandleTreeItemKeyDownLeftCount = 0;
        _diagOnMouseLeftButtonDownSelectItemCallCount = 0;
        _diagOnMouseLeftButtonDownExpanderHitCount = 0;
        _diagOnMouseLeftButtonDownSelectOnlyCount = 0;
        _diagApplySelectedItemCallCount = 0;
        _diagApplySelectedItemNoOpCount = 0;
        _diagApplySelectedItemChangedCount = 0;
        _diagSelectHierarchicalRowCallCount = 0;
        _diagExpandOrEnterHierarchicalRowCallCount = 0;
        _diagCollapseOrSelectHierarchicalParentCallCount = 0;
        _diagEstimateHierarchicalPageStepCallCount = 0;
        _diagScrollHierarchicalRowIntoViewCallCount = 0;
        _diagScrollHierarchicalRowIntoViewNoHostCount = 0;
        _diagScrollHierarchicalRowIntoViewScrollUpCount = 0;
        _diagScrollHierarchicalRowIntoViewScrollDownCount = 0;
        _diagScrollHierarchicalRowIntoViewAlreadyVisibleCount = 0;
        _diagHierarchicalRefreshRowsCallCount = 0;
        _diagHierarchicalFindRowIndexCallCount = 0;
        _diagHierarchicalSetExpandedCallCount = 0;
        _diagHierarchicalToggleExpandedCallCount = 0;
        _diagHierarchicalRealizeContainerCallCount = 0;
        _diagHierarchicalRealizeContainerRecycledCount = 0;
        _diagHierarchicalRealizeContainerNewCount = 0;
        _diagHierarchicalRecycleContainerCallCount = 0;
        _diagHierarchicalRecycleContainerKeptSelectedCount = 0;
        _diagHierarchicalRecycleContainerRecycledCount = 0;
        _diagHierarchicalApplyContainerCallCount = 0;
        _diagIsStableHierarchicalDataViewportCallCount = 0;
        _diagIsStableHierarchicalDataViewportTrueCount = 0;
        _diagShouldTreatActiveScrollViewerMeasureAsArrangeOnlyCallCount = 0;
        _diagShouldTreatActiveScrollViewerMeasureAsArrangeOnlyTrueCount = 0;
        return snapshot;
    }

    internal new static TreeViewTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot();
    }

    internal TreeViewRuntimeDiagnosticsSnapshot GetTreeViewSnapshotForDiagnostics()
    {
        return new TreeViewRuntimeDiagnosticsSnapshot(
            HasTemplateRoot,
            IsHierarchicalDataMode,
            _trackedTreeItems.Count,
            HierarchicalRowCount,
            RealizedHierarchicalContainerCount,
            ActiveScrollViewer.GetType().Name,
            _itemsHost.GetType().Name,
            _lastDataHostViewportWidth,
            _lastDataHostViewportHeight,
            _selectedDataItem,
            SelectedItem != null,
            _runtimeOnApplyTemplateCallCount,
            _runtimeInvalidateMeasureCallCount,
            _runtimeMeasureOverrideCallCount,
            _runtimeArrangeOverrideCallCount,
            _runtimeOnRenderCallCount,
            _runtimeTryHandleMeasureInvalidationCallCount,
            _runtimeHandleKeyDownCallCount,
            _runtimeSelectHierarchicalItemCallCount,
            _runtimeOnItemsChangedCallCount,
            _runtimeOnActiveScrollViewerViewportChangedCallCount,
            _runtimeRefreshTreeItemTrackingCallCount,
            _runtimeRefreshVirtualizedItemsHostCallCount,
            _runtimeGetVisibleItemEntriesCallCount,
            _runtimeHandleHierarchicalKeyDownCallCount,
            _runtimeHandleTreeItemKeyDownCallCount,
            _runtimeOnMouseLeftButtonDownSelectItemCallCount,
            _runtimeApplySelectedItemCallCount,
            _runtimeHierarchicalRefreshRowsCallCount,
            _runtimeHierarchicalRealizeContainerCallCount,
            _runtimeHierarchicalRecycleContainerCallCount);
    }

    private static TreeViewTelemetrySnapshot CreateAggregateTelemetrySnapshot()
    {
        return new TreeViewTelemetrySnapshot(
            _diagOnApplyTemplateCallCount,
            _diagOnApplyTemplateTemplatedPathCount,
            _diagOnApplyTemplateFallbackPathCount,
            _diagInvalidateMeasureCallCount,
            _diagInvalidateMeasureConvertedToArrangeCount,
            _diagInvalidateMeasureBasePathCount,
            _diagMeasureOverrideCallCount,
            TicksToMilliseconds(_diagMeasureOverrideElapsedTicks),
            _diagMeasureOverrideTemplatePathCount,
            _diagMeasureOverrideFallbackPathCount,
            _diagArrangeOverrideCallCount,
            TicksToMilliseconds(_diagArrangeOverrideElapsedTicks),
            _diagArrangeOverrideTemplatePathCount,
            _diagArrangeOverrideFallbackPathCount,
            _diagOnRenderCallCount,
            _diagOnRenderTemplateSkippedCount,
            _diagTryHandleMeasureInvalidationCallCount,
            _diagTryHandleMeasureInvalidationAntiLoopCount,
            _diagTryHandleMeasureInvalidationArrangeOnlyCount,
            _diagTryHandleMeasureInvalidationBasePathCount,
            _diagShouldSuppressMeasureInvalidationCallCount,
            _diagShouldSuppressMeasureInvalidationTrueCount,
            _diagHandleKeyDownCallCount,
            _diagHandleKeyDownHierarchicalPathCount,
            _diagHandleKeyDownTreeItemPathCount,
            _diagHandleKeyDownDisabledSkippedCount,
            _diagSelectHierarchicalItemCallCount,
            _diagSelectHierarchicalItemNotHierarchicalCount,
            _diagSelectHierarchicalItemFoundPathCount,
            _diagSelectHierarchicalItemRefreshThenFoundCount,
            _diagSelectHierarchicalItemNotFoundCount,
            _diagSetHierarchicalItemExpandedCallCount,
            _diagScrollHierarchicalItemIntoViewCallCount,
            _diagScrollHierarchicalItemIntoViewNotHierarchicalCount,
            _diagScrollHierarchicalItemIntoViewFoundCount,
            _diagScrollHierarchicalItemIntoViewRefreshThenFoundCount,
            _diagScrollHierarchicalItemIntoViewNotFoundCount,
            _diagContainerFromHierarchicalItemCallCount,
            _diagOnItemsChangedCallCount,
            _diagOnItemsChangedHierarchicalPathCount,
            _diagOnItemsChangedTreeItemPathCount,
            _diagCreateItemsHostCallCount,
            _diagCreateItemsHostHierarchicalPathCount,
            _diagCreateItemsHostItemsPanelPathCount,
            _diagCreateItemsHostVirtualizingPathCount,
            _diagOnActiveScrollViewerViewportChangedCallCount,
            _diagOnActiveScrollViewerViewportChangedSizeChangedCount,
            _diagOnActiveScrollViewerViewportChangedOffsetChangedCount,
            _diagUpdateItemsHostCallCount,
            _diagAttachItemsHostToActiveScrollViewerCallCount,
            _diagRefreshTreeItemTrackingCallCount,
            _diagRefreshTreeItemTrackingAddedCount,
            _diagRefreshTreeItemTrackingRemovedCount,
            _diagRefreshVirtualizedItemsHostCallCount,
            _diagRefreshVirtualizedItemsHostVirtualizingPathCount,
            _diagRefreshVirtualizedItemsHostNonVirtualizingPathCount,
            _diagGetVisibleItemEntriesCallCount,
            _diagPropagateTypographyFromTreeCallCount,
            _diagHandleHierarchicalKeyDownCallCount,
            _diagHandleHierarchicalKeyDownUpCount,
            _diagHandleHierarchicalKeyDownDownCount,
            _diagHandleHierarchicalKeyDownHomeCount,
            _diagHandleHierarchicalKeyDownEndCount,
            _diagHandleHierarchicalKeyDownPageUpCount,
            _diagHandleHierarchicalKeyDownPageDownCount,
            _diagHandleHierarchicalKeyDownRightCount,
            _diagHandleHierarchicalKeyDownLeftCount,
            _diagHandleTreeItemKeyDownCallCount,
            _diagHandleTreeItemKeyDownUpCount,
            _diagHandleTreeItemKeyDownDownCount,
            _diagHandleTreeItemKeyDownHomeCount,
            _diagHandleTreeItemKeyDownEndCount,
            _diagHandleTreeItemKeyDownRightCount,
            _diagHandleTreeItemKeyDownLeftCount,
            _diagOnMouseLeftButtonDownSelectItemCallCount,
            _diagOnMouseLeftButtonDownExpanderHitCount,
            _diagOnMouseLeftButtonDownSelectOnlyCount,
            _diagApplySelectedItemCallCount,
            _diagApplySelectedItemNoOpCount,
            _diagApplySelectedItemChangedCount,
            _diagSelectHierarchicalRowCallCount,
            _diagExpandOrEnterHierarchicalRowCallCount,
            _diagCollapseOrSelectHierarchicalParentCallCount,
            _diagEstimateHierarchicalPageStepCallCount,
            _diagScrollHierarchicalRowIntoViewCallCount,
            _diagScrollHierarchicalRowIntoViewNoHostCount,
            _diagScrollHierarchicalRowIntoViewScrollUpCount,
            _diagScrollHierarchicalRowIntoViewScrollDownCount,
            _diagScrollHierarchicalRowIntoViewAlreadyVisibleCount,
            _diagHierarchicalRefreshRowsCallCount,
            _diagHierarchicalFindRowIndexCallCount,
            _diagHierarchicalSetExpandedCallCount,
            _diagHierarchicalToggleExpandedCallCount,
            _diagHierarchicalRealizeContainerCallCount,
            _diagHierarchicalRealizeContainerRecycledCount,
            _diagHierarchicalRealizeContainerNewCount,
            _diagHierarchicalRecycleContainerCallCount,
            _diagHierarchicalRecycleContainerKeptSelectedCount,
            _diagHierarchicalRecycleContainerRecycledCount,
            _diagHierarchicalApplyContainerCallCount,
            _diagIsStableHierarchicalDataViewportCallCount,
            _diagIsStableHierarchicalDataViewportTrueCount,
            _diagShouldTreatActiveScrollViewerMeasureAsArrangeOnlyCallCount,
            _diagShouldTreatActiveScrollViewerMeasureAsArrangeOnlyTrueCount);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1_000_000.0 / Stopwatch.Frequency;
    }

    private static class TreeViewTelemetryStatics
    {
        internal static readonly bool IsTelemetryEnabled = true;
    }
}
