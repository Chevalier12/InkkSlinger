using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class WindowThemeBindingTests
{
    [Fact]
    public void ApplyWindowThemeToRoot_PropagatesBackgroundForegroundAndFontValues()
    {
        var window = new Window(new FakeWindowNativeAdapter(), new FakeWindowGraphicsAdapter());
        var panelRoot = new Grid();
        using var panelBinding = new WindowThemeBinding(window, panelRoot);
        var controlRoot = new Button();
        using var controlBinding = new WindowThemeBinding(window, controlRoot);

        window.Background = new Color(0x11, 0x22, 0x33);
        window.Foreground = new Color(0xEE, 0xDD, 0xCC);
        window.FontFamily = "Segoe UI";
        window.FontSize = 18f;
        window.FontWeight = "Bold";

        Assert.Equal(new Color(0x11, 0x22, 0x33), panelRoot.Background);
        Assert.Equal(new Color(0xEE, 0xDD, 0xCC), controlRoot.Foreground);
        Assert.Equal("Segoe UI", panelRoot.FontFamily);
        Assert.Equal(18f, panelRoot.FontSize);
        Assert.Equal("Bold", panelRoot.FontWeight);
    }

    [Fact]
    public void Dispose_StopsFurtherThemePropagation()
    {
        var window = new Window(new FakeWindowNativeAdapter(), new FakeWindowGraphicsAdapter());
        var root = new Button();
        var binding = new WindowThemeBinding(window, root);

        window.Foreground = new Color(0x40, 0x50, 0x60);
        Assert.Equal(new Color(0x40, 0x50, 0x60), root.Foreground);

        binding.Dispose();
        window.Foreground = new Color(0x10, 0x20, 0x30);

        Assert.Equal(new Color(0x40, 0x50, 0x60), root.Foreground);
    }

    private sealed class FakeWindowNativeAdapter : IWindowNativeAdapter
    {
        public event EventHandler<EventArgs>? ClientSizeChanged
        {
            add { }
            remove { }
        }

        public string Title { get; set; } = string.Empty;
        public bool AllowUserResizing { get; set; }
        public bool IsBorderless { get; set; }
        public Point Position { get; set; }
        public Rectangle ClientBounds { get; set; } = new(0, 0, 800, 600);
        public IntPtr Handle { get; } = new(42);
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
