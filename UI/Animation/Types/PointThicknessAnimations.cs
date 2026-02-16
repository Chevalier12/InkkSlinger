using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class PointAnimation : AnimationTimeline
{
    public Vector2? From { get; set; }

    public Vector2? To { get; set; }

    public Vector2? By { get; set; }

    public IEasingFunction? EasingFunction { get; set; }

    internal override object? GetCurrentValue(object? defaultOriginValue, object? defaultDestinationValue, float progress)
    {
        var eased = EasingFunction?.Ease(progress) ?? progress;
        var from = From ?? ConvertToPoint(defaultOriginValue);
        var to = ResolveToValue(from, defaultDestinationValue);
        return Vector2.Lerp(from, to, Math.Clamp(eased, 0f, 1f));
    }

    internal override TimeSpan ResolveNaturalDuration()
    {
        if (Duration.HasTimeSpan)
        {
            return Duration.TimeSpan!.Value;
        }

        return TimeSpan.FromSeconds(0.3);
    }

    private Vector2 ResolveToValue(Vector2 from, object? defaultDestinationValue)
    {
        if (To.HasValue)
        {
            return To.Value;
        }

        if (By.HasValue)
        {
            return from + By.Value;
        }

        return ConvertToPoint(defaultDestinationValue);
    }

    private static Vector2 ConvertToPoint(object? value)
    {
        return value is Vector2 point ? point : Vector2.Zero;
    }
}

public sealed class ThicknessAnimation : AnimationTimeline
{
    public Thickness? From { get; set; }

    public Thickness? To { get; set; }

    public Thickness? By { get; set; }

    public IEasingFunction? EasingFunction { get; set; }

    internal override object? GetCurrentValue(object? defaultOriginValue, object? defaultDestinationValue, float progress)
    {
        var eased = EasingFunction?.Ease(progress) ?? progress;
        var from = From ?? ConvertToThickness(defaultOriginValue);
        var to = ResolveToValue(from, defaultDestinationValue);
        return LerpThickness(from, to, Math.Clamp(eased, 0f, 1f));
    }

    internal override TimeSpan ResolveNaturalDuration()
    {
        if (Duration.HasTimeSpan)
        {
            return Duration.TimeSpan!.Value;
        }

        return TimeSpan.FromSeconds(0.3);
    }

    private Thickness ResolveToValue(Thickness from, object? defaultDestinationValue)
    {
        if (To.HasValue)
        {
            return To.Value;
        }

        if (By.HasValue)
        {
            var delta = By.Value;
            return new Thickness(
                from.Left + delta.Left,
                from.Top + delta.Top,
                from.Right + delta.Right,
                from.Bottom + delta.Bottom);
        }

        return ConvertToThickness(defaultDestinationValue);
    }

    private static Thickness ConvertToThickness(object? value)
    {
        return value is Thickness thickness ? thickness : Thickness.Empty;
    }

    internal static Thickness LerpThickness(Thickness from, Thickness to, float progress)
    {
        return new Thickness(
            MathHelper.Lerp(from.Left, to.Left, progress),
            MathHelper.Lerp(from.Top, to.Top, progress),
            MathHelper.Lerp(from.Right, to.Right, progress),
            MathHelper.Lerp(from.Bottom, to.Bottom, progress));
    }
}

public abstract class PointKeyFrame
{
    protected PointKeyFrame(Vector2 value, KeyTime keyTime)
    {
        Value = value;
        KeyTime = keyTime;
    }

    public Vector2 Value { get; set; }

    public KeyTime KeyTime { get; set; }

    internal abstract Vector2 InterpolateValue(Vector2 baseValue, float keyFrameProgress);
}

public sealed class LinearPointKeyFrame : PointKeyFrame
{
    public LinearPointKeyFrame()
        : this(Vector2.Zero, TimeSpan.Zero)
    {
    }

    public LinearPointKeyFrame(Vector2 value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    internal override Vector2 InterpolateValue(Vector2 baseValue, float keyFrameProgress)
    {
        return Vector2.Lerp(baseValue, Value, Math.Clamp(keyFrameProgress, 0f, 1f));
    }
}

public sealed class DiscretePointKeyFrame : PointKeyFrame
{
    public DiscretePointKeyFrame()
        : this(Vector2.Zero, TimeSpan.Zero)
    {
    }

    public DiscretePointKeyFrame(Vector2 value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    internal override Vector2 InterpolateValue(Vector2 baseValue, float keyFrameProgress)
    {
        return keyFrameProgress < 1f ? baseValue : Value;
    }
}

public sealed class SplinePointKeyFrame : PointKeyFrame
{
    public SplinePointKeyFrame()
        : this(Vector2.Zero, TimeSpan.Zero)
    {
    }

