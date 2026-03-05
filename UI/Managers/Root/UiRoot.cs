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
    private readonly List<RenderNode> _retainedRenderList = new();
    private readonly Dictionary<UIElement, int> _renderNodeIndices = new();
    private readonly Queue<UIElement> _dirtyRenderQueue = new();
    private readonly HashSet<UIElement> _dirtyRenderSet = new();
    private readonly HashSet<UIElement> _dirtyRenderRootsRequireDeepSync = new();
    private readonly List<UiUpdatePhase> _lastUpdatePhaseOrder = new(5);
    private readonly DirtyRegionTracker _dirtyRegions = new();
    private readonly InputManager _inputManager = new();
    private readonly InputDispatchState _inputState = new();
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

    public UiRoot(UIElement visualRoot)
    {
        _visualRoot = visualRoot ?? throw new ArgumentNullException(nameof(visualRoot));
        _layoutRoot = visualRoot as FrameworkElement;
        Automation = new AutomationManager(_visualRoot);
        UseRetainedRenderList = EnableRetainedRenderListByDefault;
        UseDirtyRegionRendering = EnableDirtyRegionRenderingByDefault;
        UseConditionalDrawScheduling = EnableConditionalDrawByDefault;
        _dirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
        Current = this;
    }

    public static UiRoot? Current { get; private set; }

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

    public int DirtyRenderQueueCount => _dirtyRenderQueue.Count;

    public bool HasPendingMeasureInvalidation => _hasMeasureInvalidation;

    public bool HasPendingArrangeInvalidation => _hasArrangeInvalidation;

    public bool HasPendingRenderInvalidation => _hasRenderInvalidation;

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
            UseConditionalDrawScheduling);
    }

    public AutomationMetricsSnapshot GetAutomationMetricsSnapshot()
    {
        return Automation.GetMetricsSnapshot();
    }

    internal void ForceFullRedrawForSurfaceReset()
    {
        _hasRenderInvalidation = true;
        _mustDrawNextFrame = true;
        _dirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
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

}


