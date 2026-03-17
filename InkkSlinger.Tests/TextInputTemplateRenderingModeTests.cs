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
            var richTextBox = new RichTextBox { Width = 240f, Height = 120f };
            DocumentEditing.ReplaceAllText(richTextBox.Document, "rich sample");
            host.AddChild(textBox);
            host.AddChild(passwordBox);
            host.AddChild(richTextBox);

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot, 640, 360);
            uiRoot.RebuildRenderListForTests();

            var textBoxTemplateRoot = Assert.Single(textBox.GetVisualChildren());
            var passwordBoxTemplateRoot = Assert.Single(passwordBox.GetVisualChildren());
            var richTextBoxTemplateRoot = Assert.Single(richTextBox.GetVisualChildren());
            var retainedOrder = uiRoot.GetRetainedVisualOrderForTests();

            Assert.Contains(textBox, retainedOrder);
            Assert.Contains(passwordBox, retainedOrder);
            Assert.Contains(richTextBox, retainedOrder);
            Assert.DoesNotContain(textBoxTemplateRoot, retainedOrder);
            Assert.DoesNotContain(passwordBoxTemplateRoot, retainedOrder);
            Assert.DoesNotContain(richTextBoxTemplateRoot, retainedOrder);
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
        TestApplicationResources.Restore(snapshot.Entries, snapshot.MergedDictionaries);
    }

    private sealed record ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        List<ResourceDictionary> MergedDictionaries);
}
