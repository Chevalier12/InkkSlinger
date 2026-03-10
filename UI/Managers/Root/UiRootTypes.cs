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
    int DirtyRegionThresholdFallbackCount,
    int ShapeCacheHitCount,
    int ShapeCacheMissCount,
    int TextLayoutCacheHitCount,
    int TextLayoutCacheMissCount);
