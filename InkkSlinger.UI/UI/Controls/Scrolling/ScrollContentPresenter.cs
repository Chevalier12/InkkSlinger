using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class ScrollContentPresenter : ContentPresenter
{
    public ScrollViewer? ScrollOwner { get; private set; }

    public bool CanContentScroll => ScrollOwner?.CanContentScroll == true;

    internal void AttachScrollOwner(ScrollViewer owner)
    {
        if (ReferenceEquals(ScrollOwner, owner))
        {
            return;
        }

        ScrollOwner = owner;
        InvalidateMeasure();
        InvalidateArrange();
    }

    internal void DetachScrollOwner(ScrollViewer owner)
    {
        if (!ReferenceEquals(ScrollOwner, owner))
        {
            return;
        }

        ScrollOwner = null;
        InvalidateMeasure();
        InvalidateArrange();
    }

    public LayoutRect MakeVisible(UIElement visual, LayoutRect rectangle)
    {
        if (ScrollOwner == null)
        {
            return rectangle;
        }

        _ = ScrollOwner.MakeVisible(visual, rectangle);
        return rectangle;
    }
}
