using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private void PrepareElementRenderCaches(GraphicsDevice graphicsDevice)
    {
        if (!UseElementRenderCaches)
        {
            return;
        }

        var useDirtyFilter = !_dirtyRegions.IsFullFrameDirty && _dirtyRegions.RegionCount > 0;
        var regions = _dirtyRegions.Regions;
        for (var i = 0; i < _retainedRenderList.Count; i++)
        {
            var node = _retainedRenderList[i];
            if (!node.IsEffectivelyVisible)
            {
                continue;
            }

            if (useDirtyFilter &&
                node.HasSubtreeBoundsSnapshot &&
                !IntersectsAny(node.SubtreeBoundsSnapshot, regions))
            {
                continue;
            }

            EnsureNodeCache(graphicsDevice, i);
        }
    }

    private void EnsureNodeCache(GraphicsDevice graphicsDevice, int nodeIndex)
    {
        var node = _retainedRenderList[nodeIndex];
        var context = CreateRenderCacheContext(node);
        if (!_renderCachePolicy.CanCache(node.Visual, context))
        {
            _renderCacheStore.Remove(node.Visual);
            return;
        }

        if (!_renderCachePolicy.TryGetCacheBounds(node.Visual, context, out var cacheBounds))
        {
            _renderCacheStore.Remove(node.Visual);
            return;
        }

        cacheBounds = NormalizeBounds(cacheBounds);
        if (!IsValidCacheBounds(cacheBounds))
        {
            _renderCacheStore.Remove(node.Visual);
            return;
        }

        if (_renderCacheStore.TryGet(node.Visual, out var existing))
        {
            var snapshot = new RenderCacheSnapshot(
                existing.Bounds,
                existing.RenderVersionStamp,
                existing.LayoutVersionStamp,
                existing.RenderStateSignature);
            if (!_renderCachePolicy.ShouldRebuildCache(node.Visual, context, snapshot))
            {
                return;
            }

            var rebuiltTarget = RenderVisualToCache(graphicsDevice, node, cacheBounds, existing.RenderTarget);
            if (rebuiltTarget == null)
            {
                _renderCacheStore.Remove(node.Visual);
                return;
            }

            _renderCacheStore.Upsert(
                node.Visual,
                rebuiltTarget,
                cacheBounds,
                node.SubtreeRenderVersionStamp,
                node.SubtreeLayoutVersionStamp,
                node.RenderStateSignature);
            CacheRebuildCount++;
            LastFrameCacheRebuildCount++;
            return;
        }

        var createdTarget = RenderVisualToCache(graphicsDevice, node, cacheBounds, null);
        if (createdTarget == null)
        {
            return;
        }

        _renderCacheStore.Upsert(
            node.Visual,
            createdTarget,
            cacheBounds,
            node.SubtreeRenderVersionStamp,
            node.SubtreeLayoutVersionStamp,
            node.RenderStateSignature);
        CacheMissCount++;
        LastFrameCacheMissCount++;
    }

    private RenderTarget2D? RenderVisualToCache(
        GraphicsDevice graphicsDevice,
        RenderNode node,
        LayoutRect bounds,
        RenderTarget2D? existingRenderTarget)
    {
        var width = Math.Max(1, (int)MathF.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)MathF.Ceiling(bounds.Height));
        if (width > MaxCacheTextureDimension || height > MaxCacheTextureDimension)
        {
            return null;
        }

        RenderTarget2D target;
        if (existingRenderTarget != null &&
            !existingRenderTarget.IsDisposed &&
            existingRenderTarget.Width == width &&
            existingRenderTarget.Height == height)
        {
            target = existingRenderTarget;
        }
        else
        {
            existingRenderTarget?.Dispose();
            target = new RenderTarget2D(
                graphicsDevice,
                width,
                height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0,
                RenderTargetUsage.DiscardContents);
        }

        EnsureCacheSpriteBatch(graphicsDevice);
        graphicsDevice.SetRenderTarget(target);
        graphicsDevice.Clear(Color.Transparent);
        _cacheSpriteBatch!.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.LinearClamp,
            depthStencilState: DepthStencilState.None,
            rasterizerState: UiRasterizerState);
        UiDrawing.SetActiveBatchState(graphicsDevice,
            SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, UiRasterizerState);
        try
        {
            UiDrawing.ResetState(graphicsDevice);
            UiDrawing.PushTransform(_cacheSpriteBatch, Matrix.CreateTranslation(-bounds.X, -bounds.Y, 0f));
            try
            {
                node.Visual.Draw(_cacheSpriteBatch);
            }
            finally
            {
                UiDrawing.PopTransform(_cacheSpriteBatch);
            }
        }
        finally
        {
            UiDrawing.ClearActiveBatchState(graphicsDevice);
            _cacheSpriteBatch.End();
            graphicsDevice.SetRenderTarget(null);
        }

        return target;
    }

    private void EnsureCacheSpriteBatch(GraphicsDevice graphicsDevice)
    {
        if (_cacheSpriteBatch != null &&
            !ReferenceEquals(_cacheSpriteBatch.GraphicsDevice, graphicsDevice))
        {
            _cacheSpriteBatch.Dispose();
            _cacheSpriteBatch = null;
        }

        _cacheSpriteBatch ??= new SpriteBatch(graphicsDevice);
    }

    private bool TryDrawCachedNode(SpriteBatch spriteBatch, RenderNode node, out int subtreeEndIndexExclusive)
    {
        subtreeEndIndexExclusive = node.SubtreeEndIndexExclusive;
        if (!UseElementRenderCaches)
        {
            return false;
        }

        var context = CreateRenderCacheContext(node);
        if (!_renderCachePolicy.CanCache(node.Visual, context))
        {
            return false;
        }

        if (!_renderCachePolicy.TryGetCacheBounds(node.Visual, context, out var cacheBounds))
        {
            return false;
        }

        cacheBounds = NormalizeBounds(cacheBounds);
        if (!_renderCacheStore.TryGet(node.Visual, out var entry))
        {
            return false;
        }

        if (!AreRectsEqual(entry.Bounds, cacheBounds))
        {
            return false;
        }

        DrawCachedNode(spriteBatch, node, entry);
        _lastFrameCachedSubtreeBounds.Add(entry.Bounds);
        CacheHitCount++;
        LastFrameCacheHitCount++;
        return true;
    }

    private static void DrawCachedNode(SpriteBatch spriteBatch, RenderNode node, RenderCacheEntry entry)
    {
        var pushedClipCount = 0;
        var pushedTransformCount = 0;
        var steps = node.RenderStateSteps;
        for (var i = 0; i < node.LocalRenderStateStartIndex; i++)
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

        try
        {
            UiDrawing.DrawTexture(spriteBatch, entry.RenderTarget, entry.Bounds, color: Color.White);
        }
        finally
        {
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

    private static RenderCachePolicyContext CreateRenderCacheContext(RenderNode node)
    {
        return new RenderCachePolicyContext(
            node.IsEffectivelyVisible,
            node.HasSubtreeBoundsSnapshot,
            node.SubtreeBoundsSnapshot,
            node.HasTransformState,
            node.HasClipState,
            node.RenderStateSteps.Length,
            node.RenderStateSignature,
            node.SubtreeVisualCount,
            node.SubtreeHighCostVisualCount,
            node.SubtreeRenderVersionStamp,
            node.SubtreeLayoutVersionStamp);
    }


    private static bool IsValidCacheBounds(LayoutRect bounds)
    {
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return false;
        }

        var width = Math.Max(1, (int)MathF.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)MathF.Ceiling(bounds.Height));
        return width <= MaxCacheTextureDimension && height <= MaxCacheTextureDimension;
    }

    private static LayoutRect NormalizeBounds(LayoutRect bounds)
    {
        var x = bounds.X;
        var y = bounds.Y;
        var width = bounds.Width;
        var height = bounds.Height;
        if (width < 0f)
        {
            x += width;
            width = -width;
        }

        if (height < 0f)
        {
            y += height;
            height = -height;
        }

        return new LayoutRect(x, y, width, height);
    }

    private static bool IntersectsAny(LayoutRect bounds, IReadOnlyList<LayoutRect> regions)
    {
        for (var i = 0; i < regions.Count; i++)
        {
            if (Intersects(bounds, regions[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawCachedSubtreeBoundsOverlay(SpriteBatch spriteBatch, IReadOnlyList<LayoutRect> bounds)
    {
        for (var i = 0; i < bounds.Count; i++)
        {
            UiDrawing.DrawRectStroke(spriteBatch, bounds[i], 1f, new Color(76, 217, 100), opacity: 0.9f);
        }
    }

    private void TraceRenderCacheCountersIfEnabled()
    {
        if (!TraceRenderCacheCounters)
        {
            return;
        }

        var timestamp = Stopwatch.GetTimestamp();
        if (_lastRenderCacheCounterTraceTimestamp != 0 &&
            Stopwatch.GetElapsedTime(_lastRenderCacheCounterTraceTimestamp).TotalSeconds < 1d)
        {
            return;
        }

        _lastRenderCacheCounterTraceTimestamp = timestamp;
        Console.WriteLine(
            $"[UiCache] frame-hit:{LastFrameCacheHitCount} frame-miss:{LastFrameCacheMissCount} " +
            $"frame-rebuild:{LastFrameCacheRebuildCount} entries:{CacheEntryCount} bytes:{CacheBytes} " +
            $"overlay:{_lastFrameCachedSubtreeBounds.Count}");
    }
}
