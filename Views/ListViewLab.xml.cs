using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ListViewLab : UserControl
{
    private const int ItemCount = 80;

    public ListViewLab()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "ListViewLab.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        PopulateItems();
        LabListView!.SelectionChanged += OnListSelectionChanged;
        UpdateSelectionState();
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        ApplyFontRecursive(this, font);
        if (LabListView != null)
        {
            LabListView.Font = font;
        }
    }

    private void OnSelectFirstClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (LabListView == null || LabListView.Items.Count == 0)
        {
            return;
        }

        LabListView.SelectedIndex = 0;
        UpdateSelectionState();
    }

    private void OnSelectNextClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (LabListView == null || LabListView.Items.Count == 0)
        {
            return;
        }

        var next = LabListView.SelectedIndex + 1;
        if (next >= LabListView.Items.Count)
        {
            next = 0;
        }

        LabListView.SelectedIndex = next;
        UpdateSelectionState();
    }

    private void OnClearSelectionClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (LabListView == null)
        {
            return;
        }

        LabListView.SelectedIndex = -1;
        UpdateSelectionState();
    }

    private void OnReloadItemsClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        PopulateItems();
        UpdateSelectionState();
    }

    private void OnListSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateSelectionState();
    }

    private void PopulateItems()
    {
        if (LabListView == null)
        {
            return;
        }

        LabListView.Items.Clear();
        for (var i = 1; i <= ItemCount; i++)
        {
            var tag = i % 5 == 0 ? "Group B" : "Group A";
            LabListView.Items.Add($"Item {i:000}  |  {tag}");
        }

        LabListView.SelectedIndex = -1;
    }

    private void UpdateSelectionState()
    {
        if (LabListView == null || SelectionStateLabel == null)
        {
            return;
        }

        var selected = LabListView.SelectedItem?.ToString() ?? "(none)";
        SelectionStateLabel.Text = $"SelectedIndex={LabListView.SelectedIndex} | SelectedItem={selected}";
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
}
