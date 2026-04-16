using System;
using System.Reflection;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal sealed class WindowThemeBinding : IDisposable
{
    private readonly Window _window;
    private readonly FrameworkElement _root;
    private bool _disposed;

    public WindowThemeBinding(Window window, FrameworkElement root)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _window.DependencyPropertyChanged += OnWindowDependencyPropertyChanged;
        ApplyWindowThemeToRoot();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.DependencyPropertyChanged -= OnWindowDependencyPropertyChanged;
    }

    private void OnWindowDependencyPropertyChanged(object? sender, DependencyPropertyChangedEventArgs args)
    {
        _ = sender;
        if (args.Property == Window.BackgroundProperty ||
            args.Property == Window.ForegroundProperty ||
            args.Property == Window.FontFamilyProperty ||
            args.Property == Window.FontSizeProperty ||
            args.Property == Window.FontWeightProperty ||
            args.Property == Window.FontStyleProperty)
        {
            ApplyWindowThemeToRoot();
        }
    }

    private void ApplyWindowThemeToRoot()
    {
        if (_root is Panel panel)
        {
            panel.Background = _window.Background;
        }
        
        ApplyForegroundIfSupported(_root, _window.Foreground);

        FrameworkElement.SetFontFamily(_root, _window.FontFamily);
        FrameworkElement.SetFontSize(_root, _window.FontSize);
        FrameworkElement.SetFontWeight(_root, _window.FontWeight);
        FrameworkElement.SetFontStyle(_root, _window.FontStyle);
    }

    private static void ApplyForegroundIfSupported(FrameworkElement target, Color value)
    {
        var property = target.GetType().GetProperty(
            "Foreground",
            BindingFlags.Public | BindingFlags.Instance);
        if (property is { CanWrite: true } &&
            property.PropertyType == typeof(Color))
        {
            property.SetValue(target, value);
        }
    }
}
