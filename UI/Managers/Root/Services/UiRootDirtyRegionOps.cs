using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double DirtyRegionCoverageFallbackThreshold = 0.20d;
    private const double DirtyRegionCoverageFallbackThresholdForMultipleRegions = 0.12d;
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
        if (regionCount <= 0 || regionCount > DirtyRegionCountFallbackThreshold)
        {
            return false;
        }

        var coverageThreshold = regionCount == 1
            ? DirtyRegionCoverageFallbackThreshold
            : DirtyRegionCoverageFallbackThresholdForMultipleRegions;
        return coverage <= coverageThreshold;
    }

    private void TrackDirtyBoundsForVisual(UIElement? visual)
    {
        _lastDirtyBoundsVisualElement = visual;
        _lastDirtyBoundsVisualType = visual?.GetType().Name ?? "none";
        _lastDirtyBoundsVisualName = visual is FrameworkElement frameworkElement
            ? frameworkElement.Name
            : string.Empty;
        _lastDirtyBoundsUsedHint = false;
        _hasLastDirtyBounds = false;
        _dirtyBoundsEventTrace.Add($"{_lastDirtyBoundsVisualType}#{_lastDirtyBoundsVisualName}:begin");
        if (visual == null || !IsPartOfVisualTree(visual))
        {
            _dirtyBoundsEventTrace.Add($"{_lastDirtyBoundsVisualType}#{_lastDirtyBoundsVisualName}:detached");
            MarkFullFrameDirty(UiFullDirtyReason.DetachedVisual);
            return;
        }

        if (TryGetTransformScrollDirtyBoundsHint(visual, out var transformScrollBounds))
        {
            if (!TryClipDirtyBoundsToVisualChain(visual, ref transformScrollBounds))
            {
                return;
            }

            _lastDirtyBoundsUsedHint = true;
            _lastDirtyBounds = transformScrollBounds;
            _hasLastDirtyBounds = true;
            _dirtyBoundsEventTrace.Add($"{_lastDirtyBoundsVisualType}#{_lastDirtyBoundsVisualName}:scroll-clip-hint:{transformScrollBounds.X:0.##},{transformScrollBounds.Y:0.##},{transformScrollBounds.Width:0.##},{transformScrollBounds.Height:0.##}");
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

            _lastDirtyBoundsUsedHint = true;
            _lastDirtyBounds = hintedBounds;
            _hasLastDirtyBounds = true;
            _dirtyBoundsEventTrace.Add($"{_lastDirtyBoundsVisualType}#{_lastDirtyBoundsVisualName}:hint:{hintedBounds.X:0.##},{hintedBounds.Y:0.##},{hintedBounds.Width:0.##},{hintedBounds.Height:0.##}");
            AddDirtyRegionForDiagnostics(hintedBounds, "hint");
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
        if (hasNewBounds)
        {
            _lastDirtyBounds = newBounds;
            _hasLastDirtyBounds = true;
        }
        else if (hasOldBounds)
        {
            _lastDirtyBounds = oldBounds;
            _hasLastDirtyBounds = true;
        }
        _dirtyBoundsEventTrace.Add(
            $"{_lastDirtyBoundsVisualType}#{_lastDirtyBoundsVisualName}:bounds:" +
            $"{(hasOldBounds ? $"{oldBounds.X:0.##},{oldBounds.Y:0.##},{oldBounds.Width:0.##},{oldBounds.Height:0.##}" : "none")}" +
            "->" +
            $"{(hasNewBounds ? $"{newBounds.X:0.##},{newBounds.Y:0.##},{newBounds.Width:0.##},{newBounds.Height:0.##}" : "none")}");
        AddDirtyBounds(visual, hasOldBounds, oldBounds, hasNewBounds, newBounds);
    }

    private void RecordBoundsDelta(RenderNode previous, RenderNode updated)
    {
        if (previous.HasBoundsSnapshot &&
            updated.HasBoundsSnapshot &&
            AreRectsEqual(previous.BoundsSnapshot, updated.BoundsSnapshot))
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

            _lastDirtyBoundsVisualType = updated.Visual.GetType().Name;
            _lastDirtyBoundsVisualName = updated.Visual is FrameworkElement updatedFrameworkElement
                ? updatedFrameworkElement.Name
                : string.Empty;
            _lastDirtyBoundsVisualElement = updated.Visual;
            _lastDirtyBoundsUsedHint = true;
            _lastDirtyBounds = transformScrollBounds;
            _hasLastDirtyBounds = true;
            _dirtyBoundsEventTrace.Add($"{_lastDirtyBoundsVisualType}#{_lastDirtyBoundsVisualName}:scroll-clip-hint:{transformScrollBounds.X:0.##},{transformScrollBounds.Y:0.##},{transformScrollBounds.Width:0.##},{transformScrollBounds.Height:0.##}");
            AddDirtyRegionForDiagnostics(transformScrollBounds, "scroll-clip-hint-delta");
            return;
        }

        AddDirtyBounds(
            updated.Visual,
            previous.HasBoundsSnapshot,
            previous.BoundsSnapshot,
            updated.HasBoundsSnapshot,
            updated.BoundsSnapshot);
    }

    private void AddDirtyBounds(UIElement? visual, bool hasOldBounds, LayoutRect oldBounds, bool hasNewBounds, LayoutRect newBounds)
    {
        if (hasOldBounds && hasNewBounds)
        {
            if (AreRectsEqual(oldBounds, newBounds))
            {
                if (ShouldSkipUnchangedDirtyBoundsForVisual(visual))
                {
                    _dirtyBoundsEventTrace.Add($"dirty-skip:unchanged:{DescribeDirtyBoundsVisual(visual)}:{oldBounds.X:0.##},{oldBounds.Y:0.##},{oldBounds.Width:0.##},{oldBounds.Height:0.##}");
                    return;
                }

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
            return;
        }

    }

    private static bool ShouldSkipUnchangedDirtyBoundsForVisual(UIElement? visual)
    {
        return visual switch
        {
            Grid grid => !grid.ShowGridLines,
            Canvas => true,
            StackPanel => true,
            WrapPanel => true,
            VirtualizingStackPanel => true,
            _ => false
        };
    }

    private static string DescribeDirtyBoundsVisual(UIElement? visual)
    {
        return visual switch
        {
            FrameworkElement { Name.Length: > 0 } frameworkElement => $"{frameworkElement.GetType().Name}#{frameworkElement.Name}",
            null => "none",
            _ => visual.GetType().Name
        };
    }

    private void AddDirtyRegionForDiagnostics(LayoutRect bounds, string reason)
    {
        _dirtyBoundsEventTrace.Add($"dirty-add:{reason}:{bounds.X:0.##},{bounds.Y:0.##},{bounds.Width:0.##},{bounds.Height:0.##}");
        _dirtyRegions.AddDirtyRegion(bounds);
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
        if (visual is VirtualizingStackPanel)
        {
            return false;
        }

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

        if (visual is IScrollTransformContent &&
            TryGetDirectTransformScrollOwner(visual, out var transformOwner) &&
            transformOwner.TryGetContentViewportClipRect(out bounds))
        {
            return bounds.Width > 0f && bounds.Height > 0f;
        }

        if (visual is Panel panel &&
            ScrollViewer.GetUseTransformContentScrolling(panel) &&
            TryGetDirectTransformScrollOwner(panel, out transformOwner) &&
            transformOwner.TryGetContentViewportClipRect(out bounds))
        {
            return bounds.Width > 0f && bounds.Height > 0f;
        }

        return false;
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

    private static LayoutRect IntersectRect(LayoutRect left, LayoutRect right)
    {
        var x = MathF.Max(left.X, right.X);
        var y = MathF.Max(left.Y, right.Y);
        var rightEdge = MathF.Min(left.X + left.Width, right.X + right.Width);
        var bottomEdge = MathF.Min(left.Y + left.Height, right.Y + right.Height);
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
