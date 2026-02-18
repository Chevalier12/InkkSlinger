using System;
using System.Diagnostics;
using System.Text;

namespace InkkSlinger;

internal static class TextBoxFrameworkDiagnostics
{
    private const double IdleFlushMs = 750d;
    private const int FlushSampleThreshold = 160;
    private static readonly bool IsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_TEXTBOX_FRAMEWORK_LOGS"), "1", StringComparison.Ordinal);

    private static readonly object Sync = new();
    private static int _sampleCount;
    private static long _firstTimestamp;
    private static long _lastTimestamp;
    private static TimeSpan _firstProcessCpuTime;

    private static int _commitCount;
    private static int _deferredSyncCount;
    private static int _immediateSyncCount;
    private static int _incrementalNoWrapAttemptCount;
    private static int _incrementalNoWrapSuccessCount;
    private static int _incrementalVirtualSuccessCount;
    private static int _incrementalVirtualFallbackCount;

    private static int _inputSampleCount;
    private static long _inputTotalTicks;
    private static long _inputMaxTicks;
    private static long _inputEditTotalTicks;
    private static long _inputCommitTotalTicks;
    private static long _inputEnsureTotalTicks;

    private static int _renderSampleCount;
    private static long _renderTotalTicks;
    private static long _renderMaxTicks;
    private static long _renderViewportTicks;
    private static long _renderSelectionTicks;
    private static long _renderTextTicks;
    private static long _renderCaretTicks;

    private static int _viewportSampleCount;
    private static int _viewportCacheHitCount;
    private static int _viewportCacheMissCount;
    private static long _viewportTotalTicks;
    private static long _viewportMaxTicks;

    private static int _ensureCaretSampleCount;
    private static int _ensureCaretFastPathHitCount;
    private static int _ensureCaretFastPathMissCount;
    private static long _ensureCaretTotalTicks;
    private static long _ensureCaretMaxTicks;
    private static long _ensureCaretViewportTicks;
    private static long _ensureCaretLineLookupTicks;
    private static long _ensureCaretWidthTicks;
    private static long _ensureCaretOffsetAdjustTicks;

    private static int _textSyncSampleCount;
    private static int _textSyncDeferredFlushCount;
    private static int _textSyncImmediateCount;
    private static long _textSyncTotalTicks;
    private static long _textSyncMaxTicks;
    private static int _invalidOperationCount;
    private static string _lastInvalidOperationContext = "<none>";
    private static string _lastInvalidOperationMessage = "<none>";

