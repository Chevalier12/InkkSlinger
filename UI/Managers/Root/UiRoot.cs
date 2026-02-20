using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private static readonly bool EnableRetainedRenderListByDefault =
        !string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RECURSIVE_DRAW_FALLBACK"), "1", StringComparison.Ordinal) &&
        !string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RETAINED_RENDER_QUEUE"), "0", StringComparison.Ordinal);
    private static readonly bool EnableDirtyRegionRenderingByDefault =
        !string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_DIRTY_REGION_RENDERING"), "0", StringComparison.Ordinal);
    private static readonly bool EnableConditionalDrawByDefault =
        !string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_CONDITIONAL_DRAW"), "0", StringComparison.Ordinal);
    private static readonly bool EnableElementRenderCacheByDefault =
        !string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RENDER_CACHE"), "0", StringComparison.Ordinal);
    private static readonly bool EnableRenderCacheBoundaryOverlayByDefault =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RENDER_CACHE_OVERLAY"), "1", StringComparison.Ordinal);
    private static readonly bool EnableRenderCacheCounterTraceByDefault =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RENDER_CACHE_COUNTERS"), "1", StringComparison.Ordinal);
    private static readonly bool ForceBypassMoveHitTest = false;
    private static readonly RasterizerState UiRasterizerState = new()
    {
        ScissorTestEnable = true
    };

    private const int MaxCacheTextureDimension = 2048;
    private const int MaxElementRenderCacheCount = 64;
    private const long MaxElementRenderCacheBytes = 128L * 1024L * 1024L;

    private readonly UIElement _visualRoot;
    private readonly FrameworkElement? _layoutRoot;
    private readonly List<RenderNode> _retainedRenderList = new();
    private readonly Dictionary<UIElement, int> _renderNodeIndices = new();
    private readonly Queue<UIElement> _dirtyRenderQueue = new();
    private readonly HashSet<UIElement> _dirtyRenderSet = new();
    private readonly List<UiUpdatePhase> _lastUpdatePhaseOrder = new(5);
    private readonly DirtyRegionTracker _dirtyRegions = new();
    private readonly IRenderCachePolicy _renderCachePolicy = new DefaultRenderCachePolicy();
    private readonly RenderCacheStore _renderCacheStore = new(MaxElementRenderCacheCount, MaxElementRenderCacheBytes);
    private readonly List<LayoutRect> _lastFrameCachedSubtreeBounds = new();
    private readonly InputManager _inputManager = new();
    private readonly InputDispatchState _inputState = new();
    private UIElement? _cachedClickTarget;
    private UIElement? _lastClickDownTarget;
    private Vector2 _lastClickDownPointerPosition;
    private bool _hasLastClickDownPointerPosition;
    private UIElement? _lastClickUpTarget;
    private Vector2 _lastClickUpPointerPosition;
    private bool _hasLastClickUpPointerPosition;
    private UIElement? _cachedWheelTextInputTarget;
    private ScrollViewer? _cachedWheelScrollViewerTarget;
    private Vector2 _lastWheelPointerPosition;
    private long _lastWheelPreciseRetargetTimestamp;
    private bool _hasLastWheelPointerPosition;
    private int _lastInputHitTestCount;
    private int _lastInputRoutedEventCount;
    private int _lastInputKeyEventCount;
    private int _lastInputTextEventCount;
    private int _lastInputPointerEventCount;
    private double _lastInputCaptureMs;
    private double _lastInputDispatchMs;
    private double _lastInputPointerDispatchMs;
    private double _lastInputPointerTargetResolveMs;
    private double _lastInputHoverUpdateMs;
    private double _lastInputPointerRouteMs;
    private double _lastInputKeyDispatchMs;
    private double _lastInputTextDispatchMs;
    private double _lastVisualUpdateMs;
    private readonly FrameLatencyWindowDiagnostics _scrollFrameLatencyDiagnostics = new("Scroll", coalesceToLatestEventPerDraw: true);
    private readonly FrameLatencyWindowDiagnostics _clickFrameLatencyDiagnostics = new("Click", coalesceToLatestEventPerDraw: false);
    private readonly FrameLatencyWindowDiagnostics _moveFrameLatencyDiagnostics = new("Move", coalesceToLatestEventPerDraw: true);
    private int _scrollCpuWheelEventCount;
    private int _scrollCpuOffsetMutationCount;
    private int _scrollCpuWheelPreciseRetargetCount;
    private int _scrollCpuWheelHitTestCount;
    private int _scrollCpuSampleCount;
    private int _scrollCpuDrawSampleCount;
    private int _scrollCpuDrawExecutedCount;
    private double _scrollCpuUpdateMsTotal;
    private double _scrollCpuInputMsTotal;
    private double _scrollCpuLayoutMsTotal;
    private double _scrollCpuDrawMsTotal;
    private double _scrollCpuPointerRouteMsTotal;
    private double _scrollCpuVisualUpdateMsTotal;
    private double _scrollCpuWheelHandleMsTotal;
    private double _scrollCpuMaxUpdateMs;
    private double _scrollCpuMaxInputMs;
    private double _scrollCpuMaxLayoutMs;
    private double _scrollCpuMaxDrawMs;
    private double _scrollCpuMaxPointerRouteMs;
    private double _scrollCpuMaxVisualUpdateMs;
    private double _scrollCpuMaxWheelHandleMs;
    private long _scrollCpuFirstEventTimestamp;
    private long _scrollCpuLastEventTimestamp;
    private TimeSpan _scrollCpuFirstProcessCpuTime;
    private int _clickCpuPointerDownCount;
    private int _clickCpuPointerUpCount;
    private int _clickCpuHitTestCount;
    private int _clickCpuResolveCachedCount;
    private int _clickCpuResolveCapturedCount;
    private int _clickCpuResolveHoveredCount;
    private int _clickCpuResolveHitTestCount;
    private int _clickCpuSampleCount;
    private int _clickCpuDrawSampleCount;
    private int _clickCpuDrawExecutedCount;
    private double _clickCpuUpdateMsTotal;
    private double _clickCpuInputMsTotal;
    private double _clickCpuLayoutMsTotal;
    private double _clickCpuDrawMsTotal;
    private double _clickCpuPointerRouteMsTotal;
    private double _clickCpuVisualUpdateMsTotal;
    private double _clickCpuHandleMsTotal;
    private double _clickCpuCaptureMsTotal;
    private double _clickCpuDispatchMsTotal;
    private double _clickCpuPointerDispatchMsTotal;
    private double _clickCpuPointerResolveMsTotal;
    private double _clickCpuHoverMsTotal;
    private double _clickCpuMaxUpdateMs;
    private double _clickCpuMaxInputMs;
    private double _clickCpuMaxLayoutMs;
    private double _clickCpuMaxDrawMs;
    private double _clickCpuMaxPointerRouteMs;
    private double _clickCpuMaxVisualUpdateMs;
    private double _clickCpuMaxHandleMs;
    private double _clickCpuMaxCaptureMs;
    private double _clickCpuMaxDispatchMs;
    private double _clickCpuMaxPointerDispatchMs;
    private double _clickCpuMaxPointerResolveMs;
    private double _clickCpuMaxHoverMs;
    private long _clickCpuFirstEventTimestamp;
    private long _clickCpuLastEventTimestamp;
    private TimeSpan _clickCpuFirstProcessCpuTime;
    private int _moveCpuEventCount;
    private int _moveCpuHitTestCount;
    private int _moveCpuSampleCount;
    private int _moveCpuDrawSampleCount;
    private int _moveCpuDrawExecutedCount;
    private double _moveCpuUpdateMsTotal;
    private double _moveCpuInputMsTotal;
    private double _moveCpuCaptureMsTotal;
    private double _moveCpuDispatchMsTotal;
    private double _moveCpuPointerDispatchMsTotal;
    private double _moveCpuPointerResolveMsTotal;
    private double _moveCpuHoverMsTotal;
    private double _moveCpuLayoutMsTotal;
    private double _moveCpuDrawMsTotal;
    private double _moveCpuPointerRouteMsTotal;
    private double _moveCpuVisualUpdateMsTotal;
    private double _moveCpuMaxUpdateMs;
    private double _moveCpuMaxInputMs;
    private double _moveCpuMaxCaptureMs;
    private double _moveCpuMaxDispatchMs;
    private double _moveCpuMaxPointerDispatchMs;
    private double _moveCpuMaxPointerResolveMs;
    private double _moveCpuMaxHoverMs;
    private double _moveCpuMaxLayoutMs;
    private double _moveCpuMaxDrawMs;
    private double _moveCpuMaxPointerRouteMs;
    private double _moveCpuMaxVisualUpdateMs;
    private long _moveCpuFirstEventTimestamp;
    private long _moveCpuLastEventTimestamp;
    private TimeSpan _moveCpuFirstProcessCpuTime;
    private SpriteBatch? _cacheSpriteBatch;
    private long _lastRenderCacheCounterTraceTimestamp;
    private bool _hasMeasureInvalidation;
    private bool _hasArrangeInvalidation;
    private bool _hasRenderInvalidation;
    private bool _hasCaretBlinkInvalidation;
    private bool _mustDrawNextFrame = true;
    private bool _renderListNeedsFullRebuild = true;
    private LayoutRect _lastViewportBounds;
    private bool _hasViewportBounds;
    private Viewport _lastLayoutViewport;
    private bool _hasLastLayoutViewport;
    private bool _hasCompletedInitialLayout;
    private Viewport _lastScheduledViewport;
    private bool _hasLastScheduledViewport;
    private GraphicsDevice? _lastGraphicsDevice;
    private UiRedrawReason _scheduledDrawReasons;
    private bool _forceAnimationActiveForTests;
    private Color _clearColor = Color.CornflowerBlue;

    public UiRoot(UIElement visualRoot)
    {
        _visualRoot = visualRoot ?? throw new ArgumentNullException(nameof(visualRoot));
        _layoutRoot = visualRoot as FrameworkElement;
        UseRetainedRenderList = EnableRetainedRenderListByDefault;
        UseDirtyRegionRendering = EnableDirtyRegionRenderingByDefault;
        UseConditionalDrawScheduling = EnableConditionalDrawByDefault;
        UseElementRenderCaches = EnableElementRenderCacheByDefault;
        ShowCachedSubtreeBoundsOverlay = EnableRenderCacheBoundaryOverlayByDefault;
        TraceRenderCacheCounters = EnableRenderCacheCounterTraceByDefault;
        _dirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
        Current = this;
    }

    public static UiRoot? Current { get; private set; }

    public bool UseRetainedRenderList { get; set; }

    public bool UseDirtyRegionRendering { get; set; }

    public bool UseConditionalDrawScheduling { get; set; }

    public bool UseElementRenderCaches { get; set; }

    public bool ShowCachedSubtreeBoundsOverlay { get; set; }

    public bool TraceRenderCacheCounters { get; set; }

    public bool AlwaysDrawCompatibilityMode { get; set; } =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_ALWAYS_DRAW"), "1", StringComparison.Ordinal);

    public bool UseSoftwareCursor { get; set; }

    public Color SoftwareCursorColor { get; set; } = new(238, 244, 255);

    public float SoftwareCursorSize { get; set; } = 13f;

    public Color ClearColor
    {
        get => _clearColor;
        set => _clearColor = value;
    }

    public double LastUpdateMs { get; private set; }

    public double LastInputPhaseMs { get; private set; }

    public double LastBindingPhaseMs { get; private set; }

    public double LastLayoutPhaseMs { get; private set; }

    public double LastAnimationPhaseMs { get; private set; }

    public double LastRenderSchedulingPhaseMs { get; private set; }

    public int LastDeferredOperationCount { get; private set; }

    public IReadOnlyList<UiUpdatePhase> LastUpdatePhaseOrder => _lastUpdatePhaseOrder;

    public int PendingDeferredOperationCount => Dispatcher.PendingDeferredOperationCount;

    public double LastDrawMs { get; private set; }

    public int DrawCalls { get; private set; }

    public int DrawExecutedFrameCount { get; private set; }

    public int DrawSkippedFrameCount { get; private set; }

    public int LayoutPasses { get; private set; }

    public int LayoutExecutedFrameCount { get; private set; }

    public int LayoutSkippedFrameCount { get; private set; }

    public int MeasureInvalidationCount { get; private set; }

    public int ArrangeInvalidationCount { get; private set; }

    public int RenderInvalidationCount { get; private set; }

    public int RetainedRenderNodeCount => _retainedRenderList.Count;

    public int DirtyRenderQueueCount => _dirtyRenderQueue.Count;

    public bool HasPendingMeasureInvalidation => _hasMeasureInvalidation;

    public bool HasPendingArrangeInvalidation => _hasArrangeInvalidation;

    public bool HasPendingRenderInvalidation => _hasRenderInvalidation;

    public int LastDirtyRectCount { get; private set; }

    public double LastDirtyAreaPercentage { get; private set; }

    public int FullRedrawFallbackCount => _dirtyRegions.FullRedrawFallbackCount;

    public bool LastDrawUsedPartialRedraw { get; private set; }

    public int CacheHitCount { get; private set; }

    public int CacheMissCount { get; private set; }

    public int CacheRebuildCount { get; private set; }

    public int LastFrameCacheHitCount { get; private set; }

    public int LastFrameCacheMissCount { get; private set; }

    public int LastFrameCacheRebuildCount { get; private set; }

    public int CacheEntryCount => _renderCacheStore.Count;

    public long CacheBytes => _renderCacheStore.TotalBytes;

    public UiRedrawReason LastShouldDrawReasons { get; private set; }

    public UiRedrawReason LastDrawReasons { get; private set; }

    public UiInputMetricsSnapshot GetInputMetricsSnapshot()
    {
        return new UiInputMetricsSnapshot(
            LastInputPhaseMs,
            _lastInputCaptureMs,
            _lastInputDispatchMs,
            _lastInputPointerDispatchMs,
            _lastInputPointerTargetResolveMs,
            _lastInputHoverUpdateMs,
            _lastInputPointerRouteMs,
            _lastInputKeyDispatchMs,
            _lastInputTextDispatchMs,
            _lastVisualUpdateMs,
            _lastInputHitTestCount,
            _lastInputRoutedEventCount,
            _lastInputKeyEventCount,
            _lastInputTextEventCount,
            _lastInputPointerEventCount);
    }

    public UiRootMetricsSnapshot GetMetricsSnapshot()
    {
        return new UiRootMetricsSnapshot(
            DrawExecutedFrameCount,
            DrawSkippedFrameCount,
            LayoutExecutedFrameCount,
            LayoutSkippedFrameCount,
            LastDirtyAreaPercentage,
            LastDirtyRectCount,
            FullRedrawFallbackCount,
            CacheEntryCount,
            CacheBytes,
            LastFrameCacheHitCount,
            LastFrameCacheMissCount,
            LastFrameCacheRebuildCount,
            LastShouldDrawReasons,
            LastDrawReasons,
            UseRetainedRenderList,
            UseDirtyRegionRendering,
            UseConditionalDrawScheduling);
    }

    public void Update(GameTime gameTime, Viewport viewport)
    {
        Dispatcher.VerifyAccess();
        var updateStart = Stopwatch.GetTimestamp();

        ResetUpdatePhaseDiagnostics();

        LastInputPhaseMs = ExecuteUpdatePhase(
            UiUpdatePhase.InputAndEvents,
            () => RunInputAndEventsPhase(gameTime));
        LastBindingPhaseMs = ExecuteUpdatePhase(
            UiUpdatePhase.BindingAndDeferred,
            RunBindingAndDeferredPhase);
        LastLayoutPhaseMs = ExecuteUpdatePhase(
            UiUpdatePhase.Layout,
            () => RunLayoutPhase(viewport));
        LastAnimationPhaseMs = ExecuteUpdatePhase(
            UiUpdatePhase.Animation,
            () => RunAnimationPhase(gameTime));
        LastRenderSchedulingPhaseMs = ExecuteUpdatePhase(
            UiUpdatePhase.RenderScheduling,
            () => RunRenderSchedulingPhase(viewport));

        LastUpdateMs = Stopwatch.GetElapsedTime(updateStart).TotalMilliseconds;
        ObserveScrollCpuAfterUpdate();
        ObserveClickCpuAfterUpdate();
        ObserveMoveCpuAfterUpdate();
        ObserveFrameLatencyAfterUpdate();
        ObserveDirtyRegionAfterUpdate();
        ObserveRenderCacheChurnAfterUpdate();
        ObserveAllocationGcAfterUpdate();
        ObserveInputRouteComplexityAfterUpdate();
        ObserveNoOpInvalidationAfterUpdate();
        ObserveControlHotspotAfterUpdate();
    }

    public void EnqueueDeferredOperation(Action operation)
    {
        Dispatcher.EnqueueDeferred(operation);
    }

    public void EnqueueTextInput(char character)
    {
        _inputManager.EnqueueTextInput(character);
    }


    internal void RebuildRenderListForTests()
    {
        RebuildRetainedRenderList();
    }

    internal IReadOnlyList<UIElement> GetRetainedVisualOrderForTests()
    {
        var visuals = new List<UIElement>(_retainedRenderList.Count);
        for (var i = 0; i < _retainedRenderList.Count; i++)
        {
            visuals.Add(_retainedRenderList[i].Visual);
        }

        return visuals;
    }

    internal IReadOnlyList<UIElement> GetDirtyRenderQueueSnapshotForTests()
    {
        return new List<UIElement>(_dirtyRenderQueue);
    }

    internal IReadOnlyList<LayoutRect> GetDirtyRegionsSnapshotForTests()
    {
        return new List<LayoutRect>(_dirtyRegions.Regions);
    }

    internal bool IsFullDirtyForTests()
    {
        return _dirtyRegions.IsFullFrameDirty;
    }

    internal double GetDirtyCoverageForTests()
    {
        return _dirtyRegions.GetDirtyAreaCoverage();
    }

    internal void SetDirtyRegionViewportForTests(LayoutRect viewport)
    {
        _dirtyRegions.SetViewport(viewport);
    }

    internal void ResetDirtyStateForTests()
    {
        _dirtyRegions.Clear();
        ClearDirtyRenderQueue();
    }

    internal void SynchronizeRetainedRenderListForTests()
    {
        SynchronizeRetainedRenderList();
    }

    internal void CompleteDrawStateForTests()
    {
        _hasMeasureInvalidation = false;
        _hasArrangeInvalidation = false;
        _hasRenderInvalidation = false;
        _hasCaretBlinkInvalidation = false;
        _mustDrawNextFrame = false;
        _scheduledDrawReasons = UiRedrawReason.None;
    }

    internal void SetAnimationActiveForTests(bool isActive)
    {
        _forceAnimationActiveForTests = isActive;
    }

    internal IReadOnlyList<UiUpdatePhase> GetLastUpdatePhaseOrderForTests()
    {
        return new List<UiUpdatePhase>(_lastUpdatePhaseOrder);
    }

    internal bool TryGetRenderCacheContextForTests(UIElement visual, out RenderCachePolicyContext context)
    {
        context = default;
        if (!_renderNodeIndices.TryGetValue(visual, out var renderNodeIndex))
        {
            return false;
        }

        context = CreateRenderCacheContext(_retainedRenderList[renderNodeIndex]);
        return true;
    }

    internal bool CanCacheVisualForTests(UIElement visual)
    {
        if (!TryGetRenderCacheContextForTests(visual, out var context))
        {
            return false;
        }

        return _renderCachePolicy.CanCache(visual, context);
    }

    internal bool TryGetLastPointerResolveHitTestMetricsForTests(out HitTestMetrics metrics)
    {
        if (_lastPointerResolveHitTestMetrics.HasValue)
        {
            metrics = _lastPointerResolveHitTestMetrics.Value;
            return true;
        }

        metrics = default;
        return false;
    }

}
