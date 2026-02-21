using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class AdornerLayer : Panel
{
    private readonly Dictionary<UIElement, List<Adorner>> _adornersByElement = new();
    private readonly Dictionary<UIElement, EventHandler<DependencyPropertyChangedEventArgs>> _propertyHandlers = new();
    private readonly Dictionary<FrameworkElement, EventHandler> _layoutHandlers = new();
    private readonly Dictionary<Adorner, LayoutRect> _arrangedRects = new();

    public void AddAdorner(Adorner adorner)
    {
        AddAdorner(adorner.AdornedElement, adorner);
    }

    public void AddAdorner(UIElement adornedElement, Adorner adorner)
    {
        if (adorner.Layer != null && !ReferenceEquals(adorner.Layer, this))
        {
            throw new InvalidOperationException(
                $"{adorner.GetType().Name} is already attached to a different AdornerLayer.");
        }

        if (!ReferenceEquals(adorner.AdornedElement, adornedElement))
        {
            throw new InvalidOperationException(
                $"{adorner.GetType().Name} is bound to a different adorned element.");
        }

        if (ContainsAdorner(adorner))
        {
            EnsureTrackingRegistration(adornedElement, adorner);
            return;
        }

        if (!_adornersByElement.TryGetValue(adornedElement, out var list))
        {
            list = new List<Adorner>();
            _adornersByElement[adornedElement] = list;
            SubscribeToAdornedElement(adornedElement);
        }

        if (!list.Contains(adorner))
        {
            list.Add(adorner);
        }

        adorner.Layer = this;
        AddChild(adorner);
        _arrangedRects.Remove(adorner);
        InvalidateArrange();
    }

    public bool RemoveAdorner(Adorner adorner)
    {
        var removed = false;
        if (_adornersByElement.TryGetValue(adorner.AdornedElement, out var list))
        {
            removed = list.Remove(adorner);
            if (list.Count == 0)
            {
                UnsubscribeFromAdornedElement(adorner.AdornedElement);
                _adornersByElement.Remove(adorner.AdornedElement);
            }
        }

        removed |= RemoveChild(adorner);
        _arrangedRects.Remove(adorner);
        adorner.Layer = null;
        return removed;
    }

    public void ClearAdorners(UIElement adornedElement)
    {
        if (!_adornersByElement.TryGetValue(adornedElement, out var list))
        {
            return;
        }

        var snapshot = list.ToArray();
        foreach (var adorner in snapshot)
        {
            RemoveAdorner(adorner);
        }
    }

    public void ClearAllAdorners()
    {
        var snapshot = new List<Adorner>();
        foreach (var child in Children)
        {
            if (child is Adorner adorner)
            {
                snapshot.Add(adorner);
            }
        }

        foreach (var adorner in snapshot)
        {
            RemoveAdorner(adorner);
        }
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

        layer.AddAdorner(adornedElement, adorner);
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
        PruneDetachedAdorners();
        ArrangeAdorners(forceArrange: true);
        return finalSize;
    }

    public override void Update(GameTime gameTime)
    {
        PruneDetachedAdorners();
        ArrangeAdorners(forceArrange: false);
        base.Update(gameTime);
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return clipRect.Width > 0f && clipRect.Height > 0f;
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

    private void EnsureTrackingRegistration(UIElement adornedElement, Adorner adorner)
    {
        if (!_adornersByElement.TryGetValue(adornedElement, out var list))
        {
            list = new List<Adorner>();
            _adornersByElement[adornedElement] = list;
            SubscribeToAdornedElement(adornedElement);
        }

        if (!list.Contains(adorner))
        {
            list.Add(adorner);
        }
    }

    private void PruneDetachedAdorners()
    {
        if (Children.Count == 0)
        {
            return;
        }

        var layerRoot = GetVisualRoot();
        var stale = new List<Adorner>();
        foreach (var child in Children)
        {
            if (child is not Adorner adorner)
            {
                continue;
            }

            if (!ReferenceEquals(layerRoot, adorner.AdornedElement.GetVisualRoot()))
            {
                stale.Add(adorner);
            }
        }

        foreach (var adorner in stale)
        {
            RemoveAdorner(adorner);
        }
    }

    private void ArrangeAdorners(bool forceArrange)
    {
        foreach (var child in Children)
        {
            if (child is not Adorner adorner)
            {
                continue;
            }

            var rect = adorner.GetAdornerLayoutRect();
            if (!forceArrange &&
                _arrangedRects.TryGetValue(adorner, out var previous) &&
                AreRectsEqual(previous, rect))
            {
                continue;
            }

            adorner.Arrange(rect);
            _arrangedRects[adorner] = rect;
        }
    }

    private static bool AreRectsEqual(LayoutRect left, LayoutRect right)
    {
        const float epsilon = 0.0001f;
        return MathF.Abs(left.X - right.X) <= epsilon &&
               MathF.Abs(left.Y - right.Y) <= epsilon &&
               MathF.Abs(left.Width - right.Width) <= epsilon &&
               MathF.Abs(left.Height - right.Height) <= epsilon;
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
