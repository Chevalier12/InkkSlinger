using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class RichTextBoxDemoView : UserControl
{
    public RichTextBoxDemoView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "RichTextBoxDemoView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
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

        if (element is RichTextBox richTextBox)
        {
            richTextBox.Font = font;
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }
}
