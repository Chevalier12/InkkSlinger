using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ScrollViewerEdgeCasesView : UserControl
{
    private int _rowCount = 400;
    private bool _useWideRows = true;
    private SpriteFont? _currentFont;

    public ScrollViewerEdgeCasesView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "ScrollViewerEdgeCasesView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        PopulateAll();
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        _currentFont = font;
        ApplyFontRecursive(this, font);
    }

    private void OnResetClick(object? sender, RoutedSimpleEventArgs args)
    {
        _rowCount = 400;
        _useWideRows = true;
        if (InnerViewer != null)
        {
            InnerViewer.IsVisible = true;
        }

        PopulateAll();
    }

    private void OnGrowClick(object? sender, RoutedSimpleEventArgs args)
    {
        _rowCount = Math.Min(3000, _rowCount + 200);
        PopulateScrollableRows();
    }

    private void OnShrinkClick(object? sender, RoutedSimpleEventArgs args)
    {
        _rowCount = Math.Max(20, _rowCount - 200);
        PopulateScrollableRows();
    }

    private void OnToggleWideRowsClick(object? sender, RoutedSimpleEventArgs args)
    {
        _useWideRows = !_useWideRows;
        PopulateScrollableRows();
    }

    private void OnToggleNestedViewerClick(object? sender, RoutedSimpleEventArgs args)
    {
        if (InnerViewer == null)
        {
            return;
        }

        InnerViewer.IsVisible = !InnerViewer.IsVisible;
    }

    private void PopulateAll()
    {
        PopulateScrollableRows();
        PopulateFitPanel();
        PopulateTinyPanel();
        PopulateNestedPanel();
        PopulateEdgeListBox();
    }

    private void PopulateScrollableRows()
    {
        if (AutoBothPanel == null)
        {
            return;
        }

        while (AutoBothPanel.Children.Count > 0)
        {
            _ = AutoBothPanel.RemoveChildAt(AutoBothPanel.Children.Count - 1);
        }

        var suffix = _useWideRows
            ? " | Wide payload: 0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz"
            : string.Empty;
        var rowWidth = _useWideRows ? 980f : 280f;

        for (var i = 1; i <= _rowCount; i++)
        {
            AutoBothPanel.AddChild(CreateRow($"Auto Row {i}{suffix}", rowWidth));
        }
    }

    private void PopulateFitPanel()
    {
        if (FitPanel == null)
        {
            return;
        }

        while (FitPanel.Children.Count > 0)
        {
            _ = FitPanel.RemoveChildAt(FitPanel.Children.Count - 1);
        }

        for (var i = 1; i <= 8; i++)
        {
            FitPanel.AddChild(CreateLabel($"Fit Row {i}", bottomMargin: 6f))
            ;
        }
    }

    private void PopulateTinyPanel()
    {
        if (TinyPanel == null)
        {
            return;
        }

        while (TinyPanel.Children.Count > 0)
        {
            _ = TinyPanel.RemoveChildAt(TinyPanel.Children.Count - 1);
        }

        for (var i = 1; i <= 220; i++)
        {
            TinyPanel.AddChild(CreateLabel($"Tiny Row {i}", bottomMargin: 3f));
        }
    }

    private void PopulateNestedPanel()
    {
        if (InnerPanel == null)
        {
            return;
        }

        while (InnerPanel.Children.Count > 0)
        {
            _ = InnerPanel.RemoveChildAt(InnerPanel.Children.Count - 1);
        }

        for (var i = 1; i <= 140; i++)
        {
            InnerPanel.AddChild(CreateRow(
                $"Inner Row {i} | nested horizontal + vertical stress",
                720f));
        }
    }

    private void PopulateEdgeListBox()
    {
        if (EdgeListBox == null)
        {
            return;
        }

        EdgeListBox.Items.Clear();
        for (var i = 1; i <= 220; i++)
        {
            EdgeListBox.Items.Add(CreateLabel(
                $"ListBox Item {i} | selection + scrolling behavior",
                bottomMargin: 0f));
        }
    }

    private Border CreateRow(string text, float width)
    {
        return new Border
        {
            Width = width,
            Height = 24f,
            Margin = new Thickness(0f, 0f, 0f, 4f),
            Background = new Color(17, 28, 44),
            BorderBrush = new Color(56, 87, 118),
            BorderThickness = new Thickness(1f),
            Padding = new Thickness(6f, 2f, 6f, 2f),
            Child = CreateLabel(text, bottomMargin: 0f)
        };
    }

    private Label CreateLabel(string text, float bottomMargin)
    {
        return new Label
        {
            Text = text,
            Font = _currentFont,
            Foreground = new Color(220, 238, 255),
            Margin = new Thickness(0f, 0f, 0f, bottomMargin)
        };
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
