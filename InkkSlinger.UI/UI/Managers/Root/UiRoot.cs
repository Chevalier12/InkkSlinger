using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const bool EnableRetainedRenderListByDefault = true;
    private const bool EnableDirtyRegionRenderingByDefault = true;
    private const bool EnableConditionalDrawByDefault = true;
    private const int InputCacheTrimFloor = 256;
    private const int FullRedrawSettleFrameCountAfterResize = 6;
    private static readonly List<WeakReference<UiRoot>> RegisteredRoots = new();
    private static readonly RasterizerState UiRasterizerState = new()
    {
        ScissorTestEnable = true
    };

    private readonly UIElement _visualRoot;
    private readonly FrameworkElement? _layoutRoot;
    private readonly RetainedRenderController _retainedRender;
    private readonly UiRootVisualIndex _visualIndex = new();
    private readonly List<UiUpdatePhase> _lastUpdatePhaseOrder = new(5);
    private readonly InputManager _inputManager = new();
    private readonly InputDispatchState _inputState = new();
    private readonly Dictionary<UIElement, CachedInputConnectionState> _inputConnectionCache = new();
    private readonly Dictionary<UIElement, CachedInputAncestorChain> _inputAncestorCache = new();
    private readonly List<UIElement> _inputAncestorBuilder = new();
    private readonly Stack<UIElement> _inputCacheInvalidationTraversalStack = new();
    private readonly HashSet<UIElement> _inputCacheInvalidationVisited = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<FrameworkElement, LayoutElementSample> _layoutSamplesBeforeMeasure = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<FrameworkElement, LayoutElementSample> _layoutSamplesAfterMeasure = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<FrameworkElement, LayoutElementSample> _layoutSamplesAfterArrange = new(ReferenceEqualityComparer.Instance);
    private readonly Stack<UIElement> _layoutSampleTraversalStack = new();
    private readonly List<UIElement> _openOverlayVisuals = new();
    private readonly List<ContextMenu> _openContextMenus = new();
    private readonly List<UiIndexedUpdateParticipant> _activeUpdateParticipants = new();
    private InputPhaseTelemetryState _inputTelemetry;
    private UIElement? _cachedClickTarget;
    private UIElement? _cachedPointerResolveTarget;
    private Vector2 _cachedPointerResolvePointerPosition;
    private int _cachedPointerResolveStateStamp;
    private bool _hasCachedPointerResolveTarget;
    private UIElement? _lastClickDownTarget;
    private string _lastClickDownResolvePath = "None";
    private Vector2 _lastClickDownPointerPosition;
    private bool _hasLastClickDownPointerPosition;
    private UIElement? _lastClickUpTarget;
    private string _lastClickUpResolvePath = "None";
    private Vector2 _lastClickUpPointerPosition;
    private bool _hasLastClickUpPointerPosition;
    private UIElement? _cachedWheelTextInputTarget;
    private ScrollViewer? _cachedWheelScrollViewerTarget;
    private Vector2 _lastWheelPointerPosition;
    private long _lastWheelPreciseRetargetTimestamp;
    private bool _hasLastWheelPointerPosition;
    private double _lastVisualUpdateMs;
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
    private bool _useRetainedRenderList;
    private Color _clearColor = Color.CornflowerBlue;
    private int _visualStructureVersion;
    private int _renderStateVersion;
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
    private double _lastRetainedQueueCompactionMs;
    private double _lastRetainedCandidateCoalescingMs;
    private double _lastRetainedSubtreeUpdateMs;
    private double _lastRetainedShallowSyncMs;
    private double _lastRetainedDeepSyncMs;
    private double _lastRetainedAncestorRefreshMs;
    private int _lastRetainedForceDeepSyncCount;
    private int _lastRetainedForcedDeepDowngradeBlockedCount;
    private int _lastRetainedShallowSuccessCount;
    private int _lastRetainedShallowRejectRenderStateCount;
    private int _lastRetainedShallowRejectVisibilityCount;
    private int _lastRetainedShallowRejectStructureCount;
    private int _lastRetainedOverlapForcedDeepCount;
    private int _lastSpriteBatchRestartCount;
    private double _lastSpriteBatchRestartMs;
    private double _lastDrawClearMs;
    private double _lastDrawInitialBatchBeginMs;
    private double _lastDrawVisualTreeMs;
    private double _lastDrawCursorMs;
    private double _lastDrawFinalBatchEndMs;
    private double _lastDrawCleanupMs;
    private int _dirtyRegionThresholdFallbackCount;
    private UiDirtyDrawDecisionReason _lastDirtyDrawDecisionReason = UiDirtyDrawDecisionReason.None;
    private int _retainedSyncChangedDirtyDecisionCount;
    private int _fullRetainedDrawWithoutFullClearCount;
    private bool _currentDrawPerformedFullClear;
    private bool _diagnosticCaptureFullClearPending;
    private int _fullDirtyInitialStateCount;
    private int _fullDirtyViewportChangeCount;
    private int _fullDirtySurfaceResetCount;
    private int _fullDirtyVisualStructureChangeCount;
    private int _fullDirtyRetainedRebuildCount;
    private int _fullDirtyDetachedVisualCount;
    private int _fullRedrawSettleFramesRemaining;
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
    private UIElement? _lastRenderInvalidationRequestedSourceElement;
    private string _lastRenderInvalidationRequestedSourceType = "none";
    private string _lastRenderInvalidationRequestedSourceName = string.Empty;
    private UIElement? _lastRenderInvalidationEffectiveSourceElement;
    private string _lastRenderInvalidationEffectiveSourceType = "none";
    private string _lastRenderInvalidationEffectiveSourceName = string.Empty;
    private string _lastRenderInvalidationEffectiveSourceResolution = "none";
    private string _lastRenderInvalidationClipPromotionAncestorType = "none";
    private string _lastRenderInvalidationClipPromotionAncestorName = string.Empty;
    private UIElement? _lastRenderInvalidationRetainedSyncSourceElement;
    private string _lastRenderInvalidationRetainedSyncSourceType = "none";
    private string _lastRenderInvalidationRetainedSyncSourceName = string.Empty;
    private string _lastRenderInvalidationRetainedSyncSourceResolution = "none";
    private UIElement? _lastDirtyBoundsVisualElement;
    private string _lastDirtyBoundsVisualType = "none";
    private string _lastDirtyBoundsVisualName = string.Empty;
    private string _lastDirtyBoundsSourceResolution = "none";
    private bool _lastDirtyBoundsUsedHint;
    private LayoutRect _lastDirtyBounds;
    private bool _hasLastDirtyBounds;
    private readonly List<string> _dirtyBoundsEventTrace = new();
    private KeyboardMenuScope _activeKeyboardMenuScope;
    private bool _hasActiveKeyboardMenuScope;
    private KeyboardMenuScope _cachedKeyboardMenuScope;
    private bool _hasCachedKeyboardMenuScope;
    private int _cachedKeyboardMenuScopeVisualIndexVersion = -1;
    private UIElement? _cachedKeyboardMenuScopeFocusedElement;
    private bool _activeUpdateParticipantsDirty = true;

    // Entry-point telemetry
    private long _telemetryUpdateCallCount;
    private long _telemetryUpdateElapsedTicks;
    private long _telemetryEnqueueDeferredOperationCallCount;
    private long _telemetryEnqueueTextInputCallCount;
    private long _telemetryForceFullRedrawForSurfaceResetCallCount;
    private long _telemetryForceFullRedrawForDiagnosticsCaptureCallCount;
    private long _telemetryRebuildRetainedRenderListCallCount;
    private long _telemetrySynchronizeRetainedRenderListCallCount;
    private long _telemetryClearDirtyRenderQueueCallCount;
    private long _telemetryResetUpdatePhaseStateCallCount;

    public UiRoot(UIElement visualRoot)
    {
        _visualRoot = visualRoot ?? throw new ArgumentNullException(nameof(visualRoot));
        _layoutRoot = visualRoot as FrameworkElement;
        _retainedRender = new RetainedRenderController(this);
        Automation = new AutomationManager(_visualRoot);
        _useRetainedRenderList = EnableRetainedRenderListByDefault;
        UseDirtyRegionRendering = EnableDirtyRegionRenderingByDefault;
        UseConditionalDrawScheduling = EnableConditionalDrawByDefault;
        MarkFullFrameDirty(UiFullDirtyReason.InitialState);
        EnsureVisualIndexCurrent();
        RegisterRoot(this);
        Current = this;
    }

    public static UiRoot? Current { get; private set; }

    internal static void ResetForTests()
    {
        RegisteredRoots.Clear();
        Current = null;
    }

    private static void RegisterRoot(UiRoot root)
    {
        for (var i = RegisteredRoots.Count - 1; i >= 0; i--)
        {
            if (!RegisteredRoots[i].TryGetTarget(out var registeredRoot))
            {
                RegisteredRoots.RemoveAt(i);
                continue;
            }

            if (ReferenceEquals(registeredRoot, root))
            {
                return;
            }
        }

        RegisteredRoots.Add(new WeakReference<UiRoot>(root));
    }

    private static void UnregisterRoot(UiRoot root)
    {
        for (var i = RegisteredRoots.Count - 1; i >= 0; i--)
        {
            if (!RegisteredRoots[i].TryGetTarget(out var registeredRoot) ||
                ReferenceEquals(registeredRoot, root))
            {
                RegisteredRoots.RemoveAt(i);
            }
        }
    }

    private UiRoot ResolveVisualStructureNotificationRoot(
        UIElement element,
        UIElement? oldParent,
        UIElement? newParent)
    {
        if (OwnsVisualStructureNotification(element, oldParent, newParent))
        {
            return this;
        }

        for (var i = RegisteredRoots.Count - 1; i >= 0; i--)
        {
            if (!RegisteredRoots[i].TryGetTarget(out var candidate))
            {
                RegisteredRoots.RemoveAt(i);
                continue;
            }

            if (candidate.OwnsVisualStructureNotification(element, oldParent, newParent))
            {
                return candidate;
            }
        }

        return this;
    }

    internal static void NotifyVisualStructureChangedForOwner(
        UIElement element,
        UIElement? oldParent,
        UIElement? newParent)
    {
        var elementRoot = element.GetVisualRoot();
        var oldParentRoot = oldParent?.GetVisualRoot();
        var newParentRoot = newParent?.GetVisualRoot();
        for (var i = RegisteredRoots.Count - 1; i >= 0; i--)
        {
            if (!RegisteredRoots[i].TryGetTarget(out var candidate))
            {
                RegisteredRoots.RemoveAt(i);
                continue;
            }

            if (ReferenceEquals(element, candidate._visualRoot) ||
                ReferenceEquals(elementRoot, candidate._visualRoot) ||
                ReferenceEquals(oldParentRoot, candidate._visualRoot) ||
                ReferenceEquals(newParentRoot, candidate._visualRoot))
            {
                candidate.NotifyVisualStructureChanged(element, oldParent, newParent);
                return;
            }
        }
    }

    private bool OwnsVisualStructureNotification(
        UIElement element,
        UIElement? oldParent,
        UIElement? newParent)
    {
        return ReferenceEquals(element, _visualRoot) ||
               IsElementConnectedToVisualRootCore(element) ||
               IsElementConnectedToVisualRootCore(oldParent) ||
               IsElementConnectedToVisualRootCore(newParent);
    }

    internal UIElement VisualRoot => _visualRoot;

    public AutomationManager Automation { get; }

    public bool UseRetainedRenderList
    {
        get => _useRetainedRenderList;
        set
        {
            if (_useRetainedRenderList == value)
            {
                return;
            }

            _useRetainedRenderList = value;
            if (value)
            {
                _renderListNeedsFullRebuild = true;
                _mustDrawNextFrame = true;
                MarkFullFrameDirty(UiFullDirtyReason.RetainedRebuild);
            }
        }
    }

    public bool UseDirtyRegionRendering { get; set; }

    public bool UseConditionalDrawScheduling { get; set; }

    public bool AlwaysDrawCompatibilityMode { get; set; }

    private int _dirtyRegionCountFallbackThreshold = 4;
    private double _dirtyRegionCoverageFallbackThreshold = 0.20d;
    private double _dirtyRegionCoverageFallbackThresholdForMultipleRegions = 0.12d;

    public int DirtyRegionCountFallbackThreshold
    {
        get => _dirtyRegionCountFallbackThreshold;
        set => _dirtyRegionCountFallbackThreshold = Math.Max(1, value);
    }

    public double DirtyRegionCoverageFallbackThreshold
    {
        get => _dirtyRegionCoverageFallbackThreshold;
        set => _dirtyRegionCoverageFallbackThreshold = ClampDirtyRegionCoverageFallbackThreshold(
            value,
            _dirtyRegionCoverageFallbackThreshold);
    }

    public double DirtyRegionCoverageFallbackThresholdForMultipleRegions
    {
        get => _dirtyRegionCoverageFallbackThresholdForMultipleRegions;
        set => _dirtyRegionCoverageFallbackThresholdForMultipleRegions = ClampDirtyRegionCoverageFallbackThreshold(
            value,
            _dirtyRegionCoverageFallbackThresholdForMultipleRegions);
    }

    private static double ClampDirtyRegionCoverageFallbackThreshold(double value, double fallback)
    {
        return double.IsNaN(value) ? fallback : Math.Clamp(value, 0d, 1d);
    }

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

    internal GraphicsDevice? LastGraphicsDeviceForResources => _lastGraphicsDevice;

    public int DrawSkippedFrameCount { get; private set; }

    public int LayoutPasses { get; private set; }

    public int LayoutExecutedFrameCount { get; private set; }

    public int LayoutSkippedFrameCount { get; private set; }

    public int MeasureInvalidationCount { get; private set; }

    public int ArrangeInvalidationCount { get; private set; }

    public int RenderInvalidationCount { get; private set; }

    public int RetainedRenderNodeCount => _retainedRender.NodeCount;

    public int VisualStructureChangeCount => _visualStructureChangeCount;

    public int RetainedFullRebuildCount => _retainedFullRebuildCount;

    public int RetainedSubtreeSyncCount => _retainedSubtreeSyncCount;

    public int LastRetainedDirtyVisualCount => _lastRetainedDirtyVisualCount;

    public int DirtyRenderQueueCount => _retainedRender.DirtyQueueCount;

    public bool HasPendingMeasureInvalidation => _hasMeasureInvalidation;

    public bool HasPendingArrangeInvalidation => _hasArrangeInvalidation;

    public bool HasPendingRenderInvalidation => _hasRenderInvalidation;

    internal bool HasPendingForcedDrawForInkkOops => _mustDrawNextFrame;

    internal string LastPointerResolvePathForDiagnostics => _lastPointerResolvePath;

    internal string LastClickDownResolvePathForDiagnostics => _lastClickDownResolvePath;

    internal string LastClickUpResolvePathForDiagnostics => _lastClickUpResolvePath;

    internal double LastPointerTargetResolveMsForDiagnostics => _lastInputPointerTargetResolveMs;

    internal int LastInputHitTestCountForDiagnostics => _lastInputHitTestCount;

    internal int LastInputRoutedEventCountForDiagnostics => _lastInputRoutedEventCount;

    internal int LastInputPointerEventCountForDiagnostics => _lastInputPointerEventCount;

    public int LastDirtyRectCount { get; private set; }

    public double LastDirtyAreaPercentage { get; private set; }

    public int FullRedrawFallbackCount => _retainedRender.FullRedrawFallbackCount;

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
            _retainedRender.HighCostVisualCount,
            _visualStructureChangeCount,
            _retainedFullRebuildCount,
            _retainedSubtreeSyncCount,
            _lastRetainedDirtyVisualCount);
    }

    internal UiRenderTelemetrySnapshot GetRenderTelemetrySnapshotForTests()
    {
        var retainedSnapshot = _retainedRender.GetTelemetrySnapshot();
        return new UiRenderTelemetrySnapshot(
            _lastSpriteBatchRestartCount,
            _lastSpriteBatchRestartMs,
            _lastRetainedClipPushCount,
            retainedSnapshot.NodesVisited,
            retainedSnapshot.NodesDrawn,
            retainedSnapshot.RetainedTraversalCount,
            retainedSnapshot.DirtyRegionTraversalCount,
            retainedSnapshot.DirtyRootCount,
            retainedSnapshot.ThresholdFallbackCount,
            _lastDrawClearMs,
            _lastDrawInitialBatchBeginMs,
            _lastDrawVisualTreeMs,
            _lastDrawCursorMs,
            _lastDrawFinalBatchEndMs,
            _lastDrawCleanupMs,
            _fullDirtyInitialStateCount,
            _fullDirtyViewportChangeCount,
            _fullDirtySurfaceResetCount,
            _fullDirtyVisualStructureChangeCount,
            _fullDirtyRetainedRebuildCount,
            _fullDirtyDetachedVisualCount,
            Shape.GetRenderCacheHitCountForTests(),
            Shape.GetRenderCacheMissCountForTests(),
            TextLayout.GetMetricsSnapshot().CacheHitCount,
            TextLayout.GetMetricsSnapshot().CacheMissCount,
            retainedSnapshot.LastDirtyDrawDecisionReason,
            _retainedSyncChangedDirtyDecisionCount,
            _fullRetainedDrawWithoutFullClearCount);
    }

    internal RetainedRenderControllerTelemetrySnapshot GetRetainedRenderControllerTelemetrySnapshotForTests()
    {
        return _retainedRender.GetTelemetrySnapshot();
    }

    internal UiRenderInvalidationDebugSnapshot GetRenderInvalidationDebugSnapshotForTests()
    {
        return new UiRenderInvalidationDebugSnapshot(
            _lastRenderInvalidationRequestedSourceType,
            _lastRenderInvalidationRequestedSourceName,
            GetLastRenderInvalidationSummary(_lastRenderInvalidationRequestedSourceElement),
            _lastRenderInvalidationEffectiveSourceType,
            _lastRenderInvalidationEffectiveSourceName,
            GetLastRenderInvalidationSummary(_lastRenderInvalidationEffectiveSourceElement),
            _lastRenderInvalidationEffectiveSourceResolution,
            _lastRenderInvalidationClipPromotionAncestorType,
            _lastRenderInvalidationClipPromotionAncestorName,
            _lastRenderInvalidationRetainedSyncSourceType,
            _lastRenderInvalidationRetainedSyncSourceName,
            GetLastRenderInvalidationSummary(_lastRenderInvalidationRetainedSyncSourceElement),
            _lastRenderInvalidationRetainedSyncSourceResolution,
            _lastDirtyBoundsVisualType,
            _lastDirtyBoundsVisualName,
            GetLastRenderInvalidationSummary(_lastDirtyBoundsVisualElement),
            _lastDirtyBoundsSourceResolution,
            _lastDirtyBoundsUsedHint,
            _lastDirtyBounds,
            _hasLastDirtyBounds);
    }

    private static string GetLastRenderInvalidationSummary(UIElement? element)
    {
        if (element == null)
        {
            return "none";
        }

        var summary = element.InvalidationDiagnosticsForTests.LastRenderInvalidationSummary;
        return string.IsNullOrWhiteSpace(summary)
            ? "none"
            : summary;
    }

    internal IReadOnlyList<string> GetDirtyBoundsEventTraceForTests()
    {
        return new List<string>(_dirtyBoundsEventTrace);
    }

    internal void ClearDirtyBoundsEventTraceForTests()
    {
        _dirtyBoundsEventTrace.Clear();
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
            _lastRetainedQueueCompactionMs,
            _lastRetainedCandidateCoalescingMs,
            _lastRetainedSubtreeUpdateMs,
            _lastRetainedShallowSyncMs,
            _lastRetainedDeepSyncMs,
            _lastRetainedAncestorRefreshMs,
            _lastRetainedForceDeepSyncCount,
            _lastRetainedForcedDeepDowngradeBlockedCount,
            _lastRetainedShallowSuccessCount,
            _lastRetainedShallowRejectRenderStateCount,
            _lastRetainedShallowRejectVisibilityCount,
            _lastRetainedShallowRejectStructureCount,
            _lastRetainedOverlapForcedDeepCount,
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

    internal UiRootTelemetrySnapshot GetUiRootTelemetrySnapshot()
    {
        return new UiRootTelemetrySnapshot(
            (int)_telemetryUpdateCallCount,
            _telemetryUpdateElapsedTicks * 1_000.0 / Stopwatch.Frequency,
            (int)_telemetryEnqueueDeferredOperationCallCount,
            (int)_telemetryEnqueueTextInputCallCount,
            (int)_telemetryForceFullRedrawForSurfaceResetCallCount,
            (int)_telemetryForceFullRedrawForDiagnosticsCaptureCallCount,
            (int)_telemetryRebuildRetainedRenderListCallCount,
            (int)_telemetrySynchronizeRetainedRenderListCallCount,
            (int)_telemetryClearDirtyRenderQueueCallCount,
            (int)_telemetryResetUpdatePhaseStateCallCount);
    }

    public void GetTelemetryAndReset()
    {
        _telemetryUpdateCallCount = 0;
        _telemetryUpdateElapsedTicks = 0;
        _telemetryEnqueueDeferredOperationCallCount = 0;
        _telemetryEnqueueTextInputCallCount = 0;
        _telemetryForceFullRedrawForSurfaceResetCallCount = 0;
        _telemetryForceFullRedrawForDiagnosticsCaptureCallCount = 0;
        _telemetryRebuildRetainedRenderListCallCount = 0;
        _telemetrySynchronizeRetainedRenderListCallCount = 0;
        _telemetryClearDirtyRenderQueueCallCount = 0;
        _telemetryResetUpdatePhaseStateCallCount = 0;
        _retainedSyncChangedDirtyDecisionCount = 0;
        _fullRetainedDrawWithoutFullClearCount = 0;
        _retainedRender.ResetTelemetry();
    }

    internal UIElement? GetHoveredElementForDiagnostics()
    {
        return _inputState.HoveredElement;
    }

    internal void RunLayoutForTests(Viewport viewport)
    {
        RunLayoutPhase(viewport);
    }

    internal UIElement? GetLastClickDownTargetForDiagnostics()
    {
        return _lastClickDownTarget;
    }

    internal UIElement? GetLastClickUpTargetForDiagnostics()
    {
        return _lastClickUpTarget;
    }

    internal Vector2 GetLastPointerPositionForDiagnostics()
    {
        return _inputState.LastPointerPosition;
    }

    internal (int ConnectionCacheEntryCount, int AncestorCacheEntryCount) GetInputCacheEntryCountsForTests()
    {
        return (_inputConnectionCache.Count, _inputAncestorCache.Count);
    }

    internal bool WouldUsePartialDirtyRedrawForTests()
    {
        return _retainedRender.WouldUsePartialDirtyRedraw(CreateRetainedDrawThresholds());
    }

    internal UiDirtyDrawDecisionSnapshot ResolveDirtyDrawDecisionAfterRetainedSyncForTests()
    {
        return ResolveDirtyDrawDecisionAfterRetainedSync();
    }

    internal int GetFullRedrawSettleFramesRemainingForTests()
    {
        return _fullRedrawSettleFramesRemaining;
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

    public TextLayoutMetricsSnapshot GetTextLayoutMetricsSnapshot()
    {
        return TextLayout.GetMetricsSnapshot();
    }

    public AutomationMetricsSnapshot GetAutomationMetricsSnapshot()
    {
        return Automation.GetMetricsSnapshot();
    }

    internal void ForceFullRedrawForSurfaceReset()
    {
        _telemetryForceFullRedrawForSurfaceResetCallCount++;
        _hasRenderInvalidation = true;
        _mustDrawNextFrame = true;
        ArmFullRedrawSettleWindowForResize();
        MarkFullFrameDirty(UiFullDirtyReason.SurfaceReset);
    }

    internal void ForceFullRedrawForDiagnosticsCapture()
    {
        _telemetryForceFullRedrawForDiagnosticsCaptureCallCount++;
        _hasRenderInvalidation = true;
        _mustDrawNextFrame = true;
        _diagnosticCaptureFullClearPending = true;
        _retainedRender.MarkFullFrameDirtyWithoutReason();
    }

    internal void NotifyScrollViewportChanged(ScrollViewer viewer, LayoutRect viewport)
    {
        RecordRenderInvalidationSources(viewer, viewer);
        _hasRenderInvalidation = true;
        _mustDrawNextFrame = true;
        RenderInvalidationCount++;
        _renderStateVersion++;
        BumpPointerResolveStateVersion();
        _retainedRender.NotifyScrollViewportChanged(viewer, viewport);
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
        _telemetryUpdateCallCount++;
        var callStart = Stopwatch.GetTimestamp();

        Dispatcher.VerifyAccess();
        Automation.BeginFrame();

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

        LastUpdateMs = Stopwatch.GetElapsedTime(callStart).TotalMilliseconds;
        _telemetryUpdateElapsedTicks += Stopwatch.GetTimestamp() - callStart;
        Automation.EndFrameAndFlush();
    }

    public void EnqueueDeferredOperation(Action operation)
    {
        _telemetryEnqueueDeferredOperationCallCount++;
        Dispatcher.EnqueueDeferred(operation);
    }

    public void EnqueueTextInput(char character)
    {
        _telemetryEnqueueTextInputCallCount++;
        _inputManager.EnqueueTextInput(character);
    }

    internal void RebuildRenderListForTests()
    {
        _telemetryRebuildRetainedRenderListCallCount++;
        _retainedRender.RebuildRetainedRenderList();
    }

    internal IReadOnlyList<UIElement> GetRetainedVisualOrderForTests()
    {
        return _retainedRender.GetRetainedVisualOrderSnapshot();
    }

    internal IReadOnlyList<UIElement> GetDirtyRenderQueueSnapshotForTests()
    {
        return _retainedRender.GetDirtyRenderQueueSnapshot();
    }

    internal string GetDirtyRenderQueueSummaryForTests(int limit = 6)
    {
        return _retainedRender.GetDirtyRenderQueueSummary(limit, DescribeElementForDiagnostics);
    }

    internal string GetLastSynchronizedDirtyRootSummaryForTests(int limit = 6)
    {
        return _retainedRender.GetLastSynchronizedDirtyRootSummary(limit, DescribeElementForDiagnostics);
    }

    internal string GetDirtyRegionSummaryForTests(int limit = 6)
    {
        return _retainedRender.GetDirtyRegionSummary(limit);
    }

    internal IReadOnlyList<LayoutRect> GetDirtyRegionsSnapshotForTests()
    {
        return _retainedRender.GetDirtyRegionsSnapshot();
    }

    internal bool IsFullDirtyForTests()
    {
        return _retainedRender.IsFullFrameDirty;
    }

    internal double GetDirtyCoverageForTests()
    {
        return _retainedRender.DirtyCoverage;
    }

    internal void SetDirtyRegionViewportForTests(LayoutRect viewport)
    {
        _retainedRender.SetDirtyRegionViewport(viewport);
    }

    internal void ResetDirtyStateForTests()
    {
        _telemetryClearDirtyRenderQueueCallCount++;
        _retainedRender.ResetDirtyState();
        _dirtyBoundsEventTrace.Clear();
    }

    internal void SynchronizeRetainedRenderListForTests()
    {
        _telemetrySynchronizeRetainedRenderListCallCount++;
        _retainedRender.SynchronizeRetainedRenderList();
    }

    internal bool IsRenderListFullRebuildPendingForTests()
    {
        return _renderListNeedsFullRebuild;
    }

    internal int GetRetainedNodeSubtreeEndIndexForTests(UIElement visual)
    {
        return _retainedRender.GetRetainedNodeSubtreeEndIndex(visual);
    }

    internal LayoutRect GetRetainedNodeBoundsForTests(UIElement visual)
    {
        return _retainedRender.GetRetainedNodeBounds(visual);
    }

    internal string ValidateRetainedTreeAgainstCurrentVisualStateForTests(int maxMismatches = 8)
    {
        return _retainedRender.ValidateAgainstCurrentVisualState(maxMismatches);
    }

    internal UiRedrawReason GetScheduledDrawReasonsForTests()
    {
        return _scheduledDrawReasons;
    }

    internal void RemoveRetainedNodeIndexForTests(UIElement visual)
    {
        _retainedRender.RemoveRetainedNodeIndex(visual);
    }

    internal void CompleteDrawStateForTests()
    {
        if (_scheduledDrawReasons != UiRedrawReason.None)
        {
            DrawExecutedFrameCount++;
        }

        _visualRoot.ClearRenderInvalidationRecursive();
        _hasMeasureInvalidation = false;
        _hasArrangeInvalidation = false;
        _hasRenderInvalidation = false;
        _hasCaretBlinkInvalidation = false;
        _mustDrawNextFrame = false;
        _scheduledDrawReasons = UiRedrawReason.None;
        _retainedRender.ResetRetainedSyncTrackingState();
        _diagnosticCaptureFullClearPending = false;
    }

    private void ArmFullRedrawSettleWindowForResize()
    {
        _fullRedrawSettleFramesRemaining = Math.Max(
            _fullRedrawSettleFramesRemaining,
            FullRedrawSettleFrameCountAfterResize);
    }

    private void ConsumeFullRedrawSettleFrame()
    {
        if (_fullRedrawSettleFramesRemaining > 0)
        {
            _fullRedrawSettleFramesRemaining--;
        }
    }

    private void MarkFullFrameDirty(UiFullDirtyReason reason)
    {
        _retainedRender.MarkFullFrameDirty(reason);
    }

    private void RecordFullFrameDirtyReason(UiFullDirtyReason reason)
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
    }

    internal enum UiFullDirtyReason
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
        _retainedRender.ApplyRenderInvalidationCleanupAfterDraw();
    }

    internal IReadOnlyList<UIElement> GetRetainedDrawOrderForClipForTests(LayoutRect clipRect)
    {
        var visuals = new List<UIElement>();
        _retainedRender.AppendDrawOrderForClip(clipRect, visuals);
        return visuals;
    }

    internal (int NodesVisited, int NodesDrawn, int LocalClipPushCount) GetRetainedTraversalMetricsForClipForTests(LayoutRect clipRect)
    {
        return _retainedRender.GetTraversalMetricsForClip(clipRect);
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

    private void EnsureVisualIndexCurrent()
    {
        _visualIndex.EnsureCurrent(_visualRoot);
    }

    private void MarkVisualIndexDirty()
    {
        _visualIndex.MarkDirty();
        InvalidateKeyboardMenuScopeCache();
        InvalidateActiveUpdateParticipants();
    }

    private void InvalidateInputCachesForSubtree(UIElement element)
    {
        if (_inputConnectionCache.Count == 0 &&
            _inputAncestorCache.Count == 0)
        {
            return;
        }

        _inputCacheInvalidationTraversalStack.Clear();
        _inputCacheInvalidationVisited.Clear();
        _inputCacheInvalidationTraversalStack.Push(element);

        while (_inputCacheInvalidationTraversalStack.Count > 0)
        {
            var current = _inputCacheInvalidationTraversalStack.Pop();
            if (!_inputCacheInvalidationVisited.Add(current))
            {
                continue;
            }

            _inputConnectionCache.Remove(current);
            _inputAncestorCache.Remove(current);

            foreach (var child in current.GetVisualChildren())
            {
                _inputCacheInvalidationTraversalStack.Push(child);
            }

            foreach (var child in current.GetLogicalChildren())
            {
                _inputCacheInvalidationTraversalStack.Push(child);
            }
        }

        TrimInputCachesIfOversized();
    }

    private void TrimInputCachesIfOversized()
    {
        var visualCountHint = _visualIndex.Nodes.Count;
        var maxRetainedEntries = Math.Max(InputCacheTrimFloor, visualCountHint * 4);
        if (_inputConnectionCache.Count > maxRetainedEntries)
        {
            _inputConnectionCache.Clear();
        }

        if (_inputAncestorCache.Count > maxRetainedEntries)
        {
            _inputAncestorCache.Clear();
        }
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
        if (RetainedRenderController.IsHighCostVisual(visual))
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

    private static string DescribeElementForDiagnostics(UIElement element)
    {
        return element switch
        {
            FrameworkElement { Name.Length: > 0 } frameworkElement => $"{frameworkElement.GetType().Name}#{frameworkElement.Name}",
            _ => element.GetType().Name
        };
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

    private int _lastInputHitTestCount
    {
        get => _inputTelemetry.HitTestCount;
        set => _inputTelemetry.HitTestCount = value;
    }

    private int _lastInputRoutedEventCount
    {
        get => _inputTelemetry.RoutedEventCount;
        set => _inputTelemetry.RoutedEventCount = value;
    }

    private int _lastInputKeyEventCount
    {
        get => _inputTelemetry.KeyEventCount;
        set => _inputTelemetry.KeyEventCount = value;
    }

    private int _lastInputTextEventCount
    {
        get => _inputTelemetry.TextEventCount;
        set => _inputTelemetry.TextEventCount = value;
    }

    private int _lastInputPointerEventCount
    {
        get => _inputTelemetry.PointerEventCount;
        set => _inputTelemetry.PointerEventCount = value;
    }

    private double _lastInputCaptureMs
    {
        get => _inputTelemetry.CaptureMilliseconds;
        set => _inputTelemetry.CaptureMilliseconds = value;
    }

    private double _lastInputDispatchMs
    {
        get => _inputTelemetry.DispatchMilliseconds;
        set => _inputTelemetry.DispatchMilliseconds = value;
    }

    private double _lastInputPointerDispatchMs
    {
        get => _inputTelemetry.PointerDispatchMilliseconds;
        set => _inputTelemetry.PointerDispatchMilliseconds = value;
    }

    private double _lastInputPointerTargetResolveMs
    {
        get => _inputTelemetry.PointerTargetResolveMilliseconds;
        set => _inputTelemetry.PointerTargetResolveMilliseconds = value;
    }

    private double _lastInputHoverUpdateMs
    {
        get => _inputTelemetry.HoverUpdateMilliseconds;
        set => _inputTelemetry.HoverUpdateMilliseconds = value;
    }

    private double _lastInputPointerRouteMs
    {
        get => _inputTelemetry.PointerRouteMilliseconds;
        set => _inputTelemetry.PointerRouteMilliseconds = value;
    }

    private double _lastInputPointerMoveDispatchMs
    {
        get => _inputTelemetry.PointerMoveDispatchMilliseconds;
        set => _inputTelemetry.PointerMoveDispatchMilliseconds = value;
    }

    private double _lastInputPointerMoveRoutedEventsMs
    {
        get => _inputTelemetry.PointerMoveRoutedEventsMilliseconds;
        set => _inputTelemetry.PointerMoveRoutedEventsMilliseconds = value;
    }

    private double _lastInputPointerMoveHandlerMs
    {
        get => _inputTelemetry.PointerMoveHandlerMilliseconds;
        set => _inputTelemetry.PointerMoveHandlerMilliseconds = value;
    }

    private double _lastInputPointerMovePreviewEventMs
    {
        get => _inputTelemetry.PointerMovePreviewEventMilliseconds;
        set => _inputTelemetry.PointerMovePreviewEventMilliseconds = value;
    }

    private double _lastInputPointerMoveBubbleEventMs
    {
        get => _inputTelemetry.PointerMoveBubbleEventMilliseconds;
        set => _inputTelemetry.PointerMoveBubbleEventMilliseconds = value;
    }

    private double _lastInputPointerResolveContextMenuCheckMs
    {
        get => _inputTelemetry.PointerResolveContextMenuCheckMilliseconds;
        set => _inputTelemetry.PointerResolveContextMenuCheckMilliseconds = value;
    }

    private double _lastInputPointerResolveContextMenuOverlayCandidateMs
    {
        get => _inputTelemetry.PointerResolveContextMenuOverlayCandidateMilliseconds;
        set => _inputTelemetry.PointerResolveContextMenuOverlayCandidateMilliseconds = value;
    }

    private double _lastInputPointerResolveContextMenuCachedMenuMs
    {
        get => _inputTelemetry.PointerResolveContextMenuCachedMenuMilliseconds;
        set => _inputTelemetry.PointerResolveContextMenuCachedMenuMilliseconds = value;
    }

    private double _lastInputPointerResolveHoverReuseCheckMs
    {
        get => _inputTelemetry.PointerResolveHoverReuseCheckMilliseconds;
        set => _inputTelemetry.PointerResolveHoverReuseCheckMilliseconds = value;
    }

    private double _lastInputPointerResolveFinalHitTestMs
    {
        get => _inputTelemetry.PointerResolveFinalHitTestMilliseconds;
        set => _inputTelemetry.PointerResolveFinalHitTestMilliseconds = value;
    }

    private double _lastInputToolTipLifecycleMs
    {
        get => _inputTelemetry.ToolTipLifecycleMilliseconds;
        set => _inputTelemetry.ToolTipLifecycleMilliseconds = value;
    }

    private double _lastInputCommandRequeryMs
    {
        get => _inputTelemetry.CommandRequeryMilliseconds;
        set => _inputTelemetry.CommandRequeryMilliseconds = value;
    }

    private double _lastInputKeyDispatchMs
    {
        get => _inputTelemetry.KeyDispatchMilliseconds;
        set => _inputTelemetry.KeyDispatchMilliseconds = value;
    }

    private double _lastInputTextDispatchMs
    {
        get => _inputTelemetry.TextDispatchMilliseconds;
        set => _inputTelemetry.TextDispatchMilliseconds = value;
    }

    private int _clickCpuResolveCachedCount
    {
        get => _inputTelemetry.ClickResolveCachedCount;
        set => _inputTelemetry.ClickResolveCachedCount = value;
    }

    private int _clickCpuResolveCapturedCount
    {
        get => _inputTelemetry.ClickResolveCapturedCount;
        set => _inputTelemetry.ClickResolveCapturedCount = value;
    }

    private int _clickCpuResolveHoveredCount
    {
        get => _inputTelemetry.ClickResolveHoveredCount;
        set => _inputTelemetry.ClickResolveHoveredCount = value;
    }

    private int _clickCpuResolveHitTestCount
    {
        get => _inputTelemetry.ClickResolveHitTestCount;
        set => _inputTelemetry.ClickResolveHitTestCount = value;
    }

    private double _lastInputPointerMoveCapturedDataGridHandlerMs
    {
        get => _inputTelemetry.PointerMoveCapturedDataGridHandlerMilliseconds;
        set => _inputTelemetry.PointerMoveCapturedDataGridHandlerMilliseconds = value;
    }

    private double _lastInputPointerMoveCapturedTextInputHandlerMs
    {
        get => _inputTelemetry.PointerMoveCapturedTextInputHandlerMilliseconds;
        set => _inputTelemetry.PointerMoveCapturedTextInputHandlerMilliseconds = value;
    }

    private double _lastInputPointerMoveCapturedScrollViewerHandlerMs
    {
        get => _inputTelemetry.PointerMoveCapturedScrollViewerHandlerMilliseconds;
        set => _inputTelemetry.PointerMoveCapturedScrollViewerHandlerMilliseconds = value;
    }

    private double _lastInputPointerMoveCapturedSliderHandlerMs
    {
        get => _inputTelemetry.PointerMoveCapturedSliderHandlerMilliseconds;
        set => _inputTelemetry.PointerMoveCapturedSliderHandlerMilliseconds = value;
    }

    private double _lastInputPointerMoveCapturedPopupHandlerMs
    {
        get => _inputTelemetry.PointerMoveCapturedPopupHandlerMilliseconds;
        set => _inputTelemetry.PointerMoveCapturedPopupHandlerMilliseconds = value;
    }

    private double _lastInputPointerMoveHyperlinkHandlerMs
    {
        get => _inputTelemetry.PointerMoveHyperlinkHandlerMilliseconds;
        set => _inputTelemetry.PointerMoveHyperlinkHandlerMilliseconds = value;
    }

    private double _lastInputPointerMoveContextMenuItemHandlerMs
    {
        get => _inputTelemetry.PointerMoveContextMenuItemHandlerMilliseconds;
        set => _inputTelemetry.PointerMoveContextMenuItemHandlerMilliseconds = value;
    }

    private double _lastInputPointerMoveMenuItemHandlerMs
    {
        get => _inputTelemetry.PointerMoveMenuItemHandlerMilliseconds;
        set => _inputTelemetry.PointerMoveMenuItemHandlerMilliseconds = value;
    }

    private double _lastInputPointerMoveMenuFocusRestoreMs
    {
        get => _inputTelemetry.PointerMoveMenuFocusRestoreMilliseconds;
        set => _inputTelemetry.PointerMoveMenuFocusRestoreMilliseconds = value;
    }

    private double _lastInputPointerRouteOuterContextMenuProbeMs
    {
        get => _inputTelemetry.PointerRouteOuterContextMenuProbeMilliseconds;
        set => _inputTelemetry.PointerRouteOuterContextMenuProbeMilliseconds = value;
    }

    private double _lastInputPointerRouteOuterGateMs
    {
        get => _inputTelemetry.PointerRouteOuterGateMilliseconds;
        set => _inputTelemetry.PointerRouteOuterGateMilliseconds = value;
    }

    private double _lastInputPointerRouteOuterDispatchCallMs
    {
        get => _inputTelemetry.PointerRouteOuterDispatchCallMilliseconds;
        set => _inputTelemetry.PointerRouteOuterDispatchCallMilliseconds = value;
    }

    private int _lastInputPointerRouteDispatchCount
    {
        get => _inputTelemetry.PointerRouteDispatchCount;
        set => _inputTelemetry.PointerRouteDispatchCount = value;
    }

    private struct InputPhaseTelemetryState
    {
        public int HitTestCount;
        public int RoutedEventCount;
        public int KeyEventCount;
        public int TextEventCount;
        public int PointerEventCount;
        public double CaptureMilliseconds;
        public double DispatchMilliseconds;
        public double PointerDispatchMilliseconds;
        public double PointerTargetResolveMilliseconds;
        public double HoverUpdateMilliseconds;
        public double PointerRouteMilliseconds;
        public double PointerMoveDispatchMilliseconds;
        public double PointerMoveRoutedEventsMilliseconds;
        public double PointerMoveHandlerMilliseconds;
        public double PointerMovePreviewEventMilliseconds;
        public double PointerMoveBubbleEventMilliseconds;
        public double PointerResolveContextMenuCheckMilliseconds;
        public double PointerResolveContextMenuOverlayCandidateMilliseconds;
        public double PointerResolveContextMenuCachedMenuMilliseconds;
        public double PointerResolveHoverReuseCheckMilliseconds;
        public double PointerResolveFinalHitTestMilliseconds;
        public double ToolTipLifecycleMilliseconds;
        public double CommandRequeryMilliseconds;
        public double KeyDispatchMilliseconds;
        public double TextDispatchMilliseconds;
        public int ClickResolveCachedCount;
        public int ClickResolveCapturedCount;
        public int ClickResolveHoveredCount;
        public int ClickResolveHitTestCount;
        public double PointerMoveCapturedDataGridHandlerMilliseconds;
        public double PointerMoveCapturedTextInputHandlerMilliseconds;
        public double PointerMoveCapturedScrollViewerHandlerMilliseconds;
        public double PointerMoveCapturedSliderHandlerMilliseconds;
        public double PointerMoveCapturedPopupHandlerMilliseconds;
        public double PointerMoveHyperlinkHandlerMilliseconds;
        public double PointerMoveContextMenuItemHandlerMilliseconds;
        public double PointerMoveMenuItemHandlerMilliseconds;
        public double PointerMoveMenuFocusRestoreMilliseconds;
        public double PointerRouteOuterContextMenuProbeMilliseconds;
        public double PointerRouteOuterGateMilliseconds;
        public double PointerRouteOuterDispatchCallMilliseconds;
        public int PointerRouteDispatchCount;
    }

    private readonly record struct CachedInputConnectionState(bool IsConnected);

    private sealed record CachedInputAncestorChain(UIElement[] Chain);
}
