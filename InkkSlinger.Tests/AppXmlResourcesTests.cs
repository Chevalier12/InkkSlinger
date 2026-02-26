using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AppXmlResourcesTests
{
    [Fact]
    public void LoadApplicationResourcesFromFile_ApplicationRoot_LoadsGlobalButtonStyle()
    {
        var backup = CaptureApplicationResources();
        var tempRoot = CreateTempDirectory();
        try
        {
            var appPath = Path.Combine(tempRoot, "App.xml");
            File.WriteAllText(
                appPath,
                """
<Application xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Application.Resources>
    <ResourceDictionary>
      <Style TargetType="{x:Type Button}">
        <Setter Property="Background" Value="#1255AA" />
        <Setter Property="BorderThickness" Value="5" />
      </Style>
    </ResourceDictionary>
  </Application.Resources>
</Application>
""");

            XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);

            var host = new Panel { Width = 300f, Height = 180f };
            var button = new Button { Text = "AppStyle", Width = 140f, Height = 36f };
            host.AddChild(button);
            var uiRoot = new UiRoot(host);
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 300, 180));

            Assert.Equal(new Color(18, 85, 170), button.Background);
            Assert.Equal(5f, button.BorderThickness);
        }
        finally
        {
            RestoreApplicationResources(backup);
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void LoadApplicationResourcesFromString_ResourceDictionaryRoot_LoadsResourceByTypeKey()
    {
        var backup = CaptureApplicationResources();
        try
        {
            XamlLoader.LoadApplicationResourcesFromString(
                """
<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style TargetType="{x:Type Button}">
    <Setter Property="Padding" Value="9,8,7,6" />
  </Style>
</ResourceDictionary>
""",
                clearExisting: true);

            Assert.True(UiApplication.Current.Resources.TryGetValue(typeof(Button), out var resource));
            var style = Assert.IsType<Style>(resource);
            Assert.Equal(typeof(Button), style.TargetType);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "InkkSlinger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for temp artifacts.
        }
    }

    private sealed record ResourceSnapshot(
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<object, object>> Entries,
        System.Collections.Generic.List<ResourceDictionary> MergedDictionaries);
}
