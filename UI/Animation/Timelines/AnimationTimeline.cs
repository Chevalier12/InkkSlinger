using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public abstract class AnimationTimeline : Timeline
{
    public string TargetName { get; set; } = string.Empty;

    public string TargetProperty { get; set; } = string.Empty;

    internal abstract object? GetCurrentValue(object? defaultOriginValue, object? defaultDestinationValue, float progress);
}

public sealed class DoubleAnimation : AnimationTimeline
{
    public float? From { get; set; }

    public float? To { get; set; }

    public float? By { get; set; }

    public IEasingFunction? EasingFunction { get; set; }

    internal override object? GetCurrentValue(object? defaultOriginValue, object? defaultDestinationValue, float progress)
    {
        var eased = EasingFunction?.Ease(progress) ?? progress;
        var from = From ?? ConvertToSingle(defaultOriginValue);
        var to = ResolveToValue(from, defaultDestinationValue);
        return MathHelper.Lerp(from, to, eased);
    }

    internal override TimeSpan ResolveNaturalDuration()
    {
        if (Duration.HasTimeSpan)
        {
            return Duration.TimeSpan!.Value;
        }

        return TimeSpan.FromSeconds(0.3);
    }

    private float ResolveToValue(float from, object? defaultDestinationValue)
    {
        if (To.HasValue)
        {
            return To.Value;
        }

        if (By.HasValue)
        {
            return from + By.Value;
        }

        return ConvertToSingle(defaultDestinationValue);
    }

    private static float ConvertToSingle(object? value)
    {
        return value switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            _ => 0f
        };
    }
}

public sealed class ColorAnimation : AnimationTimeline
{
    public Microsoft.Xna.Framework.Color? From { get; set; }

    public Microsoft.Xna.Framework.Color? To { get; set; }

    public IEasingFunction? EasingFunction { get; set; }

    internal override object? GetCurrentValue(object? defaultOriginValue, object? defaultDestinationValue, float progress)
    {
        var eased = EasingFunction?.Ease(progress) ?? progress;
        var from = From ?? ConvertToColor(defaultOriginValue);
        var to = To ?? ConvertToColor(defaultDestinationValue);
        return Microsoft.Xna.Framework.Color.Lerp(from, to, eased);
    }

    internal override TimeSpan ResolveNaturalDuration()
    {
        if (Duration.HasTimeSpan)
        {
            return Duration.TimeSpan!.Value;
        }

        return TimeSpan.FromSeconds(0.3);
    }

    private static Microsoft.Xna.Framework.Color ConvertToColor(object? value)
    {
        return value is Microsoft.Xna.Framework.Color c ? c : Microsoft.Xna.Framework.Color.Transparent;
    }
}
