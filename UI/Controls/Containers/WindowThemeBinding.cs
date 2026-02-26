using System;

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
            args.Property == Window.FontFamilyProperty ||
            args.Property == Window.FontSizeProperty ||
            args.Property == Window.FontWeightProperty)
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

        _root.FontFamily = _window.FontFamily;
        _root.FontSize = _window.FontSize;
        _root.FontWeight = _window.FontWeight;
    }
}
