using System;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class ColorPickerView : UserControl
{
    private bool _suppressSynchronization;

    public ColorPickerView()
    {
        InitializeComponent();

        ApplyColor(new Color(255, 48, 48, 255), "Initial state: Scarlet preset. Drag any linked surface to begin exploring hue, saturation, value, and alpha.");
    }

    private void OnMainColorPickerSelectedColorChanged(object? sender, ColorChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        OnColorSurfaceChanged(MainColorPicker, "ColorPicker surface updated the shared HSVA state.");
    }

    private void OnVerticalColorSpectrumSelectedColorChanged(object? sender, ColorChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        OnColorSurfaceChanged(VerticalColorSpectrum, "Vertical ColorSpectrum updated hue while preserving the shared saturation, value, and alpha.");
    }

    private void OnAlphaColorSpectrumSelectedColorChanged(object? sender, ColorChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        OnColorSurfaceChanged(AlphaColorSpectrum, "Dedicated alpha ColorSpectrum updated opacity while preserving the shared hue, saturation, and value.");
    }

    private void OnHorizontalColorSpectrumSelectedColorChanged(object? sender, ColorChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        OnColorSurfaceChanged(HorizontalColorSpectrum, "Horizontal ColorSpectrum updated hue through the orientation-aware spectrum surface.");
    }

    private void OnColorSurfaceChanged(dynamic source, string narrative)
    {
        if (_suppressSynchronization)
        {
            return;
        }

        ApplyHsva(source.Hue, source.Saturation, source.Value, source.Alpha, narrative);
    }

    private void OnChannelSliderChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        if (_suppressSynchronization)
        {
            return;
        }

        ApplyHsva(
            HueSlider.Value,
            SaturationSlider.Value / 100f,
            ValueSlider.Value / 100f,
            AlphaSlider.Value / 100f,
            "Channel sliders updated the shared HSVA state and pushed it back into every color surface.");
    }

    private void ApplyColor(Color color, string narrative)
    {
        ToHsva(color, out var hue, out var saturation, out var value, out var alpha);
        ApplyHsva(hue, saturation, value, alpha, narrative);
    }

    private void OnPresetScarletClicked(object? sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyColor(new Color(255, 48, 48, 255), "Preset: Scarlet. Pure warm hue with full saturation, full value, and full alpha.");
    }

    private void OnPresetLagoonClicked(object? sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyColor(new Color(18, 198, 220, 166), "Preset: Lagoon 65%. Mid-cool hue with strong saturation and a visible alpha blend.");
    }

    private void OnPresetLimeClicked(object? sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyColor(new Color(159, 255, 64, 255), "Preset: Lime Glow. High-value, high-saturation green for edge-to-edge square coverage.");
    }

    private void OnPresetLavenderClicked(object? sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyColor(new Color(188, 150, 255, 140), "Preset: Lavender Mist. Lower saturation with lifted value and translucent alpha.");
    }

    private void OnPresetSmokeClicked(object? sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyColor(new Color(98, 112, 128, 51), "Preset: Smoke 20%. Low-saturation color chosen mainly to emphasize the alpha preview.");
    }

    private void OnPresetSunsetClicked(object? sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyColor(new Color(255, 126, 64, 230), "Preset: Sunset. Warm orange with a slight alpha drop for preview contrast.");
    }

    private void ApplyHsva(float hue, float saturation, float value, float alpha, string narrative)
    {
        _suppressSynchronization = true;
        try
        {
            var normalizedHue = NormalizeHue(hue);
            var displayHue = GetDisplayHue(hue);
            var color = FromHsva(normalizedHue, saturation, value, alpha);

            MainColorPicker.SelectedColor = color;
            MainColorPicker.Hue = normalizedHue;
            MainColorPicker.Saturation = saturation;
            MainColorPicker.Value = value;
            MainColorPicker.Alpha = alpha;

            VerticalColorSpectrum.SelectedColor = color;
            VerticalColorSpectrum.Hue = displayHue;
            VerticalColorSpectrum.Saturation = saturation;
            VerticalColorSpectrum.Value = value;
            VerticalColorSpectrum.Alpha = alpha;

            AlphaColorSpectrum.SelectedColor = color;
            AlphaColorSpectrum.Hue = normalizedHue;
            AlphaColorSpectrum.Saturation = saturation;
            AlphaColorSpectrum.Value = value;
            AlphaColorSpectrum.Alpha = alpha;

            HorizontalColorSpectrum.SelectedColor = color;
            HorizontalColorSpectrum.Hue = displayHue;
            HorizontalColorSpectrum.Saturation = saturation;
            HorizontalColorSpectrum.Value = value;
            HorizontalColorSpectrum.Alpha = alpha;

            HueSlider.Value = displayHue;
            SaturationSlider.Value = saturation * 100f;
            ValueSlider.Value = value * 100f;
            AlphaSlider.Value = alpha * 100f;

            UpdateReadouts(color, displayHue, saturation, value, alpha, narrative);
        }
        finally
        {
            _suppressSynchronization = false;
        }
    }

    private void UpdateReadouts(Color color, float hue, float saturation, float value, float alpha, string narrative)
    {
        AlphaPreviewOverlay.Background = color;
        OpaquePreviewSwatch.Background = new Color(color.R, color.G, color.B, (byte)255);

        InteractionSummaryText.Text = narrative;
        HexValueText.Text = $"Hex ARGB: #{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        ArgbValueText.Text = $"ARGB channels: A {color.A}, R {color.R}, G {color.G}, B {color.B}";
        HsvaValueText.Text = string.Format(
            CultureInfo.InvariantCulture,
            "HSVA: {0:0.#}°, {1:0.#}%, {2:0.#}%, {3:0.#}%",
            hue,
            saturation * 100f,
            value * 100f,
            alpha * 100f);
        CapabilitySummaryText.Text = "ColorPicker supplies the two-dimensional saturation/value surface. ColorSpectrum now serves both as the hue spectrum and as the dedicated alpha spectrum, with all surfaces kept in sync from code-behind only.";

        HueSliderLabel.Content = string.Format(CultureInfo.InvariantCulture, "Hue: {0:0.#}°", hue);
        SaturationSliderLabel.Content = string.Format(CultureInfo.InvariantCulture, "Saturation: {0:0.#}%", saturation * 100f);
        ValueSliderLabel.Content = string.Format(CultureInfo.InvariantCulture, "Value: {0:0.#}%", value * 100f);
        AlphaSliderLabel.Content = string.Format(CultureInfo.InvariantCulture, "Alpha: {0:0.#}%", alpha * 100f);
    }

    private static void ToHsva(Color color, out float hue, out float saturation, out float value, out float alpha)
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

    private static Color FromHsva(float hue, float saturation, float value, float alpha)
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
            (byte)ToByte(alpha));
    }

    private static float NormalizeHue(float hue)
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

    private static float GetDisplayHue(float hue)
    {
        if (!float.IsFinite(hue))
        {
            return 0f;
        }

        var normalized = NormalizeHue(hue);
        if (normalized <= 0f && hue > 0f)
        {
            return 360f;
        }

        return normalized;
    }

    private static float Clamp01(float value)
    {
        return Math.Clamp(value, 0f, 1f);
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(Clamp01(value) * 255f), 0, 255);
    }
}