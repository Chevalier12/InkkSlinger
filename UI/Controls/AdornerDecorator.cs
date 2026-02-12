using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class AdornerDecorator : Decorator
{
    private readonly AdornerLayer _adornerLayer;

    public AdornerDecorator()
    {
        _adornerLayer = new AdornerLayer();
        _adornerLayer.SetVisualParent(this);
        _adornerLayer.SetLogicalParent(this);
        Panel.SetZIndex(_adornerLayer, 1000);
    }

    public AdornerLayer AdornerLayer => _adornerLayer;

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        if (Child != null)
        {
            yield return Child;
        }

        yield return _adornerLayer;
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        if (Child != null)
        {
            yield return Child;
        }

        yield return _adornerLayer;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        _adornerLayer.Measure(availableSize);
        desired.X = MathF.Max(desired.X, _adornerLayer.DesiredSize.X);
        desired.Y = MathF.Max(desired.Y, _adornerLayer.DesiredSize.Y);
        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        base.ArrangeOverride(finalSize);
        _adornerLayer.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
        return finalSize;
    }
}
