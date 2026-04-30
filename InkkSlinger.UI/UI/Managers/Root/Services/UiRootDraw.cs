using System;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

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

        DrawExecutedFrameCount++;

        var drawStart = Stopwatch.GetTimestamp();
        DrawCalls = 1;
        LastDrawUsedPartialRedraw = false;
        LastDrawReasons = _scheduledDrawReasons;
        _scheduledDrawReasons = UiRedrawReason.None;
        _lastRetainedNodesVisited = 0;
        _lastRetainedNodesDrawn = 0;
        _lastRetainedClipPushCount = 0;
        _lastRetainedTraversalCount = 0;
        _lastDirtyRegionTraversalCount = 0;
        _lastSpriteBatchRestartCount = 0;
        _lastSpriteBatchRestartMs = 0d;
        _lastDrawClearMs = 0d;
        _lastDrawInitialBatchBeginMs = 0d;
        _lastDrawVisualTreeMs = 0d;
        _lastDrawCursorMs = 0d;
        _lastDrawFinalBatchEndMs = 0d;
        _lastDrawCleanupMs = 0d;
        _currentDrawPerformedFullClear = false;
        UiDrawing.ResetFrameTelemetry();

        var graphicsDevice = spriteBatch.GraphicsDevice;
        if (!_hasLastLayoutViewport || !AreViewportsEqual(_lastLayoutViewport, graphicsDevice.Viewport))
        {
            SyncDirtyRegionViewport(graphicsDevice.Viewport);
        }

        var drawDecision = ResolveDirtyDrawDecisionAfterRetainedSync();
        var usePartialClear = drawDecision.UsePartialClear;
        if (!usePartialClear)
        {
            var clearStart = Stopwatch.GetTimestamp();
            graphicsDevice.Clear(_clearColor);
            _lastDrawClearMs = Stopwatch.GetElapsedTime(clearStart).TotalMilliseconds;
            _currentDrawPerformedFullClear = true;
        }

        var batchBeginStart = Stopwatch.GetTimestamp();
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.LinearClamp,
            depthStencilState: DepthStencilState.None,
            rasterizerState: UiRasterizerState);
        _lastDrawInitialBatchBeginMs = Stopwatch.GetElapsedTime(batchBeginStart).TotalMilliseconds;
        UiDrawing.SetActiveBatchState(graphicsDevice,
            SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, UiRasterizerState);
        try
        {
            UiDrawing.ResetState(graphicsDevice);
            var treeDrawStart = Stopwatch.GetTimestamp();
            if (UseRetainedRenderList)
            {
                if (UseDirtyRegionRendering && usePartialClear)
                {
                    DrawRetainedRenderListWithDirtyRegions(spriteBatch);
                }
                else
                {
                    LastDirtyRectCount = 1;
                    LastDirtyAreaPercentage = 1d;
                    RecordFullRetainedDrawWithoutFullClearIfNeeded();
                    DrawRetainedRenderList(spriteBatch);
                }
            }
            else
            {
                _visualRoot.Draw(spriteBatch);

                LastDirtyRectCount = 1;
                LastDirtyAreaPercentage = 1d;
            }
            _lastDrawVisualTreeMs = Stopwatch.GetElapsedTime(treeDrawStart).TotalMilliseconds;

            if (UseSoftwareCursor)
            {
                var cursorDrawStart = Stopwatch.GetTimestamp();
                DrawSoftwareCursor(spriteBatch, _inputState.LastPointerPosition);
                _lastDrawCursorMs = Stopwatch.GetElapsedTime(cursorDrawStart).TotalMilliseconds;
            }
        }
        finally
        {
            UiDrawing.ClearActiveBatchState(graphicsDevice);
            var batchEndStart = Stopwatch.GetTimestamp();
            spriteBatch.End();
            _lastDrawFinalBatchEndMs = Stopwatch.GetElapsedTime(batchEndStart).TotalMilliseconds;
        }

        var drawingTelemetry = UiDrawing.ConsumeFrameTelemetry();
        _lastSpriteBatchRestartCount = drawingTelemetry.SpriteBatchRestartCount;
        _lastSpriteBatchRestartMs = (double)drawingTelemetry.SpriteBatchRestartElapsedTicks * 1000d / Stopwatch.Frequency;
        _lastRetainedClipPushCount = drawingTelemetry.ClipPushCount;

        var cleanupStart = Stopwatch.GetTimestamp();
        ApplyRenderInvalidationCleanupAfterDraw();
        _hasMeasureInvalidation = false;
        _hasArrangeInvalidation = false;
        _hasRenderInvalidation = false;
        _hasCaretBlinkInvalidation = false;
        _mustDrawNextFrame = false;
        ClearDirtyRenderQueue();
        ResetRetainedSyncTrackingState();
        _dirtyRegions.Clear();
        _diagnosticCaptureFullClearPending = false;
        ConsumeFullRedrawSettleFrame();
        _lastDrawCleanupMs = Stopwatch.GetElapsedTime(cleanupStart).TotalMilliseconds;
        LastDrawMs = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
    }

    private UiDirtyDrawDecisionSnapshot ResolveDirtyDrawDecisionAfterRetainedSync()
    {
        var beforeSyncReason = GetCurrentDirtyDrawDecisionReason();
        if (UseRetainedRenderList)
        {
            SynchronizeRetainedRenderListForDrawIfNeeded();
        }

        var afterSyncReason = GetCurrentDirtyDrawDecisionReason();
        if (beforeSyncReason != afterSyncReason)
        {
            _retainedSyncChangedDirtyDecisionCount++;
        }

        _lastDirtyDrawDecisionReason = afterSyncReason;
        return new UiDirtyDrawDecisionSnapshot(
            beforeSyncReason,
            afterSyncReason,
            afterSyncReason == UiDirtyDrawDecisionReason.Partial,
            afterSyncReason != UiDirtyDrawDecisionReason.Partial);
    }

    private UiDirtyDrawDecisionReason GetCurrentDirtyDrawDecisionReason()
    {
        if (!UseRetainedRenderList)
        {
            return UiDirtyDrawDecisionReason.RetainedDisabled;
        }

        if (!UseDirtyRegionRendering)
        {
            return UiDirtyDrawDecisionReason.DirtyRegionRenderingDisabled;
        }

        if (_diagnosticCaptureFullClearPending)
        {
            return UiDirtyDrawDecisionReason.DiagnosticCapture;
        }

        if (_fullRedrawSettleFramesRemaining > 0)
        {
            return UiDirtyDrawDecisionReason.FullRedrawSettle;
        }

        if (_dirtyRegions.IsFullFrameDirty)
        {
            return UiDirtyDrawDecisionReason.FullDirty;
        }

        if (_dirtyRegions.RegionCount == 0)
        {
            return UiDirtyDrawDecisionReason.NoRegions;
        }

        return ShouldUsePartialDirtyRedraw(_dirtyRegions.RegionCount, _dirtyRegions.GetDirtyAreaCoverage())
            ? UiDirtyDrawDecisionReason.Partial
            : UiDirtyDrawDecisionReason.ThresholdFallback;
    }

    private void RecordFullRetainedDrawWithoutFullClearIfNeeded()
    {
        if (!_currentDrawPerformedFullClear)
        {
            _fullRetainedDrawWithoutFullClearCount++;
        }
    }

    private void SynchronizeRetainedRenderListForDrawIfNeeded()
    {
        if (_renderListNeedsFullRebuild || _dirtyRenderQueue.Count > 0 || _dirtyRenderSet.Count > 0)
        {
            EnsureVisualIndexCurrent();
            SynchronizeRetainedRenderList();
        }
    }

    private void DrawSoftwareCursor(SpriteBatch spriteBatch, Vector2 pointer)
    {
        var cursor = ResolveCursorFromHoveredElement();
        var color = SoftwareCursorColor;
        const float thickness = 2f;

        switch (cursor)
        {
            case "IBeam":
                DrawIBeamCursor(spriteBatch, pointer, color, thickness);
                break;
            case "Hand":
                DrawHandCursor(spriteBatch, pointer, color, thickness);
                break;
            case "Cross":
                DrawCrossCursor(spriteBatch, pointer, color, thickness);
                break;
            default:
                DrawArrowCursor(spriteBatch, pointer, color, thickness);
                break;
        }
    }

    private string ResolveCursorFromHoveredElement()
    {
        for (var current = _inputState.HoveredElement; current != null; current = current.VisualParent)
        {
            if (current is FrameworkElement fe)
            {
                var cursor = fe.Cursor;
                if (!string.IsNullOrEmpty(cursor))
                {
                    return cursor;
                }
            }
        }

        return "Arrow";
    }

    private static void DrawArrowCursor(SpriteBatch spriteBatch, Vector2 pointer, Color color, float thickness)
    {
        var size = MathF.Max(8f, 8f);
        var x = (int)pointer.X;
        var y = (int)pointer.Y;

        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x + 2, y, size, thickness), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x + 3, y + 2, size - 2, thickness), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x + 4, y + 4, size - 4, thickness), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x + 5, y + 6, size - 6, thickness), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x + 6, y + 8, size - 8, thickness), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x + 1, y + 10, size + 2, thickness), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x, y + 12, size + 4, thickness), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x - 1, y + 14, size + 6, thickness), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x - 2, y + 16, 2, thickness), color, 1f);
    }

    private static void DrawIBeamCursor(SpriteBatch spriteBatch, Vector2 pointer, Color color, float thickness)
    {
        var stemWidth = MathF.Max(2f, thickness);
        var serifWidth = 6f;
        var serifHeight = 2f;
        var stemHeight = 14f;

        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(pointer.X - serifWidth / 2f, pointer.Y - stemHeight / 2f - serifHeight, serifWidth, serifHeight), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(pointer.X - stemWidth / 2f, pointer.Y - stemHeight / 2f, stemWidth, stemHeight), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(pointer.X - serifWidth / 2f, pointer.Y + stemHeight / 2f, serifWidth, serifHeight), color, 1f);
    }

    private static void DrawHandCursor(SpriteBatch spriteBatch, Vector2 pointer, Color color, float thickness)
    {
        var x = (int)pointer.X;
        var y = (int)pointer.Y;

        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x, y, 3, 5), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x + 3, y - 1, 3, 6), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x + 6, y, 3, 7), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x + 9, y, 3, 8), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x + 8, y + 7, 5, 3), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x - 1, y + 4, 4, 6), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x - 2, y + 8, 14, 2), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x - 2, y + 10, 12, 2), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x - 2, y + 12, 10, 2), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x - 2, y + 14, 8, 2), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(x - 2, y + 16, 6, 2), color, 1f);
    }

    private static void DrawCrossCursor(SpriteBatch spriteBatch, Vector2 pointer, Color color, float thickness)
    {
        var size = MathF.Max(6f, 6f);
        var half = size / 2f;

        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(pointer.X - half, pointer.Y - 1f, size, thickness), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(pointer.X - 1f, pointer.Y - half, thickness, size), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(pointer.X - 1f, pointer.Y - 1f, thickness, thickness), Color.Black, 1f);
    }

    private static string _lastAppliedSystemCursor = string.Empty;

    private void ApplySystemCursor(string cursorType)
    {
        if (string.Equals(_lastAppliedSystemCursor, cursorType, StringComparison.Ordinal))
        {
            return;
        }

        var cursor = cursorType switch
        {
            "IBeam" => MouseCursor.IBeam,
            "Hand" => MouseCursor.Hand,
            "Cross" => MouseCursor.Crosshair,
            "Wait" => MouseCursor.Wait,
            "No" => MouseCursor.No,
            "SizeAll" => MouseCursor.SizeAll,
            "SizeNESW" => MouseCursor.SizeNESW,
            "SizeNS" => MouseCursor.SizeNS,
            "SizeNWSE" => MouseCursor.SizeNWSE,
            "SizeWE" => MouseCursor.SizeWE,
            _ => MouseCursor.Arrow
        };

        _lastAppliedSystemCursor = cursorType;
        Mouse.SetCursor(cursor);
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
            if (_dirtyRenderSet.Count == 0)
            {
                // When no roots were synchronized in this draw phase and nothing remains queued,
                // any surviving render flags were already synchronized earlier in the frame and
                // must not leak past cleanup.
                _visualRoot.ClearRenderInvalidationRecursive();
            }

            return;
        }

        for (var i = 0; i < _lastSynchronizedDirtyRenderRoots.Count; i++)
        {
            _lastSynchronizedDirtyRenderRoots[i].ClearRenderInvalidationRecursive();
        }

        BuildPendingDirtyAncestorCounts();
        for (var i = 0; i < _lastSynchronizedDirtyRenderRoots.Count; i++)
        {
            ClearRenderInvalidationAncestorChain(_lastSynchronizedDirtyRenderRoots[i].GetInvalidationParent());
        }

        _pendingDirtyAncestorCounts.Clear();
    }

    private void BuildPendingDirtyAncestorCounts()
    {
        _pendingDirtyAncestorCounts.Clear();

        for (var i = 0; i < _lastSynchronizedDirtyRenderRoots.Count; i++)
        {
            AddPendingDirtyAncestorChain(_lastSynchronizedDirtyRenderRoots[i]);
        }

        if (_dirtyRenderSet.Count == 0)
        {
            return;
        }

        foreach (var dirtyVisual in _dirtyRenderSet)
        {
            AddPendingDirtyAncestorChain(dirtyVisual);
        }
    }

    private void AddPendingDirtyAncestorChain(UIElement dirtyVisual)
    {
        for (var current = dirtyVisual.GetInvalidationParent(); current != null; current = current.GetInvalidationParent())
        {
            _pendingDirtyAncestorCounts.TryGetValue(current, out var count);
            _pendingDirtyAncestorCounts[current] = count + 1;
        }
    }

    private void ClearRenderInvalidationAncestorChain(UIElement? visual)
    {
        for (var current = visual; current != null; current = current.GetInvalidationParent())
        {
            if (!_pendingDirtyAncestorCounts.TryGetValue(current, out var remainingCount))
            {
                current.ClearRenderInvalidationShallow();
                continue;
            }

            remainingCount--;
            if (remainingCount > 0)
            {
                _pendingDirtyAncestorCounts[current] = remainingCount;
                continue;
            }

            _pendingDirtyAncestorCounts.Remove(current);
            current.ClearRenderInvalidationShallow();
        }
    }
}
