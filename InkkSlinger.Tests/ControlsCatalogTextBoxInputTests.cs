using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogTextBoxInputTests
{
    [Fact]
    public void CatalogTextBox_ClickThenTextInput_FocusesAndAppendsText()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();

            var view = new ControlsCatalogView();
            var uiRoot = new UiRoot(view);
            RunLayout(uiRoot, 1280, 820);

            var buttonsHost = Assert.IsType<StackPanel>(view.FindName("ControlButtonsHost"));
            var textBoxButton = buttonsHost.Children.OfType<Button>().Single(button => button.Text == "TextBox");
            textBoxButton.InvokeFromInput();
            RunLayout(uiRoot, 1280, 820);

            var previewHost = Assert.IsType<ContentControl>(view.FindName("PreviewHost"));
            var previewView = Assert.IsType<TextBoxView>(previewHost.Content);
            var textBox = FindDescendant<TextBox>(previewView);
            Assert.Equal("TextBox sample", textBox.Text);

            Click(uiRoot, Center(textBox.LayoutSlot));
            RunLayout(uiRoot, 1280, 820);

            Assert.Same(textBox, FocusManager.GetFocusedElement());
            Assert.True(textBox.IsFocused);
            Assert.True(uiRoot.ShouldDrawThisFrame(
                new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 1280, 820)));
            uiRoot.CompleteDrawStateForTests();

            uiRoot.RunInputDeltaForTests(CreateTextInputDelta('X', Center(textBox.LayoutSlot)));
            RunLayout(uiRoot, 1280, 820);

            Assert.Equal("TextBox sampleX", textBox.Text);
            Assert.True(uiRoot.ShouldDrawThisFrame(
                new GameTime(TimeSpan.FromMilliseconds(48), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 1280, 820)));
        }
        finally
        {
            RestoreApplicationResources(backup);
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();
        }
    }

    private static void LoadRootAppResources()
    {
        var appPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "App.xml"));
        Assert.True(File.Exists(appPath), $"Expected App.xml to exist at '{appPath}'.");
        XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);
    }

    private static void Click(UiRoot uiRoot, Vector2 point)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftReleased: true));
    }

    private static Vector2 Center(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static T FindDescendant<T>(UIElement root)
        where T : UIElement
    {
        var pending = new Stack<UIElement>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is T typed)
            {
                return typed;
            }

            foreach (var child in current.GetVisualChildren())
            {
                pending.Push(child);
            }
        }

        throw new InvalidOperationException($"Could not find descendant of type '{typeof(T).Name}'.");
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreateTextInputDelta(char character, Vector2 pointer)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char> { character },
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }

    private static ResourceSnapshot CaptureApplicationResources()
    {
        var resources = UiApplication.Current.Resources;
        return new ResourceSnapshot(resources.ToList(), resources.MergedDictionaries.ToList());
    }

    private static void RestoreApplicationResources(ResourceSnapshot snapshot)
    {
        var resources = UiApplication.Current.Resources;
        resources.Clear();
        foreach (var merged in resources.MergedDictionaries.ToList())
        {
            resources.RemoveMergedDictionary(merged);
        }

        foreach (var pair in snapshot.Entries)
        {
            resources[pair.Key] = pair.Value;
        }

        foreach (var merged in snapshot.MergedDictionaries)
        {
            resources.AddMergedDictionary(merged);
        }
    }

    private sealed record ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        List<ResourceDictionary> MergedDictionaries);
}
