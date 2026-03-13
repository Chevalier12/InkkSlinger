using System.Collections.Generic;
using Xunit;

namespace InkkSlinger.Tests;

public class ListViewLabFontReloadRegressionTests
{
    [Fact]
    public void ListView_ReloadLikeMutation_RecreatedLabelsPreserveListTypography()
    {
        var listView = new ListView
        {
            FontFamily = "Segoe UI",
            FontSize = 18f,
            FontWeight = "SemiBold",
            FontStyle = "Italic"
        };
        AddSeedItems(listView, count: 6);

        var beforeReloadLabels = CollectDescendantLabels(listView);

        Assert.NotEmpty(beforeReloadLabels);
        Assert.All(beforeReloadLabels, label => AssertTypography(label, listView));

        listView.Items.Clear();
        AddSeedItems(listView, count: 6);
        var afterReloadLabels = CollectDescendantLabels(listView);

        Assert.NotEmpty(afterReloadLabels);
        Assert.All(afterReloadLabels, label => AssertTypography(label, listView));
    }

    private static void AssertTypography(Label label, ListView listView)
    {
        Assert.Equal(listView.FontFamily, label.FontFamily);
        Assert.Equal(listView.FontSize, label.FontSize);
        Assert.Equal(listView.FontWeight, label.FontWeight);
        Assert.Equal(listView.FontStyle, label.FontStyle);
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
