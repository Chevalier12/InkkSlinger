using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class WindowWrapperEdgeParityTests
{
    [Fact]
    public void SetClientSize_InvalidValues_ShouldThrow()
    {
        var window = CreateWindow();

        Assert.Throws<ArgumentOutOfRangeException>(() => window.SetClientSize(0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.SetClientSize(100, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.SetClientSize(-1, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.SetClientSize(100, -1));
    }

    [Fact]
    public void SetClientSize_ApplyChangesFalse_ShouldUpdatePreferredBufferWithoutImmediateApply()
    {
        var native = new FakeWindowNativeAdapter();
        var graphics = new FakeWindowGraphicsAdapter();
        var window = new Window(native, graphics);

        window.SetClientSize(1280, 900, applyChanges: false);

        Assert.Equal(1280, graphics.PreferredBackBufferWidth);
        Assert.Equal(900, graphics.PreferredBackBufferHeight);
        Assert.Equal(0, graphics.ApplyChangesCallCount);
    }

    [Fact]
    public void ToggleFullScreen_ShouldFlipState()
    {
        var native = new FakeWindowNativeAdapter();
        var graphics = new FakeWindowGraphicsAdapter();
        var window = new Window(native, graphics);

        Assert.False(window.IsFullScreen);
        window.ToggleFullScreen(applyChanges: false);
        Assert.True(window.IsFullScreen);
        window.ToggleFullScreen(applyChanges: false);
        Assert.False(window.IsFullScreen);
    }

    [Fact]
    public void Dispose_ShouldUnhookClientSizeChanged_AndBeIdempotent()
    {
        var native = new FakeWindowNativeAdapter();
        var graphics = new FakeWindowGraphicsAdapter();
        var window = new Window(native, graphics);
        var eventCount = 0;
        window.ClientSizeChanged += (_, _) => eventCount++;

        native.RaiseClientSizeChanged();
        Assert.Equal(1, eventCount);

        window.Dispose();
        native.RaiseClientSizeChanged();
        Assert.Equal(1, eventCount);

        window.Dispose();
        native.RaiseClientSizeChanged();
        Assert.Equal(1, eventCount);
    }

    private static Window CreateWindow()
    {
        return new Window(new FakeWindowNativeAdapter(), new FakeWindowGraphicsAdapter());
    }

    private sealed class FakeWindowNativeAdapter : IWindowNativeAdapter
    {
        public event EventHandler<EventArgs>? ClientSizeChanged;

        public string Title { get; set; } = string.Empty;

        public bool AllowUserResizing { get; set; }

        public bool IsBorderless { get; set; }

        public Point Position { get; set; }

        public Rectangle ClientBounds { get; set; } = new(0, 0, 800, 600);

        public IntPtr Handle { get; } = new(1234);

        public void RaiseClientSizeChanged()
        {
            ClientSizeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeWindowGraphicsAdapter : IWindowGraphicsAdapter
    {
        public int PreferredBackBufferWidth { get; set; } = 800;

        public int PreferredBackBufferHeight { get; set; } = 600;

        public bool IsFullScreen { get; set; }

        public int ApplyChangesCallCount { get; private set; }

        public void ApplyChanges()
        {
            ApplyChangesCallCount++;
        }
    }
}