    public static void ObserveCommit(
        bool deferredSync,
        bool attemptedIncrementalNoWrap,
        bool appliedIncrementalNoWrap,
        bool appliedIncrementalVirtualWrap,
        bool incrementalVirtualFallback)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (Sync)
        {
            NoteSampleLocked();
            _commitCount++;
            if (deferredSync)
            {
                _deferredSyncCount++;
            }
            else
            {
                _immediateSyncCount++;
            }

            if (attemptedIncrementalNoWrap)
            {
                _incrementalNoWrapAttemptCount++;
                if (appliedIncrementalNoWrap)
                {
                    _incrementalNoWrapSuccessCount++;
                }
            }

            if (appliedIncrementalVirtualWrap)
            {
                _incrementalVirtualSuccessCount++;
            }

            if (incrementalVirtualFallback)
            {
                _incrementalVirtualFallbackCount++;
            }
        }
    }

    public static void ObserveInputMutation(long totalTicks, long editTicks, long commitTicks, long ensureCaretTicks)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (Sync)
        {
            NoteSampleLocked();
            _inputSampleCount++;
            _inputTotalTicks += Math.Max(0L, totalTicks);
            _inputMaxTicks = Math.Max(_inputMaxTicks, totalTicks);
            _inputEditTotalTicks += Math.Max(0L, editTicks);
            _inputCommitTotalTicks += Math.Max(0L, commitTicks);
            _inputEnsureTotalTicks += Math.Max(0L, ensureCaretTicks);
        }
    }

    public static void ObserveRender(long totalTicks, long viewportTicks, long selectionTicks, long textTicks, long caretTicks)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (Sync)
        {
            NoteSampleLocked();
            _renderSampleCount++;
            _renderTotalTicks += Math.Max(0L, totalTicks);
            _renderMaxTicks = Math.Max(_renderMaxTicks, totalTicks);
            _renderViewportTicks += Math.Max(0L, viewportTicks);
            _renderSelectionTicks += Math.Max(0L, selectionTicks);
            _renderTextTicks += Math.Max(0L, textTicks);
            _renderCaretTicks += Math.Max(0L, caretTicks);
        }
    }

    public static void ObserveViewportState(long ticks, bool cacheHit)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (Sync)
        {
            NoteSampleLocked();
            _viewportSampleCount++;
            if (cacheHit)
            {
                _viewportCacheHitCount++;
            }
            else
            {
                _viewportCacheMissCount++;
            }

            _viewportTotalTicks += Math.Max(0L, ticks);
            _viewportMaxTicks = Math.Max(_viewportMaxTicks, ticks);
        }
    }

    public static void ObserveEnsureCaret(
        long totalTicks,
        long viewportTicks,
        long lineLookupTicks,
        long widthTicks,
        long offsetAdjustTicks,
        bool usedFastPath)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (Sync)
        {
            NoteSampleLocked();
            _ensureCaretSampleCount++;
            if (usedFastPath)
            {
                _ensureCaretFastPathHitCount++;
            }
            else
            {
                _ensureCaretFastPathMissCount++;
            }

            _ensureCaretTotalTicks += Math.Max(0L, totalTicks);
            _ensureCaretMaxTicks = Math.Max(_ensureCaretMaxTicks, totalTicks);
            _ensureCaretViewportTicks += Math.Max(0L, viewportTicks);
            _ensureCaretLineLookupTicks += Math.Max(0L, lineLookupTicks);
            _ensureCaretWidthTicks += Math.Max(0L, widthTicks);
            _ensureCaretOffsetAdjustTicks += Math.Max(0L, offsetAdjustTicks);
        }
    }

    public static void ObserveTextSync(long ticks, bool wasDeferredFlush)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (Sync)
        {
            NoteSampleLocked();
            _textSyncSampleCount++;
            if (wasDeferredFlush)
            {
                _textSyncDeferredFlushCount++;
            }
            else
            {
                _textSyncImmediateCount++;
            }

            _textSyncTotalTicks += Math.Max(0L, ticks);
            _textSyncMaxTicks = Math.Max(_textSyncMaxTicks, ticks);
        }
    }

    public static void ObserveInvalidOperation(Exception exception, string context)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (Sync)
        {
            NoteSampleLocked();
            _invalidOperationCount++;
            _lastInvalidOperationContext = string.IsNullOrWhiteSpace(context) ? "<unknown>" : context;
            _lastInvalidOperationMessage = string.IsNullOrWhiteSpace(exception.Message) ? "<empty>" : exception.Message;
        }
    }

    public static void Flush()
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (Sync)
        {
            FlushInternalLocked();
        }
    }

    private static void NoteSampleLocked()
    {
        var now = Stopwatch.GetTimestamp();
        if (_sampleCount > 0 && Stopwatch.GetElapsedTime(_lastTimestamp, now).TotalMilliseconds >= IdleFlushMs)
        {
            FlushInternalLocked();
        }

        if (_sampleCount == 0)
        {
            _firstTimestamp = now;
            _firstProcessCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        }

        _lastTimestamp = now;
        _sampleCount++;
        if (_sampleCount >= FlushSampleThreshold)
        {
            FlushInternalLocked();
        }
    }

    private static void FlushInternalLocked()
    {
        if (_sampleCount <= 0)
        {
            ResetLocked();
            return;
        }

        var durationMs = Stopwatch.GetElapsedTime(_firstTimestamp).TotalMilliseconds;
        var elapsedSeconds = Math.Max(0d, durationMs / 1000d);
        var cpuTimeSeconds = (Process.GetCurrentProcess().TotalProcessorTime - _firstProcessCpuTime).TotalSeconds;
        var logicalProcessors = Math.Max(1, Environment.ProcessorCount);
        var processCpuPct = elapsedSeconds > 0d
            ? Math.Max(0d, (cpuTimeSeconds / elapsedSeconds) * 100d / logicalProcessors)
            : 0d;

        var sb = new StringBuilder();
        sb.Append($"[TextBoxFrameworkCpu] samples={_sampleCount} dur={durationMs:0}ms cpu={processCpuPct:0.0}%");
        sb.Append(
            $" | commit: n={_commitCount} deferred={_deferredSyncCount} immediate={_immediateSyncCount} noWrap={_incrementalNoWrapSuccessCount}/{_incrementalNoWrapAttemptCount} virtual(success={_incrementalVirtualSuccessCount},fallback={_incrementalVirtualFallbackCount})");
        sb.Append(
            $" | input: n={_inputSampleCount} avg={AverageMs(_inputTotalTicks, _inputSampleCount):0.000}ms max={TicksToMs(_inputMaxTicks):0.000}ms edit={AverageMs(_inputEditTotalTicks, _inputSampleCount):0.000}ms commit={AverageMs(_inputCommitTotalTicks, _inputSampleCount):0.000}ms ensure={AverageMs(_inputEnsureTotalTicks, _inputSampleCount):0.000}ms");
        sb.Append(
            $" | render: n={_renderSampleCount} avg={AverageMs(_renderTotalTicks, _renderSampleCount):0.000}ms max={TicksToMs(_renderMaxTicks):0.000}ms viewport={AverageMs(_renderViewportTicks, _renderSampleCount):0.000}ms selection={AverageMs(_renderSelectionTicks, _renderSampleCount):0.000}ms text={AverageMs(_renderTextTicks, _renderSampleCount):0.000}ms caret={AverageMs(_renderCaretTicks, _renderSampleCount):0.000}ms");
        var viewportHitRate = _viewportSampleCount > 0
            ? (_viewportCacheHitCount * 100d) / _viewportSampleCount
            : 0d;
        sb.Append(
            $" | viewport: n={_viewportSampleCount} hit={_viewportCacheHitCount} miss={_viewportCacheMissCount} hitRate={viewportHitRate:0.0}% avg={AverageMs(_viewportTotalTicks, _viewportSampleCount):0.000}ms max={TicksToMs(_viewportMaxTicks):0.000}ms");
        sb.Append(
            $" | ensureCaret: n={_ensureCaretSampleCount} fast={_ensureCaretFastPathHitCount}/{Math.Max(1, _ensureCaretSampleCount)} avg={AverageMs(_ensureCaretTotalTicks, _ensureCaretSampleCount):0.000}ms max={TicksToMs(_ensureCaretMaxTicks):0.000}ms viewport={AverageMs(_ensureCaretViewportTicks, _ensureCaretSampleCount):0.000}ms line={AverageMs(_ensureCaretLineLookupTicks, _ensureCaretSampleCount):0.000}ms width={AverageMs(_ensureCaretWidthTicks, _ensureCaretSampleCount):0.000}ms adjust={AverageMs(_ensureCaretOffsetAdjustTicks, _ensureCaretSampleCount):0.000}ms");
        sb.Append(
            $" | textSync: n={_textSyncSampleCount} deferredFlush={_textSyncDeferredFlushCount} immediate={_textSyncImmediateCount} avg={AverageMs(_textSyncTotalTicks, _textSyncSampleCount):0.000}ms max={TicksToMs(_textSyncMaxTicks):0.000}ms");
        sb.Append(
            $" | invalidOp: n={_invalidOperationCount} lastCtx={_lastInvalidOperationContext} lastMsg={_lastInvalidOperationMessage}");

        var message = sb.ToString();
        Debug.WriteLine(message);
        Console.WriteLine(message);
        ResetLocked();
    }

    private static double TicksToMs(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    private static double AverageMs(long totalTicks, int sampleCount)
    {
        if (sampleCount <= 0)
        {
            return 0d;
        }

        return TicksToMs(totalTicks) / sampleCount;
    }

    private static void ResetLocked()
    {
        _sampleCount = 0;
        _firstTimestamp = 0L;
        _lastTimestamp = 0L;
        _firstProcessCpuTime = TimeSpan.Zero;

        _commitCount = 0;
        _deferredSyncCount = 0;
        _immediateSyncCount = 0;
        _incrementalNoWrapAttemptCount = 0;
        _incrementalNoWrapSuccessCount = 0;
        _incrementalVirtualSuccessCount = 0;
        _incrementalVirtualFallbackCount = 0;

        _inputSampleCount = 0;
        _inputTotalTicks = 0L;
        _inputMaxTicks = 0L;
        _inputEditTotalTicks = 0L;
        _inputCommitTotalTicks = 0L;
        _inputEnsureTotalTicks = 0L;

        _renderSampleCount = 0;
        _renderTotalTicks = 0L;
        _renderMaxTicks = 0L;
        _renderViewportTicks = 0L;
        _renderSelectionTicks = 0L;
        _renderTextTicks = 0L;
        _renderCaretTicks = 0L;

        _viewportSampleCount = 0;
        _viewportCacheHitCount = 0;
        _viewportCacheMissCount = 0;
        _viewportTotalTicks = 0L;
        _viewportMaxTicks = 0L;

        _ensureCaretSampleCount = 0;
        _ensureCaretFastPathHitCount = 0;
        _ensureCaretFastPathMissCount = 0;
        _ensureCaretTotalTicks = 0L;
        _ensureCaretMaxTicks = 0L;
        _ensureCaretViewportTicks = 0L;
        _ensureCaretLineLookupTicks = 0L;
        _ensureCaretWidthTicks = 0L;
        _ensureCaretOffsetAdjustTicks = 0L;

        _textSyncSampleCount = 0;
        _textSyncDeferredFlushCount = 0;
        _textSyncImmediateCount = 0;
        _textSyncTotalTicks = 0L;
        _textSyncMaxTicks = 0L;
        _invalidOperationCount = 0;
        _lastInvalidOperationContext = "<none>";
        _lastInvalidOperationMessage = "<none>";
    }
}
