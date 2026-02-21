using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public abstract class AnchoredAdorner : Adorner
{
    protected AnchoredAdorner(UIElement adornedElement)
        : base(adornedElement)
    {
    }

    public AdornerTrackingMode TrackingMode { get; set; } = AdornerTrackingMode.RenderBounds;

    protected override LayoutRect GetAdornedBounds()
    {
        LayoutRect targetBounds;
        switch (TrackingMode)
        {
            case AdornerTrackingMode.LayoutSlot:
                targetBounds = AdornedElement.LayoutSlot;
                break;
            case AdornerTrackingMode.RenderBounds:
            default:
                targetBounds = AdornedElement.TryGetRenderBoundsInRootSpace(out var renderBounds)
                    ? renderBounds
                    : AdornedElement.LayoutSlot;
                break;
        }

        return GetAnchorRect(targetBounds);
    }

    protected virtual LayoutRect GetAnchorRect(LayoutRect targetBounds)
    {
        return targetBounds;
    }

    protected sealed override void OnRender(SpriteBatch spriteBatch)
    {
        OnRenderAdorner(spriteBatch, LayoutSlot);
    }

    protected virtual void OnRenderAdorner(SpriteBatch spriteBatch, LayoutRect rect)
    {
    }
}