    public SplinePointKeyFrame(Vector2 value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    public IEasingFunction? KeySpline { get; set; }

    internal override Vector2 InterpolateValue(Vector2 baseValue, float keyFrameProgress)
    {
        var t = KeySpline?.Ease(keyFrameProgress) ?? keyFrameProgress;
        return Vector2.Lerp(baseValue, Value, Math.Clamp(t, 0f, 1f));
    }
}

public abstract class ThicknessKeyFrame
{
    protected ThicknessKeyFrame(Thickness value, KeyTime keyTime)
    {
        Value = value;
        KeyTime = keyTime;
    }

    public Thickness Value { get; set; }

    public KeyTime KeyTime { get; set; }

    internal abstract Thickness InterpolateValue(Thickness baseValue, float keyFrameProgress);
}

public sealed class LinearThicknessKeyFrame : ThicknessKeyFrame
{
    public LinearThicknessKeyFrame()
        : this(Thickness.Empty, TimeSpan.Zero)
    {
    }

    public LinearThicknessKeyFrame(Thickness value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    internal override Thickness InterpolateValue(Thickness baseValue, float keyFrameProgress)
    {
        return ThicknessAnimation.LerpThickness(baseValue, Value, Math.Clamp(keyFrameProgress, 0f, 1f));
    }
}

public sealed class DiscreteThicknessKeyFrame : ThicknessKeyFrame
{
    public DiscreteThicknessKeyFrame()
        : this(Thickness.Empty, TimeSpan.Zero)
    {
    }

    public DiscreteThicknessKeyFrame(Thickness value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    internal override Thickness InterpolateValue(Thickness baseValue, float keyFrameProgress)
    {
        return keyFrameProgress < 1f ? baseValue : Value;
    }
}

public sealed class SplineThicknessKeyFrame : ThicknessKeyFrame
{
    public SplineThicknessKeyFrame()
        : this(Thickness.Empty, TimeSpan.Zero)
    {
    }

    public SplineThicknessKeyFrame(Thickness value, TimeSpan keyTime)
        : base(value, keyTime)
    {
    }

    public IEasingFunction? KeySpline { get; set; }

    internal override Thickness InterpolateValue(Thickness baseValue, float keyFrameProgress)
    {
        var t = KeySpline?.Ease(keyFrameProgress) ?? keyFrameProgress;
        return ThicknessAnimation.LerpThickness(baseValue, Value, Math.Clamp(t, 0f, 1f));
    }
}

public sealed class PointAnimationUsingKeyFrames : AnimationTimeline
{
    private readonly List<PointKeyFrame> _keyFrames = new();

    public IList<PointKeyFrame> KeyFrames => _keyFrames;

    internal override object? GetCurrentValue(object? defaultOriginValue, object? defaultDestinationValue, float progress)
    {
        var startValue = defaultOriginValue is Vector2 point ? point : Vector2.Zero;
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
                var a = from is Vector2 fromPoint ? fromPoint : Vector2.Zero;
                var b = to is Vector2 toPoint ? toPoint : Vector2.Zero;
                return Vector2.Distance(a, b);
            });
        var total = ResolveNaturalDuration();
        var now = TimeSpan.FromTicks((long)(total.Ticks * Math.Clamp(progress, 0f, 1f)));

        PointKeyFrame? previousFrame = null;
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

public sealed class ThicknessAnimationUsingKeyFrames : AnimationTimeline
{
    private readonly List<ThicknessKeyFrame> _keyFrames = new();

    public IList<ThicknessKeyFrame> KeyFrames => _keyFrames;

    internal override object? GetCurrentValue(object? defaultOriginValue, object? defaultDestinationValue, float progress)
    {
        var startValue = defaultOriginValue is Thickness thickness ? thickness : Thickness.Empty;
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
                var a = from is Thickness fromThickness ? fromThickness : Thickness.Empty;
                var b = to is Thickness toThickness ? toThickness : Thickness.Empty;
                var dl = b.Left - a.Left;
                var dt = b.Top - a.Top;
                var dr = b.Right - a.Right;
                var db = b.Bottom - a.Bottom;
                return MathF.Sqrt((dl * dl) + (dt * dt) + (dr * dr) + (db * db));
            });
        var total = ResolveNaturalDuration();
        var now = TimeSpan.FromTicks((long)(total.Ticks * Math.Clamp(progress, 0f, 1f)));

        ThicknessKeyFrame? previousFrame = null;
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
