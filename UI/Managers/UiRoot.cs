using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed class UiRoot
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
    }

    public void EnqueueDeferredOperation(Action operation)
    {
        Dispatcher.EnqueueDeferred(operation);
    }

    public bool ShouldDrawThisFrame(GameTime gameTime, Viewport viewport, GraphicsDevice? graphicsDevice = null)
    {
        _ = gameTime;
        var reasons = UiRedrawReason.None;

        if (!_hasLastScheduledViewport ||
            _lastScheduledViewport.Width != viewport.Width ||
            _lastScheduledViewport.Height != viewport.Height ||
            _lastScheduledViewport.X != viewport.X ||
            _lastScheduledViewport.Y != viewport.Y)
        {
            reasons |= UiRedrawReason.Resize;
            _lastScheduledViewport = viewport;
            _hasLastScheduledViewport = true;
        }

        if (!ReferenceEquals(_lastGraphicsDevice, graphicsDevice))
        {
            reasons |= UiRedrawReason.Resize;
            _lastGraphicsDevice = graphicsDevice;
        }

        if (_mustDrawNextFrame)
        {
            reasons |= UiRedrawReason.LayoutInvalidated;
        }

        if (_hasMeasureInvalidation || _hasArrangeInvalidation)
        {
            reasons |= UiRedrawReason.LayoutInvalidated;
        }

        if (_hasRenderInvalidation || _dirtyRegions.IsFullFrameDirty || _dirtyRegions.RegionCount > 0)
        {
            reasons |= UiRedrawReason.RenderInvalidated;
        }

        if (AnimationManager.Current.HasRunningAnimations || _forceAnimationActiveForTests)
        {
            reasons |= UiRedrawReason.AnimationActive;
        }

        if (_hasCaretBlinkInvalidation)
        {
            reasons |= UiRedrawReason.CaretBlinkActive;
        }

        if (AlwaysDrawCompatibilityMode)
        {
            reasons |= UiRedrawReason.RenderInvalidated;
        }

        if (!UseConditionalDrawScheduling)
        {
            reasons |= UiRedrawReason.RenderInvalidated;
        }

        var shouldDraw = !UseConditionalDrawScheduling || AlwaysDrawCompatibilityMode || reasons != UiRedrawReason.None;
        LastShouldDrawReasons = reasons;
        _scheduledDrawReasons = shouldDraw ? reasons : UiRedrawReason.None;
        if (shouldDraw)
        {
            DrawExecutedFrameCount++;
        }
        else
        {
            DrawSkippedFrameCount++;
        }

        return shouldDraw;
    }

    public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
    {
        Dispatcher.VerifyAccess();
        _ = gameTime;
        if (spriteBatch == null)
        {
            throw new ArgumentNullException(nameof(spriteBatch));
        }

        var drawStart = Stopwatch.GetTimestamp();
        DrawCalls = 1;
        LastDrawUsedPartialRedraw = false;
        LastFrameCacheHitCount = 0;
        LastFrameCacheMissCount = 0;
        LastFrameCacheRebuildCount = 0;
        _lastFrameCachedSubtreeBounds.Clear();
        LastDrawReasons = _scheduledDrawReasons;
        _scheduledDrawReasons = UiRedrawReason.None;

        var graphicsDevice = spriteBatch.GraphicsDevice;
        if (!_hasLastLayoutViewport || !AreViewportsEqual(_lastLayoutViewport, graphicsDevice.Viewport))
        {
            SyncDirtyRegionViewport(graphicsDevice.Viewport);
        }

        _renderCacheStore.EnsureDevice(graphicsDevice);

        if (UseRetainedRenderList)
        {
            PrepareElementRenderCaches(graphicsDevice);
        }

        var usePartialClear = UseRetainedRenderList &&
                              UseDirtyRegionRendering &&
                              !_dirtyRegions.IsFullFrameDirty &&
                              _dirtyRegions.RegionCount > 0;
        if (!usePartialClear)
        {
            graphicsDevice.Clear(_clearColor);
        }

        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.LinearClamp,
            depthStencilState: DepthStencilState.None,
            rasterizerState: UiRasterizerState);
        UiDrawing.SetActiveBatchState(graphicsDevice,
            SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, UiRasterizerState);
        try
        {
            UiDrawing.ResetState(graphicsDevice);
            if (UseRetainedRenderList)
            {
                if (UseDirtyRegionRendering)
                {
                    DrawRetainedRenderListWithDirtyRegions(spriteBatch);
                }
                else
                {
                    LastDirtyRectCount = 1;
                    LastDirtyAreaPercentage = 1d;
                    DrawRetainedRenderList(spriteBatch);
                }
            }
            else
            {
                _visualRoot.Draw(spriteBatch);
                LastDirtyRectCount = 1;
                LastDirtyAreaPercentage = 1d;
            }

            if (ShowCachedSubtreeBoundsOverlay && _lastFrameCachedSubtreeBounds.Count > 0)
            {
                DrawCachedSubtreeBoundsOverlay(spriteBatch, _lastFrameCachedSubtreeBounds);
            }
        }
        finally
        {
            UiDrawing.ClearActiveBatchState(graphicsDevice);
            spriteBatch.End();
        }

        _visualRoot.ClearRenderInvalidationRecursive();
        _hasMeasureInvalidation = false;
        _hasArrangeInvalidation = false;
        _hasRenderInvalidation = false;
        _hasCaretBlinkInvalidation = false;
        _mustDrawNextFrame = false;
        ClearDirtyRenderQueue();
        _dirtyRegions.Clear();
        LastDrawMs = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
        TraceRenderCacheCountersIfEnabled();
    }

    public void Shutdown()
    {
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }

        _cacheSpriteBatch?.Dispose();
        _cacheSpriteBatch = null;
        _renderCacheStore.Dispose();
    }

    internal void NotifyInvalidation(UiInvalidationType invalidationType, UIElement? source = null)
    {
        switch (invalidationType)
        {
            case UiInvalidationType.Measure:
                _hasMeasureInvalidation = true;
                _mustDrawNextFrame = true;
                MeasureInvalidationCount++;
                break;
            case UiInvalidationType.Arrange:
                _hasArrangeInvalidation = true;
                _mustDrawNextFrame = true;
                ArrangeInvalidationCount++;
                break;
            case UiInvalidationType.Render:
                _hasRenderInvalidation = true;
                _mustDrawNextFrame = true;
                RenderInvalidationCount++;
                if (source is TextBox textBox && textBox.IsFocused)
                {
                    _hasCaretBlinkInvalidation = true;
                }

                if (UseDirtyRegionRendering)
                {
                    TrackDirtyBoundsForVisual(source);
                }
                if (source != null)
                {
                    EnqueueDirtyRenderNode(source);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidationType), invalidationType, null);
        }
    }

    internal void NotifyVisualStructureChanged(UIElement element, UIElement? oldParent, UIElement? newParent)
    {
        if (!IsPartOfVisualTree(element) &&
            !IsPartOfVisualTree(oldParent) &&
            !IsPartOfVisualTree(newParent) &&
            !ReferenceEquals(element, _visualRoot))
        {
            return;
        }

        _renderListNeedsFullRebuild = true;
        _mustDrawNextFrame = true;
        _dirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
        _renderCacheStore.Clear();
        EnqueueDirtyRenderNode(element);
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

    private void ResetUpdatePhaseDiagnostics()
    {
        _lastUpdatePhaseOrder.Clear();
        LastInputPhaseMs = 0d;
        LastBindingPhaseMs = 0d;
        LastLayoutPhaseMs = 0d;
        LastAnimationPhaseMs = 0d;
        LastRenderSchedulingPhaseMs = 0d;
        LastDeferredOperationCount = 0;
        LayoutPasses = 0;
    }

    private double ExecuteUpdatePhase(UiUpdatePhase phase, Action action)
    {
        var phaseStart = Stopwatch.GetTimestamp();
        _lastUpdatePhaseOrder.Add(phase);
        action();
        return Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds;
    }

    private void RunInputAndEventsPhase(GameTime gameTime)
    {
        _visualRoot.Update(gameTime);
    }

    private void RunBindingAndDeferredPhase()
    {
        LastDeferredOperationCount = Dispatcher.DrainDeferredOperations();
    }

    private void RunLayoutPhase(Viewport viewport)
    {
        if (_layoutRoot == null)
        {
            LayoutSkippedFrameCount++;
            return;
        }

        var viewportChanged = !_hasLastLayoutViewport || !AreViewportsEqual(_lastLayoutViewport, viewport);
        if (viewportChanged)
        {
            _lastLayoutViewport = viewport;
            _hasLastLayoutViewport = true;
            _hasMeasureInvalidation = true;
            _hasArrangeInvalidation = true;
            _mustDrawNextFrame = true;
        }

        var shouldRunLayout = !_hasCompletedInitialLayout ||
                              viewportChanged ||
                              _hasMeasureInvalidation ||
                              _hasArrangeInvalidation ||
                              _layoutRoot.NeedsMeasure ||
                              _layoutRoot.NeedsArrange;
        if (!shouldRunLayout)
        {
            LayoutSkippedFrameCount++;
            return;
        }

        _layoutRoot.Measure(new Vector2(viewport.Width, viewport.Height));
        _layoutRoot.Arrange(new LayoutRect(0f, 0f, viewport.Width, viewport.Height));
        LayoutPasses = 1;
        _hasCompletedInitialLayout = true;
        LayoutExecutedFrameCount++;
    }

    private static void RunAnimationPhase(GameTime gameTime)
    {
        AnimationManager.Current.Update(gameTime);
    }

    private void RunRenderSchedulingPhase(Viewport viewport)
    {
        SyncDirtyRegionViewport(viewport);
        if (!UseRetainedRenderList)
        {
            return;
        }

        SynchronizeRetainedRenderList();
    }

    private void SyncDirtyRegionViewport(Viewport viewport)
    {
        var viewportBounds = new LayoutRect(viewport.X, viewport.Y, viewport.Width, viewport.Height);
        var viewportChanged = !_hasViewportBounds || !AreRectsEqual(_lastViewportBounds, viewportBounds);
        _hasViewportBounds = true;
        _lastViewportBounds = viewportBounds;
        _dirtyRegions.SetViewport(viewportBounds);
        if (viewportChanged)
        {
            _mustDrawNextFrame = true;
            _dirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
            _renderCacheStore.Clear();
        }
    }

    private bool IsPartOfVisualTree(UIElement? element)
    {
        return element != null && ReferenceEquals(element.GetVisualRoot(), _visualRoot);
    }

    private void EnqueueDirtyRenderNode(UIElement visual)
    {
        if (!IsPartOfVisualTree(visual))
        {
            return;
        }

        if (!_dirtyRenderSet.Add(visual))
        {
            return;
        }

        _dirtyRenderQueue.Enqueue(visual);
    }

    private void ClearDirtyRenderQueue()
    {
        _dirtyRenderQueue.Clear();
        _dirtyRenderSet.Clear();
    }

    private void SynchronizeRetainedRenderList()
    {
        if (_renderListNeedsFullRebuild || _retainedRenderList.Count == 0)
        {
            RebuildRetainedRenderList();
            return;
        }

        if (_dirtyRenderQueue.Count == 0)
        {
            return;
        }

        while (_dirtyRenderQueue.TryDequeue(out var dirtyVisual))
        {
            _dirtyRenderSet.Remove(dirtyVisual);
            UpdateRenderNodeSubtree(dirtyVisual);
            if (_renderListNeedsFullRebuild)
            {
                RebuildRetainedRenderList();
                return;
            }
        }
    }

    private void RebuildRetainedRenderList()
    {
        _retainedRenderList.Clear();
        _renderNodeIndices.Clear();
        _ = BuildRenderSubtree(_visualRoot, traversalOrder: 0, depth: 0);
        _renderListNeedsFullRebuild = false;
        _dirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
        _renderCacheStore.Clear();
        ClearDirtyRenderQueue();
    }

    private (int TraversalOrder, SubtreeMetadata Metadata) BuildRenderSubtree(UIElement visual, int traversalOrder, int depth)
    {
        var nodeIndex = _retainedRenderList.Count;
        var node = CreateRenderNode(visual, traversalOrder, depth, subtreeEndIndexExclusive: nodeIndex + 1);
        _renderNodeIndices[visual] = nodeIndex;
        _retainedRenderList.Add(node);
        traversalOrder += 1;

        var metadata = CreateSubtreeMetadataForNode(node);
        foreach (var child in visual.GetVisualChildren())
        {
            var childResult = BuildRenderSubtree(child, traversalOrder, depth + 1);
            traversalOrder = childResult.TraversalOrder;
            metadata = MergeSubtreeMetadata(metadata, childResult.Metadata);
        }

        var subtreeEndIndexExclusive = _retainedRenderList.Count;
        var finalized = node.WithSubtreeMetadata(
            subtreeEndIndexExclusive,
            metadata.HasBoundsSnapshot,
            metadata.BoundsSnapshot,
            metadata.VisualCount,
            metadata.HighCostVisualCount,
            metadata.RenderVersionStamp,
            metadata.LayoutVersionStamp);
        _retainedRenderList[nodeIndex] = finalized;
        return (traversalOrder, metadata);
    }

    private void UpdateRenderNodeSubtree(UIElement dirtySubtreeRoot)
    {
        if (!IsPartOfVisualTree(dirtySubtreeRoot))
        {
            _renderListNeedsFullRebuild = true;
            return;
        }

        _ = UpdateRenderNodeSubtreeRecursive(dirtySubtreeRoot);
        if (_renderListNeedsFullRebuild)
        {
            return;
        }

        RefreshAncestorNodeSubtreeMetadata(dirtySubtreeRoot.VisualParent);
    }

    private SubtreeMetadata UpdateRenderNodeSubtreeRecursive(UIElement visual)
    {
        if (!_renderNodeIndices.TryGetValue(visual, out var renderNodeIndex))
        {
            _renderListNeedsFullRebuild = true;
            return default;
        }

        var previous = _retainedRenderList[renderNodeIndex];
        var updated = CreateRenderNode(
            visual,
            previous.TraversalOrder,
            previous.Depth,
            previous.SubtreeEndIndexExclusive);
        var metadata = CreateSubtreeMetadataForNode(updated);

        foreach (var child in visual.GetVisualChildren())
        {
            var childMetadata = UpdateRenderNodeSubtreeRecursive(child);
            if (_renderListNeedsFullRebuild)
            {
                return default;
            }

            metadata = MergeSubtreeMetadata(metadata, childMetadata);
        }

        updated = updated.WithSubtreeMetadata(
            previous.SubtreeEndIndexExclusive,
            metadata.HasBoundsSnapshot,
            metadata.BoundsSnapshot,
            metadata.VisualCount,
            metadata.HighCostVisualCount,
            metadata.RenderVersionStamp,
            metadata.LayoutVersionStamp);
        RecordBoundsDelta(previous, updated);
        _retainedRenderList[renderNodeIndex] = updated;
        return metadata;
    }

    private void RefreshAncestorNodeSubtreeMetadata(UIElement? visual)
    {
        for (var current = visual; current != null; current = current.VisualParent)
        {
            if (!_renderNodeIndices.TryGetValue(current, out var renderNodeIndex))
            {
                _renderListNeedsFullRebuild = true;
                return;
            }

            var previous = _retainedRenderList[renderNodeIndex];
            var updated = CreateRenderNode(
                current,
                previous.TraversalOrder,
                previous.Depth,
                previous.SubtreeEndIndexExclusive);

            var metadata = CreateSubtreeMetadataForNode(updated);
            foreach (var child in current.GetVisualChildren())
            {
                if (!_renderNodeIndices.TryGetValue(child, out var childNodeIndex))
                {
                    _renderListNeedsFullRebuild = true;
                    return;
                }

                metadata = MergeSubtreeMetadata(
                    metadata,
                    CreateSubtreeMetadataFromSubtreeNode(_retainedRenderList[childNodeIndex]));
            }

            updated = updated.WithSubtreeMetadata(
                previous.SubtreeEndIndexExclusive,
                metadata.HasBoundsSnapshot,
                metadata.BoundsSnapshot,
                metadata.VisualCount,
                metadata.HighCostVisualCount,
                metadata.RenderVersionStamp,
                metadata.LayoutVersionStamp);
            _retainedRenderList[renderNodeIndex] = updated;
        }
    }

    private void PrepareElementRenderCaches(GraphicsDevice graphicsDevice)
    {
        if (!UseElementRenderCaches)
        {
            return;
        }

        var useDirtyFilter = !_dirtyRegions.IsFullFrameDirty && _dirtyRegions.RegionCount > 0;
        var regions = _dirtyRegions.Regions;
        for (var i = 0; i < _retainedRenderList.Count; i++)
        {
            var node = _retainedRenderList[i];
            if (!node.IsEffectivelyVisible)
            {
                continue;
            }

            if (useDirtyFilter &&
                node.HasSubtreeBoundsSnapshot &&
                !IntersectsAny(node.SubtreeBoundsSnapshot, regions))
            {
                continue;
            }

            EnsureNodeCache(graphicsDevice, i);
        }
    }

    private void EnsureNodeCache(GraphicsDevice graphicsDevice, int nodeIndex)
    {
        var node = _retainedRenderList[nodeIndex];
        var context = CreateRenderCacheContext(node);
        if (!_renderCachePolicy.CanCache(node.Visual, context))
        {
            _renderCacheStore.Remove(node.Visual);
            return;
        }

        if (!_renderCachePolicy.TryGetCacheBounds(node.Visual, context, out var cacheBounds))
        {
            _renderCacheStore.Remove(node.Visual);
            return;
        }

        cacheBounds = NormalizeBounds(cacheBounds);
        if (!IsValidCacheBounds(cacheBounds))
        {
            _renderCacheStore.Remove(node.Visual);
            return;
        }

        if (_renderCacheStore.TryGet(node.Visual, out var existing))
        {
            var snapshot = new RenderCacheSnapshot(
                existing.Bounds,
                existing.RenderVersionStamp,
                existing.LayoutVersionStamp,
                existing.RenderStateSignature);
            if (!_renderCachePolicy.ShouldRebuildCache(node.Visual, context, snapshot))
            {
                return;
            }

            var rebuiltTarget = RenderVisualToCache(graphicsDevice, node, cacheBounds, existing.RenderTarget);
            if (rebuiltTarget == null)
            {
                _renderCacheStore.Remove(node.Visual);
                return;
            }

            _renderCacheStore.Upsert(
                node.Visual,
                rebuiltTarget,
                cacheBounds,
                node.SubtreeRenderVersionStamp,
                node.SubtreeLayoutVersionStamp,
                node.RenderStateSignature);
            CacheRebuildCount++;
            LastFrameCacheRebuildCount++;
            return;
        }

        var createdTarget = RenderVisualToCache(graphicsDevice, node, cacheBounds, null);
        if (createdTarget == null)
        {
            return;
        }

        _renderCacheStore.Upsert(
            node.Visual,
            createdTarget,
            cacheBounds,
            node.SubtreeRenderVersionStamp,
            node.SubtreeLayoutVersionStamp,
            node.RenderStateSignature);
        CacheMissCount++;
        LastFrameCacheMissCount++;
    }

    private RenderTarget2D? RenderVisualToCache(
        GraphicsDevice graphicsDevice,
        RenderNode node,
        LayoutRect bounds,
        RenderTarget2D? existingRenderTarget)
    {
        var width = Math.Max(1, (int)MathF.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)MathF.Ceiling(bounds.Height));
        if (width > MaxCacheTextureDimension || height > MaxCacheTextureDimension)
        {
            return null;
        }

        RenderTarget2D target;
        if (existingRenderTarget != null &&
            !existingRenderTarget.IsDisposed &&
            existingRenderTarget.Width == width &&
            existingRenderTarget.Height == height)
        {
            target = existingRenderTarget;
        }
        else
        {
            existingRenderTarget?.Dispose();
            target = new RenderTarget2D(
                graphicsDevice,
                width,
                height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0,
                RenderTargetUsage.DiscardContents);
        }

        EnsureCacheSpriteBatch(graphicsDevice);
        graphicsDevice.SetRenderTarget(target);
        graphicsDevice.Clear(Color.Transparent);
        _cacheSpriteBatch!.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.LinearClamp,
            depthStencilState: DepthStencilState.None,
            rasterizerState: UiRasterizerState);
        UiDrawing.SetActiveBatchState(graphicsDevice,
            SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, UiRasterizerState);
        try
        {
            UiDrawing.ResetState(graphicsDevice);
            UiDrawing.PushTransform(_cacheSpriteBatch, Matrix.CreateTranslation(-bounds.X, -bounds.Y, 0f));
            try
            {
                node.Visual.Draw(_cacheSpriteBatch);
            }
            finally
            {
                UiDrawing.PopTransform(_cacheSpriteBatch);
            }
        }
        finally
        {
            UiDrawing.ClearActiveBatchState(graphicsDevice);
            _cacheSpriteBatch.End();
            graphicsDevice.SetRenderTarget(null);
        }

        return target;
    }

    private void EnsureCacheSpriteBatch(GraphicsDevice graphicsDevice)
    {
        if (_cacheSpriteBatch != null &&
            !ReferenceEquals(_cacheSpriteBatch.GraphicsDevice, graphicsDevice))
        {
            _cacheSpriteBatch.Dispose();
            _cacheSpriteBatch = null;
        }

        _cacheSpriteBatch ??= new SpriteBatch(graphicsDevice);
    }

    private bool TryDrawCachedNode(SpriteBatch spriteBatch, RenderNode node, out int subtreeEndIndexExclusive)
    {
        subtreeEndIndexExclusive = node.SubtreeEndIndexExclusive;
        if (!UseElementRenderCaches)
        {
            return false;
        }

        var context = CreateRenderCacheContext(node);
        if (!_renderCachePolicy.CanCache(node.Visual, context))
        {
            return false;
        }

        if (!_renderCachePolicy.TryGetCacheBounds(node.Visual, context, out var cacheBounds))
        {
            return false;
        }

        cacheBounds = NormalizeBounds(cacheBounds);
        if (!_renderCacheStore.TryGet(node.Visual, out var entry))
        {
            return false;
        }

        if (!AreRectsEqual(entry.Bounds, cacheBounds))
        {
            return false;
        }

        DrawCachedNode(spriteBatch, node, entry);
        _lastFrameCachedSubtreeBounds.Add(entry.Bounds);
        CacheHitCount++;
        LastFrameCacheHitCount++;
        return true;
    }

    private static void DrawCachedNode(SpriteBatch spriteBatch, RenderNode node, RenderCacheEntry entry)
    {
        var pushedClipCount = 0;
        var pushedTransformCount = 0;
        var steps = node.RenderStateSteps;
        for (var i = 0; i < node.LocalRenderStateStartIndex; i++)
        {
            var step = steps[i];
            if (step.Kind == RenderStateStepKind.Clip)
            {
                UiDrawing.PushClip(spriteBatch, step.ClipRect);
                pushedClipCount++;
                continue;
            }

            UiDrawing.PushTransform(spriteBatch, step.Transform);
            pushedTransformCount++;
        }

        try
        {
            UiDrawing.DrawTexture(spriteBatch, entry.RenderTarget, entry.Bounds, color: Color.White);
        }
        finally
        {
            for (var i = 0; i < pushedTransformCount; i++)
            {
                UiDrawing.PopTransform(spriteBatch);
            }

            for (var i = 0; i < pushedClipCount; i++)
            {
                UiDrawing.PopClip(spriteBatch);
            }
        }
    }

    private void DrawRetainedRenderListWithDirtyRegions(SpriteBatch spriteBatch)
    {
        LastDirtyRectCount = _dirtyRegions.IsFullFrameDirty ? 1 : _dirtyRegions.RegionCount;
        LastDirtyAreaPercentage = _dirtyRegions.GetDirtyAreaCoverage();

        if (_dirtyRegions.IsFullFrameDirty || _dirtyRegions.RegionCount == 0)
        {
            DrawRetainedRenderList(spriteBatch);
            return;
        }

        DrawRetainedRenderListForDirtyRegions(spriteBatch, _dirtyRegions.Regions);
        LastDrawUsedPartialRedraw = true;
    }

    private void DrawRetainedRenderList(SpriteBatch spriteBatch)
    {
        for (var i = 0; i < _retainedRenderList.Count; i++)
        {
            var node = _retainedRenderList[i];
            if (!node.IsEffectivelyVisible)
            {
                continue;
            }

            if (TryDrawCachedNode(spriteBatch, node, out var subtreeEndIndexExclusive))
            {
                if (subtreeEndIndexExclusive > i + 1)
                {
                    i = subtreeEndIndexExclusive - 1;
                }

                continue;
            }

            DrawRetainedNode(spriteBatch, node);
        }
    }

    private void DrawRetainedRenderListForDirtyRegions(SpriteBatch spriteBatch, IReadOnlyList<LayoutRect> regions)
    {
        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var dirtyRegion = regions[regionIndex];
            UiDrawing.PushClip(spriteBatch, dirtyRegion);
            try
            {
                UiDrawing.DrawFilledRect(spriteBatch, dirtyRegion, _clearColor);
                for (var nodeIndex = 0; nodeIndex < _retainedRenderList.Count; nodeIndex++)
                {
                    var node = _retainedRenderList[nodeIndex];
                    if (!node.IsEffectivelyVisible)
                    {
                        continue;
                    }

                    if (node.HasSubtreeBoundsSnapshot && !Intersects(node.SubtreeBoundsSnapshot, dirtyRegion))
                    {
                        continue;
                    }

                    if (TryDrawCachedNode(spriteBatch, node, out var subtreeEndIndexExclusive))
                    {
                        if (subtreeEndIndexExclusive > nodeIndex + 1)
                        {
                            nodeIndex = subtreeEndIndexExclusive - 1;
                        }

                        continue;
                    }

                    DrawRetainedNode(spriteBatch, node);
                }
            }
            finally
            {
                UiDrawing.PopClip(spriteBatch);
            }
        }
    }

    private static void DrawRetainedNode(SpriteBatch spriteBatch, RenderNode node)
    {
        var pushedClipCount = 0;
        var pushedTransformCount = 0;
        var steps = node.RenderStateSteps;
        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];
            if (step.Kind == RenderStateStepKind.Clip)
            {
                UiDrawing.PushClip(spriteBatch, step.ClipRect);
                pushedClipCount++;
                continue;
            }

            UiDrawing.PushTransform(spriteBatch, step.Transform);
            pushedTransformCount++;
        }

        try
        {
            node.Visual.DrawSelf(spriteBatch);
        }
        finally
        {
            for (var i = 0; i < pushedTransformCount; i++)
            {
                UiDrawing.PopTransform(spriteBatch);
            }

            for (var i = 0; i < pushedClipCount; i++)
            {
                UiDrawing.PopClip(spriteBatch);
            }
        }
    }

    private void TrackDirtyBoundsForVisual(UIElement? visual)
    {
        if (visual == null || !IsPartOfVisualTree(visual))
        {
            _dirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
            return;
        }

        if (visual is IRenderDirtyBoundsHintProvider dirtyHintProvider &&
            dirtyHintProvider.TryConsumeRenderDirtyBoundsHint(out var hintedBounds))
        {
            _dirtyRegions.AddDirtyRegion(hintedBounds);
            return;
        }

        var hasOldBounds = false;
        var oldBounds = default(LayoutRect);
        if (_renderNodeIndices.TryGetValue(visual, out var renderNodeIndex))
        {
            var existingNode = _retainedRenderList[renderNodeIndex];
            hasOldBounds = existingNode.HasBoundsSnapshot;
            oldBounds = existingNode.BoundsSnapshot;
        }

        var hasNewBounds = visual.TryGetRenderBoundsInRootSpace(out var newBounds);
        AddDirtyBounds(hasOldBounds, oldBounds, hasNewBounds, newBounds);
    }

    private void RecordBoundsDelta(RenderNode previous, RenderNode updated)
    {
        if (previous.HasBoundsSnapshot &&
            updated.HasBoundsSnapshot &&
            AreRectsEqual(previous.BoundsSnapshot, updated.BoundsSnapshot))
        {
            return;
        }

        AddDirtyBounds(
            previous.HasBoundsSnapshot,
            previous.BoundsSnapshot,
            updated.HasBoundsSnapshot,
            updated.BoundsSnapshot);
    }

    private void AddDirtyBounds(bool hasOldBounds, LayoutRect oldBounds, bool hasNewBounds, LayoutRect newBounds)
    {
        if (hasOldBounds && hasNewBounds)
        {
            _dirtyRegions.AddDirtyRegion(Union(oldBounds, newBounds));
            return;
        }

        if (hasOldBounds)
        {
            _dirtyRegions.AddDirtyRegion(oldBounds);
            return;
        }

        if (hasNewBounds)
        {
            _dirtyRegions.AddDirtyRegion(newBounds);
            return;
        }
    }

    private RenderNode CreateRenderNode(
        UIElement visual,
        int traversalOrder,
        int depth,
        int subtreeEndIndexExclusive)
    {
        var hasBounds = visual.TryGetRenderBoundsInRootSpace(out var bounds);
        var steps = CaptureRenderStateSteps(
            visual,
            out var hasTransformState,
            out var hasClipState,
            out var localRenderStateStartIndex);
        var isEffectivelyVisible = IsEffectivelyVisible(visual);
        return new RenderNode(
            visual,
            traversalOrder,
            depth,
            bounds,
            hasBounds,
            steps,
            localRenderStateStartIndex,
            ComputeRenderStateSignature(steps),
            hasTransformState,
            hasClipState,
            isEffectivelyVisible,
            subtreeEndIndexExclusive,
            hasBounds,
            bounds,
            1,
            IsHighCostVisual(visual) ? 1 : 0,
            MixHash(17, visual.RenderCacheRenderVersion),
            MixHash(17, visual.RenderCacheLayoutVersion));
    }

    private static RenderStateStep[] CaptureRenderStateSteps(
        UIElement visual,
        out bool hasTransformState,
        out bool hasClipState,
        out int localRenderStateStartIndex)
    {
        hasTransformState = false;
        hasClipState = false;
        localRenderStateStartIndex = 0;

        var ancestry = new List<UIElement>(8);
        for (var current = visual; current != null; current = current.VisualParent)
        {
            ancestry.Add(current);
        }

        ancestry.Reverse();
        var steps = new List<RenderStateStep>(ancestry.Count * 2);
        for (var i = 0; i < ancestry.Count; i++)
        {
            var current = ancestry[i];
            if (ReferenceEquals(current, visual))
            {
                localRenderStateStartIndex = steps.Count;
            }

            if (current.TryGetLocalClipSnapshot(out var clipRect))
            {
                steps.Add(RenderStateStep.ForClip(clipRect));
                hasClipState = true;
            }

            if (current.TryGetLocalRenderTransformSnapshot(out var transform))
            {
                steps.Add(RenderStateStep.ForTransform(transform));
                hasTransformState = true;
            }
        }

        return steps.ToArray();
    }

    private bool IsEffectivelyVisible(UIElement visual)
    {
        if (!IsPartOfVisualTree(visual))
        {
            return false;
        }

        for (var current = visual; current != null; current = current.VisualParent)
        {
            if (!current.IsVisible)
            {
                return false;
            }

            if (ReferenceEquals(current, _visualRoot))
            {
                break;
            }
        }

        return true;
    }

    private static RenderCachePolicyContext CreateRenderCacheContext(RenderNode node)
    {
        return new RenderCachePolicyContext(
            node.IsEffectivelyVisible,
            node.HasSubtreeBoundsSnapshot,
            node.SubtreeBoundsSnapshot,
            node.HasTransformState,
            node.HasClipState,
            node.RenderStateSteps.Length,
            node.RenderStateSignature,
            node.SubtreeVisualCount,
            node.SubtreeHighCostVisualCount,
            node.SubtreeRenderVersionStamp,
            node.SubtreeLayoutVersionStamp);
    }

    private static SubtreeMetadata CreateSubtreeMetadataForNode(RenderNode node)
    {
        return new SubtreeMetadata(
            node.HasBoundsSnapshot,
            node.BoundsSnapshot,
            1,
            IsHighCostVisual(node.Visual) ? 1 : 0,
            MixHash(17, node.Visual.RenderCacheRenderVersion),
            MixHash(17, node.Visual.RenderCacheLayoutVersion));
    }

    private static SubtreeMetadata CreateSubtreeMetadataFromSubtreeNode(RenderNode node)
    {
        return new SubtreeMetadata(
            node.HasSubtreeBoundsSnapshot,
            node.SubtreeBoundsSnapshot,
            node.SubtreeVisualCount,
            node.SubtreeHighCostVisualCount,
            node.SubtreeRenderVersionStamp,
            node.SubtreeLayoutVersionStamp);
    }

    private static SubtreeMetadata MergeSubtreeMetadata(SubtreeMetadata root, SubtreeMetadata child)
    {
        var hasBounds = root.HasBoundsSnapshot || child.HasBoundsSnapshot;
        var bounds = root.BoundsSnapshot;
        if (!root.HasBoundsSnapshot && child.HasBoundsSnapshot)
        {
            bounds = child.BoundsSnapshot;
        }
        else if (root.HasBoundsSnapshot && child.HasBoundsSnapshot)
        {
            bounds = Union(root.BoundsSnapshot, child.BoundsSnapshot);
        }

        return new SubtreeMetadata(
            hasBounds,
            bounds,
            root.VisualCount + child.VisualCount,
            root.HighCostVisualCount + child.HighCostVisualCount,
            MixHash(root.RenderVersionStamp, child.RenderVersionStamp),
            MixHash(root.LayoutVersionStamp, child.LayoutVersionStamp));
    }

    private static bool IsHighCostVisual(UIElement visual)
    {
        return visual is TextBox or TextBlock or Shape;
    }

    private static bool IsValidCacheBounds(LayoutRect bounds)
    {
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return false;
        }

        var width = Math.Max(1, (int)MathF.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)MathF.Ceiling(bounds.Height));
        return width <= MaxCacheTextureDimension && height <= MaxCacheTextureDimension;
    }

    private static LayoutRect NormalizeBounds(LayoutRect bounds)
    {
        var x = bounds.X;
        var y = bounds.Y;
        var width = bounds.Width;
        var height = bounds.Height;
        if (width < 0f)
        {
            x += width;
            width = -width;
        }

        if (height < 0f)
        {
            y += height;
            height = -height;
        }

        return new LayoutRect(x, y, width, height);
    }

    private static bool IntersectsAny(LayoutRect bounds, IReadOnlyList<LayoutRect> regions)
    {
        for (var i = 0; i < regions.Count; i++)
        {
            if (Intersects(bounds, regions[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Intersects(LayoutRect left, LayoutRect right)
    {
        return left.X < right.X + right.Width &&
               left.X + left.Width > right.X &&
               left.Y < right.Y + right.Height &&
               left.Y + left.Height > right.Y;
    }

    private static LayoutRect Union(LayoutRect left, LayoutRect right)
    {
        var x = MathF.Min(left.X, right.X);
        var y = MathF.Min(left.Y, right.Y);
        var rightEdge = MathF.Max(left.X + left.Width, right.X + right.Width);
        var bottomEdge = MathF.Max(left.Y + left.Height, right.Y + right.Height);
        return new LayoutRect(x, y, MathF.Max(0f, rightEdge - x), MathF.Max(0f, bottomEdge - y));
    }

    private static void DrawCachedSubtreeBoundsOverlay(SpriteBatch spriteBatch, IReadOnlyList<LayoutRect> bounds)
    {
        for (var i = 0; i < bounds.Count; i++)
        {
            UiDrawing.DrawRectStroke(spriteBatch, bounds[i], 1f, new Color(76, 217, 100), opacity: 0.9f);
        }
    }

    private void TraceRenderCacheCountersIfEnabled()
    {
        if (!TraceRenderCacheCounters)
        {
            return;
        }

        var timestamp = Stopwatch.GetTimestamp();
        if (_lastRenderCacheCounterTraceTimestamp != 0 &&
            Stopwatch.GetElapsedTime(_lastRenderCacheCounterTraceTimestamp).TotalSeconds < 1d)
        {
            return;
        }

        _lastRenderCacheCounterTraceTimestamp = timestamp;
        Console.WriteLine(
            $"[UiCache] frame-hit:{LastFrameCacheHitCount} frame-miss:{LastFrameCacheMissCount} " +
            $"frame-rebuild:{LastFrameCacheRebuildCount} entries:{CacheEntryCount} bytes:{CacheBytes} " +
            $"overlay:{_lastFrameCachedSubtreeBounds.Count}");
    }

    private static bool AreViewportsEqual(Viewport left, Viewport right)
    {
        return left.X == right.X &&
               left.Y == right.Y &&
               left.Width == right.Width &&
               left.Height == right.Height;
    }

    private static bool AreRectsEqual(LayoutRect left, LayoutRect right)
    {
        const float epsilon = 0.0001f;
        return MathF.Abs(left.X - right.X) <= epsilon &&
               MathF.Abs(left.Y - right.Y) <= epsilon &&
               MathF.Abs(left.Width - right.Width) <= epsilon &&
               MathF.Abs(left.Height - right.Height) <= epsilon;
    }

    private static int ComputeRenderStateSignature(RenderStateStep[] steps)
    {
        var hash = 17;
        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];
            hash = MixHash(hash, (int)step.Kind);
            if (step.Kind == RenderStateStepKind.Clip)
            {
                hash = MixHash(hash, BitConverter.SingleToInt32Bits(step.ClipRect.X));
                hash = MixHash(hash, BitConverter.SingleToInt32Bits(step.ClipRect.Y));
                hash = MixHash(hash, BitConverter.SingleToInt32Bits(step.ClipRect.Width));
                hash = MixHash(hash, BitConverter.SingleToInt32Bits(step.ClipRect.Height));
                continue;
            }

            hash = MixHash(hash, step.Transform.GetHashCode());
        }

        return hash;
    }

    private static int MixHash(int hash, int value)
    {
        unchecked
        {
            return (hash * 31) ^ value;
        }
    }

    private readonly struct RenderNode
    {
        public RenderNode(
            UIElement visual,
            int traversalOrder,
            int depth,
            LayoutRect boundsSnapshot,
            bool hasBoundsSnapshot,
            RenderStateStep[] renderStateSteps,
            int localRenderStateStartIndex,
            int renderStateSignature,
            bool hasTransformState,
            bool hasClipState,
            bool isEffectivelyVisible,
            int subtreeEndIndexExclusive,
            bool hasSubtreeBoundsSnapshot,
            LayoutRect subtreeBoundsSnapshot,
            int subtreeVisualCount,
            int subtreeHighCostVisualCount,
            int subtreeRenderVersionStamp,
            int subtreeLayoutVersionStamp)
        {
            Visual = visual;
            TraversalOrder = traversalOrder;
            Depth = depth;
            BoundsSnapshot = boundsSnapshot;
            HasBoundsSnapshot = hasBoundsSnapshot;
            RenderStateSteps = renderStateSteps;
            LocalRenderStateStartIndex = localRenderStateStartIndex;
            RenderStateSignature = renderStateSignature;
            HasTransformState = hasTransformState;
            HasClipState = hasClipState;
            IsEffectivelyVisible = isEffectivelyVisible;
            SubtreeEndIndexExclusive = subtreeEndIndexExclusive;
            HasSubtreeBoundsSnapshot = hasSubtreeBoundsSnapshot;
            SubtreeBoundsSnapshot = subtreeBoundsSnapshot;
            SubtreeVisualCount = subtreeVisualCount;
            SubtreeHighCostVisualCount = subtreeHighCostVisualCount;
            SubtreeRenderVersionStamp = subtreeRenderVersionStamp;
            SubtreeLayoutVersionStamp = subtreeLayoutVersionStamp;
        }

        public UIElement Visual { get; }

        public int TraversalOrder { get; }

        public int Depth { get; }

        public LayoutRect BoundsSnapshot { get; }

        public bool HasBoundsSnapshot { get; }

        public RenderStateStep[] RenderStateSteps { get; }

        public int LocalRenderStateStartIndex { get; }

        public int RenderStateSignature { get; }

        public bool HasTransformState { get; }

        public bool HasClipState { get; }

        public bool IsEffectivelyVisible { get; }

        public int SubtreeEndIndexExclusive { get; }

        public bool HasSubtreeBoundsSnapshot { get; }

        public LayoutRect SubtreeBoundsSnapshot { get; }

        public int SubtreeVisualCount { get; }

        public int SubtreeHighCostVisualCount { get; }

        public int SubtreeRenderVersionStamp { get; }

        public int SubtreeLayoutVersionStamp { get; }

        public RenderNode WithSubtreeMetadata(
            int subtreeEndIndexExclusive,
            bool hasSubtreeBoundsSnapshot,
            LayoutRect subtreeBoundsSnapshot,
            int subtreeVisualCount,
            int subtreeHighCostVisualCount,
            int subtreeRenderVersionStamp,
            int subtreeLayoutVersionStamp)
        {
            return new RenderNode(
                Visual,
                TraversalOrder,
                Depth,
                BoundsSnapshot,
                HasBoundsSnapshot,
                RenderStateSteps,
                LocalRenderStateStartIndex,
                RenderStateSignature,
                HasTransformState,
                HasClipState,
                IsEffectivelyVisible,
                subtreeEndIndexExclusive,
                hasSubtreeBoundsSnapshot,
                subtreeBoundsSnapshot,
                subtreeVisualCount,
                subtreeHighCostVisualCount,
                subtreeRenderVersionStamp,
                subtreeLayoutVersionStamp);
        }
    }

    private readonly record struct SubtreeMetadata(
        bool HasBoundsSnapshot,
        LayoutRect BoundsSnapshot,
        int VisualCount,
        int HighCostVisualCount,
        int RenderVersionStamp,
        int LayoutVersionStamp);

    private enum RenderStateStepKind
    {
        Clip,
        Transform
    }

    private readonly struct RenderStateStep
    {
        private RenderStateStep(
            RenderStateStepKind kind,
            LayoutRect clipRect,
            Matrix transform)
        {
            Kind = kind;
            ClipRect = clipRect;
            Transform = transform;
        }

        public RenderStateStepKind Kind { get; }

        public LayoutRect ClipRect { get; }

        public Matrix Transform { get; }

        public static RenderStateStep ForClip(LayoutRect clipRect)
        {
            return new RenderStateStep(RenderStateStepKind.Clip, clipRect, Matrix.Identity);
        }

        public static RenderStateStep ForTransform(Matrix transform)
        {
            return new RenderStateStep(RenderStateStepKind.Transform, default, transform);
        }
    }
}

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
