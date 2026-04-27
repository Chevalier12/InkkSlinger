namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Core UI framework telemetry snapshots for root, rendering, and performance metrics.
/// </summary>
public readonly record struct UiRootMetricsSnapshot(
    int DrawExecutedFrameCount,
    int DrawSkippedFrameCount,
    int LayoutExecutedFrameCount,
    int LayoutSkippedFrameCount,
    double LastDirtyAreaPercentage,
    int LastDirtyRectCount,
    int FullRedrawFallbackCount,
    UiRedrawReason LastShouldDrawReasons,
    UiRedrawReason LastDrawReasons,
    bool UseRetainedRenderList,
    bool UseDirtyRegionRendering,
    bool UseConditionalDrawScheduling,
    int RetainedRenderNodeCount,
    int RetainedHighCostNodeCount,
    int VisualStructureChangeCount,
    int RetainedFullRebuildCount,
    int RetainedSubtreeSyncCount,
    int LastRetainedDirtyVisualCount);

/// <summary>
/// Visual tree metrics snapshot.
/// </summary>
public readonly record struct UiVisualTreeMetricsSnapshot(
    int VisualCount,
    int FrameworkElementCount,
    int HighCostVisualCount,
    int MaxDepth,
    long MeasureCallCount,
    long ArrangeCallCount,
    long UpdateCallCount,
    long DrawCallCount,
    long MeasureInvalidationCount,
    long ArrangeInvalidationCount,
    long RenderInvalidationCount);

/// <summary>
/// Visual tree work metrics for internal tracking.
/// </summary>
internal readonly record struct UiVisualTreeWorkMetricsSnapshot(
    long MeasureWorkCount,
    long ArrangeWorkCount);

/// <summary>
/// Input metrics snapshot for performance tracking.
/// </summary>
public readonly record struct UiInputMetricsSnapshot(
    double LastInputPhaseMilliseconds,
    double LastInputCaptureMilliseconds,
    double LastInputDispatchMilliseconds,
    double LastInputPointerDispatchMilliseconds,
    double LastInputPointerTargetResolveMilliseconds,
    double LastInputHoverUpdateMilliseconds,
    double LastInputPointerRouteMilliseconds,
    double LastInputKeyDispatchMilliseconds,
    double LastInputTextDispatchMilliseconds,
    double LastVisualUpdateMilliseconds,
    int HitTestCount,
    int RoutedEventCount,
    int KeyEventCount,
    int TextEventCount,
    int PointerEventCount);

/// <summary>
/// Rendering telemetry snapshot.
/// </summary>
internal readonly record struct UiRenderTelemetrySnapshot(
    int SpriteBatchRestartCount,
    double SpriteBatchRestartMilliseconds,
    int ClipPushCount,
    int RetainedNodesVisited,
    int RetainedNodesDrawn,
    int RetainedTraversalCount,
    int DirtyRegionTraversalCount,
    int DirtyRootCount,
    int DirtyRegionThresholdFallbackCount,
    double DrawClearMilliseconds,
    double DrawInitialBatchBeginMilliseconds,
    double DrawVisualTreeMilliseconds,
    double DrawCursorMilliseconds,
    double DrawFinalBatchEndMilliseconds,
    double DrawCleanupMilliseconds,
    int FullDirtyInitialStateCount,
    int FullDirtyViewportChangeCount,
    int FullDirtySurfaceResetCount,
    int FullDirtyVisualStructureChangeCount,
    int FullDirtyRetainedRebuildCount,
    int FullDirtyDetachedVisualCount,
    int ShapeCacheHitCount,
    int ShapeCacheMissCount,
    int TextLayoutCacheHitCount,
    int TextLayoutCacheMissCount,
    UiDirtyDrawDecisionReason LastDirtyDrawDecisionReason,
    int RetainedSyncChangedDirtyDecisionCount,
    int FullRetainedDrawWithoutFullClearCount);

/// <summary>
/// Dirty draw decision snapshot.
/// </summary>
internal readonly record struct UiDirtyDrawDecisionSnapshot(
    UiDirtyDrawDecisionReason BeforeSyncReason,
    UiDirtyDrawDecisionReason AfterSyncReason,
    bool UsePartialClear,
    bool UseFullClear);

