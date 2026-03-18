using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public static partial class XamlLoader
{
    private static object ParseLooseValue(string rawValue)
    {
        if (bool.TryParse(rawValue, out var boolValue))
        {
            return boolValue;
        }

        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return intValue;
        }

        if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
        {
            return floatValue;
        }

        return rawValue;
    }


    private static object ConvertValue(string rawValue, Type targetType)
    {
        var nullableUnderlyingType = Nullable.GetUnderlyingType(targetType);
        if (nullableUnderlyingType != null)
        {
            if (string.Equals(rawValue.Trim(), "null", StringComparison.OrdinalIgnoreCase))
            {
                return null!;
            }

            return ConvertValue(rawValue, nullableUnderlyingType);
        }

        if (targetType == typeof(string))
        {
            return rawValue;
        }

        if (targetType == typeof(object))
        {
            return rawValue;
        }

        if (targetType == typeof(Type))
        {
            return ResolveTypeReference(rawValue);
        }

        if (targetType == typeof(int))
        {
            return int.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(float))
        {
            var trimmed = rawValue.Trim();
            if (string.Equals(trimmed, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                return float.NaN;
            }

            if ((trimmed.Contains(',') || trimmed.Contains(' ')) &&
                TryParseFloatList(trimmed.AsSpan(), out var components) &&
                components.Length > 1)
            {
                var max = 0f;
                for (var i = 0; i < components.Length; i++)
                {
                    if (components[i] > max)
                    {
                        max = components[i];
                    }
                }

                return max;
            }

            return float.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(bool))
        {
            return bool.Parse(rawValue);
        }

        if (targetType == typeof(Color))
        {
            return ParseColor(rawValue);
        }

        if (targetType == typeof(Brush))
        {
            return new SolidColorBrush(ParseColor(rawValue));
        }

        if (targetType == typeof(DoubleCollection))
        {
            return DoubleCollection.Parse(rawValue);
        }

        if (targetType == typeof(Thickness))
        {
            return ParseThickness(rawValue);
        }

        if (targetType == typeof(CornerRadius))
        {
            return ParseCornerRadius(rawValue);
        }

        if (targetType == typeof(GridLength))
        {
            return ParseGridLength(rawValue);
        }

        if (targetType == typeof(ImageSource))
        {
            return ImageSource.FromUri(rawValue);
        }

        if (targetType == typeof(Vector2))
        {
            return GeometryParsers.ParsePoint(rawValue);
        }

        if (targetType == typeof(Geometry))
        {
            return PathGeometry.Parse(rawValue);
        }

        if (targetType == typeof(PathGeometry))
        {
            return PathGeometry.Parse(rawValue);
        }

        if (targetType == typeof(Transform))
        {
            return ParseTransform(rawValue);
        }

        if (targetType == typeof(TimeSpan))
        {
            return TimeSpan.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(DateTime))
        {
            var trimmed = rawValue.Trim();
            if (DateTime.TryParse(
                    trimmed,
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var currentCultureDate))
            {
                return currentCultureDate;
            }

            if (DateTime.TryParse(
                    trimmed,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                    out var invariantDate))
            {
                return invariantDate;
            }

            throw CreateXamlException(
                $"Cannot parse '{rawValue}' as DateTime.",
                code: XamlDiagnosticCode.InvalidValue,
                hint: "Use a valid date value compatible with current or invariant culture.");
        }

        if (targetType == typeof(KeySpline))
        {
            return KeySpline.Parse(rawValue);
        }

        if (targetType == typeof(IEasingFunction))
        {
            return KeySpline.Parse(rawValue);
        }

        if (targetType == typeof(KeyTime))
        {
            var text = rawValue.Trim();
            if (string.Equals(text, "Uniform", StringComparison.OrdinalIgnoreCase))
            {
                return KeyTime.Uniform;
            }

            if (string.Equals(text, "Paced", StringComparison.OrdinalIgnoreCase))
            {
                return KeyTime.Paced;
            }

            return new KeyTime(TimeSpan.Parse(text, CultureInfo.InvariantCulture));
        }

        if (targetType == typeof(Duration))
        {
            var text = rawValue.Trim();
            if (string.Equals(text, "Automatic", StringComparison.OrdinalIgnoreCase))
            {
                return Duration.Automatic;
            }

            if (string.Equals(text, "Forever", StringComparison.OrdinalIgnoreCase))
            {
                return Duration.Forever;
            }

            return new Duration(TimeSpan.Parse(text, CultureInfo.InvariantCulture));
        }

        if (targetType == typeof(RepeatBehavior))
        {
            var text = rawValue.Trim();
            if (string.Equals(text, "Forever", StringComparison.OrdinalIgnoreCase))
            {
                return RepeatBehavior.Forever;
            }

            if (text.EndsWith("x", StringComparison.OrdinalIgnoreCase))
            {
                var count = double.Parse(text[..^1], CultureInfo.InvariantCulture);
                return new RepeatBehavior(count);
            }

            return new RepeatBehavior(TimeSpan.Parse(text, CultureInfo.InvariantCulture));
        }

        if (targetType.IsEnum)
        {
            return ParseEnumValue(rawValue, targetType);
        }

        throw CreateXamlException(
            $"Cannot convert value '{rawValue}' to type '{targetType.Name}'.",
            code: XamlDiagnosticCode.InvalidValue,
            hint: $"Provide a value compatible with '{targetType.Name}'.");
    }


    private static Color BuildColorObject(XElement element)
    {
        if (HasChildElements(element))
        {
            throw CreateXamlException("Color element does not support child elements.", element);
        }

        var text = (element.Value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw CreateXamlException("Color element requires a color value.", element);
        }

        try
        {
            return ParseColor(text);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
        {
            throw CreateXamlException(ex.Message, element, ex);
        }
    }


    private static CornerRadius BuildCornerRadiusObject(XElement element)
    {
        if (HasChildElements(element))
        {
            throw CreateXamlException("CornerRadius element does not support child elements.", element);
        }

        var text = (element.Value ?? string.Empty).Trim();
        var hasAttributes = false;
        var topLeft = 0f;
        var topRight = 0f;
        var bottomRight = 0f;
        var bottomLeft = 0f;

        if (TryGetOptionalAttributeFloatValue(element, nameof(CornerRadius.TopLeft), out var parsedTopLeft))
        {
            hasAttributes = true;
            topLeft = parsedTopLeft;
        }

        if (TryGetOptionalAttributeFloatValue(element, nameof(CornerRadius.TopRight), out var parsedTopRight))
        {
            hasAttributes = true;
            topRight = parsedTopRight;
        }

        if (TryGetOptionalAttributeFloatValue(element, nameof(CornerRadius.BottomRight), out var parsedBottomRight))
        {
            hasAttributes = true;
            bottomRight = parsedBottomRight;
        }

        if (TryGetOptionalAttributeFloatValue(element, nameof(CornerRadius.BottomLeft), out var parsedBottomLeft))
        {
            hasAttributes = true;
            bottomLeft = parsedBottomLeft;
        }

        if (hasAttributes)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                throw CreateXamlException("CornerRadius element cannot mix text content with corner attributes.", element);
            }

            return new CornerRadius(topLeft, topRight, bottomRight, bottomLeft).ClampNonNegative();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return CornerRadius.Empty;
        }

        return ParseCornerRadius(text);
    }


    private static Color ParseColor(string value)
    {
        var trimmed = value.Trim().AsSpan();
        if (!trimmed.IsEmpty && trimmed[0] == '#')
        {
            var hex = trimmed[1..];
            if (hex.Length == 6)
            {
                return new Color(
                    ParseHexByte(hex[0..2]),
                    ParseHexByte(hex[2..4]),
                    ParseHexByte(hex[4..6]));
            }

            if (hex.Length == 8)
            {
                return new Color(
                    ParseHexByte(hex[2..4]),
                    ParseHexByte(hex[4..6]),
                    ParseHexByte(hex[6..8]),
                    ParseHexByte(hex[0..2]));
            }
        }

        var trimmedName = trimmed.ToString();
        if (NamedColors.TryGetValue(trimmedName, out var namedColor))
        {
            return namedColor;
        }

        throw CreateXamlException($"Color value '{value}' is not valid.");
    }


    private static Thickness ParseThickness(string value)
    {
        if (!TryParseFloatList(value.AsSpan(), out var parts))
        {
            throw CreateXamlException("Thickness must be one, two, or four comma-separated values.");
        }

        if (parts.Length == 1)
        {
            return new Thickness(parts[0]);
        }

        if (parts.Length == 2)
        {
            return new Thickness(parts[0], parts[1], parts[0], parts[1]);
        }

        if (parts.Length == 4)
        {
            return new Thickness(parts[0], parts[1], parts[2], parts[3]);
        }

        throw CreateXamlException("Thickness must be one, two, or four comma-separated values.");
    }


    private static CornerRadius ParseCornerRadius(string value)
    {
        if (!TryParseFloatList(value.AsSpan(), out var parts))
        {
            throw CreateXamlException("CornerRadius must be one or four comma-separated values.");
        }

        if (parts.Length == 1)
        {
            return new CornerRadius(parts[0]).ClampNonNegative();
        }

        if (parts.Length == 4)
        {
            return new CornerRadius(parts[0], parts[1], parts[2], parts[3]).ClampNonNegative();
        }

        throw CreateXamlException("CornerRadius must be one or four comma-separated values.");
    }


    private static bool TryGetOptionalAttributeFloatValue(XElement element, string attributeName, out float value)
    {
        var attribute = element.Attribute(attributeName);
        if (attribute == null)
        {
            value = 0f;
            return false;
        }

        value = float.Parse(attribute.Value, CultureInfo.InvariantCulture);
        return true;
    }


    private static GridLength ParseGridLength(string value)
    {
        var trimmed = value.Trim();
        if (string.Equals(trimmed, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return GridLength.Auto;
        }

        if (trimmed.EndsWith('*'))
        {
            var weightText = trimmed[..^1];
            var weight = string.IsNullOrWhiteSpace(weightText)
                ? 1f
                : float.Parse(weightText, CultureInfo.InvariantCulture);
            return new GridLength(weight, GridUnitType.Star);
        }

        return new GridLength(float.Parse(trimmed, CultureInfo.InvariantCulture), GridUnitType.Pixel);
    }


    private static Transform ParseTransform(string rawValue)
    {
        var trimmed = rawValue.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return new MatrixTransform(Matrix.Identity);
        }

        if (trimmed.StartsWith("translate(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")"))
        {
            var args = ParseFloatList(trimmed["translate(".Length..^1]);
            return new TranslateTransform
            {
                X = args.Length > 0 ? args[0] : 0f,
                Y = args.Length > 1 ? args[1] : 0f
            };
        }

        if (trimmed.StartsWith("scale(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")"))
        {
            var args = ParseFloatList(trimmed["scale(".Length..^1]);
            return new ScaleTransform
            {
                ScaleX = args.Length > 0 ? args[0] : 1f,
                ScaleY = args.Length > 1 ? args[1] : (args.Length > 0 ? args[0] : 1f),
                CenterX = args.Length > 2 ? args[2] : 0f,
                CenterY = args.Length > 3 ? args[3] : 0f
            };
        }

        if (trimmed.StartsWith("rotate(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")"))
        {
            var args = ParseFloatList(trimmed["rotate(".Length..^1]);
            return new RotateTransform
            {
                Angle = args.Length > 0 ? args[0] : 0f,
                CenterX = args.Length > 1 ? args[1] : 0f,
                CenterY = args.Length > 2 ? args[2] : 0f
            };
        }

        if (trimmed.StartsWith("skew(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")"))
        {
            var args = ParseFloatList(trimmed["skew(".Length..^1]);
            return new SkewTransform
            {
                AngleX = args.Length > 0 ? args[0] : 0f,
                AngleY = args.Length > 1 ? args[1] : 0f,
                CenterX = args.Length > 2 ? args[2] : 0f,
                CenterY = args.Length > 3 ? args[3] : 0f
            };
        }

        if (trimmed.StartsWith("matrix(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")"))
        {
            var args = ParseFloatList(trimmed["matrix(".Length..^1]);
            if (args.Length != 6)
            {
                throw CreateXamlException("matrix(...) transform requires 6 values.");
            }

            var matrix = new Matrix(
                args[0], args[1], 0f, 0f,
                args[2], args[3], 0f, 0f,
                0f, 0f, 1f, 0f,
                args[4], args[5], 0f, 1f);
            return new MatrixTransform(matrix);
        }

        throw CreateXamlException($"Transform value '{rawValue}' is not valid.");
    }


    private static float[] ParseFloatList(string text)
    {
        if (!TryParseFloatList(text.AsSpan(), out var result))
        {
            throw CreateXamlException($"Value list '{text}' is not valid.");
        }

        return result;
    }


    private static bool TryParseFloatList(string text, out float[] result)
    {
        return TryParseFloatList(text.AsSpan(), out result);
    }


    private static bool TryParseFloatList(ReadOnlySpan<char> text, out float[] result)
    {
        float[]? buffer = null;
        var count = 0;
        var index = 0;
        while (TryReadNextFloatToken(text, ref index, out var token))
        {
            if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                result = Array.Empty<float>();
                return false;
            }

            if (buffer == null)
            {
                buffer = new float[4];
            }
            else if (count == buffer.Length)
            {
                Array.Resize(ref buffer, buffer.Length * 2);
            }

            buffer[count] = parsed;
            count++;
        }

        if (count == 0 || buffer == null)
        {
            result = Array.Empty<float>();
            return false;
        }

        if (count == buffer.Length)
        {
            result = buffer;
            return true;
        }

        result = new float[count];
        Array.Copy(buffer, result, count);
        return true;
    }


    private static bool TryReadNextFloatToken(ReadOnlySpan<char> text, ref int index, out ReadOnlySpan<char> token)
    {
        while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] == ','))
        {
            index++;
        }

        if (index >= text.Length)
        {
            token = default;
            return false;
        }

        var start = index;
        while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] != ',')
        {
            index++;
        }

        token = text[start..index];
        return true;
    }


    private static byte ParseHexByte(ReadOnlySpan<char> hex)
    {
        return byte.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }


    private static IReadOnlyDictionary<string, Color> BuildNamedColorMap()
    {
        var map = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in typeof(Color).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (property.PropertyType == typeof(Color) &&
                property.CanRead &&
                property.GetIndexParameters().Length == 0)
            {
                map[property.Name] = (Color)property.GetValue(null)!;
            }
        }

        return map;
    }


    private static TEnum ParseEnumValue<TEnum>(string rawValue)
        where TEnum : struct, Enum
    {
        return (TEnum)ParseEnumValue(rawValue, typeof(TEnum));
    }


    private static object ParseEnumValue(string rawValue, Type enumType)
    {
        var trimmed = rawValue.Trim();
        var values = EnumValueCache.GetOrAdd(enumType, static type =>
        {
            var names = Enum.GetNames(type);
            var rawValues = Enum.GetValues(type);
            var map = new Dictionary<string, object>(names.Length, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < names.Length; i++)
            {
                map[names[i]] = rawValues.GetValue(i)!;
            }

            return map;
        });

        return values.TryGetValue(trimmed, out var value)
            ? value
            : Enum.Parse(enumType, rawValue, ignoreCase: true);
    }


}
