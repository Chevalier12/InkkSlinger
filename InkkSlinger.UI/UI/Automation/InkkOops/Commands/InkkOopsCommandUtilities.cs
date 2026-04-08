using System;
using System.Numerics;

namespace InkkSlinger;

internal static class InkkOopsCommandUtilities
{
    public static Vector2 GetCenterPoint(UIElement element)
    {
        if (element.TryGetRenderBoundsInRootSpace(out var bounds))
        {
            return GetAnchorPoint(bounds, InkkOopsPointerAnchor.Center);
        }

        var slot = element.LayoutSlot;
        return new Vector2(slot.X + (slot.Width * 0.5f), slot.Y + (slot.Height * 0.5f));
    }

    public static Vector2 GetAnchorPoint(LayoutRect bounds, InkkOopsPointerAnchor anchor)
    {
        return anchor.Kind switch
        {
            InkkOopsPointerAnchorKind.TopLeft => new Vector2(bounds.X, bounds.Y),
            InkkOopsPointerAnchorKind.TopRight => new Vector2(bounds.X + bounds.Width, bounds.Y),
            InkkOopsPointerAnchorKind.BottomLeft => new Vector2(bounds.X, bounds.Y + bounds.Height),
            InkkOopsPointerAnchorKind.BottomRight => new Vector2(bounds.X + bounds.Width, bounds.Y + bounds.Height),
            InkkOopsPointerAnchorKind.Offset => new Vector2(bounds.X + anchor.Offset.X, bounds.Y + anchor.Offset.Y),
            _ => new Vector2(bounds.X + (bounds.Width * 0.5f), bounds.Y + (bounds.Height * 0.5f))
        };
    }

    public static Vector2 GetPreferredActionPoint(UIElement element, LayoutRect bounds, LayoutRect viewport, InkkOopsPointerAnchor anchor)
    {
        if (anchor.Kind != InkkOopsPointerAnchorKind.Offset &&
            TryGetVisibleBoundsForInput(element, viewport, out var visibleBounds))
        {
            return GetAnchorPoint(visibleBounds, anchor);
        }

        return GetAnchorPoint(bounds, anchor);
    }

    public static bool TryGetVisibleBoundsForInput(UIElement element, LayoutRect viewport, out LayoutRect visibleBounds)
    {
        if (!element.TryGetRenderBoundsInRootSpace(out var bounds) || bounds.Width <= 0f || bounds.Height <= 0f)
        {
            visibleBounds = default;
            return false;
        }

        visibleBounds = GetRenderedLayoutRectForInput(element);
        for (var current = element.VisualParent; current != null; current = current.VisualParent)
        {
            visibleBounds = IntersectRects(visibleBounds, GetRenderedLayoutRectForInput(current));
            if (visibleBounds.Width <= 0f || visibleBounds.Height <= 0f)
            {
                return false;
            }
        }

        visibleBounds = IntersectRects(visibleBounds, viewport);
        return visibleBounds.Width > 0f && visibleBounds.Height > 0f;
    }

    public static bool Contains(LayoutRect rect, Vector2 point)
    {
        return point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }

    public static object? ReadPropertyValue(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName);
        return property?.CanRead == true ? property.GetValue(target) : null;
    }

    public static string FormatObject(object? value)
    {
        return value switch
        {
            null => "<null>",
            float floatValue => floatValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static LayoutRect GetRenderedLayoutRectForInput(UIElement element)
    {
        var slot = element.LayoutSlot;
        var x = slot.X;
        var y = slot.Y;
        for (var current = element.VisualParent; current != null; current = current.VisualParent)
        {
            if (current is ScrollViewer viewer)
            {
                x -= viewer.HorizontalOffset;
                y -= viewer.VerticalOffset;
            }
        }

        return new LayoutRect(x, y, slot.Width, slot.Height);
    }

    private static LayoutRect IntersectRects(LayoutRect first, LayoutRect second)
    {
        var left = MathF.Max(first.X, second.X);
        var top = MathF.Max(first.Y, second.Y);
        var right = MathF.Min(first.X + first.Width, second.X + second.Width);
        var bottom = MathF.Min(first.Y + first.Height, second.Y + second.Height);
        if (right <= left || bottom <= top)
        {
            return new LayoutRect(left, top, 0f, 0f);
        }

        return new LayoutRect(left, top, right - left, bottom - top);
    }
}
