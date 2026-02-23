using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class CollectionAddIsolationView : UserControl
{
    private readonly CollectionAddIsolationViewModel _viewModel = new();
    private int _nextId = 1;

    public CollectionAddIsolationView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "CollectionAddIsolationView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        DataContext = _viewModel;

        for (var i = 0; i < 9; i++)
        {
            _viewModel.SharedItems.Add($"#{_nextId++} Seed Item");
        }

        DemoListBox!.ItemsSource = _viewModel.SharedItems;
        DemoListView!.ItemsSource = _viewModel.SharedItems;
        _viewModel.Status = $"Items={_viewModel.SharedItems.Count}";
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        ApplyFontRecursive(this, font);
    }

    private void OnAddItemClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _viewModel.SharedItems.Add($"#{_nextId++} New Item");
        _viewModel.Status = $"Items={_viewModel.SharedItems.Count}";
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

        if (element is ListBox listBox)
        {
            listBox.Font = font;
        }

        if (element is ListView listView)
        {
            listView.Font = font;
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }
}
