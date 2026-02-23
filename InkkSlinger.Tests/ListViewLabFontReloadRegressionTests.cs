using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public class ListViewLabFontReloadRegressionTests
{
    [Fact]
    public void ListView_ReloadLikeMutation_RecreatedLabelsPreserveListFont()
    {
        var listView = new ListView();
        var appliedFont = (SpriteFont)RuntimeHelpers.GetUninitializedObject(typeof(SpriteFont));
        listView.Font = appliedFont;
        AddSeedItems(listView, count: 6);

        var beforeReloadLabels = CollectDescendantLabels(listView);

        Assert.NotEmpty(beforeReloadLabels);
        Assert.All(beforeReloadLabels, label => Assert.Same(appliedFont, label.Font));

        // Mirrors ListViewLab.PopulateItems() behavior used by "Reload Items":
        // clear existing items, add new string items, and let ListView generate new containers.
        listView.Items.Clear();
        AddSeedItems(listView, count: 6);
        var afterReloadLabels = CollectDescendantLabels(listView);

        Assert.NotEmpty(afterReloadLabels);
        Assert.DoesNotContain(afterReloadLabels, label => label.Font is null);
        Assert.All(afterReloadLabels, label => Assert.Same(appliedFont, label.Font));
    }

    private static void AddSeedItems(ListView listView, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            var tag = i % 5 == 0 ? "Group B" : "Group A";
            listView.Items.Add($"Item {i:000}  |  {tag}");
        }
    }

    private static List<Label> CollectDescendantLabels(UIElement root)
    {
        var labels = new List<Label>();
        CollectDescendantLabelsRecursive(root, labels);
        return labels;
    }

    private static void CollectDescendantLabelsRecursive(UIElement? element, List<Label> labels)
    {
        if (element == null)
        {
            return;
        }

        if (element is Label label)
        {
            labels.Add(label);
        }

        foreach (var child in element.GetVisualChildren())
        {
            CollectDescendantLabelsRecursive(child, labels);
        }
    }
}
