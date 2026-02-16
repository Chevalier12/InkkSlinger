using System;
using System.Diagnostics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double ClickCpuIdleFlushMs = 450d;
    private const int ClickCpuMinSamples = 4;
    private static readonly bool IsClickCpuDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_CLICK_CPU_LOGS"), "1", StringComparison.Ordinal);

    private void ObserveClickCpuPointerDispatch(bool isDown, int clickHitTests, double clickHandleMs)
    {
        if (!IsClickCpuDiagnosticsEnabled)
        {
            return;
        }

        if (_clickCpuPointerDownCount == 0 && _clickCpuPointerUpCount == 0)
        {
            _clickCpuFirstEventTimestamp = Stopwatch.GetTimestamp();
            _clickCpuFirstProcessCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        }

        _clickCpuLastEventTimestamp = Stopwatch.GetTimestamp();
        if (isDown)
        {
            _clickCpuPointerDownCount++;
        }
        else
        {
            _clickCpuPointerUpCount++;
        }

        _clickCpuHitTestCount += Math.Max(0, clickHitTests);
        _clickCpuHandleMsTotal += clickHandleMs;
        _clickCpuMaxHandleMs = Math.Max(_clickCpuMaxHandleMs, clickHandleMs);
    }

    private void ObserveClickCpuAfterUpdate()
    {
        if (!IsClickCpuDiagnosticsEnabled)
        {
            return;
        }

        if (_clickCpuPointerDownCount == 0 && _clickCpuPointerUpCount == 0)
        {
            return;
        }

        _clickCpuSampleCount++;
        _clickCpuUpdateMsTotal += LastUpdateMs;
        _clickCpuInputMsTotal += LastInputPhaseMs;
        _clickCpuCaptureMsTotal += _lastInputCaptureMs;
        _clickCpuDispatchMsTotal += _lastInputDispatchMs;
        _clickCpuPointerDispatchMsTotal += _lastInputPointerDispatchMs;
        _clickCpuPointerResolveMsTotal += _lastInputPointerTargetResolveMs;
        _clickCpuHoverMsTotal += _lastInputHoverUpdateMs;
        _clickCpuLayoutMsTotal += LastLayoutPhaseMs;
        _clickCpuPointerRouteMsTotal += _lastInputPointerRouteMs;
        _clickCpuVisualUpdateMsTotal += _lastVisualUpdateMs;
        _clickCpuMaxUpdateMs = Math.Max(_clickCpuMaxUpdateMs, LastUpdateMs);
        _clickCpuMaxInputMs = Math.Max(_clickCpuMaxInputMs, LastInputPhaseMs);
        _clickCpuMaxCaptureMs = Math.Max(_clickCpuMaxCaptureMs, _lastInputCaptureMs);
        _clickCpuMaxDispatchMs = Math.Max(_clickCpuMaxDispatchMs, _lastInputDispatchMs);
        _clickCpuMaxPointerDispatchMs = Math.Max(_clickCpuMaxPointerDispatchMs, _lastInputPointerDispatchMs);
        _clickCpuMaxPointerResolveMs = Math.Max(_clickCpuMaxPointerResolveMs, _lastInputPointerTargetResolveMs);
        _clickCpuMaxHoverMs = Math.Max(_clickCpuMaxHoverMs, _lastInputHoverUpdateMs);
        _clickCpuMaxLayoutMs = Math.Max(_clickCpuMaxLayoutMs, LastLayoutPhaseMs);
        _clickCpuMaxPointerRouteMs = Math.Max(_clickCpuMaxPointerRouteMs, _lastInputPointerRouteMs);
        _clickCpuMaxVisualUpdateMs = Math.Max(_clickCpuMaxVisualUpdateMs, _lastVisualUpdateMs);

        var idleMs = Stopwatch.GetElapsedTime(_clickCpuLastEventTimestamp).TotalMilliseconds;
        if (idleMs >= ClickCpuIdleFlushMs)
        {
            FlushClickCpuDiagnostics();
        }
    }

    private void ObserveClickCpuAfterDraw()
    {
        if (!IsClickCpuDiagnosticsEnabled)
        {
            return;
        }

        if (_clickCpuPointerDownCount == 0 && _clickCpuPointerUpCount == 0)
        {
            return;
        }

        _clickCpuDrawSampleCount++;
        _clickCpuDrawExecutedCount += DrawCalls > 0 ? 1 : 0;
        _clickCpuDrawMsTotal += LastDrawMs;
        _clickCpuMaxDrawMs = Math.Max(_clickCpuMaxDrawMs, LastDrawMs);

        var idleMs = Stopwatch.GetElapsedTime(_clickCpuLastEventTimestamp).TotalMilliseconds;
        if (idleMs >= ClickCpuIdleFlushMs)
        {
            FlushClickCpuDiagnostics();
        }
    }

    private void FlushClickCpuDiagnostics()
    {
        if (!IsClickCpuDiagnosticsEnabled)
        {
            ResetClickCpuDiagnostics();
            return;
        }

        if (_clickCpuSampleCount < ClickCpuMinSamples)
        {
            ResetClickCpuDiagnostics();
            return;
        }

        var pointerEvents = _clickCpuPointerDownCount + _clickCpuPointerUpCount;
        var durationMs = Stopwatch.GetElapsedTime(_clickCpuFirstEventTimestamp).TotalMilliseconds;
        var elapsedSeconds = Math.Max(0d, durationMs / 1000d);
        var cpuTimeSeconds = (Process.GetCurrentProcess().TotalProcessorTime - _clickCpuFirstProcessCpuTime).TotalSeconds;
        var logicalProcessors = Math.Max(1, Environment.ProcessorCount);
        var processCpuPct = elapsedSeconds > 0d
            ? Math.Max(0d, (cpuTimeSeconds / elapsedSeconds) * 100d / logicalProcessors)
            : 0d;
        var avgUpdateMs = _clickCpuUpdateMsTotal / _clickCpuSampleCount;
        var avgInputMs = _clickCpuInputMsTotal / _clickCpuSampleCount;
        var avgCaptureMs = _clickCpuCaptureMsTotal / _clickCpuSampleCount;
        var avgDispatchMs = _clickCpuDispatchMsTotal / _clickCpuSampleCount;
        var avgPointerDispatchMs = _clickCpuPointerDispatchMsTotal / _clickCpuSampleCount;
        var avgPointerResolveMs = _clickCpuPointerResolveMsTotal / _clickCpuSampleCount;
        var avgHoverMs = _clickCpuHoverMsTotal / _clickCpuSampleCount;
        var avgLayoutMs = _clickCpuLayoutMsTotal / _clickCpuSampleCount;
        var avgRouteMs = _clickCpuPointerRouteMsTotal / _clickCpuSampleCount;
        var avgVisualUpdateMs = _clickCpuVisualUpdateMsTotal / _clickCpuSampleCount;
        var avgDrawMs = _clickCpuDrawSampleCount > 0 ? _clickCpuDrawMsTotal / _clickCpuDrawSampleCount : 0d;
        var avgHandleMs = pointerEvents > 0 ? _clickCpuHandleMsTotal / pointerEvents : 0d;
        var drawExecPct = _clickCpuDrawSampleCount > 0
            ? ((double)_clickCpuDrawExecutedCount / _clickCpuDrawSampleCount) * 100d
            : 0d;

        var summary =
            $"[ClickCpu] downs={_clickCpuPointerDownCount} ups={_clickCpuPointerUpCount} events={pointerEvents} " +
            $"samples={_clickCpuSampleCount} drawSamples={_clickCpuDrawSampleCount} drawExec={drawExecPct:0}% dur={durationMs:0}ms " +
            $"avg(update={avgUpdateMs:0.0}ms input={avgInputMs:0.0}ms capture={avgCaptureMs:0.0}ms dispatch={avgDispatchMs:0.0}ms " +
            $"ptrDispatch={avgPointerDispatchMs:0.0}ms ptrResolve={avgPointerResolveMs:0.0}ms hover={avgHoverMs:0.0}ms " +
            $"layout={avgLayoutMs:0.0}ms draw={avgDrawMs:0.0}ms " +
            $"route={avgRouteMs:0.0}ms visualUpd={avgVisualUpdateMs:0.0}ms clickHandle={avgHandleMs:0.0}ms) " +
            $"max(update={_clickCpuMaxUpdateMs:0.0}ms input={_clickCpuMaxInputMs:0.0}ms capture={_clickCpuMaxCaptureMs:0.0}ms dispatch={_clickCpuMaxDispatchMs:0.0}ms " +
            $"ptrDispatch={_clickCpuMaxPointerDispatchMs:0.0}ms ptrResolve={_clickCpuMaxPointerResolveMs:0.0}ms hover={_clickCpuMaxHoverMs:0.0}ms " +
            $"layout={_clickCpuMaxLayoutMs:0.0}ms draw={_clickCpuMaxDrawMs:0.0}ms " +
            $"route={_clickCpuMaxPointerRouteMs:0.0}ms visualUpd={_clickCpuMaxVisualUpdateMs:0.0}ms clickHandle={_clickCpuMaxHandleMs:0.0}ms) " +
            $"clickHitTests={_clickCpuHitTestCount} resolve(cached={_clickCpuResolveCachedCount} captured={_clickCpuResolveCapturedCount} hovered={_clickCpuResolveHoveredCount} hitTest={_clickCpuResolveHitTestCount}) " +
            $"cpu={processCpuPct:0.0}%";

        Debug.WriteLine(summary);
        Console.WriteLine(summary);
        ResetClickCpuDiagnostics();
    }

    private void ResetClickCpuDiagnostics()
    {
        _clickCpuPointerDownCount = 0;
        _clickCpuPointerUpCount = 0;
        _clickCpuHitTestCount = 0;
        _clickCpuResolveCachedCount = 0;
        _clickCpuResolveCapturedCount = 0;
        _clickCpuResolveHoveredCount = 0;
        _clickCpuResolveHitTestCount = 0;
        _clickCpuSampleCount = 0;
        _clickCpuDrawSampleCount = 0;
        _clickCpuDrawExecutedCount = 0;
        _clickCpuUpdateMsTotal = 0d;
        _clickCpuInputMsTotal = 0d;
        _clickCpuLayoutMsTotal = 0d;
        _clickCpuDrawMsTotal = 0d;
        _clickCpuPointerRouteMsTotal = 0d;
        _clickCpuVisualUpdateMsTotal = 0d;
        _clickCpuHandleMsTotal = 0d;
        _clickCpuCaptureMsTotal = 0d;
        _clickCpuDispatchMsTotal = 0d;
        _clickCpuPointerDispatchMsTotal = 0d;
        _clickCpuPointerResolveMsTotal = 0d;
        _clickCpuHoverMsTotal = 0d;
        _clickCpuMaxUpdateMs = 0d;
        _clickCpuMaxInputMs = 0d;
        _clickCpuMaxLayoutMs = 0d;
        _clickCpuMaxDrawMs = 0d;
        _clickCpuMaxPointerRouteMs = 0d;
        _clickCpuMaxVisualUpdateMs = 0d;
        _clickCpuMaxHandleMs = 0d;
        _clickCpuMaxCaptureMs = 0d;
        _clickCpuMaxDispatchMs = 0d;
        _clickCpuMaxPointerDispatchMs = 0d;
        _clickCpuMaxPointerResolveMs = 0d;
        _clickCpuMaxHoverMs = 0d;
        _clickCpuFirstEventTimestamp = 0L;
        _clickCpuLastEventTimestamp = 0L;
        _clickCpuFirstProcessCpuTime = TimeSpan.Zero;
    }
}
