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
        if (TryUpdateSparseLeafMetadataBatch(new[] { new KeyValuePair<UIElement, RenderInvalidationKind>(visual, kind) }))
        {
            return true;
        }

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
            if (kind == RenderInvalidationKind.Transform &&
                RetainedCompositionLayerBoundary.IsTransformStableLayer(nodes[nodeIndex].Visual) &&
                TryUpdateTransformStableLayerMetadataByDelta(nodes, nodeIndex))
            {
                RefreshAncestorSubtreeBounds(nodes, nodes[nodeIndex].ParentIndex);
                _graph = new RetainedCompositionGraph(nodes, _graph.NodeIndices);
                return true;
            }

            if (kind is RenderInvalidationKind.Transform or RenderInvalidationKind.Clip &&
                RetainedCompositionLayerBoundary.IsTransformStableLayer(nodes[nodeIndex].Visual))
            {
                RefreshTransformStableLayerMetadata(nodes, nodeIndex);
            }
            else
            {
                RefreshSubtreeMetadataAndBounds(nodes, nodeIndex, refreshCacheKeys: kind == RenderInvalidationKind.Bounds);
            }

            RefreshAncestorSubtreeBounds(nodes, nodes[nodeIndex].ParentIndex);
            _graph = new RetainedCompositionGraph(nodes, _graph.NodeIndices);
            return true;
        }

        nodes[nodeIndex] = CaptureMetadata(nodes[nodeIndex]);
        _graph = new RetainedCompositionGraph(nodes, _graph.NodeIndices);
        return true;
    }

    public bool TryUpdateMetadataBatch(IReadOnlyList<KeyValuePair<UIElement, RenderInvalidationKind>> updates)
    {
        if (updates.Count == 0)
        {
            return true;
        }

        if (TryUpdateSparseLeafMetadataBatch(updates))
        {
            return true;
        }

        var oldNodes = _graph.Nodes;
        var updateNodes = new int[updates.Count];
        for (var i = 0; i < updates.Count; i++)
        {
            if (!_graph.NodeIndices.TryGetValue(updates[i].Key, out var nodeIndex) ||
                (uint)nodeIndex >= (uint)oldNodes.Count)
            {
                return false;
            }

            updateNodes[i] = nodeIndex;
        }

        var nodes = CopyNodes(oldNodes);
        var refreshAncestors = new bool[nodes.Length];
        for (var i = 0; i < updates.Count; i++)
        {
            var nodeIndex = updateNodes[i];
            var kind = updates[i].Value;
            if (kind == RenderInvalidationKind.Visibility)
            {
                UpdateVisibilitySubtree(nodes, nodeIndex);
                MarkAncestorsForRefresh(nodes, nodes[nodeIndex].ParentIndex, refreshAncestors);
                continue;
            }

            if (kind is RenderInvalidationKind.Transform or RenderInvalidationKind.Clip or RenderInvalidationKind.Bounds)
            {
                if (kind == RenderInvalidationKind.Transform &&
                    RetainedCompositionLayerBoundary.IsTransformStableLayer(nodes[nodeIndex].Visual) &&
                    TryUpdateTransformStableLayerMetadataByDelta(nodes, nodeIndex))
                {
                    MarkAncestorsForRefresh(nodes, nodes[nodeIndex].ParentIndex, refreshAncestors);
                    continue;
                }

                if (kind is RenderInvalidationKind.Transform or RenderInvalidationKind.Clip &&
                    RetainedCompositionLayerBoundary.IsTransformStableLayer(nodes[nodeIndex].Visual))
                {
                    RefreshTransformStableLayerMetadata(nodes, nodeIndex);
                }
                else
                {
                    RefreshSubtreeMetadataAndBounds(nodes, nodeIndex, refreshCacheKeys: kind == RenderInvalidationKind.Bounds);
                }

                MarkAncestorsForRefresh(nodes, nodes[nodeIndex].ParentIndex, refreshAncestors);
                continue;
            }

            nodes[nodeIndex] = CaptureMetadata(nodes[nodeIndex]);
            MarkAncestorsForRefresh(nodes, nodes[nodeIndex].ParentIndex, refreshAncestors);
        }

        RefreshMarkedAncestors(nodes, refreshAncestors);
        _graph = new RetainedCompositionGraph(nodes, _graph.NodeIndices);
        return true;
    }

    private bool TryUpdateSparseLeafMetadataBatch(IReadOnlyList<KeyValuePair<UIElement, RenderInvalidationKind>> updates)
    {
        var nodes = _graph.Nodes as RetainedCompositionNode[];
        if (nodes == null)
        {
            return false;
        }

        var nodeIndices = new int[updates.Count];
        for (var i = 0; i < updates.Count; i++)
        {
            var kind = updates[i].Value;
            if (kind is not (RenderInvalidationKind.Bounds or RenderInvalidationKind.Transform or RenderInvalidationKind.Clip or RenderInvalidationKind.Opacity) ||
                !_graph.NodeIndices.TryGetValue(updates[i].Key, out var nodeIndex) ||
                (uint)nodeIndex >= (uint)nodes.Length ||
                nodes[nodeIndex].SubtreeEndIndexExclusive - nodeIndex > 4096 ||
                RetainedCompositionLayerBoundary.IsTransformStableLayer(nodes[nodeIndex].Visual))
            {
                return false;
            }

            nodeIndices[i] = nodeIndex;
        }

        var touchedNodeIndices = new List<int>(updates.Count * 4);
        for (var i = 0; i < updates.Count; i++)
        {
            var nodeIndex = nodeIndices[i];
            if (nodes[nodeIndex].ChildCount == 0 ||
                updates[i].Value == RenderInvalidationKind.Opacity)
            {
                nodes[nodeIndex] = CaptureSparseLeafMetadata(nodes[nodeIndex], updates[i].Value);
            }
            else
            {
                _ = RefreshSubtreeMetadataAndBounds(
                    nodes,
                    nodeIndex,
                    refreshCacheKeys: updates[i].Value == RenderInvalidationKind.Bounds);
            }

            AddUniqueNodeIndex(touchedNodeIndices, nodeIndex);
            AddAncestorNodeIndices(nodes, nodes[nodeIndex].ParentIndex, touchedNodeIndices);
        }

        touchedNodeIndices.Sort((left, right) => nodes[right].Depth.CompareTo(nodes[left].Depth));
        for (var i = 0; i < touchedNodeIndices.Count; i++)
        {
            if (!ContainsNodeIndex(nodeIndices, touchedNodeIndices[i]))
            {
                RefreshNodeSubtreeBounds(nodes, touchedNodeIndices[i]);
            }
        }

        return true;
    }

    public void BuildFromRetainedList(IReadOnlyList<UiRoot.RetainedRenderController.RenderNode> retainedNodes)
    {
        var buildStart = Stopwatch.GetTimestamp();
        _graph = CreateFromRetainedList(retainedNodes);
        RebuildCount++;
        LastBuildMilliseconds = Stopwatch.GetElapsedTime(buildStart).TotalMilliseconds;
    }

    public bool TryReplaceRetainedSubtree(
        IReadOnlyList<UiRoot.RetainedRenderController.RenderNode> retainedNodes,
        int subtreeStartIndex,
        int previousSubtreeEndIndexExclusive)
    {
        var oldNodes = _graph.Nodes;
        if (subtreeStartIndex < 0 ||
            previousSubtreeEndIndexExclusive <= subtreeStartIndex ||
            subtreeStartIndex >= oldNodes.Count ||
            previousSubtreeEndIndexExclusive > oldNodes.Count ||
            subtreeStartIndex >= retainedNodes.Count)
        {
            return false;
        }

        var retainedRoot = retainedNodes[subtreeStartIndex];
        if (!ReferenceEquals(oldNodes[subtreeStartIndex].Visual, retainedRoot.Visual))
        {
            return false;
        }

        var newSubtreeEndIndexExclusive = retainedRoot.SubtreeEndIndexExclusive;
        if (newSubtreeEndIndexExclusive <= subtreeStartIndex ||
            newSubtreeEndIndexExclusive > retainedNodes.Count)
        {
            return false;
        }

        var newCount = oldNodes.Count + (newSubtreeEndIndexExclusive - previousSubtreeEndIndexExclusive);
        if (newCount != retainedNodes.Count)
        {
            return false;
        }

        if (newSubtreeEndIndexExclusive == previousSubtreeEndIndexExclusive &&
            TryRefreshSameShapeRetainedSubtree(retainedNodes, subtreeStartIndex, newSubtreeEndIndexExclusive))
        {
            return true;
        }

        var nodes = new RetainedCompositionNode[newCount];
        for (var i = 0; i < subtreeStartIndex; i++)
        {
            nodes[i] = UpdatePreservedNodeForSubtreeReplacement(
                oldNodes[i],
                i,
                subtreeStartIndex,
                previousSubtreeEndIndexExclusive,
                newSubtreeEndIndexExclusive);
        }

        BuildReplacementSubtreeNodes(
            retainedNodes,
            nodes,
            subtreeStartIndex,
            newSubtreeEndIndexExclusive,
            oldNodes[subtreeStartIndex].ParentIndex);

        var delta = newSubtreeEndIndexExclusive - previousSubtreeEndIndexExclusive;
        for (var oldIndex = previousSubtreeEndIndexExclusive; oldIndex < oldNodes.Count; oldIndex++)
        {
            var newIndex = oldIndex + delta;
            nodes[newIndex] = UpdatePreservedNodeForSubtreeReplacement(
                oldNodes[oldIndex],
                newIndex,
                subtreeStartIndex,
                previousSubtreeEndIndexExclusive,
                newSubtreeEndIndexExclusive);
        }

        RefreshSubtreeBoundsAndCacheKeys(nodes, subtreeStartIndex, newSubtreeEndIndexExclusive);
        RefreshAncestorSubtreeBoundsAndCacheKeys(nodes, nodes[subtreeStartIndex].ParentIndex);
        _graph = new RetainedCompositionGraph(nodes, CreateNodeIndexMap(nodes));
        return true;
    }

    public bool TryRefreshRetainedNodes(
        IReadOnlyList<UiRoot.RetainedRenderController.RenderNode> retainedNodes,
        IReadOnlyList<int> retainedNodeIndices)
    {
        var oldNodes = _graph.Nodes;
        if (retainedNodeIndices.Count == 0 ||
            oldNodes.Count != retainedNodes.Count)
        {
            return false;
        }

        var refreshCacheKeys = HasAnyCacheBoundary(oldNodes);
        var nodes = CopyNodes(oldNodes);
        var refreshNodes = new bool[nodes.Length];
        var changed = false;
        for (var i = 0; i < retainedNodeIndices.Count; i++)
        {
            var nodeIndex = retainedNodeIndices[i];
            if ((uint)nodeIndex >= (uint)nodes.Length ||
                !ReferenceEquals(nodes[nodeIndex].Visual, retainedNodes[nodeIndex].Visual))
            {
                return false;
            }

            if (TryRefreshSameShapeNodeFromRetained(
                    nodes[nodeIndex],
                    retainedNodes[nodeIndex],
                    refreshCacheKey: false,
                    out var refreshed))
            {
                nodes[nodeIndex] = refreshed;
                refreshNodes[nodeIndex] = true;
                changed = true;
            }

            for (var ancestorIndex = nodes[nodeIndex].ParentIndex; ancestorIndex >= 0; ancestorIndex = nodes[ancestorIndex].ParentIndex)
            {
                refreshNodes[ancestorIndex] = true;
            }
        }

        if (!changed)
        {
            return true;
        }

        for (var nodeIndex = nodes.Length - 1; nodeIndex >= 0; nodeIndex--)
        {
            if (!refreshNodes[nodeIndex])
            {
                continue;
            }

            if (refreshCacheKeys)
            {
                RefreshNodeSubtreeBoundsAndCacheKey(nodes, nodeIndex);
            }
            else
            {
                RefreshNodeSubtreeBounds(nodes, nodeIndex);
            }
        }

        _graph = new RetainedCompositionGraph(nodes, _graph.NodeIndices);
        return true;
    }

    private bool TryRefreshSameShapeRetainedSubtree(
        IReadOnlyList<UiRoot.RetainedRenderController.RenderNode> retainedNodes,
        int subtreeStartIndex,
        int subtreeEndIndexExclusive)
    {
        var oldNodes = _graph.Nodes;
        for (var i = subtreeStartIndex; i < subtreeEndIndexExclusive; i++)
        {
            if (!ReferenceEquals(oldNodes[i].Visual, retainedNodes[i].Visual))
            {
                return false;
            }
        }

        var refreshCacheKeys = HasAnyCacheBoundary(oldNodes);
        var nodes = CopyNodes(oldNodes);
        for (var i = subtreeStartIndex; i < subtreeEndIndexExclusive; i++)
        {
            if (TryRefreshSameShapeNodeFromRetained(nodes[i], retainedNodes[i], refreshCacheKeys, out var refreshed))
            {
                nodes[i] = refreshed;
            }
        }

        if (refreshCacheKeys)
        {
            RefreshSubtreeBoundsAndCacheKeys(nodes, subtreeStartIndex, subtreeEndIndexExclusive);
            RefreshAncestorSubtreeBoundsAndCacheKeys(nodes, nodes[subtreeStartIndex].ParentIndex);
        }
        else
        {
            RefreshAncestorSubtreeBounds(nodes, nodes[subtreeStartIndex].ParentIndex);
        }

        _graph = new RetainedCompositionGraph(nodes, _graph.NodeIndices);
        return true;
    }

    private static bool HasAnyCacheBoundary(IReadOnlyList<RetainedCompositionNode> nodes)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].CacheMode != RetainedCompositionCacheMode.None)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryRefreshSameShapeNodeFromRetained(
        RetainedCompositionNode node,
        UiRoot.RetainedRenderController.RenderNode retained,
        bool refreshCacheKey,
        out RetainedCompositionNode refreshed)
    {
        var visual = retained.Visual;
        var opacity = visual.Opacity;
        var metadataVersion = CreateMetadataVersion(
            retained.HasLocalTransform,
            retained.LocalTransform,
            retained.HasLocalClip,
            retained.LocalClipRect,
            opacity,
            retained.IsEffectivelyVisible);
        var cacheMode = RetainedCompositionLayerBoundary.ResolveCacheMode(visual);
        var cacheKey = refreshCacheKey ? CreateSelfCacheKey(visual) : node.CacheKey;

        if (node.HasBounds == retained.HasBoundsSnapshot &&
            (!retained.HasBoundsSnapshot || AreRectsEqual(node.Bounds, retained.BoundsSnapshot)) &&
            node.HasSubtreeBounds == retained.HasSubtreeBoundsSnapshot &&
            (!retained.HasSubtreeBoundsSnapshot || AreRectsEqual(node.SubtreeBounds, retained.SubtreeBoundsSnapshot)) &&
            node.HasLocalTransform == retained.HasLocalTransform &&
            (!retained.HasLocalTransform || AreMatricesEqual(node.LocalTransform, retained.LocalTransform)) &&
            node.HasLocalClip == retained.HasLocalClip &&
            (!retained.HasLocalClip || AreRectsEqual(node.LocalClip, retained.LocalClipRect)) &&
            MathF.Abs(node.Opacity - opacity) <= 0.0001f &&
            node.IsEffectivelyVisible == retained.IsEffectivelyVisible &&
            node.ContentVersion == visual.RenderVersionStamp &&
            node.MetadataVersion == metadataVersion &&
            node.CacheMode == cacheMode &&
            node.CacheKey.Equals(cacheKey))
        {
            refreshed = node;
            return false;
        }

        refreshed = node with
        {
            HasBounds = retained.HasBoundsSnapshot,
            Bounds = retained.BoundsSnapshot,
            HasSubtreeBounds = retained.HasSubtreeBoundsSnapshot,
            SubtreeBounds = retained.SubtreeBoundsSnapshot,
            HasLocalTransform = retained.HasLocalTransform,
            LocalTransform = retained.LocalTransform,
            HasLocalClip = retained.HasLocalClip,
            LocalClip = retained.LocalClipRect,
            Opacity = opacity,
            IsEffectivelyVisible = retained.IsEffectivelyVisible,
            ContentVersion = visual.RenderVersionStamp,
            MetadataVersion = metadataVersion,
            CacheMode = cacheMode,
            CacheKey = cacheKey
        };
        return true;
    }

    private static bool TryUpdateTransformStableLayerMetadataByDelta(RetainedCompositionNode[] nodes, int nodeIndex)
    {
        var previousRoot = nodes[nodeIndex];
        var currentRoot = CaptureMetadata(previousRoot);
        var previousTransform = previousRoot.HasLocalTransform ? previousRoot.LocalTransform : Matrix.Identity;
        var currentTransform = currentRoot.HasLocalTransform ? currentRoot.LocalTransform : Matrix.Identity;
        var transformDelta = Matrix.Invert(previousTransform) * currentTransform;

        if (previousRoot.HasBounds)
        {
            var transformedRootBounds = TransformBounds(previousRoot.Bounds, transformDelta);
            if (currentRoot.HasBounds != previousRoot.HasBounds ||
                !AreRectsEqual(transformedRootBounds, currentRoot.Bounds))
            {
                return false;
            }
        }
        else if (currentRoot.HasBounds)
        {
            return false;
        }

        for (var i = nodeIndex; i < previousRoot.SubtreeEndIndexExclusive; i++)
        {
            var node = nodes[i];
            if (node.HasBounds)
            {
                node = node with
                {
                    Bounds = TransformBounds(node.Bounds, transformDelta)
                };
            }

            if (node.HasSubtreeBounds)
            {
                node = node with
                {
                    SubtreeBounds = TransformBounds(node.SubtreeBounds, transformDelta)
                };
            }

            nodes[i] = node;
        }

        nodes[nodeIndex] = currentRoot with
        {
            HasSubtreeBounds = nodes[nodeIndex].HasSubtreeBounds,
            SubtreeBounds = nodes[nodeIndex].SubtreeBounds,
            CacheMode = RetainedCompositionLayerBoundary.ResolveCacheMode(currentRoot.Visual)
        };

        if (RetainedCompositionLayerBoundary.TryGetTransformStableLayerViewport(currentRoot.Visual, out var layerBounds))
        {
            var root = nodes[nodeIndex];
            nodes[nodeIndex] = root with
            {
                HasSubtreeBounds = true,
                SubtreeBounds = layerBounds
            };
        }

        return true;
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

    private static void BuildReplacementSubtreeNodes(
        IReadOnlyList<UiRoot.RetainedRenderController.RenderNode> retainedNodes,
        RetainedCompositionNode[] nodes,
        int subtreeStartIndex,
        int subtreeEndIndexExclusive,
        int parentIndex)
    {
        var rootDepth = retainedNodes[subtreeStartIndex].Depth;
        var parentByDepth = new List<int>();
        EnsureDepthSlot(parentByDepth, rootDepth);
        if (rootDepth > 0)
        {
            parentByDepth[rootDepth - 1] = parentIndex;
        }

        for (var i = subtreeStartIndex; i < subtreeEndIndexExclusive; i++)
        {
            var retained = retainedNodes[i];
            EnsureDepthSlot(parentByDepth, retained.Depth);
            var retainedParentIndex = retained.Depth > rootDepth
                ? parentByDepth[retained.Depth - 1]
                : parentIndex;
            var node = CreateNodeFromRetainedNode(retained, retainedParentIndex);
            nodes[i] = node;

            if (retainedParentIndex >= subtreeStartIndex &&
                retainedParentIndex < subtreeEndIndexExclusive)
            {
                var parent = nodes[retainedParentIndex];
                nodes[retainedParentIndex] = parent with
                {
                    FirstChildIndex = parent.FirstChildIndex < 0 ? i : parent.FirstChildIndex,
                    ChildCount = parent.ChildCount + 1
                };
            }

            parentByDepth[retained.Depth] = i;
        }
    }

    private static RetainedCompositionNode UpdatePreservedNodeForSubtreeReplacement(
        RetainedCompositionNode node,
        int newIndex,
        int subtreeStartIndex,
        int previousSubtreeEndIndexExclusive,
        int newSubtreeEndIndexExclusive)
    {
        var delta = newSubtreeEndIndexExclusive - previousSubtreeEndIndexExclusive;
        var subtreeEndIndexExclusive = node.SubtreeEndIndexExclusive >= previousSubtreeEndIndexExclusive
            ? node.SubtreeEndIndexExclusive + delta
            : node.SubtreeEndIndexExclusive;
        return node with
        {
            ParentIndex = ShiftPreservedIndex(node.ParentIndex, subtreeStartIndex, previousSubtreeEndIndexExclusive, delta),
            FirstChildIndex = ShiftPreservedIndex(node.FirstChildIndex, subtreeStartIndex, previousSubtreeEndIndexExclusive, delta),
            SubtreeStartIndex = newIndex,
            SubtreeEndIndexExclusive = subtreeEndIndexExclusive,
            DrawOrderIndex = newIndex
        };
    }

    private static int ShiftPreservedIndex(int index, int subtreeStartIndex, int previousSubtreeEndIndexExclusive, int delta)
    {
        if (index < 0)
        {
            return -1;
        }

        if (index >= previousSubtreeEndIndexExclusive)
        {
            return index + delta;
        }

        return index < subtreeStartIndex ? index : subtreeStartIndex;
    }

    private static void EnsureDepthSlot(List<int> parentByDepth, int depth)
    {
        while (parentByDepth.Count <= depth)
        {
            parentByDepth.Add(-1);
        }
    }

    private static IReadOnlyDictionary<UIElement, int> CreateNodeIndexMap(IReadOnlyList<RetainedCompositionNode> nodes)
    {
        var indices = new Dictionary<UIElement, int>(nodes.Count, ReferenceEqualityComparer.Instance);
        for (var i = 0; i < nodes.Count; i++)
        {
            indices[nodes[i].Visual] = i;
        }

        return indices;
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
            RetainedCompositionLayerBoundary.ResolveCacheMode(visual),
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
            RetainedCompositionLayerBoundary.ResolveCacheMode(visual),
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

    private static void RefreshTransformStableLayerMetadata(RetainedCompositionNode[] nodes, int nodeIndex)
    {
        _ = RefreshSubtreeMetadataAndBounds(nodes, nodeIndex, refreshCacheKeys: false);
        var node = nodes[nodeIndex];
        var hasLayerBounds = RetainedCompositionLayerBoundary.TryGetTransformStableLayerViewport(node.Visual, out var layerBounds);
        nodes[nodeIndex] = node with
        {
            HasSubtreeBounds = hasLayerBounds || node.HasSubtreeBounds,
            SubtreeBounds = hasLayerBounds ? layerBounds : node.SubtreeBounds,
            CacheMode = RetainedCompositionLayerBoundary.ResolveCacheMode(node.Visual)
        };
    }

    private static void RefreshAncestorSubtreeBounds(RetainedCompositionNode[] nodes, int nodeIndex)
    {
        for (var currentIndex = nodeIndex; currentIndex >= 0;)
        {
            RefreshNodeSubtreeBounds(nodes, currentIndex);
            currentIndex = nodes[currentIndex].ParentIndex;
        }
    }

    private static void MarkAncestorsForRefresh(
        RetainedCompositionNode[] nodes,
        int nodeIndex,
        bool[] refreshAncestors)
    {
        for (var currentIndex = nodeIndex; currentIndex >= 0;)
        {
            refreshAncestors[currentIndex] = true;
            currentIndex = nodes[currentIndex].ParentIndex;
        }
    }

    private static void AddAncestorNodeIndices(
        RetainedCompositionNode[] nodes,
        int nodeIndex,
        List<int> nodeIndices)
    {
        for (var currentIndex = nodeIndex; currentIndex >= 0;)
        {
            AddUniqueNodeIndex(nodeIndices, currentIndex);
            currentIndex = nodes[currentIndex].ParentIndex;
        }
    }

    private static void AddUniqueNodeIndex(List<int> nodeIndices, int nodeIndex)
    {
        for (var i = 0; i < nodeIndices.Count; i++)
        {
            if (nodeIndices[i] == nodeIndex)
            {
                return;
            }
        }

        nodeIndices.Add(nodeIndex);
    }

    private static bool ContainsNodeIndex(IReadOnlyList<int> nodeIndices, int nodeIndex)
    {
        for (var i = 0; i < nodeIndices.Count; i++)
        {
            if (nodeIndices[i] == nodeIndex)
            {
                return true;
            }
        }

        return false;
    }

    private static void RefreshMarkedAncestors(RetainedCompositionNode[] nodes, bool[] refreshAncestors)
    {
        for (var nodeIndex = nodes.Length - 1; nodeIndex >= 0; nodeIndex--)
        {
            if (refreshAncestors[nodeIndex])
            {
                RefreshNodeSubtreeBounds(nodes, nodeIndex);
            }
        }
    }

    private static RetainedCompositionNode CaptureSparseLeafMetadata(
        RetainedCompositionNode node,
        RenderInvalidationKind kind)
    {
        var refreshed = CaptureMetadata(node);
        if (kind == RenderInvalidationKind.Bounds)
        {
            var hasBounds = refreshed.Visual.TryGetRenderBoundsInRootSpace(out var bounds);
            return refreshed with
            {
                HasBounds = hasBounds,
                Bounds = bounds,
                HasSubtreeBounds = hasBounds,
                SubtreeBounds = bounds,
                CacheKey = CreateSelfCacheKey(refreshed.Visual)
            };
        }

        return refreshed;
    }

    private static void RefreshNodeSubtreeBounds(RetainedCompositionNode[] nodes, int nodeIndex)
    {
        var current = nodes[nodeIndex];
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

        nodes[nodeIndex] = current with
        {
            HasSubtreeBounds = hasSubtreeBounds,
            SubtreeBounds = subtreeBounds
        };
    }

    private static void RefreshSubtreeBoundsAndCacheKeys(
        RetainedCompositionNode[] nodes,
        int subtreeStartIndex,
        int subtreeEndIndexExclusive)
    {
        for (var nodeIndex = subtreeEndIndexExclusive - 1; nodeIndex >= subtreeStartIndex; nodeIndex--)
        {
            RefreshNodeSubtreeBoundsAndCacheKey(nodes, nodeIndex);
        }
    }

    private static void RefreshAncestorSubtreeBoundsAndCacheKeys(RetainedCompositionNode[] nodes, int nodeIndex)
    {
        for (var currentIndex = nodeIndex; currentIndex >= 0;)
        {
            RefreshNodeSubtreeBoundsAndCacheKey(nodes, currentIndex);
            currentIndex = nodes[currentIndex].ParentIndex;
        }
    }

    private static void RefreshNodeSubtreeBoundsAndCacheKey(RetainedCompositionNode[] nodes, int nodeIndex)
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

        var updated = node with
        {
            HasSubtreeBounds = hasSubtreeBounds,
            SubtreeBounds = subtreeBounds
        };
        nodes[nodeIndex] = updated with
        {
            CacheKey = CreateSubtreeCacheKey(updated, nodes)
        };
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

    private static LayoutRect TransformBounds(LayoutRect rect, Matrix transform)
    {
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

    private static bool AreRectsEqual(LayoutRect left, LayoutRect right)
    {
        return MathF.Abs(left.X - right.X) <= 0.01f &&
               MathF.Abs(left.Y - right.Y) <= 0.01f &&
               MathF.Abs(left.Width - right.Width) <= 0.01f &&
               MathF.Abs(left.Height - right.Height) <= 0.01f;
    }

    private static bool AreMatricesEqual(Matrix left, Matrix right)
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
        if (nodes is RetainedCompositionNode[] nodeArray)
        {
            var arrayCopy = new RetainedCompositionNode[nodeArray.Length];
            Array.Copy(nodeArray, arrayCopy, nodeArray.Length);
            return arrayCopy;
        }

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
