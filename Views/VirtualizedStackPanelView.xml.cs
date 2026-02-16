using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class VirtualizedStackPanelView : UserControl
{
    public VirtualizedStackPanelView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "VirtualizedStackPanelView.xml");
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
        if (DemoVirtualizedStackPanel == null)
        {
            return;
        }

        for (var i = 1; i <= 500; i++)
        {
            DemoVirtualizedStackPanel.AddChild(new Label
            {
                Text = $"Item {i}",
                Foreground = new Color(220, 238, 255),
                Margin = new Thickness(0f, 0f, 0f, 6f)
            });
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
