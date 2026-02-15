using System;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ScrollViewerTextBoxView : UserControl
{
    public ScrollViewerTextBoxView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "ScrollViewerTextBoxView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        PopulateText();
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        ApplyFontRecursive(this, font);
    }

    public string GetDiagnostics()
    {
        if (DemoTextBox == null)
        {
            return "TB:n/a";
        }

        var actualWidth = DemoTextBox.ActualWidth;
        var innerWidth = MathF.Max(0f, actualWidth - DemoTextBox.Padding.Horizontal - (DemoTextBox.BorderThickness * 2f));
        var fontState = DemoTextBox.Font != null ? "Y" : "N";
        return $"TB:{actualWidth:0} IN:{innerWidth:0} WR:{DemoTextBox.TextWrapping} F:{fontState}";
    }

    private void PopulateText()
    {
        if (DemoTextBox == null)
        {
            return;
        }

        var builder = new StringBuilder(4096);
        for (var i = 1; i <= 120; i++)
        {
            builder.Append("Line ");
            builder.Append(i);
            builder.Append(": This is filler content to force a tall TextBox inside a ScrollViewer.");
            builder.Append('\n');
        }

        DemoTextBox.Text = builder.ToString();
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

        if (element is TextBox textBox)
        {
            textBox.Font = font;
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }
}
