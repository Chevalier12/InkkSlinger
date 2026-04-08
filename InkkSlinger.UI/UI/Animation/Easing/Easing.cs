using System;

namespace InkkSlinger;

public interface IEasingFunction
{
    float Ease(float normalizedTime);
}

public abstract class EasingFunctionBase : IEasingFunction
{
    public EasingMode EasingMode { get; set; } = EasingMode.EaseOut;

    public float Ease(float normalizedTime)
    {
        var t = Math.Clamp(normalizedTime, 0f, 1f);
        return EasingMode switch
        {
            EasingMode.EaseIn => EaseInCore(t),
            EasingMode.EaseOut => 1f - EaseInCore(1f - t),
            EasingMode.EaseInOut => t < 0.5f
                ? EaseInCore(t * 2f) * 0.5f
                : (1f - EaseInCore((1f - t) * 2f) * 0.5f),
            _ => t
        };
    }

    protected abstract float EaseInCore(float t);
}

public sealed class SineEase : EasingFunctionBase
{
    protected override float EaseInCore(float t) => 1f - MathF.Cos(t * MathF.PI * 0.5f);
}

public sealed class QuadraticEase : EasingFunctionBase
{
    protected override float EaseInCore(float t) => t * t;
}

public sealed class CubicEase : EasingFunctionBase
{
    protected override float EaseInCore(float t) => t * t * t;
}

public sealed class QuarticEase : EasingFunctionBase
{
    protected override float EaseInCore(float t) => t * t * t * t;
}

public sealed class QuinticEase : EasingFunctionBase
{
    protected override float EaseInCore(float t) => t * t * t * t * t;
}

public sealed class CircleEase : EasingFunctionBase
{
    protected override float EaseInCore(float t) => 1f - MathF.Sqrt(MathF.Max(0f, 1f - (t * t)));
}

public sealed class ExponentialEase : EasingFunctionBase
{
    public float Exponent { get; set; } = 2f;

    protected override float EaseInCore(float t)
    {
        if (t <= 0f)
        {
            return 0f;
        }

        return (MathF.Exp(Exponent * t) - 1f) / (MathF.Exp(Exponent) - 1f);
    }
}

public sealed class BackEase : EasingFunctionBase
{
    public float Amplitude { get; set; } = 1f;

    protected override float EaseInCore(float t)
    {
        var s = Amplitude + 1f;
        return (t * t * ((s * t) - Amplitude));
    }
}

public sealed class BounceEase : EasingFunctionBase
{
    protected override float EaseInCore(float t)
    {
        // Mirrors the out-bounce curve to produce an in-bounce baseline.
        var x = 1f - t;
        var outBounce = x switch
        {
            < (1f / 2.75f) => 7.5625f * x * x,
            < (2f / 2.75f) => 7.5625f * (x - (1.5f / 2.75f)) * (x - (1.5f / 2.75f)) + 0.75f,
            < (2.5f / 2.75f) => 7.5625f * (x - (2.25f / 2.75f)) * (x - (2.25f / 2.75f)) + 0.9375f,
            _ => 7.5625f * (x - (2.625f / 2.75f)) * (x - (2.625f / 2.75f)) + 0.984375f
        };
        return 1f - outBounce;
    }
}
