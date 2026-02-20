using System;
using System.Diagnostics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double DirtyRegionEffectivenessIdleFlushMs = 900d;
    private const int DirtyRegionEffectivenessMinDrawSamples = 4;
    private static readonly bool IsDirtyRegionEffectivenessDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_DIRTY_REGION_EFFECTIVENESS_LOGS"), "1", StringComparison.Ordinal);

    private int _dirtyDiagDrawSampleCount;
    private int _dirtyDiagPartialEligibleCount;
    private int _dirtyDiagPartialUsedCount;
    private int _dirtyDiagFullFallbackCount;
    private int _dirtyDiagFallbackViewportCount;
    private int _dirtyDiagFallbackVisualStructureCount;
    private int _dirtyDiagFallbackDetachedSourceCount;
    private int _dirtyDiagFallbackRebuildCount;
    private int _dirtyDiagFallbackFragmentationCount;
    private int _dirtyDiagCandidateRegionAddCount;
    private int _dirtyDiagMergeInputCount;
    private int _dirtyDiagMergeSavedCount;
    private long _dirtyDiagLastActivityTimestamp;
    private int _dirtyDiagLastKnownFragmentationFallbackCount;
    private bool _dirtyDiagPreDrawEligible;
    private bool _dirtyDiagPreDrawFullFallback;
    private int _dirtyDiagPreDrawRegionCount;

    private void ObserveDirtyRegionCandidateAdded()
    {
        if (!IsDirtyRegionEffectivenessDiagnosticsEnabled)
        {
            return;
        }

        _dirtyDiagCandidateRegionAddCount++;
        _dirtyDiagLastActivityTimestamp = Stopwatch.GetTimestamp();
    }

    private void ObserveDirtyRegionFallbackViewportChange()
    {
        ObserveDirtyRegionFallbackReason(ref _dirtyDiagFallbackViewportCount);
    }

    private void ObserveDirtyRegionFallbackVisualStructureChange()
    {
        ObserveDirtyRegionFallbackReason(ref _dirtyDiagFallbackVisualStructureCount);
    }

    private void ObserveDirtyRegionFallbackDetachedSource()
    {
        ObserveDirtyRegionFallbackReason(ref _dirtyDiagFallbackDetachedSourceCount);
    }

    private void ObserveDirtyRegionFallbackRetainedRebuild()
    {
        ObserveDirtyRegionFallbackReason(ref _dirtyDiagFallbackRebuildCount);
    }

    private void ObserveDirtyRegionBeforeDraw()
    {
        if (!IsDirtyRegionEffectivenessDiagnosticsEnabled)
        {
            return;
        }

        _dirtyDiagPreDrawRegionCount = _dirtyRegions.RegionCount;
        _dirtyDiagPreDrawEligible = UseRetainedRenderList && UseDirtyRegionRendering && !_dirtyRegions.IsFullFrameDirty && _dirtyDiagPreDrawRegionCount > 0;
        _dirtyDiagPreDrawFullFallback = UseRetainedRenderList && UseDirtyRegionRendering && _dirtyRegions.IsFullFrameDirty;
    }

    private void ObserveDirtyRegionAfterDraw()
    {
        if (!IsDirtyRegionEffectivenessDiagnosticsEnabled)
        {
            return;
        }

        var fragmentationDelta = _dirtyRegions.FullRedrawFallbackCount - _dirtyDiagLastKnownFragmentationFallbackCount;
        if (fragmentationDelta > 0)
        {
            _dirtyDiagFallbackFragmentationCount += fragmentationDelta;
        }

        _dirtyDiagLastKnownFragmentationFallbackCount = _dirtyRegions.FullRedrawFallbackCount;
        if (_dirtyDiagPreDrawEligible || _dirtyDiagPreDrawFullFallback || _dirtyDiagCandidateRegionAddCount > 0 || fragmentationDelta > 0)
        {
            _dirtyDiagDrawSampleCount++;
            if (_dirtyDiagPreDrawEligible)
            {
                _dirtyDiagPartialEligibleCount++;
                if (LastDrawUsedPartialRedraw)
                {
                    _dirtyDiagPartialUsedCount++;
                }
            }

            if (_dirtyDiagPreDrawFullFallback)
            {
                _dirtyDiagFullFallbackCount++;
            }

            if (_dirtyDiagCandidateRegionAddCount > 0)
            {
                _dirtyDiagMergeInputCount += _dirtyDiagCandidateRegionAddCount;
                _dirtyDiagMergeSavedCount += Math.Max(0, _dirtyDiagCandidateRegionAddCount - _dirtyDiagPreDrawRegionCount);
            }

            _dirtyDiagLastActivityTimestamp = Stopwatch.GetTimestamp();
        }

        _dirtyDiagCandidateRegionAddCount = 0;
        _dirtyDiagPreDrawRegionCount = 0;
        _dirtyDiagPreDrawEligible = false;
        _dirtyDiagPreDrawFullFallback = false;
        TryFlushDirtyRegionEffectivenessDiagnostics();
    }

    private void ObserveDirtyRegionAfterUpdate()
    {
        if (!IsDirtyRegionEffectivenessDiagnosticsEnabled)
        {
            return;
        }

        TryFlushDirtyRegionEffectivenessDiagnostics();
    }

    private void TryFlushDirtyRegionEffectivenessDiagnostics()
    {
        if (_dirtyDiagDrawSampleCount < DirtyRegionEffectivenessMinDrawSamples || _dirtyDiagLastActivityTimestamp == 0)
        {
            return;
        }

        var idleMs = Stopwatch.GetElapsedTime(_dirtyDiagLastActivityTimestamp).TotalMilliseconds;
        if (idleMs < DirtyRegionEffectivenessIdleFlushMs)
        {
            return;
        }

        var partialSuccessPct = _dirtyDiagPartialEligibleCount > 0
            ? (_dirtyDiagPartialUsedCount * 100d) / _dirtyDiagPartialEligibleCount
            : 0d;
        var mergeRatioPct = _dirtyDiagMergeInputCount > 0
            ? (_dirtyDiagMergeSavedCount * 100d) / _dirtyDiagMergeInputCount
            : 0d;
        var totalFallbacks =
            _dirtyDiagFallbackViewportCount +
            _dirtyDiagFallbackVisualStructureCount +
            _dirtyDiagFallbackDetachedSourceCount +
            _dirtyDiagFallbackRebuildCount +
            _dirtyDiagFallbackFragmentationCount;

        var summary =
            $"[DirtyRegionEffectiveness] draws={_dirtyDiagDrawSampleCount} partialEligible={_dirtyDiagPartialEligibleCount} partialUsed={_dirtyDiagPartialUsedCount} partialSuccess={partialSuccessPct:0.0}% " +
            $"merge(candidates={_dirtyDiagMergeInputCount} saved={_dirtyDiagMergeSavedCount} ratio={mergeRatioPct:0.0}%) " +
            $"fullFallbacks(total={_dirtyDiagFullFallbackCount}, causes={totalFallbacks}, viewport={_dirtyDiagFallbackViewportCount}, visualStructure={_dirtyDiagFallbackVisualStructureCount}, detached={_dirtyDiagFallbackDetachedSourceCount}, rebuild={_dirtyDiagFallbackRebuildCount}, fragmentation={_dirtyDiagFallbackFragmentationCount})";

        Debug.WriteLine(summary);
        Console.WriteLine(summary);
        ResetDirtyRegionEffectivenessDiagnostics();
    }

    private void ObserveDirtyRegionFallbackReason(ref int counter)
    {
        if (!IsDirtyRegionEffectivenessDiagnosticsEnabled)
        {
            return;
        }

        counter++;
        _dirtyDiagLastActivityTimestamp = Stopwatch.GetTimestamp();
    }

    private void ResetDirtyRegionEffectivenessDiagnostics()
    {
        _dirtyDiagDrawSampleCount = 0;
        _dirtyDiagPartialEligibleCount = 0;
        _dirtyDiagPartialUsedCount = 0;
        _dirtyDiagFullFallbackCount = 0;
        _dirtyDiagFallbackViewportCount = 0;
        _dirtyDiagFallbackVisualStructureCount = 0;
        _dirtyDiagFallbackDetachedSourceCount = 0;
        _dirtyDiagFallbackRebuildCount = 0;
        _dirtyDiagFallbackFragmentationCount = 0;
        _dirtyDiagCandidateRegionAddCount = 0;
        _dirtyDiagMergeInputCount = 0;
        _dirtyDiagMergeSavedCount = 0;
        _dirtyDiagLastActivityTimestamp = 0L;
        _dirtyDiagPreDrawEligible = false;
        _dirtyDiagPreDrawFullFallback = false;
        _dirtyDiagPreDrawRegionCount = 0;
        _dirtyDiagLastKnownFragmentationFallbackCount = _dirtyRegions.FullRedrawFallbackCount;
    }
}