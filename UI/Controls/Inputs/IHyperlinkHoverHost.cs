using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal interface IHyperlinkHoverHost
{
    void UpdateHoveredHyperlinkFromPointer(Vector2 pointerPosition);
}
