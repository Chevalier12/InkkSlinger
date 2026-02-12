using System;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class KeySpline : IEasingFunction
{
    public KeySpline()
        : this(new Vector2(0f, 0f), new Vector2(1f, 1f))
    {
    }

    public KeySpline(Vector2 controlPoint1, Vector2 controlPoint2)
    {
        ControlPoint1 = controlPoint1;
        ControlPoint2 = controlPoint2;
    }

    public Vector2 ControlPoint1 { get; set; }

    public Vector2 ControlPoint2 { get; set; }

    public float Ease(float normalizedTime)
    {
        var x = Math.Clamp(normalizedTime, 0f, 1f);
        var t = SolveCurveX(x);
        return Math.Clamp(SampleCurveY(t), 0f, 1f);
    }

    public static KeySpline Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormatException("KeySpline cannot be empty.");
        }

        var parts = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new FormatException("KeySpline must be 'x1,y1 x2,y2'.");
        }

        var cp1 = ParsePoint(parts[0]);
        var cp2 = ParsePoint(parts[1]);
        return new KeySpline(cp1, cp2);
    }

    private static Vector2 ParsePoint(string token)
    {
        var pair = token.Split(',');
        if (pair.Length != 2)
        {
            throw new FormatException("KeySpline control point must be 'x,y'.");
        }

        var x = float.Parse(pair[0], CultureInfo.InvariantCulture);
        var y = float.Parse(pair[1], CultureInfo.InvariantCulture);
        return new Vector2(x, y);
    }

    private float SolveCurveX(float x)
    {
        var x1 = Math.Clamp(ControlPoint1.X, 0f, 1f);
        var x2 = Math.Clamp(ControlPoint2.X, 0f, 1f);

        // Newton-Raphson for fast convergence.
        var t = x;
        for (var i = 0; i < 8; i++)
        {
            var currentX = SampleCurve(t, x1, x2) - x;
            var derivative = SampleCurveDerivative(t, x1, x2);
            if (MathF.Abs(currentX) < 1e-6f || MathF.Abs(derivative) < 1e-6f)
            {
                break;
            }

            t -= currentX / derivative;
            t = Math.Clamp(t, 0f, 1f);
        }

        // Bisection fallback for stability.
        var lower = 0f;
        var upper = 1f;
        for (var i = 0; i < 12; i++)
        {
            var currentX = SampleCurve(t, x1, x2);
            if (MathF.Abs(currentX - x) < 1e-6f)
            {
                break;
            }

            if (currentX < x)
            {
                lower = t;
            }
            else
            {
                upper = t;
            }

            t = (lower + upper) * 0.5f;
        }

        return t;
    }

    private float SampleCurveY(float t)
    {
        var y1 = Math.Clamp(ControlPoint1.Y, 0f, 1f);
        var y2 = Math.Clamp(ControlPoint2.Y, 0f, 1f);
        return SampleCurve(t, y1, y2);
    }

    private static float SampleCurve(float t, float p1, float p2)
    {
        var u = 1f - t;
        return (3f * u * u * t * p1) + (3f * u * t * t * p2) + (t * t * t);
    }

    private static float SampleCurveDerivative(float t, float p1, float p2)
    {
        var u = 1f - t;
        return (3f * u * u * p1) + (6f * u * t * (p2 - p1)) + (3f * t * t * (1f - p2));
    }
}
