using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const bool EnableRetainedRenderListByDefault = true;
    private const bool EnableDirtyRegionRenderingByDefault = true;
    private const bool EnableConditionalDrawByDefault = true;
    private static readonly RasterizerState UiRasterizerState = new()
    {
        ScissorTestEnable = true
    };

    private readonly UIElement _visualRoot;
    private readonly FrameworkElement? _layoutRoot;
    private readonly UiRootVisualIndex _visualIndex = new();
    private readonly List<RenderNode> _retainedRenderList = new();
    private readonly Dictionary<UIElement, int> _renderNodeIndices = new();
    private readonly Queue<UIElement> _dirtyRenderQueue = new();
    private readonly HashSet<UIElement> _dirtyRenderSet = new();
    private readonly HashSet<UIElement> _dirtyRenderRootsRequireDeepSync = new();
    private readonly List<DirtyRenderWorkItem> _dirtyRenderWorkItems = new();
    private readonly List<UIElement> _pendingAncestorMetadataRefreshRoots = new();
    private readonly List<UIElement> _ancestorMetadataRefreshBuffer = new();
    private readonly HashSet<UIElement> _ancestorMetadataRefreshSet = new();
    private readonly List<UIElement> _lastSynchronizedDirtyRenderRoots = new();
    private readonly List<DirtyRenderSpan> _lastSynchronizedDirtyRenderSpans = new();
    private readonly List<UIElement> _dirtyRenderCompactionBuffer = new();
    private readonly List<RenderNode> _activeRetainedDrawPath = new();
    private readonly List<UiUpdatePhase> _lastUpdatePhaseOrder = new(5);
    private readonly DirtyRegionTracker _dirtyRegions = new();
    private readonly InputManager _inputManager = new();
    private readonly InputDispatchState _inputState = new();
    private readonly Dictionary<UIElement, CachedInputConnectionState> _inputConnectionCache = new();
    private readonly Dictionary<UIElement, CachedInputAncestorChain> _inputAncestorCache = new();
    private readonly List<UIElement> _inputAncestorBuilder = new();
    private readonly List<UIElement> _openOverlayVisuals = new();
    private readonly List<ContextMenu> _openContextMenus = new();
    private readonly List<UiIndexedUpdateParticipant> _activeUpdateParticipants = new();
    private UIElement? _cachedClickTarget;
    private UIElement? _cachedPointerResolveTarget;
    private Vector2 _cachedPointerResolvePointerPosition;
    private int _cachedPointerResolveStateStamp;
    private bool _hasCachedPointerResolveTarget;
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
    private double _lastInputPointerMoveDispatchMs;
    private double _lastInputPointerMoveRoutedEventsMs;
    private double _lastInputPointerMoveHandlerMs;
    private double _lastInputPointerMovePreviewEventMs;
    private double _lastInputPointerMoveBubbleEventMs;
    private double _lastInputPointerResolveContextMenuCheckMs;
    private double _lastInputPointerResolveContextMenuOverlayCandidateMs;
    private double _lastInputPointerResolveContextMenuCachedMenuMs;
    private double _lastInputPointerResolveHoverReuseCheckMs;
    private double _lastInputPointerResolveFinalHitTestMs;
    private double _lastInputToolTipLifecycleMs;
    private double _lastInputCommandRequeryMs;
    private double _lastInputKeyDispatchMs;
    private double _lastInputTextDispatchMs;
    private double _lastVisualUpdateMs;
    private int _clickCpuResolveCachedCount;
    private int _clickCpuResolveCapturedCount;
    private int _clickCpuResolveHoveredCount;
    private int _clickCpuResolveHitTestCount;
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
    private int _layoutGeneration;
    private Viewport _lastScheduledViewport;
    private bool _hasLastScheduledViewport;
    private GraphicsDevice? _lastGraphicsDevice;
    private UiRedrawReason _scheduledDrawReasons;
    private bool _forceAnimationActiveForTests;
    private Color _clearColor = Color.CornflowerBlue;
    private int _visualStructureVersion;
    private int _renderStateVersion;
    private int _inputCacheVersion;
    private int _pointerResolveStateVersion;
    private int _visualStructureChangeCount;
    private int _retainedFullRebuildCount;
    private int _retainedSubtreeSyncCount;
    private int _lastRetainedDirtyVisualCount;
    private bool _lastRetainedSyncUsedFullRebuild;
    private int _lastRetainedNodesVisited;
    private int _lastRetainedNodesDrawn;
    private int _lastRetainedClipPushCount;
    private int _lastRetainedTraversalCount;
    private int _lastDirtyRegionTraversalCount;
    private int _lastAncestorMetadataRefreshNodeCount;
    private int _lastSpriteBatchRestartCount;
    private int _dirtyRegionThresholdFallbackCount;
    private int _fullDirtyInitialStateCount;
    private int _fullDirtyViewportChangeCount;
    private int _fullDirtySurfaceResetCount;
    private int _fullDirtyVisualStructureChangeCount;
    private int _fullDirtyRetainedRebuildCount;
    private int _fullDirtyDetachedVisualCount;
    private int _lastFrameUpdateParticipantCount;
    private int _lastFrameUpdateParticipantRefreshCount;
    private double _lastFrameUpdateParticipantRefreshMs;
    private double _lastFrameUpdateParticipantUpdateMs;
    private string _lastHottestFrameUpdateParticipantType = "none";
    private double _lastHottestFrameUpdateParticipantMs;
    private double _lastLayoutMeasureWorkMs;
    private double _lastLayoutMeasureExclusiveWorkMs;
    private double _lastLayoutArrangeWorkMs;
    private string _lastHottestLayoutMeasureElementType = "none";
    private string _lastHottestLayoutMeasureElementName = string.Empty;
    private double _lastHottestLayoutMeasureElementMs;
    private string _lastHottestLayoutArrangeElementType = "none";
    private string _lastHottestLayoutArrangeElementName = string.Empty;
    private double _lastHottestLayoutArrangeElementMs;
    private int _lastDirtyRootCountAfterCoalescing;
    private int _lastMenuScopeBuildCount;
    private int _lastOverlayRegistryScanCount;
    private int _lastOverlayRegistryHitCount;
    private KeyboardMenuScope _activeKeyboardMenuScope;
    private bool _hasActiveKeyboardMenuScope;
    private KeyboardMenuScope _cachedKeyboardMenuScope;
    private bool _hasCachedKeyboardMenuScope;
    private int _cachedKeyboardMenuScopeVisualIndexVersion = -1;
    private UIElement? _cachedKeyboardMenuScopeFocusedElement;
    private bool _activeUpdateParticipantsDirty = true;

    public UiRoot(UIElement visualRoot)
    {
        _visualRoot = visualRoot ?? throw new ArgumentNullException(nameof(visualRoot));
        _layoutRoot = visualRoot as FrameworkElement;
        Automation = new AutomationManager(_visualRoot);
        UseRetainedRenderList = EnableRetainedRenderListByDefault;
        UseDirtyRegionRendering = EnableDirtyRegionRenderingByDefault;
        UseConditionalDrawScheduling = EnableConditionalDrawByDefault;
        MarkFullFrameDirty(UiFullDirtyReason.InitialState);
        EnsureVisualIndexCurrent();
        Current = this;
    }

    public static UiRoot? Current { get; private set; }

    internal UIElement VisualRoot => _visualRoot;

    public AutomationManager Automation { get; }

    public bool UseRetainedRenderList { get; set; }

    public bool UseDirtyRegionRendering { get; set; }

    public bool UseConditionalDrawScheduling { get; set; }

    public bool AlwaysDrawCompatibilityMode { get; set; }

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

    public int VisualStructureChangeCount => _visualStructureChangeCount;

    public int RetainedFullRebuildCount => _retainedFullRebuildCount;

    public int RetainedSubtreeSyncCount => _retainedSubtreeSyncCount;

    public int LastRetainedDirtyVisualCount => _lastRetainedDirtyVisualCount;

    public int DirtyRenderQueueCount => _dirtyRenderQueue.Count;

    public bool HasPendingMeasureInvalidation => _hasMeasureInvalidation;

    public bool HasPendingArrangeInvalidation => _hasArrangeInvalidation;

    public bool HasPendingRenderInvalidation => _hasRenderInvalidation;

    internal string LastPointerResolvePathForDiagnostics => _lastPointerResolvePath;

    internal double LastPointerTargetResolveMsForDiagnostics => _lastInputPointerTargetResolveMs;

    internal int LastInputHitTestCountForDiagnostics => _lastInputHitTestCount;

    internal int LastInputRoutedEventCountForDiagnostics => _lastInputRoutedEventCount;

    internal int LastInputPointerEventCountForDiagnostics => _lastInputPointerEventCount;

    public int LastDirtyRectCount { get; private set; }

    public double LastDirtyAreaPercentage { get; private set; }

    public int FullRedrawFallbackCount => _dirtyRegions.FullRedrawFallbackCount;

    public bool LastDrawUsedPartialRedraw { get; private set; }

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
            LastShouldDrawReasons,
            LastDrawReasons,
            UseRetainedRenderList,
            UseDirtyRegionRendering,
            UseConditionalDrawScheduling,
            RetainedRenderNodeCount,
            GetRetainedHighCostVisualCount(),
            _visualStructureChangeCount,
            _retainedFullRebuildCount,
            _retainedSubtreeSyncCount,
            _lastRetainedDirtyVisualCount);
    }

    internal UiRenderTelemetrySnapshot GetRenderTelemetrySnapshotForTests()
    {
        return new UiRenderTelemetrySnapshot(
            _lastSpriteBatchRestartCount,
            _lastRetainedClipPushCount,
            _lastRetainedNodesVisited,
            _lastRetainedNodesDrawn,
            _lastRetainedTraversalCount,
            _lastDirtyRegionTraversalCount,
            _lastDirtyRootCountAfterCoalescing,
            _dirtyRegionThresholdFallbackCount,
            _fullDirtyInitialStateCount,
            _fullDirtyViewportChangeCount,
            _fullDirtySurfaceResetCount,
            _fullDirtyVisualStructureChangeCount,
            _fullDirtyRetainedRebuildCount,
            _fullDirtyDetachedVisualCount,
            Shape.GetRenderCacheHitCountForTests(),
            Shape.GetRenderCacheMissCountForTests(),
            TextLayout.GetMetricsSnapshot().CacheHitCount,
            TextLayout.GetMetricsSnapshot().CacheMissCount);
    }

    internal UiRootPerformanceTelemetrySnapshot GetPerformanceTelemetrySnapshotForTests()
    {
        return new UiRootPerformanceTelemetrySnapshot(
            LastInputPhaseMs,
            LastBindingPhaseMs,
            LastLayoutPhaseMs,
            LastAnimationPhaseMs,
            LastRenderSchedulingPhaseMs,
            _lastVisualUpdateMs,
            _lastFrameUpdateParticipantCount,
            _lastFrameUpdateParticipantRefreshCount,
            _lastFrameUpdateParticipantRefreshMs,
            _lastFrameUpdateParticipantUpdateMs,
            _lastHottestFrameUpdateParticipantType,
            _lastHottestFrameUpdateParticipantMs,
            _lastLayoutMeasureWorkMs,
            _lastLayoutMeasureExclusiveWorkMs,
            _lastLayoutArrangeWorkMs,
            _lastHottestLayoutMeasureElementType,
            _lastHottestLayoutMeasureElementName,
            _lastHottestLayoutMeasureElementMs,
            _lastHottestLayoutArrangeElementType,
            _lastHottestLayoutArrangeElementName,
            _lastHottestLayoutArrangeElementMs,
            _lastDirtyRootCountAfterCoalescing,
            _lastRetainedTraversalCount,
            _lastAncestorMetadataRefreshNodeCount,
            _lastMenuScopeBuildCount,
            _lastOverlayRegistryScanCount,
            _lastOverlayRegistryHitCount,
            _visualIndex.Version);
    }

    internal UiPointerMoveTelemetrySnapshot GetPointerMoveTelemetrySnapshotForTests()
    {
        return new UiPointerMoveTelemetrySnapshot(
            _lastInputPointerDispatchMs,
            _lastInputPointerTargetResolveMs,
            _lastInputHoverUpdateMs,
            _lastInputPointerRouteMs,
            _lastInputPointerMoveDispatchMs,
            _lastInputPointerMoveRoutedEventsMs,
            _lastInputPointerMoveHandlerMs,
            _lastInputPointerMovePreviewEventMs,
            _lastInputPointerMoveBubbleEventMs,
            _lastInputPointerResolveHoverReuseCheckMs,
            _lastInputPointerResolveFinalHitTestMs,
            _lastInputHitTestCount,
            _lastInputRoutedEventCount,
            _lastInputPointerEventCount,
            _lastPointerResolvePath);
    }

    internal UIElement? GetHoveredElementForDiagnostics()
    {
        return _inputState.HoveredElement;
    }

    internal (int ConnectionCacheEntryCount, int AncestorCacheEntryCount) GetInputCacheEntryCountsForTests()
    {
        return (_inputConnectionCache.Count, _inputAncestorCache.Count);
    }

    internal bool WouldUsePartialDirtyRedrawForTests()
    {
        return ShouldUsePartialDirtyRedraw(_dirtyRegions.RegionCount, _dirtyRegions.GetDirtyAreaCoverage());
    }

    public UiVisualTreeMetricsSnapshot GetVisualTreeMetricsSnapshot()
    {
        var accumulator = new VisualTreeMetricsAccumulator();
        AccumulateVisualTreeMetrics(_visualRoot, depth: 0, ref accumulator);
        return new UiVisualTreeMetricsSnapshot(
            accumulator.VisualCount,
            accumulator.FrameworkElementCount,
            accumulator.HighCostVisualCount,
            accumulator.MaxDepth,
            accumulator.MeasureCallCount,
            accumulator.ArrangeCallCount,
            accumulator.UpdateCallCount,
            accumulator.DrawCallCount,
            accumulator.MeasureInvalidationCount,
            accumulator.ArrangeInvalidationCount,
            accumulator.RenderInvalidationCount);
    }

    internal UiVisualTreeWorkMetricsSnapshot GetVisualTreeWorkMetricsSnapshotForTests()
    {
        var accumulator = new VisualTreeMetricsAccumulator();
        AccumulateVisualTreeMetrics(_visualRoot, depth: 0, ref accumulator);
        return new UiVisualTreeWorkMetricsSnapshot(
            accumulator.MeasureWorkCount,
            accumulator.ArrangeWorkCount);
    }

    public TextLayout.TextLayoutMetricsSnapshot GetTextLayoutMetricsSnapshot()
    {
        return TextLayout.GetMetricsSnapshot();
    }

    public AutomationMetricsSnapshot GetAutomationMetricsSnapshot()
    {
        return Automation.GetMetricsSnapshot();
    }

    internal void ForceFullRedrawForSurfaceReset()
    {
        _hasRenderInvalidation = true;
        _mustDrawNextFrame = true;
        MarkFullFrameDirty(UiFullDirtyReason.SurfaceReset);
    }

    internal void RecordForcedDrawForSurfaceReset()
    {
        if (_scheduledDrawReasons != UiRedrawReason.None)
        {
            return;
        }

        if (DrawSkippedFrameCount > 0)
        {
            DrawSkippedFrameCount--;
        }

        DrawExecutedFrameCount++;
        LastShouldDrawReasons = UiRedrawReason.Resize;
        _scheduledDrawReasons = UiRedrawReason.Resize;
    }

    public void Update(GameTime gameTime, Viewport viewport)
    {
        Dispatcher.VerifyAccess();
        Automation.BeginFrame();
        var updateStart = Stopwatch.GetTimestamp();

        ResetUpdatePhaseState();

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
        Automation.EndFrameAndFlush();
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
        ResetRetainedSyncTrackingState();
    }

    internal void SynchronizeRetainedRenderListForTests()
    {
        SynchronizeRetainedRenderList();
    }

    internal bool IsRenderListFullRebuildPendingForTests()
    {
        return _renderListNeedsFullRebuild;
    }

    internal int GetRetainedNodeSubtreeEndIndexForTests(UIElement visual)
    {
        if (!_renderNodeIndices.TryGetValue(visual, out var nodeIndex))
        {
            throw new ArgumentException("Visual is not tracked in retained render list.", nameof(visual));
        }

        return _retainedRenderList[nodeIndex].SubtreeEndIndexExclusive;
    }

    internal UiRedrawReason GetScheduledDrawReasonsForTests()
    {
        return _scheduledDrawReasons;
    }

    internal void RemoveRetainedNodeIndexForTests(UIElement visual)
    {
        _ = _renderNodeIndices.Remove(visual);
    }

    internal void CompleteDrawStateForTests()
    {
        _hasMeasureInvalidation = false;
        _hasArrangeInvalidation = false;
        _hasRenderInvalidation = false;
        _hasCaretBlinkInvalidation = false;
        _mustDrawNextFrame = false;
        _scheduledDrawReasons = UiRedrawReason.None;
        ResetRetainedSyncTrackingState();
    }

    private void MarkFullFrameDirty(UiFullDirtyReason reason)
    {
        switch (reason)
        {
            case UiFullDirtyReason.InitialState:
                _fullDirtyInitialStateCount++;
                break;
            case UiFullDirtyReason.ViewportChanged:
                _fullDirtyViewportChangeCount++;
                break;
            case UiFullDirtyReason.SurfaceReset:
                _fullDirtySurfaceResetCount++;
                break;
            case UiFullDirtyReason.VisualStructureChanged:
                _fullDirtyVisualStructureChangeCount++;
                break;
            case UiFullDirtyReason.RetainedRebuild:
                _fullDirtyRetainedRebuildCount++;
                break;
            case UiFullDirtyReason.DetachedVisual:
                _fullDirtyDetachedVisualCount++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
        }

        _dirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
    }

    private enum UiFullDirtyReason
    {
        InitialState,
        ViewportChanged,
        SurfaceReset,
        VisualStructureChanged,
        RetainedRebuild,
        DetachedVisual
    }

    internal void ApplyRenderInvalidationCleanupForTests()
    {
        ApplyRenderInvalidationCleanupAfterDraw();
    }

    internal IReadOnlyList<UIElement> GetRetainedDrawOrderForClipForTests(LayoutRect clipRect)
    {
        var visuals = new List<UIElement>();
        AppendRetainedDrawOrderForClip(clipRect, visuals);
        return visuals;
    }

    internal (int NodesVisited, int NodesDrawn, int LocalClipPushCount) GetRetainedTraversalMetricsForClipForTests(LayoutRect clipRect)
    {
        var metrics = TraverseRetainedNodesWithinClip(spriteBatch: null, clipRect);
        return (metrics.NodesVisited, metrics.NodesDrawn, metrics.ClipPushCount);
    }

    internal void SetAnimationActiveForTests(bool isActive)
    {
        _forceAnimationActiveForTests = isActive;
    }

    internal IReadOnlyList<UiUpdatePhase> GetLastUpdatePhaseOrderForTests()
    {
        return new List<UiUpdatePhase>(_lastUpdatePhaseOrder);
    }

    internal void SetFocusedElementForTests(UIElement? element)
    {
        _inputState.FocusedElement = element;
        FocusManager.SetFocus(element);
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

    private int GetRetainedHighCostVisualCount()
    {
        if (_retainedRenderList.Count == 0)
        {
            return 0;
        }

        return _retainedRenderList[0].SubtreeHighCostVisualCount;
    }

    private void EnsureVisualIndexCurrent()
    {
        _visualIndex.EnsureCurrent(_visualRoot);
    }

    private void MarkVisualIndexDirty()
    {
        _visualIndex.MarkDirty();
        BumpInputCacheVersion();
        InvalidateKeyboardMenuScopeCache();
        InvalidateActiveUpdateParticipants();
    }

    private void BumpInputCacheVersion()
    {
        _inputCacheVersion++;
        _inputConnectionCache.Clear();
        _inputAncestorCache.Clear();
    }

    private void InvalidateKeyboardMenuScopeCache()
    {
        _hasCachedKeyboardMenuScope = false;
        _cachedKeyboardMenuScope = default;
        _cachedKeyboardMenuScopeVisualIndexVersion = -1;
        _cachedKeyboardMenuScopeFocusedElement = null;
    }

    private void InvalidateActiveUpdateParticipants()
    {
        _activeUpdateParticipantsDirty = true;
    }

    internal void NotifyMenuStateMutation()
    {
        InvalidateKeyboardMenuScopeCache();
    }

    internal void NotifyOverlayOpened(UIElement overlay)
    {
        RegisterOpenOverlay(overlay);
        InvalidateOverlayInteractionCaches(clearOverlayOwnedHover: true);
    }

    internal void NotifyOverlayClosed(UIElement overlay)
    {
        UnregisterOpenOverlay(overlay);
        InvalidateOverlayInteractionCaches(clearOverlayOwnedHover: true);
    }

    private void RegisterOpenOverlay(UIElement overlay)
    {
        for (var i = 0; i < _openOverlayVisuals.Count; i++)
        {
            if (ReferenceEquals(_openOverlayVisuals[i], overlay))
            {
                goto registerContextMenu;
            }
        }

        _openOverlayVisuals.Add(overlay);

registerContextMenu:
        if (overlay is ContextMenu contextMenu)
        {
            _lastKnownOpenContextMenu = contextMenu;
            for (var i = 0; i < _openContextMenus.Count; i++)
            {
                if (ReferenceEquals(_openContextMenus[i], contextMenu))
                {
                    return;
                }
            }

            _openContextMenus.Add(contextMenu);
        }
    }

    private void UnregisterOpenOverlay(UIElement overlay)
    {
        for (var i = _openOverlayVisuals.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_openOverlayVisuals[i], overlay))
            {
                _openOverlayVisuals.RemoveAt(i);
            }
        }

        if (overlay is ContextMenu contextMenu)
        {
            for (var i = _openContextMenus.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_openContextMenus[i], contextMenu))
                {
                    _openContextMenus.RemoveAt(i);
                }
            }

            if (ReferenceEquals(_lastKnownOpenContextMenu, contextMenu))
            {
                _lastKnownOpenContextMenu = null;
            }
        }
    }

    private void BumpPointerResolveStateVersion()
    {
        _pointerResolveStateVersion++;
    }

    private bool IsElementConnectedToVisualRootCore(UIElement? element)
    {
        return element != null &&
               (ReferenceEquals(element, _visualRoot) ||
                ReferenceEquals(element.GetVisualRoot(), _visualRoot));
    }

    private static void AccumulateVisualTreeMetrics(UIElement visual, int depth, ref VisualTreeMetricsAccumulator accumulator)
    {
        accumulator.VisualCount++;
        accumulator.MaxDepth = Math.Max(accumulator.MaxDepth, depth);
        accumulator.UpdateCallCount += visual.UpdateCallCount;
        accumulator.DrawCallCount += visual.DrawCallCount;
        accumulator.MeasureInvalidationCount += visual.MeasureInvalidationCount;
        accumulator.ArrangeInvalidationCount += visual.ArrangeInvalidationCount;
        accumulator.RenderInvalidationCount += visual.RenderInvalidationCount;
        if (IsHighCostVisual(visual))
        {
            accumulator.HighCostVisualCount++;
        }

        if (visual is FrameworkElement frameworkElement)
        {
            accumulator.FrameworkElementCount++;
            accumulator.MeasureCallCount += frameworkElement.MeasureCallCount;
            accumulator.ArrangeCallCount += frameworkElement.ArrangeCallCount;
            accumulator.MeasureWorkCount += frameworkElement.MeasureWorkCount;
            accumulator.ArrangeWorkCount += frameworkElement.ArrangeWorkCount;
        }

        foreach (var child in visual.GetVisualChildren())
        {
            AccumulateVisualTreeMetrics(child, depth + 1, ref accumulator);
        }
    }

    private struct VisualTreeMetricsAccumulator
    {
        public int VisualCount;
        public int FrameworkElementCount;
        public int HighCostVisualCount;
        public int MaxDepth;
        public long MeasureCallCount;
        public long ArrangeCallCount;
        public long MeasureWorkCount;
        public long ArrangeWorkCount;
        public long UpdateCallCount;
        public long DrawCallCount;
        public long MeasureInvalidationCount;
        public long ArrangeInvalidationCount;
        public long RenderInvalidationCount;
    }

    private readonly record struct CachedInputConnectionState(int Version, bool IsConnected);

    private sealed record CachedInputAncestorChain(int Version, UIElement[] Chain);
}
