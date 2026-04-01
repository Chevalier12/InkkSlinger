using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        _lastHottestLayoutMeasureElementMs = 0d;
        _lastHottestLayoutArrangeElementType = "none";
        _lastHottestLayoutArrangeElementName = string.Empty;
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

        CaptureLayoutSamples(_layoutRoot, _layoutSamplesBeforeMeasure);
        RunLayoutUntilStable(new Vector2(viewport.Width, viewport.Height));
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
            CaptureLayoutSamples(_layoutRoot, _layoutSamplesAfterMeasure);
            RecordLayoutMeasureTelemetry(_layoutSamplesBeforeMeasure, _layoutSamplesAfterMeasure);

            _layoutRoot.Arrange(new LayoutRect(0f, 0f, viewportSize.X, viewportSize.Y));
            CaptureLayoutSamples(_layoutRoot, _layoutSamplesAfterArrange);
            RecordLayoutArrangeTelemetry(_layoutSamplesAfterMeasure, _layoutSamplesAfterArrange);
            LayoutPasses++;

            if (!HasInvalidLayout(_layoutRoot))
            {
                return;
            }

            CaptureLayoutSamples(_layoutRoot, _layoutSamplesBeforeMeasure);
        }
    }

    private static bool HasInvalidLayout(FrameworkElement element)
    {
        if (element.NeedsMeasure || element.NeedsArrange)
        {
            return true;
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
        SyncDirtyRegionViewport(viewport);
        if (!UseRetainedRenderList)
        {
            return;
        }

        EnsureVisualIndexCurrent();
        SynchronizeRetainedRenderList();
    }

    private void CaptureLayoutSamples(FrameworkElement root, Dictionary<FrameworkElement, LayoutElementSample> samples)
    {
        samples.Clear();
        _layoutSampleTraversalStack.Clear();
        _layoutSampleTraversalStack.Push(root);

        while (_layoutSampleTraversalStack.Count > 0)
        {
            var current = _layoutSampleTraversalStack.Pop();
            if (current is FrameworkElement frameworkElement)
            {
                samples[frameworkElement] = new LayoutElementSample(
                    frameworkElement.MeasureElapsedTicksForTests,
                    frameworkElement.MeasureExclusiveElapsedTicksForTests,
                    frameworkElement.ArrangeElapsedTicksForTests);
            }

            foreach (var child in current.GetVisualChildren())
            {
                _layoutSampleTraversalStack.Push(child);
            }
        }
    }

    private void RecordLayoutMeasureTelemetry(
        IReadOnlyDictionary<FrameworkElement, LayoutElementSample> beforeLayout,
        IReadOnlyDictionary<FrameworkElement, LayoutElementSample> afterMeasure)
    {
        long totalMeasureTicks = 0L;
        long totalMeasureExclusiveTicks = 0L;
        FrameworkElement? hottestMeasureElement = null;
        long hottestMeasureTicks = 0L;

        foreach (var pair in afterMeasure)
        {
            var before = beforeLayout.TryGetValue(pair.Key, out var beforeSample)
                ? beforeSample
                : default;
            var measureDelta = Math.Max(0L, pair.Value.MeasureElapsedTicks - before.MeasureElapsedTicks);
            var measureExclusiveDelta = Math.Max(0L, pair.Value.MeasureExclusiveElapsedTicks - before.MeasureExclusiveElapsedTicks);
            totalMeasureTicks += measureDelta;
            totalMeasureExclusiveTicks += measureExclusiveDelta;
            if (measureDelta > hottestMeasureTicks)
            {
                hottestMeasureTicks = measureDelta;
                hottestMeasureElement = pair.Key;
            }
        }

        _lastLayoutMeasureWorkMs = TicksToMilliseconds(totalMeasureTicks);
        _lastLayoutMeasureExclusiveWorkMs = TicksToMilliseconds(totalMeasureExclusiveTicks);
        _lastHottestLayoutMeasureElementType = hottestMeasureElement?.GetType().Name ?? "none";
        _lastHottestLayoutMeasureElementName = hottestMeasureElement?.Name ?? string.Empty;
        _lastHottestLayoutMeasureElementMs = TicksToMilliseconds(hottestMeasureTicks);
    }

    private void RecordLayoutArrangeTelemetry(
        IReadOnlyDictionary<FrameworkElement, LayoutElementSample> afterMeasure,
        IReadOnlyDictionary<FrameworkElement, LayoutElementSample> afterArrange)
    {
        long totalArrangeTicks = 0L;
        FrameworkElement? hottestArrangeElement = null;
        long hottestArrangeTicks = 0L;

        foreach (var pair in afterArrange)
        {
            var before = afterMeasure.TryGetValue(pair.Key, out var beforeSample)
                ? beforeSample
                : default;
            var arrangeDelta = Math.Max(0L, pair.Value.ArrangeElapsedTicks - before.ArrangeElapsedTicks);
            totalArrangeTicks += arrangeDelta;
            if (arrangeDelta > hottestArrangeTicks)
            {
                hottestArrangeTicks = arrangeDelta;
                hottestArrangeElement = pair.Key;
            }
        }

        _lastLayoutArrangeWorkMs = TicksToMilliseconds(totalArrangeTicks);
        _lastHottestLayoutArrangeElementType = hottestArrangeElement?.GetType().Name ?? "none";
        _lastHottestLayoutArrangeElementName = hottestArrangeElement?.Name ?? string.Empty;
        _lastHottestLayoutArrangeElementMs = TicksToMilliseconds(hottestArrangeTicks);
    }

    private readonly record struct LayoutElementSample(
        long MeasureElapsedTicks,
        long MeasureExclusiveElapsedTicks,
        long ArrangeElapsedTicks);

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }
}

