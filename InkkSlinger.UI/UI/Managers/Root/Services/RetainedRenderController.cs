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
        internal readonly List<RenderNode> RetainedRenderList = new();
        internal readonly Dictionary<UIElement, int> RenderNodeIndices = new();
        internal readonly Queue<UIElement> DirtyRenderQueue = new();
        internal readonly HashSet<UIElement> DirtyRenderSet = new();
        internal readonly HashSet<UIElement> DirtyRenderRootsRequireDeepSync = new();
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
        private readonly Dictionary<UIElement, int> _pendingDirtyAncestorCounts = new(ReferenceEqualityComparer.Instance);

        public RetainedRenderController(UiRoot root)
        {
            _root = root;
        }

        internal int NodeCount => RetainedRenderList.Count;

        internal int DirtyQueueCount => DirtyRenderQueue.Count;

        internal int FullRedrawFallbackCount => DirtyRegions.FullRedrawFallbackCount;

        internal bool HasDirtyWork => DirtyRegions.IsFullFrameDirty || DirtyRegions.RegionCount > 0;

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
            DirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
        }

        internal void MarkFullFrameDirtyWithoutReason()
        {
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
            if (invalidation.Kind == RetainedInvalidationKind.Structure)
            {
                _structureInvalidationCount++;
            }

            if (_root.UseDirtyRegionRendering)
            {
                TrackDirtyBoundsForVisual(invalidation.DirtyBoundsSource);
            }

            if (invalidation.RetainedSyncRoot != null)
            {
                EnqueueDirtyRenderNode(invalidation.RetainedSyncRoot, invalidation.RequireDeepSync);
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
            return element is IScrollTransformContent or VirtualizingStackPanel;
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

            if (viewer.Content is UIElement content)
            {
                EnqueueDirtyRenderNode(content, requireDeepSync: true);
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
                DirtyRenderSet.Count > 0)
            {
                _root.EnsureVisualIndexCurrent();
                SynchronizeRetainedRenderList();
            }
        }

        internal void Draw(SpriteBatch spriteBatch, bool useDirtyRegions, RetainedDrawThresholds thresholds)
        {
            _ = thresholds;
            if (useDirtyRegions)
            {
                DrawRetainedRenderListWithDirtyRegions(spriteBatch);
            }
            else
            {
                _root.LastDirtyRectCount = 1;
                _root.LastDirtyAreaPercentage = 1d;
                _root.RecordFullRetainedDrawWithoutFullClearIfNeeded();
                DrawRetainedRenderList(spriteBatch);
            }
        }

        internal void AppendDrawOrderForClip(LayoutRect clipRect, List<UIElement> visuals)
        {
            _ = TraverseNodesWithinClip(spriteBatch: null, clipRect, visuals);
        }

        internal (int NodesVisited, int NodesDrawn, int LocalClipPushCount) GetTraversalMetricsForClip(LayoutRect clipRect)
        {
            var metrics = TraverseNodesWithinClip(spriteBatch: null, clipRect);
            return (metrics.NodesVisited, metrics.NodesDrawn, metrics.ClipPushCount);
        }

        private void DrawRetainedRenderListWithDirtyRegions(SpriteBatch spriteBatch)
        {
            var dirtyCoverage = DirtyRegions.GetDirtyAreaCoverage();
            _root.LastDirtyRectCount = DirtyRegions.IsFullFrameDirty ? 1 : DirtyRegions.RegionCount;
            _root.LastDirtyAreaPercentage = dirtyCoverage;

            if (DirtyRegions.IsFullFrameDirty || DirtyRegions.RegionCount == 0)
            {
                _root.RecordFullRetainedDrawWithoutFullClearIfNeeded();
                DrawRetainedRenderList(spriteBatch);
                return;
            }

            DrawRetainedRenderListForDirtyRegions(spriteBatch, DirtyRegions.Regions);
            _root.LastDrawUsedPartialRedraw = true;
        }

        private void DrawRetainedRenderList(SpriteBatch spriteBatch)
        {
            var metrics = TraverseNodesWithinClip(
                spriteBatch,
                ToLayoutRect(spriteBatch.GraphicsDevice.ScissorRectangle));
            _root._lastRetainedTraversalCount++;
            _root._lastRetainedNodesVisited += metrics.NodesVisited;
            _root._lastRetainedNodesDrawn += metrics.NodesDrawn;
        }

        private void DrawRetainedRenderListForDirtyRegions(SpriteBatch spriteBatch, IReadOnlyList<LayoutRect> regions)
        {
            for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
            {
                var dirtyRegion = regions[regionIndex];
                UiDrawing.PushAbsoluteClip(spriteBatch, dirtyRegion);
                try
                {
                    UiDrawing.DrawFilledRect(spriteBatch, dirtyRegion, _root._clearColor);

                    var metrics = TraverseNodesWithinClip(spriteBatch, dirtyRegion);
                    _root._lastRetainedTraversalCount++;
                    _root._lastRetainedNodesVisited += metrics.NodesVisited;
                    _root._lastRetainedNodesDrawn += metrics.NodesDrawn;
                    _root._lastDirtyRegionTraversalCount++;
                }
                finally
                {
                    UiDrawing.PopClip(spriteBatch);
                }
            }
        }

        private RenderTraversalMetrics TraverseNodesWithinClip(SpriteBatch? spriteBatch, LayoutRect clipRect, List<UIElement>? visuals = null)
        {
            var visited = 0;
            var drawn = 0;
            var clipPushCount = 0;
            ActiveRetainedDrawPath.Clear();

            try
            {
                for (var nodeIndex = 0; nodeIndex < RetainedRenderList.Count; nodeIndex++)
                {
                    visited++;
                    var node = RetainedRenderList[nodeIndex];
                    if (!node.IsEffectivelyVisible)
                    {
                        nodeIndex = Math.Max(nodeIndex, node.SubtreeEndIndexExclusive - 1);
                        continue;
                    }

                    if (node.HasSubtreeBoundsSnapshot &&
                        !Intersects(node.SubtreeBoundsSnapshot, clipRect))
                    {
                        nodeIndex = Math.Max(nodeIndex, node.SubtreeEndIndexExclusive - 1);
                        continue;
                    }

                    var shouldDrawSelf = ShouldDrawRetainedNodeSelf(node, clipRect);
                    if (spriteBatch != null)
                    {
                        SyncRetainedDrawState(spriteBatch, node, ref clipPushCount);
                        if (shouldDrawSelf)
                        {
                            node.Visual.DrawSelf(spriteBatch);
                        }
                    }
                    else if (node.HasLocalClip)
                    {
                        clipPushCount++;
                    }

                    if (shouldDrawSelf)
                    {
                        visuals?.Add(node.Visual);
                        drawn++;
                    }
                }
            }
            finally
            {
                if (spriteBatch != null)
                {
                    ResetRetainedDrawState(spriteBatch);
                }
            }

            return new RenderTraversalMetrics(visited, drawn, clipPushCount);
        }

        private static bool ShouldDrawRetainedNodeSelf(RenderNode node, LayoutRect clipRect)
        {
            return !node.HasBoundsSnapshot || Intersects(node.BoundsSnapshot, clipRect);
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
                _root._dirtyBoundsEventTrace.Add($"{_root._lastDirtyBoundsVisualType}#{_root._lastDirtyBoundsVisualName}:scroll-clip-hint:{transformScrollBounds.X:0.##},{transformScrollBounds.Y:0.##},{transformScrollBounds.Width:0.##},{transformScrollBounds.Height:0.##}");
                AddDirtyRegionForDiagnostics(transformScrollBounds, "scroll-clip-hint");
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

        private void AddDirtyRegionForDiagnostics(LayoutRect bounds, string reason)
        {
            _root._dirtyBoundsEventTrace.Add($"dirty-add:{reason}:{bounds.X:0.##},{bounds.Y:0.##},{bounds.Width:0.##},{bounds.Height:0.##}");
            DirtyRegions.AddDirtyRegion(bounds);
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

            var coverageThreshold = DirtyRegions.RegionCount == 1
                ? thresholds.SingleRegionCoverageFallbackThreshold
                : thresholds.MultipleRegionCoverageFallbackThreshold;
            return DirtyRegions.GetDirtyAreaCoverage() <= coverageThreshold;
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
                _root._lastDirtyDrawDecisionReason);
        }

        internal void ResetTelemetry()
        {
            _scrollViewportDirtyCount = 0;
            _structureInvalidationCount = 0;
        }

        private readonly record struct RenderTraversalMetrics(
            int NodesVisited,
            int NodesDrawn,
            int ClipPushCount);
    }
}
