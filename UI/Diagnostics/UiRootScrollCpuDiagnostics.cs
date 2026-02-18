using System;
using System.Diagnostics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double ScrollCpuIdleFlushMs = 450d;
    private const int ScrollCpuMinSamples = 4;
    private static readonly bool IsScrollCpuDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_SCROLL_CPU_LOGS"), "1", StringComparison.Ordinal);

    internal void ObserveScrollCpuOffsetMutation(double horizontalDelta, double verticalDelta, bool isInputMutation)
    {
        if (isInputMutation && (horizontalDelta > 0.001d || verticalDelta > 0.001d))
        {
            ObserveFrameLatencyScrollEvent();
        }

        if (!IsScrollCpuDiagnosticsEnabled)
        {
            return;
        }

        if (horizontalDelta <= 0.001d && verticalDelta <= 0.001d)
        {
            return;
        }

        if (_scrollCpuWheelEventCount == 0 && _scrollCpuOffsetMutationCount == 0)
        {
            _scrollCpuFirstEventTimestamp = Stopwatch.GetTimestamp();
            _scrollCpuFirstProcessCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        }

        _scrollCpuLastEventTimestamp = Stopwatch.GetTimestamp();
        _scrollCpuOffsetMutationCount++;
    }

    private void ObserveScrollCpuWheelDispatch(int delta, bool usedPreciseRetarget, int wheelHitTests, double wheelHandleMs)
    {
        if (!IsScrollCpuDiagnosticsEnabled)
        {
            return;
        }

        if (delta == 0)
        {
            return;
        }

        if (_scrollCpuWheelEventCount == 0 && _scrollCpuOffsetMutationCount == 0)
        {
            _scrollCpuFirstEventTimestamp = Stopwatch.GetTimestamp();
            _scrollCpuFirstProcessCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        }

        _scrollCpuLastEventTimestamp = Stopwatch.GetTimestamp();
        _scrollCpuWheelEventCount++;
        if (usedPreciseRetarget)
        {
            _scrollCpuWheelPreciseRetargetCount++;
        }

        _scrollCpuWheelHitTestCount += Math.Max(0, wheelHitTests);
        _scrollCpuWheelHandleMsTotal += wheelHandleMs;
        _scrollCpuMaxWheelHandleMs = Math.Max(_scrollCpuMaxWheelHandleMs, wheelHandleMs);
    }

    private void ObserveScrollCpuAfterUpdate()
    {
        if (!IsScrollCpuDiagnosticsEnabled)
        {
            return;
        }

        if (_scrollCpuWheelEventCount == 0 && _scrollCpuOffsetMutationCount == 0)
        {
            return;
        }

        _scrollCpuSampleCount++;
        _scrollCpuUpdateMsTotal += LastUpdateMs;
        _scrollCpuInputMsTotal += LastInputPhaseMs;
        _scrollCpuLayoutMsTotal += LastLayoutPhaseMs;
        _scrollCpuPointerRouteMsTotal += _lastInputPointerRouteMs;
        _scrollCpuVisualUpdateMsTotal += _lastVisualUpdateMs;
        _scrollCpuMaxUpdateMs = Math.Max(_scrollCpuMaxUpdateMs, LastUpdateMs);
        _scrollCpuMaxInputMs = Math.Max(_scrollCpuMaxInputMs, LastInputPhaseMs);
        _scrollCpuMaxLayoutMs = Math.Max(_scrollCpuMaxLayoutMs, LastLayoutPhaseMs);
        _scrollCpuMaxPointerRouteMs = Math.Max(_scrollCpuMaxPointerRouteMs, _lastInputPointerRouteMs);
        _scrollCpuMaxVisualUpdateMs = Math.Max(_scrollCpuMaxVisualUpdateMs, _lastVisualUpdateMs);

        var idleMs = Stopwatch.GetElapsedTime(_scrollCpuLastEventTimestamp).TotalMilliseconds;
        if (idleMs >= ScrollCpuIdleFlushMs)
        {
            FlushScrollCpuDiagnostics();
        }
    }

    private void ObserveScrollCpuAfterDraw()
    {
        if (!IsScrollCpuDiagnosticsEnabled)
        {
            return;
        }

        if (_scrollCpuWheelEventCount == 0 && _scrollCpuOffsetMutationCount == 0)
        {
            return;
        }

        _scrollCpuDrawSampleCount++;
        _scrollCpuDrawExecutedCount += DrawCalls > 0 ? 1 : 0;
        _scrollCpuDrawMsTotal += LastDrawMs;
        _scrollCpuMaxDrawMs = Math.Max(_scrollCpuMaxDrawMs, LastDrawMs);

        var idleMs = Stopwatch.GetElapsedTime(_scrollCpuLastEventTimestamp).TotalMilliseconds;
        if (idleMs >= ScrollCpuIdleFlushMs)
        {
            FlushScrollCpuDiagnostics();
        }
    }

    private void FlushScrollCpuDiagnostics()
    {
        if (!IsScrollCpuDiagnosticsEnabled)
        {
            ResetScrollCpuDiagnostics();
            return;
        }

        if (_scrollCpuSampleCount < ScrollCpuMinSamples)
        {
            ResetScrollCpuDiagnostics();
            return;
        }

        var durationMs = Stopwatch.GetElapsedTime(_scrollCpuFirstEventTimestamp).TotalMilliseconds;
        var elapsedSeconds = Math.Max(0d, durationMs / 1000d);
        var cpuTimeSeconds = (Process.GetCurrentProcess().TotalProcessorTime - _scrollCpuFirstProcessCpuTime).TotalSeconds;
        var logicalProcessors = Math.Max(1, Environment.ProcessorCount);
        var processCpuPct = elapsedSeconds > 0d
            ? Math.Max(0d, (cpuTimeSeconds / elapsedSeconds) * 100d / logicalProcessors)
            : 0d;
        var avgUpdateMs = _scrollCpuUpdateMsTotal / _scrollCpuSampleCount;
        var avgInputMs = _scrollCpuInputMsTotal / _scrollCpuSampleCount;
        var avgLayoutMs = _scrollCpuLayoutMsTotal / _scrollCpuSampleCount;
        var avgRouteMs = _scrollCpuPointerRouteMsTotal / _scrollCpuSampleCount;
        var avgVisualUpdateMs = _scrollCpuVisualUpdateMsTotal / _scrollCpuSampleCount;
        var avgDrawMs = _scrollCpuDrawSampleCount > 0 ? _scrollCpuDrawMsTotal / _scrollCpuDrawSampleCount : 0d;
        var avgWheelHandleMs = _scrollCpuWheelEventCount > 0 ? _scrollCpuWheelHandleMsTotal / _scrollCpuWheelEventCount : 0d;
        var drawExecPct = _scrollCpuDrawSampleCount > 0
            ? ((double)_scrollCpuDrawExecutedCount / _scrollCpuDrawSampleCount) * 100d
            : 0d;

        var summary =
            $"[ScrollCpu] wheels={_scrollCpuWheelEventCount} offsetMutations={_scrollCpuOffsetMutationCount} " +
            $"samples={_scrollCpuSampleCount} drawSamples={_scrollCpuDrawSampleCount} drawExec={drawExecPct:0}% dur={durationMs:0}ms " +
            $"avg(update={avgUpdateMs:0.0}ms input={avgInputMs:0.0}ms layout={avgLayoutMs:0.0}ms draw={avgDrawMs:0.0}ms " +
            $"route={avgRouteMs:0.0}ms visualUpd={avgVisualUpdateMs:0.0}ms wheelHandle={avgWheelHandleMs:0.0}ms) " +
            $"max(update={_scrollCpuMaxUpdateMs:0.0}ms input={_scrollCpuMaxInputMs:0.0}ms layout={_scrollCpuMaxLayoutMs:0.0}ms draw={_scrollCpuMaxDrawMs:0.0}ms " +
            $"route={_scrollCpuMaxPointerRouteMs:0.0}ms visualUpd={_scrollCpuMaxVisualUpdateMs:0.0}ms wheelHandle={_scrollCpuMaxWheelHandleMs:0.0}ms) " +
            $"wheelHitTests={_scrollCpuWheelHitTestCount} preciseRetarget={_scrollCpuWheelPreciseRetargetCount} cpu={processCpuPct:0.0}%";

        Debug.WriteLine(summary);
        Console.WriteLine(summary);
        ResetScrollCpuDiagnostics();
    }

    private void ResetScrollCpuDiagnostics()
    {
        _scrollCpuWheelEventCount = 0;
        _scrollCpuOffsetMutationCount = 0;
        _scrollCpuWheelPreciseRetargetCount = 0;
        _scrollCpuWheelHitTestCount = 0;
        _scrollCpuSampleCount = 0;
        _scrollCpuDrawSampleCount = 0;
        _scrollCpuDrawExecutedCount = 0;
        _scrollCpuUpdateMsTotal = 0d;
        _scrollCpuInputMsTotal = 0d;
        _scrollCpuLayoutMsTotal = 0d;
        _scrollCpuDrawMsTotal = 0d;
        _scrollCpuPointerRouteMsTotal = 0d;
        _scrollCpuVisualUpdateMsTotal = 0d;
        _scrollCpuWheelHandleMsTotal = 0d;
        _scrollCpuMaxUpdateMs = 0d;
        _scrollCpuMaxInputMs = 0d;
        _scrollCpuMaxLayoutMs = 0d;
        _scrollCpuMaxDrawMs = 0d;
        _scrollCpuMaxPointerRouteMs = 0d;
        _scrollCpuMaxVisualUpdateMs = 0d;
        _scrollCpuMaxWheelHandleMs = 0d;
        _scrollCpuFirstEventTimestamp = 0L;
        _scrollCpuLastEventTimestamp = 0L;
        _scrollCpuFirstProcessCpuTime = TimeSpan.Zero;
    }
}
