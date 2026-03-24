using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class AnimationManager
{
    private static readonly AnimationManager Instance = new();
    private readonly List<StoryboardInstance> _storyboards = new();
    private readonly Dictionary<StoryboardControlKey, StoryboardInstance> _controllableByName = new();
    private readonly Dictionary<AnimationLaneKey, AppliedLaneState> _appliedLanes = new();
    private ConditionalWeakTable<Storyboard, StoryboardInstance.PreparedStoryboardMetadata> _preparedStoryboardMetadata = new();
    private long _nextSequence;
    private int _nextStoryboardInstanceId;
    private TimeSpan _currentTime = TimeSpan.Zero;
    private int _beginStoryboardCallCount;
    private int _storyboardStartCount;
    private int _composePassCount;
    private int _laneApplicationCount;
    private int _sinkValueSetCount;
    private int _clearedLaneCount;
    private long _beginStoryboardElapsedTicks;
    private long _storyboardStartElapsedTicks;
    private long _storyboardUpdateElapsedTicks;
    private long _composeElapsedTicks;
    private long _composeCollectElapsedTicks;
    private long _composeSortElapsedTicks;
    private long _composeMergeElapsedTicks;
    private long _composeApplyElapsedTicks;
    private long _composeBatchBeginElapsedTicks;
    private long _composeBatchEndElapsedTicks;
    private long _sinkSetValueElapsedTicks;
    private long _cleanupCompletedElapsedTicks;
    private readonly Dictionary<string, (int Count, long Ticks)> _setValueByPath = new(StringComparer.Ordinal);

    public static AnimationManager Current => Instance;

    public TimeSpan CurrentTime => _currentTime;

    public bool HasRunningAnimations =>
        _storyboards.Any(static storyboard => storyboard.HasTimeAdvancingAnimations);

    public void ResetForTests()
    {
        foreach (var state in _appliedLanes.Values)
        {
            try
            {
                state.Sink.ClearValue(state.BaseValue);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
            {
                // Test reset must be best-effort even if a lane targeted a now-frozen CLR object.
            }
            catch (InvalidOperationException)
            {
                // Test reset must be best-effort even if a lane targeted a now-frozen CLR object.
            }
        }

        _appliedLanes.Clear();
        _preparedStoryboardMetadata = new ConditionalWeakTable<Storyboard, StoryboardInstance.PreparedStoryboardMetadata>();
        _storyboards.Clear();
        _controllableByName.Clear();
        _nextSequence = 0L;
        _currentTime = TimeSpan.Zero;
        ResetTelemetryForTests();
    }

    public void Update(GameTime gameTime)
    {
        _currentTime = gameTime.TotalGameTime;
        if (_storyboards.Count == 0 && _appliedLanes.Count == 0)
        {
            return;
        }

        var updateStartTicks = Stopwatch.GetTimestamp();
        foreach (var instance in _storyboards)
        {
            instance.Update(_currentTime);
        }
        _storyboardUpdateElapsedTicks += Stopwatch.GetTimestamp() - updateStartTicks;

        ComposeAndApplyActiveLanes();
        var cleanupStartTicks = Stopwatch.GetTimestamp();
        CleanupCompletedStoryboards();
        _cleanupCompletedElapsedTicks += Stopwatch.GetTimestamp() - cleanupStartTicks;
    }

    public void BeginStoryboard(
        Storyboard storyboard,
        FrameworkElement containingObject,
        string? controlName,
        Func<string, object?>? resolveTargetByName,
        bool isControllable,
        HandoffBehavior handoff)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _beginStoryboardCallCount++;
        try
        {
            var instance = new StoryboardInstance(
                this,
                storyboard,
                containingObject,
                resolveTargetByName,
                _currentTime,
                controlName,
                isControllable);

            var storyboardStartTicks = Stopwatch.GetTimestamp();
            instance.Start(handoff);
            _storyboardStartElapsedTicks += Stopwatch.GetTimestamp() - storyboardStartTicks;
            _storyboardStartCount++;
            _storyboards.Add(instance);
            InvalidateFrozenLaneState(instance);
            if (isControllable && !string.IsNullOrWhiteSpace(controlName))
            {
                _controllableByName[new StoryboardControlKey(containingObject, controlName)] = instance;
            }
        }
        finally
        {
            _beginStoryboardElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        }
    }

    public void PrepareStoryboardMetadata(Storyboard storyboard)
    {
        _ = GetOrCreatePreparedStoryboardMetadata(storyboard);
    }

    public void PauseStoryboard(Storyboard storyboard, FrameworkElement containingObject)
    {
        foreach (var instance in FindInstances(storyboard, containingObject))
        {
            instance.Pause(_currentTime);
        }
    }

    public void ResumeStoryboard(Storyboard storyboard, FrameworkElement containingObject)
    {
        foreach (var instance in FindInstances(storyboard, containingObject))
        {
            instance.Resume(_currentTime);
            InvalidateFrozenLaneState(instance);
        }
    }

    public void StopStoryboard(Storyboard storyboard, FrameworkElement containingObject)
    {
        foreach (var instance in FindInstances(storyboard, containingObject))
        {
            instance.Stop();
        }
    }

    public void RemoveStoryboard(Storyboard storyboard, FrameworkElement containingObject)
    {
        foreach (var instance in FindInstances(storyboard, containingObject))
        {
            instance.Remove();
        }
    }

    public void SeekStoryboard(Storyboard storyboard, FrameworkElement containingObject, TimeSpan offset)
    {
        foreach (var instance in FindInstances(storyboard, containingObject))
        {
            instance.Seek(offset, TimeSeekOrigin.BeginTime, _currentTime);
            InvalidateFrozenLaneState(instance);
        }
    }

    public void SetStoryboardSpeedRatio(Storyboard storyboard, FrameworkElement containingObject, float speedRatio)
    {
        foreach (var instance in FindInstances(storyboard, containingObject))
        {
            instance.SpeedRatio = Math.Max(0.01f, speedRatio);
            InvalidateFrozenLaneState(instance);
        }
    }

    public void SkipStoryboardToFill(Storyboard storyboard, FrameworkElement containingObject)
    {
        foreach (var instance in FindInstances(storyboard, containingObject))
        {
            instance.SkipToFill(_currentTime);
            InvalidateFrozenLaneState(instance);
        }
    }

    internal bool TryResolveControllable(FrameworkElement containingObject, string controlName, out StoryboardInstance? instance)
    {
        return _controllableByName.TryGetValue(new StoryboardControlKey(containingObject, controlName), out instance);
    }

    internal void SetControllableSpeedRatio(StoryboardInstance instance, float speedRatio)
    {
        instance.SpeedRatio = Math.Max(0.01f, speedRatio);
        InvalidateFrozenLaneState(instance);
    }

    internal long ReserveSequence()
    {
        return ++_nextSequence;
    }

    internal int ReserveStoryboardInstanceId()
    {
        return ++_nextStoryboardInstanceId;
    }

    internal void RemoveFromLane(AnimationLaneKey key, StoryboardInstance owner, HandoffBehavior handoff)
    {
        if (handoff != HandoffBehavior.SnapshotAndReplace)
        {
            return;
        }

        InvalidateFrozenLaneState(key);

        foreach (var instance in _storyboards)
        {
            if (ReferenceEquals(instance, owner))
            {
                continue;
            }

            instance.RemoveLane(key);
        }
    }

    internal void RemoveFromLanes(IReadOnlyCollection<AnimationLaneKey> keys, StoryboardInstance owner, HandoffBehavior handoff)
    {
        if (handoff != HandoffBehavior.SnapshotAndReplace || keys.Count == 0)
        {
            return;
        }

        foreach (var key in keys)
        {
            InvalidateFrozenLaneState(key);
        }

        foreach (var instance in _storyboards)
        {
            if (ReferenceEquals(instance, owner))
            {
                continue;
            }

            instance.RemoveLanes(keys);
        }
    }

    private IEnumerable<StoryboardInstance> FindInstances(Storyboard storyboard, FrameworkElement containingObject)
    {
        return _storyboards.Where(s => ReferenceEquals(s.Storyboard, storyboard) && ReferenceEquals(s.Scope, containingObject));
    }

    internal StoryboardInstance.PreparedStoryboardMetadata GetOrCreatePreparedStoryboardMetadata(Storyboard storyboard)
    {
        if (_preparedStoryboardMetadata.TryGetValue(storyboard, out var prepared))
        {
            return prepared;
        }

        prepared = StoryboardInstance.PreparedStoryboardMetadata.Create(storyboard);
        _preparedStoryboardMetadata.Add(storyboard, prepared);
        return prepared;
    }

    private void ComposeAndApplyActiveLanes()
    {
        var composeStartTicks = Stopwatch.GetTimestamp();
        _composePassCount++;
        try
        {
            var pendingWrites = new List<PendingAnimationWrite>();
            var activeByLane = new Dictionary<AnimationLaneKey, List<LaneContribution>>();
            var collectStartTicks = Stopwatch.GetTimestamp();
            foreach (var storyboard in _storyboards)
            {
                foreach (var entry in storyboard.Entries)
                {
                    if (_appliedLanes.TryGetValue(entry.Key, out var appliedState) && appliedState.IsParkedFrozen)
                    {
                        continue;
                    }

                    if (!entry.TryGetContribution(out var contribution))
                    {
                        continue;
                    }

                    if (!activeByLane.TryGetValue(contribution.Key, out var lane))
                    {
                        lane = new List<LaneContribution>();
                        activeByLane[contribution.Key] = lane;
                    }

                    lane.Add(contribution);
                }
            }
            _composeCollectElapsedTicks += Stopwatch.GetTimestamp() - collectStartTicks;

            var inactive = _appliedLanes.Keys.Where(k => !activeByLane.ContainsKey(k)).ToList();
            foreach (var key in inactive)
            {
                var state = _appliedLanes[key];
                if (state.IsParkedFrozen)
                {
                    if (state.HasLastAppliedValue && ValuesEqual(state.LastAppliedValue, state.BaseValue))
                    {
                        state.Sink.ClearValue(state.BaseValue);
                        _clearedLaneCount++;
                        _appliedLanes.Remove(key);
                    }

                    continue;
                }

                state.Sink.ClearValue(state.BaseValue);
                _clearedLaneCount++;
                _appliedLanes.Remove(key);
            }

            foreach (var pair in activeByLane)
            {
                var key = pair.Key;
                var sortStartTicks = Stopwatch.GetTimestamp();
                var laneContributions = pair.Value.OrderBy(c => c.Sequence).ToList();
                _composeSortElapsedTicks += Stopwatch.GetTimestamp() - sortStartTicks;
                var sink = laneContributions[0].Sink;
                if (!_appliedLanes.TryGetValue(key, out var applied))
                {
                    applied = new AppliedLaneState(sink, sink.GetValue());
                    _appliedLanes[key] = applied;
                }

                var hasTimeAdvancingContribution = laneContributions.Exists(static contribution => contribution.IsTimeAdvancing);
                var contributionSignature = 0;
                if (!hasTimeAdvancingContribution)
                {
                    contributionSignature = ComputeContributionSignature(laneContributions);
                    if (applied.HasFrozenContributionSignature && applied.FrozenContributionSignature == contributionSignature)
                    {
                        applied.IsParkedFrozen = true;
                        continue;
                    }
                }

                var mergeStartTicks = Stopwatch.GetTimestamp();
                object? current = applied.BaseValue;
                for (var i = 0; i < laneContributions.Count; i++)
                {
                    var contribution = laneContributions[i];
                    current = i == 0
                        ? contribution.Value
                        : ComposeValue(current, contribution.OriginValue, contribution.Value);
                }

                var converted = ConvertForSinkType(current, sink.ValueType);
                if (!applied.HasLastAppliedValue || !ValuesEqual(applied.LastAppliedValue, converted))
                {
                    pendingWrites.Add(new PendingAnimationWrite(
                        sink,
                        converted,
                        laneContributions[0].TargetPropertyPath,
                        applied));
                }
                _composeMergeElapsedTicks += Stopwatch.GetTimestamp() - mergeStartTicks;

                _laneApplicationCount++;

                if (hasTimeAdvancingContribution)
                {
                    applied.HasFrozenContributionSignature = false;
                    applied.FrozenContributionSignature = 0;
                    applied.IsParkedFrozen = false;
                }
                else
                {
                    applied.HasFrozenContributionSignature = true;
                    applied.FrozenContributionSignature = contributionSignature;
                    applied.IsParkedFrozen = true;
                }
            }

            var applyStartTicks = Stopwatch.GetTimestamp();
            ApplyPendingWrites(pendingWrites);
            _composeApplyElapsedTicks += Stopwatch.GetTimestamp() - applyStartTicks;
        }
        finally
        {
            _composeElapsedTicks += Stopwatch.GetTimestamp() - composeStartTicks;
        }
    }

    private bool HasLiveContributionForLane(AnimationLaneKey key)
    {
        foreach (var storyboard in _storyboards)
        {
            foreach (var entry in storyboard.Entries)
            {
                if (!entry.Key.Equals(key))
                {
                    continue;
                }

                if (entry.TryGetContribution(out _))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void InvalidateFrozenLaneState(StoryboardInstance storyboard)
    {
        foreach (var entry in storyboard.Entries)
        {
            InvalidateFrozenLaneState(entry.Key);
        }
    }

    internal AnimationTelemetrySnapshot GetTelemetrySnapshotForTests()
    {
        return new AnimationTelemetrySnapshot(
            _beginStoryboardCallCount,
            _storyboardStartCount,
            _storyboards.Count,
            _appliedLanes.Count,
            _storyboards.Sum(static storyboard => storyboard.Entries.Count),
            _composePassCount,
            _laneApplicationCount,
            _sinkValueSetCount,
            _clearedLaneCount,
            TicksToMilliseconds(_beginStoryboardElapsedTicks),
            TicksToMilliseconds(_storyboardStartElapsedTicks),
            TicksToMilliseconds(_storyboardUpdateElapsedTicks),
            TicksToMilliseconds(_composeElapsedTicks),
            TicksToMilliseconds(_composeCollectElapsedTicks),
            TicksToMilliseconds(_composeSortElapsedTicks),
            TicksToMilliseconds(_composeMergeElapsedTicks),
            TicksToMilliseconds(_composeApplyElapsedTicks),
            TicksToMilliseconds(_composeBatchBeginElapsedTicks),
            TicksToMilliseconds(_composeBatchEndElapsedTicks),
            TicksToMilliseconds(_sinkSetValueElapsedTicks),
            TicksToMilliseconds(_cleanupCompletedElapsedTicks),
            SummarizeSetValuePaths(limit: 4));
    }

    internal void ResetTelemetryForTests()
    {
        _beginStoryboardCallCount = 0;
        _storyboardStartCount = 0;
        _composePassCount = 0;
        _laneApplicationCount = 0;
        _sinkValueSetCount = 0;
        _clearedLaneCount = 0;
        _beginStoryboardElapsedTicks = 0;
        _storyboardStartElapsedTicks = 0;
        _storyboardUpdateElapsedTicks = 0;
        _composeElapsedTicks = 0;
        _composeCollectElapsedTicks = 0;
        _composeSortElapsedTicks = 0;
        _composeMergeElapsedTicks = 0;
        _composeApplyElapsedTicks = 0;
        _composeBatchBeginElapsedTicks = 0;
        _composeBatchEndElapsedTicks = 0;
        _sinkSetValueElapsedTicks = 0;
        _cleanupCompletedElapsedTicks = 0;
        _setValueByPath.Clear();
    }

    private void RecordSetValuePathTiming(string targetPropertyPath, long ticks)
    {
        if (_setValueByPath.TryGetValue(targetPropertyPath, out var entry))
        {
            _setValueByPath[targetPropertyPath] = (entry.Count + 1, entry.Ticks + ticks);
            return;
        }

        _setValueByPath[targetPropertyPath] = (1, ticks);
    }

    private string SummarizeSetValuePaths(int limit)
    {
        if (_setValueByPath.Count == 0)
        {
            return "none";
        }

        return string.Join(
            ", ",
            _setValueByPath
                .OrderByDescending(static pair => pair.Value.Ticks)
                .Take(limit)
                .Select(pair => $"{pair.Key}(n={pair.Value.Count},ms={TicksToMilliseconds(pair.Value.Ticks):0.###})"));
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    private void ApplyPendingWrites(List<PendingAnimationWrite> pendingWrites)
    {
        if (pendingWrites.Count == 0)
        {
            return;
        }

        var batchedFreezables = new HashSet<Freezable>();
        try
        {
            var batchBeginStartTicks = Stopwatch.GetTimestamp();
            for (var i = 0; i < pendingWrites.Count; i++)
            {
                if (pendingWrites[i].Sink.BatchTarget is not Freezable freezable ||
                    !batchedFreezables.Add(freezable))
                {
                    continue;
                }

                freezable.BeginBatchUpdate();
            }
            _composeBatchBeginElapsedTicks += Stopwatch.GetTimestamp() - batchBeginStartTicks;

            for (var i = 0; i < pendingWrites.Count; i++)
            {
                var write = pendingWrites[i];
                var setStartTicks = Stopwatch.GetTimestamp();
                write.Sink.SetValue(write.Value);
                var setElapsedTicks = Stopwatch.GetTimestamp() - setStartTicks;
                _sinkSetValueElapsedTicks += setElapsedTicks;
                RecordSetValuePathTiming(write.TargetPropertyPath, setElapsedTicks);
                _sinkValueSetCount++;
                write.AppliedState.LastAppliedValue = write.Value;
                write.AppliedState.HasLastAppliedValue = true;
            }
        }
        finally
        {
            UIElement.BeginFreezableInvalidationBatch();
            var batchEndStartTicks = Stopwatch.GetTimestamp();
            try
            {
                foreach (var freezable in batchedFreezables)
                {
                    freezable.EndBatchUpdate();
                }
            }
            finally
            {
                UIElement.EndFreezableInvalidationBatch();
            }
            _composeBatchEndElapsedTicks += Stopwatch.GetTimestamp() - batchEndStartTicks;
        }
    }

    private void InvalidateFrozenLaneState(AnimationLaneKey key)
    {
        if (!_appliedLanes.TryGetValue(key, out var state))
        {
            return;
        }

        state.IsParkedFrozen = false;
        state.HasFrozenContributionSignature = false;
        state.FrozenContributionSignature = 0;
    }

    private static int ComputeContributionSignature(IReadOnlyList<LaneContribution> contributions)
    {
        var hash = new HashCode();
        for (var i = 0; i < contributions.Count; i++)
        {
            var contribution = contributions[i];
            hash.Add(contribution.OwnerStoryboardInstanceId);
            hash.Add(contribution.Sequence);
            hash.Add(contribution.TargetPropertyPath, StringComparer.Ordinal);
            hash.Add(ComputeValueHashCode(contribution.Value));
        }

        return hash.ToHashCode();
    }

    private static int ComputeValueHashCode(object? value)
    {
        if (value == null)
        {
            return 0;
        }

        return value switch
        {
            float number => HashCode.Combine(number),
            double number => HashCode.Combine(number),
            decimal number => HashCode.Combine(number),
            Vector2 vector => HashCode.Combine(vector.X, vector.Y),
            Thickness thickness => HashCode.Combine(thickness.Left, thickness.Top, thickness.Right, thickness.Bottom),
            Color color => HashCode.Combine(color.R, color.G, color.B, color.A),
            _ => value.GetHashCode()
        };
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        return left switch
        {
            float leftFloat when right is float rightFloat => leftFloat.Equals(rightFloat),
            double leftDouble when right is double rightDouble => leftDouble.Equals(rightDouble),
            decimal leftDecimal when right is decimal rightDecimal => leftDecimal.Equals(rightDecimal),
            Vector2 leftVector when right is Vector2 rightVector => leftVector.Equals(rightVector),
            Thickness leftThickness when right is Thickness rightThickness =>
                leftThickness.Left.Equals(rightThickness.Left) &&
                leftThickness.Top.Equals(rightThickness.Top) &&
                leftThickness.Right.Equals(rightThickness.Right) &&
                leftThickness.Bottom.Equals(rightThickness.Bottom),
            Color leftColor when right is Color rightColor => leftColor.Equals(rightColor),
            _ => left.Equals(right)
        };
    }

    private static object? ComposeValue(object? current, object? origin, object? value)
    {
        if (value == null)
        {
            return current;
        }

        if (current == null || origin == null)
        {
            return value;
        }

        if (TryConvertToDouble(current, out var currentDouble) &&
            TryConvertToDouble(origin, out var originDouble) &&
            TryConvertToDouble(value, out var valueDouble))
        {
            return currentDouble + (valueDouble - originDouble);
        }

        if (current is Color currentColor && origin is Color originColor && value is Color valueColor)
        {
            var dr = valueColor.R - originColor.R;
            var dg = valueColor.G - originColor.G;
            var db = valueColor.B - originColor.B;
            var da = valueColor.A - originColor.A;
            return new Color(
                (byte)Math.Clamp(currentColor.R + dr, byte.MinValue, byte.MaxValue),
                (byte)Math.Clamp(currentColor.G + dg, byte.MinValue, byte.MaxValue),
                (byte)Math.Clamp(currentColor.B + db, byte.MinValue, byte.MaxValue),
                (byte)Math.Clamp(currentColor.A + da, byte.MinValue, byte.MaxValue));
        }

        if (current is Vector2 currentPoint && origin is Vector2 originPoint && value is Vector2 valuePoint)
        {
            return currentPoint + (valuePoint - originPoint);
        }

        if (current is Thickness currentThickness &&
            origin is Thickness originThickness &&
            value is Thickness valueThickness)
        {
            return new Thickness(
                currentThickness.Left + (valueThickness.Left - originThickness.Left),
                currentThickness.Top + (valueThickness.Top - originThickness.Top),
                currentThickness.Right + (valueThickness.Right - originThickness.Right),
                currentThickness.Bottom + (valueThickness.Bottom - originThickness.Bottom));
        }

        return value;
    }

    private static object? ConvertForSinkType(object? value, Type targetType)
    {
        if (value == null)
        {
            return null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType == typeof(float) && value is double asDouble)
        {
            return (float)asDouble;
        }

        if (targetType == typeof(double) && value is float asFloat)
        {
            return (double)asFloat;
        }

        return Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool TryConvertToDouble(object value, out double result)
    {
        switch (value)
        {
            case byte b:
                result = b;
                return true;
            case sbyte sb:
                result = sb;
                return true;
            case short s:
                result = s;
                return true;
            case ushort us:
                result = us;
                return true;
            case int i:
                result = i;
                return true;
            case uint ui:
                result = ui;
                return true;
            case long l:
                result = l;
                return true;
            case ulong ul:
                result = ul;
                return true;
            case float f:
                result = f;
                return true;
            case double d:
                result = d;
                return true;
            case decimal m:
                result = (double)m;
                return true;
            default:
                result = 0d;
                return false;
        }
    }

    private void CleanupCompletedStoryboards()
    {
        for (var i = _storyboards.Count - 1; i >= 0; i--)
        {
            var instance = _storyboards[i];
            if (!instance.IsCompleted)
            {
                continue;
            }

            _storyboards.RemoveAt(i);
            if (instance.ControlName != null)
            {
                _controllableByName.Remove(new StoryboardControlKey(instance.Scope, instance.ControlName));
            }
        }
    }

    private sealed class AppliedLaneState
    {
        public AppliedLaneState(AnimationValueSink sink, object? baseValue)
        {
            Sink = sink;
            BaseValue = baseValue;
        }

        public AnimationValueSink Sink { get; }

        public object? BaseValue { get; }

        public object? LastAppliedValue { get; set; }

        public bool HasLastAppliedValue { get; set; }

        public int FrozenContributionSignature { get; set; }

        public bool HasFrozenContributionSignature { get; set; }

        public bool IsParkedFrozen { get; set; }
    }

    private readonly record struct PendingAnimationWrite(
        AnimationValueSink Sink,
        object? Value,
        string TargetPropertyPath,
        AppliedLaneState AppliedState);
}

internal sealed class StoryboardInstance
{
    private readonly AnimationManager _manager;
    private readonly List<AnimationLaneEntry> _entries = new();
    private readonly Func<string, object?>? _resolveTargetByName;
    private TimeSpan _startedAt;
    private TimeSpan _pauseStartedAt;
    private TimeSpan _pausedDuration = TimeSpan.Zero;

    public StoryboardInstance(
        AnimationManager manager,
        Storyboard storyboard,
        FrameworkElement scope,
        Func<string, object?>? resolveTargetByName,
        TimeSpan startedAt,
        string? controlName,
        bool isControllable)
    {
        _manager = manager;
        Storyboard = storyboard;
        Scope = scope;
        _resolveTargetByName = resolveTargetByName;
        _startedAt = startedAt;
        ControlName = controlName;
        IsControllable = isControllable;
        DebugId = manager.ReserveStoryboardInstanceId();
    }

    public int DebugId { get; }

    public Storyboard Storyboard { get; }

    public FrameworkElement Scope { get; }

    public string? ControlName { get; }

    public bool IsControllable { get; }

    public bool IsCompleted { get; private set; }

    public bool IsPaused { get; private set; }

    public bool HasTimeAdvancingAnimations =>
        !IsCompleted &&
        !IsPaused &&
        _entries.Exists(static entry => entry.IsTimeAdvancing && !entry.IsStopped);

    public float SpeedRatio { get; set; } = 1f;

    public IReadOnlyList<AnimationLaneEntry> Entries => _entries;

    public void Start(HandoffBehavior handoff)
    {
        var laneKeysToReplace = new HashSet<AnimationLaneKey>();
        var resolvedTargetCache = new Dictionary<string, object?>(StringComparer.Ordinal);
        var preparedMetadata = _manager.GetOrCreatePreparedStoryboardMetadata(Storyboard);

        foreach (var descriptor in preparedMetadata.Lanes)
        {
            var target = ResolveTarget(descriptor.Animation, Scope, _resolveTargetByName, resolvedTargetCache);
            if (target == null)
            {
                continue;
            }

            var sink = AnimationPropertyPathResolver.Resolve(target, descriptor.Animation.TargetProperty);
            if (sink == null)
            {
                continue;
            }

            laneKeysToReplace.Add(sink.Key);
            _entries.Add(
                new AnimationLaneEntry(
                    sink,
                    descriptor.Animation,
                    _startedAt,
                    _manager.ReserveSequence(),
                    descriptor.ParentBeginOffset,
                    descriptor.ParentSpeedRatio,
                    DebugId,
                    ControlName,
                    descriptor.Animation.TargetProperty));
        }

        _manager.RemoveFromLanes(laneKeysToReplace, this, handoff);
        IsCompleted = _entries.Count == 0;
    }

    public void Update(TimeSpan now)
    {
        if (IsCompleted || IsPaused)
        {
            return;
        }

        foreach (var entry in _entries)
        {
            entry.SpeedRatio = SpeedRatio * Math.Max(0.01f, entry.Animation.SpeedRatio);
            entry.Advance(now, _pausedDuration);
        }

        IsCompleted = _entries.All(e => e.IsStopped);
    }

    public void Pause(TimeSpan now)
    {
        if (IsCompleted || IsPaused)
        {
            return;
        }

        IsPaused = true;
        _pauseStartedAt = now;
    }

    public void Resume(TimeSpan now)
    {
        if (IsCompleted || !IsPaused)
        {
            return;
        }

        IsPaused = false;
        _pausedDuration += now - _pauseStartedAt;
    }

    public void Stop()
    {
        foreach (var entry in _entries)
        {
            entry.Stop();
        }

        IsCompleted = true;
    }

    public void Remove()
    {
        foreach (var entry in _entries)
        {
            entry.Remove();
        }

        IsCompleted = true;
    }

    public void Seek(TimeSpan offset, TimeSeekOrigin origin, TimeSpan now)
    {
        var clamped = offset < TimeSpan.Zero ? TimeSpan.Zero : offset;
        if (origin == TimeSeekOrigin.Duration)
        {
            var total = ResolveStoryboardDuration();
            clamped = total - clamped;
            if (clamped < TimeSpan.Zero)
            {
                clamped = TimeSpan.Zero;
            }
        }

        _startedAt = now - clamped;
        _pausedDuration = TimeSpan.Zero;
        if (IsPaused)
        {
            _pauseStartedAt = now;
        }

        foreach (var entry in _entries)
        {
            entry.Seek(now, _startedAt);
        }
    }

    public void SkipToFill(TimeSpan now)
    {
        if (Storyboard.RepeatBehavior.IsForever)
        {
            return;
        }

        foreach (var entry in _entries)
        {
            entry.SkipToFill(now, _startedAt);
        }

        IsCompleted = _entries.All(e => e.IsStopped);
    }

    public void RemoveLane(AnimationLaneKey key)
    {
        foreach (var entry in _entries)
        {
            if (entry.Key.Equals(key))
            {
                entry.Remove();
            }
        }

        IsCompleted = _entries.All(static e => e.IsStopped);
    }

    public void RemoveLanes(IReadOnlyCollection<AnimationLaneKey> keys)
    {
        if (keys.Count == 0)
        {
            return;
        }

        foreach (var entry in _entries)
        {
            if (keys.Contains(entry.Key))
            {
                entry.Remove();
            }
        }

        IsCompleted = _entries.All(static e => e.IsStopped);
    }

    private TimeSpan ResolveStoryboardDuration()
    {
        if (_entries.Count == 0)
        {
            return TimeSpan.Zero;
        }

        return _entries.Max(e => e.GetTotalDurationWithOffsets());
    }

    private static IEnumerable<LeafAnimationDescriptor> EnumerateLeafAnimations(
        Storyboard storyboard,
        TimeSpan parentBeginOffset,
        float parentSpeedRatio)
    {
        foreach (var child in storyboard.Children)
        {
            if (child is AnimationTimeline animation)
            {
                yield return new LeafAnimationDescriptor(animation, parentBeginOffset, parentSpeedRatio);
                continue;
            }

            if (child is Storyboard nested)
            {
                var nestedOffset = parentBeginOffset + nested.BeginTime;
                var nestedSpeed = parentSpeedRatio * Math.Max(0.01f, nested.SpeedRatio);
                foreach (var descriptor in EnumerateLeafAnimations(nested, nestedOffset, nestedSpeed))
                {
                    yield return descriptor;
                }
            }
        }
    }

    private static object? ResolveTarget(
        AnimationTimeline animation,
        FrameworkElement scope,
        Func<string, object?>? resolveByName,
        Dictionary<string, object?> resolvedTargetCache)
    {
        if (string.IsNullOrWhiteSpace(animation.TargetName))
        {
            return scope;
        }

        if (resolvedTargetCache.TryGetValue(animation.TargetName, out var cached))
        {
            return cached;
        }

        if (resolveByName != null)
        {
            var fromResolver = resolveByName(animation.TargetName);
            if (fromResolver != null)
            {
                resolvedTargetCache[animation.TargetName] = fromResolver;
                return fromResolver;
            }
        }

        var scoped = NameScopeService.FindName(scope, animation.TargetName);
        if (scoped != null)
        {
            resolvedTargetCache[animation.TargetName] = scoped;
            return scoped;
        }

        var resolved = scope.FindName(animation.TargetName);
        resolvedTargetCache[animation.TargetName] = resolved;
        return resolved;
    }

    internal readonly struct LeafAnimationDescriptor
    {
        public LeafAnimationDescriptor(AnimationTimeline animation, TimeSpan parentBeginOffset, float parentSpeedRatio)
        {
            Animation = animation;
            ParentBeginOffset = parentBeginOffset;
            ParentSpeedRatio = parentSpeedRatio;
        }

        public AnimationTimeline Animation { get; }

        public TimeSpan ParentBeginOffset { get; }

        public float ParentSpeedRatio { get; }
    }

    internal sealed class PreparedStoryboardMetadata
    {
        private PreparedStoryboardMetadata(List<LeafAnimationDescriptor> lanes)
        {
            Lanes = lanes;
        }

        internal IReadOnlyList<LeafAnimationDescriptor> Lanes { get; }

        internal static PreparedStoryboardMetadata Create(Storyboard storyboard)
        {
            var lanes = new List<LeafAnimationDescriptor>();
            foreach (var descriptor in EnumerateLeafAnimations(storyboard, TimeSpan.Zero, 1f))
            {
                lanes.Add(descriptor);
            }

            return new PreparedStoryboardMetadata(lanes);
        }
    }

}

internal sealed class AnimationLaneEntry
{
    private readonly AnimationValueSink _sink;
    private readonly object? _originValue;
    private readonly object? _destinationValue;
    private readonly TimeSpan _parentBeginOffset;
    private readonly float _parentSpeedRatio;
    private readonly int _ownerStoryboardInstanceId;
    private readonly string? _ownerControlName;
    private readonly string _targetPropertyPath;
    private TimeSpan _startedAt;

    public AnimationLaneEntry(
        AnimationValueSink sink,
        AnimationTimeline animation,
        TimeSpan startedAt,
        long sequence,
        TimeSpan parentBeginOffset,
        float parentSpeedRatio,
        int ownerStoryboardInstanceId,
        string? ownerControlName,
        string targetPropertyPath)
    {
        _sink = sink;
        Animation = animation;
        _startedAt = startedAt;
        Sequence = sequence;
        _parentBeginOffset = parentBeginOffset;
        _parentSpeedRatio = parentSpeedRatio;
        _ownerStoryboardInstanceId = ownerStoryboardInstanceId;
        _ownerControlName = ownerControlName;
        _targetPropertyPath = targetPropertyPath;
        _originValue = sink.GetValue();
        _destinationValue = _originValue;
    }

    public AnimationLaneKey Key => _sink.Key;

    public AnimationTimeline Animation { get; }

    public long Sequence { get; }

    public bool IsStopped { get; private set; }

    public bool IsTimeAdvancing { get; private set; }

    public float SpeedRatio { get; set; } = 1f;

    public void Seek(TimeSpan now, TimeSpan startedAt)
    {
        if (IsStopped)
        {
            return;
        }

        _isForcedFillHold = false;
        _startedAt = startedAt;
        Advance(now, TimeSpan.Zero);
    }

    public void SkipToFill(TimeSpan now, TimeSpan startedAt)
    {
        if (IsStopped)
        {
            return;
        }

        _startedAt = startedAt;
        _isForcedFillHold = false;
        IsTimeAdvancing = false;

        // Forever timelines do not have a terminal fill boundary to skip to.
        if (Animation.RepeatBehavior.IsForever)
        {
            return;
        }

        var effectiveDuration = ResolveEffectiveDuration(Animation);
        var totalActiveDuration = ResolveTotalActiveDuration(Animation, effectiveDuration);
        var totalActiveTicks = totalActiveDuration.Ticks;
        if (totalActiveTicks <= 0L)
        {
            totalActiveTicks = effectiveDuration.Ticks;
        }

        if (Animation.FillBehavior == FillBehavior.Stop)
        {
            IsStopped = true;
            _isForcedFillHold = false;
            _latest = null;
            return;
        }

        var cycleProgress = effectiveDuration.Ticks <= 0L
            ? 1f
            : ComputeCycleProgress(totalActiveTicks, effectiveDuration.Ticks, Animation.AutoReverse);
        var holdValue = ConvertForSink(Animation.GetCurrentValue(_originValue, _destinationValue, cycleProgress));
        _latest = new LaneContribution(
            Key,
            _sink,
            Sequence,
            _originValue,
            holdValue,
            isTimeAdvancing: false,
            _ownerStoryboardInstanceId,
            _ownerControlName,
            _targetPropertyPath);
        _isForcedFillHold = true;
    }

    public void Stop()
    {
        IsStopped = true;
        _isForcedFillHold = false;
        IsTimeAdvancing = false;
        _latest = null;
    }

    public void Remove()
    {
        IsStopped = true;
        _isForcedFillHold = false;
        IsTimeAdvancing = false;
        _latest = null;
    }

    public void Advance(TimeSpan now, TimeSpan pausedDuration)
    {
        if (IsStopped)
        {
            return;
        }

        if (_isForcedFillHold)
        {
            IsTimeAdvancing = false;
            return;
        }

        var startAt = _startedAt + _parentBeginOffset + Animation.BeginTime + pausedDuration;
        var elapsed = now - startAt;
        if (elapsed < TimeSpan.Zero)
        {
            _latest = null;
            IsTimeAdvancing = false;
            return;
        }

        var effectiveDuration = ResolveEffectiveDuration(Animation);
        if (effectiveDuration <= TimeSpan.Zero)
        {
            var finalValue = ConvertForSink(Animation.GetCurrentValue(_originValue, _destinationValue, 1f));
            if (Animation.FillBehavior == FillBehavior.Stop)
            {
                IsStopped = true;
                IsTimeAdvancing = false;
                _latest = null;
            }
            else
            {
                IsTimeAdvancing = false;
                _latest = new LaneContribution(
                    Key,
                    _sink,
                    Sequence,
                    _originValue,
                    finalValue,
                    isTimeAdvancing: false,
                    _ownerStoryboardInstanceId,
                    _ownerControlName,
                    _targetPropertyPath);
            }

            return;
        }

        var scaledTicks = (long)(elapsed.Ticks * SpeedRatio * _parentSpeedRatio);
        var scaled = TimeSpan.FromTicks(Math.Max(0L, scaledTicks));
        var cycleTicks = effectiveDuration.Ticks;
        var totalActiveDuration = ResolveTotalActiveDuration(Animation, effectiveDuration);
        var totalActiveTicks = Math.Max(0L, totalActiveDuration.Ticks);
        var scaledTicksClamped = Math.Max(0L, scaled.Ticks);

        if (!Animation.RepeatBehavior.IsForever && scaledTicksClamped >= totalActiveTicks)
        {
            if (Animation.FillBehavior == FillBehavior.HoldEnd && totalActiveTicks > 0L)
            {
                IsStopped = true;
                IsTimeAdvancing = false;
                var holdProgress = ComputeCycleProgress(totalActiveTicks, cycleTicks, Animation.AutoReverse);
                var holdValue = ConvertForSink(Animation.GetCurrentValue(_originValue, _destinationValue, holdProgress));
                _latest = new LaneContribution(
                    Key,
                    _sink,
                    Sequence,
                    _originValue,
                    holdValue,
                    isTimeAdvancing: false,
                    _ownerStoryboardInstanceId,
                    _ownerControlName,
                    _targetPropertyPath);
            }
            else
            {
                IsStopped = true;
                IsTimeAdvancing = false;
                _latest = null;
            }

            return;
        }

        IsTimeAdvancing = true;
        var cycleProgress = ComputeCycleProgress(scaledTicksClamped, cycleTicks, Animation.AutoReverse);
        var currentValue = ConvertForSink(Animation.GetCurrentValue(_originValue, _destinationValue, Math.Clamp(cycleProgress, 0f, 1f)));
        _latest = new LaneContribution(
            Key,
            _sink,
            Sequence,
            _originValue,
            currentValue,
            isTimeAdvancing: true,
            _ownerStoryboardInstanceId,
            _ownerControlName,
            _targetPropertyPath);
    }

    public bool TryGetContribution(out LaneContribution contribution)
    {
        if (_latest.HasValue)
        {
            contribution = _latest.Value;
            return true;
        }

        contribution = default;
        return false;
    }

    public TimeSpan GetTotalActiveDuration()
    {
        var effectiveDuration = ResolveEffectiveDuration(Animation);
        return ResolveTotalActiveDuration(Animation, effectiveDuration);
    }

    public TimeSpan GetTotalDurationWithOffsets()
    {
        return _parentBeginOffset + Animation.BeginTime + GetTotalActiveDuration();
    }

    private static TimeSpan ResolveEffectiveDuration(Timeline timeline)
    {
        var baseDuration = timeline.ResolveNaturalDuration();
        if (timeline.AutoReverse)
        {
            baseDuration += baseDuration;
        }

        return baseDuration;
    }

    private static TimeSpan ResolveTotalActiveDuration(Timeline timeline, TimeSpan effectiveDuration)
    {
        if (timeline.RepeatBehavior.IsForever)
        {
            return TimeSpan.MaxValue;
        }

        if (timeline.RepeatBehavior.HasDuration)
        {
            return timeline.RepeatBehavior.Duration!.Value;
        }

        var count = timeline.RepeatBehavior.HasCount ? timeline.RepeatBehavior.Count!.Value : 1d;
        return TimeSpan.FromTicks((long)(effectiveDuration.Ticks * Math.Max(0d, count)));
    }

    private object? ConvertForSink(object? value)
    {
        if (value == null)
        {
            return null;
        }

        var targetType = _sink.ValueType;
        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType == typeof(float) && value is double asDouble)
        {
            return (float)asDouble;
        }

        if (targetType == typeof(double) && value is float asFloat)
        {
            return (double)asFloat;
        }

        return Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
    }

    private LaneContribution? _latest;
    private bool _isForcedFillHold;

    private static float ComputeCycleProgress(long timelineTicks, long cycleTicks, bool autoReverse)
    {
        if (cycleTicks <= 0)
        {
            return 1f;
        }

        var local = timelineTicks % cycleTicks;
        if (timelineTicks > 0 && local == 0)
        {
            local = cycleTicks;
        }

        var progress = (float)local / cycleTicks;
        if (!autoReverse)
        {
            return Math.Clamp(progress, 0f, 1f);
        }

        var mapped = progress <= 0.5f
            ? progress * 2f
            : 1f - ((progress - 0.5f) * 2f);
        return Math.Clamp(mapped, 0f, 1f);
    }
}

internal readonly struct LaneContribution
{
    public LaneContribution(
        AnimationLaneKey key,
        AnimationValueSink sink,
        long sequence,
        object? originValue,
        object? value,
        bool isTimeAdvancing,
        int ownerStoryboardInstanceId,
        string? ownerControlName,
        string targetPropertyPath)
    {
        Key = key;
        Sink = sink;
        Sequence = sequence;
        OriginValue = originValue;
        Value = value;
        IsTimeAdvancing = isTimeAdvancing;
        OwnerStoryboardInstanceId = ownerStoryboardInstanceId;
        OwnerControlName = ownerControlName;
        TargetPropertyPath = targetPropertyPath;
    }

    public AnimationLaneKey Key { get; }

    public AnimationValueSink Sink { get; }

    public long Sequence { get; }

    public object? OriginValue { get; }

    public object? Value { get; }

    public bool IsTimeAdvancing { get; }

    public int OwnerStoryboardInstanceId { get; }

    public string? OwnerControlName { get; }

    public string TargetPropertyPath { get; }
}
