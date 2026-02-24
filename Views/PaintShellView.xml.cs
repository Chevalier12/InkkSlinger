using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class PaintShellView : UserControl
{
    private sealed class SelectionAdorner : Adorner
    {
        public SelectionAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
        }
    }

    public PaintShellView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "PaintShellView.xml");
        XamlLoader.LoadInto(this, markupPath, this);

        if (this.FindName("CanvasAdornerRoot") is AdornerDecorator canvasAdornerRoot &&
            this.FindName("SelectedShape") is Border selectedShape)
        {
            canvasAdornerRoot.AdornerLayer.AddAdorner(new SelectionAdorner(selectedShape));
        }
    }

    public void SetFont(SpriteFont? font)
    {
        _ = font;
    }
}

