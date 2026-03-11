using System;
using System.Collections.Generic;
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
            _dirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
        }
    }

    private bool IsPartOfVisualTree(UIElement? element)
    {
        if (element == null)
        {
            return false;
        }

        EnsureVisualIndexCurrent();
        return _visualIndex.TryGetNode(element, out _) &&
               ReferenceEquals(element.GetVisualRoot(), _visualRoot);
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

    private void CompactDirtyRenderQueueForSync()
    {
        _dirtyRenderRootsRequireDeepSync.Clear();
        _dirtyRenderWorkItems.Clear();
        _dirtyRenderCompactionBuffer.Clear();
        while (_dirtyRenderQueue.TryDequeue(out var visual))
        {
            _dirtyRenderCompactionBuffer.Add(visual);
        }

        _dirtyRenderSet.Clear();
        for (var i = 0; i < _dirtyRenderCompactionBuffer.Count; i++)
        {
            var candidate = _dirtyRenderCompactionBuffer[i];
            if (!IsPartOfVisualTree(candidate))
            {
                continue;
            }

            if (_renderListNeedsFullRebuild || _retainedRenderList.Count == 0)
            {
                _dirtyRenderWorkItems.Add(new DirtyRenderWorkItem(candidate, -1, -1, true));
                continue;
            }

            if (!_renderNodeIndices.TryGetValue(candidate, out var renderNodeIndex))
            {
                _renderListNeedsFullRebuild = true;
                _dirtyRenderWorkItems.Add(new DirtyRenderWorkItem(candidate, -1, -1, true));
                continue;
            }

            var node = _retainedRenderList[renderNodeIndex];
            _dirtyRenderWorkItems.Add(new DirtyRenderWorkItem(candidate, renderNodeIndex, node.SubtreeEndIndexExclusive, false));
        }

        if (_renderListNeedsFullRebuild || _dirtyRenderWorkItems.Count <= 1)
        {
            _lastDirtyRootCountAfterCoalescing = _dirtyRenderWorkItems.Count;
            return;
        }

        _dirtyRenderWorkItems.Sort(static (left, right) => left.RenderNodeIndex.CompareTo(right.RenderNodeIndex));

        var keptCount = 0;
        for (var i = 0; i < _dirtyRenderWorkItems.Count; i++)
        {
            var current = _dirtyRenderWorkItems[i];
            if (keptCount == 0)
            {
                _dirtyRenderWorkItems[keptCount++] = current;
                continue;
            }

            var previous = _dirtyRenderWorkItems[keptCount - 1];
            if (current.RenderNodeIndex < previous.SubtreeEndIndexExclusive)
            {
                _dirtyRenderWorkItems[keptCount - 1] = previous with { RequiresDeepSync = true };
                continue;
            }

            _dirtyRenderWorkItems[keptCount++] = current;
        }

        if (keptCount < _dirtyRenderWorkItems.Count)
        {
            _dirtyRenderWorkItems.RemoveRange(keptCount, _dirtyRenderWorkItems.Count - keptCount);
        }

        _lastDirtyRootCountAfterCoalescing = _dirtyRenderWorkItems.Count;
    }

    private void ClearDirtyRenderQueue()
    {
        _dirtyRenderQueue.Clear();
        _dirtyRenderSet.Clear();
        _dirtyRenderRootsRequireDeepSync.Clear();
        _dirtyRenderWorkItems.Clear();
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
        CompactDirtyRenderQueueForSync();
        _lastRetainedDirtyVisualCount = _dirtyRenderWorkItems.Count;

        if (_renderListNeedsFullRebuild || _retainedRenderList.Count == 0)
        {
            _lastRetainedSyncUsedFullRebuild = true;
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
            UpdateRenderNodeSubtree(dirtyVisual, forceDeepSync);
            _retainedSubtreeSyncCount++;
            if (_renderListNeedsFullRebuild)
            {
                _lastRetainedSyncUsedFullRebuild = true;
                _lastSynchronizedDirtyRenderRoots.Clear();
                _lastSynchronizedDirtyRenderSpans.Clear();
                RebuildRetainedRenderList();
                return;
            }

            _lastSynchronizedDirtyRenderRoots.Add(dirtyVisual);
            if (_renderNodeIndices.TryGetValue(dirtyVisual, out var renderNodeIndex))
            {
                var node = _retainedRenderList[renderNodeIndex];
                _lastSynchronizedDirtyRenderSpans.Add(new DirtyRenderSpan(renderNodeIndex, node.SubtreeEndIndexExclusive));
            }
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
        StructureMismatch
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
        _dirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
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

    private void UpdateRenderNodeSubtree(UIElement dirtySubtreeRoot, bool forceDeepSync)
    {
        var shallowRejectReason = ShallowSyncRejectReason.None;
        if (!IsPartOfVisualTree(dirtySubtreeRoot))
        {
            _renderListNeedsFullRebuild = true;
            return;
        }

        if (!forceDeepSync && TryUpdateRenderNodeSubtreeShallow(dirtySubtreeRoot, out shallowRejectReason))
        {
            RefreshAncestorNodeSubtreeMetadata(dirtySubtreeRoot.VisualParent);
            return;
        }

        if (forceDeepSync)
        {
            var canDowngradeForcedDeep = CanSafelyDowngradeForcedDeepSync(dirtySubtreeRoot);
            if (canDowngradeForcedDeep && TryUpdateRenderNodeSubtreeShallow(dirtySubtreeRoot, out shallowRejectReason))
            {
                RefreshAncestorNodeSubtreeMetadata(dirtySubtreeRoot.VisualParent);
                return;
            }
        }

        RenderNode? parentNode = null;
        if (dirtySubtreeRoot.VisualParent != null &&
            _renderNodeIndices.TryGetValue(dirtySubtreeRoot.VisualParent, out var parentNodeIndex))
        {
            parentNode = _retainedRenderList[parentNodeIndex];
        }

        _ = UpdateRenderNodeSubtreeRecursive(dirtySubtreeRoot, parentNode);
        if (_renderListNeedsFullRebuild)
        {
            return;
        }

        RefreshAncestorNodeSubtreeMetadata(dirtySubtreeRoot.VisualParent);
    }

    private bool TryUpdateRenderNodeSubtreeShallow(UIElement visual, out ShallowSyncRejectReason rejectReason)
    {
        rejectReason = ShallowSyncRejectReason.None;
        if (!_renderNodeIndices.TryGetValue(visual, out var renderNodeIndex))
        {
            _renderListNeedsFullRebuild = true;
            rejectReason = ShallowSyncRejectReason.StructureMismatch;
            return false;
        }

        var previous = _retainedRenderList[renderNodeIndex];
        RenderNode? parentNode = null;
        if (visual.VisualParent != null &&
            _renderNodeIndices.TryGetValue(visual.VisualParent, out var parentNodeIndex))
        {
            parentNode = _retainedRenderList[parentNodeIndex];
        }

        var updated = CreateRenderNode(
            visual,
            previous.TraversalOrder,
            previous.Depth,
            previous.SubtreeEndIndexExclusive,
            parentNode);

        if (previous.RenderStateSignature != updated.RenderStateSignature ||
            previous.IsEffectivelyVisible != updated.IsEffectivelyVisible)
        {
            rejectReason = previous.IsEffectivelyVisible != updated.IsEffectivelyVisible
                ? ShallowSyncRejectReason.EffectiveVisibilityChanged
                : ShallowSyncRejectReason.RenderStateChanged;
            return false;
        }

        var metadata = CreateSubtreeMetadataForNode(updated);
        foreach (var child in visual.GetRetainedRenderChildren())
        {
            if (!_renderNodeIndices.TryGetValue(child, out var childNodeIndex))
            {
                _renderListNeedsFullRebuild = true;
                rejectReason = ShallowSyncRejectReason.StructureMismatch;
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
        RecordBoundsDelta(previous, updated);
        _retainedRenderList[renderNodeIndex] = updated;
        return true;
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

        foreach (var child in visual.GetRetainedRenderChildren())
        {
            var childMetadata = UpdateRenderNodeSubtreeRecursive(child, updated);
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
            RenderNode? parentNode = null;
            if (current.VisualParent != null &&
                _renderNodeIndices.TryGetValue(current.VisualParent, out var parentNodeIndex))
            {
                parentNode = _retainedRenderList[parentNodeIndex];
            }

            var updated = CreateRenderNode(
                current,
                previous.TraversalOrder,
                previous.Depth,
                previous.SubtreeEndIndexExclusive,
                parentNode);

            var metadata = CreateSubtreeMetadataForNode(updated);
            foreach (var child in current.GetRetainedRenderChildren())
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

            if (previous.RenderStateSignature == updated.RenderStateSignature &&
                previous.SubtreeRenderVersionStamp == updated.SubtreeRenderVersionStamp &&
                previous.SubtreeLayoutVersionStamp == updated.SubtreeLayoutVersionStamp &&
                previous.SubtreeVisualCount == updated.SubtreeVisualCount &&
                previous.SubtreeHighCostVisualCount == updated.SubtreeHighCostVisualCount &&
                previous.SubtreeEndIndexExclusive == updated.SubtreeEndIndexExclusive &&
                previous.HasSubtreeBoundsSnapshot == updated.HasSubtreeBoundsSnapshot &&
                (!previous.HasSubtreeBoundsSnapshot || AreRectsEqual(previous.SubtreeBoundsSnapshot, updated.SubtreeBoundsSnapshot)))
            {
                break;
            }
        }
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
            MixHash(17, visual.LayoutVersionStamp));
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
        var slot = visual.LayoutSlot;
        if (slot.Width <= 0f || slot.Height <= 0f)
        {
            bounds = slot;
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
            bounds = slot;
            return true;
        }

        var topLeft = Vector2.Transform(new Vector2(slot.X, slot.Y), transformFromThisToRoot);
        var topRight = Vector2.Transform(new Vector2(slot.X + slot.Width, slot.Y), transformFromThisToRoot);
        var bottomLeft = Vector2.Transform(new Vector2(slot.X, slot.Y + slot.Height), transformFromThisToRoot);
        var bottomRight = Vector2.Transform(new Vector2(slot.X + slot.Width, slot.Y + slot.Height), transformFromThisToRoot);
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
        if (hasLocalClip)
        {
            localRenderStateSignature = MixClipStepHash(localRenderStateSignature, localClipRect);
        }

        if (hasLocalTransform)
        {
            localRenderStateSignature = MixTransformStepHash(localRenderStateSignature, localTransform);
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
            int subtreeLayoutVersionStamp)
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
