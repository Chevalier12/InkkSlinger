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
            RecordFullRetainedDrawWithoutFullClearIfNeeded();
            DrawRetainedRenderList(spriteBatch);
            return;
        }

        if (!ShouldUsePartialDirtyRedraw(_dirtyRegions.RegionCount, LastDirtyAreaPercentage))
        {
            _dirtyRegionThresholdFallbackCount++;
            RecordFullRetainedDrawWithoutFullClearIfNeeded();
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
        var translationX = 0f;
        var translationY = 0f;
        var translationStack = new List<ScrollTranslationFrame>(4);
        _activeRetainedDrawPath.Clear();

        try
        {
            for (var nodeIndex = 0; nodeIndex < _retainedRenderList.Count; nodeIndex++)
            {
                visited++;
                var node = _retainedRenderList[nodeIndex];
                while (translationStack.Count > 0 && translationStack[^1].Depth >= node.Depth)
                {
                    var frame = translationStack[^1];
                    translationX -= frame.TranslationX;
                    translationY -= frame.TranslationY;
                    translationStack.RemoveAt(translationStack.Count - 1);
                }

                if (!node.IsEffectivelyVisible)
                {
                    nodeIndex = Math.Max(nodeIndex, node.SubtreeEndIndexExclusive - 1);
                    continue;
                }

                if (node.HasSubtreeBoundsSnapshot)
                {
                    var subtreeBounds = (AreClose(translationX, 0f) && AreClose(translationY, 0f))
                        ? node.SubtreeBoundsSnapshot
                        : TranslateRect(node.SubtreeBoundsSnapshot, translationX, translationY);
                    if (!Intersects(subtreeBounds, clipRect))
                    {
                        nodeIndex = Math.Max(nodeIndex, node.SubtreeEndIndexExclusive - 1);
                        continue;
                    }
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

                if (node.HasScrollTranslation)
                {
                    var offsetX = node.ScrollTranslationX;
                    var offsetY = node.ScrollTranslationY;
                    if (!AreClose(offsetX, 0f) || !AreClose(offsetY, 0f))
                    {
                        translationStack.Add(new ScrollTranslationFrame(node.Depth, offsetX, offsetY));
                        translationX += offsetX;
                        translationY += offsetY;
                    }
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
            if (hasOldBounds)
            {
                oldBounds = ApplyScrollTranslationFromAncestors(visual, oldBounds);
            }
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
        var previousBounds = previous.BoundsSnapshot;
        if (previous.HasBoundsSnapshot)
        {
            previousBounds = ApplyScrollTranslationFromAncestors(previous.Visual, previousBounds);
        }

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
            previousBounds,
            updated.HasBoundsSnapshot,
            updated.BoundsSnapshot);
    }

    private void AddDirtyBounds(UIElement? visual, bool hasOldBounds, LayoutRect oldBounds, bool hasNewBounds, LayoutRect newBounds)
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
            return;
        }

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
