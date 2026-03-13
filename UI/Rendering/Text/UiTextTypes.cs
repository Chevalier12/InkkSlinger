using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

[Flags]
internal enum UiTextStyleOverride
{
    None = 0,
    Bold = 1,
    Italic = 2
}

internal enum UiTextAntialiasMode
{
    Grayscale,
    Lcd
}

public readonly record struct UiTypography(
    string Family,
    float Size,
    string Weight,
    string Style)
{
    public static UiTypography FromElement(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return new UiTypography(
            NormalizeFamily(element.FontFamily),
            MathF.Max(1f, element.FontSize),
            NormalizeWeight(element.FontWeight),
            NormalizeStyle(element.FontStyle));
    }

    internal UiTypography Apply(UiTextStyleOverride styleOverride)
    {
        var weight = Weight;
        var style = Style;
        if ((styleOverride & UiTextStyleOverride.Bold) != 0)
        {
            weight = "Bold";
        }

        if ((styleOverride & UiTextStyleOverride.Italic) != 0)
        {
            style = "Italic";
        }

        return new UiTypography(Family, Size, weight, style);
    }

    public static string NormalizeFamily(string? family)
    {
        return string.IsNullOrWhiteSpace(family)
            ? "Segoe UI"
            : family.Trim();
    }

    public static string NormalizeWeight(string? weight)
    {
        if (string.IsNullOrWhiteSpace(weight))
        {
            return "Normal";
        }

        var normalized = weight.Trim();
        return normalized.Equals("Regular", StringComparison.OrdinalIgnoreCase)
            ? "Normal"
            : normalized;
    }

    public static string NormalizeStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return "Normal";
        }

        var normalized = style.Trim();
        return normalized.Equals("Oblique", StringComparison.OrdinalIgnoreCase)
            ? "Italic"
            : normalized;
    }
}

internal readonly record struct UiTextMetrics(
    float Width,
    float Height,
    float LineHeight);

internal readonly record struct UiGlyphRasterized(
    Color[] PixelData,
    int Width,
    int Height,
    float BearingX,
    float BearingY,
    float AdvanceX,
    UiTextAntialiasMode AntialiasMode);

internal readonly record struct UiGlyphKey(
    string FontPath,
    int PixelSize,
    int CodePoint,
    UiTextAntialiasMode AntialiasMode);

internal readonly record struct UiResolvedTypeface(
    string Family,
    string Weight,
    string Style,
    string FontPath,
    int WeightValue);

internal readonly record struct UiGlyphEntry(
    Texture2D Texture,
    Rectangle SourceRect,
    float BearingX,
    float BearingY,
    float AdvanceX,
    UiTextAntialiasMode AntialiasMode);
