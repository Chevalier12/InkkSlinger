using System;

namespace InkkSlinger;

public enum UiInvalidationType
{
    Measure,
    Arrange,
    Render
}

[Flags]
public enum UiRedrawReason
{
    None = 0,
    LayoutInvalidated = 1 << 0,
    RenderInvalidated = 1 << 1,
    AnimationActive = 1 << 2,
    CaretBlinkActive = 1 << 3,
    Resize = 1 << 4
}

public enum UiUpdatePhase
{
    InputAndEvents,
    BindingAndDeferred,
    Layout,
    Animation,
    RenderScheduling
}

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

internal readonly record struct UiVisualTreeWorkMetricsSnapshot(
    long MeasureWorkCount,
    long ArrangeWorkCount);

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

internal readonly record struct UiRenderTelemetrySnapshot(
    int SpriteBatchRestartCount,
    int ClipPushCount,
    int RetainedNodesVisited,
    int RetainedNodesDrawn,
    int RetainedTraversalCount,
    int DirtyRegionTraversalCount,
    int DirtyRootCount,
    int DirtyRegionThresholdFallbackCount,
    int FullDirtyInitialStateCount,
    int FullDirtyViewportChangeCount,
    int FullDirtySurfaceResetCount,
    int FullDirtyVisualStructureChangeCount,
    int FullDirtyRetainedRebuildCount,
    int FullDirtyDetachedVisualCount,
    int ShapeCacheHitCount,
    int ShapeCacheMissCount,
    int TextLayoutCacheHitCount,
    int TextLayoutCacheMissCount);

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
    int MenuScopeBuildCount,
    int OverlayRegistryScanCount,
    int OverlayRegistryHitCount,
    int VisualIndexVersion);

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

internal readonly record struct AnimationTelemetrySnapshot(
    int BeginStoryboardCallCount,
    int StoryboardStartCount,
    int ActiveStoryboardCount,
    int ActiveLaneCount,
    int ActiveStoryboardEntryCount,
    int ComposePassCount,
    int LaneApplicationCount,
    int SinkValueSetCount,
    int ClearedLaneCount,
    double BeginStoryboardMilliseconds,
    double StoryboardStartMilliseconds,
    double StoryboardUpdateMilliseconds,
    double ComposeMilliseconds,
    double ComposeCollectMilliseconds,
    double ComposeSortMilliseconds,
    double ComposeMergeMilliseconds,
    double ComposeApplyMilliseconds,
    double ComposeBatchBeginMilliseconds,
    double ComposeBatchEndMilliseconds,
    double SinkSetValueMilliseconds,
    double CleanupCompletedMilliseconds,
    string HottestSetValuePathSummary,
    string HottestSetValueWriteSummary,
    double HottestSetValueWriteMilliseconds);

internal readonly record struct AnimationSinkTelemetrySnapshot(
    int DependencyPropertySetValueCount,
    double DependencyPropertySetValueMilliseconds,
    int ClrPropertySetValueCount,
    double ClrPropertySetValueMilliseconds);

internal readonly record struct UiFreezableInvalidationBatchSnapshot(
    int FlushCount,
    int FlushTargetCount,
    int QueuedTargetCount,
    int MaxPendingTargetCount,
    double FlushMilliseconds);
