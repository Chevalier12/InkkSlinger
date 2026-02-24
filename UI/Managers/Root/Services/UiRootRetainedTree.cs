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

    private static bool AreViewportsEqual(Viewport left, Viewport right)
    {
        return left.X == right.X &&
               left.Y == right.Y &&
               left.Width == right.Width &&
               left.Height == right.Height;
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
