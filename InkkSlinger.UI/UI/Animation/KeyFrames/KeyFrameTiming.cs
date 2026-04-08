using System;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

internal static class KeyFrameTiming
{
    public static TimeSpan ResolveNaturalDuration<TFrame>(
        IReadOnlyList<TFrame> frames,
        Func<TFrame, KeyTime> keyTimeSelector,
        TimeSpan dynamicFallbackDuration)
    {
        if (frames.Count == 0)
        {
            return TimeSpan.Zero;
        }

        var hasExplicit = false;
        var hasDynamic = false;
        var max = TimeSpan.Zero;
        foreach (var frame in frames)
        {
            var keyTime = keyTimeSelector(frame);
            if (keyTime.IsTimeSpan)
            {
                hasExplicit = true;
                var value = keyTime.TimeSpan!.Value;
                if (value > max)
                {
                    max = value;
                }
            }
            else
            {
                hasDynamic = true;
            }
        }

        if (hasExplicit)
        {
            return max;
        }

        return hasDynamic ? dynamicFallbackDuration : TimeSpan.Zero;
    }

    public static IReadOnlyList<(TFrame Frame, TimeSpan Time)> ResolveSchedule<TFrame>(
        IReadOnlyList<TFrame> frames,
        Func<TFrame, KeyTime> keyTimeSelector,
        Func<TFrame, object?> valueSelector,
        object? startValue,
        TimeSpan totalDuration,
        Func<object?, object?, float>? distanceCalculator)
    {
        var count = frames.Count;
        if (count == 0)
        {
            return Array.Empty<(TFrame Frame, TimeSpan Time)>();
        }

        var resolved = new TimeSpan?[count];
        for (var i = 0; i < count; i++)
        {
            var keyTime = keyTimeSelector(frames[i]);
            if (keyTime.IsTimeSpan)
            {
                resolved[i] = keyTime.TimeSpan!.Value;
            }
        }

        var index = 0;
        while (index < count)
        {
            if (resolved[index].HasValue)
            {
                index++;
                continue;
            }

            var runStart = index;
            while (index < count && !resolved[index].HasValue)
            {
                index++;
            }

            var runEnd = index - 1;
            var hasNextAnchor = index < count;

            var startTime = runStart > 0 && resolved[runStart - 1].HasValue
                ? resolved[runStart - 1]!.Value
                : TimeSpan.Zero;
            var endTime = hasNextAnchor ? resolved[index]!.Value : totalDuration;
            if (endTime < startTime)
            {
                endTime = startTime;
            }

            var allPaced = distanceCalculator != null;
            if (allPaced)
            {
                for (var i = runStart; i <= runEnd; i++)
                {
                    if (keyTimeSelector(frames[i]).Type != KeyTimeType.Paced)
                    {
                        allPaced = false;
                        break;
                    }
                }
            }

            if (allPaced && TryResolvePacedRun(
                    frames,
                    resolved,
                    runStart,
                    runEnd,
                    index,
                    startTime,
                    endTime,
                    valueSelector,
                    startValue,
                    distanceCalculator!))
            {
                continue;
            }

            ResolveUniformRun(resolved, runStart, runEnd, startTime, endTime, hasNextAnchor);
        }

        // Keep keyframe progression deterministic for ties while evaluating in timeline order.
        return frames
            .Select((frame, i) => (Frame: frame, Time: resolved[i] ?? TimeSpan.Zero, Index: i))
            .OrderBy(x => x.Time)
            .ThenBy(x => x.Index)
            .Select(x => (x.Frame, x.Time))
            .ToList();
    }

    private static void ResolveUniformRun(
        TimeSpan?[] resolved,
        int runStart,
        int runEnd,
        TimeSpan startTime,
        TimeSpan endTime,
        bool hasNextAnchor)
    {
        var count = runEnd - runStart + 1;
        if (count <= 0)
        {
            return;
        }

        var denominator = hasNextAnchor ? count + 1 : count;
        var ticks = Math.Max(0L, endTime.Ticks - startTime.Ticks);
        for (var i = 0; i < count; i++)
        {
            var fraction = (double)(i + 1) / denominator;
            resolved[runStart + i] = startTime + TimeSpan.FromTicks((long)(ticks * fraction));
        }
    }

    private static bool TryResolvePacedRun<TFrame>(
        IReadOnlyList<TFrame> frames,
        TimeSpan?[] resolved,
        int runStart,
        int runEnd,
        int nextAnchorIndex,
        TimeSpan startTime,
        TimeSpan endTime,
        Func<TFrame, object?> valueSelector,
        object? startValue,
        Func<object?, object?, float> distanceCalculator)
    {
        var distances = new List<float>();
        var previous = runStart > 0 ? valueSelector(frames[runStart - 1]) : startValue;
        var cumulative = new float[runEnd - runStart + 1];
        var totalDistance = 0f;

        for (var i = runStart; i <= runEnd; i++)
        {
            var current = valueSelector(frames[i]);
            var distance = Math.Max(0f, distanceCalculator(previous, current));
            distances.Add(distance);
            totalDistance += distance;
            cumulative[i - runStart] = totalDistance;
            previous = current;
        }

        if (nextAnchorIndex < frames.Count)
        {
            var nextValue = valueSelector(frames[nextAnchorIndex]);
            totalDistance += Math.Max(0f, distanceCalculator(previous, nextValue));
        }

        if (totalDistance <= 0.0001f)
        {
            return false;
        }

        var spanTicks = Math.Max(0L, endTime.Ticks - startTime.Ticks);
        for (var i = runStart; i <= runEnd; i++)
        {
            var fraction = cumulative[i - runStart] / totalDistance;
            resolved[i] = startTime + TimeSpan.FromTicks((long)(spanTicks * fraction));
        }

        return true;
    }
}
