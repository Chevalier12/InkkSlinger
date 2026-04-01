namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// ScrollViewer scroll metrics snapshot.
/// </summary>
public readonly record struct ScrollViewerScrollMetricsSnapshot(
    int WheelEvents,
    int WheelHandled,
    int SetOffsetCalls,
    int SetOffsetNoOpCalls,
    float TotalHorizontalDelta,
    float TotalVerticalDelta);

/// <summary>
/// ScrollViewer interaction telemetry snapshot.
/// </summary>
internal readonly record struct ScrollViewerInteractionTelemetrySnapshot(
    int ScrollToHorizontalOffsetCallCount,
    int ScrollToVerticalOffsetCallCount,
    int InvalidateScrollInfoCallCount,
    int HandleMouseWheelCallCount,
    double HandleMouseWheelMilliseconds,
    int HandleMouseWheelHandledCount,
    int HandleMouseWheelIgnoredDisabledCount,
    int HandleMouseWheelIgnoredZeroDeltaCount,
    int SetOffsetsCallCount,
    double SetOffsetsMilliseconds,
    int SetOffsetsExternalSourceCount,
    int SetOffsetsHorizontalScrollBarSourceCount,
    int SetOffsetsVerticalScrollBarSourceCount,
    int SetOffsetsWorkCount,
    int SetOffsetsNoOpCount,
    int SetOffsetsDeferredLayoutPathCount,
    int SetOffsetsVirtualizingMeasureInvalidationPathCount,
    int SetOffsetsVirtualizingArrangeOnlyPathCount,
    int SetOffsetsTransformInvalidationPathCount,
    int SetOffsetsManualArrangePathCount,
    int PopupCloseCallCount,
    int ArrangeContentForCurrentOffsetsCallCount,
    double ArrangeContentForCurrentOffsetsMilliseconds,
    int ArrangeContentSkippedNoContentCount,
    int ArrangeContentSkippedZeroViewportCount,
    int ArrangeContentTransformPathCount,
    int ArrangeContentOffsetPathCount,
    int UpdateScrollBarValuesCallCount,
    double UpdateScrollBarValuesMilliseconds,
    int UpdateHorizontalScrollBarValueCallCount,
    double UpdateHorizontalScrollBarValueMilliseconds,
    int UpdateVerticalScrollBarValueCallCount,
    double UpdateVerticalScrollBarValueMilliseconds);

/// <summary>
/// ScrollViewer value changed telemetry snapshot.
/// </summary>
internal readonly record struct ScrollViewerValueChangedTelemetrySnapshot(
    int HorizontalValueChangedCallCount,
    double HorizontalValueChangedMilliseconds,
    double HorizontalValueChangedSetOffsetsMilliseconds,
    int HorizontalValueChangedSuppressedCount,
    int VerticalValueChangedCallCount,
    double VerticalValueChangedMilliseconds,
    double VerticalValueChangedSetOffsetsMilliseconds,
    int VerticalValueChangedSuppressedCount);

/// <summary>
/// ScrollViewer layout telemetry snapshot.
/// </summary>
internal readonly record struct ScrollViewerLayoutTelemetrySnapshot(
    int MeasureOverrideCallCount,
    double MeasureOverrideMilliseconds,
    int ArrangeOverrideCallCount,
    double ArrangeOverrideMilliseconds,
    int ResolveBarsAndMeasureContentCallCount,
    double ResolveBarsAndMeasureContentMilliseconds,
    int ResolveBarsAndMeasureContentIterationCount,
    int ResolveBarsAndMeasureContentHorizontalFlipCount,
    int ResolveBarsAndMeasureContentVerticalFlipCount,
    int ResolveBarsAndMeasureContentSingleMeasurePathCount,
    int ResolveBarsAndMeasureContentRemeasurePathCount,
    int ResolveBarsAndMeasureContentFallbackCount,
    int ResolveBarsAndMeasureContentInitialHorizontalVisibleCount,
    int ResolveBarsAndMeasureContentInitialHorizontalHiddenCount,
    int ResolveBarsAndMeasureContentInitialVerticalVisibleCount,
    int ResolveBarsAndMeasureContentInitialVerticalHiddenCount,
    int ResolveBarsAndMeasureContentResolvedHorizontalVisibleCount,
    int ResolveBarsAndMeasureContentResolvedHorizontalHiddenCount,
    int ResolveBarsAndMeasureContentResolvedVerticalVisibleCount,
    int ResolveBarsAndMeasureContentResolvedVerticalHiddenCount,
    string ResolveBarsAndMeasureContentLastTrace,
    string ResolveBarsAndMeasureContentHottestTrace,
    int ResolveBarsForArrangeCallCount,
    double ResolveBarsForArrangeMilliseconds,
    int ResolveBarsForArrangeIterationCount,
    int ResolveBarsForArrangeHorizontalFlipCount,
    int ResolveBarsForArrangeVerticalFlipCount,
    int MeasureContentCallCount,
    double MeasureContentMilliseconds,
    int UpdateScrollBarsCallCount,
    double UpdateScrollBarsMilliseconds);

/// <summary>
/// ScrollBar thumb drag telemetry snapshot.
/// </summary>
internal readonly record struct ScrollBarThumbDragTelemetrySnapshot(
    int OnThumbDragDeltaCallCount,
    double OnThumbDragDeltaMilliseconds,
    double OnThumbDragDeltaValueSetMilliseconds,
    double OnValueChangedBaseMilliseconds,
    double OnValueChangedSyncTrackStateMilliseconds,
    double SyncTrackStateMilliseconds,
    double RefreshTrackLayoutMilliseconds);

/// <summary>
/// Track thumb travel telemetry snapshot.
/// </summary>
internal readonly record struct TrackThumbTravelTelemetrySnapshot(
    int GetValueFromThumbTravelCallCount,
    double GetValueFromThumbTravelMilliseconds,
    int RefreshLayoutForStateMutationCallCount,
    double RefreshLayoutForStateMutationMilliseconds,
    int RefreshLayoutValueMutationCallCount,
    double RefreshLayoutValueMutationMilliseconds,
    int RefreshLayoutViewportMutationCallCount,
    double RefreshLayoutViewportMutationMilliseconds,
    int RefreshLayoutMinimumMutationCallCount,
    double RefreshLayoutMinimumMutationMilliseconds,
    int RefreshLayoutMaximumMutationCallCount,
    double RefreshLayoutMaximumMutationMilliseconds,
    int RefreshLayoutDirectionMutationCallCount,
    double RefreshLayoutDirectionMutationMilliseconds,
    int RefreshLayoutNeedsMeasureFallbackCount,
    double RefreshLayoutNeedsMeasureFallbackMilliseconds,
    double RefreshLayoutCaptureSnapshotMilliseconds,
    double RefreshLayoutInvalidateArrangeMilliseconds,
    double RefreshLayoutArrangeMilliseconds,
    double RefreshLayoutDirtyBoundsMilliseconds,
    int RefreshLayoutDirtyBoundsHintCount,
    int RefreshLayoutVisualFallbackCount,
    double RefreshLayoutVisualInvalidationMilliseconds);
