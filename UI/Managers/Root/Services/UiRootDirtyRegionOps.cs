using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double DirtyRegionCoverageFallbackThreshold = 0.20d;
    private const int DirtyRegionCountFallbackThreshold = 4;

    private void DrawRetainedRenderListWithDirtyRegions(SpriteBatch spriteBatch)
    {
        var dirtyCoverage = _dirtyRegions.GetDirtyAreaCoverage();
        LastDirtyRectCount = _dirtyRegions.IsFullFrameDirty ? 1 : _dirtyRegions.RegionCount;
        LastDirtyAreaPercentage = dirtyCoverage;

        if (_dirtyRegions.IsFullFrameDirty || _dirtyRegions.RegionCount == 0)
        {
            DrawRetainedRenderList(spriteBatch);
            return;
        }

        if (!ShouldUsePartialDirtyRedraw(_dirtyRegions.RegionCount, LastDirtyAreaPercentage))
        {
            _dirtyRegionThresholdFallbackCount++;
            DrawRetainedRenderList(spriteBatch);
            return;
        }

        DrawRetainedRenderListForDirtyRegions(spriteBatch, _dirtyRegions.Regions);
        LastDrawUsedPartialRedraw = true;
    }

    private void DrawRetainedRenderList(SpriteBatch spriteBatch)
    {
        var metrics = TraverseRetainedNodesWithinClip(
            spriteBatch,
            ToLayoutRect(spriteBatch.GraphicsDevice.ScissorRectangle));
        _lastRetainedTraversalCount++;
        _lastRetainedNodesVisited += metrics.NodesVisited;
        _lastRetainedNodesDrawn += metrics.NodesDrawn;
    }

    private void DrawRetainedRenderListForDirtyRegions(SpriteBatch spriteBatch, IReadOnlyList<LayoutRect> regions)
    {
        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var dirtyRegion = regions[regionIndex];
            UiDrawing.PushAbsoluteClip(spriteBatch, dirtyRegion);
            try
            {
                UiDrawing.DrawFilledRect(spriteBatch, dirtyRegion, _clearColor);

                var metrics = TraverseRetainedNodesWithinClip(spriteBatch, dirtyRegion);
                _lastRetainedTraversalCount++;
                _lastRetainedNodesVisited += metrics.NodesVisited;
                _lastRetainedNodesDrawn += metrics.NodesDrawn;
                _lastDirtyRegionTraversalCount++;
            }
            finally
            {
                UiDrawing.PopClip(spriteBatch);
            }
        }
    }

    private RenderTraversalMetrics TraverseRetainedNodesWithinClip(SpriteBatch? spriteBatch, LayoutRect clipRect, List<UIElement>? visuals = null)
    {
        var visited = 0;
        var drawn = 0;
        var clipPushCount = 0;
        _activeRetainedDrawPath.Clear();

        try
        {
            for (var nodeIndex = 0; nodeIndex < _retainedRenderList.Count; nodeIndex++)
            {
                visited++;
                var node = _retainedRenderList[nodeIndex];
                if (!node.IsEffectivelyVisible)
                {
                    nodeIndex = Math.Max(nodeIndex, node.SubtreeEndIndexExclusive - 1);
                    continue;
                }

                if (node.HasSubtreeBoundsSnapshot && !Intersects(node.SubtreeBoundsSnapshot, clipRect))
                {
                    nodeIndex = Math.Max(nodeIndex, node.SubtreeEndIndexExclusive - 1);
                    continue;
                }

                if (spriteBatch != null)
                {
                    SyncRetainedDrawState(spriteBatch, node, ref clipPushCount);
                    node.Visual.DrawSelf(spriteBatch);
                }
                else if (node.HasLocalClip)
                {
                    clipPushCount++;
                }

                visuals?.Add(node.Visual);
                drawn++;
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

    private void AppendRetainedDrawOrderForClip(LayoutRect clipRect, List<UIElement> visuals)
    {
        _ = TraverseRetainedNodesWithinClip(spriteBatch: null, clipRect, visuals);
    }

    private void SyncRetainedDrawState(SpriteBatch spriteBatch, RenderNode node, ref int clipPushCount)
    {
        while (_activeRetainedDrawPath.Count > node.Depth)
        {
            PopRetainedDrawNodeState(spriteBatch, _activeRetainedDrawPath[^1]);
            _activeRetainedDrawPath.RemoveAt(_activeRetainedDrawPath.Count - 1);
        }

        ApplyRetainedDrawNodeState(spriteBatch, node, ref clipPushCount);
        _activeRetainedDrawPath.Add(node);
    }

    private void ResetRetainedDrawState(SpriteBatch spriteBatch)
    {
        for (var i = _activeRetainedDrawPath.Count - 1; i >= 0; i--)
        {
            PopRetainedDrawNodeState(spriteBatch, _activeRetainedDrawPath[i]);
        }

        _activeRetainedDrawPath.Clear();
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

    private static bool ShouldUsePartialDirtyRedraw(int regionCount, double coverage)
    {
        return regionCount > 0 &&
               regionCount <= DirtyRegionCountFallbackThreshold &&
               coverage <= DirtyRegionCoverageFallbackThreshold;
    }

    private void TrackDirtyBoundsForVisual(UIElement? visual)
    {
        if (visual == null || !IsPartOfVisualTree(visual))
        {
            MarkFullFrameDirty(UiFullDirtyReason.DetachedVisual);
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
            if (AreRectsEqual(oldBounds, newBounds))
            {
                _dirtyRegions.AddDirtyRegion(oldBounds);
                return;
            }

            if (IntersectsOrTouches(oldBounds, newBounds))
            {
                _dirtyRegions.AddDirtyRegion(Union(oldBounds, newBounds));
                return;
            }

            _dirtyRegions.AddDirtyRegion(oldBounds);
            _dirtyRegions.AddDirtyRegion(newBounds);
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

    private static LayoutRect Union(LayoutRect left, LayoutRect right)
    {
        var x = MathF.Min(left.X, right.X);
        var y = MathF.Min(left.Y, right.Y);
        var rightEdge = MathF.Max(left.X + left.Width, right.X + right.Width);
        var bottomEdge = MathF.Max(left.Y + left.Height, right.Y + right.Height);
        return new LayoutRect(x, y, MathF.Max(0f, rightEdge - x), MathF.Max(0f, bottomEdge - y));
    }

    private static bool AreRectsEqual(LayoutRect left, LayoutRect right)
    {
        const float epsilon = 0.0001f;
        return MathF.Abs(left.X - right.X) <= epsilon &&
               MathF.Abs(left.Y - right.Y) <= epsilon &&
               MathF.Abs(left.Width - right.Width) <= epsilon &&
               MathF.Abs(left.Height - right.Height) <= epsilon;
    }

    private static LayoutRect ToLayoutRect(Rectangle rectangle)
    {
        return new LayoutRect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }

    private readonly record struct RenderTraversalMetrics(
        int NodesVisited,
        int NodesDrawn,
        int ClipPushCount);
}
