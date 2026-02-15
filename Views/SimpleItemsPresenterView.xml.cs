using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class SimpleItemsPresenterView : UserControl
{
    private readonly ItemsControl _itemsOwner = new();

    public SimpleItemsPresenterView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "SimpleItemsPresenterView.xml");
        XamlLoader.LoadInto(this, markupPath, this);

        if (DemoItemsPresenter != null)
        {
            DemoItemsPresenter.SetExplicitItemsOwner(_itemsOwner);
        }

        for (var i = 1; i <= 50; i++)
        {
            _itemsOwner.Items.Add(new Label
            {
                Text = $"Label {i}",
                Foreground = new Color(220, 238, 255)
            });
        }
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
