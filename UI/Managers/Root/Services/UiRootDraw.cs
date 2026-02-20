using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
    {
        Dispatcher.VerifyAccess();
        _ = gameTime;
        if (spriteBatch == null)
        {
            throw new ArgumentNullException(nameof(spriteBatch));
        }

        var drawStart = Stopwatch.GetTimestamp();
        DrawCalls = 1;
        LastDrawUsedPartialRedraw = false;
        LastFrameCacheHitCount = 0;
        LastFrameCacheMissCount = 0;
        LastFrameCacheRebuildCount = 0;
        _lastFrameCachedSubtreeBounds.Clear();
        LastDrawReasons = _scheduledDrawReasons;
        _scheduledDrawReasons = UiRedrawReason.None;
        ObserveDirtyRegionBeforeDraw();

        var graphicsDevice = spriteBatch.GraphicsDevice;
        if (!_hasLastLayoutViewport || !AreViewportsEqual(_lastLayoutViewport, graphicsDevice.Viewport))
        {
            SyncDirtyRegionViewport(graphicsDevice.Viewport);
        }

        _renderCacheStore.EnsureDevice(graphicsDevice);

        if (UseRetainedRenderList)
        {
            PrepareElementRenderCaches(graphicsDevice);
        }

        var usePartialClear = UseRetainedRenderList &&
                              UseDirtyRegionRendering &&
                              !_dirtyRegions.IsFullFrameDirty &&
                              _dirtyRegions.RegionCount > 0;
        if (!usePartialClear)
        {
            graphicsDevice.Clear(_clearColor);
        }

        spriteBatch.Begin(
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
            if (UseRetainedRenderList)
            {
                if (UseDirtyRegionRendering)
                {
                    DrawRetainedRenderListWithDirtyRegions(spriteBatch);
                }
                else
                {
                    LastDirtyRectCount = 1;
                    LastDirtyAreaPercentage = 1d;
                    DrawRetainedRenderList(spriteBatch);
                }
            }
            else
            {
                _visualRoot.Draw(spriteBatch);
                LastDirtyRectCount = 1;
                LastDirtyAreaPercentage = 1d;
            }

            if (ShowCachedSubtreeBoundsOverlay && _lastFrameCachedSubtreeBounds.Count > 0)
            {
                DrawCachedSubtreeBoundsOverlay(spriteBatch, _lastFrameCachedSubtreeBounds);
            }
        }
        finally
        {
            UiDrawing.ClearActiveBatchState(graphicsDevice);
            spriteBatch.End();
        }

        _visualRoot.ClearRenderInvalidationRecursive();
        _hasMeasureInvalidation = false;
        _hasArrangeInvalidation = false;
        _hasRenderInvalidation = false;
        _hasCaretBlinkInvalidation = false;
        _mustDrawNextFrame = false;
        ClearDirtyRenderQueue();
        _dirtyRegions.Clear();
        LastDrawMs = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
        ObserveScrollCpuAfterDraw();
        ObserveClickCpuAfterDraw();
        ObserveMoveCpuAfterDraw();
        ObserveFrameLatencyAfterDraw();
        ObserveDirtyRegionAfterDraw();
        ObserveRenderCacheChurnAfterDraw();
        ObserveAllocationGcAfterDraw();
        ObserveInputRouteComplexityAfterDraw();
        ObserveNoOpInvalidationAfterDraw();
        ObserveControlHotspotAfterDraw();
        TraceRenderCacheCountersIfEnabled();
    }
}
