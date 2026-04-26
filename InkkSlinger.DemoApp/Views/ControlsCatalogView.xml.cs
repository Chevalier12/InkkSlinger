using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class ControlsCatalogView : UserControl
{
    private StackPanel? _controlButtonsHost;
    private Label? _selectedControlLabel;
    private ContentControl? _previewHost;
    private string _selectedControlName = string.Empty;

    public ControlsCatalogView()
    {
        ApplyRuntimeTestWindowOverrides();
        InitializeComponent();

        _controlButtonsHost = this.FindName("ControlButtonsHost") as StackPanel;
        _selectedControlLabel = this.FindName("SelectedControlLabel") as Label;
        _previewHost = this.FindName("PreviewHost") as ContentControl;

        BuildButtons();
        if (ControlViews.All.Length > 0)
        {
            ShowControl(ControlViews.All[0]);
        }
    }

    private static void ApplyRuntimeTestWindowOverrides()
    {
        var configuredWidth = Environment.GetEnvironmentVariable("INKKSLINGER_TEST_MAINWINDOW_WIDTH");
        if (!string.IsNullOrWhiteSpace(configuredWidth) &&
            int.TryParse(configuredWidth, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) &&
            width > 0 &&
            UiApplication.Current.HasMainWindow)
        {
            Application.Current.MainWindow.Width = width;
        }
    }

    private void BuildButtons()
    {
        if (_controlButtonsHost == null)
        {
            return;
        }

        foreach (var name in ControlViews.All)
        {
            var capture = name;
            var button = new Button
            {
                Name = CreateCatalogButtonName(name),
                Content = GetDisplayName(name),
                Margin = new Thickness(0, 0, 0, 4)
            };
            button.Click += (_, _) => ShowControl(capture);
            _controlButtonsHost.AddChild(button);
        }
    }

    internal void ShowControl(string controlName)
    {
        _selectedControlName = controlName;
        if (_selectedControlLabel != null)
        {
            _selectedControlLabel.Content = $"Selected: {GetDisplayName(controlName)}";
        }

        if (_previewHost != null)
        {
            var view = CreateView(controlName);
            HarmonizePreviewChrome(view);
            _previewHost.Content = view;
        }
    }

    internal string SelectedControlName => _selectedControlName;

    private static UserControl CreateView(string controlName)
    {
        return ControlViews.CreateCatalogView(controlName);
    }

    private static string GetDisplayName(string controlName)
    {
        return string.Equals(controlName, "CatchMe", StringComparison.Ordinal)
            ? "Catch Me!"
            : controlName;
    }

    private static string CreateCatalogButtonName(string controlName)
    {
        var builder = new System.Text.StringBuilder(controlName.Length + "CatalogButton".Length);
        foreach (var character in controlName)
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                builder.Append(character);
            }
        }

        if (builder.Length == 0 || !char.IsLetter(builder[0]) && builder[0] != '_')
        {
            builder.Insert(0, '_');
        }

        builder.Append("CatalogButton");
        return builder.ToString();
    }

    private static void HarmonizePreviewChrome(UserControl view)
    {
        var darkBg = ResolveThemeColor("DarkBgBrush", new Color(0x1E, 0x1E, 0x1E));
        var darkSurface = ResolveThemeColor("DarkSurfaceBrush", new Color(0x2A, 0x2A, 0x2A));
        var darkBorder = ResolveThemeColor("DarkBorderBrush", new Color(0x3F, 0x3F, 0x3F));
        var textPrimary = ResolveThemeColor("TextPrimaryBrush", new Color(0xF0, 0xF0, 0xF0));

        var remap = new Dictionary<Color, Color>
        {
            [new Color(0x0F, 0x16, 0x22)] = darkBg,
            [new Color(0x11, 0x1D, 0x2D)] = darkBg,
            [new Color(0x16, 0x23, 0x34)] = darkSurface,
            [new Color(0x2E, 0x4A, 0x66)] = darkBorder,
            [new Color(0x35, 0x54, 0x74)] = darkBorder,
            [new Color(0xE8, 0xF5, 0xFF)] = textPrimary
        };

        ApplyChromeRemapRecursive(view, remap);
    }

    private static void ApplyChromeRemapRecursive(UIElement element, IReadOnlyDictionary<Color, Color> remap)
    {
        if (element is Control control)
        {
            RemapIfLocal(control, Control.BackgroundProperty, remap);
            RemapIfLocal(control, Control.BorderBrushProperty, remap);
            RemapIfLocal(control, Control.ForegroundProperty, remap);
        }

        if (element is Border border)
        {
            RemapIfLocal(border, Border.BackgroundProperty, remap);
            RemapIfLocal(border, Border.BorderBrushProperty, remap);
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyChromeRemapRecursive(child, remap);
        }
    }

    private static void RemapIfLocal(DependencyObject target, DependencyProperty property, IReadOnlyDictionary<Color, Color> remap)
    {
        if (target.GetValueSource(property) != DependencyPropertyValueSource.Local)
        {
            return;
        }

        var currentValue = target.GetValue(property);
        var current = currentValue switch
        {
            Color color => color,
            Brush brush => brush.ToColor(),
            _ => (Color?)null
        };

        if (current == null)
        {
            return;
        }

        if (remap.TryGetValue(current.Value, out var replacement) && replacement != current.Value)
        {
            target.SetValue(property, replacement);
        }
    }

    private static Color ResolveThemeColor(string key, Color fallback)
    {
        if (UiApplication.Current.Resources.TryGetValue(key, out var resource))
        {
            if (resource is Color color)
            {
                return color;
            }

            if (resource is Brush brush)
            {
                return brush.ToColor();
            }
        }

        return fallback;
    }
}

public sealed class MissingControlView : UserControl
{
    public MissingControlView(string controlName)
    {
        Content = new Label
        {
            Content = $"Missing generated view for {controlName}",
            Foreground = new Color(232, 245, 255)
        };
    }
}


