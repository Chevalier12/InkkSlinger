using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private void ResetUpdatePhaseDiagnostics()
    {
        _lastUpdatePhaseOrder.Clear();
        LastInputPhaseMs = 0d;
        LastBindingPhaseMs = 0d;
        LastLayoutPhaseMs = 0d;
        LastAnimationPhaseMs = 0d;
        LastRenderSchedulingPhaseMs = 0d;
        LastDeferredOperationCount = 0;
        LayoutPasses = 0;
    }

    private double ExecuteUpdatePhase(UiUpdatePhase phase, Action action)
    {
        var phaseStart = Stopwatch.GetTimestamp();
        _lastUpdatePhaseOrder.Add(phase);
        action();
        return Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds;
    }

    private void RunBindingAndDeferredPhase()
    {
        LastDeferredOperationCount = Dispatcher.DrainDeferredOperations();
    }

    private void RunLayoutPhase(Viewport viewport)
    {
        if (_layoutRoot == null)
        {
            LayoutSkippedFrameCount++;
            return;
        }

        var viewportChanged = !_hasLastLayoutViewport || !AreViewportsEqual(_lastLayoutViewport, viewport);
        if (viewportChanged)
        {
            _lastLayoutViewport = viewport;
            _hasLastLayoutViewport = true;
            _hasMeasureInvalidation = true;
            _hasArrangeInvalidation = true;
            _mustDrawNextFrame = true;
        }

        var shouldRunLayout = !_hasCompletedInitialLayout ||
                              viewportChanged ||
                              _hasMeasureInvalidation ||
                              _hasArrangeInvalidation ||
                              _layoutRoot.NeedsMeasure ||
                              _layoutRoot.NeedsArrange;
        if (!shouldRunLayout)
        {
            LayoutSkippedFrameCount++;
            return;
        }

        _layoutRoot.Measure(new Vector2(viewport.Width, viewport.Height));
        _layoutRoot.Arrange(new LayoutRect(0f, 0f, viewport.Width, viewport.Height));
        LayoutPasses = 1;
        _hasCompletedInitialLayout = true;
        LayoutExecutedFrameCount++;
    }

    private static void RunAnimationPhase(GameTime gameTime)
    {
        AnimationManager.Current.Update(gameTime);
    }

    private void RunRenderSchedulingPhase(Viewport viewport)
    {
        SyncDirtyRegionViewport(viewport);
        if (!UseRetainedRenderList)
        {
            return;
        }

        SynchronizeRetainedRenderList();
    }
}
