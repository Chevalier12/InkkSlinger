using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal sealed class CompositionTreeIndex
{
    private RetainedCompositionGraph _graph = RetainedCompositionGraph.Empty;

    public RetainedCompositionGraph Graph => _graph;

    public int RebuildCount { get; private set; }

    public double LastBuildMilliseconds { get; private set; }

    public bool TryUpdateMetadata(UIElement visual, RenderInvalidationKind kind)
    {
        if (!_graph.NodeIndices.TryGetValue(visual, out var nodeIndex) ||
            (uint)nodeIndex >= (uint)_graph.Nodes.Count)
        {
            return false;
        }

        var nodes = CopyNodes(_graph.Nodes);
        if (kind == RenderInvalidationKind.Visibility)
        {
            UpdateVisibilitySubtree(nodes, nodeIndex);
            _graph = new RetainedCompositionGraph(nodes, _graph.NodeIndices);
            return true;
        }

        if (kind is RenderInvalidationKind.Transform or RenderInvalidationKind.Clip or RenderInvalidationKind.Bounds)
        {
            RefreshSubtreeMetadataAndBounds(nodes, nodeIndex, refreshCacheKeys: kind == RenderInvalidationKind.Bounds);
            RefreshAncestorSubtreeBounds(nodes, nodes[nodeIndex].ParentIndex);
            _graph = new RetainedCompositionGraph(nodes, _graph.NodeIndices);
            return true;
        }

        nodes[nodeIndex] = CaptureMetadata(nodes[nodeIndex]);
        _graph = new RetainedCompositionGraph(nodes, _graph.NodeIndices);
        return true;
    }

    public void BuildFromRetainedList(IReadOnlyList<UiRoot.RetainedRenderController.RenderNode> retainedNodes)
    {
        var buildStart = Stopwatch.GetTimestamp();
        _graph = CreateFromRetainedList(retainedNodes);
        RebuildCount++;
        LastBuildMilliseconds = Stopwatch.GetElapsedTime(buildStart).TotalMilliseconds;
    }

    public void BuildFromVisualTree(UIElement root)
    {
        var buildStart = Stopwatch.GetTimestamp();
        _graph = CreateFromVisualTree(root);
        RebuildCount++;
        LastBuildMilliseconds = Stopwatch.GetElapsedTime(buildStart).TotalMilliseconds;
    }

    public IReadOnlyList<UIElement> GetVisualOrderSnapshot()
    {
        var visuals = new List<UIElement>(_graph.NodeCount);
        var nodes = _graph.Nodes;
        for (var i = 0; i < nodes.Count; i++)
        {
            visuals.Add(nodes[i].Visual);
        }

        return visuals;
    }

    private static RetainedCompositionGraph CreateFromRetainedList(IReadOnlyList<UiRoot.RetainedRenderController.RenderNode> retainedNodes)
    {
        if (retainedNodes.Count == 0)
        {
            return RetainedCompositionGraph.Empty;
        }

        var nodes = new RetainedCompositionNode[retainedNodes.Count];
        var indices = new Dictionary<UIElement, int>(retainedNodes.Count, ReferenceEqualityComparer.Instance);
        var parentByDepth = new List<int>();

        for (var i = 0; i < retainedNodes.Count; i++)
        {
            var retained = retainedNodes[i];
            while (parentByDepth.Count > retained.Depth)
            {
                parentByDepth.RemoveAt(parentByDepth.Count - 1);
            }

            var parentIndex = retained.Depth > 0 && parentByDepth.Count >= retained.Depth
                ? parentByDepth[retained.Depth - 1]
                : -1;
            var node = CreateNodeFromRetainedNode(retained, parentIndex);
            nodes[i] = node;
            indices[retained.Visual] = i;

            if (parentIndex >= 0)
            {
                var parent = nodes[parentIndex];
                nodes[parentIndex] = parent with
                {
                    FirstChildIndex = parent.FirstChildIndex < 0 ? i : parent.FirstChildIndex,
                    ChildCount = parent.ChildCount + 1
                };
            }

            if (parentByDepth.Count == retained.Depth)
            {
                parentByDepth.Add(i);
            }
            else
            {
                parentByDepth[retained.Depth] = i;
            }
        }

        RefreshSubtreeBoundsFromChildren(nodes);
        return new RetainedCompositionGraph(nodes, indices);
    }

    private static RetainedCompositionGraph CreateFromVisualTree(UIElement root)
    {
        var nodes = new List<RetainedCompositionNode>();
        var indices = new Dictionary<UIElement, int>(ReferenceEqualityComparer.Instance);
        _ = BuildVisualSubtree(root, parentIndex: -1, depth: 0, nodes, indices);
        return new RetainedCompositionGraph(nodes.ToArray(), indices);
    }

    private static LayoutRect BuildVisualSubtree(
        UIElement visual,
        int parentIndex,
        int depth,
        List<RetainedCompositionNode> nodes,
        Dictionary<UIElement, int> indices)
    {
        var nodeIndex = nodes.Count;
        var hasBounds = visual.TryGetRenderBoundsInRootSpace(out var subtreeBounds);
        var node = CreateNodeFromVisual(visual, parentIndex, depth, nodeIndex, hasBounds, subtreeBounds);
        nodes.Add(node);
        indices[visual] = nodeIndex;

        foreach (var child in visual.GetRetainedRenderChildren())
        {
            var childIndex = nodes.Count;
            var childBounds = BuildVisualSubtree(child, nodeIndex, depth + 1, nodes, indices);
            var parent = nodes[nodeIndex];
            nodes[nodeIndex] = parent with
            {
                FirstChildIndex = parent.FirstChildIndex < 0 ? childIndex : parent.FirstChildIndex,
                ChildCount = parent.ChildCount + 1
            };

            if (hasBounds)
            {
                subtreeBounds = UiRoot.RetainedRenderController.Union(subtreeBounds, childBounds);
            }
            else
            {
                subtreeBounds = childBounds;
                hasBounds = true;
            }
        }

        var updatedNode = nodes[nodeIndex] with
        {
            HasSubtreeBounds = hasBounds,
            SubtreeBounds = subtreeBounds,
            SubtreeEndIndexExclusive = nodes.Count
        };
        nodes[nodeIndex] = updatedNode with
        {
            CacheKey = CreateSubtreeCacheKey(updatedNode, nodes)
        };
        return subtreeBounds;
    }

    private static RetainedCompositionNode CreateNodeFromRetainedNode(
        UiRoot.RetainedRenderController.RenderNode retained,
        int parentIndex)
    {
        var visual = retained.Visual;
        var hasBounds = visual.TryGetRenderBoundsInRootSpace(out var bounds);
        var hasLocalTransform = visual.TryGetLocalRenderTransformSnapshot(out var localTransform);
        var hasLocalClip = visual.TryGetLocalClipSnapshot(out var localClip);
        var opacity = visual.Opacity;
        var isEffectivelyVisible = retained.IsEffectivelyVisible;
        return new RetainedCompositionNode(
            visual,
            parentIndex,
            FirstChildIndex: -1,
            ChildCount: 0,
            retained.TraversalOrder,
            retained.SubtreeEndIndexExclusive,
            retained.Depth,
            retained.TraversalOrder,
            hasBounds,
            bounds,
            hasBounds,
            bounds,
            hasLocalTransform,
            localTransform,
            hasLocalClip,
            localClip,
            opacity,
            isEffectivelyVisible,
            visual.RenderVersionStamp,
            CreateMetadataVersion(hasLocalTransform, localTransform, hasLocalClip, localClip, opacity, isEffectivelyVisible),
            visual.RetainedCompositionCacheMode,
            CreateSelfCacheKey(visual));
    }

    private static void RefreshSubtreeBoundsFromChildren(RetainedCompositionNode[] nodes)
    {
        for (var nodeIndex = nodes.Length - 1; nodeIndex >= 0; nodeIndex--)
        {
            var node = nodes[nodeIndex];
            var hasSubtreeBounds = node.HasBounds;
            var subtreeBounds = node.Bounds;

            for (var childIndex = node.FirstChildIndex; childIndex >= 0 && childIndex < node.SubtreeEndIndexExclusive;)
            {
                var child = nodes[childIndex];
                if (child.HasSubtreeBounds)
                {
                    subtreeBounds = hasSubtreeBounds
                        ? UiRoot.RetainedRenderController.Union(subtreeBounds, child.SubtreeBounds)
                        : child.SubtreeBounds;
                    hasSubtreeBounds = true;
                }

                childIndex = child.SubtreeEndIndexExclusive;
            }

            nodes[nodeIndex] = node with
            {
                HasSubtreeBounds = hasSubtreeBounds,
                SubtreeBounds = subtreeBounds,
                CacheKey = CreateSubtreeCacheKey(node, nodes)
            };
        }
    }

    private static RetainedCompositionNode CreateNodeFromVisual(
        UIElement visual,
        int parentIndex,
        int depth,
        int drawOrderIndex,
        bool hasSubtreeBounds,
        LayoutRect subtreeBounds)
    {
        var hasLocalTransform = visual.TryGetLocalRenderTransformSnapshot(out var localTransform);
        var hasLocalClip = visual.TryGetLocalClipSnapshot(out var localClip);
        return new RetainedCompositionNode(
            visual,
            parentIndex,
            FirstChildIndex: -1,
            ChildCount: 0,
            drawOrderIndex,
            SubtreeEndIndexExclusive: drawOrderIndex + 1,
            depth,
            drawOrderIndex,
            hasSubtreeBounds,
            subtreeBounds,
            hasSubtreeBounds,
            subtreeBounds,
            hasLocalTransform,
            localTransform,
            hasLocalClip,
            localClip,
            visual.Opacity,
            IsEffectivelyVisible(visual),
            visual.RenderVersionStamp,
            CreateMetadataVersion(hasLocalTransform, localTransform, hasLocalClip, localClip, visual.Opacity, visual.IsVisible),
            visual.RetainedCompositionCacheMode,
            CreateSelfCacheKey(visual));
    }

    private static bool IsEffectivelyVisible(UIElement visual)
    {
        for (var current = visual; current != null; current = current.VisualParent)
        {
            if (!current.IsVisible)
            {
                return false;
            }
        }

        return true;
    }

    private static void UpdateVisibilitySubtree(RetainedCompositionNode[] nodes, int nodeIndex)
    {
        var node = nodes[nodeIndex];
        nodes[nodeIndex] = CaptureMetadata(node);

        for (var childIndex = node.FirstChildIndex; childIndex >= 0 && childIndex < node.SubtreeEndIndexExclusive;)
        {
            UpdateVisibilitySubtree(nodes, childIndex);
            childIndex = nodes[childIndex].SubtreeEndIndexExclusive;
        }
    }

    private static BoundsSnapshot RefreshSubtreeMetadataAndBounds(
        RetainedCompositionNode[] nodes,
        int nodeIndex,
        bool refreshCacheKeys)
    {
        var node = CaptureMetadata(nodes[nodeIndex]);
        var hasSelfBounds = node.Visual.TryGetRenderBoundsInRootSpace(out var selfBounds);
        var hasSubtreeBounds = hasSelfBounds;
        var subtreeBounds = selfBounds;

        for (var childIndex = node.FirstChildIndex; childIndex >= 0 && childIndex < node.SubtreeEndIndexExclusive;)
        {
            var childBounds = RefreshSubtreeMetadataAndBounds(nodes, childIndex, refreshCacheKeys);
            if (childBounds.HasBounds)
            {
                subtreeBounds = hasSubtreeBounds
                    ? UiRoot.RetainedRenderController.Union(subtreeBounds, childBounds.Bounds)
                    : childBounds.Bounds;
                hasSubtreeBounds = true;
            }

            childIndex = nodes[childIndex].SubtreeEndIndexExclusive;
        }

        nodes[nodeIndex] = node with
        {
            HasBounds = hasSelfBounds,
            Bounds = selfBounds,
            HasSubtreeBounds = hasSubtreeBounds,
            SubtreeBounds = subtreeBounds,
            CacheKey = refreshCacheKeys ? CreateSubtreeCacheKey(node, nodes) : node.CacheKey
        };

        return new BoundsSnapshot(hasSubtreeBounds, subtreeBounds);
    }

    private static void RefreshAncestorSubtreeBounds(RetainedCompositionNode[] nodes, int nodeIndex)
    {
        for (var currentIndex = nodeIndex; currentIndex >= 0;)
        {
            var current = nodes[currentIndex];
            var hasSubtreeBounds = current.HasBounds;
            var subtreeBounds = current.Bounds;

            for (var childIndex = current.FirstChildIndex; childIndex >= 0 && childIndex < current.SubtreeEndIndexExclusive;)
            {
                var child = nodes[childIndex];
                if (child.HasSubtreeBounds)
                {
                    subtreeBounds = hasSubtreeBounds
                        ? UiRoot.RetainedRenderController.Union(subtreeBounds, child.SubtreeBounds)
                        : child.SubtreeBounds;
                    hasSubtreeBounds = true;
                }

                childIndex = child.SubtreeEndIndexExclusive;
            }

            nodes[currentIndex] = current with
            {
                HasSubtreeBounds = hasSubtreeBounds,
                SubtreeBounds = subtreeBounds
            };
            currentIndex = current.ParentIndex;
        }
    }

    private static RetainedCompositionNode CaptureMetadata(RetainedCompositionNode node)
    {
        var visual = node.Visual;
        var hasLocalTransform = visual.TryGetLocalRenderTransformSnapshot(out var localTransform);
        var hasLocalClip = visual.TryGetLocalClipSnapshot(out var localClip);
        var opacity = visual.Opacity;
        var isEffectivelyVisible = IsEffectivelyVisible(visual);

        return node with
        {
            HasLocalTransform = hasLocalTransform,
            LocalTransform = localTransform,
            HasLocalClip = hasLocalClip,
            LocalClip = localClip,
            Opacity = opacity,
            IsEffectivelyVisible = isEffectivelyVisible,
            MetadataVersion = CreateMetadataVersion(
                hasLocalTransform,
                localTransform,
                hasLocalClip,
                localClip,
                opacity,
                isEffectivelyVisible)
        };
    }

    private static RetainedCompositionCacheKey CreateSelfCacheKey(UIElement visual)
    {
        var hasBounds = visual.TryGetLocalRenderBoundsSnapshot(out var bounds);
        return new RetainedCompositionCacheKey(
            visual.RenderVersionStamp,
            CreateStructureVersion(visual, childCount: 0, childStructureVersion: 0),
            hasBounds,
            bounds,
            DeviceWidth: 0,
            DeviceHeight: 0);
    }

    private static RetainedCompositionCacheKey CreateSubtreeCacheKey(
        RetainedCompositionNode node,
        IReadOnlyList<RetainedCompositionNode> nodes)
    {
        var subtreeContentVersion = node.Visual.RenderVersionStamp;
        var childStructureVersion = 0;
        var childCount = 0;

        for (var childIndex = node.FirstChildIndex; childIndex >= 0 && childIndex < node.SubtreeEndIndexExclusive;)
        {
            var child = nodes[childIndex];
            subtreeContentVersion = MixHash(subtreeContentVersion, child.CacheKey.SubtreeContentVersion);
            childStructureVersion = MixHash(childStructureVersion, child.CacheKey.StructureVersion);
            childCount++;
            childIndex = child.SubtreeEndIndexExclusive;
        }

        var hasBounds = node.Visual.TryGetLocalRenderBoundsSnapshot(out var bounds);
        return new RetainedCompositionCacheKey(
            subtreeContentVersion,
            CreateStructureVersion(node.Visual, childCount, childStructureVersion),
            hasBounds,
            bounds,
            DeviceWidth: 0,
            DeviceHeight: 0);
    }

    private static int CreateStructureVersion(UIElement visual, int childCount, int childStructureVersion)
    {
        var hash = MixHash(17, visual.GetType().GetHashCode());
        hash = MixHash(hash, childCount);
        return MixHash(hash, childStructureVersion);
    }

    private static RetainedCompositionNode[] CopyNodes(IReadOnlyList<RetainedCompositionNode> nodes)
    {
        var copy = new RetainedCompositionNode[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            copy[i] = nodes[i];
        }

        return copy;
    }

    private static int CreateMetadataVersion(
        bool hasLocalTransform,
        Matrix localTransform,
        bool hasLocalClip,
        LayoutRect localClip,
        float opacity,
        bool isVisible)
    {
        var hash = 17;
        hash = MixHash(hash, hasLocalTransform ? 1 : 0);
        if (hasLocalTransform)
        {
            hash = MixHash(hash, localTransform.GetHashCode());
        }

        hash = MixHash(hash, hasLocalClip ? 1 : 0);
        if (hasLocalClip)
        {
            hash = MixHash(hash, BitConverter.SingleToInt32Bits(localClip.X));
            hash = MixHash(hash, BitConverter.SingleToInt32Bits(localClip.Y));
            hash = MixHash(hash, BitConverter.SingleToInt32Bits(localClip.Width));
            hash = MixHash(hash, BitConverter.SingleToInt32Bits(localClip.Height));
        }

        hash = MixHash(hash, BitConverter.SingleToInt32Bits(opacity));
        hash = MixHash(hash, isVisible ? 1 : 0);
        return hash;
    }

    private static int MixHash(int hash, int value)
    {
        unchecked
        {
            return (hash * 31) ^ value;
        }
    }

    private readonly record struct BoundsSnapshot(bool HasBounds, LayoutRect Bounds);
}
