using System;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

public sealed class Int32Animation : AnimationTimeline
{
    public int? From { get; set; }

    public int? To { get; set; }

    public int? By { get; set; }

    public IEasingFunction? EasingFunction { get; set; }

    internal override object? GetCurrentValue(object? defaultOriginValue, object? defaultDestinationValue, float progress)
    {
        var eased = EasingFunction?.Ease(progress) ?? progress;
        var from = From ?? ConvertToInt32(defaultOriginValue);
        var to = ResolveToValue(from, defaultDestinationValue);
        return (int)Math.Round(from + ((to - from) * Math.Clamp(eased, 0f, 1f)));
    }

    internal override TimeSpan ResolveNaturalDuration()
    {
        if (Duration.HasTimeSpan)
        {
            return Duration.TimeSpan!.Value;
        }

        return TimeSpan.FromSeconds(0.3);
    }

    private int ResolveToValue(int from, object? defaultDestinationValue)
    {
        if (To.HasValue)
        {
            return To.Value;
        }

        if (By.HasValue)
        {
            return from + By.Value;
        }

        return ConvertToInt32(defaultDestinationValue);
    }

    private static int ConvertToInt32(object? value)
    {
        return value switch
        {
            int i => i,
            float f => (int)Math.Round(f),
            double d => (int)Math.Round(d),
            _ => 0
        };
    }
}

public abstract class Int32KeyFrame
{
    protected Int32KeyFrame(int value, KeyTime keyTime)
    {
        Value = value;
        KeyTime = keyTime;
    }

    public int Value { get; set; }

    public KeyTime KeyTime { get; set; }

    internal abstract int InterpolateValue(int baseValue, float keyFrameProgress);
}

public sealed class LinearInt32KeyFrame : Int32KeyFrame
{
    public LinearInt32KeyFrame()
        : this(0, TimeSpan.Zero)
    {
    }

    public LinearInt32KeyFrame(int value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    internal override int InterpolateValue(int baseValue, float keyFrameProgress)
    {
        var t = Math.Clamp(keyFrameProgress, 0f, 1f);
        return (int)Math.Round(baseValue + ((Value - baseValue) * t));
    }
}

public sealed class DiscreteInt32KeyFrame : Int32KeyFrame
{
    public DiscreteInt32KeyFrame()
        : this(0, TimeSpan.Zero)
    {
    }

    public DiscreteInt32KeyFrame(int value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    internal override int InterpolateValue(int baseValue, float keyFrameProgress)
    {
        return keyFrameProgress < 1f ? baseValue : Value;
    }
}

public sealed class SplineInt32KeyFrame : Int32KeyFrame
{
    public SplineInt32KeyFrame()
        : this(0, TimeSpan.Zero)
    {
    }

    public SplineInt32KeyFrame(int value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    public IEasingFunction? KeySpline { get; set; }

    internal override int InterpolateValue(int baseValue, float keyFrameProgress)
    {
        var t = KeySpline?.Ease(keyFrameProgress) ?? keyFrameProgress;
        return (int)Math.Round(baseValue + ((Value - baseValue) * Math.Clamp(t, 0f, 1f)));
    }
}

public sealed class Int32AnimationUsingKeyFrames : AnimationTimeline
{
    private readonly List<Int32KeyFrame> _keyFrames = new();

    public IList<Int32KeyFrame> KeyFrames => _keyFrames;

    internal override object? GetCurrentValue(object? defaultOriginValue, object? defaultDestinationValue, float progress)
    {
        var startValue = defaultOriginValue switch
        {
            int i => i,
            float f => (int)Math.Round(f),
            double d => (int)Math.Round(d),
            _ => 0
        };

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
            static (from, to) => MathF.Abs(ConvertToInt32(to) - ConvertToInt32(from)));
        var total = ResolveNaturalDuration();
        var now = TimeSpan.FromTicks((long)(total.Ticks * Math.Clamp(progress, 0f, 1f)));

        Int32KeyFrame? previousFrame = null;
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

    private static int ConvertToInt32(object? value)
    {
        return value switch
        {
            int i => i,
            float f => (int)Math.Round(f),
            double d => (int)Math.Round(d),
            _ => 0
        };
    }
}
