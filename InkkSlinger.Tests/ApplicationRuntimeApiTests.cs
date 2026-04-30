using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ApplicationRuntimeApiTests
{
    [Fact]
    public void ApplicationCurrent_MainWindow_ExposesRuntimeWindowControls()
    {
        var application = UiApplication.Current;
        var originalFpsEnabled = application.FpsEnabled;
        using var window = new Window(new FakeWindowNativeAdapter(), new FakeWindowGraphicsAdapter());

        try
        {
            application.AttachMainWindow(window, new InkkSlingerOptions { FpsEnabled = true });

            Application.Current.MainWindow.AllowUserResizing = false;
            Application.Current.MainWindow.Width = 1280;
            Application.Current.MainWindow.Height = 720;
            Application.Current.MainWindow.IsFullScreen = true;

            Assert.False(window.AllowUserResizing);
            Assert.Equal(1280, window.Width);
            Assert.Equal(720, window.Height);
            Assert.True(window.IsFullScreen);
        }
        finally
        {
            application.DetachMainWindow(window);
            application.FpsEnabled = originalFpsEnabled;
        }
    }

    [Fact]
    public void ApplicationCurrent_FpsEnabled_IsMutableAtRuntime()
    {
        var application = UiApplication.Current;
        var originalFpsEnabled = application.FpsEnabled;
        using var window = new Window(new FakeWindowNativeAdapter(), new FakeWindowGraphicsAdapter());

        try
        {
            application.AttachMainWindow(window, new InkkSlingerOptions { FpsEnabled = true });
            Assert.True(Application.Current.FpsEnabled);

            Application.Current.FpsEnabled = false;

            Assert.False(application.FpsEnabled);
        }
        finally
        {
            application.DetachMainWindow(window);
            application.FpsEnabled = originalFpsEnabled;
        }
    }

    [Fact]
    public void ApplicationCurrent_Shutdown_InvokesRegisteredShutdownCallback()
    {
        var application = UiApplication.Current;
        var originalFpsEnabled = application.FpsEnabled;
        using var window = new Window(new FakeWindowNativeAdapter(), new FakeWindowGraphicsAdapter());
        var shutdownCalls = 0;

        try
        {
            application.AttachMainWindow(
                window,
                new InkkSlingerOptions { FpsEnabled = true },
                () => shutdownCalls++);

            Application.Current.Shutdown();

            Assert.Equal(1, shutdownCalls);
        }
        finally
        {
            application.DetachMainWindow(window);
            application.FpsEnabled = originalFpsEnabled;
        }
    }

    private sealed class FakeWindowNativeAdapter : IWindowNativeAdapter
    {
        public event EventHandler<EventArgs>? ClientSizeChanged
        {
            add { }
            remove { }
        }

        public string Title { get; set; } = string.Empty;
        public bool AllowUserResizing { get; set; } = true;
        public bool IsBorderless { get; set; }
        public Point Position { get; set; }
        public Rectangle ClientBounds { get; set; } = new(0, 0, 800, 600);
        public IntPtr Handle { get; } = new(42);

        public void EnsureBorderlessChromeStyle(bool isBorderless)
        {
        }
    }

    private sealed class FakeWindowGraphicsAdapter : IWindowGraphicsAdapter
    {
        public int PreferredBackBufferWidth { get; set; } = 800;
        public int PreferredBackBufferHeight { get; set; } = 600;
        public bool IsFullScreen { get; set; }

        public void ApplyChanges()
        {
        }
    }
}