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

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        if (TryFindAncestorScrollViewer(AdornedElement, out var scrollViewer) &&
            scrollViewer != null &&
            scrollViewer.TryGetContentViewportClipRect(out clipRect))
        {
            return clipRect.Width > 0f && clipRect.Height > 0f;
        }

        clipRect = default;
        return false;
    }

    private static bool TryFindAncestorScrollViewer(UIElement start, out ScrollViewer? scrollViewer)
    {
        for (var current = start.VisualParent ?? start.LogicalParent; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is ScrollViewer found)
            {
                scrollViewer = found;
                return true;
            }
        }

        scrollViewer = null;
        return false;
    }
}