/// <summary>
/// Render invalidation debug snapshot.
/// </summary>
internal readonly record struct UiRenderInvalidationDebugSnapshot(
    string RequestedSourceType,
    string RequestedSourceName,
    string RequestedSourceSummary,
    string EffectiveSourceType,
    string EffectiveSourceName,
    string EffectiveSourceSummary,
    string EffectiveSourceResolution,
    string ClipPromotionAncestorType,
    string ClipPromotionAncestorName,
    string RetainedSyncSourceType,
    string RetainedSyncSourceName,
    string RetainedSyncSourceSummary,
    string RetainedSyncSourceResolution,
    string DirtyBoundsVisualType,
    string DirtyBoundsVisualName,
    string DirtyBoundsVisualSummary,
    string DirtyBoundsSourceResolution,
    bool DirtyBoundsUsedHint,
    LayoutRect DirtyBounds,
    bool HasDirtyBounds);

/// <summary>
/// UI root performance telemetry snapshot.
/// </summary>
internal readonly record struct UiRootPerformanceTelemetrySnapshot(
    double InputPhaseMilliseconds,
    double BindingPhaseMilliseconds,
    double LayoutPhaseMilliseconds,
    double AnimationPhaseMilliseconds,
    double RenderSchedulingPhaseMilliseconds,
    double VisualUpdateMilliseconds,
    int FrameUpdateParticipantCount,
    int FrameUpdateParticipantRefreshCount,
    double FrameUpdateParticipantRefreshMilliseconds,
    double FrameUpdateParticipantUpdateMilliseconds,
    string HottestFrameUpdateParticipantType,
    double HottestFrameUpdateParticipantMilliseconds,
    double LayoutMeasureWorkMilliseconds,
    double LayoutMeasureExclusiveWorkMilliseconds,
    double LayoutArrangeWorkMilliseconds,
    string HottestLayoutMeasureElementType,
    string HottestLayoutMeasureElementName,
    double HottestLayoutMeasureElementMilliseconds,
    string HottestLayoutArrangeElementType,
    string HottestLayoutArrangeElementName,
    double HottestLayoutArrangeElementMilliseconds,
    int DirtyRootCount,
    int RetainedTraversalCount,
    int AncestorMetadataRefreshNodeCount,
    double RetainedQueueCompactionMilliseconds,
    double RetainedCandidateCoalescingMilliseconds,
    double RetainedSubtreeUpdateMilliseconds,
    double RetainedShallowSyncMilliseconds,
    double RetainedDeepSyncMilliseconds,
    double RetainedAncestorRefreshMilliseconds,
    int RetainedForceDeepSyncCount,
    int RetainedForcedDeepDowngradeBlockedCount,
    int RetainedShallowSuccessCount,
    int RetainedShallowRejectRenderStateCount,
    int RetainedShallowRejectVisibilityCount,
    int RetainedShallowRejectStructureCount,
    int RetainedOverlapForcedDeepCount,
    int MenuScopeBuildCount,
    int OverlayRegistryScanCount,
    int OverlayRegistryHitCount,
    int VisualIndexVersion);

/// <summary>
/// Pointer move telemetry snapshot.
/// </summary>
internal readonly record struct UiPointerMoveTelemetrySnapshot(
    double PointerDispatchMilliseconds,
    double PointerTargetResolveMilliseconds,
    double HoverUpdateMilliseconds,
    double PointerRouteMilliseconds,
    double PointerMoveDispatchMilliseconds,
    double PointerMoveRoutedEventsMilliseconds,
    double PointerMoveHandlerMilliseconds,
    double PointerMovePreviewEventMilliseconds,
    double PointerMoveBubbleEventMilliseconds,
    double PointerResolveHoverReuseCheckMilliseconds,
    double PointerResolveFinalHitTestMilliseconds,
    int HitTestCount,
    int RoutedEventCount,
    int PointerEventCount,
    string PointerResolvePath);

/// <summary>
/// Freezable invalidation batch snapshot.
/// </summary>
internal readonly record struct UiFreezableInvalidationBatchSnapshot(
    int FlushCount,
    int FlushTargetCount,
    int QueuedTargetCount,
    int MaxPendingTargetCount,
    double FlushMilliseconds,
    string LastFlushTargetSummary);

/// <summary>
/// UiRoot entry-point telemetry snapshot for tracking call counts and elapsed time.
/// </summary>
internal readonly record struct UiRootTelemetrySnapshot(
    int UpdateCallCount,
    double UpdateElapsedMs,
    int EnqueueDeferredOperationCallCount,
    int EnqueueTextInputCallCount,
    int ForceFullRedrawForSurfaceResetCallCount,
    int ForceFullRedrawForDiagnosticsCaptureCallCount,
    int RebuildRetainedRenderListCallCount,
    int SynchronizeRetainedRenderListCallCount,
    int ClearDirtyRenderQueueCallCount,
    int ResetUpdatePhaseStateCallCount);
