using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace InkkSlinger;

internal static class UiFrameworkFileLoadDiagnostics
{
    private const double IdleFlushMs = 450d;
    private const int FlushSampleThreshold = 40;
    private static readonly bool IsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_FILE_LOAD_LOGS"), "1", StringComparison.Ordinal);

    private static readonly object Sync = new();
    private static readonly Dictionary<string, MetricBucket> Buckets = new(StringComparer.Ordinal);
    private static int _sampleCount;
    private static long _firstTimestamp;
    private static long _lastTimestamp;
    private static TimeSpan _firstProcessCpuTime;

    public static void Observe(string metric, double elapsedMs, int itemCount = 1)
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

            if (!Buckets.TryGetValue(metric, out var bucket))
            {
                bucket = new MetricBucket();
                Buckets[metric] = bucket;
            }

            bucket.SampleCount++;
            bucket.TotalMs += elapsedMs;
            bucket.MaxMs = Math.Max(bucket.MaxMs, elapsedMs);
            bucket.TotalItems += Math.Max(0, itemCount);

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

        var summary = new StringBuilder();
        summary.Append($"[FileLoadCpu] samples={_sampleCount} dur={durationMs:0}ms cpu={processCpuPct:0.0}%");
        foreach (var (metric, bucket) in Buckets)
        {
            var avgMs = bucket.SampleCount > 0 ? bucket.TotalMs / bucket.SampleCount : 0d;
            var avgPerItemMs = bucket.TotalItems > 0 ? bucket.TotalMs / bucket.TotalItems : 0d;
            summary.Append(
                $" | {metric}: n={bucket.SampleCount} avg={avgMs:0.0}ms max={bucket.MaxMs:0.0}ms items={bucket.TotalItems} msPerItem={avgPerItemMs:0.000}");
        }

        var message = summary.ToString();
        Debug.WriteLine(message);
        Console.WriteLine(message);
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

    private sealed class MetricBucket
    {
        public int SampleCount;
        public int TotalItems;
        public double TotalMs;
        public double MaxMs;
    }
}
