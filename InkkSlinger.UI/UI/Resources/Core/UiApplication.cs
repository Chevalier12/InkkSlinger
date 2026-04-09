using System;

namespace InkkSlinger;

public sealed class UiApplication
{
    private static readonly UiApplication Instance = new();
    private Window? _mainWindow;

    private UiApplication()
    {
    }

    public static UiApplication Current => Instance;

    public ResourceDictionary Resources { get; } = new();

    public Window MainWindow => _mainWindow
        ?? throw new InvalidOperationException("MainWindow is unavailable before InkkSlingerUI.Initialize runs.");

    public bool HasMainWindow => _mainWindow is not null;

    public bool FpsEnabled { get; set; } = true;

    internal void AttachMainWindow(Window mainWindow, InkkSlingerOptions options)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        ArgumentNullException.ThrowIfNull(options);
        FpsEnabled = options.FpsEnabled;
    }

    internal void DetachMainWindow(Window mainWindow)
    {
        if (ReferenceEquals(_mainWindow, mainWindow))
        {
            _mainWindow = null;
        }
    }
}
