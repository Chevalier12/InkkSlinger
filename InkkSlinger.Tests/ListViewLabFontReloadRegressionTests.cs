using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public class ListViewLabFontReloadRegressionTests
{
    [Fact]
    public void ListView_ReloadLikeMutation_RecreatedLabelsLosePreviouslyAppliedFont()
    {
        var listView = new ListView();
        AddSeedItems(listView, count: 6);

        var appliedFont = (SpriteFont)RuntimeHelpers.GetUninitializedObject(typeof(SpriteFont));
        ApplyFontRecursive(listView, appliedFont);
        var beforeReloadLabels = CollectDescendantLabels(listView);

        Assert.NotEmpty(beforeReloadLabels);
        Assert.All(beforeReloadLabels, label => Assert.Same(appliedFont, label.Font));

        // Mirrors ListViewLab.PopulateItems() behavior used by "Reload Items":
        // clear existing items, add new string items, and let ListView generate new containers.
        listView.Items.Clear();
        AddSeedItems(listView, count: 6);
        var afterReloadLabels = CollectDescendantLabels(listView);

        Assert.NotEmpty(afterReloadLabels);
        Assert.Contains(afterReloadLabels, label => label.Font is null);
        Assert.DoesNotContain(afterReloadLabels, label => ReferenceEquals(label.Font, appliedFont));
    }

    private static void AddSeedItems(ListView listView, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            var tag = i % 5 == 0 ? "Group B" : "Group A";
            listView.Items.Add($"Item {i:000}  |  {tag}");
        }
    }

    private static void ApplyFontRecursive(UIElement? element, SpriteFont font)
    {
        if (element == null)
        {
            return;
        }

        if (element is Label label)
        {
            label.Font = font;
        }

        if (element is TextBlock textBlock)
        {
            textBlock.Font = font;
        }

        if (element is Button button)
        {
            button.Font = font;
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
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
