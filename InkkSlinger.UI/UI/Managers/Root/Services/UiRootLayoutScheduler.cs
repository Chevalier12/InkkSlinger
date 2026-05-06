using System;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private void ResetUpdatePhaseState()
    {
        _telemetryResetUpdatePhaseStateCallCount++;
        _lastUpdatePhaseOrder.Clear();
        LastInputPhaseMs = 0d;
        LastBindingPhaseMs = 0d;
        LastLayoutPhaseMs = 0d;
        LastAnimationPhaseMs = 0d;
        LastRenderSchedulingPhaseMs = 0d;
        LastDeferredOperationCount = 0;
        LayoutPasses = 0;
        _lastFrameUpdateParticipantCount = 0;
        _lastFrameUpdateParticipantRefreshCount = 0;
        _lastFrameUpdateParticipantRefreshMs = 0d;
        _lastFrameUpdateParticipantUpdateMs = 0d;
        _lastHottestFrameUpdateParticipantType = "none";
        _lastHottestFrameUpdateParticipantMs = 0d;
        _lastLayoutMeasureWorkMs = 0d;
        _lastLayoutMeasureExclusiveWorkMs = 0d;
        _lastLayoutArrangeWorkMs = 0d;
        _lastHottestLayoutMeasureElementType = "none";
        _lastHottestLayoutMeasureElementName = string.Empty;
        _lastHottestLayoutMeasureElementPath = "none";
        _lastHottestLayoutMeasureElementMs = 0d;
        _lastHottestLayoutArrangeElementType = "none";
        _lastHottestLayoutArrangeElementName = string.Empty;
        _lastHottestLayoutArrangeElementPath = "none";
        _lastHottestLayoutArrangeElementMs = 0d;
        _lastDirtyRootCountAfterCoalescing = 0;
        _lastRetainedTraversalCount = 0;
        _lastDirtyRegionTraversalCount = 0;
        _lastAncestorMetadataRefreshNodeCount = 0;
        _lastRetainedQueueCompactionMs = 0d;
        _lastRetainedCandidateCoalescingMs = 0d;
        _lastRetainedSubtreeUpdateMs = 0d;
        _lastRetainedShallowSyncMs = 0d;
        _lastRetainedDeepSyncMs = 0d;
        _lastRetainedAncestorRefreshMs = 0d;
        _lastRetainedForceDeepSyncCount = 0;
        _lastRetainedForcedDeepDowngradeBlockedCount = 0;
        _lastRetainedShallowSuccessCount = 0;
        _lastRetainedShallowRejectRenderStateCount = 0;
        _lastRetainedShallowRejectVisibilityCount = 0;
        _lastRetainedShallowRejectStructureCount = 0;
        _lastRetainedOverlapForcedDeepCount = 0;
        _lastMenuScopeBuildCount = 0;
        _lastOverlayRegistryScanCount = 0;
        _lastOverlayRegistryHitCount = 0;
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

        var viewportChanged = !_hasLastLayoutViewport || !RetainedRenderController.AreViewportsEqual(_lastLayoutViewport, viewport);
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
                              _layoutRoot.NeedsArrange ||
                              HasInvalidLayout(_layoutRoot);
        if (!shouldRunLayout)
        {
            LayoutSkippedFrameCount++;
            return;
        }

        FrameworkElement.ResetFrameTimingForTests();
        RunLayoutUntilStable(new Vector2(viewport.Width, viewport.Height));
        RecordLayoutTelemetryFromFrameSnapshot(FrameworkElement.GetFrameTimingSnapshotForTests());
        _layoutGeneration++;
        RefreshPointerTargetsAfterLayoutMutation();
        _hasCompletedInitialLayout = true;
        LayoutExecutedFrameCount++;
    }

    private void RunLayoutUntilStable(Vector2 viewportSize)
    {
        const int maxLayoutPasses = 8;
        LayoutPasses = 0;

        for (var pass = 0; pass < maxLayoutPasses; pass++)
        {
            _layoutRoot!.Measure(viewportSize);
            _layoutRoot.Arrange(new LayoutRect(0f, 0f, viewportSize.X, viewportSize.Y));
            LayoutPasses++;

            if (HasInvalidLayout(_layoutRoot))
            {
                _layoutRoot.UpdateLayout();
            }

            if (!HasInvalidLayout(_layoutRoot))
            {
                return;
            }
        }
    }

    private static bool HasInvalidLayout(FrameworkElement element)
    {
        if (element.NeedsMeasure || element.NeedsArrange)
        {
            return true;
        }

        if (!element.SubtreeDirty)
        {
            return false;
        }

        foreach (var child in element.GetVisualChildren())
        {
            if (child is FrameworkElement frameworkChild && HasInvalidLayout(frameworkChild))
            {
                return true;
            }
        }

        return false;
    }
    private static void RunAnimationPhase(GameTime gameTime)
    {
        AnimationManager.Current.Update(gameTime);
    }

    private void RunRenderSchedulingPhase(Viewport viewport)
    {
        if (!UseRetainedRenderList)
        {
            _retainedRender.SyncDirtyRegionViewport(viewport);
            return;
        }

        _retainedRender.Sync(viewport);
    }

    private void RecordLayoutTelemetryFromFrameSnapshot(FrameworkLayoutTimingSnapshot snapshot)
    {
        _lastLayoutMeasureWorkMs = TicksToMilliseconds(snapshot.MeasureElapsedTicks);
        _lastLayoutMeasureExclusiveWorkMs = TicksToMilliseconds(snapshot.MeasureExclusiveElapsedTicks);
        _lastHottestLayoutMeasureElementType = snapshot.HottestMeasureElementType;
        _lastHottestLayoutMeasureElementName = snapshot.HottestMeasureElementName;
        _lastHottestLayoutMeasureElementPath = snapshot.HottestMeasureElementPath;
        _lastHottestLayoutMeasureElementMs = TicksToMilliseconds(snapshot.HottestMeasureElapsedTicks);
        _lastLayoutArrangeWorkMs = TicksToMilliseconds(snapshot.ArrangeElapsedTicks);
        _lastHottestLayoutArrangeElementType = snapshot.HottestArrangeElementType;
        _lastHottestLayoutArrangeElementName = snapshot.HottestArrangeElementName;
        _lastHottestLayoutArrangeElementPath = snapshot.HottestArrangeElementPath;
        _lastHottestLayoutArrangeElementMs = TicksToMilliseconds(snapshot.HottestArrangeElapsedTicks);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }
}

