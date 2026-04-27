using System;
using System.Collections.Generic;

namespace InkkSlinger;

internal static class KeyFrameTiming
{
    public sealed class ScheduleCache<TFrame>
    {
        private int _count;
        private TimeSpan _totalDuration;
        private int _fingerprint;
        private bool _hasSchedule;
        private IReadOnlyList<(TFrame Frame, TimeSpan Time)> _schedule = Array.Empty<(TFrame Frame, TimeSpan Time)>();

        public IReadOnlyList<(TFrame Frame, TimeSpan Time)> GetOrResolve(
            IReadOnlyList<TFrame> frames,
            Func<TFrame, KeyTime> keyTimeSelector,
            Func<TFrame, object?> valueSelector,
            object? startValue,
            TimeSpan totalDuration,
            Func<object?, object?, float>? distanceCalculator)
        {
            var count = frames.Count;
            var fingerprint = ComputeScheduleFingerprint(
                frames,
                keyTimeSelector,
                valueSelector,
                startValue,
                includeStartValue: distanceCalculator != null);
            if (_hasSchedule &&
                _count == count &&
                _totalDuration == totalDuration &&
                _fingerprint == fingerprint)
            {
                return _schedule;
            }

            _schedule = ResolveSchedule(
                frames,
                keyTimeSelector,
                valueSelector,
                startValue,
                totalDuration,
                distanceCalculator);
            _count = count;
            _totalDuration = totalDuration;
            _fingerprint = fingerprint;
            _hasSchedule = true;
            return _schedule;
        }
    }

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
        var ordered = new (TFrame Frame, TimeSpan Time, int Index)[count];
        for (var i = 0; i < count; i++)
        {
            ordered[i] = (frames[i], resolved[i] ?? TimeSpan.Zero, i);
        }

        Array.Sort(
            ordered,
            static (left, right) =>
            {
                var timeComparison = left.Time.CompareTo(right.Time);
                return timeComparison != 0 ? timeComparison : left.Index.CompareTo(right.Index);
            });

        var schedule = new (TFrame Frame, TimeSpan Time)[count];
        for (var i = 0; i < count; i++)
        {
            schedule[i] = (ordered[i].Frame, ordered[i].Time);
        }

        return schedule;
    }

    private static int ComputeScheduleFingerprint<TFrame>(
        IReadOnlyList<TFrame> frames,
        Func<TFrame, KeyTime> keyTimeSelector,
        Func<TFrame, object?> valueSelector,
        object? startValue,
        bool includeStartValue)
    {
        var hash = new HashCode();
        if (includeStartValue)
        {
            hash.Add(ComputeValueFingerprint(startValue));
        }

        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            hash.Add(keyTimeSelector(frame));
            hash.Add(ComputeValueFingerprint(valueSelector(frame)));
        }

        return hash.ToHashCode();
    }

    private static int ComputeValueFingerprint(object? value)
    {
        return value switch
        {
            null => 0,
            float number => HashCode.Combine(number),
            double number => HashCode.Combine(number),
            int number => HashCode.Combine(number),
            Microsoft.Xna.Framework.Vector2 vector => HashCode.Combine(vector.X, vector.Y),
            Microsoft.Xna.Framework.Color color => HashCode.Combine(color.R, color.G, color.B, color.A),
            Thickness thickness => HashCode.Combine(thickness.Left, thickness.Top, thickness.Right, thickness.Bottom),
            _ => value.GetHashCode()
        };
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
        var previous = runStart > 0 ? valueSelector(frames[runStart - 1]) : startValue;
        var cumulative = new float[runEnd - runStart + 1];
        var totalDistance = 0f;

        for (var i = runStart; i <= runEnd; i++)
        {
            var current = valueSelector(frames[i]);
            var distance = Math.Max(0f, distanceCalculator(previous, current));
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
