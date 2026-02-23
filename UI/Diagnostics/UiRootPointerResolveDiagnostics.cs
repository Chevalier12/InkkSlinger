using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private static readonly bool IsPointerResolveDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_POINTER_RESOLVE_LOGS"), "1", StringComparison.Ordinal);
    private static readonly bool IsPointerResolveVerboseEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_POINTER_RESOLVE_VERBOSE"), "1", StringComparison.Ordinal);
    private static readonly double PointerResolveSlowThresholdMs =
        ParsePointerResolveSlowThresholdMs(Environment.GetEnvironmentVariable("INKKSLINGER_POINTER_RESOLVE_SLOW_MS"), 16d);

    private readonly List<PointerResolveStepSample> _pointerResolveStepSamples = new(16);
    private Vector2 _pointerResolvePointerPosition;
    private bool _pointerResolveRequiresPreciseTarget;
    private bool _pointerResolveBypassClickShortcuts;
    private bool _hasPointerResolveContext;
    private long _pointerResolveStartTimestamp;
    private double _pointerResolveTotalMilliseconds;
    private string _pointerResolveFinalPath = "None";
    private string _pointerResolveFinalTargetType = "null";

    private readonly record struct PointerResolveStepSample(
        string Name,
        double DurationMilliseconds,
        bool Success,
        string Detail);

    private static double ParsePointerResolveSlowThresholdMs(string? raw, double fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0d)
        {
            return parsed;
        }

        return fallback;
    }

    private void BeginPointerResolveTrace(Vector2 pointerPosition, bool requiresPreciseTarget)
    {
        _pointerResolveStepSamples.Clear();
        _pointerResolvePointerPosition = pointerPosition;
        _pointerResolveRequiresPreciseTarget = requiresPreciseTarget;
        _pointerResolveBypassClickShortcuts = false;
        _pointerResolveStartTimestamp = Stopwatch.GetTimestamp();
        _pointerResolveTotalMilliseconds = 0d;
        _pointerResolveFinalPath = "None";
        _pointerResolveFinalTargetType = "null";
        _hasPointerResolveContext = true;
    }

    private void SetPointerResolveBypassFlag(bool bypass)
    {
        if (!_hasPointerResolveContext)
        {
            return;
        }

        _pointerResolveBypassClickShortcuts = bypass;
    }

    private void TracePointerResolveStep(string name, long startTimestamp, bool success, string? detail = null)
    {
        if (!_hasPointerResolveContext)
        {
            return;
        }

        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        _pointerResolveStepSamples.Add(new PointerResolveStepSample(
            name,
            elapsedMs,
            success,
            detail ?? string.Empty));
    }

    private UIElement? CompletePointerResolve(string path, UIElement? target)
    {
        _lastPointerResolvePath = path;
        if (_hasPointerResolveContext)
        {
            _pointerResolveTotalMilliseconds = Stopwatch.GetElapsedTime(_pointerResolveStartTimestamp).TotalMilliseconds;
            _pointerResolveFinalPath = path;
            _pointerResolveFinalTargetType = target?.GetType().Name ?? "null";
        }

        if (_pointerResolveRequiresPreciseTarget)
        {
            UpdateCachedPointerResolveTarget(_pointerResolvePointerPosition, target);
        }

        return target;
    }

    private void EmitPointerResolveDiagnosticsForClick(string phase, UIElement? target, int clickHitTests)
    {
        if (!_hasPointerResolveContext)
        {
            return;
        }

        var totalMs = _pointerResolveTotalMilliseconds;
        var shouldLog = IsPointerResolveDiagnosticsEnabled &&
                        (IsPointerResolveVerboseEnabled || totalMs >= PointerResolveSlowThresholdMs);
        if (shouldLog)
        {
            var accountedMs = SumStepDurations(_pointerResolveStepSamples);
            var orderedSteps = _pointerResolveStepSamples.Count > 0
                ? string.Join(" | ", BuildOrderedStepTokens(_pointerResolveStepSamples))
                : "none";
            var hottestSteps = _pointerResolveStepSamples.Count > 0
                ? string.Join(" | ", BuildHottestStepTokens(_pointerResolveStepSamples))
                : "none";
            var unaccountedMs = Math.Max(0d, totalMs - accountedMs);
            var summary =
                $"[PtrResolveCpu] phase={phase} total={totalMs:0.###}ms final={_pointerResolveFinalPath} target={_pointerResolveFinalTargetType} " +
                $"precise={(_pointerResolveRequiresPreciseTarget ? 1 : 0)} bypass={(_pointerResolveBypassClickShortcuts ? 1 : 0)} " +
                $"hitTests={Math.Max(0, clickHitTests)} pointer=({_pointerResolvePointerPosition.X:0.#},{_pointerResolvePointerPosition.Y:0.#}) " +
                $"cache(hovered={(_inputState.HoveredElement != null ? 1 : 0)} cachedClick={(_cachedClickTarget != null ? 1 : 0)} captured={(_inputState.CapturedPointerElement != null ? 1 : 0)}) " +
                $"steps(ordered={orderedSteps}) hot={hottestSteps} unaccounted={unaccountedMs:0.###}ms";
            Debug.WriteLine(summary);
            Console.WriteLine(summary);
        }

        _hasPointerResolveContext = false;
        _pointerResolveStepSamples.Clear();
        _pointerResolveTotalMilliseconds = 0d;
        _pointerResolveFinalPath = "None";
        _pointerResolveFinalTargetType = "null";
        _ = target;
    }

    private static double SumStepDurations(IReadOnlyList<PointerResolveStepSample> steps)
    {
        var total = 0d;
        for (var i = 0; i < steps.Count; i++)
        {
            total += steps[i].DurationMilliseconds;
        }

        return total;
    }

    private static IEnumerable<string> BuildOrderedStepTokens(IReadOnlyList<PointerResolveStepSample> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (string.IsNullOrWhiteSpace(step.Detail))
            {
                yield return $"{step.Name}:{step.DurationMilliseconds:0.###}ms/{(step.Success ? "hit" : "miss")}";
            }
            else
            {
                yield return $"{step.Name}:{step.DurationMilliseconds:0.###}ms/{(step.Success ? "hit" : "miss")}<{step.Detail}>";
            }
        }
    }

    private static IEnumerable<string> BuildHottestStepTokens(IReadOnlyList<PointerResolveStepSample> steps)
    {
        const int maxSteps = 4;
        var indexed = new List<(int Index, PointerResolveStepSample Step)>(steps.Count);
        for (var i = 0; i < steps.Count; i++)
        {
            indexed.Add((i, steps[i]));
        }

        indexed.Sort(static (left, right) => right.Step.DurationMilliseconds.CompareTo(left.Step.DurationMilliseconds));
        var count = Math.Min(maxSteps, indexed.Count);
        for (var i = 0; i < count; i++)
        {
            var sample = indexed[i].Step;
            yield return $"{sample.Name}:{sample.DurationMilliseconds:0.###}ms/{(sample.Success ? "hit" : "miss")}";
        }
    }
}
