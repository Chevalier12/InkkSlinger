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

        if (effectiveTargetType == typeof(FontFamily) && value is string familyText)
        {
            coerced = new FontFamily(familyText);
            return true;
        }

        if (effectiveTargetType == typeof(float) && value is double doubleValue)
        {
            coerced = (float)doubleValue;
            return true;
        }

        if (effectiveTargetType == typeof(double) && value is float floatValue)
        {
            coerced = (double)floatValue;
            return true;
        }

        if (effectiveTargetType == typeof(double) && value is int intValue)
        {
            coerced = (double)intValue;
            return true;
        }

        if (effectiveTargetType == typeof(float) && value is int intToFloatValue)
        {
            coerced = (float)intToFloatValue;
            return true;
        }

        if (effectiveTargetType == typeof(string) && value is FontFamily fontFamily)
        {
            coerced = fontFamily.Source;
            return true;
        }

        return false;
    }
}
