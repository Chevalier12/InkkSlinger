using System;
using System.Collections.Generic;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    internal sealed partial class RetainedRenderController
    {
        private readonly UiRoot _root;
        private int _scrollViewportDirtyCount;
        private int _structureInvalidationCount;
        private int _dirtyRegionAddCount;
        private int _dirtyRegionFragmentationFullDirtyCount;
        private int _dirtyRegionBoundsDeltaSuppressedByTransformScrollCount;
        private readonly int[] _renderInvalidationKindCounts = new int[Enum.GetValues<RenderInvalidationKind>().Length];
        private readonly int[] _compositionMetadataUpdateKindCounts = new int[Enum.GetValues<RenderInvalidationKind>().Length];
        private string _lastDirtyRegionAddReason = "none";
        private string _lastFullDirtySource = "none";
        private string _lastRenderInvalidationKind = "none";
        private string _lastRenderInvalidationSource = "none";
        private string _lastCompositionMetadataUpdateKind = "none";
        private string _lastCompositionMetadataUpdateSource = "none";
        private bool _hasViewportScopedScrollDirtyRegion;
        private UIElement? _transformScrollDirtyBoundsSuppressionRoot;
        internal readonly List<RenderNode> RetainedRenderList = new();
        internal readonly Dictionary<UIElement, int> RenderNodeIndices = new();
        internal readonly VisualRecordStore VisualRecords = new();
        internal readonly Queue<UIElement> DirtyRenderQueue = new();
        internal readonly HashSet<UIElement> DirtyRenderSet = new();
        internal readonly HashSet<UIElement> DirtyRenderRootsRequireDeepSync = new();
        internal readonly HashSet<UIElement> DirtyRenderRootsAllowSubtreeRebuild = new();
        internal readonly List<DirtyRenderWorkItem> DirtyRenderWorkItems = new();
        internal readonly List<IndexedDirtyRenderCandidate> DirtyRenderCandidates = new();
        internal readonly List<UIElement> PendingAncestorMetadataRefreshRoots = new();
        internal readonly List<UIElement> AncestorMetadataRefreshBuffer = new();
        internal readonly HashSet<UIElement> AncestorMetadataRefreshSet = new();
        internal readonly List<UIElement> LastSynchronizedDirtyRenderRoots = new();
        internal readonly List<UIElement> LastCompletedSynchronizedDirtyRenderRoots = new();
        internal readonly List<DirtyRenderSpan> LastSynchronizedDirtyRenderSpans = new();
        internal readonly List<UIElement> LastCoalescedDirtyRenderRoots = new();
        internal readonly List<UIElement> DirtyRenderCompactionBuffer = new();
        internal readonly List<RenderNode> ActiveRetainedDrawPath = new();
        internal readonly DirtyRegionTracker DirtyRegions = new();
        internal readonly CompositionTreeIndex CompositionTreeIndex = new();
        internal readonly RetainedCompositionCompositor CompositionCompositor = new();
        internal readonly Dictionary<UIElement, RenderInvalidationKind> PendingCompositionMetadataUpdates = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<UIElement, int> _pendingDirtyAncestorCounts = new(ReferenceEqualityComparer.Instance);
        private int _lastCompositionRecordPassCount;
        private int _lastCompositionMetadataPassCount;
        private int _lastCompositionFullPassCount;
        private string _lastCompositionPrimaryMode = "none";
        private string _lastCompositionPrimaryReason = "none";
        private int _lastCompositionFrameVisualRecordRebuildTotal;
        private int _lastCompositionFrameMetadataUpdateTotal;
        private int _lastCompositionFrameVisualRecordTouchedTotal;
        private int _visualRecordTouchedCount;
        private int _fullCompositionFrameCount;
        private int _compositorOnlyFrameCount;
        private bool _lastCompositionMetadataOnlySyncSucceeded;
        private bool _lastCompositionMetadataOnlySyncWasTransformStableLayer;
        private bool _lastSyncUsedFullVisualRecordRefresh;
        private int _commandReplayCount;
        private int _commandReplayFallbackCount;
        private int _unsupportedCommandFallbackCount;
        private int _compositionSubtreeCullCount;
        private int _compositionSelfCullCount;
        private int _compositionTransformPushCount;
        private int _compositionOpacityPushCount;
        private int _cacheModeBoundaryCount;
        private int _bitmapCacheBoundaryCount;
        private int _deferredBitmapCacheBoundaryCount;
        private double _compositionCullingMilliseconds;
        private double _compositionCommandReplayMilliseconds;

        public RetainedRenderController(UiRoot root)
        {
            _root = root;
        }

        internal int NodeCount => RetainedRenderList.Count;

        internal int CompositionNodeCount => CompositionTreeIndex.Graph.NodeCount;

        internal int CompositionRebuildCount => CompositionTreeIndex.RebuildCount;

        internal double LastCompositionSyncBuildMilliseconds => CompositionTreeIndex.LastBuildMilliseconds;

        internal int VisualRecordCount => VisualRecords.RecordCount;

        internal int VisualRecordRebuildCount => VisualRecords.RebuildCount;

        internal int VisualRecordReuseCount => VisualRecords.ReuseCount;

        internal int CompositionMetadataUpdateCount { get; private set; }

        internal int CompositionMetadataUpdateMissCount { get; private set; }

        internal int DirtyQueueCount => DirtyRenderQueue.Count;

        internal int FullRedrawFallbackCount => DirtyRegions.FullRedrawFallbackCount;

        internal bool HasDirtyWork =>
            DirtyRegions.IsFullFrameDirty ||
            DirtyRegions.RegionCount > 0 ||
            PendingCompositionMetadataUpdates.Count > 0;

        internal int HighCostVisualCount =>
            RetainedRenderList.Count == 0
                ? 0
                : RetainedRenderList[0].SubtreeHighCostVisualCount;

        internal bool IsFullFrameDirty => DirtyRegions.IsFullFrameDirty;

        internal int DirtyRegionCount => DirtyRegions.RegionCount;

        internal double DirtyCoverage => DirtyRegions.GetDirtyAreaCoverage();

        internal bool IsDirtyRenderQueued(UIElement visual)
        {
            return DirtyRenderSet.Contains(visual);
        }

        internal void MarkFullFrameDirty(UiFullDirtyReason reason)
        {
            _root.RecordFullFrameDirtyReason(reason);
            _lastFullDirtySource = reason.ToString();
            DirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
        }

        internal void MarkFullFrameDirtyWithoutReason()
        {
            _lastFullDirtySource = "unspecified";
            DirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
        }

        internal IReadOnlyList<UIElement> GetRetainedVisualOrderSnapshot()
        {
            var visuals = new List<UIElement>(RetainedRenderList.Count);
            for (var i = 0; i < RetainedRenderList.Count; i++)
            {
                visuals.Add(RetainedRenderList[i].Visual);
            }

            return visuals;
        }

        internal IReadOnlyList<UIElement> GetCompositionVisualOrderSnapshot()
        {
            return CompositionTreeIndex.GetVisualOrderSnapshot();
        }

        internal RetainedCompositionGraph GetCompositionGraphSnapshot()
        {
            return CompositionTreeIndex.Graph;
        }

        internal IReadOnlyList<UIElement> GetDirtyRenderQueueSnapshot()
        {
            return new List<UIElement>(DirtyRenderQueue);
        }

        internal string GetDirtyRenderQueueSummary(int limit, Func<UIElement, string> describe)
        {
            if (LastCoalescedDirtyRenderRoots.Count > 0)
            {
                var count = Math.Min(limit, LastCoalescedDirtyRenderRoots.Count);
                var coalescedItems = new string[count];
                for (var i = 0; i < count; i++)
                {
                    coalescedItems[i] = describe(LastCoalescedDirtyRenderRoots[i]);
                }

                return string.Join(" | ", coalescedItems);
            }

            if (DirtyRenderQueue.Count == 0)
            {
                return "none";
            }

            var items = new List<string>(limit);
            foreach (var element in DirtyRenderQueue)
            {
                if (items.Count >= limit)
                {
                    break;
                }

                items.Add(describe(element));
            }

            return string.Join(" | ", items);
        }

        internal string GetLastSynchronizedDirtyRootSummary(int limit, Func<UIElement, string> describe)
        {
            var source = LastCompletedSynchronizedDirtyRenderRoots.Count > 0
                ? LastCompletedSynchronizedDirtyRenderRoots
                : LastSynchronizedDirtyRenderRoots;
            if (source.Count == 0)
            {
                return "none";
            }

            var count = Math.Min(limit, source.Count);
            var items = new string[count];
            for (var i = 0; i < count; i++)
            {
                items[i] = describe(source[i]);
            }

            return string.Join(" | ", items);
        }

        internal string GetDirtyRegionSummary(int limit)
        {
            if (DirtyRegions.RegionCount == 0)
            {
                return "none";
            }

            var count = Math.Min(limit, DirtyRegions.RegionCount);
            var items = new string[count];
            for (var i = 0; i < count; i++)
            {
                var region = DirtyRegions.Regions[i];
                items[i] = $"{region.X:0.#},{region.Y:0.#},{region.Width:0.#},{region.Height:0.#}";
            }

            return string.Join(" | ", items);
        }

        internal IReadOnlyList<LayoutRect> GetDirtyRegionsSnapshot()
        {
            return new List<LayoutRect>(DirtyRegions.Regions);
        }

        internal void SetDirtyRegionViewport(LayoutRect viewport)
        {
            DirtyRegions.SetViewport(viewport);
        }

        internal void ResetDirtyState()
        {
            DirtyRegions.Clear();
            _hasViewportScopedScrollDirtyRegion = false;
            ClearDirtyRenderQueue();
            ResetRetainedSyncTrackingState();
            LastCompletedSynchronizedDirtyRenderRoots.Clear();
        }

        internal int GetRetainedNodeSubtreeEndIndex(UIElement visual)
        {
            if (!RenderNodeIndices.TryGetValue(visual, out var nodeIndex))
            {
                throw new ArgumentException("Visual is not tracked in retained render list.", nameof(visual));
            }

            return RetainedRenderList[nodeIndex].SubtreeEndIndexExclusive;
        }

        internal LayoutRect GetRetainedNodeBounds(UIElement visual)
        {
            if (!RenderNodeIndices.TryGetValue(visual, out var nodeIndex))
            {
                throw new ArgumentException("Visual is not tracked in retained render list.", nameof(visual));
            }

            return RetainedRenderList[nodeIndex].BoundsSnapshot;
        }

        internal void RemoveRetainedNodeIndex(UIElement visual)
        {
            _ = RenderNodeIndices.Remove(visual);
        }

        internal void NotifyInvalidation(RetainedInvalidation invalidation)
        {
            NotifyInvalidation(invalidation, MapRetainedInvalidationKind(invalidation.Kind));
        }

        internal void NotifyInvalidation(RetainedInvalidation invalidation, RenderInvalidationKind renderInvalidationKind)
        {
            RecordRenderInvalidationTelemetry(invalidation, renderInvalidationKind);
            QueueCompositionMetadataUpdate(invalidation, renderInvalidationKind);

            if (invalidation.Kind == RetainedInvalidationKind.Structure)
            {
                _structureInvalidationCount++;
            }

            if (_root.UseDirtyRegionRendering)
            {
                TrackDirtyBoundsForVisual(invalidation.DirtyBoundsSource);
            }

            if (invalidation.RetainedSyncRoot != null &&
                ShouldEnqueueDirtyRenderNode(renderInvalidationKind, invalidation.Kind))
            {
                EnqueueDirtyRenderNode(
                    invalidation.RetainedSyncRoot,
                    invalidation.RequireDeepSync,
                    allowSubtreeRebuild: invalidation.Kind == RetainedInvalidationKind.Structure);
            }
        }

        internal RetainedInvalidation CreateRenderStateInvalidation(
            UIElement? requestedSource,
            UIElement? effectiveSource,
            bool requireDeepSync)
        {
            return new RetainedInvalidation(
                requestedSource,
                effectiveSource,
                ResolveRetainedSyncSource(requestedSource, effectiveSource, requireDeepSync),
                ResolveDirtyBoundsSource(requestedSource, effectiveSource),
                RetainedInvalidationKind.RenderState,
                requireDeepSync);
        }

        private void RecordRenderInvalidationTelemetry(
            RetainedInvalidation invalidation,
            RenderInvalidationKind renderInvalidationKind = RenderInvalidationKind.Content)
        {
            var kindIndex = (int)renderInvalidationKind;
            if ((uint)kindIndex < (uint)_renderInvalidationKindCounts.Length)
            {
                _renderInvalidationKindCounts[kindIndex]++;
            }

            _lastRenderInvalidationKind = renderInvalidationKind.ToString();
            _lastRenderInvalidationSource = DescribeElementForDiagnostics(
                invalidation.RequestedSource ??
                invalidation.EffectiveSource ??
                invalidation.RetainedSyncRoot ??
                invalidation.DirtyBoundsSource);
        }

        private static RenderInvalidationKind MapRetainedInvalidationKind(RetainedInvalidationKind kind)
        {
            return kind == RetainedInvalidationKind.Structure
                ? RenderInvalidationKind.Structure
                : RenderInvalidationKind.Content;
        }

        internal bool TryResolveInvalidationSource(UIElement source, bool allowRetainedAncestorFallback, out UIElement? effectiveSource)
        {
            effectiveSource = null;
            UIElement? connectedFallback = null;
            _root.ResetRenderInvalidationResolutionDebugState();
            var clipPromotionAncestor = allowRetainedAncestorFallback
                ? FindEscapingRenderClipAncestor(source)
                : null;
            _root._lastRenderInvalidationClipPromotionAncestorType = clipPromotionAncestor?.GetType().Name ?? "none";
            _root._lastRenderInvalidationClipPromotionAncestorName = clipPromotionAncestor is FrameworkElement clipPromotionFrameworkElement
                ? clipPromotionFrameworkElement.Name
                : string.Empty;
            for (var current = source; current != null; current = current.GetInvalidationParent())
            {
                if (allowRetainedAncestorFallback)
                {
                    if (TryGetIndexedVisualNodeCore(current, out _))
                    {
                        if (clipPromotionAncestor != null && !ReferenceEquals(current, clipPromotionAncestor))
                        {
                            if (_root.IsElementConnectedToVisualRootCore(current))
                            {
                                connectedFallback = current;
                            }

                            continue;
                        }

                        effectiveSource = current;
                        _root._lastRenderInvalidationEffectiveSourceResolution = ReferenceEquals(current, source)
                            ? "requested-indexed"
                            : clipPromotionAncestor != null && ReferenceEquals(current, clipPromotionAncestor)
                                ? "clip-promotion-ancestor"
                                : "ancestor-indexed";
                        return true;
                    }

                    if (_root.IsElementConnectedToVisualRootCore(current))
                    {
                        connectedFallback = current;
                    }

                    continue;
                }

                if (!TryGetIndexedVisualNodeCore(current, out _))
                {
                    continue;
                }

                if (!allowRetainedAncestorFallback && !ReferenceEquals(current, source))
                {
                    return false;
                }

                effectiveSource = current;
                _root._lastRenderInvalidationEffectiveSourceResolution = ReferenceEquals(current, source)
                    ? "requested-indexed"
                    : "ancestor-indexed";
                return true;
            }

            if (allowRetainedAncestorFallback && connectedFallback != null)
            {
                effectiveSource = connectedFallback;
                _root._lastRenderInvalidationEffectiveSourceResolution = "connected-fallback";
                return true;
            }

            _root._lastRenderInvalidationEffectiveSourceResolution = allowRetainedAncestorFallback
                ? "unresolved-no-connected-source"
                : "unresolved-no-indexed-source";

            return false;
        }

        private UIElement? ResolveRetainedSyncSource(UIElement? requestedSource, UIElement? effectiveSource, bool requireDeepSync)
        {
            if (requireDeepSync)
            {
                var deepSyncSource = requestedSource ?? effectiveSource;
                _root._lastRenderInvalidationRetainedSyncSourceElement = deepSyncSource;
                _root._lastRenderInvalidationRetainedSyncSourceType = deepSyncSource?.GetType().Name ?? "none";
                _root._lastRenderInvalidationRetainedSyncSourceName = deepSyncSource is FrameworkElement deepSyncFrameworkElement
                    ? deepSyncFrameworkElement.Name
                    : string.Empty;
                _root._lastRenderInvalidationRetainedSyncSourceResolution = deepSyncSource == null
                    ? "none"
                    : requestedSource != null
                        ? "explicit-deep-sync-requested"
                        : "explicit-deep-sync-effective";
                return deepSyncSource;
            }

            if (TryFindTransformScrollDirtyBoundsAnchor(requestedSource, out var transformScrollAnchor))
            {
                _root._lastRenderInvalidationRetainedSyncSourceElement = transformScrollAnchor;
                _root._lastRenderInvalidationRetainedSyncSourceType = transformScrollAnchor?.GetType().Name ?? "none";
                _root._lastRenderInvalidationRetainedSyncSourceName = transformScrollAnchor is FrameworkElement transformFrameworkElement
                    ? transformFrameworkElement.Name
                    : string.Empty;
                _root._lastRenderInvalidationRetainedSyncSourceResolution = "transform-scroll-anchor";
                return transformScrollAnchor;
            }

            if (ShouldAnchorEscapingRenderInvalidationToRequestedSource(requestedSource, effectiveSource))
            {
                _root._lastRenderInvalidationRetainedSyncSourceElement = requestedSource;
                _root._lastRenderInvalidationRetainedSyncSourceType = requestedSource?.GetType().Name ?? "none";
                _root._lastRenderInvalidationRetainedSyncSourceName = requestedSource is FrameworkElement requestedFrameworkElement
                    ? requestedFrameworkElement.Name
                    : string.Empty;
                _root._lastRenderInvalidationRetainedSyncSourceResolution = "clip-promotion-requested";
                return requestedSource;
            }

            _root._lastRenderInvalidationRetainedSyncSourceElement = effectiveSource;
            _root._lastRenderInvalidationRetainedSyncSourceType = effectiveSource?.GetType().Name ?? "none";
            _root._lastRenderInvalidationRetainedSyncSourceName = effectiveSource is FrameworkElement effectiveFrameworkElement
                ? effectiveFrameworkElement.Name
                : string.Empty;
            _root._lastRenderInvalidationRetainedSyncSourceResolution = effectiveSource == null
                ? "none"
                : "effective-source";
            return effectiveSource;
        }

        private UIElement? ResolveDirtyBoundsSource(UIElement? requestedSource, UIElement? effectiveSource)
        {
            if (TryFindTransformScrollDirtyBoundsAnchor(requestedSource, out var transformScrollAnchor))
            {
                _root._lastDirtyBoundsSourceResolution = "transform-scroll-anchor";
                return transformScrollAnchor;
            }

            if (ShouldAnchorEscapingRenderInvalidationToRequestedSource(requestedSource, effectiveSource))
            {
                _root._lastDirtyBoundsSourceResolution = "clip-promotion-requested";
                return requestedSource;
            }

            _root._lastDirtyBoundsSourceResolution = effectiveSource == null
                ? "none"
                : "effective-source";
            return effectiveSource;
        }

        private bool TryFindTransformScrollDirtyBoundsAnchor(UIElement? source, out UIElement? anchor)
        {
            anchor = null;
            if (source == null)
            {
                return false;
            }

            if (source is ScrollViewer viewer &&
                viewer.TryGetContentViewportClipRect(out _) &&
                TryGetIndexedVisualNodeCore(viewer, out _))
            {
                anchor = viewer;
                return true;
            }

            if (!TryGetIndexedVisualNodeCore(source, out _))
            {
                return false;
            }

            if (!IsTransformScrollRetainedSyncCandidate(source) ||
                !TryGetTransformScrollOwner(source, out var transformOwner) ||
                !transformOwner.TryGetContentViewportClipRect(out _))
            {
                return false;
            }

            anchor = source;
            return true;
        }

        private static bool TryGetTransformScrollOwner(UIElement element, out ScrollViewer owner)
        {
            owner = null!;

            var visualOwner = element.VisualParent as ScrollViewer;
            if (visualOwner != null && ReferenceEquals(visualOwner.Content, element))
            {
                owner = visualOwner;
                return true;
            }

            var logicalOwner = element.LogicalParent as ScrollViewer;
            if (logicalOwner != null && ReferenceEquals(logicalOwner.Content, element))
            {
                owner = logicalOwner;
                return true;
            }

            return false;
        }

        private bool ShouldAnchorEscapingRenderInvalidationToRequestedSource(UIElement? requestedSource, UIElement? effectiveSource)
        {
            if (requestedSource == null ||
                effectiveSource == null ||
                ReferenceEquals(requestedSource, effectiveSource) ||
                !TryGetIndexedVisualNodeCore(requestedSource, out _))
            {
                return false;
            }

            // Keep retained sync and dirty-bounds tracking anchored to the actual mutated subtree
            // when clip promotion only exists to widen coverage for transformed/effected descendants.
            return ReferenceEquals(FindEscapingRenderClipAncestor(requestedSource), effectiveSource);
        }

        private static bool IsTransformScrollRetainedSyncCandidate(UIElement element)
        {
            return element is VirtualizingStackPanel ||
                   element is IScrollTransformContent && ScrollViewer.GetIsTransformContentLayerStable(element);
        }

        internal UIElement? FindEscapingRenderClipAncestor(UIElement source)
        {
            var foundEscapingRender = false;

            for (var current = source; current != null; current = current.GetInvalidationParent())
            {
                foundEscapingRender |= CanRenderOutsideOwnSlot(current);
                if (foundEscapingRender && current.TryGetLocalClipSnapshot(out _))
                {
                    return current;
                }
            }

            return null;
        }

        private static bool CanRenderOutsideOwnSlot(UIElement element)
        {
            if (element.TryGetLocalRenderTransformSnapshot(out var transform) && !AreTransformsEffectivelyEqual(transform, Matrix.Identity))
            {
                return true;
            }

            if (element.Effect == null)
            {
                return false;
            }

            var slot = element.LayoutSlot;
            var renderBounds = element.Effect.GetRenderBounds(element);
            return renderBounds.X < slot.X - 0.01f ||
                   renderBounds.Y < slot.Y - 0.01f ||
                   renderBounds.X + renderBounds.Width > slot.X + slot.Width + 0.01f ||
                   renderBounds.Y + renderBounds.Height > slot.Y + slot.Height + 0.01f;
        }

        internal static bool AreTransformsEffectivelyEqual(Matrix left, Matrix right)
        {
            return MathF.Abs(left.M11 - right.M11) <= 0.0001f &&
                   MathF.Abs(left.M12 - right.M12) <= 0.0001f &&
                   MathF.Abs(left.M13 - right.M13) <= 0.0001f &&
                   MathF.Abs(left.M14 - right.M14) <= 0.0001f &&
                   MathF.Abs(left.M21 - right.M21) <= 0.0001f &&
                   MathF.Abs(left.M22 - right.M22) <= 0.0001f &&
                   MathF.Abs(left.M23 - right.M23) <= 0.0001f &&
                   MathF.Abs(left.M24 - right.M24) <= 0.0001f &&
                   MathF.Abs(left.M31 - right.M31) <= 0.0001f &&
                   MathF.Abs(left.M32 - right.M32) <= 0.0001f &&
                   MathF.Abs(left.M33 - right.M33) <= 0.0001f &&
                   MathF.Abs(left.M34 - right.M34) <= 0.0001f &&
                   MathF.Abs(left.M41 - right.M41) <= 0.0001f &&
                   MathF.Abs(left.M42 - right.M42) <= 0.0001f &&
                   MathF.Abs(left.M43 - right.M43) <= 0.0001f &&
                   MathF.Abs(left.M44 - right.M44) <= 0.0001f;
        }

        internal void NotifyScrollViewportChanged(ScrollViewer viewer, LayoutRect viewport)
        {
            if (viewport.Width <= 0f || viewport.Height <= 0f)
            {
                return;
            }

            var content = viewer.Content as UIElement;
            var invalidation = new RetainedInvalidation(
                content ?? viewer,
                content ?? viewer,
                null,
                viewer,
                RetainedInvalidationKind.ScrollViewport,
                RequireDeepSync: false);
            RecordRenderInvalidationTelemetry(invalidation, RenderInvalidationKind.Transform);
            QueueCompositionMetadataUpdate(invalidation, RenderInvalidationKind.Transform);
            _scrollViewportDirtyCount++;
            _root._dirtyBoundsEventTrace.Add(
                $"{nameof(ScrollViewer)}#{viewer.Name}:scroll-viewport:{viewport.X:0.##},{viewport.Y:0.##},{viewport.Width:0.##},{viewport.Height:0.##}");
            _root._lastDirtyBoundsVisualElement = viewer;
            _root._lastDirtyBoundsVisualType = nameof(ScrollViewer);
            _root._lastDirtyBoundsVisualName = viewer.Name;
            _root._lastDirtyBoundsSourceResolution = "scroll-viewport";
            _root._lastDirtyBoundsUsedHint = true;
            _root._lastDirtyBounds = viewport;
            _root._hasLastDirtyBounds = true;

            if (_root.UseDirtyRegionRendering)
            {
                AddDirtyRegionForDiagnostics(viewport, "scroll-viewport");
            }
        }

        internal void Sync(Viewport viewport)
        {
            SyncDirtyRegionViewport(viewport);
            _root.EnsureVisualIndexCurrent();
            SynchronizeRetainedRenderList();
        }

        internal void SyncIfNeeded()
        {
            if (_root._renderListNeedsFullRebuild ||
                DirtyRenderQueue.Count > 0 ||
                DirtyRenderSet.Count > 0 ||
                PendingCompositionMetadataUpdates.Count > 0)
            {
                _root.EnsureVisualIndexCurrent();
                SynchronizeRetainedRenderList();
            }
        }

        internal void Draw(SpriteBatch spriteBatch, bool useDirtyRegions, RetainedDrawThresholds thresholds)
        {
            _ = thresholds;
            UpdateVisualRecords();
            DrawMimisbrunnrComposition(spriteBatch, useDirtyRegions);
        }

        internal void AppendDrawOrderForClip(LayoutRect clipRect, List<UIElement> visuals)
        {
            _ = TraverseNodesWithinClip(spriteBatch: null, clipRect, visuals);
        }

        internal void UpdateVisualRecordsForTests()
        {
            UpdateVisualRecords();
        }

        internal VisualCommandList GetVisualRecordForTests(UIElement visual)
        {
            if (!VisualRecords.TryGetRecord(visual, out var commands))
            {
                throw new ArgumentException("Visual does not have a retained visual record.", nameof(visual));
            }

            return commands;
        }

        internal (int NodesVisited, int NodesDrawn, int LocalClipPushCount) GetTraversalMetricsForClip(LayoutRect clipRect)
        {
            var metrics = TraverseNodesWithinClip(spriteBatch: null, clipRect);
            return (metrics.NodesVisited, metrics.NodesDrawn, metrics.ClipPushCount);
        }

        private void ClearMimisbrunnrRegion(SpriteBatch spriteBatch, LayoutRect region)
        {
            UiDrawing.DrawFilledRect(
                spriteBatch,
                region,
                _root._clearColor);
        }

        private void UpdateVisualRecords()
        {
            if (RetainedRenderList.Count == 0)
            {
                return;
            }

            var requiresFullRefresh =
                VisualRecords.RecordCount != RetainedRenderList.Count ||
                _lastSyncUsedFullVisualRecordRefresh ||
                _renderListNeedsFullRebuild;
            if (_lastCompositionMetadataOnlySyncSucceeded &&
                !requiresFullRefresh &&
                LastCompletedSynchronizedDirtyRenderRoots.Count == 0)
            {
                UpdateCompositionFrameTelemetry();
                return;
            }

            if (requiresFullRefresh)
            {
                RefreshAllVisualRecords();
                _lastSyncUsedFullVisualRecordRefresh = false;
                UpdateCompositionFrameTelemetry();
                return;
            }

            UpdateDirtyVisualRecords();
            UpdateCompositionFrameTelemetry();
        }

        private void UpdateDirtyVisualRecords()
        {
            if (LastCompletedSynchronizedDirtyRenderRoots.Count == 0)
            {
                return;
            }

            for (var i = 0; i < LastCompletedSynchronizedDirtyRenderRoots.Count; i++)
            {
                var root = LastCompletedSynchronizedDirtyRenderRoots[i];
                if (!RenderNodeIndices.TryGetValue(root, out var nodeIndex) ||
                    (uint)nodeIndex >= (uint)RetainedRenderList.Count)
                {
                    RefreshAllVisualRecords();
                    _lastSyncUsedFullVisualRecordRefresh = false;
                    return;
                }

                _ = VisualRecords.RecordOrReuse(RetainedRenderList[nodeIndex].Visual);
                _visualRecordTouchedCount++;
            }
        }

        private void RefreshAllVisualRecords()
        {
            var retainedVisuals = new List<UIElement>(RetainedRenderList.Count);
            for (var i = 0; i < RetainedRenderList.Count; i++)
            {
                var visual = RetainedRenderList[i].Visual;
                retainedVisuals.Add(visual);
                _ = VisualRecords.RecordOrReuse(visual);
                _visualRecordTouchedCount++;
            }

            VisualRecords.RetainOnly(retainedVisuals);
        }

        private void UpdateCompositionFrameTelemetry()
        {
            var visualRecordRebuildDelta = Math.Max(0, VisualRecords.RebuildCount - _lastCompositionFrameVisualRecordRebuildTotal);
            var visualRecordTouchedDelta = Math.Max(0, _visualRecordTouchedCount - _lastCompositionFrameVisualRecordTouchedTotal);
            var compositionMetadataUpdateDelta = Math.Max(0, CompositionMetadataUpdateCount - _lastCompositionFrameMetadataUpdateTotal);

            _lastCompositionFrameVisualRecordRebuildTotal = VisualRecords.RebuildCount;
            _lastCompositionFrameVisualRecordTouchedTotal = _visualRecordTouchedCount;
            _lastCompositionFrameMetadataUpdateTotal = CompositionMetadataUpdateCount;

            _lastCompositionRecordPassCount = visualRecordTouchedDelta > 0 ? 1 : 0;
            _lastCompositionMetadataPassCount = 0;
            _lastCompositionFullPassCount = 0;
            _lastCompositionPrimaryMode = "none";
            _lastCompositionPrimaryReason = "none";

            if (IsFullFrameDirty)
            {
                _lastCompositionFullPassCount = 1;
                _lastCompositionPrimaryMode = "FullComposition";
                _lastCompositionPrimaryReason = "full-dirty";
                _fullCompositionFrameCount++;
                return;
            }

            if (_lastCompositionMetadataOnlySyncSucceeded &&
                compositionMetadataUpdateDelta > 0 &&
                visualRecordRebuildDelta == 0)
            {
                _lastCompositionMetadataPassCount = 1;
                _lastCompositionPrimaryMode = "MetadataOnly";
                _lastCompositionPrimaryReason = "composition-metadata-only";
                _compositorOnlyFrameCount++;
                return;
            }

            if (compositionMetadataUpdateDelta > 0 &&
                visualRecordTouchedDelta > 0)
            {
                _lastCompositionMetadataPassCount = 1;
                _lastCompositionPrimaryMode = "MetadataAndDirtyRecords";
                _lastCompositionPrimaryReason = "composition-metadata-and-dirty-records";
                _compositorOnlyFrameCount++;
                return;
            }

            if (DirtyRegionCount > 0 || visualRecordRebuildDelta > 0)
            {
                _lastCompositionFullPassCount = 1;
                _lastCompositionPrimaryMode = "FullComposition";
                _lastCompositionPrimaryReason = "full-composition";
                _fullCompositionFrameCount++;
            }
        }

        private void DrawMimisbrunnrComposition(SpriteBatch spriteBatch, bool useDirtyRegions)
        {
            if (!useDirtyRegions || DirtyRegions.RegionCount == 0)
            {
                DrawFullMimisbrunnrComposition(spriteBatch);
                return;
            }

            var regions = DirtyRegions.Regions;
            _root.LastDirtyRectCount = regions.Count;
            _root.LastDirtyAreaPercentage = DirtyRegions.GetDirtyAreaCoverage();

            for (var i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                if (region.Width <= 0f || region.Height <= 0f)
                {
                    continue;
                }

                UiDrawing.PushAbsoluteClip(spriteBatch, region);
                try
                {
                    ClearMimisbrunnrRegion(spriteBatch, region);
                    var metrics = DrawCompositionWithinClip(spriteBatch, region);
                    _root._lastDirtyRegionTraversalCount++;
                    _root._lastRetainedNodesVisited += metrics.NodesVisited;
                    _root._lastRetainedNodesDrawn += metrics.NodesDrawn;
                }
                finally
                {
                    UiDrawing.PopClip(spriteBatch);
                }
            }
        }

        private void DrawFullMimisbrunnrComposition(SpriteBatch spriteBatch)
        {
            var metrics = DrawCompositionWithinClip(
                spriteBatch,
                ToLayoutRect(spriteBatch.GraphicsDevice.ScissorRectangle));
            _root._lastRetainedTraversalCount++;
            _root._lastRetainedNodesVisited += metrics.NodesVisited;
            _root._lastRetainedNodesDrawn += metrics.NodesDrawn;
            _root.LastDirtyRectCount = 1;
            _root.LastDirtyAreaPercentage = 1d;
            _root.RecordFullRetainedDrawWithoutFullClearIfNeeded();
        }

        private RetainedCompositionDrawMetrics DrawCompositionWithinClip(SpriteBatch spriteBatch, LayoutRect clipRect)
        {
            var metrics = CompositionCompositor.Draw(CompositionTreeIndex.Graph, VisualRecords, spriteBatch, clipRect);
            AccumulateCompositionMetrics(metrics);
            return metrics;
        }

        private RenderTraversalMetrics TraverseNodesWithinClip(SpriteBatch? spriteBatch, LayoutRect clipRect, List<UIElement>? visuals = null)
        {
            var metrics = CompositionCompositor.Draw(CompositionTreeIndex.Graph, VisualRecords, spriteBatch, clipRect, visuals);
            AccumulateCompositionMetrics(metrics);
            return new RenderTraversalMetrics(metrics.NodesVisited, metrics.NodesDrawn, metrics.ClipPushCount);
        }

        private void AccumulateCompositionMetrics(RetainedCompositionDrawMetrics metrics)
        {
            _commandReplayCount += metrics.CommandReplayCount;
            _commandReplayFallbackCount += metrics.CommandReplayFallbackCount;
            _unsupportedCommandFallbackCount += metrics.UnsupportedCommandFallbackCount;
            _compositionSubtreeCullCount += metrics.SubtreesCulled;
            _compositionSelfCullCount += metrics.SelfCulled;
            _compositionTransformPushCount += metrics.TransformPushCount;
            _compositionOpacityPushCount += metrics.OpacityPushCount;
            _cacheModeBoundaryCount += metrics.CacheModeBoundaryCount;
            _bitmapCacheBoundaryCount += metrics.BitmapCacheBoundaryCount;
            _deferredBitmapCacheBoundaryCount += metrics.DeferredBitmapCacheBoundaryCount;
            _compositionCullingMilliseconds += metrics.CullingMilliseconds;
            _compositionCommandReplayMilliseconds += metrics.CommandReplayMilliseconds;
        }

        private static bool ShouldDrawRetainedNodeSelf(RenderNode node, LayoutRect clipRect)
        {
            return !node.HasBoundsSnapshot || Intersects(node.BoundsSnapshot, clipRect);
        }

        private void DrawRetainedNodeSelf(SpriteBatch spriteBatch, RenderNode node)
        {
            if (!VisualRecords.TryGetRecord(node.Visual, out var commands))
            {
                _commandReplayFallbackCount++;
                node.Visual.DrawSelf(spriteBatch);
                return;
            }

            var slot = node.Visual.LayoutSlot;
            var hasSlotTranslation = slot.X != 0f || slot.Y != 0f;
            if (hasSlotTranslation)
            {
                UiDrawing.PushTransform(spriteBatch, Matrix.CreateTranslation(slot.X, slot.Y, 0f));
            }

            try
            {
                if (VisualCommandReplayer.TryReplay(spriteBatch, commands))
                {
                    _commandReplayCount++;
                    return;
                }
            }
            finally
            {
                if (hasSlotTranslation)
                {
                    UiDrawing.PopTransform(spriteBatch);
                }
            }

            _commandReplayFallbackCount++;
            _unsupportedCommandFallbackCount += commands.UnsupportedCommandCount > 0 ? 1 : 0;
            node.Visual.DrawSelf(spriteBatch);
        }

        private void SyncRetainedDrawState(SpriteBatch spriteBatch, RenderNode node, ref int clipPushCount)
        {
            while (ActiveRetainedDrawPath.Count > node.Depth)
            {
                PopRetainedDrawNodeState(spriteBatch, ActiveRetainedDrawPath[^1]);
                ActiveRetainedDrawPath.RemoveAt(ActiveRetainedDrawPath.Count - 1);
            }

            ApplyRetainedDrawNodeState(spriteBatch, node, ref clipPushCount);
            ActiveRetainedDrawPath.Add(node);
        }

        private void ResetRetainedDrawState(SpriteBatch spriteBatch)
        {
            for (var i = ActiveRetainedDrawPath.Count - 1; i >= 0; i--)
            {
                PopRetainedDrawNodeState(spriteBatch, ActiveRetainedDrawPath[i]);
            }

            ActiveRetainedDrawPath.Clear();
        }

        private static void ApplyRetainedDrawNodeState(SpriteBatch spriteBatch, RenderNode node, ref int clipPushCount)
        {
            UiDrawing.PushLocalState(
                spriteBatch,
                node.HasLocalTransform,
                node.LocalTransform,
                node.HasLocalClip,
                node.LocalClipRect);

            if (node.HasLocalClip)
            {
                clipPushCount++;
            }
        }

        private static void PopRetainedDrawNodeState(SpriteBatch spriteBatch, RenderNode node)
        {
            UiDrawing.PopLocalState(spriteBatch, node.HasLocalTransform, node.HasLocalClip);
        }

        private void TrackDirtyBoundsForVisual(UIElement? visual)
        {
            _root._lastDirtyBoundsVisualElement = visual;
            _root._lastDirtyBoundsVisualType = visual?.GetType().Name ?? "none";
            _root._lastDirtyBoundsVisualName = visual is FrameworkElement frameworkElement
                ? frameworkElement.Name
                : string.Empty;
            _root._lastDirtyBoundsUsedHint = false;
            _root._hasLastDirtyBounds = false;
            _root._dirtyBoundsEventTrace.Add($"{_root._lastDirtyBoundsVisualType}#{_root._lastDirtyBoundsVisualName}:begin");
            if (visual == null || !IsPartOfVisualTree(visual))
            {
                _root._dirtyBoundsEventTrace.Add($"{_root._lastDirtyBoundsVisualType}#{_root._lastDirtyBoundsVisualName}:detached");
                MarkFullFrameDirty(UiFullDirtyReason.DetachedVisual);
                return;
            }

            if (TryGetTransformScrollDirtyBoundsHint(visual, out var transformScrollBounds))
            {
                if (!TryClipDirtyBoundsToVisualChain(visual, ref transformScrollBounds))
                {
                    return;
                }

                _root._lastDirtyBoundsUsedHint = true;
                _root._lastDirtyBounds = transformScrollBounds;
                _root._hasLastDirtyBounds = true;
                if (AddDirtyRegionForDiagnostics(transformScrollBounds, "scroll-clip-hint"))
                {
                    _root._dirtyBoundsEventTrace.Add($"{_root._lastDirtyBoundsVisualType}#{_root._lastDirtyBoundsVisualName}:scroll-clip-hint:{transformScrollBounds.X:0.##},{transformScrollBounds.Y:0.##},{transformScrollBounds.Width:0.##},{transformScrollBounds.Height:0.##}");
                }

                return;
            }

            if (TryGetAncestorTransformScrollDirtyBoundsHint(visual, out var ancestorTransformScrollBounds))
            {
                if (!TryClipDirtyBoundsToVisualChain(visual, ref ancestorTransformScrollBounds))
                {
                    return;
                }

                _root._lastDirtyBoundsUsedHint = true;
                _root._lastDirtyBounds = ancestorTransformScrollBounds;
                _root._hasLastDirtyBounds = true;
                if (AddDirtyRegionForDiagnostics(ancestorTransformScrollBounds, "ancestor-scroll-clip-hint"))
                {
                    _root._dirtyBoundsEventTrace.Add($"{_root._lastDirtyBoundsVisualType}#{_root._lastDirtyBoundsVisualName}:ancestor-scroll-clip-hint:{ancestorTransformScrollBounds.X:0.##},{ancestorTransformScrollBounds.Y:0.##},{ancestorTransformScrollBounds.Width:0.##},{ancestorTransformScrollBounds.Height:0.##}");
                }

                return;
            }

            if (visual is IRenderDirtyBoundsHintProvider dirtyHintProvider &&
                dirtyHintProvider.TryConsumeRenderDirtyBoundsHint(out var hintedBounds))
            {
                if (!TryClipDirtyBoundsToVisualChain(visual, ref hintedBounds))
                {
                    return;
                }

                _root._lastDirtyBoundsUsedHint = true;
                _root._lastDirtyBounds = hintedBounds;
                _root._hasLastDirtyBounds = true;
                _root._dirtyBoundsEventTrace.Add($"{_root._lastDirtyBoundsVisualType}#{_root._lastDirtyBoundsVisualName}:hint:{hintedBounds.X:0.##},{hintedBounds.Y:0.##},{hintedBounds.Width:0.##},{hintedBounds.Height:0.##}");
                AddDirtyRegionForDiagnostics(hintedBounds, "hint");
                return;
            }

            var hasOldBounds = false;
            var oldBounds = default(LayoutRect);
            if (RenderNodeIndices.TryGetValue(visual, out var renderNodeIndex))
            {
                var existingNode = RetainedRenderList[renderNodeIndex];
                hasOldBounds = existingNode.HasBoundsSnapshot;
                oldBounds = existingNode.BoundsSnapshot;
            }

            var hasNewBounds = visual.TryGetRenderBoundsInRootSpace(out var newBounds);
            if (hasNewBounds)
            {
                _root._lastDirtyBounds = newBounds;
                _root._hasLastDirtyBounds = true;
            }
            else if (hasOldBounds)
            {
                _root._lastDirtyBounds = oldBounds;
                _root._hasLastDirtyBounds = true;
            }

            _root._dirtyBoundsEventTrace.Add(
                $"{_root._lastDirtyBoundsVisualType}#{_root._lastDirtyBoundsVisualName}:bounds:" +
                $"{(hasOldBounds ? $"{oldBounds.X:0.##},{oldBounds.Y:0.##},{oldBounds.Width:0.##},{oldBounds.Height:0.##}" : "none")}" +
                "->" +
                $"{(hasNewBounds ? $"{newBounds.X:0.##},{newBounds.Y:0.##},{newBounds.Width:0.##},{newBounds.Height:0.##}" : "none")}");
            AddDirtyBounds(hasOldBounds, oldBounds, hasNewBounds, newBounds);
        }

        internal void RecordBoundsDelta(RenderNode previous, RenderNode updated)
        {
            if (_transformScrollDirtyBoundsSuppressionRoot != null &&
                (ReferenceEquals(updated.Visual, _transformScrollDirtyBoundsSuppressionRoot) ||
                 IsDescendantOf(updated.Visual, _transformScrollDirtyBoundsSuppressionRoot)))
            {
                _dirtyRegionBoundsDeltaSuppressedByTransformScrollCount++;
                return;
            }

            var previousBounds = previous.BoundsSnapshot;
            if (previous.HasBoundsSnapshot &&
                updated.HasBoundsSnapshot &&
                AreRectsEqual(previousBounds, updated.BoundsSnapshot))
            {
                return;
            }

            if (TryGetTransformScrollDirtyBoundsHint(updated.Visual, out var transformScrollBounds) ||
                TryGetTransformScrollDirtyBoundsHint(previous.Visual, out transformScrollBounds))
            {
                var clipVisual = TryGetTransformScrollDirtyBoundsHint(updated.Visual, out _)
                    ? updated.Visual
                    : previous.Visual;
                if (!TryClipDirtyBoundsToVisualChain(clipVisual, ref transformScrollBounds))
                {
                    return;
                }

                _root._lastDirtyBoundsVisualType = updated.Visual.GetType().Name;
                _root._lastDirtyBoundsVisualName = updated.Visual is FrameworkElement updatedFrameworkElement
                    ? updatedFrameworkElement.Name
                    : string.Empty;
                _root._lastDirtyBoundsVisualElement = updated.Visual;
                _root._lastDirtyBoundsUsedHint = true;
                _root._lastDirtyBounds = transformScrollBounds;
                _root._hasLastDirtyBounds = true;
                _root._dirtyBoundsEventTrace.Add($"{_root._lastDirtyBoundsVisualType}#{_root._lastDirtyBoundsVisualName}:scroll-clip-hint:{transformScrollBounds.X:0.##},{transformScrollBounds.Y:0.##},{transformScrollBounds.Width:0.##},{transformScrollBounds.Height:0.##}");
                AddDirtyRegionForDiagnostics(transformScrollBounds, "scroll-clip-hint-delta");
                return;
            }

            AddDirtyBounds(
                previous.HasBoundsSnapshot,
                previousBounds,
                updated.HasBoundsSnapshot,
                updated.BoundsSnapshot);
        }

        private void AddDirtyBounds(bool hasOldBounds, LayoutRect oldBounds, bool hasNewBounds, LayoutRect newBounds)
        {
            if (hasOldBounds && hasNewBounds)
            {
                if (AreRectsEqual(oldBounds, newBounds))
                {
                    AddDirtyRegionForDiagnostics(oldBounds, "unchanged");
                    return;
                }

                if (IntersectsOrTouches(oldBounds, newBounds))
                {
                    AddDirtyRegionForDiagnostics(Union(oldBounds, newBounds), "union");
                    return;
                }

                AddDirtyRegionForDiagnostics(oldBounds, "old");
                AddDirtyRegionForDiagnostics(newBounds, "new");
                return;
            }

            if (hasOldBounds)
            {
                AddDirtyRegionForDiagnostics(oldBounds, "old-only");
                return;
            }

            if (hasNewBounds)
            {
                AddDirtyRegionForDiagnostics(newBounds, "new-only");
            }
        }

        private bool AddDirtyRegionForDiagnostics(LayoutRect bounds, string reason)
        {
            var wasFullFrameDirty = DirtyRegions.IsFullFrameDirty;
            var fallbackCountBefore = DirtyRegions.FullRedrawFallbackCount;
            var accepted = DirtyRegions.AddDirtyRegion(bounds);
            if (accepted)
            {
                _root._dirtyBoundsEventTrace.Add($"dirty-add:{reason}:{bounds.X:0.##},{bounds.Y:0.##},{bounds.Width:0.##},{bounds.Height:0.##}");
                _dirtyRegionAddCount++;
                _lastDirtyRegionAddReason = reason;
                if (IsViewportScopedScrollDirtyRegionReason(reason))
                {
                    _hasViewportScopedScrollDirtyRegion = true;
                }
            }

            if (!wasFullFrameDirty &&
                DirtyRegions.IsFullFrameDirty &&
                DirtyRegions.FullRedrawFallbackCount > fallbackCountBefore)
            {
                _dirtyRegionFragmentationFullDirtyCount++;
                _lastFullDirtySource = $"dirty-region-fragmentation:{reason}";
                _root._dirtyBoundsEventTrace.Add($"full-dirty:dirty-region-fragmentation:{reason}");
            }

            return accepted;
        }

        private bool TryClipDirtyBoundsToVisualChain(UIElement? visual, ref LayoutRect bounds)
        {
            if (visual == null)
            {
                return bounds.Width > 0f && bounds.Height > 0f;
            }

            var clipped = bounds;
            var intersectedAnyClip = false;
            for (var current = visual; current != null; current = current.GetInvalidationParent())
            {
                if (!current.TryGetLocalClipSnapshot(out var clipRect) || clipRect.Width <= 0f || clipRect.Height <= 0f)
                {
                    continue;
                }

                if (TryGetTransformFromVisualToRoot(current, out var transformToRoot))
                {
                    clipRect = TransformRect(clipRect, transformToRoot);
                }

                clipped = IntersectRect(clipped, clipRect);
                intersectedAnyClip = true;
                if (clipped.Width <= 0f || clipped.Height <= 0f)
                {
                    return false;
                }
            }

            if (intersectedAnyClip)
            {
                bounds = clipped;
            }

            return bounds.Width > 0f && bounds.Height > 0f;
        }

        private static bool TryGetTransformScrollDirtyBoundsHint(UIElement visual, out LayoutRect bounds)
        {
            bounds = default;

            if (visual is ScrollViewer viewer &&
                viewer.Content is UIElement viewerContent)
            {
                if (viewer.NeedsRender &&
                    !viewer.NeedsMeasure &&
                    !viewer.NeedsArrange &&
                    viewer.TryGetContentViewportClipRect(out bounds))
                {
                    return bounds.Width > 0f && bounds.Height > 0f;
                }

                visual = viewerContent;
            }

            if (IsTransformScrollContent(visual) &&
                TryGetDirectTransformScrollOwner(visual, out var transformOwner) &&
                transformOwner.TryGetContentViewportClipRect(out bounds))
            {
                return bounds.Width > 0f && bounds.Height > 0f;
            }

            return false;
        }

        private static bool TryGetAncestorTransformScrollDirtyBoundsHint(UIElement visual, out LayoutRect bounds)
        {
            bounds = default;
            for (var current = visual.VisualParent; current != null; current = current.VisualParent)
            {
                if (!IsTransformScrollContent(current))
                {
                    continue;
                }

                if (TryGetDirectTransformScrollOwner(current, out var transformOwner) &&
                    transformOwner.TryGetContentViewportClipRect(out bounds))
                {
                    return bounds.Width > 0f && bounds.Height > 0f;
                }

                return false;
            }

            return false;
        }

        private static bool IsTransformScrollContent(UIElement visual)
        {
            return visual is IScrollTransformContent or VirtualizingStackPanel;
        }

        private static bool TryGetTransformFromVisualToRoot(UIElement element, out Matrix transform)
        {
            transform = Matrix.Identity;
            var hasTransform = false;
            for (var current = element; current != null; current = current.VisualParent)
            {
                if (!current.TryGetLocalRenderTransformSnapshot(out var localTransform))
                {
                    continue;
                }

                transform *= localTransform;
                hasTransform = true;
            }

            return hasTransform;
        }

        private static bool TryGetDirectTransformScrollOwner(UIElement element, out ScrollViewer owner)
        {
            owner = null!;

            var visualOwner = element.VisualParent as ScrollViewer;
            if (visualOwner != null && ReferenceEquals(visualOwner.Content, element))
            {
                owner = visualOwner;
                return true;
            }

            var logicalOwner = element.LogicalParent as ScrollViewer;
            if (logicalOwner != null && ReferenceEquals(logicalOwner.Content, element))
            {
                owner = logicalOwner;
                return true;
            }

            return false;
        }

        internal UiDirtyDrawDecisionSnapshot ResolveDirtyDrawDecisionAfterSync(RetainedDrawThresholds thresholds)
        {
            var beforeSyncReason = GetCurrentDirtyDrawDecisionReason(thresholds);
            if (_root.UseRetainedRenderList)
            {
                SyncIfNeeded();
            }

            var afterSyncReason = GetCurrentDirtyDrawDecisionReason(thresholds);
            if (beforeSyncReason != afterSyncReason)
            {
                _root._retainedSyncChangedDirtyDecisionCount++;
            }

            if (afterSyncReason == UiDirtyDrawDecisionReason.ThresholdFallback)
            {
                _root._dirtyRegionThresholdFallbackCount++;
            }

            _root._lastDirtyDrawDecisionReason = afterSyncReason;
            return new UiDirtyDrawDecisionSnapshot(
                beforeSyncReason,
                afterSyncReason,
                afterSyncReason == UiDirtyDrawDecisionReason.Partial,
                afterSyncReason != UiDirtyDrawDecisionReason.Partial);
        }

        internal bool WouldUsePartialDirtyRedraw(RetainedDrawThresholds thresholds)
        {
            return GetCurrentDirtyDrawDecisionReason(thresholds) == UiDirtyDrawDecisionReason.Partial;
        }

        private UiDirtyDrawDecisionReason GetCurrentDirtyDrawDecisionReason(RetainedDrawThresholds thresholds)
        {
            if (!_root.UseRetainedRenderList)
            {
                return UiDirtyDrawDecisionReason.RetainedDisabled;
            }

            if (!_root.UseDirtyRegionRendering)
            {
                return UiDirtyDrawDecisionReason.DirtyRegionRenderingDisabled;
            }

            if (_root._diagnosticCaptureFullClearPending)
            {
                return UiDirtyDrawDecisionReason.DiagnosticCapture;
            }

            if (_root._fullRedrawSettleFramesRemaining > 0)
            {
                return UiDirtyDrawDecisionReason.FullRedrawSettle;
            }

            if (DirtyRegions.IsFullFrameDirty)
            {
                return UiDirtyDrawDecisionReason.FullDirty;
            }

            if (DirtyRegions.RegionCount == 0)
            {
                return UiDirtyDrawDecisionReason.NoRegions;
            }

            return ShouldUsePartialDirtyRedraw(thresholds)
                ? UiDirtyDrawDecisionReason.Partial
                : UiDirtyDrawDecisionReason.ThresholdFallback;
        }

        private bool ShouldUsePartialDirtyRedraw(RetainedDrawThresholds thresholds)
        {
            if (DirtyRegions.RegionCount <= 0 ||
                DirtyRegions.RegionCount > thresholds.RegionCountFallbackThreshold)
            {
                return false;
            }

            if (_hasViewportScopedScrollDirtyRegion)
            {
                return true;
            }

            var coverageThreshold = DirtyRegions.RegionCount == 1
                ? thresholds.SingleRegionCoverageFallbackThreshold
                : thresholds.MultipleRegionCoverageFallbackThreshold;
            return DirtyRegions.GetDirtyAreaCoverage() <= coverageThreshold;
        }

        private static bool IsViewportScopedScrollDirtyRegionReason(string reason)
        {
            return string.Equals(reason, "scroll-viewport", StringComparison.Ordinal) ||
                   string.Equals(reason, "scroll-clip-hint", StringComparison.Ordinal) ||
                   string.Equals(reason, "ancestor-scroll-clip-hint", StringComparison.Ordinal) ||
                   string.Equals(reason, "scroll-clip-hint-delta", StringComparison.Ordinal);
        }

        internal static bool AreRectsEqual(LayoutRect left, LayoutRect right)
        {
            const float epsilon = 0.0001f;
            return MathF.Abs(left.X - right.X) <= epsilon &&
                   MathF.Abs(left.Y - right.Y) <= epsilon &&
                   MathF.Abs(left.Width - right.Width) <= epsilon &&
                   MathF.Abs(left.Height - right.Height) <= epsilon;
        }

        internal static LayoutRect Union(LayoutRect left, LayoutRect right)
        {
            var x = MathF.Min(left.X, right.X);
            var y = MathF.Min(left.Y, right.Y);
            var rightEdge = MathF.Max(left.X + left.Width, right.X + right.Width);
            var bottomEdge = MathF.Max(left.Y + left.Height, right.Y + right.Height);
            return new LayoutRect(x, y, MathF.Max(0f, rightEdge - x), MathF.Max(0f, bottomEdge - y));
        }

        private static bool Intersects(LayoutRect left, LayoutRect right)
        {
            return left.X < right.X + right.Width &&
                   left.X + left.Width > right.X &&
                   left.Y < right.Y + right.Height &&
                   left.Y + left.Height > right.Y;
        }

        private static bool IntersectsOrTouches(LayoutRect left, LayoutRect right)
        {
            return left.X <= right.X + right.Width &&
                   left.X + left.Width >= right.X &&
                   left.Y <= right.Y + right.Height &&
                   left.Y + left.Height >= right.Y;
        }

        private static LayoutRect IntersectRect(LayoutRect left, LayoutRect right)
        {
            var x = MathF.Max(left.X, right.X);
            var y = MathF.Max(left.Y, right.Y);
            var rightEdge = MathF.Min(left.X + left.Width, right.X + right.Width);
            var bottomEdge = MathF.Min(left.Y + left.Height, right.Y + right.Height);
            return new LayoutRect(x, y, MathF.Max(0f, rightEdge - x), MathF.Max(0f, bottomEdge - y));
        }

        private static LayoutRect TransformRect(LayoutRect rect, Matrix transform)
        {
            if (transform == Matrix.Identity)
            {
                return rect;
            }

            var topLeft = Vector2.Transform(new Vector2(rect.X, rect.Y), transform);
            var topRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y), transform);
            var bottomLeft = Vector2.Transform(new Vector2(rect.X, rect.Y + rect.Height), transform);
            var bottomRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y + rect.Height), transform);
            var minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
            var minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
            var maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
            var maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));
            return new LayoutRect(minX, minY, MathF.Max(0f, maxX - minX), MathF.Max(0f, maxY - minY));
        }

        private static LayoutRect ToLayoutRect(Rectangle rectangle)
        {
            return new LayoutRect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }

        internal void ClearAfterDraw()
        {
            ClearDirtyRenderQueue();
            ResetRetainedSyncTrackingState();
            DirtyRegions.Clear();
            _hasViewportScopedScrollDirtyRegion = false;
        }

        internal void ApplyRenderInvalidationCleanupAfterDraw()
        {
            if (!_root.UseRetainedRenderList || IsFullFrameDirty || _root._lastRetainedSyncUsedFullRebuild)
            {
                _root._visualRoot.ClearRenderInvalidationRecursive();
                return;
            }

            if (LastSynchronizedDirtyRenderRoots.Count == 0)
            {
                if (DirtyRenderSet.Count == 0)
                {
                    _root._visualRoot.ClearRenderInvalidationRecursive();
                }

                return;
            }

            for (var i = 0; i < LastSynchronizedDirtyRenderRoots.Count; i++)
            {
                LastSynchronizedDirtyRenderRoots[i].ClearRenderInvalidationRecursive();
            }

            BuildPendingDirtyAncestorCounts();
            for (var i = 0; i < LastSynchronizedDirtyRenderRoots.Count; i++)
            {
                ClearRenderInvalidationAncestorChain(LastSynchronizedDirtyRenderRoots[i].GetInvalidationParent());
            }

            _pendingDirtyAncestorCounts.Clear();
        }

        private void BuildPendingDirtyAncestorCounts()
        {
            _pendingDirtyAncestorCounts.Clear();

            for (var i = 0; i < LastSynchronizedDirtyRenderRoots.Count; i++)
            {
                AddPendingDirtyAncestorChain(LastSynchronizedDirtyRenderRoots[i]);
            }

            if (DirtyRenderSet.Count == 0)
            {
                return;
            }

            foreach (var dirtyVisual in DirtyRenderSet)
            {
                AddPendingDirtyAncestorChain(dirtyVisual);
            }
        }

        private void AddPendingDirtyAncestorChain(UIElement dirtyVisual)
        {
            for (var current = dirtyVisual.GetInvalidationParent(); current != null; current = current.GetInvalidationParent())
            {
                _pendingDirtyAncestorCounts.TryGetValue(current, out var count);
                _pendingDirtyAncestorCounts[current] = count + 1;
            }
        }

        private void ClearRenderInvalidationAncestorChain(UIElement? visual)
        {
            for (var current = visual; current != null; current = current.GetInvalidationParent())
            {
                if (!_pendingDirtyAncestorCounts.TryGetValue(current, out var remainingCount))
                {
                    current.ClearRenderInvalidationShallow();
                    continue;
                }

                remainingCount--;
                if (remainingCount > 0)
                {
                    _pendingDirtyAncestorCounts[current] = remainingCount;
                    continue;
                }

                _pendingDirtyAncestorCounts.Remove(current);
                current.ClearRenderInvalidationShallow();
            }
        }

        internal string ValidateAgainstCurrentVisualState(int maxMismatches)
        {
            return ValidateRetainedTreeAgainstCurrentVisualState(maxMismatches);
        }

        internal RetainedRenderControllerTelemetrySnapshot GetTelemetrySnapshot()
        {
            return new RetainedRenderControllerTelemetrySnapshot(
                NodeCount,
                HighCostVisualCount,
                DirtyRegionCount,
                DirtyCoverage,
                IsFullFrameDirty,
                FullRedrawFallbackCount,
                _root._retainedFullRebuildCount,
                _root._retainedSubtreeSyncCount,
                _root._lastRetainedDirtyVisualCount,
                _root._lastDirtyRootCountAfterCoalescing,
                _scrollViewportDirtyCount,
                _structureInvalidationCount,
                _root._lastRetainedTraversalCount,
                _root._lastDirtyRegionTraversalCount,
                _root._lastRetainedNodesVisited,
                _root._lastRetainedNodesDrawn,
                _root._dirtyRegionThresholdFallbackCount,
                _root._lastDirtyDrawDecisionReason,
                _dirtyRegionAddCount,
                _dirtyRegionFragmentationFullDirtyCount,
                _dirtyRegionBoundsDeltaSuppressedByTransformScrollCount,
                _lastDirtyRegionAddReason,
                _lastFullDirtySource,
                GetRenderInvalidationKindCount(RenderInvalidationKind.Content),
                GetRenderInvalidationKindCount(RenderInvalidationKind.Transform),
                GetRenderInvalidationKindCount(RenderInvalidationKind.Clip),
                GetRenderInvalidationKindCount(RenderInvalidationKind.Opacity),
                GetRenderInvalidationKindCount(RenderInvalidationKind.Visibility),
                GetRenderInvalidationKindCount(RenderInvalidationKind.Effect),
                GetRenderInvalidationKindCount(RenderInvalidationKind.Structure),
                GetRenderInvalidationKindCount(RenderInvalidationKind.Bounds),
                GetRenderInvalidationKindCount(RenderInvalidationKind.Overlay),
                GetRenderInvalidationKindCount(RenderInvalidationKind.DeviceResource),
                _lastRenderInvalidationKind,
                _lastRenderInvalidationSource,
                CompositionMetadataUpdateCount,
                GetCompositionMetadataUpdateKindCount(RenderInvalidationKind.Transform),
                GetCompositionMetadataUpdateKindCount(RenderInvalidationKind.Clip),
                GetCompositionMetadataUpdateKindCount(RenderInvalidationKind.Opacity),
                GetCompositionMetadataUpdateKindCount(RenderInvalidationKind.Visibility),
                CompositionMetadataUpdateMissCount,
                _lastCompositionMetadataUpdateKind,
                _lastCompositionMetadataUpdateSource,
                CompositionNodeCount,
                NodeCount - CompositionNodeCount,
                CompositionRebuildCount,
                LastCompositionSyncBuildMilliseconds,
                VisualRecordCount,
                VisualRecordRebuildCount,
                VisualRecordReuseCount,
                VisualRecords.LastRecordedCommandCount,
                VisualRecords.LastRecordedVisualType,
                VisualRecords.LastRecordedVisualName,
                _commandReplayCount,
                _commandReplayFallbackCount,
                _unsupportedCommandFallbackCount,
                _lastCompositionRecordPassCount,
                _lastCompositionMetadataPassCount,
                _lastCompositionFullPassCount,
                _lastCompositionPrimaryMode,
                _lastCompositionPrimaryReason,
                _compositorOnlyFrameCount,
                _fullCompositionFrameCount,
                _compositionSubtreeCullCount,
                _compositionSelfCullCount,
                _compositionTransformPushCount,
                _compositionOpacityPushCount,
                _cacheModeBoundaryCount,
                _bitmapCacheBoundaryCount,
                _deferredBitmapCacheBoundaryCount,
                _compositionCullingMilliseconds,
                _compositionCommandReplayMilliseconds);
        }

        internal void ResetTelemetry()
        {
            _scrollViewportDirtyCount = 0;
            _structureInvalidationCount = 0;
            _dirtyRegionAddCount = 0;
            _dirtyRegionFragmentationFullDirtyCount = 0;
            _dirtyRegionBoundsDeltaSuppressedByTransformScrollCount = 0;
            Array.Clear(_renderInvalidationKindCounts);
            _lastDirtyRegionAddReason = "none";
            _lastFullDirtySource = "none";
            _lastRenderInvalidationKind = "none";
            _lastRenderInvalidationSource = "none";
            Array.Clear(_compositionMetadataUpdateKindCounts);
            _lastCompositionMetadataUpdateKind = "none";
            _lastCompositionMetadataUpdateSource = "none";
            CompositionMetadataUpdateCount = 0;
            CompositionMetadataUpdateMissCount = 0;
            VisualRecords.ResetTelemetry();
            _lastCompositionRecordPassCount = 0;
            _lastCompositionMetadataPassCount = 0;
            _lastCompositionFullPassCount = 0;
            _lastCompositionPrimaryMode = "none";
            _lastCompositionPrimaryReason = "none";
            _lastCompositionFrameVisualRecordRebuildTotal = 0;
            _lastCompositionFrameMetadataUpdateTotal = 0;
            _lastCompositionFrameVisualRecordTouchedTotal = 0;
            _visualRecordTouchedCount = 0;
            _fullCompositionFrameCount = 0;
            _compositorOnlyFrameCount = 0;
            _lastCompositionMetadataOnlySyncSucceeded = false;
            _lastCompositionMetadataOnlySyncWasTransformStableLayer = false;
            _lastSyncUsedFullVisualRecordRefresh = false;
            _commandReplayCount = 0;
            _commandReplayFallbackCount = 0;
            _unsupportedCommandFallbackCount = 0;
            _compositionSubtreeCullCount = 0;
            _compositionSelfCullCount = 0;
            _compositionTransformPushCount = 0;
            _compositionOpacityPushCount = 0;
            _cacheModeBoundaryCount = 0;
            _bitmapCacheBoundaryCount = 0;
            _deferredBitmapCacheBoundaryCount = 0;
            _compositionCullingMilliseconds = 0d;
            _compositionCommandReplayMilliseconds = 0d;
        }

        private int GetRenderInvalidationKindCount(RenderInvalidationKind kind)
        {
            var kindIndex = (int)kind;
            return (uint)kindIndex < (uint)_renderInvalidationKindCounts.Length
                ? _renderInvalidationKindCounts[kindIndex]
                : 0;
        }

        private int GetCompositionMetadataUpdateKindCount(RenderInvalidationKind kind)
        {
            var kindIndex = (int)kind;
            return (uint)kindIndex < (uint)_compositionMetadataUpdateKindCounts.Length
                ? _compositionMetadataUpdateKindCounts[kindIndex]
                : 0;
        }

        private void QueueCompositionMetadataUpdate(
            RetainedInvalidation invalidation,
            RenderInvalidationKind renderInvalidationKind)
        {
            if (!IsCompositionMetadataInvalidationKind(renderInvalidationKind))
            {
                return;
            }

            var visual = invalidation.RequestedSource ??
                         invalidation.EffectiveSource ??
                         invalidation.RetainedSyncRoot;
            if (visual == null)
            {
                CompositionMetadataUpdateMissCount++;
                _lastCompositionMetadataUpdateKind = renderInvalidationKind.ToString();
                _lastCompositionMetadataUpdateSource = "none";
                return;
            }

            PendingCompositionMetadataUpdates[visual] = renderInvalidationKind;
        }

        private bool ApplyPendingCompositionMetadataUpdates()
        {
            if (PendingCompositionMetadataUpdates.Count == 0)
            {
                _lastCompositionMetadataOnlySyncWasTransformStableLayer = false;
                return true;
            }

            var allApplied = true;
            var allUpdatesAreTransformStableLayers = true;
            foreach (var update in PendingCompositionMetadataUpdates)
            {
                allUpdatesAreTransformStableLayers &=
                    update.Value is RenderInvalidationKind.Transform or RenderInvalidationKind.Clip &&
                    RetainedCompositionLayerBoundary.IsTransformStableLayer(update.Key);

                var retainedMetadataApplied = TryRefreshRetainedRenderMetadata(update.Key);
                if (!CompositionTreeIndex.TryUpdateMetadata(update.Key, update.Value))
                {
                    allApplied = false;
                    CompositionMetadataUpdateMissCount++;
                    _lastCompositionMetadataUpdateKind = update.Value.ToString();
                    _lastCompositionMetadataUpdateSource = DescribeElementForDiagnostics(update.Key);
                    continue;
                }

                allApplied &= retainedMetadataApplied;
                CompositionMetadataUpdateCount++;
                var kindIndex = (int)update.Value;
                if ((uint)kindIndex < (uint)_compositionMetadataUpdateKindCounts.Length)
                {
                    _compositionMetadataUpdateKindCounts[kindIndex]++;
                }

                _lastCompositionMetadataUpdateKind = update.Value.ToString();
                _lastCompositionMetadataUpdateSource = DescribeElementForDiagnostics(update.Key);
            }

            PendingCompositionMetadataUpdates.Clear();
            _lastCompositionMetadataOnlySyncWasTransformStableLayer = allUpdatesAreTransformStableLayers;
            return allApplied;
        }

        private bool HasPendingCompositionMetadataUpdate(UIElement visual)
        {
            return PendingCompositionMetadataUpdates.ContainsKey(visual);
        }

        private static bool IsCompositionMetadataInvalidationKind(RenderInvalidationKind kind)
        {
            return kind == RenderInvalidationKind.Transform ||
                   kind == RenderInvalidationKind.Clip ||
                   kind == RenderInvalidationKind.Opacity ||
                   kind == RenderInvalidationKind.Visibility;
        }

        private static bool ShouldEnqueueDirtyRenderNode(
            RenderInvalidationKind renderInvalidationKind,
            RetainedInvalidationKind retainedInvalidationKind)
        {
            if (retainedInvalidationKind == RetainedInvalidationKind.Structure)
            {
                return true;
            }

            return renderInvalidationKind == RenderInvalidationKind.Content ||
                   renderInvalidationKind == RenderInvalidationKind.Bounds ||
                   renderInvalidationKind == RenderInvalidationKind.Effect ||
                   renderInvalidationKind == RenderInvalidationKind.Overlay ||
                   renderInvalidationKind == RenderInvalidationKind.DeviceResource;
        }

        private readonly record struct RenderTraversalMetrics(
            int NodesVisited,
            int NodesDrawn,
            int ClipPushCount);
    }
}
