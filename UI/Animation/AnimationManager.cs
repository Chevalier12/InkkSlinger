using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class AnimationManager
{
    private static readonly AnimationManager Instance = new();
    private readonly List<StoryboardInstance> _storyboards = new();
    private readonly Dictionary<StoryboardControlKey, StoryboardInstance> _controllableByName = new();
    private readonly Dictionary<AnimationLaneKey, AppliedLaneState> _appliedLanes = new();
    private long _nextSequence;
    private TimeSpan _currentTime = TimeSpan.Zero;

    public static AnimationManager Current => Instance;

    public TimeSpan CurrentTime => _currentTime;

    public bool HasRunningAnimations =>
        _storyboards.Any(static storyboard => !storyboard.IsCompleted && !storyboard.IsPaused);

    public void ResetForTests()
    {
        foreach (var state in _appliedLanes.Values)
        {
            state.Sink.ClearValue(state.BaseValue);
        }

        _appliedLanes.Clear();
        _storyboards.Clear();
        _controllableByName.Clear();
        _nextSequence = 0L;
        _currentTime = TimeSpan.Zero;
    }

    public void Update(GameTime gameTime)
    {
        _currentTime = gameTime.TotalGameTime;
        foreach (var instance in _storyboards)
        {
            instance.Update(_currentTime);
        }

        ComposeAndApplyActiveLanes();
        CleanupCompletedStoryboards();
    }

    public void BeginStoryboard(
        Storyboard storyboard,
        FrameworkElement containingObject,
        string? controlName,
        Func<string, object?>? resolveTargetByName,
        bool isControllable,
        HandoffBehavior handoff)
    {
        var instance = new StoryboardInstance(
            this,
            storyboard,
            containingObject,
            resolveTargetByName,
            _currentTime,
            controlName,
            isControllable);

        instance.Start(handoff);
        _storyboards.Add(instance);

        if (isControllable && !string.IsNullOrWhiteSpace(controlName))
        {
            _controllableByName[new StoryboardControlKey(containingObject, controlName)] = instance;
        }
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
        }
    }

    public void SetStoryboardSpeedRatio(Storyboard storyboard, FrameworkElement containingObject, float speedRatio)
    {
        foreach (var instance in FindInstances(storyboard, containingObject))
        {
            instance.SpeedRatio = Math.Max(0.01f, speedRatio);
        }
    }

    internal bool TryResolveControllable(FrameworkElement containingObject, string controlName, out StoryboardInstance? instance)
    {
        return _controllableByName.TryGetValue(new StoryboardControlKey(containingObject, controlName), out instance);
    }

    internal long ReserveSequence()
    {
        return ++_nextSequence;
    }

    internal void RemoveFromLane(AnimationLaneKey key, StoryboardInstance owner, HandoffBehavior handoff)
    {
        if (handoff != HandoffBehavior.SnapshotAndReplace)
        {
            return;
        }

        foreach (var instance in _storyboards)
        {
            if (ReferenceEquals(instance, owner))
            {
                continue;
            }

            instance.RemoveLane(key);
        }
    }

    private IEnumerable<StoryboardInstance> FindInstances(Storyboard storyboard, FrameworkElement containingObject)
    {
        return _storyboards.Where(s => ReferenceEquals(s.Storyboard, storyboard) && ReferenceEquals(s.Scope, containingObject));
    }

    private void ComposeAndApplyActiveLanes()
    {
        var activeByLane = new Dictionary<AnimationLaneKey, List<LaneContribution>>();
        foreach (var storyboard in _storyboards)
        {
            foreach (var entry in storyboard.Entries)
            {
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

        var inactive = _appliedLanes.Keys.Where(k => !activeByLane.ContainsKey(k)).ToList();
        foreach (var key in inactive)
        {
            var state = _appliedLanes[key];
            state.Sink.ClearValue(state.BaseValue);
            _appliedLanes.Remove(key);
        }

        foreach (var pair in activeByLane)
        {
            var key = pair.Key;
            var laneContributions = pair.Value.OrderBy(c => c.Sequence).ToList();
            var sink = laneContributions[0].Sink;
            if (!_appliedLanes.TryGetValue(key, out var applied))
            {
                applied = new AppliedLaneState(sink, sink.GetValue());
                _appliedLanes[key] = applied;
            }

            object? current = applied.BaseValue;
            for (var i = 0; i < laneContributions.Count; i++)
            {
                var contribution = laneContributions[i];
                current = i == 0
                    ? contribution.Value
                    : ComposeValue(current, contribution.OriginValue, contribution.Value);
            }

            sink.SetValue(ConvertForSinkType(current, sink.ValueType));
        }
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
    }
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
    }

    public Storyboard Storyboard { get; }

    public FrameworkElement Scope { get; }

    public string? ControlName { get; }

    public bool IsControllable { get; }

    public bool IsCompleted { get; private set; }

    public bool IsPaused { get; private set; }

    public float SpeedRatio { get; set; } = 1f;

    public IReadOnlyList<AnimationLaneEntry> Entries => _entries;

    public void Start(HandoffBehavior handoff)
    {
        foreach (var descriptor in EnumerateLeafAnimations(Storyboard, TimeSpan.Zero, 1f))
        {
            var animation = descriptor.Animation;
            var target = ResolveTarget(animation, Scope, _resolveTargetByName);
            if (target == null)
            {
                continue;
            }

            var sink = AnimationPropertyPathResolver.Resolve(target, animation.TargetProperty);
            if (sink == null)
            {
                continue;
            }

            _manager.RemoveFromLane(sink.Key, this, handoff);
            _entries.Add(
                new AnimationLaneEntry(
                    sink,
                    animation,
                    _startedAt,
                    _manager.ReserveSequence(),
                    descriptor.ParentBeginOffset,
                    descriptor.ParentSpeedRatio));
        }

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

    public void RemoveLane(AnimationLaneKey key)
    {
        foreach (var entry in _entries)
        {
            if (entry.Key.Equals(key))
            {
                entry.Remove();
            }
        }
    }

    private TimeSpan ResolveStoryboardDuration()
    {
        if (_entries.Count == 0)
        {
            return TimeSpan.Zero;
        }

        return _entries.Max(e => e.GetTotalDurationWithOffsets());
    }

    private static object? ResolveTarget(
        AnimationTimeline animation,
        FrameworkElement scope,
        Func<string, object?>? resolveByName)
    {
        if (string.IsNullOrWhiteSpace(animation.TargetName))
        {
            return scope;
        }

        if (resolveByName != null)
        {
            var fromResolver = resolveByName(animation.TargetName);
            if (fromResolver != null)
            {
                return fromResolver;
            }
        }

        var scoped = NameScopeService.FindName(scope, animation.TargetName);
        if (scoped != null)
        {
            return scoped;
        }

        return scope.FindName(animation.TargetName);
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

    private readonly struct LeafAnimationDescriptor
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
}

internal sealed class AnimationLaneEntry
{
    private readonly AnimationValueSink _sink;
    private readonly object? _originValue;
    private readonly object? _destinationValue;
    private readonly TimeSpan _parentBeginOffset;
    private readonly float _parentSpeedRatio;
    private TimeSpan _startedAt;

    public AnimationLaneEntry(
        AnimationValueSink sink,
        AnimationTimeline animation,
        TimeSpan startedAt,
        long sequence,
        TimeSpan parentBeginOffset,
        float parentSpeedRatio)
    {
        _sink = sink;
        Animation = animation;
        _startedAt = startedAt;
        Sequence = sequence;
        _parentBeginOffset = parentBeginOffset;
        _parentSpeedRatio = parentSpeedRatio;
        _originValue = sink.GetValue();
        _destinationValue = _originValue;
    }

    public AnimationLaneKey Key => _sink.Key;

    public AnimationTimeline Animation { get; }

    public long Sequence { get; }

    public bool IsStopped { get; private set; }

    public float SpeedRatio { get; set; } = 1f;

    public void Seek(TimeSpan now, TimeSpan startedAt)
    {
        if (IsStopped)
        {
            return;
        }

        _startedAt = startedAt;
        Advance(now, TimeSpan.Zero);
    }

    public void Stop()
    {
        IsStopped = true;
    }

    public void Remove()
    {
        IsStopped = true;
    }

    public void Advance(TimeSpan now, TimeSpan pausedDuration)
    {
        if (IsStopped)
        {
            return;
        }

        var startAt = _startedAt + _parentBeginOffset + Animation.BeginTime + pausedDuration;
        var elapsed = now - startAt;
        if (elapsed < TimeSpan.Zero)
        {
            _latest = null;
            return;
        }

        var effectiveDuration = ResolveEffectiveDuration(Animation);
        if (effectiveDuration <= TimeSpan.Zero)
        {
            var finalValue = ConvertForSink(Animation.GetCurrentValue(_originValue, _destinationValue, 1f));
            if (Animation.FillBehavior == FillBehavior.Stop)
            {
                IsStopped = true;
                _latest = null;
            }
            else
            {
                _latest = new LaneContribution(Key, _sink, Sequence, _originValue, finalValue);
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
                var holdProgress = ComputeCycleProgress(totalActiveTicks, cycleTicks, Animation.AutoReverse);
                var holdValue = ConvertForSink(Animation.GetCurrentValue(_originValue, _destinationValue, holdProgress));
                _latest = new LaneContribution(Key, _sink, Sequence, _originValue, holdValue);
            }
            else
            {
                IsStopped = true;
                _latest = null;
            }

            return;
        }

        var cycleProgress = ComputeCycleProgress(scaledTicksClamped, cycleTicks, Animation.AutoReverse);
        var currentValue = ConvertForSink(Animation.GetCurrentValue(_originValue, _destinationValue, Math.Clamp(cycleProgress, 0f, 1f)));
        _latest = new LaneContribution(Key, _sink, Sequence, _originValue, currentValue);
    }

    public bool TryGetContribution(out LaneContribution contribution)
    {
        if (_latest.HasValue && !IsStopped)
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
        object? value)
    {
        Key = key;
        Sink = sink;
        Sequence = sequence;
        OriginValue = originValue;
        Value = value;
    }

    public AnimationLaneKey Key { get; }

    public AnimationValueSink Sink { get; }

    public long Sequence { get; }

    public object? OriginValue { get; }

    public object? Value { get; }
}
