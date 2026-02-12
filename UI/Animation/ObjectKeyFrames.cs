using System;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

public abstract class ObjectKeyFrame
{
    protected ObjectKeyFrame(object? value, KeyTime keyTime)
    {
        Value = value;
        KeyTime = keyTime;
    }

    public object? Value { get; set; }

    public KeyTime KeyTime { get; set; }

    internal abstract object? InterpolateValue(object? baseValue, float keyFrameProgress);
}

public sealed class DiscreteObjectKeyFrame : ObjectKeyFrame
{
    public DiscreteObjectKeyFrame()
        : this(null, TimeSpan.Zero)
    {
    }

    public DiscreteObjectKeyFrame(object? value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    internal override object? InterpolateValue(object? baseValue, float keyFrameProgress)
    {
        return keyFrameProgress < 1f ? baseValue : Value;
    }
}

public sealed class ObjectAnimationUsingKeyFrames : AnimationTimeline
{
    private readonly List<ObjectKeyFrame> _keyFrames = new();

    public IList<ObjectKeyFrame> KeyFrames => _keyFrames;

    internal override object? GetCurrentValue(object? defaultOriginValue, object? defaultDestinationValue, float progress)
    {
        var startValue = defaultOriginValue;
        if (_keyFrames.Count == 0)
        {
            return startValue;
        }

        var ordered = KeyFrameTiming.ResolveSchedule(
            _keyFrames,
            k => k.KeyTime,
            k => k.Value,
            startValue,
            ResolveNaturalDuration(),
            distanceCalculator: null);
        var total = ResolveNaturalDuration();
        var now = TimeSpan.FromTicks((long)(total.Ticks * Math.Clamp(progress, 0f, 1f)));

        ObjectKeyFrame? previousFrame = null;
        TimeSpan previousTime = TimeSpan.Zero;
        foreach (var (frame, frameTime) in ordered)
        {
            if (now <= frameTime)
            {
                var startTime = previousFrame == null ? TimeSpan.Zero : previousTime;
                var segmentDurationTicks = Math.Max(1L, frameTime.Ticks - startTime.Ticks);
                var segmentProgress = (float)(now.Ticks - startTime.Ticks) / segmentDurationTicks;
                var baseValue = previousFrame?.Value ?? startValue;
                return frame.InterpolateValue(baseValue, segmentProgress);
            }

            previousFrame = frame;
            previousTime = frameTime;
        }

        return ordered[^1].Frame.Value;
    }

    internal override TimeSpan ResolveNaturalDuration()
    {
        if (Duration.HasTimeSpan)
        {
            return Duration.TimeSpan!.Value;
        }

        if (_keyFrames.Count == 0)
        {
            return TimeSpan.Zero;
        }

        return KeyFrameTiming.ResolveNaturalDuration(_keyFrames, k => k.KeyTime, TimeSpan.FromSeconds(1));
    }
}
