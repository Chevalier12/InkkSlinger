using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal static class ColorControlUtilities
{
    internal static float Clamp01(float value)
    {
        return Math.Clamp(value, 0f, 1f);
    }

    internal static float NormalizeHue(float hue)
    {
        if (!float.IsFinite(hue))
        {
            return 0f;
        }

        var normalized = hue % 360f;
        if (normalized < 0f)
        {
            normalized += 360f;
        }

        return normalized >= 360f ? 0f : normalized;
    }

    internal static void ToHsva(Color color, out float hue, out float saturation, out float value, out float alpha)
    {
        var r = color.R / 255f;
        var g = color.G / 255f;
        var b = color.B / 255f;
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        var delta = max - min;

        value = max;
        saturation = max <= 0.000001f ? 0f : delta / max;
        alpha = color.A / 255f;

        if (delta <= 0.000001f)
        {
            hue = 0f;
            return;
        }

        if (MathF.Abs(max - r) <= 0.000001f)
        {
            hue = 60f * (((g - b) / delta) % 6f);
        }
        else if (MathF.Abs(max - g) <= 0.000001f)
        {
            hue = 60f * (((b - r) / delta) + 2f);
        }
        else
        {
            hue = 60f * (((r - g) / delta) + 4f);
        }

        hue = NormalizeHue(hue);
    }

    internal static Color FromHsva(float hue, float saturation, float value, float alpha)
    {
        hue = NormalizeHue(hue);
        saturation = Clamp01(saturation);
        value = Clamp01(value);
        alpha = Clamp01(alpha);

        var chroma = value * saturation;
        var hueSection = hue / 60f;
        var x = chroma * (1f - MathF.Abs((hueSection % 2f) - 1f));
        var match = value - chroma;

        float r;
        float g;
        float b;
        if (hueSection < 1f)
        {
            r = chroma;
            g = x;
            b = 0f;
        }
        else if (hueSection < 2f)
        {
            r = x;
            g = chroma;
            b = 0f;
        }
        else if (hueSection < 3f)
        {
            r = 0f;
            g = chroma;
            b = x;
        }
        else if (hueSection < 4f)
        {
            r = 0f;
            g = x;
            b = chroma;
        }
        else if (hueSection < 5f)
        {
            r = x;
            g = 0f;
            b = chroma;
        }
        else
        {
            r = chroma;
            g = 0f;
            b = x;
        }

        return new Color(
            ToByte(r + match),
            ToByte(g + match),
            ToByte(b + match),
            ToByte(alpha));
    }

    internal static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(Clamp01(value) * 255f), 0, 255);
    }
}