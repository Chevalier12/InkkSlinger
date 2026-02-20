using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
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

    private void DrawRetainedNode(SpriteBatch spriteBatch, RenderNode node)
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

        var drawStart = Stopwatch.GetTimestamp();
        try
        {
            node.Visual.DrawSelf(spriteBatch);
        }
        finally
        {
            ObserveControlHotspotDraw(node.Visual, Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds);
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
            ObserveDirtyRegionFallbackDetachedSource();
            _dirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
            return;
        }

        if (visual is IRenderDirtyBoundsHintProvider dirtyHintProvider &&
            dirtyHintProvider.TryConsumeRenderDirtyBoundsHint(out var hintedBounds))
        {
            ObserveDirtyRegionCandidateAdded();
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
            ObserveDirtyRegionCandidateAdded();
            _dirtyRegions.AddDirtyRegion(Union(oldBounds, newBounds));
            return;
        }

        if (hasOldBounds)
        {
            ObserveDirtyRegionCandidateAdded();
            _dirtyRegions.AddDirtyRegion(oldBounds);
            return;
        }

        if (hasNewBounds)
        {
            ObserveDirtyRegionCandidateAdded();
            _dirtyRegions.AddDirtyRegion(newBounds);
            return;
        }

        ObserveNoOpRenderInvalidationNoBounds();
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

    private static bool AreRectsEqual(LayoutRect left, LayoutRect right)
    {
        const float epsilon = 0.0001f;
        return MathF.Abs(left.X - right.X) <= epsilon &&
               MathF.Abs(left.Y - right.Y) <= epsilon &&
               MathF.Abs(left.Width - right.Width) <= epsilon &&
               MathF.Abs(left.Height - right.Height) <= epsilon;
    }
}
