using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class AdornerLayer : Panel
{
    private readonly Dictionary<UIElement, List<Adorner>> _adornersByElement = new();
    private readonly Dictionary<UIElement, EventHandler<DependencyPropertyChangedEventArgs>> _propertyHandlers = new();
    private readonly Dictionary<FrameworkElement, EventHandler> _layoutHandlers = new();

    public void AddAdorner(Adorner adorner)
    {
        if (ContainsAdorner(adorner))
        {
            return;
        }

        var adornedElement = adorner.AdornedElement;
        if (!_adornersByElement.TryGetValue(adornedElement, out var list))
        {
            list = new List<Adorner>();
            _adornersByElement[adornedElement] = list;
            SubscribeToAdornedElement(adornedElement);
        }

        list.Add(adorner);
        adorner.Layer = this;
        AddChild(adorner);
        Panel.SetZIndex(adorner, 2000);
    }

    public bool RemoveAdorner(Adorner adorner)
    {
        var adornedElement = adorner.AdornedElement;
        if (!_adornersByElement.TryGetValue(adornedElement, out var list))
        {
            return false;
        }

        var removed = list.Remove(adorner);
        if (!removed)
        {
            return false;
        }

        RemoveChild(adorner);
        adorner.Layer = null;

        if (list.Count == 0)
        {
            UnsubscribeFromAdornedElement(adornedElement);
            _adornersByElement.Remove(adornedElement);
        }

        return true;
    }

    public IReadOnlyList<Adorner> GetAdorners(UIElement adornedElement)
    {
        if (_adornersByElement.TryGetValue(adornedElement, out var list))
        {
            return list;
        }

        return Array.Empty<Adorner>();
    }

    public static AdornerLayer? GetAdornerLayer(UIElement element)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is AdornerDecorator decorator)
            {
                return decorator.AdornerLayer;
            }

            if (current is AdornerLayer layer)
            {
                return layer;
            }
        }

        return null;
    }

    public static bool Attach(UIElement adornedElement, Adorner adorner)
    {
        var layer = GetAdornerLayer(adornedElement);
        if (layer == null)
        {
            return false;
        }

        layer.AddAdorner(adorner);
        return true;
    }

    public static bool Detach(UIElement adornedElement, Adorner adorner)
    {
        var layer = GetAdornerLayer(adornedElement);
        return layer != null && layer.RemoveAdorner(adorner);
    }

    public override bool HitTest(Vector2 point)
    {
        if (!IsVisible || !IsEnabled || !IsHitTestVisible)
        {
            return false;
        }

        if (!IsPointVisibleThroughClipChain(point))
        {
            return false;
        }

        foreach (var child in Children)
        {
            if (child.HitTest(point))
            {
                return true;
            }
        }

        // Pass through when no interactive adorner child is hit.
        return false;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var child in Children)
        {
            if (child is not Adorner adorner)
            {
                continue;
            }

            var rect = adorner.GetAdornerLayoutRect();
            adorner.Arrange(rect);
        }

        return finalSize;
    }

    private bool ContainsAdorner(Adorner adorner)
    {
        foreach (var child in Children)
        {
            if (ReferenceEquals(child, adorner))
            {
                return true;
            }
        }

        return false;
    }

    private void SubscribeToAdornedElement(UIElement element)
    {
        EventHandler<DependencyPropertyChangedEventArgs> propertyHandler = (_, _) => InvalidateArrange();
        element.DependencyPropertyChanged += propertyHandler;
        _propertyHandlers[element] = propertyHandler;

        if (element is FrameworkElement frameworkElement)
        {
            EventHandler layoutHandler = (_, _) => InvalidateArrange();
            frameworkElement.LayoutUpdated += layoutHandler;
            _layoutHandlers[frameworkElement] = layoutHandler;
        }
    }

    private void UnsubscribeFromAdornedElement(UIElement element)
    {
        if (_propertyHandlers.TryGetValue(element, out var propertyHandler))
        {
            element.DependencyPropertyChanged -= propertyHandler;
            _propertyHandlers.Remove(element);
        }

        if (element is FrameworkElement frameworkElement &&
            _layoutHandlers.TryGetValue(frameworkElement, out var layoutHandler))
        {
            frameworkElement.LayoutUpdated -= layoutHandler;
            _layoutHandlers.Remove(frameworkElement);
        }
    }
}
