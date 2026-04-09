using System.Collections.Generic;
using System.IO;
using Xunit;

namespace InkkSlinger.Tests;

internal static class TestApplicationResources
{
    public static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    public static string GetDemoAppAppXmlPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory != null)
        {
            var candidate = Path.Combine(currentDirectory.FullName, "InkkSlinger.DemoApp", "App.xml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            currentDirectory = currentDirectory.Parent;
        }

        var fallback = Path.Combine(GetRepositoryRoot(), "InkkSlinger.DemoApp", "App.xml");
        Assert.True(File.Exists(fallback), $"Expected DemoApp App.xml to exist at '{fallback}'.");
        return fallback;
    }

    public static void LoadDemoAppResources()
    {
        XamlLoader.LoadApplicationResourcesFromFile(GetDemoAppAppXmlPath(), clearExisting: true);
    }

    public static void Restore(IEnumerable<KeyValuePair<object, object>> entries)
    {
        UiApplication.Current.Resources.ReplaceContents(entries, notifyChanged: false);
    }

    public static void Restore(
        IEnumerable<KeyValuePair<object, object>> entries,
        IEnumerable<ResourceDictionary> mergedDictionaries)
    {
        UiApplication.Current.Resources.ReplaceContents(entries, mergedDictionaries, notifyChanged: false);
    }
}