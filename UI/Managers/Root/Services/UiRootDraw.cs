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
        LastDrawReasons = _scheduledDrawReasons;
        _scheduledDrawReasons = UiRedrawReason.None;
        _lastRetainedNodesVisited = 0;
        _lastRetainedNodesDrawn = 0;
        _lastRetainedClipPushCount = 0;
        _lastSpriteBatchRestartCount = 0;
        UiDrawing.ResetFrameTelemetry();

        var graphicsDevice = spriteBatch.GraphicsDevice;
        if (!_hasLastLayoutViewport || !AreViewportsEqual(_lastLayoutViewport, graphicsDevice.Viewport))
        {
            SyncDirtyRegionViewport(graphicsDevice.Viewport);
        }

        var dirtyCoverage = _dirtyRegions.GetDirtyAreaCoverage();
        var usePartialClear = UseRetainedRenderList &&
                              UseDirtyRegionRendering &&
                              !_dirtyRegions.IsFullFrameDirty &&
                              ShouldUsePartialDirtyRedraw(_dirtyRegions.RegionCount, dirtyCoverage);
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

            if (UseSoftwareCursor)
            {
                DrawSoftwareCursor(spriteBatch, _inputState.LastPointerPosition);
            }
        }
        finally
        {
            UiDrawing.ClearActiveBatchState(graphicsDevice);
            spriteBatch.End();
        }

        var drawingTelemetry = UiDrawing.ConsumeFrameTelemetry();
        _lastSpriteBatchRestartCount = drawingTelemetry.SpriteBatchRestartCount;
        _lastRetainedClipPushCount = drawingTelemetry.ClipPushCount;

        ApplyRenderInvalidationCleanupAfterDraw();
        _hasMeasureInvalidation = false;
        _hasArrangeInvalidation = false;
        _hasRenderInvalidation = false;
        _hasCaretBlinkInvalidation = false;
        _mustDrawNextFrame = false;
        ClearDirtyRenderQueue();
        ResetRetainedSyncTrackingState();
        _dirtyRegions.Clear();
        LastDrawMs = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
    }

    private void DrawSoftwareCursor(SpriteBatch spriteBatch, Vector2 pointer)
    {
        var size = MathF.Max(6f, SoftwareCursorSize);
        var half = size / 2f;
        var color = SoftwareCursorColor;
        const float thickness = 2f;

        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(pointer.X - half, pointer.Y - 1f, size, thickness), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(pointer.X - 1f, pointer.Y - half, thickness, size), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(pointer.X - 1f, pointer.Y - 1f, thickness, thickness), Color.Black, 1f);
    }

    private void ApplyRenderInvalidationCleanupAfterDraw()
    {
        if (!UseRetainedRenderList || _dirtyRegions.IsFullFrameDirty || _lastRetainedSyncUsedFullRebuild)
        {
            _visualRoot.ClearRenderInvalidationRecursive();
            return;
        }

        if (_lastSynchronizedDirtyRenderRoots.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _lastSynchronizedDirtyRenderRoots.Count; i++)
        {
            _lastSynchronizedDirtyRenderRoots[i].ClearRenderInvalidationRecursive();
        }

        for (var i = 0; i < _lastSynchronizedDirtyRenderRoots.Count; i++)
        {
            ClearRenderInvalidationAncestorChain(_lastSynchronizedDirtyRenderRoots[i].GetInvalidationParent());
        }
    }

    private void ClearRenderInvalidationAncestorChain(UIElement? visual)
    {
        for (var current = visual; current != null; current = current.GetInvalidationParent())
        {
            if (HasPendingDirtyVisualInSubtree(current))
            {
                continue;
            }

            current.ClearRenderInvalidationShallow();
        }
    }

    private bool HasPendingDirtyVisualInSubtree(UIElement subtreeRoot)
    {
        if (_dirtyRenderSet.Count == 0)
        {
            return false;
        }

        foreach (var dirtyVisual in _dirtyRenderSet)
        {
            if (ReferenceEquals(dirtyVisual, subtreeRoot) || IsDescendantOf(dirtyVisual, subtreeRoot))
            {
                return true;
            }
        }

        return false;
    }
}
