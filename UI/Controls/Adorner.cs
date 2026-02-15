using Microsoft.Xna.Framework;

namespace InkkSlinger;

public abstract class Adorner : FrameworkElement
{
    protected Adorner(UIElement adornedElement)
    {
        AdornedElement = adornedElement;
        IsHitTestVisible = false;
    }

    public UIElement AdornedElement { get; }

    internal AdornerLayer? Layer { get; set; }

    internal LayoutRect LastAdornerRectForTesting { get; private set; }

    internal LayoutRect GetAdornerLayoutRect()
    {
        var rect = GetAdornedBounds();
        LastAdornerRectForTesting = rect;
        return rect;
    }

    protected virtual LayoutRect GetAdornedBounds()
    {
        return AdornedElement.LayoutSlot;
    }
}
