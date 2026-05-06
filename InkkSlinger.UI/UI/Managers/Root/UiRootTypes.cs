using System;
using InkkSlinger.UI.Telemetry;

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

public enum UiDirtyDrawDecisionReason
{
    None,
    Partial,
    FullDirty,
    ThresholdFallback,
    NoRegions,
    RetainedDisabled,
    DirtyRegionRenderingDisabled,
    FullRedrawSettle,
    DiagnosticCapture
}

internal enum RetainedInvalidationKind
{
    RenderState,
    LayoutState,
    Structure,
    ScrollViewport
}

internal readonly record struct RetainedInvalidation(
    UIElement? RequestedSource,
    UIElement? EffectiveSource,
    UIElement? RetainedSyncRoot,
    UIElement? DirtyBoundsSource,
    RetainedInvalidationKind Kind,
    bool RequireDeepSync);

internal readonly record struct RetainedDrawThresholds(
    int RegionCountFallbackThreshold,
    double SingleRegionCoverageFallbackThreshold,
    double MultipleRegionCoverageFallbackThreshold);

