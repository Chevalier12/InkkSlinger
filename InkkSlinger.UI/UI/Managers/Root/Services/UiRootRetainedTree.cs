using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
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
            _renderListNeedsFullRebuild = true;
            ArmFullRedrawSettleWindowForResize();
            MarkFullFrameDirty(UiFullDirtyReason.ViewportChanged);
        }
    }

    private bool IsPartOfVisualTree(UIElement? element)
    {
        EnsureVisualIndexCurrent();
        return TryGetIndexedVisualNodeCore(element, out _);
    }

    private bool TryGetIndexedVisualNodeCore(UIElement? element, out UiIndexedVisualNode node)
    {
        if (element == null || !ReferenceEquals(element.GetVisualRoot(), _visualRoot))
        {
            node = default;
            return false;
        }

        return _visualIndex.TryGetNode(element, out node);
    }

    private void EnqueueDirtyRenderNode(UIElement visual, bool requireDeepSync = false)
    {
        if (!ReferenceEquals(visual, _visualRoot) &&
            !ReferenceEquals(visual.GetVisualRoot(), _visualRoot))
        {
            return;
        }

        if (!_dirtyRenderSet.Add(visual))
        {
            if (requireDeepSync)
            {
                _dirtyRenderRootsRequireDeepSync.Add(visual);
            }

            return;
        }

        if (requireDeepSync)
        {
            _dirtyRenderRootsRequireDeepSync.Add(visual);
        }

        _dirtyRenderQueue.Enqueue(visual);
    }

    private void CompactDirtyRenderQueueForSync()
    {
        var compactionStart = Stopwatch.GetTimestamp();
        var deepSyncRoots = _dirtyRenderRootsRequireDeepSync.Count == 0
            ? null
            : new HashSet<UIElement>(_dirtyRenderRootsRequireDeepSync);
        _dirtyRenderRootsRequireDeepSync.Clear();
        _dirtyRenderWorkItems.Clear();
        _dirtyRenderCandidates.Clear();
        _dirtyRenderCompactionBuffer.Clear();
        _lastCoalescedDirtyRenderRoots.Clear();
        while (_dirtyRenderQueue.TryDequeue(out var visual))
        {
            _dirtyRenderCompactionBuffer.Add(visual);
        }

        _dirtyRenderSet.Clear();
        if (_dirtyRenderCompactionBuffer.Count == 0)
        {
            _lastDirtyRootCountAfterCoalescing = 0;
            _lastRetainedQueueCompactionMs += Stopwatch.GetElapsedTime(compactionStart).TotalMilliseconds;
            return;
        }

        EnsureVisualIndexCurrent();
        var requiresFullRebuild = _renderListNeedsFullRebuild || _retainedRenderList.Count == 0;
        if (_dirtyRenderCompactionBuffer.Count == 1 &&
            TryCompactSingleDirtyRenderCandidate(
                _dirtyRenderCompactionBuffer[0],
                requiresFullRebuild,
                deepSyncRoots?.Contains(_dirtyRenderCompactionBuffer[0]) == true))
        {
            _lastDirtyRootCountAfterCoalescing = _dirtyRenderWorkItems.Count;
            for (var i = 0; i < _dirtyRenderWorkItems.Count; i++)
            {
                _lastCoalescedDirtyRenderRoots.Add(_dirtyRenderWorkItems[i].Visual);
            }

            _lastRetainedQueueCompactionMs += Stopwatch.GetElapsedTime(compactionStart).TotalMilliseconds;
            return;
        }

        for (var i = 0; i < _dirtyRenderCompactionBuffer.Count; i++)
        {
            var candidate = _dirtyRenderCompactionBuffer[i];
            if (!TryGetIndexedVisualNodeCore(candidate, out _))
            {
                continue;
            }

            if (requiresFullRebuild)
            {
                _dirtyRenderWorkItems.Add(new DirtyRenderWorkItem(candidate, -1, -1, true));
                continue;
            }

            if (!_renderNodeIndices.TryGetValue(candidate, out var renderNodeIndex))
            {
                _renderListNeedsFullRebuild = true;
                requiresFullRebuild = true;
                _dirtyRenderWorkItems.Add(new DirtyRenderWorkItem(candidate, -1, -1, true));
                continue;
            }

            var node = _retainedRenderList[renderNodeIndex];
            _dirtyRenderCandidates.Add(new IndexedDirtyRenderCandidate(
                candidate,
                renderNodeIndex,
                node.SubtreeEndIndexExclusive,
                deepSyncRoots?.Contains(candidate) == true));
        }

        var coalesceStart = Stopwatch.GetTimestamp();
        CoalesceDirtyRenderCandidates();
        _lastRetainedCandidateCoalescingMs += Stopwatch.GetElapsedTime(coalesceStart).TotalMilliseconds;

        _lastDirtyRootCountAfterCoalescing = _dirtyRenderWorkItems.Count;
        for (var i = 0; i < _dirtyRenderWorkItems.Count; i++)
        {
            _lastCoalescedDirtyRenderRoots.Add(_dirtyRenderWorkItems[i].Visual);
        }

        _lastRetainedQueueCompactionMs += Stopwatch.GetElapsedTime(compactionStart).TotalMilliseconds;
    }

    private bool TryCompactSingleDirtyRenderCandidate(UIElement candidate, bool requiresFullRebuild, bool requiresDeepSync)
    {
        if (!TryGetIndexedVisualNodeCore(candidate, out _))
        {
            return true;
        }

        if (requiresFullRebuild)
        {
            _dirtyRenderWorkItems.Add(new DirtyRenderWorkItem(candidate, -1, -1, true));
            return true;
        }

        if (!_renderNodeIndices.TryGetValue(candidate, out var renderNodeIndex))
        {
            _renderListNeedsFullRebuild = true;
            _dirtyRenderWorkItems.Add(new DirtyRenderWorkItem(candidate, -1, -1, true));
            return true;
        }

        var node = _retainedRenderList[renderNodeIndex];
        _dirtyRenderWorkItems.Add(new DirtyRenderWorkItem(
            candidate,
            renderNodeIndex,
            node.SubtreeEndIndexExclusive,
            requiresDeepSync));
        return true;
    }

    private void CoalesceDirtyRenderCandidates()
    {
        if (_dirtyRenderCandidates.Count == 0)
        {
            return;
        }

        _dirtyRenderCandidates.Sort(static (left, right) => left.RenderNodeIndex.CompareTo(right.RenderNodeIndex));

        for (var i = 0; i < _dirtyRenderCandidates.Count; i++)
        {
            AddSortedDirtyRenderCandidate(_dirtyRenderCandidates[i]);
        }

        _dirtyRenderCandidates.Clear();
    }

    private void AddSortedDirtyRenderCandidate(IndexedDirtyRenderCandidate candidate)
    {
        if (_dirtyRenderWorkItems.Count == 0)
        {
            _dirtyRenderWorkItems.Add(new DirtyRenderWorkItem(
                candidate.Visual,
                candidate.RenderNodeIndex,
                candidate.SubtreeEndIndexExclusive,
                candidate.RequiresDeepSync));
            return;
        }

        var lastIndex = _dirtyRenderWorkItems.Count - 1;
        var last = _dirtyRenderWorkItems[lastIndex];

        // Retained subtree intervals are either disjoint or nested, so sorted starts only need
        // to compare against the current tail item to preserve the original coalescing semantics.
        if (last.RenderNodeIndex >= 0 &&
            candidate.RenderNodeIndex < last.SubtreeEndIndexExclusive)
        {
            if (!last.RequiresDeepSync &&
                !candidate.RequiresDeepSync &&
                ShouldKeepTrackOverlapAsSeparateWorkItems(last.Visual, candidate.Visual))
            {
                _dirtyRenderWorkItems.Add(new DirtyRenderWorkItem(
                    candidate.Visual,
                    candidate.RenderNodeIndex,
                    candidate.SubtreeEndIndexExclusive,
                    false));
                return;
            }

            _lastRetainedOverlapForcedDeepCount++;
            _dirtyRenderWorkItems[lastIndex] = last with { RequiresDeepSync = true };
            return;
        }

        _dirtyRenderWorkItems.Add(new DirtyRenderWorkItem(
            candidate.Visual,
            candidate.RenderNodeIndex,
            candidate.SubtreeEndIndexExclusive,
            candidate.RequiresDeepSync));
    }

    private static bool ShouldKeepTrackOverlapAsSeparateWorkItems(UIElement first, UIElement second)
    {
        return HasTrackBetween(first, second) || HasTrackBetween(second, first);
    }

    private static bool HasTrackBetween(UIElement descendant, UIElement ancestor)
    {
        for (var current = descendant; current != null; current = current.VisualParent)
        {
            if (current is Track)
            {
                return true;
            }

            if (ReferenceEquals(current, ancestor))
            {
                break;
            }
        }

        return false;
    }

    private void ClearDirtyRenderQueue()
    {
        _dirtyRenderQueue.Clear();
        _dirtyRenderSet.Clear();
        _dirtyRenderRootsRequireDeepSync.Clear();
        _dirtyRenderWorkItems.Clear();
        _dirtyRenderCandidates.Clear();
        _pendingAncestorMetadataRefreshRoots.Clear();
        _lastCoalescedDirtyRenderRoots.Clear();
    }

    private void ResetRetainedSyncTrackingState()
    {
        _lastSynchronizedDirtyRenderRoots.Clear();
        _lastSynchronizedDirtyRenderSpans.Clear();
        _lastRetainedSyncUsedFullRebuild = false;
    }

    private void SynchronizeRetainedRenderList()
    {
        ResetRetainedSyncTrackingState();
        _lastAncestorMetadataRefreshNodeCount = 0;
        _pendingAncestorMetadataRefreshRoots.Clear();
        CompactDirtyRenderQueueForSync();
        _lastRetainedDirtyVisualCount = _dirtyRenderWorkItems.Count;

        if (_renderListNeedsFullRebuild || _retainedRenderList.Count == 0)
        {
            _lastRetainedSyncUsedFullRebuild = true;
            _lastCompletedSynchronizedDirtyRenderRoots.Clear();
            for (var i = 0; i < _dirtyRenderWorkItems.Count; i++)
            {
                _lastCompletedSynchronizedDirtyRenderRoots.Add(_dirtyRenderWorkItems[i].Visual);
            }
            RebuildRetainedRenderList();
            return;
        }

        if (_dirtyRenderWorkItems.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _dirtyRenderWorkItems.Count; i++)
        {
            var dirtyItem = _dirtyRenderWorkItems[i];
            var dirtyVisual = dirtyItem.Visual;
            var forceDeepSync = dirtyItem.RequiresDeepSync;
            var updateSubtreeStart = Stopwatch.GetTimestamp();
            UpdateRenderNodeSubtree(dirtyVisual, forceDeepSync, out var ancestorRefreshRoot);
            _lastRetainedSubtreeUpdateMs += Stopwatch.GetElapsedTime(updateSubtreeStart).TotalMilliseconds;
            _retainedSubtreeSyncCount++;
            if (_renderListNeedsFullRebuild)
            {
                _lastRetainedSyncUsedFullRebuild = true;
                _lastSynchronizedDirtyRenderRoots.Clear();
                _lastSynchronizedDirtyRenderSpans.Clear();
                _lastCompletedSynchronizedDirtyRenderRoots.Clear();
                for (var completedIndex = 0; completedIndex < _dirtyRenderWorkItems.Count; completedIndex++)
                {
                    _lastCompletedSynchronizedDirtyRenderRoots.Add(_dirtyRenderWorkItems[completedIndex].Visual);
                }
                RebuildRetainedRenderList();
                return;
            }

            if (ancestorRefreshRoot != null)
            {
                _pendingAncestorMetadataRefreshRoots.Add(ancestorRefreshRoot);
            }

            _lastSynchronizedDirtyRenderRoots.Add(dirtyVisual);
            if (_renderNodeIndices.TryGetValue(dirtyVisual, out var renderNodeIndex))
            {
                var node = _retainedRenderList[renderNodeIndex];
                _lastSynchronizedDirtyRenderSpans.Add(new DirtyRenderSpan(renderNodeIndex, node.SubtreeEndIndexExclusive));
            }
        }

        _lastCompletedSynchronizedDirtyRenderRoots.Clear();
        _lastCompletedSynchronizedDirtyRenderRoots.AddRange(_lastSynchronizedDirtyRenderRoots);

        var ancestorRefreshStart = Stopwatch.GetTimestamp();
        RefreshQueuedAncestorNodeSubtreeMetadata();
        _lastRetainedAncestorRefreshMs += Stopwatch.GetElapsedTime(ancestorRefreshStart).TotalMilliseconds;
        if (_renderListNeedsFullRebuild)
        {
            _lastRetainedSyncUsedFullRebuild = true;
            _lastSynchronizedDirtyRenderRoots.Clear();
            _lastSynchronizedDirtyRenderSpans.Clear();
            RebuildRetainedRenderList();
            return;
        }

        _dirtyRenderWorkItems.Clear();
        _dirtyRenderSet.Clear();
    }

    private static bool IsDescendantOfAny(UIElement element, IReadOnlyList<UIElement> roots)
    {
        for (var i = 0; i < roots.Count; i++)
        {
            if (IsDescendantOf(element, roots[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static UIElement? FindAncestorIn(UIElement element, IReadOnlyList<UIElement> roots)
    {
        for (var i = 0; i < roots.Count; i++)
        {
            if (IsDescendantOf(element, roots[i]))
            {
                return roots[i];
            }
        }

        return null;
    }

    private static bool IsDescendantOf(UIElement element, UIElement potentialAncestor)
    {
        for (var current = element.VisualParent; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, potentialAncestor))
            {
                return true;
            }
        }

        return false;
    }

    private enum ShallowSyncRejectReason
    {
        None,
        RenderStateChanged,
        EffectiveVisibilityChanged,
        StructureMismatch,
        DescendantStateChanged
    }

    private void RebuildRetainedRenderList()
    {
        _retainedFullRebuildCount++;
        var previousCount = _retainedRenderList.Count;
        _retainedRenderList.Clear();
        if (previousCount > 0 && _retainedRenderList.Capacity < previousCount)
        {
            _retainedRenderList.Capacity = previousCount;
        }

        _renderNodeIndices.Clear();
        if (previousCount > 0)
        {
            _renderNodeIndices.EnsureCapacity(previousCount);
        }

        _ = BuildRenderSubtree(_visualRoot, traversalOrder: 0, depth: 0, parentNode: null);
        _renderListNeedsFullRebuild = false;
        MarkFullFrameDirty(UiFullDirtyReason.RetainedRebuild);
        ClearDirtyRenderQueue();
    }

    private (int TraversalOrder, SubtreeMetadata Metadata) BuildRenderSubtree(UIElement visual, int traversalOrder, int depth, RenderNode? parentNode)
    {
        var nodeIndex = _retainedRenderList.Count;
        var node = CreateRenderNode(visual, traversalOrder, depth, subtreeEndIndexExclusive: nodeIndex + 1, parentNode);
        _renderNodeIndices[visual] = nodeIndex;
        _retainedRenderList.Add(node);
        traversalOrder += 1;

        var metadata = CreateSubtreeMetadataForNode(node);
        foreach (var child in visual.GetRetainedRenderChildren())
        {
            var childResult = BuildRenderSubtree(child, traversalOrder, depth + 1, node);
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

    private void UpdateRenderNodeSubtree(UIElement dirtySubtreeRoot, bool forceDeepSync, out UIElement? ancestorRefreshRoot)
    {
        ancestorRefreshRoot = null;
        var shallowRejectReason = ShallowSyncRejectReason.None;
        if (!IsPartOfVisualTree(dirtySubtreeRoot))
        {
            _renderListNeedsFullRebuild = true;
            return;
        }

        if (!_renderNodeIndices.TryGetValue(dirtySubtreeRoot, out var dirtySubtreeRootIndex))
        {
            _renderListNeedsFullRebuild = true;
            return;
        }

        var previousRootNode = _retainedRenderList[dirtySubtreeRootIndex];

        if (!forceDeepSync && TryUpdateRenderNodeSubtreeShallow(dirtySubtreeRoot, out shallowRejectReason, out var shallowMetadataChanged))
        {
            if (shallowMetadataChanged)
            {
                ancestorRefreshRoot = dirtySubtreeRoot.VisualParent;
            }

            return;
        }

        if (forceDeepSync)
        {
            _lastRetainedForceDeepSyncCount++;
            var canDowngradeForcedDeep = CanSafelyDowngradeForcedDeepSync(dirtySubtreeRoot);
            if (!canDowngradeForcedDeep)
            {
                _lastRetainedForcedDeepDowngradeBlockedCount++;
            }

            if (canDowngradeForcedDeep && TryUpdateRenderNodeSubtreeShallow(dirtySubtreeRoot, out shallowRejectReason, out shallowMetadataChanged))
            {
                if (shallowMetadataChanged)
                {
                    ancestorRefreshRoot = dirtySubtreeRoot.VisualParent;
                }

                return;
            }
        }

        RenderNode? parentNode = null;
        if (dirtySubtreeRoot.VisualParent != null &&
            _renderNodeIndices.TryGetValue(dirtySubtreeRoot.VisualParent, out var parentNodeIndex))
        {
            parentNode = _retainedRenderList[parentNodeIndex];
        }

        var deepSyncStart = Stopwatch.GetTimestamp();
        _ = UpdateRenderNodeSubtreeRecursive(dirtySubtreeRoot, parentNode);
        _lastRetainedDeepSyncMs += Stopwatch.GetElapsedTime(deepSyncStart).TotalMilliseconds;
        if (_renderListNeedsFullRebuild)
        {
            return;
        }

        var updatedRootNode = _retainedRenderList[dirtySubtreeRootIndex];
        if (!HasEquivalentSubtreeMetadata(previousRootNode, updatedRootNode))
        {
            ancestorRefreshRoot = dirtySubtreeRoot.VisualParent;
        }
    }

    private bool TryUpdateRenderNodeSubtreeShallow(UIElement visual, out ShallowSyncRejectReason rejectReason, out bool subtreeMetadataChanged)
    {
        var shallowStart = Stopwatch.GetTimestamp();
        rejectReason = ShallowSyncRejectReason.None;
        subtreeMetadataChanged = false;
        if (!_renderNodeIndices.TryGetValue(visual, out var renderNodeIndex))
        {
            _renderListNeedsFullRebuild = true;
            rejectReason = ShallowSyncRejectReason.StructureMismatch;
            _lastRetainedShallowRejectStructureCount++;
            _lastRetainedShallowSyncMs += Stopwatch.GetElapsedTime(shallowStart).TotalMilliseconds;
            return false;
        }

        var previous = _retainedRenderList[renderNodeIndex];
        RenderNode? parentNode = null;
        if (visual.VisualParent != null &&
            _renderNodeIndices.TryGetValue(visual.VisualParent, out var parentNodeIndex))
        {
            parentNode = _retainedRenderList[parentNodeIndex];
        }

        if (CanUseScrollTranslationFastPath(visual))
        {
            var updatedForTranslation = CreateRenderNode(
                visual,
                previous.TraversalOrder,
                previous.Depth,
                previous.SubtreeEndIndexExclusive,
                parentNode);
            if (previous.IsEffectivelyVisible == updatedForTranslation.IsEffectivelyVisible &&
                TryUpdateRenderNodeSubtreeForScrollTranslation(
                    renderNodeIndex,
                    visual,
                    previous,
                    updatedForTranslation,
                    out subtreeMetadataChanged))
            {
                _lastRetainedShallowSuccessCount++;
                _lastRetainedShallowSyncMs += Stopwatch.GetElapsedTime(shallowStart).TotalMilliseconds;
                return true;
            }
        }

        if (!TryUpdateRetainedSubtreeNodesInPlace(
                visual,
                renderNodeIndex,
                parentNode,
                out var updated))
        {
            rejectReason = ShallowSyncRejectReason.DescendantStateChanged;
            _lastRetainedShallowRejectStructureCount++;
            _lastRetainedShallowSyncMs += Stopwatch.GetElapsedTime(shallowStart).TotalMilliseconds;
            return false;
        }

        if (previous.RenderStateSignature != updated.RenderStateSignature ||
            previous.IsEffectivelyVisible != updated.IsEffectivelyVisible)
        {
            if (previous.IsEffectivelyVisible == updated.IsEffectivelyVisible &&
                TryUpdateRenderNodeSubtreeForScrollTranslation(renderNodeIndex, visual, previous, updated, out subtreeMetadataChanged))
            {
                return true;
            }

            rejectReason = previous.IsEffectivelyVisible != updated.IsEffectivelyVisible
                ? ShallowSyncRejectReason.EffectiveVisibilityChanged
                : ShallowSyncRejectReason.RenderStateChanged;
            if (rejectReason == ShallowSyncRejectReason.EffectiveVisibilityChanged)
            {
                _lastRetainedShallowRejectVisibilityCount++;
            }
            else if (rejectReason == ShallowSyncRejectReason.RenderStateChanged)
            {
                _lastRetainedShallowRejectRenderStateCount++;
            }

            _lastRetainedShallowSyncMs += Stopwatch.GetElapsedTime(shallowStart).TotalMilliseconds;
            return false;
        }
        subtreeMetadataChanged = !HasEquivalentSubtreeMetadata(previous, updated);
        _lastRetainedShallowSuccessCount++;
        _lastRetainedShallowSyncMs += Stopwatch.GetElapsedTime(shallowStart).TotalMilliseconds;
        return true;
    }

    private bool TryUpdateRenderNodeSubtreeForScrollTranslation(
        int renderNodeIndex,
        UIElement visual,
        RenderNode previous,
        RenderNode updated,
        out bool subtreeMetadataChanged)
    {
        subtreeMetadataChanged = false;
        if (!IsScrollTranslationFastPathCandidate(visual) ||
            !TryGetTranslationDelta(previous, updated, out var translationX, out var translationY))
        {
            return false;
        }

        var subtreeEndIndexExclusive = previous.SubtreeEndIndexExclusive;
        if (subtreeEndIndexExclusive <= renderNodeIndex ||
            subtreeEndIndexExclusive > _retainedRenderList.Count)
        {
            return false;
        }

        if (!RetainedDirectChildSpanMatchesCurrentVisualChildren(
                visual,
                renderNodeIndex,
                subtreeEndIndexExclusive))
        {
            _renderListNeedsFullRebuild = true;
            return false;
        }

        var translatedRoot = updated.WithSubtreeMetadata(
            subtreeEndIndexExclusive,
            previous.HasSubtreeBoundsSnapshot,
            TranslateRect(previous.SubtreeBoundsSnapshot, translationX, translationY),
            previous.SubtreeVisualCount,
            previous.SubtreeHighCostVisualCount,
            previous.SubtreeRenderVersionStamp,
            previous.SubtreeLayoutVersionStamp);
        var totalTranslationX = previous.HasScrollTranslation
            ? previous.ScrollTranslationX + translationX
            : translationX;
        var totalTranslationY = previous.HasScrollTranslation
            ? previous.ScrollTranslationY + translationY
            : translationY;
        translatedRoot = translatedRoot.WithScrollTranslation(totalTranslationX, totalTranslationY);
        _retainedRenderList[renderNodeIndex] = translatedRoot;

        subtreeMetadataChanged = !HasEquivalentSubtreeMetadata(previous, translatedRoot);
        return true;
    }

    private bool RetainedDirectChildSpanMatchesCurrentVisualChildren(
        UIElement visual,
        int renderNodeIndex,
        int subtreeEndIndexExclusive)
    {
        var expectedChildNodeIndex = renderNodeIndex + 1;
        foreach (var child in visual.GetRetainedRenderChildren())
        {
            if (!_renderNodeIndices.TryGetValue(child, out var childNodeIndex) ||
                childNodeIndex != expectedChildNodeIndex ||
                childNodeIndex < 0 ||
                childNodeIndex >= _retainedRenderList.Count)
            {
                return false;
            }

            expectedChildNodeIndex = _retainedRenderList[childNodeIndex].SubtreeEndIndexExclusive;
            if (expectedChildNodeIndex > subtreeEndIndexExclusive)
            {
                return false;
            }
        }

        return expectedChildNodeIndex == subtreeEndIndexExclusive;
    }

    private static RenderNode TranslateRetainedSubtreeNode(
        RenderNode previous,
        RenderNode? parentNode,
        float translationX,
        float translationY)
    {
        var hasTransformFromThisToRoot = false;
        var transformFromThisToRoot = Matrix.Identity;
        if (parentNode.HasValue)
        {
            var parent = parentNode.Value;
            if (previous.HasLocalTransform && parent.HasTransformFromThisToRoot)
            {
                transformFromThisToRoot = previous.LocalTransform * parent.TransformFromThisToRoot;
                hasTransformFromThisToRoot = true;
            }
            else if (previous.HasLocalTransform)
            {
                transformFromThisToRoot = previous.LocalTransform;
                hasTransformFromThisToRoot = true;
            }
            else if (parent.HasTransformFromThisToRoot)
            {
                transformFromThisToRoot = parent.TransformFromThisToRoot;
                hasTransformFromThisToRoot = true;
            }
        }
        else if (previous.HasLocalTransform)
        {
            transformFromThisToRoot = previous.LocalTransform;
            hasTransformFromThisToRoot = true;
        }

        var renderStateSignature = MixHash(parentNode?.RenderStateSignature ?? 17, previous.LocalRenderStateSignature);
        var boundsSnapshot = previous.HasBoundsSnapshot
            ? TranslateRect(previous.BoundsSnapshot, translationX, translationY)
            : previous.BoundsSnapshot;
        var subtreeBoundsSnapshot = previous.HasSubtreeBoundsSnapshot
            ? TranslateRect(previous.SubtreeBoundsSnapshot, translationX, translationY)
            : previous.SubtreeBoundsSnapshot;

        return new RenderNode(
            previous.Visual,
            previous.TraversalOrder,
            previous.Depth,
            boundsSnapshot,
            previous.HasBoundsSnapshot,
            previous.HasLocalClip,
            previous.LocalClipRect,
            previous.HasLocalTransform,
            previous.LocalTransform,
            renderStateSignature,
            previous.LocalRenderStateSignature,
            hasTransformFromThisToRoot,
            transformFromThisToRoot,
            previous.IsEffectivelyVisible,
            previous.SubtreeEndIndexExclusive,
            previous.HasSubtreeBoundsSnapshot,
            subtreeBoundsSnapshot,
            previous.SubtreeVisualCount,
            previous.SubtreeHighCostVisualCount,
            previous.SubtreeRenderVersionStamp,
            previous.SubtreeLayoutVersionStamp,
            previous.HasScrollTranslation,
            previous.ScrollTranslationX,
            previous.ScrollTranslationY);
    }

    private static bool IsScrollTranslationFastPathCandidate(UIElement visual)
    {
        return visual is IScrollTransformContent or VirtualizingStackPanel;
    }

    private static bool CanUseScrollTranslationFastPath(UIElement visual)
    {
        if (!IsScrollTranslationFastPathCandidate(visual))
        {
            return false;
        }

        foreach (var child in visual.GetRetainedRenderChildren())
        {
            if (child.SubtreeDirty)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetTranslationDelta(RenderNode previous, RenderNode updated, out float translationX, out float translationY)
    {
        translationX = 0f;
        translationY = 0f;

        if (previous.HasLocalClip != updated.HasLocalClip ||
            (previous.HasLocalClip && !AreRectsEqual(previous.LocalClipRect, updated.LocalClipRect)))
        {
            return false;
        }

        if (!TryDecomposePureTranslation(previous.HasLocalTransform, previous.LocalTransform, out var previousTranslationX, out var previousTranslationY) ||
            !TryDecomposePureTranslation(updated.HasLocalTransform, updated.LocalTransform, out var updatedTranslationX, out var updatedTranslationY))
        {
            return false;
        }

        translationX = updatedTranslationX - previousTranslationX;
        translationY = updatedTranslationY - previousTranslationY;
        return MathF.Abs(translationX) > 0.0001f || MathF.Abs(translationY) > 0.0001f;
    }

    private static bool TryDecomposePureTranslation(bool hasTransform, Matrix transform, out float translationX, out float translationY)
    {
        translationX = 0f;
        translationY = 0f;
        if (!hasTransform)
        {
            return true;
        }

        if (!AreClose(transform.M11, 1f) ||
            !AreClose(transform.M22, 1f) ||
            !AreClose(transform.M33, 1f) ||
            !AreClose(transform.M44, 1f) ||
            !AreClose(transform.M12, 0f) ||
            !AreClose(transform.M13, 0f) ||
            !AreClose(transform.M14, 0f) ||
            !AreClose(transform.M21, 0f) ||
            !AreClose(transform.M23, 0f) ||
            !AreClose(transform.M24, 0f) ||
            !AreClose(transform.M31, 0f) ||
            !AreClose(transform.M32, 0f) ||
            !AreClose(transform.M34, 0f) ||
            !AreClose(transform.M43, 0f))
        {
            return false;
        }

        translationX = transform.M41;
        translationY = transform.M42;
        return true;
    }

    private static LayoutRect TranslateRect(LayoutRect rect, float translationX, float translationY)
    {
        return new LayoutRect(rect.X + translationX, rect.Y + translationY, rect.Width, rect.Height);
    }

    private LayoutRect ApplyScrollTranslationFromAncestors(UIElement visual, LayoutRect bounds)
    {
        if (TryGetScrollTranslationOffsetFromAncestors(visual, out var translationX, out var translationY))
        {
            return TranslateRect(bounds, translationX, translationY);
        }

        return bounds;
    }

    private bool TryGetScrollTranslationOffsetFromAncestors(UIElement visual, out float translationX, out float translationY)
    {
        translationX = 0f;
        translationY = 0f;
        var hasOffset = false;
        for (var current = visual.VisualParent; current != null; current = current.VisualParent)
        {
            if (!_renderNodeIndices.TryGetValue(current, out var nodeIndex))
            {
                continue;
            }

            var node = _retainedRenderList[nodeIndex];
            if (!node.HasScrollTranslation)
            {
                continue;
            }

            translationX += node.ScrollTranslationX;
            translationY += node.ScrollTranslationY;
            hasOffset = true;
        }

        return hasOffset;
    }

    private static RenderNode TranslateRetainedNodeBounds(RenderNode node, float translationX, float translationY)
    {
        if (AreClose(translationX, 0f) && AreClose(translationY, 0f))
        {
            return node;
        }

        var boundsSnapshot = node.HasBoundsSnapshot
            ? TranslateRect(node.BoundsSnapshot, translationX, translationY)
            : node.BoundsSnapshot;
        var subtreeBoundsSnapshot = node.HasSubtreeBoundsSnapshot
            ? TranslateRect(node.SubtreeBoundsSnapshot, translationX, translationY)
            : node.SubtreeBoundsSnapshot;
        return node.WithBoundsSnapshots(
            boundsSnapshot,
            node.HasBoundsSnapshot,
            subtreeBoundsSnapshot,
            node.HasSubtreeBoundsSnapshot);
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.0001f;
    }

    private bool CanSafelyDowngradeForcedDeepSync(UIElement root)
    {
        foreach (var child in root.GetRetainedRenderChildren())
        {
            if (child.SubtreeDirty)
            {
                return false;
            }
        }

        return true;
    }

    private SubtreeMetadata UpdateRenderNodeSubtreeRecursive(UIElement visual, RenderNode? parentNode)
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
            previous.SubtreeEndIndexExclusive,
            parentNode);
        var metadata = CreateSubtreeMetadataForNode(updated);
        var expectedChildNodeIndex = renderNodeIndex + 1;

        foreach (var child in visual.GetRetainedRenderChildren())
        {
            if (!_renderNodeIndices.TryGetValue(child, out var childNodeIndex) ||
                childNodeIndex != expectedChildNodeIndex)
            {
                _renderListNeedsFullRebuild = true;
                return default;
            }

            var childMetadata = UpdateRenderNodeSubtreeRecursive(child, updated);
            if (_renderListNeedsFullRebuild)
            {
                return default;
            }

            metadata = MergeSubtreeMetadata(metadata, childMetadata);
            expectedChildNodeIndex = _retainedRenderList[childNodeIndex].SubtreeEndIndexExclusive;
        }

        if (expectedChildNodeIndex != previous.SubtreeEndIndexExclusive)
        {
            _renderListNeedsFullRebuild = true;
            return default;
        }

        updated = updated.WithSubtreeMetadata(
            expectedChildNodeIndex,
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

    private void RefreshQueuedAncestorNodeSubtreeMetadata()
    {
        if (_pendingAncestorMetadataRefreshRoots.Count == 0)
        {
            return;
        }

        _ancestorMetadataRefreshBuffer.Clear();
        _ancestorMetadataRefreshSet.Clear();
        for (var i = 0; i < _pendingAncestorMetadataRefreshRoots.Count; i++)
        {
            for (var current = _pendingAncestorMetadataRefreshRoots[i]; current != null; current = current.VisualParent)
            {
                if (!_ancestorMetadataRefreshSet.Add(current))
                {
                    break;
                }

                _ancestorMetadataRefreshBuffer.Add(current);
            }
        }

        _pendingAncestorMetadataRefreshRoots.Clear();
        _ancestorMetadataRefreshBuffer.Sort(CompareAncestorRefreshVisuals);
        for (var i = 0; i < _ancestorMetadataRefreshBuffer.Count; i++)
        {
            if (!TryRefreshAncestorNodeMetadata(_ancestorMetadataRefreshBuffer[i]))
            {
                return;
            }
        }
    }

    private int CompareAncestorRefreshVisuals(UIElement left, UIElement right)
    {
        if (!_renderNodeIndices.TryGetValue(left, out var leftIndex) ||
            !_renderNodeIndices.TryGetValue(right, out var rightIndex))
        {
            return 0;
        }

        var depthCompare = _retainedRenderList[rightIndex].Depth.CompareTo(_retainedRenderList[leftIndex].Depth);
        if (depthCompare != 0)
        {
            return depthCompare;
        }

        return rightIndex.CompareTo(leftIndex);
    }

    private bool TryRefreshAncestorNodeMetadata(UIElement visual)
    {
        if (!_renderNodeIndices.TryGetValue(visual, out var renderNodeIndex))
        {
            _renderListNeedsFullRebuild = true;
            return false;
        }

        var previous = _retainedRenderList[renderNodeIndex];
        RenderNode? parentNode = null;
        if (visual.VisualParent != null &&
            _renderNodeIndices.TryGetValue(visual.VisualParent, out var parentNodeIndex))
        {
            parentNode = _retainedRenderList[parentNodeIndex];
        }

        if (!TryBuildUpdatedNodeWithCurrentChildren(visual, previous, parentNode, out var updated))
        {
            return false;
        }

        _retainedRenderList[renderNodeIndex] = updated;
        _lastAncestorMetadataRefreshNodeCount++;
        return true;
    }

    private bool TryBuildUpdatedNodeWithCurrentChildren(UIElement visual, RenderNode previous, RenderNode? parentNode, out RenderNode updated)
    {
        updated = CreateRenderNode(
            visual,
            previous.TraversalOrder,
            previous.Depth,
            previous.SubtreeEndIndexExclusive,
            parentNode);

        var metadata = CreateSubtreeMetadataForNode(updated);
        foreach (var child in visual.GetRetainedRenderChildren())
        {
            if (!_renderNodeIndices.TryGetValue(child, out var childNodeIndex))
            {
                _renderListNeedsFullRebuild = true;
                updated = default;
                return false;
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
        return true;
    }

    private bool TryUpdateRetainedSubtreeNodesInPlace(
        UIElement visual,
        int renderNodeIndex,
        RenderNode? parentNode,
        out RenderNode updated)
    {
        if (renderNodeIndex < 0 || renderNodeIndex >= _retainedRenderList.Count)
        {
            updated = default;
            _renderListNeedsFullRebuild = true;
            return false;
        }

        var previous = _retainedRenderList[renderNodeIndex];
        updated = CreateRenderNode(
            visual,
            previous.TraversalOrder,
            previous.Depth,
            previous.SubtreeEndIndexExclusive,
            parentNode);

        var metadata = CreateSubtreeMetadataForNode(updated);
        var expectedChildNodeIndex = renderNodeIndex + 1;
        foreach (var child in visual.GetRetainedRenderChildren())
        {
            if (!_renderNodeIndices.TryGetValue(child, out var childNodeIndex))
            {
                _renderListNeedsFullRebuild = true;
                updated = default;
                return false;
            }

            if (childNodeIndex != expectedChildNodeIndex)
            {
                updated = default;
                return false;
            }

            if (!TryUpdateRetainedSubtreeNodesInPlace(
                    child,
                    childNodeIndex,
                    updated,
                    out var currentChild))
            {
                updated = default;
                return false;
            }

            metadata = MergeSubtreeMetadata(
                metadata,
                CreateSubtreeMetadataFromSubtreeNode(currentChild));
            expectedChildNodeIndex = currentChild.SubtreeEndIndexExclusive;
        }

        if (previous.SubtreeEndIndexExclusive != expectedChildNodeIndex)
        {
            updated = default;
            return false;
        }

        updated = updated.WithSubtreeMetadata(
            previous.SubtreeEndIndexExclusive,
            metadata.HasBoundsSnapshot,
            metadata.BoundsSnapshot,
            metadata.VisualCount,
            metadata.HighCostVisualCount,
            metadata.RenderVersionStamp,
            metadata.LayoutVersionStamp);
        if (previous.RenderStateSignature != updated.RenderStateSignature ||
            previous.LocalRenderStateSignature != updated.LocalRenderStateSignature ||
            previous.IsEffectivelyVisible != updated.IsEffectivelyVisible ||
            previous.HasLocalClip != updated.HasLocalClip ||
            (previous.HasLocalClip && !AreRectsEqual(previous.LocalClipRect, updated.LocalClipRect)) ||
            previous.HasLocalTransform != updated.HasLocalTransform ||
            (previous.HasLocalTransform && !AreTransformsEffectivelyEqual(previous.LocalTransform, updated.LocalTransform)))
        {
            updated = default;
            return false;
        }

        RecordBoundsDelta(previous, updated);
        _retainedRenderList[renderNodeIndex] = updated;
        return true;
    }

    private string ValidateRetainedTreeAgainstCurrentVisualState(int maxMismatches)
    {
        if (_retainedRenderList.Count == 0)
        {
            return "retained render list is empty";
        }

        if (!_renderNodeIndices.TryGetValue(_visualRoot, out var rootNodeIndex))
        {
            return "visual root is missing from retained indices";
        }

        var mismatches = new List<string>();
        var expectedTraversalOrder = 0;
        var expectedRenderNodeIndex = 0;
        if (!TryValidateRetainedSubtreeRecursive(
                _visualRoot,
                parentNode: null,
                expectedDepth: 0,
                mismatches,
                Math.Max(1, maxMismatches),
            ancestorScrollTranslationX: 0f,
            ancestorScrollTranslationY: 0f,
                ref expectedTraversalOrder,
                ref expectedRenderNodeIndex,
                out _))
        {
            return mismatches.Count == 0
                ? "retained tree validation aborted"
                : string.Join(" | ", mismatches);
        }

        if (_retainedRenderList[rootNodeIndex].SubtreeEndIndexExclusive != _retainedRenderList.Count)
        {
            mismatches.Add(
                $"root subtree span mismatch retainedEnd={_retainedRenderList[rootNodeIndex].SubtreeEndIndexExclusive} retainedCount={_retainedRenderList.Count}");
        }

        return mismatches.Count == 0 ? "ok" : string.Join(" | ", mismatches);
    }

    private bool TryValidateRetainedSubtreeRecursive(
        UIElement visual,
        RenderNode? parentNode,
        int expectedDepth,
        List<string> mismatches,
        int maxMismatches,
        float ancestorScrollTranslationX,
        float ancestorScrollTranslationY,
        ref int expectedTraversalOrder,
        ref int expectedRenderNodeIndex,
        out RenderNode current)
    {
        current = default;
        if (!_renderNodeIndices.TryGetValue(visual, out var indexedRenderNodeIndex))
        {
            mismatches.Add($"missing retained node for {DescribeElementForDiagnostics(visual)}");
            return false;
        }

        if (indexedRenderNodeIndex != expectedRenderNodeIndex)
        {
            mismatches.Add(
                $"{DescribeElementForDiagnostics(visual)} retainedIndex={indexedRenderNodeIndex} expectedIndex={expectedRenderNodeIndex}");
            return false;
        }

        var retained = _retainedRenderList[expectedRenderNodeIndex];
        var retainedForCompare = TranslateRetainedNodeBounds(retained, ancestorScrollTranslationX, ancestorScrollTranslationY);
        if (!ReferenceEquals(retained.Visual, visual))
        {
            mismatches.Add(
                $"retained preorder mismatch expected={DescribeElementForDiagnostics(visual)} actual={DescribeElementForDiagnostics(retained.Visual)} index={expectedRenderNodeIndex}");
            return false;
        }

        var currentTraversalOrder = expectedTraversalOrder;
        expectedTraversalOrder++;
        expectedRenderNodeIndex++;
        current = CreateRenderNode(
            visual,
            currentTraversalOrder,
            expectedDepth,
            retained.SubtreeEndIndexExclusive,
            parentNode);

        var metadata = CreateSubtreeMetadataForNode(current);
        var childScrollTranslationX = ancestorScrollTranslationX;
        var childScrollTranslationY = ancestorScrollTranslationY;
        if (retained.HasScrollTranslation)
        {
            childScrollTranslationX += retained.ScrollTranslationX;
            childScrollTranslationY += retained.ScrollTranslationY;
        }
        foreach (var child in visual.GetRetainedRenderChildren())
        {
            if (!TryValidateRetainedSubtreeRecursive(
                    child,
                    current,
                    expectedDepth + 1,
                    mismatches,
                    maxMismatches,
                    childScrollTranslationX,
                    childScrollTranslationY,
                    ref expectedTraversalOrder,
                    ref expectedRenderNodeIndex,
                    out var currentChild))
            {
                if (mismatches.Count >= maxMismatches)
                {
                    return false;
                }

                continue;
            }

            metadata = MergeSubtreeMetadata(
                metadata,
                CreateSubtreeMetadataFromSubtreeNode(currentChild));
        }

        current = current.WithSubtreeMetadata(
            expectedRenderNodeIndex,
            metadata.HasBoundsSnapshot,
            metadata.BoundsSnapshot,
            metadata.VisualCount,
            metadata.HighCostVisualCount,
            metadata.RenderVersionStamp,
            metadata.LayoutVersionStamp);

        RecordRetainedNodeMismatchIfNeeded(retainedForCompare, current, mismatches, maxMismatches);
        return mismatches.Count < maxMismatches;
    }

    private void RecordRetainedNodeMismatchIfNeeded(
        RenderNode retained,
        RenderNode current,
        List<string> mismatches,
        int maxMismatches)
    {
        if (mismatches.Count >= maxMismatches)
        {
            return;
        }

        if (retained.TraversalOrder != current.TraversalOrder ||
            retained.Depth != current.Depth ||
            retained.RenderStateSignature != current.RenderStateSignature ||
            retained.LocalRenderStateSignature != current.LocalRenderStateSignature ||
            retained.IsEffectivelyVisible != current.IsEffectivelyVisible ||
            retained.HasBoundsSnapshot != current.HasBoundsSnapshot ||
            (retained.HasBoundsSnapshot && !AreRectsEqual(retained.BoundsSnapshot, current.BoundsSnapshot)) ||
            retained.HasLocalClip != current.HasLocalClip ||
            (retained.HasLocalClip && !AreRectsEqual(retained.LocalClipRect, current.LocalClipRect)) ||
            retained.HasLocalTransform != current.HasLocalTransform ||
            (retained.HasLocalTransform && !AreTransformsEffectivelyEqual(retained.LocalTransform, current.LocalTransform)) ||
            !HasEquivalentSubtreeMetadata(retained, current))
        {
            mismatches.Add(
                $"{DescribeElementForDiagnostics(retained.Visual)} retainedBounds={FormatRectForDiagnostics(retained.HasBoundsSnapshot, retained.BoundsSnapshot)} " +
                $"currentBounds={FormatRectForDiagnostics(current.HasBoundsSnapshot, current.BoundsSnapshot)} " +
                $"retainedSubtree={FormatRectForDiagnostics(retained.HasSubtreeBoundsSnapshot, retained.SubtreeBoundsSnapshot)} " +
                $"currentSubtree={FormatRectForDiagnostics(current.HasSubtreeBoundsSnapshot, current.SubtreeBoundsSnapshot)}");
        }
    }

    private static string FormatRectForDiagnostics(bool hasRect, LayoutRect rect)
    {
        return hasRect ? $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}" : "none";
    }

    private static bool HasEquivalentSubtreeMetadata(RenderNode previous, RenderNode updated)
    {
        return previous.SubtreeRenderVersionStamp == updated.SubtreeRenderVersionStamp &&
               previous.SubtreeLayoutVersionStamp == updated.SubtreeLayoutVersionStamp &&
               previous.SubtreeVisualCount == updated.SubtreeVisualCount &&
               previous.SubtreeHighCostVisualCount == updated.SubtreeHighCostVisualCount &&
               previous.SubtreeEndIndexExclusive == updated.SubtreeEndIndexExclusive &&
               previous.HasSubtreeBoundsSnapshot == updated.HasSubtreeBoundsSnapshot &&
               (!previous.HasSubtreeBoundsSnapshot || AreRectsEqual(previous.SubtreeBoundsSnapshot, updated.SubtreeBoundsSnapshot));
    }

    private RenderNode CreateRenderNode(
        UIElement visual,
        int traversalOrder,
        int depth,
        int subtreeEndIndexExclusive,
        RenderNode? parentNode)
    {
        var localRenderState = CaptureLocalRenderState(visual);
        var hasBounds = TryComputeBoundsFromParentState(
            visual,
            parentNode,
            localRenderState.HasLocalTransform,
            localRenderState.LocalTransform,
            out var bounds,
            out var hasTransformFromThisToRoot,
            out var transformFromThisToRoot);
        var parentRenderSignature = parentNode?.RenderStateSignature ?? 17;
        var renderStateSignature = MixHash(parentRenderSignature, localRenderState.LocalRenderStateSignature);
        var isEffectivelyVisible = parentNode.HasValue
            ? parentNode.Value.IsEffectivelyVisible && visual.IsVisible
            : IsEffectivelyVisible(visual);

        return new RenderNode(
            visual,
            traversalOrder,
            depth,
            bounds,
            hasBounds,
            localRenderState.HasLocalClip,
            localRenderState.LocalClipRect,
            localRenderState.HasLocalTransform,
            localRenderState.LocalTransform,
            renderStateSignature,
            localRenderState.LocalRenderStateSignature,
            hasTransformFromThisToRoot,
            transformFromThisToRoot,
            isEffectivelyVisible,
            subtreeEndIndexExclusive,
            hasBounds,
            bounds,
            1,
            IsHighCostVisual(visual) ? 1 : 0,
            MixHash(17, visual.RenderVersionStamp),
            MixHash(17, visual.LayoutVersionStamp),
            false,
            0f,
            0f);
    }

    private static bool TryComputeBoundsFromParentState(
        UIElement visual,
        RenderNode? parentNode,
        bool hasLocalTransform,
        Matrix localTransform,
        out LayoutRect bounds,
        out bool hasTransformFromThisToRoot,
        out Matrix transformFromThisToRoot)
    {
        if (!visual.TryGetLocalRenderBoundsSnapshot(out var localBounds))
        {
            bounds = localBounds;
            hasTransformFromThisToRoot = false;
            transformFromThisToRoot = Matrix.Identity;
            return false;
        }

        if (parentNode.HasValue)
        {
            var parent = parentNode.Value;
            if (hasLocalTransform && parent.HasTransformFromThisToRoot)
            {
                transformFromThisToRoot = localTransform * parent.TransformFromThisToRoot;
                hasTransformFromThisToRoot = true;
            }
            else if (hasLocalTransform)
            {
                transformFromThisToRoot = localTransform;
                hasTransformFromThisToRoot = true;
            }
            else if (parent.HasTransformFromThisToRoot)
            {
                transformFromThisToRoot = parent.TransformFromThisToRoot;
                hasTransformFromThisToRoot = true;
            }
            else
            {
                transformFromThisToRoot = Matrix.Identity;
                hasTransformFromThisToRoot = false;
            }
        }
        else if (hasLocalTransform)
        {
            transformFromThisToRoot = localTransform;
            hasTransformFromThisToRoot = true;
        }
        else
        {
            transformFromThisToRoot = Matrix.Identity;
            hasTransformFromThisToRoot = false;
        }

        if (!hasTransformFromThisToRoot)
        {
            bounds = localBounds;
            return true;
        }

        var topLeft = Vector2.Transform(new Vector2(localBounds.X, localBounds.Y), transformFromThisToRoot);
        var topRight = Vector2.Transform(new Vector2(localBounds.X + localBounds.Width, localBounds.Y), transformFromThisToRoot);
        var bottomLeft = Vector2.Transform(new Vector2(localBounds.X, localBounds.Y + localBounds.Height), transformFromThisToRoot);
        var bottomRight = Vector2.Transform(new Vector2(localBounds.X + localBounds.Width, localBounds.Y + localBounds.Height), transformFromThisToRoot);
        var minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
        var minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
        var maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
        var maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));
        bounds = new LayoutRect(minX, minY, MathF.Max(0f, maxX - minX), MathF.Max(0f, maxY - minY));
        return bounds.Width > 0f && bounds.Height > 0f;
    }

    private static CapturedLocalRenderState CaptureLocalRenderState(UIElement visual)
    {
        var hasLocalClip = visual.TryGetLocalClipSnapshot(out var localClipRect);
        var hasLocalTransform = visual.TryGetLocalRenderTransformSnapshot(out var localTransform);
        var localRenderStateSignature = 17;
        if (hasLocalTransform)
        {
            localRenderStateSignature = MixTransformStepHash(localRenderStateSignature, localTransform);
        }

        if (hasLocalClip)
        {
            localRenderStateSignature = MixClipStepHash(localRenderStateSignature, localClipRect);
        }

        return new CapturedLocalRenderState(
            hasLocalClip,
            localClipRect,
            hasLocalTransform,
            localTransform,
            localRenderStateSignature);
    }

    private static int MixClipStepHash(int hash, LayoutRect clipRect)
    {
        hash = MixHash(hash, 1);
        hash = MixHash(hash, BitConverter.SingleToInt32Bits(clipRect.X));
        hash = MixHash(hash, BitConverter.SingleToInt32Bits(clipRect.Y));
        hash = MixHash(hash, BitConverter.SingleToInt32Bits(clipRect.Width));
        hash = MixHash(hash, BitConverter.SingleToInt32Bits(clipRect.Height));
        return hash;
    }

    private static int MixTransformStepHash(int hash, Matrix transform)
    {
        hash = MixHash(hash, 2);
        hash = MixHash(hash, transform.GetHashCode());
        return hash;
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

    private static SubtreeMetadata CreateSubtreeMetadataForNode(RenderNode node)
    {
        return new SubtreeMetadata(
            node.HasBoundsSnapshot,
            node.BoundsSnapshot,
            1,
            IsHighCostVisual(node.Visual) ? 1 : 0,
            MixHash(17, node.Visual.RenderVersionStamp),
            MixHash(17, node.Visual.LayoutVersionStamp));
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

    private static bool AreViewportsEqual(Viewport left, Viewport right)
    {
        return left.X == right.X &&
               left.Y == right.Y &&
               left.Width == right.Width &&
               left.Height == right.Height;
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
            bool hasLocalClip,
            LayoutRect localClipRect,
            bool hasLocalTransform,
            Matrix localTransform,
            int renderStateSignature,
            int localRenderStateSignature,
            bool hasTransformFromThisToRoot,
            Matrix transformFromThisToRoot,
            bool isEffectivelyVisible,
            int subtreeEndIndexExclusive,
            bool hasSubtreeBoundsSnapshot,
            LayoutRect subtreeBoundsSnapshot,
            int subtreeVisualCount,
            int subtreeHighCostVisualCount,
            int subtreeRenderVersionStamp,
            int subtreeLayoutVersionStamp,
            bool hasScrollTranslation,
            float scrollTranslationX,
            float scrollTranslationY)
        {
            Visual = visual;
            TraversalOrder = traversalOrder;
            Depth = depth;
            BoundsSnapshot = boundsSnapshot;
            HasBoundsSnapshot = hasBoundsSnapshot;
            HasLocalClip = hasLocalClip;
            LocalClipRect = localClipRect;
            HasLocalTransform = hasLocalTransform;
            LocalTransform = localTransform;
            RenderStateSignature = renderStateSignature;
            LocalRenderStateSignature = localRenderStateSignature;
            HasTransformFromThisToRoot = hasTransformFromThisToRoot;
            TransformFromThisToRoot = transformFromThisToRoot;
            IsEffectivelyVisible = isEffectivelyVisible;
            SubtreeEndIndexExclusive = subtreeEndIndexExclusive;
            HasSubtreeBoundsSnapshot = hasSubtreeBoundsSnapshot;
            SubtreeBoundsSnapshot = subtreeBoundsSnapshot;
            SubtreeVisualCount = subtreeVisualCount;
            SubtreeHighCostVisualCount = subtreeHighCostVisualCount;
            SubtreeRenderVersionStamp = subtreeRenderVersionStamp;
            SubtreeLayoutVersionStamp = subtreeLayoutVersionStamp;
            HasScrollTranslation = hasScrollTranslation;
            ScrollTranslationX = scrollTranslationX;
            ScrollTranslationY = scrollTranslationY;
        }

        public UIElement Visual { get; }

        public int TraversalOrder { get; }

        public int Depth { get; }

        public LayoutRect BoundsSnapshot { get; }

        public bool HasBoundsSnapshot { get; }

        public bool HasLocalClip { get; }

        public LayoutRect LocalClipRect { get; }

        public bool HasLocalTransform { get; }

        public Matrix LocalTransform { get; }

        public int RenderStateSignature { get; }

        public int LocalRenderStateSignature { get; }

        public bool HasTransformFromThisToRoot { get; }

        public Matrix TransformFromThisToRoot { get; }

        public bool HasScrollTranslation { get; }

        public float ScrollTranslationX { get; }

        public float ScrollTranslationY { get; }

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
                HasLocalClip,
                LocalClipRect,
                HasLocalTransform,
                LocalTransform,
                RenderStateSignature,
                LocalRenderStateSignature,
                HasTransformFromThisToRoot,
                TransformFromThisToRoot,
                IsEffectivelyVisible,
                subtreeEndIndexExclusive,
                hasSubtreeBoundsSnapshot,
                subtreeBoundsSnapshot,
                subtreeVisualCount,
                subtreeHighCostVisualCount,
                subtreeRenderVersionStamp,
                subtreeLayoutVersionStamp,
                HasScrollTranslation,
                ScrollTranslationX,
                ScrollTranslationY);
        }

        public RenderNode WithBoundsSnapshots(
            LayoutRect boundsSnapshot,
            bool hasBoundsSnapshot,
            LayoutRect subtreeBoundsSnapshot,
            bool hasSubtreeBoundsSnapshot)
        {
            return new RenderNode(
                Visual,
                TraversalOrder,
                Depth,
                boundsSnapshot,
                hasBoundsSnapshot,
                HasLocalClip,
                LocalClipRect,
                HasLocalTransform,
                LocalTransform,
                RenderStateSignature,
                LocalRenderStateSignature,
                HasTransformFromThisToRoot,
                TransformFromThisToRoot,
                IsEffectivelyVisible,
                SubtreeEndIndexExclusive,
                hasSubtreeBoundsSnapshot,
                subtreeBoundsSnapshot,
                SubtreeVisualCount,
                SubtreeHighCostVisualCount,
                SubtreeRenderVersionStamp,
                SubtreeLayoutVersionStamp,
                HasScrollTranslation,
                ScrollTranslationX,
                ScrollTranslationY);
        }

        public RenderNode WithScrollTranslation(float scrollTranslationX, float scrollTranslationY)
        {
            var hasScrollTranslation = MathF.Abs(scrollTranslationX) > 0.0001f || MathF.Abs(scrollTranslationY) > 0.0001f;
            return new RenderNode(
                Visual,
                TraversalOrder,
                Depth,
                BoundsSnapshot,
                HasBoundsSnapshot,
                HasLocalClip,
                LocalClipRect,
                HasLocalTransform,
                LocalTransform,
                RenderStateSignature,
                LocalRenderStateSignature,
                HasTransformFromThisToRoot,
                TransformFromThisToRoot,
                IsEffectivelyVisible,
                SubtreeEndIndexExclusive,
                HasSubtreeBoundsSnapshot,
                SubtreeBoundsSnapshot,
                SubtreeVisualCount,
                SubtreeHighCostVisualCount,
                SubtreeRenderVersionStamp,
                SubtreeLayoutVersionStamp,
                hasScrollTranslation,
                scrollTranslationX,
                scrollTranslationY);
        }
    }

    private readonly record struct ScrollTranslationFrame(int Depth, float TranslationX, float TranslationY);

    private readonly record struct SubtreeMetadata(
        bool HasBoundsSnapshot,
        LayoutRect BoundsSnapshot,
        int VisualCount,
        int HighCostVisualCount,
        int RenderVersionStamp,
        int LayoutVersionStamp);

    private readonly record struct IndexedDirtyRenderCandidate(
        UIElement Visual,
        int RenderNodeIndex,
        int SubtreeEndIndexExclusive,
        bool RequiresDeepSync);

    private readonly record struct DirtyRenderWorkItem(
        UIElement Visual,
        int RenderNodeIndex,
        int SubtreeEndIndexExclusive,
        bool RequiresDeepSync);

    private readonly record struct DirtyRenderSpan(
        int StartIndex,
        int EndIndexExclusive);

    private readonly record struct CapturedLocalRenderState(
        bool HasLocalClip,
        LayoutRect LocalClipRect,
        bool HasLocalTransform,
        Matrix LocalTransform,
        int LocalRenderStateSignature);
}
