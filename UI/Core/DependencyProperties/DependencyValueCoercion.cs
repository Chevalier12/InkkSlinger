using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal static class DependencyValueCoercion
{
    public static bool TryCoerce(object? value, Type targetType, out object? coerced)
    {
        coerced = value;
        if (value == null)
        {
            return true;
        }

        var effectiveTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (effectiveTargetType.IsInstanceOfType(value))
        {
            return true;
        }

        if (effectiveTargetType == typeof(Color) && value is Brush brush)
        {
            coerced = brush.ToColor();
            return true;
        }

        if (effectiveTargetType == typeof(Brush) && value is Color color)
        {
            coerced = new SolidColorBrush(color);
            return true;
        }

        return false;
    }
}
