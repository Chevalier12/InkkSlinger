using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class Decorator : FrameworkElement
{
    private UIElement? _child;

    public UIElement? Child
    {
        get => _child;
        set
        {
            if (ReferenceEquals(_child, value))
            {
                return;
            }

            if (_child != null)
            {
                _child.SetVisualParent(null);
                _child.SetLogicalParent(null);
            }

            _child = value;
            if (_child != null)
            {
                _child.SetVisualParent(this);
                _child.SetLogicalParent(this);
            }

            InvalidateMeasure();
        }
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        if (_child != null)
        {
            yield return _child;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return _child != null ? 1 : 0;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        if (index == 0 && _child != null)
        {
            return _child;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        if (_child != null)
        {
            yield return _child;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (_child is not FrameworkElement child)
        {
            return Vector2.Zero;
        }

        child.Measure(availableSize);
        return child.DesiredSize;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (_child is FrameworkElement child)
        {
            child.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
        }

        return finalSize;
    }

    protected internal override bool ShouldSuppressMeasureInvalidationFromDescendantDuringMeasure(FrameworkElement descendant)
    {
        return (IsMeasuring || IsArrangingOverride) && IsDescendantOfChildSubtree(descendant);
    }

    private bool IsDescendantOfChildSubtree(UIElement descendant)
    {
        if (_child == null)
        {
            return false;
        }

        for (UIElement? current = descendant; current != null; current = current.GetInvalidationParent())
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }

            if (ReferenceEquals(current, _child))
            {
                return true;
            }
        }

        return false;
    }
}
