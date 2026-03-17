using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ResourceDictionaryMergedSemanticsTests
{
    [Fact]
    public void LocalEntry_OverridesMergedEntry_WithSameKey()
    {
        var root = new ResourceDictionary();
        var merged = new ResourceDictionary();
        merged["Accent"] = "merged";
        root.AddMergedDictionary(merged);
        root["Accent"] = "local";

        Assert.True(root.TryGetValue("Accent", out var resolved));
        Assert.Equal("local", resolved);
    }

    [Fact]
    public void DuplicateKeysAcrossMergedDictionaries_LastAddedWins()
    {
        var root = new ResourceDictionary();
        var first = new ResourceDictionary();
        first["Accent"] = "first";
        var second = new ResourceDictionary();
        second["Accent"] = "second";

        root.AddMergedDictionary(first);
        root.AddMergedDictionary(second);

        Assert.True(root.TryGetValue("Accent", out var resolved));
        Assert.Equal("second", resolved);
    }

    [Fact]
    public void AddMergedDictionary_AllowsDirectMergeWhenAlreadyTransitivelyMerged()
    {
        var root = new ResourceDictionary();
        var bridge = new ResourceDictionary();
        var transitive = new ResourceDictionary();
        var direct = new ResourceDictionary();

        transitive["Accent"] = "transitive";
        direct["Accent"] = "direct";

        bridge.AddMergedDictionary(transitive);
        root.AddMergedDictionary(bridge);
        root.AddMergedDictionary(direct);

        Assert.True(root.TryGetValue("Accent", out var initial));
        Assert.Equal("direct", initial);

        root.RemoveMergedDictionary(direct);
        Assert.True(root.TryGetValue("Accent", out var fallback));
        Assert.Equal("transitive", fallback);

        root.AddMergedDictionary(transitive);
        Assert.True(root.TryGetValue("Accent", out var promoted));
        Assert.Equal("transitive", promoted);
    }

    [Fact]
    public void RemoveMergedDictionary_WithDuplicateDirectEntries_RemovesOneOccurrence()
    {
        var root = new ResourceDictionary();
        var shared = new ResourceDictionary();
        shared["Accent"] = "shared";

        root.AddMergedDictionary(shared);
        root.AddMergedDictionary(shared);
        Assert.Equal(2, root.MergedDictionaries.Count);

        var removedFirst = root.RemoveMergedDictionary(shared);
        Assert.True(removedFirst);
        Assert.Single(root.MergedDictionaries);
        Assert.Same(shared, root.MergedDictionaries[0]);
        Assert.True(root.TryGetValue("Accent", out var afterFirstRemove));
        Assert.Equal("shared", afterFirstRemove);

        var removedSecond = root.RemoveMergedDictionary(shared);
        Assert.True(removedSecond);
        Assert.Empty(root.MergedDictionaries);
        Assert.False(root.TryGetValue("Accent", out _));
    }

    [Fact]
    public void NestedMergedDictionaries_RespectReverseMergePrecedence()
    {
        var root = new ResourceDictionary();
        var first = new ResourceDictionary();
        first["Accent"] = "first";

        var second = new ResourceDictionary();
        second["Accent"] = "second-local";
        var secondNested = new ResourceDictionary();
        secondNested["Accent"] = "second-nested";
        second.AddMergedDictionary(secondNested);

        root.AddMergedDictionary(first);
        root.AddMergedDictionary(second);

        Assert.True(root.TryGetValue("Accent", out var initial));
        Assert.Equal("second-local", initial);

        second.Remove("Accent");
        Assert.True(root.TryGetValue("Accent", out var fallback));
        Assert.Equal("second-nested", fallback);
    }

    [Fact]
    public void ImplicitStyle_FromMergedDictionary_ReappliesWhenMergedDictionariesChange()
    {
        var backup = CaptureApplicationResources();
        try
        {
            var first = new ResourceDictionary();
            first[typeof(Panel)] = BuildPanelStyle(new Color(0x15, 0x25, 0x35));

            var second = new ResourceDictionary();
            second[typeof(Panel)] = BuildPanelStyle(new Color(0x75, 0x65, 0x55));

            UiApplication.Current.Resources.AddMergedDictionary(first);

            var panel = new Panel
            {
                Width = 200f,
                Height = 100f
            };
            var uiRoot = BuildUiRootWithSingleChild(panel, 420, 260);
            RunLayout(uiRoot, 420, 260);
            panel.RaiseLoaded();
            Assert.Equal(new Color(0x15, 0x25, 0x35), panel.Background);

            UiApplication.Current.Resources.AddMergedDictionary(second);
            Assert.Equal(new Color(0x75, 0x65, 0x55), panel.Background);

            UiApplication.Current.Resources.RemoveMergedDictionary(second);
            Assert.Equal(new Color(0x15, 0x25, 0x35), panel.Background);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void AddMergedDictionary_SelfReference_Throws()
    {
        var dictionary = new ResourceDictionary();
        var ex = Assert.Throws<InvalidOperationException>(() => dictionary.AddMergedDictionary(dictionary));
        Assert.Contains("cannot merge itself", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddMergedDictionary_Cycle_Throws()
    {
        var first = new ResourceDictionary();
        var second = new ResourceDictionary();
        first.AddMergedDictionary(second);

        var ex = Assert.Throws<InvalidOperationException>(() => second.AddMergedDictionary(first));
        Assert.Contains("cycle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Style BuildPanelStyle(Color background)
    {
        var style = new Style(typeof(Panel));
        style.Setters.Add(new Setter(Panel.BackgroundProperty, background));
        return style;
    }

    private static UiRoot BuildUiRootWithSingleChild(FrameworkElement element, int width, int height)
    {
        var host = new Canvas
        {
            Width = width,
            Height = height
        };
        host.AddChild(element);
        Canvas.SetLeft(element, 24f);
        Canvas.SetTop(element, 18f);
        return new UiRoot(host);
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
