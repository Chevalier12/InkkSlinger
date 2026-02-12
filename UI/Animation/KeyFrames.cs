using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public abstract class DoubleKeyFrame
{
    protected DoubleKeyFrame(float value, KeyTime keyTime)
    {
        Value = value;
        KeyTime = keyTime;
    }

    public float Value { get; set; }

    public KeyTime KeyTime { get; set; }

    internal abstract float InterpolateValue(float baseValue, float keyFrameProgress);
}

public sealed class LinearDoubleKeyFrame : DoubleKeyFrame
{
    public LinearDoubleKeyFrame()
        : this(0f, TimeSpan.Zero)
    {
    }

    public LinearDoubleKeyFrame(float value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    internal override float InterpolateValue(float baseValue, float keyFrameProgress)
    {
        return MathHelper.Lerp(baseValue, Value, Math.Clamp(keyFrameProgress, 0f, 1f));
    }
}

public sealed class DiscreteDoubleKeyFrame : DoubleKeyFrame
{
    public DiscreteDoubleKeyFrame()
        : this(0f, TimeSpan.Zero)
    {
    }

    public DiscreteDoubleKeyFrame(float value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    internal override float InterpolateValue(float baseValue, float keyFrameProgress)
    {
        return keyFrameProgress < 1f ? baseValue : Value;
    }
}

public sealed class SplineDoubleKeyFrame : DoubleKeyFrame
{
    public SplineDoubleKeyFrame()
        : this(0f, TimeSpan.Zero)
    {
    }

    public SplineDoubleKeyFrame(float value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    public IEasingFunction? KeySpline { get; set; }

    internal override float InterpolateValue(float baseValue, float keyFrameProgress)
    {
        var t = KeySpline?.Ease(keyFrameProgress) ?? keyFrameProgress;
        return MathHelper.Lerp(baseValue, Value, Math.Clamp(t, 0f, 1f));
    }
}

public abstract class ColorKeyFrame
{
    protected ColorKeyFrame(Color value, KeyTime keyTime)
    {
        Value = value;
        KeyTime = keyTime;
    }

    public Color Value { get; set; }

    public KeyTime KeyTime { get; set; }

    internal abstract Color InterpolateValue(Color baseValue, float keyFrameProgress);
}

public sealed class LinearColorKeyFrame : ColorKeyFrame
{
    public LinearColorKeyFrame()
        : this(Color.Transparent, TimeSpan.Zero)
    {
    }

    public LinearColorKeyFrame(Color value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    internal override Color InterpolateValue(Color baseValue, float keyFrameProgress)
    {
        return Color.Lerp(baseValue, Value, Math.Clamp(keyFrameProgress, 0f, 1f));
    }
}

public sealed class DiscreteColorKeyFrame : ColorKeyFrame
{
    public DiscreteColorKeyFrame()
        : this(Color.Transparent, TimeSpan.Zero)
    {
    }

    public DiscreteColorKeyFrame(Color value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    internal override Color InterpolateValue(Color baseValue, float keyFrameProgress)
    {
        return keyFrameProgress < 1f ? baseValue : Value;
    }
}

public sealed class SplineColorKeyFrame : ColorKeyFrame
{
    public SplineColorKeyFrame()
        : this(Color.Transparent, TimeSpan.Zero)
    {
    }

    public SplineColorKeyFrame(Color value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    public IEasingFunction? KeySpline { get; set; }

    internal override Color InterpolateValue(Color baseValue, float keyFrameProgress)
    {
        var t = KeySpline?.Ease(keyFrameProgress) ?? keyFrameProgress;
        return Color.Lerp(baseValue, Value, Math.Clamp(t, 0f, 1f));
    }
}

public sealed class DoubleAnimationUsingKeyFrames : AnimationTimeline
{
    private readonly List<DoubleKeyFrame> _keyFrames = new();

    public IList<DoubleKeyFrame> KeyFrames => _keyFrames;

    internal override object? GetCurrentValue(object? defaultOriginValue, object? defaultDestinationValue, float progress)
    {
        var startValue = defaultOriginValue switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            _ => 0f
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
            static (from, to) =>
            {
                static float ToSingle(object? value)
                {
                    return value switch
                    {
                        float f => f,
                        double d => (float)d,
                        int i => i,
                        _ => 0f
                    };
                }

                return MathF.Abs(ToSingle(to) - ToSingle(from));
            });
        var total = ResolveNaturalDuration();
        var now = TimeSpan.FromTicks((long)(total.Ticks * Math.Clamp(progress, 0f, 1f)));

        DoubleKeyFrame? previousFrame = null;
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

public sealed class ColorAnimationUsingKeyFrames : AnimationTimeline
{
    private readonly List<ColorKeyFrame> _keyFrames = new();

    public IList<ColorKeyFrame> KeyFrames => _keyFrames;

    internal override object? GetCurrentValue(object? defaultOriginValue, object? defaultDestinationValue, float progress)
    {
        var startValue = defaultOriginValue is Color c ? c : Color.Transparent;
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
            static (from, to) =>
            {
                var a = from is Color fromColor ? fromColor : Color.Transparent;
                var b = to is Color toColor ? toColor : Color.Transparent;
                var dr = b.R - a.R;
                var dg = b.G - a.G;
                var db = b.B - a.B;
                var da = b.A - a.A;
                return MathF.Sqrt((dr * dr) + (dg * dg) + (db * db) + (da * da));
            });
        var total = ResolveNaturalDuration();
        var now = TimeSpan.FromTicks((long)(total.Ticks * Math.Clamp(progress, 0f, 1f)));

        ColorKeyFrame? previousFrame = null;
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
