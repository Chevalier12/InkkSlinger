using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace InkkSlinger;

internal static class RoutedEventDispatchDiagnostics
{
    private const int FlushEverySamples = 160;
    private const int MinSamplesToFlush = 32;
    private const double IdleFlushMs = 900d;
    private static readonly bool IsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_SLOW_ROUTE_LOGS"), "1", StringComparison.Ordinal);
    private static readonly double SlowThresholdMs = ParseThresholdMs();

    private sealed class Aggregate
    {
        public int Count;
        public int SlowCount;
        public double TotalMs;
        public double MaxMs;
    }

    private static readonly Dictionary<string, Aggregate> RouteTotals = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Aggregate> HandlerTotals = new(StringComparer.Ordinal);
    private static int _sampleCount;
    private static int _slowRouteCount;
    private static long _firstTimestamp;
    private static long _lastTimestamp;

    internal static bool Enabled => IsEnabled;

    internal static void ObserveRouteTotal(UIElement target, RoutedEvent routedEvent, double elapsedMs)
    {
        if (!IsEnabled)
        {
            return;
        }

        EnsureWindowStarted();
        _sampleCount++;
        var routeKey = BuildRouteKey(target, routedEvent);
        ObserveAggregate(RouteTotals, routeKey, elapsedMs);
        if (elapsedMs >= SlowThresholdMs)
        {
            _slowRouteCount++;
            EmitSlowRoute(routeKey, elapsedMs);
        }

        _lastTimestamp = Stopwatch.GetTimestamp();
        TryFlush();
    }

    internal static void ObserveClassHandlers(UIElement target, RoutedEvent routedEvent, double elapsedMs)
    {
        if (!IsEnabled)
        {
            return;
        }

        EnsureWindowStarted();
        var key = $"{BuildRouteKey(target, routedEvent)}::ClassHandlers";
        ObserveAggregate(HandlerTotals, key, elapsedMs);
        _lastTimestamp = Stopwatch.GetTimestamp();
        TryFlush();
    }

    internal static void ObserveInstanceHandler(UIElement target, RoutedEvent routedEvent, Delegate handler, double elapsedMs)
    {
        if (!IsEnabled)
        {
            return;
        }

        EnsureWindowStarted();
        var key = $"{BuildRouteKey(target, routedEvent)}::{BuildHandlerKey(handler)}";
        ObserveAggregate(HandlerTotals, key, elapsedMs);
        _lastTimestamp = Stopwatch.GetTimestamp();
        TryFlush();
    }

    private static void EnsureWindowStarted()
    {
        if (_firstTimestamp != 0)
        {
            return;
        }

        _firstTimestamp = Stopwatch.GetTimestamp();
        _lastTimestamp = _firstTimestamp;
    }

    private static void ObserveAggregate(Dictionary<string, Aggregate> bucket, string key, double elapsedMs)
    {
        if (!bucket.TryGetValue(key, out var aggregate))
        {
            aggregate = new Aggregate();
            bucket[key] = aggregate;
        }

        var bounded = Math.Max(0d, elapsedMs);
        aggregate.Count++;
        aggregate.TotalMs += bounded;
        aggregate.MaxMs = Math.Max(aggregate.MaxMs, bounded);
        if (bounded >= SlowThresholdMs)
        {
            aggregate.SlowCount++;
        }
    }

    private static void TryFlush()
    {
        if (_sampleCount < MinSamplesToFlush)
        {
            return;
        }

        if (_sampleCount % FlushEverySamples != 0 &&
            Stopwatch.GetElapsedTime(_lastTimestamp).TotalMilliseconds < IdleFlushMs)
        {
            return;
        }

        var durationMs = Stopwatch.GetElapsedTime(_firstTimestamp).TotalMilliseconds;
        var routeTop = BuildTopSummary(RouteTotals, top: 6);
        var handlerTop = BuildTopSummary(HandlerTotals, top: 8);
        var line =
            $"[SlowRouteDispatch] threshold={SlowThresholdMs:0.##}ms samples={_sampleCount} slowRoutes={_slowRouteCount} " +
            $"window={durationMs:0}ms topRoutes={routeTop} topHandlers={handlerTop}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
        Reset();
    }

    private static void EmitSlowRoute(string routeKey, double elapsedMs)
    {
        var line = $"[SlowRouteDispatch.Event] route={routeKey} elapsed={elapsedMs:0.###}ms threshold={SlowThresholdMs:0.##}ms";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    private static string BuildTopSummary(Dictionary<string, Aggregate> bucket, int top)
    {
        if (bucket.Count == 0)
        {
            return "none";
        }

        var pairs = new List<KeyValuePair<string, Aggregate>>(bucket);
        pairs.Sort(static (left, right) =>
        {
            var byTotal = right.Value.TotalMs.CompareTo(left.Value.TotalMs);
            if (byTotal != 0)
            {
                return byTotal;
            }

            return right.Value.MaxMs.CompareTo(left.Value.MaxMs);
        });

        var count = Math.Min(top, pairs.Count);
        var parts = new string[count];
        for (var i = 0; i < count; i++)
        {
            var pair = pairs[i];
            var avg = pair.Value.Count > 0 ? pair.Value.TotalMs / pair.Value.Count : 0d;
            parts[i] = $"{pair.Key}(sum={pair.Value.TotalMs:0.###}ms avg={avg:0.###}ms max={pair.Value.MaxMs:0.###}ms n={pair.Value.Count})";
        }

        return string.Join(" | ", parts);
    }

    private static string BuildRouteKey(UIElement target, RoutedEvent routedEvent)
    {
        return $"{routedEvent.Name}@{target.GetType().Name}";
    }

    private static string BuildHandlerKey(Delegate handler)
    {
        var method = handler.Method;
        var owner = method.DeclaringType?.Name ?? "<anon>";
        var targetType = handler.Target?.GetType().Name;
        return targetType == null
            ? $"{owner}.{method.Name}"
            : $"{owner}.{method.Name}#{targetType}";
    }

    private static double ParseThresholdMs()
    {
        var raw = Environment.GetEnvironmentVariable("INKKSLINGER_SLOW_ROUTE_MS");
        return double.TryParse(raw, out var parsed) && parsed > 0d
            ? parsed
            : 8d;
    }

    private static void Reset()
    {
        RouteTotals.Clear();
        HandlerTotals.Clear();
        _sampleCount = 0;
        _slowRouteCount = 0;
        _firstTimestamp = 0;
        _lastTimestamp = 0;
    }
}
