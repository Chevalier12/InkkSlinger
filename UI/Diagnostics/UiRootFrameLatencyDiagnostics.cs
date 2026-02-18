using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double FrameLatencyIdleFlushMs = 650d;
    private const int FrameLatencyMinSamples = 6;
    private const int FrameLatencyMaxSamples = 512;
    private const double FrameBudget60FpsMs = 16.6d;
    private const double FrameP99AlertMs = 33d;
    private const double FrameMissRateAlertPct = 1d;
    private const double InputP95AlertMs = 2d;
    private const double LayoutP95AlertMs = 1d;
    private const double DrawP95AlertMs = 6d;
    private static readonly bool IsFrameLatencyDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_FRAME_LATENCY_LOGS"), "1", StringComparison.Ordinal);

    private void ObserveFrameLatencyMoveEvent()
    {
        if (!IsFrameLatencyDiagnosticsEnabled)
        {
            return;
        }

        _moveFrameLatencyDiagnostics.NoteActivity();
    }

    private void ObserveFrameLatencyClickEvent()
    {
        if (!IsFrameLatencyDiagnosticsEnabled)
        {
            return;
        }

        _clickFrameLatencyDiagnostics.NoteActivity();
    }

    private void ObserveFrameLatencyScrollEvent()
    {
        if (!IsFrameLatencyDiagnosticsEnabled)
        {
            return;
        }

        _scrollFrameLatencyDiagnostics.NoteActivity();
    }

    private void ObserveFrameLatencyAfterUpdate()
    {
        if (!IsFrameLatencyDiagnosticsEnabled)
        {
            return;
        }

        var nowTimestamp = Stopwatch.GetTimestamp();
        ObserveFrameLatencyWindowAfterUpdate(_scrollFrameLatencyDiagnostics, nowTimestamp);
        ObserveFrameLatencyWindowAfterUpdate(_clickFrameLatencyDiagnostics, nowTimestamp);
        ObserveFrameLatencyWindowAfterUpdate(_moveFrameLatencyDiagnostics, nowTimestamp);
    }

    private void ObserveFrameLatencyAfterDraw()
    {
        if (!IsFrameLatencyDiagnosticsEnabled)
        {
            return;
        }

        var nowTimestamp = Stopwatch.GetTimestamp();
        ObserveFrameLatencyWindowAfterDraw(_scrollFrameLatencyDiagnostics, nowTimestamp);
        ObserveFrameLatencyWindowAfterDraw(_clickFrameLatencyDiagnostics, nowTimestamp);
        ObserveFrameLatencyWindowAfterDraw(_moveFrameLatencyDiagnostics, nowTimestamp);
    }

    private void ObserveFrameLatencyWindowAfterUpdate(FrameLatencyWindowDiagnostics window, long nowTimestamp)
    {
        if (!window.IsActive)
        {
            return;
        }

        window.NoteUpdateSampleWhileAwaitingDraw(LastUpdateMs, LastInputPhaseMs, LastLayoutPhaseMs);
        if (window.ShouldFlush(nowTimestamp, FrameLatencyIdleFlushMs))
        {
            FlushFrameLatencyWindow(window);
        }
    }

    private void ObserveFrameLatencyWindowAfterDraw(FrameLatencyWindowDiagnostics window, long nowTimestamp)
    {
        if (!window.IsActive)
        {
            return;
        }

        window.NoteDrawSampleAfterPendingEvents(LastDrawMs);
        if (window.ShouldFlush(nowTimestamp, FrameLatencyIdleFlushMs))
        {
            FlushFrameLatencyWindow(window);
        }
    }

    private void FlushFrameLatencyWindow(FrameLatencyWindowDiagnostics window)
    {
        if (!IsFrameLatencyDiagnosticsEnabled)
        {
            window.Reset();
            return;
        }

        if (window.TotalEventToDrawSampleCount < FrameLatencyMinSamples)
        {
            window.Reset();
            return;
        }

        var durationMs = Stopwatch.GetElapsedTime(window.FirstEventTimestamp).TotalMilliseconds;
        var elapsedSeconds = Math.Max(0d, durationMs / 1000d);
        var cpuTimeSeconds = (Process.GetCurrentProcess().TotalProcessorTime - window.FirstProcessCpuTime).TotalSeconds;
        var logicalProcessors = Math.Max(1, Environment.ProcessorCount);
        var processCpuPct = elapsedSeconds > 0d
            ? Math.Max(0d, (cpuTimeSeconds / elapsedSeconds) * 100d / logicalProcessors)
            : 0d;

        var frameP95 = Percentile(window.EventToDrawSamples, 0.95d);
        var frameP99 = Percentile(window.EventToDrawSamples, 0.99d);
        var inputP95 = Percentile(window.InputSamples, 0.95d);
        var layoutP95 = Percentile(window.LayoutSamples, 0.95d);
        var drawP95 = Percentile(window.DrawSamples, 0.95d);
        var missRatePct = window.TotalEventToDrawSampleCount > 0
            ? (window.Budget60FrameMissCount * 100d) / window.TotalEventToDrawSampleCount
            : 0d;
        var isBad =
            frameP95 > FrameBudget60FpsMs ||
            frameP99 > FrameP99AlertMs ||
            missRatePct > FrameMissRateAlertPct ||
            inputP95 > InputP95AlertMs ||
            layoutP95 > LayoutP95AlertMs ||
            drawP95 > DrawP95AlertMs;
        if (!isBad)
        {
            window.Reset();
            return;
        }

        var summary =
            $"[FrameLatencyAlert:{window.Name}] events={window.EventCount} samples={window.TotalEventToDrawSampleCount} dur={durationMs:0}ms " +
            $"eventToDraw(p95={frameP95:0.0}ms p99={frameP99:0.0}ms max={window.MaxEventToDrawMs:0.0}ms miss={window.Budget60FrameMissCount}/{window.TotalEventToDrawSampleCount}={missRatePct:0.0}%) " +
            $"phases(p95 input={inputP95:0.0}ms layout={layoutP95:0.0}ms draw={drawP95:0.0}ms) " +
            $"dominantOnMiss(input={window.Budget60DominantInputCount},layout={window.Budget60DominantLayoutCount},draw={window.Budget60DominantDrawCount},otherUpd={window.Budget60DominantOtherUpdateCount}) " +
            $"coalesce(eventsPerDraw={window.EventsPerDraw:0.00} suppressed={window.SuppressedByCoalescingCount}) cpu={processCpuPct:0.0}%";

        Debug.WriteLine(summary);
        Console.WriteLine(summary);
        window.Reset();
    }

    private static double Percentile(List<double> samples, double percentile)
    {
        if (samples.Count == 0)
        {
            return 0d;
        }

        var ordered = samples.ToArray();
        Array.Sort(ordered);
        var rawIndex = (ordered.Length - 1) * percentile;
        var lower = (int)Math.Floor(rawIndex);
        var upper = (int)Math.Ceiling(rawIndex);
        if (lower == upper)
        {
            return ordered[lower];
        }

        var fraction = rawIndex - lower;
        return ordered[lower] + ((ordered[upper] - ordered[lower]) * fraction);
    }

    private sealed class FrameLatencyWindowDiagnostics
    {
        private readonly List<long> _pendingEventTimestamps = new();
        private readonly bool _coalesceToLatestEventPerDraw;
        private bool _awaitingDrawForPendingEvents;
        private double _pendingUpdateMs;
        private double _pendingInputMs;
        private double _pendingLayoutMs;

        public FrameLatencyWindowDiagnostics(string name, bool coalesceToLatestEventPerDraw)
        {
            Name = name;
            _coalesceToLatestEventPerDraw = coalesceToLatestEventPerDraw;
        }

        public string Name { get; }

        public int EventCount { get; private set; }

        public long FirstEventTimestamp { get; private set; }

        public long LastEventTimestamp { get; private set; }

        public TimeSpan FirstProcessCpuTime { get; private set; }

        public List<double> EventToDrawSamples { get; } = new();

        public List<double> InputSamples { get; } = new();

        public List<double> LayoutSamples { get; } = new();

        public List<double> DrawSamples { get; } = new();

        public double MaxEventToDrawMs { get; private set; }

        public int DrawSampleCount { get; private set; }

        public int SuppressedByCoalescingCount { get; private set; }

        public int Budget60FrameMissCount { get; private set; }

        public int TotalEventToDrawSampleCount { get; private set; }

        public int Budget60DominantInputCount { get; private set; }

        public int Budget60DominantLayoutCount { get; private set; }

        public int Budget60DominantDrawCount { get; private set; }

        public int Budget60DominantOtherUpdateCount { get; private set; }

        public int EventToDrawSampleCount => EventToDrawSamples.Count;

        public double EventsPerDraw => DrawSampleCount <= 0 ? 0d : (double)EventCount / DrawSampleCount;

        public bool IsActive => EventCount > 0;

        public void NoteActivity()
        {
            if (EventCount == 0)
            {
                FirstEventTimestamp = Stopwatch.GetTimestamp();
                FirstProcessCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
            }

            var now = Stopwatch.GetTimestamp();
            LastEventTimestamp = now;
            EventCount++;
            _pendingEventTimestamps.Add(now);
            _awaitingDrawForPendingEvents = true;
        }

        public void NoteUpdateSampleWhileAwaitingDraw(double updateMs, double inputMs, double layoutMs)
        {
            if (!_awaitingDrawForPendingEvents)
            {
                return;
            }

            _pendingUpdateMs = Math.Max(_pendingUpdateMs, Math.Max(0d, updateMs));
            _pendingInputMs = Math.Max(_pendingInputMs, Math.Max(0d, inputMs));
            _pendingLayoutMs = Math.Max(_pendingLayoutMs, Math.Max(0d, layoutMs));
        }

        public void NoteDrawSampleAfterPendingEvents(double drawMs)
        {
            if (!_awaitingDrawForPendingEvents || _pendingEventTimestamps.Count == 0)
            {
                return;
            }

            var now = Stopwatch.GetTimestamp();
            var boundedDrawMs = Math.Max(0d, drawMs);
            AppendCapped(InputSamples, _pendingInputMs);
            AppendCapped(LayoutSamples, _pendingLayoutMs);
            AppendCapped(DrawSamples, boundedDrawMs);
            DrawSampleCount++;
            if (_coalesceToLatestEventPerDraw)
            {
                SuppressedByCoalescingCount += Math.Max(0, _pendingEventTimestamps.Count - 1);
                var latestEventTimestamp = _pendingEventTimestamps[_pendingEventTimestamps.Count - 1];
                var latencyMs = Stopwatch.GetElapsedTime(latestEventTimestamp, now).TotalMilliseconds;
                AppendCapped(EventToDrawSamples, latencyMs);
                TotalEventToDrawSampleCount++;
                MaxEventToDrawMs = Math.Max(MaxEventToDrawMs, latencyMs);
                if (latencyMs > FrameBudget60FpsMs)
                {
                    Budget60FrameMissCount++;
                    AccumulateDominantMissPhase(_pendingUpdateMs, _pendingInputMs, _pendingLayoutMs, boundedDrawMs);
                }
            }
            else
            {
                for (var i = 0; i < _pendingEventTimestamps.Count; i++)
                {
                    var latencyMs = Stopwatch.GetElapsedTime(_pendingEventTimestamps[i], now).TotalMilliseconds;
                    AppendCapped(EventToDrawSamples, latencyMs);
                    TotalEventToDrawSampleCount++;
                    MaxEventToDrawMs = Math.Max(MaxEventToDrawMs, latencyMs);
                    if (latencyMs > FrameBudget60FpsMs)
                    {
                        Budget60FrameMissCount++;
                        AccumulateDominantMissPhase(_pendingUpdateMs, _pendingInputMs, _pendingLayoutMs, boundedDrawMs);
                    }
                }
            }

            _pendingEventTimestamps.Clear();
            _awaitingDrawForPendingEvents = false;
            _pendingUpdateMs = 0d;
            _pendingInputMs = 0d;
            _pendingLayoutMs = 0d;
        }

        public bool ShouldFlush(long nowTimestamp, double idleFlushMs)
        {
            if (!IsActive)
            {
                return false;
            }

            return Stopwatch.GetElapsedTime(LastEventTimestamp, nowTimestamp).TotalMilliseconds >= idleFlushMs;
        }

        public void Reset()
        {
            EventCount = 0;
            FirstEventTimestamp = 0L;
            LastEventTimestamp = 0L;
            FirstProcessCpuTime = TimeSpan.Zero;
            _pendingEventTimestamps.Clear();
            _awaitingDrawForPendingEvents = false;
            EventToDrawSamples.Clear();
            InputSamples.Clear();
            LayoutSamples.Clear();
            DrawSamples.Clear();
            MaxEventToDrawMs = 0d;
            DrawSampleCount = 0;
            SuppressedByCoalescingCount = 0;
            Budget60FrameMissCount = 0;
            TotalEventToDrawSampleCount = 0;
            Budget60DominantInputCount = 0;
            Budget60DominantLayoutCount = 0;
            Budget60DominantDrawCount = 0;
            Budget60DominantOtherUpdateCount = 0;
            _pendingUpdateMs = 0d;
            _pendingInputMs = 0d;
            _pendingLayoutMs = 0d;
        }

        private void AccumulateDominantMissPhase(double updateMs, double inputMs, double layoutMs, double drawMs)
        {
            var boundedUpdateMs = Math.Max(0d, updateMs);
            var boundedInputMs = Math.Max(0d, inputMs);
            var boundedLayoutMs = Math.Max(0d, layoutMs);
            var boundedDrawMs = Math.Max(0d, drawMs);
            var otherUpdateMs = Math.Max(0d, boundedUpdateMs - boundedInputMs - boundedLayoutMs);

            var dominantPhase = 3;
            var dominantValue = otherUpdateMs;
            if (boundedInputMs > dominantValue)
            {
                dominantPhase = 0;
                dominantValue = boundedInputMs;
            }

            if (boundedLayoutMs > dominantValue)
            {
                dominantPhase = 1;
                dominantValue = boundedLayoutMs;
            }

            if (boundedDrawMs > dominantValue)
            {
                dominantPhase = 2;
            }

            switch (dominantPhase)
            {
                case 0:
                    Budget60DominantInputCount++;
                    break;
                case 1:
                    Budget60DominantLayoutCount++;
                    break;
                case 2:
                    Budget60DominantDrawCount++;
                    break;
                default:
                    Budget60DominantOtherUpdateCount++;
                    break;
            }
        }

        private static void AppendCapped(List<double> samples, double value)
        {
            if (samples.Count >= FrameLatencyMaxSamples)
            {
                samples.RemoveAt(0);
            }

            samples.Add(Math.Max(0d, value));
        }
    }
}
