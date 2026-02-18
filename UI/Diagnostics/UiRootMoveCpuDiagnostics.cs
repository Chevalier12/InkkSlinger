using System;
using System.Diagnostics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double MoveCpuIdleFlushMs = 450d;
    private const int MoveCpuMinSamples = 4;
    private static readonly bool IsMoveCpuDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_MOVE_CPU_LOGS"), "1", StringComparison.Ordinal);

    private void ObserveMoveCpuPointerDispatch(int moveHitTests)
    {
        if (!IsMoveCpuDiagnosticsEnabled)
        {
            return;
        }

        if (_moveCpuEventCount == 0)
        {
            _moveCpuFirstEventTimestamp = Stopwatch.GetTimestamp();
            _moveCpuFirstProcessCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        }

        _moveCpuLastEventTimestamp = Stopwatch.GetTimestamp();
        _moveCpuEventCount++;
        _moveCpuHitTestCount += Math.Max(0, moveHitTests);
    }

    private void ObserveMoveCpuAfterUpdate()
    {
        if (!IsMoveCpuDiagnosticsEnabled)
        {
            return;
        }

        if (_moveCpuEventCount == 0)
        {
            return;
        }

        _moveCpuSampleCount++;
        _moveCpuUpdateMsTotal += LastUpdateMs;
        _moveCpuInputMsTotal += LastInputPhaseMs;
        _moveCpuCaptureMsTotal += _lastInputCaptureMs;
        _moveCpuDispatchMsTotal += _lastInputDispatchMs;
        _moveCpuPointerDispatchMsTotal += _lastInputPointerDispatchMs;
        _moveCpuPointerResolveMsTotal += _lastInputPointerTargetResolveMs;
        _moveCpuHoverMsTotal += _lastInputHoverUpdateMs;
        _moveCpuLayoutMsTotal += LastLayoutPhaseMs;
        _moveCpuPointerRouteMsTotal += _lastInputPointerRouteMs;
        _moveCpuVisualUpdateMsTotal += _lastVisualUpdateMs;
        _moveCpuMaxUpdateMs = Math.Max(_moveCpuMaxUpdateMs, LastUpdateMs);
        _moveCpuMaxInputMs = Math.Max(_moveCpuMaxInputMs, LastInputPhaseMs);
        _moveCpuMaxCaptureMs = Math.Max(_moveCpuMaxCaptureMs, _lastInputCaptureMs);
        _moveCpuMaxDispatchMs = Math.Max(_moveCpuMaxDispatchMs, _lastInputDispatchMs);
        _moveCpuMaxPointerDispatchMs = Math.Max(_moveCpuMaxPointerDispatchMs, _lastInputPointerDispatchMs);
        _moveCpuMaxPointerResolveMs = Math.Max(_moveCpuMaxPointerResolveMs, _lastInputPointerTargetResolveMs);
        _moveCpuMaxHoverMs = Math.Max(_moveCpuMaxHoverMs, _lastInputHoverUpdateMs);
        _moveCpuMaxLayoutMs = Math.Max(_moveCpuMaxLayoutMs, LastLayoutPhaseMs);
        _moveCpuMaxPointerRouteMs = Math.Max(_moveCpuMaxPointerRouteMs, _lastInputPointerRouteMs);
        _moveCpuMaxVisualUpdateMs = Math.Max(_moveCpuMaxVisualUpdateMs, _lastVisualUpdateMs);

        var idleMs = Stopwatch.GetElapsedTime(_moveCpuLastEventTimestamp).TotalMilliseconds;
        if (idleMs >= MoveCpuIdleFlushMs)
        {
            FlushMoveCpuDiagnostics();
        }
    }

    private void ObserveMoveCpuAfterDraw()
    {
        if (!IsMoveCpuDiagnosticsEnabled)
        {
            return;
        }

        if (_moveCpuEventCount == 0)
        {
            return;
        }

        _moveCpuDrawSampleCount++;
        _moveCpuDrawExecutedCount += DrawCalls > 0 ? 1 : 0;
        _moveCpuDrawMsTotal += LastDrawMs;
        _moveCpuMaxDrawMs = Math.Max(_moveCpuMaxDrawMs, LastDrawMs);

        var idleMs = Stopwatch.GetElapsedTime(_moveCpuLastEventTimestamp).TotalMilliseconds;
        if (idleMs >= MoveCpuIdleFlushMs)
        {
            FlushMoveCpuDiagnostics();
        }
    }

    private void FlushMoveCpuDiagnostics()
    {
        if (!IsMoveCpuDiagnosticsEnabled)
        {
            ResetMoveCpuDiagnostics();
            return;
        }

        if (_moveCpuSampleCount < MoveCpuMinSamples)
        {
            ResetMoveCpuDiagnostics();
            return;
        }

        var durationMs = Stopwatch.GetElapsedTime(_moveCpuFirstEventTimestamp).TotalMilliseconds;
        var elapsedSeconds = Math.Max(0d, durationMs / 1000d);
        var cpuTimeSeconds = (Process.GetCurrentProcess().TotalProcessorTime - _moveCpuFirstProcessCpuTime).TotalSeconds;
        var logicalProcessors = Math.Max(1, Environment.ProcessorCount);
        var processCpuPct = elapsedSeconds > 0d
            ? Math.Max(0d, (cpuTimeSeconds / elapsedSeconds) * 100d / logicalProcessors)
            : 0d;
        var avgUpdateMs = _moveCpuUpdateMsTotal / _moveCpuSampleCount;
        var avgInputMs = _moveCpuInputMsTotal / _moveCpuSampleCount;
        var avgCaptureMs = _moveCpuCaptureMsTotal / _moveCpuSampleCount;
        var avgDispatchMs = _moveCpuDispatchMsTotal / _moveCpuSampleCount;
        var avgPointerDispatchMs = _moveCpuPointerDispatchMsTotal / _moveCpuSampleCount;
        var avgPointerResolveMs = _moveCpuPointerResolveMsTotal / _moveCpuSampleCount;
        var avgHoverMs = _moveCpuHoverMsTotal / _moveCpuSampleCount;
        var avgLayoutMs = _moveCpuLayoutMsTotal / _moveCpuSampleCount;
        var avgRouteMs = _moveCpuPointerRouteMsTotal / _moveCpuSampleCount;
        var avgVisualUpdateMs = _moveCpuVisualUpdateMsTotal / _moveCpuSampleCount;
        var avgDrawMs = _moveCpuDrawSampleCount > 0 ? _moveCpuDrawMsTotal / _moveCpuDrawSampleCount : 0d;
        var drawExecPct = _moveCpuDrawSampleCount > 0
            ? ((double)_moveCpuDrawExecutedCount / _moveCpuDrawSampleCount) * 100d
            : 0d;

        var summary =
            $"[MoveCpu] moves={_moveCpuEventCount} samples={_moveCpuSampleCount} drawSamples={_moveCpuDrawSampleCount} " +
            $"drawExec={drawExecPct:0}% dur={durationMs:0}ms " +
            $"avg(update={avgUpdateMs:0.0}ms input={avgInputMs:0.0}ms capture={avgCaptureMs:0.0}ms dispatch={avgDispatchMs:0.0}ms " +
            $"ptrDispatch={avgPointerDispatchMs:0.0}ms ptrResolve={avgPointerResolveMs:0.0}ms hover={avgHoverMs:0.0}ms " +
            $"layout={avgLayoutMs:0.0}ms draw={avgDrawMs:0.0}ms route={avgRouteMs:0.0}ms visualUpd={avgVisualUpdateMs:0.0}ms) " +
            $"max(update={_moveCpuMaxUpdateMs:0.0}ms input={_moveCpuMaxInputMs:0.0}ms capture={_moveCpuMaxCaptureMs:0.0}ms dispatch={_moveCpuMaxDispatchMs:0.0}ms " +
            $"ptrDispatch={_moveCpuMaxPointerDispatchMs:0.0}ms ptrResolve={_moveCpuMaxPointerResolveMs:0.0}ms hover={_moveCpuMaxHoverMs:0.0}ms " +
            $"layout={_moveCpuMaxLayoutMs:0.0}ms draw={_moveCpuMaxDrawMs:0.0}ms route={_moveCpuMaxPointerRouteMs:0.0}ms visualUpd={_moveCpuMaxVisualUpdateMs:0.0}ms) " +
            $"moveHitTests={_moveCpuHitTestCount} cpu={processCpuPct:0.0}%";

        Debug.WriteLine(summary);
        Console.WriteLine(summary);
        ResetMoveCpuDiagnostics();
    }

    private void ResetMoveCpuDiagnostics()
    {
        _moveCpuEventCount = 0;
        _moveCpuHitTestCount = 0;
        _moveCpuSampleCount = 0;
        _moveCpuDrawSampleCount = 0;
        _moveCpuDrawExecutedCount = 0;
        _moveCpuUpdateMsTotal = 0d;
        _moveCpuInputMsTotal = 0d;
        _moveCpuCaptureMsTotal = 0d;
        _moveCpuDispatchMsTotal = 0d;
        _moveCpuPointerDispatchMsTotal = 0d;
        _moveCpuPointerResolveMsTotal = 0d;
        _moveCpuHoverMsTotal = 0d;
        _moveCpuLayoutMsTotal = 0d;
        _moveCpuDrawMsTotal = 0d;
        _moveCpuPointerRouteMsTotal = 0d;
        _moveCpuVisualUpdateMsTotal = 0d;
        _moveCpuMaxUpdateMs = 0d;
        _moveCpuMaxInputMs = 0d;
        _moveCpuMaxCaptureMs = 0d;
        _moveCpuMaxDispatchMs = 0d;
        _moveCpuMaxPointerDispatchMs = 0d;
        _moveCpuMaxPointerResolveMs = 0d;
        _moveCpuMaxHoverMs = 0d;
        _moveCpuMaxLayoutMs = 0d;
        _moveCpuMaxDrawMs = 0d;
        _moveCpuMaxPointerRouteMs = 0d;
        _moveCpuMaxVisualUpdateMs = 0d;
        _moveCpuFirstEventTimestamp = 0L;
        _moveCpuLastEventTimestamp = 0L;
        _moveCpuFirstProcessCpuTime = TimeSpan.Zero;
    }
}
