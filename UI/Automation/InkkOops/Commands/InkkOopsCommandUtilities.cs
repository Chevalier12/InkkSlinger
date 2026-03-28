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
}
