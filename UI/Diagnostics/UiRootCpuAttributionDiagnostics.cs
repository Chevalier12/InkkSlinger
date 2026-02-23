using System;
using System.Diagnostics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double CpuAttributionWindowMs = 1200d;
    private const int CpuAttributionMinSamples = 20;
    private static readonly bool IsCpuAttributionDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_CPU_ATTRIBUTION_LOGS"), "1", StringComparison.Ordinal);

    private int _cpuAttrUpdateSampleCount;
    private int _cpuAttrDrawSampleCount;
    private double _cpuAttrUpdateMsTotal;
    private double _cpuAttrInputMsTotal;
    private double _cpuAttrBindingMsTotal;
    private double _cpuAttrLayoutMsTotal;
    private double _cpuAttrAnimationMsTotal;
    private double _cpuAttrRenderSchedulingMsTotal;
    private double _cpuAttrDrawMsTotal;
    private double _cpuAttrPointerResolveMsTotal;
    private double _cpuAttrPointerRouteMsTotal;
    private double _cpuAttrHoverMsTotal;
    private double _cpuAttrDispatchMsTotal;
    private double _cpuAttrVisualUpdateMsTotal;
    private double _cpuAttrUpdateMsMax;
    private double _cpuAttrDrawMsMax;
    private double _cpuAttrInputMsMax;
    private double _cpuAttrLayoutMsMax;
    private double _cpuAttrBindingMsMax;
    private double _cpuAttrPointerResolveMsMax;
    private double _cpuAttrPointerRouteMsMax;
    private double _cpuAttrDispatchMsMax;
    private long _cpuAttrFirstTimestamp;
    private TimeSpan _cpuAttrFirstProcessCpuTime;
    private long _cpuAttrFirstAllocatedBytes;
    private int _cpuAttrFirstGen0Collections;
    private int _cpuAttrFirstGen1Collections;
    private int _cpuAttrFirstGen2Collections;
    private int _cpuAttrFirstMeasureInvalidationCount;
    private int _cpuAttrFirstArrangeInvalidationCount;
    private int _cpuAttrFirstRenderInvalidationCount;
    private int _cpuAttrFirstCacheHitCount;
    private int _cpuAttrFirstCacheMissCount;
    private int _cpuAttrFirstCacheRebuildCount;

    private void ObserveCpuAttributionAfterUpdate()
    {
        if (!IsCpuAttributionDiagnosticsEnabled)
        {
            return;
        }

        EnsureCpuAttributionWindowStarted();

        _cpuAttrUpdateSampleCount++;
        _cpuAttrUpdateMsTotal += LastUpdateMs;
        _cpuAttrInputMsTotal += LastInputPhaseMs;
        _cpuAttrBindingMsTotal += LastBindingPhaseMs;
        _cpuAttrLayoutMsTotal += LastLayoutPhaseMs;
        _cpuAttrAnimationMsTotal += LastAnimationPhaseMs;
        _cpuAttrRenderSchedulingMsTotal += LastRenderSchedulingPhaseMs;
        _cpuAttrPointerResolveMsTotal += _lastInputPointerTargetResolveMs;
        _cpuAttrPointerRouteMsTotal += _lastInputPointerRouteMs;
        _cpuAttrHoverMsTotal += _lastInputHoverUpdateMs;
        _cpuAttrDispatchMsTotal += _lastInputDispatchMs;
        _cpuAttrVisualUpdateMsTotal += _lastVisualUpdateMs;

        _cpuAttrUpdateMsMax = Math.Max(_cpuAttrUpdateMsMax, LastUpdateMs);
        _cpuAttrInputMsMax = Math.Max(_cpuAttrInputMsMax, LastInputPhaseMs);
        _cpuAttrLayoutMsMax = Math.Max(_cpuAttrLayoutMsMax, LastLayoutPhaseMs);
        _cpuAttrBindingMsMax = Math.Max(_cpuAttrBindingMsMax, LastBindingPhaseMs);
        _cpuAttrPointerResolveMsMax = Math.Max(_cpuAttrPointerResolveMsMax, _lastInputPointerTargetResolveMs);
        _cpuAttrPointerRouteMsMax = Math.Max(_cpuAttrPointerRouteMsMax, _lastInputPointerRouteMs);
        _cpuAttrDispatchMsMax = Math.Max(_cpuAttrDispatchMsMax, _lastInputDispatchMs);

        TryFlushCpuAttributionDiagnostics();
    }

    private void ObserveCpuAttributionAfterDraw()
    {
        if (!IsCpuAttributionDiagnosticsEnabled)
        {
            return;
        }

        if (_cpuAttrFirstTimestamp == 0L)
        {
            return;
        }

        _cpuAttrDrawSampleCount++;
        _cpuAttrDrawMsTotal += LastDrawMs;
        _cpuAttrDrawMsMax = Math.Max(_cpuAttrDrawMsMax, LastDrawMs);

        TryFlushCpuAttributionDiagnostics();
    }

    private void EnsureCpuAttributionWindowStarted()
    {
        if (_cpuAttrFirstTimestamp != 0L)
        {
            return;
        }

        _cpuAttrFirstTimestamp = Stopwatch.GetTimestamp();
        _cpuAttrFirstProcessCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        _cpuAttrFirstAllocatedBytes = GC.GetTotalAllocatedBytes(false);
        _cpuAttrFirstGen0Collections = GC.CollectionCount(0);
        _cpuAttrFirstGen1Collections = GC.CollectionCount(1);
        _cpuAttrFirstGen2Collections = GC.CollectionCount(2);
        _cpuAttrFirstMeasureInvalidationCount = MeasureInvalidationCount;
        _cpuAttrFirstArrangeInvalidationCount = ArrangeInvalidationCount;
        _cpuAttrFirstRenderInvalidationCount = RenderInvalidationCount;
        _cpuAttrFirstCacheHitCount = CacheHitCount;
        _cpuAttrFirstCacheMissCount = CacheMissCount;
        _cpuAttrFirstCacheRebuildCount = CacheRebuildCount;
    }

    private void TryFlushCpuAttributionDiagnostics()
    {
        if (_cpuAttrFirstTimestamp == 0L)
        {
            return;
        }

        var windowMs = Stopwatch.GetElapsedTime(_cpuAttrFirstTimestamp).TotalMilliseconds;
        if (windowMs < CpuAttributionWindowMs)
        {
            return;
        }

        if (_cpuAttrUpdateSampleCount < CpuAttributionMinSamples)
        {
            ResetCpuAttributionDiagnostics();
            return;
        }

        var elapsedSeconds = Math.Max(0.001d, windowMs / 1000d);
        var cpuTimeSeconds = (Process.GetCurrentProcess().TotalProcessorTime - _cpuAttrFirstProcessCpuTime).TotalSeconds;
        var logicalProcessors = Math.Max(1, Environment.ProcessorCount);
        var processCpuPct = Math.Max(0d, (cpuTimeSeconds / elapsedSeconds) * 100d / logicalProcessors);

        var avgUpdateMs = _cpuAttrUpdateMsTotal / _cpuAttrUpdateSampleCount;
        var avgInputMs = _cpuAttrInputMsTotal / _cpuAttrUpdateSampleCount;
        var avgBindingMs = _cpuAttrBindingMsTotal / _cpuAttrUpdateSampleCount;
        var avgLayoutMs = _cpuAttrLayoutMsTotal / _cpuAttrUpdateSampleCount;
        var avgAnimationMs = _cpuAttrAnimationMsTotal / _cpuAttrUpdateSampleCount;
        var avgRenderSchedulingMs = _cpuAttrRenderSchedulingMsTotal / _cpuAttrUpdateSampleCount;
        var avgDrawMs = _cpuAttrDrawSampleCount > 0 ? _cpuAttrDrawMsTotal / _cpuAttrDrawSampleCount : 0d;
        var avgPointerResolveMs = _cpuAttrPointerResolveMsTotal / _cpuAttrUpdateSampleCount;
        var avgPointerRouteMs = _cpuAttrPointerRouteMsTotal / _cpuAttrUpdateSampleCount;
        var avgHoverMs = _cpuAttrHoverMsTotal / _cpuAttrUpdateSampleCount;
        var avgDispatchMs = _cpuAttrDispatchMsTotal / _cpuAttrUpdateSampleCount;
        var avgVisualUpdateMs = _cpuAttrVisualUpdateMsTotal / _cpuAttrUpdateSampleCount;
        var avgOtherUpdateMs = Math.Max(
            0d,
            avgUpdateMs - (avgInputMs + avgBindingMs + avgLayoutMs + avgAnimationMs + avgRenderSchedulingMs));

        var allocatedBytesDelta = Math.Max(0L, GC.GetTotalAllocatedBytes(false) - _cpuAttrFirstAllocatedBytes);
        var allocatedMbPerSec = (allocatedBytesDelta / 1024d / 1024d) / elapsedSeconds;
        var gen0Delta = Math.Max(0, GC.CollectionCount(0) - _cpuAttrFirstGen0Collections);
        var gen1Delta = Math.Max(0, GC.CollectionCount(1) - _cpuAttrFirstGen1Collections);
        var gen2Delta = Math.Max(0, GC.CollectionCount(2) - _cpuAttrFirstGen2Collections);

        var measureInvalidationsDelta = Math.Max(0, MeasureInvalidationCount - _cpuAttrFirstMeasureInvalidationCount);
        var arrangeInvalidationsDelta = Math.Max(0, ArrangeInvalidationCount - _cpuAttrFirstArrangeInvalidationCount);
        var renderInvalidationsDelta = Math.Max(0, RenderInvalidationCount - _cpuAttrFirstRenderInvalidationCount);
        var cacheHitDelta = Math.Max(0, CacheHitCount - _cpuAttrFirstCacheHitCount);
        var cacheMissDelta = Math.Max(0, CacheMissCount - _cpuAttrFirstCacheMissCount);
        var cacheRebuildDelta = Math.Max(0, CacheRebuildCount - _cpuAttrFirstCacheRebuildCount);

        var dominant = ClassifyCpuAttribution(
            avgInputMs,
            avgBindingMs,
            avgLayoutMs,
            avgDrawMs,
            avgOtherUpdateMs,
            gen0Delta,
            gen1Delta,
            gen2Delta,
            allocatedMbPerSec);

        var summary =
            $"[CpuAttribution] dominant={dominant} cpu={processCpuPct:0.0}% window={windowMs:0}ms " +
            $"samples(update={_cpuAttrUpdateSampleCount},draw={_cpuAttrDrawSampleCount}) " +
            $"avg(update={avgUpdateMs:0.00}ms input={avgInputMs:0.00}ms binding={avgBindingMs:0.00}ms layout={avgLayoutMs:0.00}ms anim={avgAnimationMs:0.00}ms renderSched={avgRenderSchedulingMs:0.00}ms draw={avgDrawMs:0.00}ms otherUpd={avgOtherUpdateMs:0.00}ms) " +
            $"inputSplit(avg resolve={avgPointerResolveMs:0.00}ms route={avgPointerRouteMs:0.00}ms hover={avgHoverMs:0.00}ms dispatch={avgDispatchMs:0.00}ms visualUpd={avgVisualUpdateMs:0.00}ms; max resolve={_cpuAttrPointerResolveMsMax:0.00}ms route={_cpuAttrPointerRouteMsMax:0.00}ms dispatch={_cpuAttrDispatchMsMax:0.00}ms) " +
            $"max(update={_cpuAttrUpdateMsMax:0.00}ms input={_cpuAttrInputMsMax:0.00}ms binding={_cpuAttrBindingMsMax:0.00}ms layout={_cpuAttrLayoutMsMax:0.00}ms draw={_cpuAttrDrawMsMax:0.00}ms) " +
            $"invalidations(dMeasure={measureInvalidationsDelta},dArrange={arrangeInvalidationsDelta},dRender={renderInvalidationsDelta}) " +
            $"cache(dHit={cacheHitDelta},dMiss={cacheMissDelta},dRebuild={cacheRebuildDelta}) " +
            $"gc(alloc={allocatedMbPerSec:0.00}MB/s gen0={gen0Delta} gen1={gen1Delta} gen2={gen2Delta})";

        Debug.WriteLine(summary);
        Console.WriteLine(summary);
        ResetCpuAttributionDiagnostics();
    }

    private static string ClassifyCpuAttribution(
        double avgInputMs,
        double avgBindingMs,
        double avgLayoutMs,
        double avgDrawMs,
        double avgOtherUpdateMs,
        int gen0Delta,
        int gen1Delta,
        int gen2Delta,
        double allocatedMbPerSec)
    {
        if (gen2Delta > 0 || gen1Delta >= 2 || allocatedMbPerSec >= 40d)
        {
            return "GcBound";
        }

        var dominant = "InputBound";
        var max = avgInputMs;
        if (avgLayoutMs > max)
        {
            dominant = "LayoutBound";
            max = avgLayoutMs;
        }

        if (avgDrawMs > max)
        {
            dominant = "DrawBound";
            max = avgDrawMs;
        }

        if (avgBindingMs > max)
        {
            dominant = "BindingBound";
            max = avgBindingMs;
        }

        if (avgOtherUpdateMs > max)
        {
            dominant = "OtherUpdateBound";
        }

        if (avgInputMs > 0d &&
            avgLayoutMs > 0d &&
            avgDrawMs > 0d &&
            Math.Abs(avgInputMs - avgLayoutMs) < 0.4d &&
            Math.Abs(avgLayoutMs - avgDrawMs) < 0.4d)
        {
            return "Mixed";
        }

        return dominant;
    }

    private void ResetCpuAttributionDiagnostics()
    {
        _cpuAttrUpdateSampleCount = 0;
        _cpuAttrDrawSampleCount = 0;
        _cpuAttrUpdateMsTotal = 0d;
        _cpuAttrInputMsTotal = 0d;
        _cpuAttrBindingMsTotal = 0d;
        _cpuAttrLayoutMsTotal = 0d;
        _cpuAttrAnimationMsTotal = 0d;
        _cpuAttrRenderSchedulingMsTotal = 0d;
        _cpuAttrDrawMsTotal = 0d;
        _cpuAttrPointerResolveMsTotal = 0d;
        _cpuAttrPointerRouteMsTotal = 0d;
        _cpuAttrHoverMsTotal = 0d;
        _cpuAttrDispatchMsTotal = 0d;
        _cpuAttrVisualUpdateMsTotal = 0d;
        _cpuAttrUpdateMsMax = 0d;
        _cpuAttrDrawMsMax = 0d;
        _cpuAttrInputMsMax = 0d;
        _cpuAttrLayoutMsMax = 0d;
        _cpuAttrBindingMsMax = 0d;
        _cpuAttrPointerResolveMsMax = 0d;
        _cpuAttrPointerRouteMsMax = 0d;
        _cpuAttrDispatchMsMax = 0d;
        _cpuAttrFirstTimestamp = 0L;
        _cpuAttrFirstProcessCpuTime = TimeSpan.Zero;
        _cpuAttrFirstAllocatedBytes = 0L;
        _cpuAttrFirstGen0Collections = 0;
        _cpuAttrFirstGen1Collections = 0;
        _cpuAttrFirstGen2Collections = 0;
        _cpuAttrFirstMeasureInvalidationCount = 0;
        _cpuAttrFirstArrangeInvalidationCount = 0;
        _cpuAttrFirstRenderInvalidationCount = 0;
        _cpuAttrFirstCacheHitCount = 0;
        _cpuAttrFirstCacheMissCount = 0;
        _cpuAttrFirstCacheRebuildCount = 0;
    }
}
