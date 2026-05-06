using System;
using System.Numerics;
using XnaMatrix = Microsoft.Xna.Framework.Matrix;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

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

        visibleBounds = bounds;
        for (var current = element.VisualParent; current != null; current = current.VisualParent)
        {
            if (!current.TryGetLocalClipSnapshot(out var clipRect) ||
                clipRect.Width <= 0f ||
                clipRect.Height <= 0f)
            {
                continue;
            }

            visibleBounds = IntersectRects(visibleBounds, TransformRectToRoot(current, clipRect));
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

    private static LayoutRect TransformRectToRoot(UIElement element, LayoutRect rect)
    {
        var transform = XnaMatrix.Identity;
        var hasTransform = false;
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (!current.TryGetLocalRenderTransformSnapshot(out var localTransform))
            {
                continue;
            }

            transform *= localTransform;
            hasTransform = true;
        }

        if (!hasTransform)
        {
            return rect;
        }

        var topLeft = XnaVector2.Transform(new XnaVector2(rect.X, rect.Y), transform);
        var topRight = XnaVector2.Transform(new XnaVector2(rect.X + rect.Width, rect.Y), transform);
        var bottomLeft = XnaVector2.Transform(new XnaVector2(rect.X, rect.Y + rect.Height), transform);
        var bottomRight = XnaVector2.Transform(new XnaVector2(rect.X + rect.Width, rect.Y + rect.Height), transform);
        var minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
        var minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
        var maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
        var maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));
        return new LayoutRect(minX, minY, MathF.Max(0f, maxX - minX), MathF.Max(0f, maxY - minY));
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
