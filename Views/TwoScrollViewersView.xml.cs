using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class TwoScrollViewersView : UserControl
{
    public TwoScrollViewersView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "TwoScrollViewersView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        PopulateVirtualizedPanelItems();
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        ApplyFontRecursive(this, font);
    }

    private static void ApplyFontRecursive(UIElement? element, SpriteFont font)
    {
        if (element == null)
        {
            return;
        }

        if (element is TextBlock textBlock)
        {
            textBlock.Font = font;
        }

        if (element is Label label)
        {
            label.Font = font;
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }

    private void PopulateVirtualizedPanelItems()
    {
        if (DemoVirtualizedScrollPanel == null)
        {
            return;
        }

        for (var i = 1; i <= 500; i++)
        {
            DemoVirtualizedScrollPanel.AddChild(new Label
            {
                Text = $"Virtualized Item {i}",
                Foreground = new Microsoft.Xna.Framework.Color(220, 238, 255),
                Margin = new Thickness(0f, 0f, 0f, 6f)
            });
        }
    }
}
