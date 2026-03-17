using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class WindowImplicitStyleParityTests
{
    [Fact]
    public void ImplicitStyle_WhenReplaced_ReappliesToExistingWindow()
    {
        var snapshot = CaptureApplicationResources();
        try
        {
            UiApplication.Current.Resources[typeof(Window)] = BuildWindowStyle(new Color(0x12, 0x34, 0x56));
            using var window = new Window(new FakeWindowNativeAdapter(), new FakeWindowGraphicsAdapter());
            Assert.Equal(new Color(0x12, 0x34, 0x56), window.Background);

            UiApplication.Current.Resources[typeof(Window)] = BuildWindowStyle(new Color(0x77, 0x88, 0x99));
            Assert.Equal(new Color(0x77, 0x88, 0x99), window.Background);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void ImplicitStyle_WhenRemoved_ClearsPreviouslyAppliedImplicitValues()
    {
        var snapshot = CaptureApplicationResources();
        try
        {
            UiApplication.Current.Resources[typeof(Window)] = BuildWindowStyle(new Color(0x22, 0x44, 0x66));
            using var window = new Window(new FakeWindowNativeAdapter(), new FakeWindowGraphicsAdapter());
            Assert.Equal(new Color(0x22, 0x44, 0x66), window.Background);

            UiApplication.Current.Resources.Remove(typeof(Window));
            Assert.Equal(Color.Transparent, window.Background);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    private static Style BuildWindowStyle(Color background)
    {
        var style = new Style(typeof(Window));
        style.Setters.Add(new Setter(Window.BackgroundProperty, background));
        return style;
    }

    private static ResourceSnapshot CaptureApplicationResources()
    {
        var resources = UiApplication.Current.Resources;
        return new ResourceSnapshot(
            resources.ToList(),
            resources.MergedDictionaries.ToList());
    }

    private static void RestoreApplicationResources(ResourceSnapshot snapshot)
    {
        TestApplicationResources.Restore(snapshot.Entries, snapshot.MergedDictionaries);
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
        public IntPtr Handle { get; } = new(7);
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

    private readonly record struct ResourceSnapshot(
        IReadOnlyList<KeyValuePair<object, object>> Entries,
        IReadOnlyList<ResourceDictionary> MergedDictionaries);
}
