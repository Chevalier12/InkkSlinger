using System;
using InkkSlinger;
using Microsoft.Xna.Framework;

namespace InkkSlinger.Designer;

public partial class DesignerSourceColorPropertyEditor : UserControl
{
    public static readonly DependencyProperty PropertyNameProperty =
        DependencyProperty.Register(
            nameof(PropertyName),
            typeof(string),
            typeof(DesignerSourceColorPropertyEditor),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty DisplayTextProperty =
        DependencyProperty.Register(
            nameof(DisplayText),
            typeof(string),
            typeof(DesignerSourceColorPropertyEditor),
            new FrameworkPropertyMetadata(
                string.Empty,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is DesignerSourceColorPropertyEditor editor)
                    {
                        editor.SynchronizeFromProperties();
                    }
                }));

    public static readonly DependencyProperty SelectedColorValueProperty =
        DependencyProperty.Register(
            nameof(SelectedColorValue),
            typeof(Color),
            typeof(DesignerSourceColorPropertyEditor),
            new FrameworkPropertyMetadata(
                Color.Transparent,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is DesignerSourceColorPropertyEditor editor)
                    {
                        if (editor._isWritingSelectedColorValue)
                        {
                            return;
                        }

                        editor.SynchronizeFromProperties();
                    }
                }));

    private bool _handlingInteractivePopupToggle;
    private bool _isSynchronizing;
    private bool _isWritingSelectedColorValue;
    private static DesignerSourceColorPropertyEditor? _openEditor;
    private Color _currentColor;
    private float _currentHue;
    private float _currentSaturation;
    private float _currentValue;
    private float _currentAlpha = 1f;
    private string _displayText = string.Empty;

    public DesignerSourceColorPropertyEditor()
    {
        InitializeComponent();
        if (Content is Panel rootPanel)
        {
            _ = rootPanel.RemoveChild(InteractivePopup);
        }

        EditorComboBox.DependencyPropertyChanged += OnEditorComboBoxDependencyPropertyChanged;
        EditorComboBox.SelectedIndex = 0;
        SynchronizeFromProperties();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _ = base.MeasureOverride(availableSize);
        return EditorComboBox.DesiredSize;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        return base.ArrangeOverride(finalSize);
    }

    public string PropertyName
    {
        get => GetValue<string>(PropertyNameProperty) ?? string.Empty;
        set => SetValue(PropertyNameProperty, value ?? string.Empty);
    }

    public string DisplayText
    {
        get => GetValue<string>(DisplayTextProperty) ?? string.Empty;
        set => SetValue(DisplayTextProperty, value ?? string.Empty);
    }

    public Color SelectedColorValue
    {
        get => GetValue<Color>(SelectedColorValueProperty);
        set => SetValue(SelectedColorValueProperty, value);
    }

    public event EventHandler? InteractivePopupOpening;

    public event EventHandler? ColorValueCommitted;

    public Color CurrentSelectedColor => _currentColor;

    public void ClosePopup()
    {
        if (InteractivePopup.IsOpen)
        {
            InteractivePopup.Close();
        }
    }

    private void OnEditorComboBoxDependencyPropertyChanged(object? sender, DependencyPropertyChangedEventArgs args)
    {
        _ = sender;
        if (args.Property != ComboBox.IsDropDownOpenProperty ||
            _handlingInteractivePopupToggle ||
            args.NewValue is not bool isOpen ||
            !isOpen)
        {
            return;
        }

        _handlingInteractivePopupToggle = true;
        try
        {
            EditorComboBox.IsDropDownOpen = false;
            if (InteractivePopup.IsOpen)
            {
                InteractivePopup.Close();
            }
            else
            {
                OpenInteractivePopup();
            }
        }
        finally
        {
            _handlingInteractivePopupToggle = false;
        }
    }

    private void SynchronizeFromProperties()
    {
        var color = SelectedColorValue;
        var displayText = string.IsNullOrWhiteSpace(DisplayText)
            ? DesignerSourcePropertyInspector.FormatColorValue(color)
            : DisplayText;

        if (AreColorsEqual(color, _currentColor) && string.Equals(displayText, _displayText, StringComparison.Ordinal))
        {
            return;
        }

        ToHsva(color, out var hue, out var saturation, out var value, out var alpha);
        ApplyHsva(hue, saturation, value, alpha, displayText, commitToSource: false);
    }

    private void OnColorPickerSelectedColorChanged(object? sender, ColorChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_isSynchronizing)
        {
            return;
        }

        ApplyHsva(
            ColorPickerControl.Hue,
            ColorPickerControl.Saturation,
            ColorPickerControl.Value,
            ColorPickerControl.Alpha,
            DesignerSourcePropertyInspector.FormatColorValue(ColorPickerControl.SelectedColor),
            commitToSource: true);
    }

    private void OnHueSpectrumSelectedColorChanged(object? sender, ColorChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_isSynchronizing)
        {
            return;
        }

        ApplyHsva(
            HueSpectrumControl.Hue,
            _currentSaturation,
            _currentValue,
            _currentAlpha,
            displayText: null,
            commitToSource: true);
    }

    private void OnAlphaSpectrumSelectedColorChanged(object? sender, ColorChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_isSynchronizing)
        {
            return;
        }

        ApplyHsva(
            _currentHue,
            _currentSaturation,
            _currentValue,
            AlphaSpectrumControl.Alpha,
            displayText: null,
            commitToSource: true);
    }

    private void ApplyHsva(
        float hue,
        float saturation,
        float value,
        float alpha,
        string? displayText,
        bool commitToSource)
    {
        _currentHue = CoerceHueValue(hue);
        _currentSaturation = Clamp01(saturation);
        _currentValue = Clamp01(value);
        _currentAlpha = Clamp01(alpha);
        _currentColor = FromHsva(_currentHue, _currentSaturation, _currentValue, _currentAlpha);
        _displayText = string.IsNullOrWhiteSpace(displayText)
            ? DesignerSourcePropertyInspector.FormatColorValue(_currentColor)
            : displayText;

        _isSynchronizing = true;
        try
        {
            EditorComboBox.SelectedIndex = 0;
            ColorPickerControl.SelectedColor = _currentColor;
            ColorPickerControl.Hue = _currentHue;
            ColorPickerControl.Saturation = _currentSaturation;
            ColorPickerControl.Value = _currentValue;
            ColorPickerControl.Alpha = _currentAlpha;

            HueSpectrumControl.SelectedColor = _currentColor;
            HueSpectrumControl.Hue = _currentHue;
            HueSpectrumControl.Alpha = _currentAlpha;

            AlphaSpectrumControl.SelectedColor = _currentColor;
            AlphaSpectrumControl.Hue = _currentHue;
            AlphaSpectrumControl.Alpha = _currentAlpha;

            DisplayItem.Text = _displayText;
            ColorPickerItem.Text = _displayText;
        }
        finally
        {
            _isSynchronizing = false;
        }

        if (commitToSource)
        {
            _isWritingSelectedColorValue = true;
            try
            {
                SelectedColorValue = _currentColor;
            }
            finally
            {
                _isWritingSelectedColorValue = false;
            }

            ColorValueCommitted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OpenInteractivePopup()
    {
        var host = FindOverlayHost();
        if (host == null)
        {
            return;
        }

        if (_openEditor != null && !ReferenceEquals(_openEditor, this))
        {
            _openEditor.ClosePopup();
        }

        InteractivePopupOpening?.Invoke(this, EventArgs.Empty);
        InteractivePopup.PlacementTarget = EditorComboBox;
        InteractivePopup.PlacementMode = PopupPlacementMode.Bottom;
        InteractivePopup.HorizontalOffset = 0f;
        InteractivePopup.VerticalOffset = 2f;
        InteractivePopup.Width = Math.Max(EditorComboBox.ActualWidth > 0f ? EditorComboBox.ActualWidth : EditorComboBox.Width, 80f);
        _openEditor = this;
        InteractivePopup.Show(host);
    }

    private Panel? FindOverlayHost()
    {
        Panel? fallbackHost = null;
        for (var current = (UIElement?)this; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is Panel panel)
            {
                fallbackHost = panel;
            }
        }

        return fallbackHost;
    }

    private void OnInteractivePopupClosed(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        if (ReferenceEquals(_openEditor, this))
        {
            _openEditor = null;
        }
    }

    private static bool AreColorsEqual(Color left, Color right)
    {
        return left.PackedValue == right.PackedValue;
    }

    private static float CoerceHueValue(float hue)
    {
        if (float.IsNaN(hue) || float.IsInfinity(hue))
        {
            return 0f;
        }

        return Math.Clamp(hue, 0f, 360f);
    }

    private static float Clamp01(float value)
    {
        return Math.Clamp(value, 0f, 1f);
    }

    private static Color FromHsva(float hue, float saturation, float value, float alpha)
    {
        saturation = Clamp01(saturation);
        value = Clamp01(value);
        alpha = Clamp01(alpha);

        var normalizedHue = NormalizeHue(hue);
        var chroma = value * saturation;
        var hueSector = normalizedHue / 60f;
        var x = chroma * (1f - MathF.Abs((hueSector % 2f) - 1f));

        float redPrime;
        float greenPrime;
        float bluePrime;
        if (hueSector < 1f)
        {
            redPrime = chroma;
            greenPrime = x;
            bluePrime = 0f;
        }
        else if (hueSector < 2f)
        {
            redPrime = x;
            greenPrime = chroma;
            bluePrime = 0f;
        }
        else if (hueSector < 3f)
        {
            redPrime = 0f;
            greenPrime = chroma;
            bluePrime = x;
        }
        else if (hueSector < 4f)
        {
            redPrime = 0f;
            greenPrime = x;
            bluePrime = chroma;
        }
        else if (hueSector < 5f)
        {
            redPrime = x;
            greenPrime = 0f;
            bluePrime = chroma;
        }
        else
        {
            redPrime = chroma;
            greenPrime = 0f;
            bluePrime = x;
        }

        var match = value - chroma;
        return new Color(
            ToByte(redPrime + match),
            ToByte(greenPrime + match),
            ToByte(bluePrime + match),
            ToByte(alpha));
    }

    private static void ToHsva(Color color, out float hue, out float saturation, out float value, out float alpha)
    {
        var red = color.R / 255f;
        var green = color.G / 255f;
        var blue = color.B / 255f;
        var max = MathF.Max(red, MathF.Max(green, blue));
        var min = MathF.Min(red, MathF.Min(green, blue));
        var delta = max - min;

        value = max;
        saturation = max <= 0f ? 0f : delta / max;
        alpha = color.A / 255f;

        if (delta <= 0f)
        {
            hue = 0f;
            return;
        }

        if (MathF.Abs(max - red) <= 0.0001f)
        {
            hue = 60f * (((green - blue) / delta) % 6f);
        }
        else if (MathF.Abs(max - green) <= 0.0001f)
        {
            hue = 60f * (((blue - red) / delta) + 2f);
        }
        else
        {
            hue = 60f * (((red - green) / delta) + 4f);
        }

        hue = NormalizeHue(hue);
    }

    private static float NormalizeHue(float hue)
    {
        var normalized = hue % 360f;
        if (normalized < 0f)
        {
            normalized += 360f;
        }

        return normalized;
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(Clamp01(value) * 255f), 0, 255);
    }
}
