using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace InkkSlinger;

internal static class UiFrameworkPopulationPhaseDiagnostics
{
    private const double IdleFlushMs = 450d;
    private const int FlushSampleThreshold = 80;
    private static readonly bool IsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_POPULATION_PHASE_LOGS"), "1", StringComparison.Ordinal);

    private static readonly object Sync = new();
    private static readonly Dictionary<string, Bucket> Buckets = new(StringComparer.Ordinal);
    private static int _sampleCount;
    private static long _firstTimestamp;
    private static long _lastTimestamp;
    private static TimeSpan _firstProcessCpuTime;

    public static void Observe(string phase, double elapsedMs, int count = 1)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (Sync)
        {
            var now = Stopwatch.GetTimestamp();
            if (_sampleCount > 0 && Stopwatch.GetElapsedTime(_lastTimestamp, now).TotalMilliseconds >= IdleFlushMs)
            {
                FlushInternal();
            }

            if (_sampleCount == 0)
            {
                _firstTimestamp = now;
                _firstProcessCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
            }

            _lastTimestamp = now;
            _sampleCount++;
            if (!Buckets.TryGetValue(phase, out var bucket))
            {
                bucket = new Bucket();
                Buckets[phase] = bucket;
            }

            bucket.Samples++;
            bucket.TotalMs += elapsedMs;
            bucket.MaxMs = Math.Max(bucket.MaxMs, elapsedMs);
            bucket.Count += Math.Max(0, count);

            if (_sampleCount >= FlushSampleThreshold)
            {
                FlushInternal();
            }
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
            FlushInternal();
        }
    }

    private static void FlushInternal()
    {
        if (_sampleCount <= 0)
        {
            Reset();
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
        sb.Append($"[PopulationPhaseCpu] samples={_sampleCount} dur={durationMs:0}ms cpu={processCpuPct:0.0}%");
        foreach (var (phase, bucket) in Buckets)
        {
            var avg = bucket.Samples > 0 ? bucket.TotalMs / bucket.Samples : 0d;
            var perCount = bucket.Count > 0 ? bucket.TotalMs / bucket.Count : 0d;
            sb.Append($" | {phase}: n={bucket.Samples} avg={avg:0.0}ms max={bucket.MaxMs:0.0}ms count={bucket.Count} msPerCount={perCount:0.000}");
        }

        var msg = sb.ToString();
        Debug.WriteLine(msg);
        Console.WriteLine(msg);
        Reset();
    }

    private static void Reset()
    {
        _sampleCount = 0;
        _firstTimestamp = 0L;
        _lastTimestamp = 0L;
        _firstProcessCpuTime = TimeSpan.Zero;
        Buckets.Clear();
    }

    private sealed class Bucket
    {
        public int Samples;
        public int Count;
        public double TotalMs;
        public double MaxMs;
    }
}
