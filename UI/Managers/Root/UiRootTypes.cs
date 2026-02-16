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
    int CacheEntryCount,
    long CacheBytes,
    int LastFrameCacheHitCount,
    int LastFrameCacheMissCount,
    int LastFrameCacheRebuildCount,
    UiRedrawReason LastShouldDrawReasons,
    UiRedrawReason LastDrawReasons,
    bool UseRetainedRenderList,
    bool UseDirtyRegionRendering,
    bool UseConditionalDrawScheduling);

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
