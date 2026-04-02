using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class BorderView : UserControl
{
    private Border? _clipProbeBorder;
    private CheckBox? _clipToBoundsCheckBox;
    private TextBlock? _clipStateText;
    private Border? _presetBorder;
    private Border? _presetBadgeBorder;
    private TextBlock? _presetHeaderText;
    private TextBlock? _presetBodyText;
    private TextBlock? _presetBadgeText;
    private TextBlock? _presetStateText;
    private int _chromePresetIndex;
    private int _shapePresetIndex;
    private int _spacingPresetIndex;

    private static readonly ChromePreset[] ChromePresets =
    {
        new("Slate panel", new Color(24, 33, 42), new Color(54, 85, 116), new Color(240, 240, 240), new Color(255, 140, 0), new Color(204, 112, 0), new Color(30, 30, 30)),
        new("Success capsule", new Color(25, 50, 41), new Color(47, 138, 99), new Color(240, 240, 240), new Color(97, 211, 143), new Color(47, 138, 99), new Color(21, 27, 24)),
        new("Alert shell", new Color(58, 28, 22), new Color(181, 78, 64), new Color(248, 237, 233), new Color(231, 76, 60), new Color(181, 78, 64), new Color(255, 244, 240))
    };

    private static readonly ShapePreset[] ShapePresets =
    {
        new("Balanced frame", new Thickness(1), new CornerRadius(12f), "Radius 12"),
        new("Pill cap", new Thickness(2), new CornerRadius(22f, 22f, 8f, 8f), "Cap edge"),
        new("Accent rail", new Thickness(5f, 1f, 1f, 1f), new CornerRadius(16f, 8f, 16f, 8f), "Left rail")
    };

    private static readonly SpacingPreset[] SpacingPresets =
    {
        new("Compact", new Thickness(8f), "Compact spacing keeps the content dense and lets the chrome read as a tighter utility container."),
        new("Comfortable", new Thickness(16f, 12f, 16f, 12f), "Balanced spacing is the default card treatment: enough breathing room without making the frame feel oversized."),
        new("Showcase", new Thickness(24f, 18f, 24f, 18f), "Large padding turns the same Border into a display surface where the frame becomes part of the composition.")
    };

    public BorderView()
    {
        InitializeComponent();

        ResolveNamedParts();
        WireEvents();
        ApplyPresetSurface();
        ApplyClipMode();
    }

    private void ResolveNamedParts()
    {
        _clipProbeBorder = this.FindName("ClipProbeBorder") as Border;
        _clipToBoundsCheckBox = this.FindName("ClipToBoundsCheckBox") as CheckBox;
        _clipStateText = this.FindName("ClipStateText") as TextBlock;
        _presetBorder = this.FindName("PresetBorder") as Border;
        _presetBadgeBorder = this.FindName("PresetBadgeBorder") as Border;
        _presetHeaderText = this.FindName("PresetHeaderText") as TextBlock;
        _presetBodyText = this.FindName("PresetBodyText") as TextBlock;
        _presetBadgeText = this.FindName("PresetBadgeText") as TextBlock;
        _presetStateText = this.FindName("PresetStateText") as TextBlock;
    }

    private void WireEvents()
    {
        if (this.FindName("NextChromeButton") is Button nextChromeButton)
        {
            nextChromeButton.Click += HandleNextChromeClick;
        }

        if (this.FindName("NextShapeButton") is Button nextShapeButton)
        {
            nextShapeButton.Click += HandleNextShapeClick;
        }

        if (this.FindName("NextSpacingButton") is Button nextSpacingButton)
        {
            nextSpacingButton.Click += HandleNextSpacingClick;
        }

        if (_clipToBoundsCheckBox != null)
        {
            _clipToBoundsCheckBox.Checked += HandleClipToggleChanged;
            _clipToBoundsCheckBox.Unchecked += HandleClipToggleChanged;
        }
    }

    private void HandleNextChromeClick(object? sender, RoutedSimpleEventArgs args)
    {
        _chromePresetIndex = (_chromePresetIndex + 1) % ChromePresets.Length;
        ApplyPresetSurface();
    }

    private void HandleNextShapeClick(object? sender, RoutedSimpleEventArgs args)
    {
        _shapePresetIndex = (_shapePresetIndex + 1) % ShapePresets.Length;
        ApplyPresetSurface();
    }

    private void HandleNextSpacingClick(object? sender, RoutedSimpleEventArgs args)
    {
        _spacingPresetIndex = (_spacingPresetIndex + 1) % SpacingPresets.Length;
        ApplyPresetSurface();
    }

    private void ApplyPresetSurface()
    {
        if (_presetBorder == null ||
            _presetBadgeBorder == null ||
            _presetHeaderText == null ||
            _presetBodyText == null ||
            _presetBadgeText == null ||
            _presetStateText == null)
        {
            return;
        }

        var chrome = ChromePresets[_chromePresetIndex];
        var shape = ShapePresets[_shapePresetIndex];
        var spacing = SpacingPresets[_spacingPresetIndex];

        _presetBorder.Background = chrome.Background;
        _presetBorder.BorderBrush = chrome.BorderBrush;
        _presetBorder.BorderThickness = shape.BorderThickness;
        _presetBorder.CornerRadius = shape.CornerRadius;
        _presetBorder.Padding = spacing.Padding;

        _presetBadgeBorder.Background = chrome.BadgeBackground;
        _presetBadgeBorder.BorderBrush = chrome.BadgeBorderBrush;

        _presetHeaderText.Text = chrome.Name;
        _presetHeaderText.Foreground = chrome.HeaderForeground;

        _presetBodyText.Text = spacing.Description;
        _presetBodyText.Foreground = chrome.BodyForeground;

        _presetBadgeText.Text = shape.BadgeText;
        _presetBadgeText.Foreground = chrome.BadgeForeground;

        _presetStateText.Text = $"Chrome: {chrome.Name}. Shape: {shape.Name} ({FormatThickness(shape.BorderThickness)}, radius {FormatCornerRadius(shape.CornerRadius)}). Spacing: {spacing.Name} ({FormatThickness(spacing.Padding)}).";
    }

    private void HandleClipToggleChanged(object? sender, RoutedSimpleEventArgs args)
    {
        ApplyClipMode();
    }

    private void ApplyClipMode()
    {
        var isEnabled = _clipToBoundsCheckBox?.IsChecked == true;
        if (_clipProbeBorder != null)
        {
            _clipProbeBorder.ClipToBounds = isEnabled;
        }

        if (_clipStateText != null)
        {
            _clipStateText.Text = isEnabled
                ? "ClipToBounds is true. The orange badge is clipped to the Border layout slot, and hit testing follows that same clipped region."
                : "ClipToBounds is false. The orange badge can render and receive hits beyond the Border frame when no ancestor clip blocks it.";
        }
    }

    private static string FormatThickness(Thickness thickness)
    {
        return $"{FormatNumber(thickness.Left)},{FormatNumber(thickness.Top)},{FormatNumber(thickness.Right)},{FormatNumber(thickness.Bottom)}";
    }

    private static string FormatCornerRadius(CornerRadius cornerRadius)
    {
        return $"{FormatNumber(cornerRadius.TopLeft)},{FormatNumber(cornerRadius.TopRight)},{FormatNumber(cornerRadius.BottomRight)},{FormatNumber(cornerRadius.BottomLeft)}";
    }

    private static string FormatNumber(float value)
    {
        return MathF.Abs(value - MathF.Round(value)) < 0.01f
            ? MathF.Round(value).ToString("0")
            : value.ToString("0.##");
    }

    private readonly record struct ChromePreset(
        string Name,
        Color Background,
        Color BorderBrush,
        Color HeaderForeground,
        Color BadgeBackground,
        Color BadgeBorderBrush,
        Color BadgeForeground)
    {
        public Color BodyForeground => new(208, 216, 226);
    }

    private readonly record struct ShapePreset(
        string Name,
        Thickness BorderThickness,
        CornerRadius CornerRadius,
        string BadgeText);

    private readonly record struct SpacingPreset(
        string Name,
        Thickness Padding,
        string Description);
}




