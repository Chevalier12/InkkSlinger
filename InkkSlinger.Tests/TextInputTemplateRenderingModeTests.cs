using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TextInputTemplateRenderingModeTests
{
    [Fact]
    public void TextInputs_WithTemplateRoot_DoNotTrackTemplateChildrenInRetainedRenderList()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();

            var host = new StackPanel();
            var textBox = new TextBox { Text = "sample", Width = 240f, Height = 44f };
            var passwordBox = new PasswordBox { Password = "secret", Width = 240f, Height = 44f };
            host.AddChild(textBox);
            host.AddChild(passwordBox);

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot, 640, 360);
            uiRoot.RebuildRenderListForTests();

            var textBoxTemplateRoot = Assert.Single(textBox.GetVisualChildren());
            var passwordBoxTemplateRoot = Assert.Single(passwordBox.GetVisualChildren());
            var retainedOrder = uiRoot.GetRetainedVisualOrderForTests();

            Assert.Contains(textBox, retainedOrder);
            Assert.Contains(passwordBox, retainedOrder);
            Assert.DoesNotContain(textBoxTemplateRoot, retainedOrder);
            Assert.DoesNotContain(passwordBoxTemplateRoot, retainedOrder);
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

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
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
