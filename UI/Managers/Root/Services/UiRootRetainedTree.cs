using System;
using System.Buffers;
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

    private void CompactDirtyRenderQueueForSync()
    {
        _dirtyRenderRootsRequireDeepSync.Clear();
        if (_dirtyRenderQueue.Count <= 1)
        {
            return;
        }

        var drained = new List<UIElement>(_dirtyRenderQueue.Count);
        while (_dirtyRenderQueue.TryDequeue(out var visual))
        {
            drained.Add(visual);
        }

        _dirtyRenderSet.Clear();

        var coalesced = new List<UIElement>(drained.Count);
        for (var i = 0; i < drained.Count; i++)
        {
            var candidate = drained[i];
            if (!IsPartOfVisualTree(candidate))
            {
                continue;
            }

            var existingAncestor = FindAncestorIn(candidate, coalesced);
            if (existingAncestor != null)
            {
                _dirtyRenderRootsRequireDeepSync.Add(existingAncestor);
                continue;
            }

            for (var keptIndex = coalesced.Count - 1; keptIndex >= 0; keptIndex--)
            {
                if (IsDescendantOf(coalesced[keptIndex], candidate))
                {
                    coalesced.RemoveAt(keptIndex);
                    _dirtyRenderRootsRequireDeepSync.Add(candidate);
                }
            }

            coalesced.Add(candidate);
        }

        for (var i = 0; i < coalesced.Count; i++)
        {
            var visual = coalesced[i];
            _dirtyRenderSet.Add(visual);
            _dirtyRenderQueue.Enqueue(visual);
        }
    }

    private void ClearDirtyRenderQueue()
    {
        _dirtyRenderQueue.Clear();
        _dirtyRenderSet.Clear();
        _dirtyRenderRootsRequireDeepSync.Clear();
    }

    private void SynchronizeRetainedRenderList()
    {
        CompactDirtyRenderQueueForSync();

        if (_renderListNeedsFullRebuild || _retainedRenderList.Count == 0)
        {
            RebuildRetainedRenderList();
            return;
        }

        if (_dirtyRenderQueue.Count == 0)
        {
            return;
        }

        var processedRoots = new List<UIElement>(Math.Min(_dirtyRenderQueue.Count, 16));

        while (_dirtyRenderQueue.TryDequeue(out var dirtyVisual))
        {
            _dirtyRenderSet.Remove(dirtyVisual);
            if (IsDescendantOfAny(dirtyVisual, processedRoots))
            {
                continue;
            }

            var forceDeepSync = _dirtyRenderRootsRequireDeepSync.Contains(dirtyVisual);
            UpdateRenderNodeSubtree(dirtyVisual, forceDeepSync);
            if (_renderListNeedsFullRebuild)
            {
                RebuildRetainedRenderList();
                return;
            }

            processedRoots.Add(dirtyVisual);
        }
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
        _retainedRenderList.Clear();
        _renderNodeIndices.Clear();
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
        }
    }

    private RenderNode CreateRenderNode(
        UIElement visual,
        int traversalOrder,
        int depth,
        int subtreeEndIndexExclusive,
        RenderNode? parentNode)
    {
        var hasBounds = TryComputeBoundsFromParentState(visual, parentNode, out var bounds, out var hasTransformFromThisToRoot, out var transformFromThisToRoot);
        var steps = parentNode.HasValue
            ? CaptureRenderStateStepsFromParent(
                parentNode.Value,
                visual,
                out var hasTransformState,
                out var hasClipState,
                out var localRenderStateStartIndex,
                out var renderStateSignature,
                out var ancestorRenderStateSignature,
                out var localRenderStateSignature)
            : CaptureRenderStateSteps(
                visual,
                out hasTransformState,
                out hasClipState,
                out localRenderStateStartIndex,
                out renderStateSignature,
                out ancestorRenderStateSignature,
                out localRenderStateSignature);
        var isEffectivelyVisible = parentNode.HasValue
            ? parentNode.Value.IsEffectivelyVisible && visual.IsVisible
            : IsEffectivelyVisible(visual);
        var node = new RenderNode(
            visual,
            traversalOrder,
            depth,
            bounds,
            hasBounds,
            steps,
            localRenderStateStartIndex,
            renderStateSignature,
            ancestorRenderStateSignature,
            localRenderStateSignature,
            hasTransformFromThisToRoot,
            transformFromThisToRoot,
            hasTransformState,
            hasClipState,
            isEffectivelyVisible,
            subtreeEndIndexExclusive,
            hasBounds,
            bounds,
            1,
            IsHighCostVisual(visual) ? 1 : 0,
            MixHash(17, visual.RenderVersionStamp),
            MixHash(17, visual.LayoutVersionStamp));
        return node;
    }

    private static bool TryComputeBoundsFromParentState(
        UIElement visual,
        RenderNode? parentNode,
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

        var hasLocalTransform = visual.TryGetLocalRenderTransformSnapshot(out var localTransform);
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

    private static RenderStateStep[] CaptureRenderStateSteps(
        UIElement visual,
        out bool hasTransformState,
        out bool hasClipState,
        out int localRenderStateStartIndex,
        out int renderStateSignature,
        out int ancestorRenderStateSignature,
        out int localRenderStateSignature)
    {
        hasTransformState = false;
        hasClipState = false;
        localRenderStateStartIndex = 0;
        ancestorRenderStateSignature = 17;
        localRenderStateSignature = 17;

        var depth = 0;
        for (var current = visual; current != null; current = current.VisualParent)
        {
            depth++;
        }

        var pathStates = ArrayPool<LocalRenderStateInfo>.Shared.Rent(depth);
        try
        {
            var stepCount = 0;
            var pathIndex = depth - 1;
            for (var current = visual; current != null; current = current.VisualParent)
            {
                var hasClip = current.TryGetLocalClipSnapshot(out var clipRect);
                var hasTransform = current.TryGetLocalRenderTransformSnapshot(out var transform);
                if (hasClip)
                {
                    hasClipState = true;
                    stepCount++;
                }

                if (hasTransform)
                {
                    hasTransformState = true;
                    stepCount++;
                }

                pathStates[pathIndex] = new LocalRenderStateInfo(hasClip, clipRect, hasTransform, transform);
                pathIndex--;
            }

            var steps = new RenderStateStep[stepCount];
            var stepIndex = 0;
            var hash = 17;
            for (var i = 0; i < depth; i++)
            {
                var isLocalState = i == depth - 1;
                if (i == depth - 1)
                {
                    localRenderStateStartIndex = stepIndex;
                    ancestorRenderStateSignature = hash;
                }

                var state = pathStates[i];
                if (state.HasClip)
                {
                    var step = RenderStateStep.ForClip(state.ClipRect);
                    steps[stepIndex++] = step;
                    hash = MixHash(hash, (int)step.Kind);
                    hash = MixHash(hash, BitConverter.SingleToInt32Bits(step.ClipRect.X));
                    hash = MixHash(hash, BitConverter.SingleToInt32Bits(step.ClipRect.Y));
                    hash = MixHash(hash, BitConverter.SingleToInt32Bits(step.ClipRect.Width));
                    hash = MixHash(hash, BitConverter.SingleToInt32Bits(step.ClipRect.Height));
                    if (isLocalState)
                    {
                        localRenderStateSignature = MixClipStepHash(localRenderStateSignature, step.ClipRect);
                    }
                }

                if (state.HasTransform)
                {
                    var step = RenderStateStep.ForTransform(state.Transform);
                    steps[stepIndex++] = step;
                    hash = MixHash(hash, (int)step.Kind);
                    hash = MixHash(hash, step.Transform.GetHashCode());
                    if (isLocalState)
                    {
                        localRenderStateSignature = MixTransformStepHash(localRenderStateSignature, step.Transform);
                    }
                }
            }

            renderStateSignature = hash;
            return steps;
        }
        finally
        {
            ArrayPool<LocalRenderStateInfo>.Shared.Return(pathStates, clearArray: false);
        }
    }

    private static RenderStateStep[] CaptureRenderStateStepsFromParent(
        RenderNode parentNode,
        UIElement visual,
        out bool hasTransformState,
        out bool hasClipState,
        out int localRenderStateStartIndex,
        out int renderStateSignature,
        out int ancestorRenderStateSignature,
        out int localRenderStateSignature)
    {
        var hasLocalClip = visual.TryGetLocalClipSnapshot(out var localClipRect);
        var hasLocalTransform = visual.TryGetLocalRenderTransformSnapshot(out var localTransform);
        localRenderStateSignature = 17;
        var localStepCount = 0;
        if (hasLocalClip)
        {
            localStepCount++;
            localRenderStateSignature = MixClipStepHash(localRenderStateSignature, localClipRect);
        }

        if (hasLocalTransform)
        {
            localStepCount++;
            localRenderStateSignature = MixTransformStepHash(localRenderStateSignature, localTransform);
        }

        var parentSteps = parentNode.RenderStateSteps;
        var parentStepCount = parentSteps.Length;
        var steps = new RenderStateStep[parentStepCount + localStepCount];
        if (parentStepCount > 0)
        {
            Array.Copy(parentSteps, steps, parentStepCount);
        }

        var nextStepIndex = parentStepCount;
        if (hasLocalClip)
        {
            steps[nextStepIndex++] = RenderStateStep.ForClip(localClipRect);
        }

        if (hasLocalTransform)
        {
            steps[nextStepIndex] = RenderStateStep.ForTransform(localTransform);
        }

        ancestorRenderStateSignature = parentNode.RenderStateSignature;
        localRenderStateStartIndex = parentStepCount;
        renderStateSignature = MixHash(ancestorRenderStateSignature, localRenderStateSignature);
        hasTransformState = parentNode.HasTransformState || hasLocalTransform;
        hasClipState = parentNode.HasClipState || hasLocalClip;
        return steps;
    }

    private static int MixClipStepHash(int hash, LayoutRect clipRect)
    {
        hash = MixHash(hash, (int)RenderStateStepKind.Clip);
        hash = MixHash(hash, BitConverter.SingleToInt32Bits(clipRect.X));
        hash = MixHash(hash, BitConverter.SingleToInt32Bits(clipRect.Y));
        hash = MixHash(hash, BitConverter.SingleToInt32Bits(clipRect.Width));
        hash = MixHash(hash, BitConverter.SingleToInt32Bits(clipRect.Height));
        return hash;
    }

    private static int MixTransformStepHash(int hash, Matrix transform)
    {
        hash = MixHash(hash, (int)RenderStateStepKind.Transform);
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
            RenderStateStep[] renderStateSteps,
            int localRenderStateStartIndex,
            int renderStateSignature,
            int ancestorRenderStateSignature,
            int localRenderStateSignature,
            bool hasTransformFromThisToRoot,
            Matrix transformFromThisToRoot,
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
            AncestorRenderStateSignature = ancestorRenderStateSignature;
            LocalRenderStateSignature = localRenderStateSignature;
            HasTransformFromThisToRoot = hasTransformFromThisToRoot;
            TransformFromThisToRoot = transformFromThisToRoot;
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

        public int AncestorRenderStateSignature { get; }

        public int LocalRenderStateSignature { get; }

        public bool HasTransformFromThisToRoot { get; }

        public Matrix TransformFromThisToRoot { get; }

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
                AncestorRenderStateSignature,
                LocalRenderStateSignature,
                HasTransformFromThisToRoot,
                TransformFromThisToRoot,
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

    private readonly struct LocalRenderStateInfo
    {
        public LocalRenderStateInfo(bool hasClip, LayoutRect clipRect, bool hasTransform, Matrix transform)
        {
            HasClip = hasClip;
            ClipRect = clipRect;
            HasTransform = hasTransform;
            Transform = transform;
        }

        public bool HasClip { get; }

        public LayoutRect ClipRect { get; }

        public bool HasTransform { get; }

        public Matrix Transform { get; }
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
