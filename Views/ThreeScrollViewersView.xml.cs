using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ThreeScrollViewersView : UserControl
{
    public ThreeScrollViewersView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "ThreeScrollViewersView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        PopulateItems();
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        ApplyFontRecursive(this, font);
    }

    private void PopulateItems()
    {
        var textColor = new Color(220, 238, 255);

        if (DemoStackPanel != null)
        {
            for (var i = 1; i <= 200; i++)
            {
                DemoStackPanel.AddChild(new Label
                {
                    Text = $"StackPanel Item {i}",
                    Foreground = textColor,
                    Margin = new Thickness(0f, 0f, 0f, 6f)
                });
            }
        }

        if (DemoVirtualizingStackPanel != null)
        {
            for (var i = 1; i <= 500; i++)
            {
                DemoVirtualizingStackPanel.AddChild(new Label
                {
                    Text = $"Virtualized Item {i}",
                    Foreground = textColor,
                    Margin = new Thickness(0f, 0f, 0f, 6f)
                });
            }
        }

        if (DemoListBox != null)
        {
            for (var i = 1; i <= 300; i++)
            {
                DemoListBox.Items.Add($"ListBox Item {i}");
            }
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

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }
}
