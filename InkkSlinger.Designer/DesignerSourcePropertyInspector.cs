using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Microsoft.Xna.Framework;
using InkkSlinger;

namespace InkkSlinger.Designer;

internal sealed record DesignerSourceAttributeSpan(
    string Name,
    string Value,
    int TokenStartIndex,
    int TokenLength,
    int ValueStartIndex,
    int ValueLength,
    char QuoteCharacter);

internal sealed record DesignerSourceTagSelection(
    string ElementName,
    int AnchorIndex,
    int TagStartIndex,
    int NameStartIndex,
    int NameLength,
    int AttributeInsertionIndex,
    int TagCloseIndex,
    bool IsSelfClosing,
    IReadOnlyDictionary<string, DesignerSourceAttributeSpan> Attributes)
{
    public bool TryGetAttribute(string propertyName, out DesignerSourceAttributeSpan attribute)
    {
        return Attributes.TryGetValue(propertyName, out attribute!);
    }
}

internal enum DesignerSourcePropertyEditorKind
{
    Text,
    Choice
}

internal sealed record DesignerSourceInspectableProperty(
    string Name,
    string TypeName,
    string DefaultValueDisplay,
    bool IsCurrentlySet,
    DesignerSourcePropertyEditorKind EditorKind,
    IReadOnlyList<string> ChoiceValues);

internal static class DesignerSourcePropertyInspector
{
    private static readonly Dictionary<string, Type?> ControlTypeCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<Type, IReadOnlyList<DesignerSourceInspectableProperty>> PropertyCache = [];
    private static readonly IReadOnlyList<string> BooleanChoiceValues = [bool.FalseString, bool.TrueString];
    private static readonly IReadOnlyList<string> FontWeightChoiceValues =
    [
        "Thin",
        "Light",
        "Normal",
        "Medium",
        "SemiBold",
        "Bold",
        "ExtraBold",
        "Black"
    ];
    private static readonly IReadOnlyList<string> FontStyleChoiceValues =
    [
        "Normal",
        "Italic",
        "Oblique"
    ];

    public static bool TryResolveTagSelection(string? sourceText, int anchorIndex, out DesignerSourceTagSelection selection)
    {
        selection = default!;
        var text = sourceText ?? string.Empty;
        anchorIndex = Math.Clamp(anchorIndex, 0, text.Length);

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '<' || index + 1 >= text.Length)
            {
                continue;
            }

            var next = text[index + 1];
            if (next is '/' or '!' or '?')
            {
                continue;
            }

            if (!TryFindTagClose(text, index, out var closeIndex))
            {
                break;
            }

            if (anchorIndex < index + 1 || anchorIndex > closeIndex)
            {
                index = closeIndex;
                continue;
            }

            if (!TryParseTagSelection(text, anchorIndex, index, closeIndex, out selection))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    public static Type? ResolveControlType(string elementName)
    {
        if (string.IsNullOrWhiteSpace(elementName))
        {
            return null;
        }

        lock (ControlTypeCache)
        {
            if (ControlTypeCache.TryGetValue(elementName, out var cached))
            {
                return cached;
            }

            var resolved = typeof(UIElement).Assembly
                .GetTypes()
                .Where(static type => !type.IsAbstract && typeof(UIElement).IsAssignableFrom(type))
                .FirstOrDefault(type => string.Equals(type.Name, elementName, StringComparison.Ordinal));
            ControlTypeCache[elementName] = resolved;
            return resolved;
        }
    }

    public static IReadOnlyList<DesignerSourceInspectableProperty> GetInspectableProperties(
        Type controlType,
        DesignerSourceTagSelection selection)
    {
        var definitions = GetOrCreatePropertyDefinitions(controlType);
        return definitions
            .Select(
                property => property with
                {
                    IsCurrentlySet = selection.Attributes.ContainsKey(property.Name)
                })
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool TryApplyPropertyEdit(
        string? sourceText,
        DesignerSourceTagSelection selection,
        string propertyName,
        string? propertyValue,
        out string updatedText,
        out int updatedAnchorIndex)
    {
        var text = sourceText ?? string.Empty;
        updatedText = text;
        updatedAnchorIndex = selection.AnchorIndex;

        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        if (selection.TryGetAttribute(propertyName, out var attribute))
        {
            if (string.IsNullOrEmpty(propertyValue))
            {
                updatedText = text.Remove(attribute.TokenStartIndex, attribute.TokenLength);
                updatedAnchorIndex = AdjustAnchorIndex(
                    selection.AnchorIndex,
                    attribute.TokenStartIndex,
                    attribute.TokenLength,
                    0);
                return true;
            }

            var escapedValue = EscapeAttributeValue(propertyValue);
            updatedText = text.Remove(attribute.ValueStartIndex, attribute.ValueLength).Insert(attribute.ValueStartIndex, escapedValue);
            updatedAnchorIndex = AdjustAnchorIndex(
                selection.AnchorIndex,
                attribute.ValueStartIndex,
                attribute.ValueLength,
                escapedValue.Length);
            return true;
        }

        if (string.IsNullOrEmpty(propertyValue))
        {
            return false;
        }

        var insertion = string.Create(
            CultureInfo.InvariantCulture,
            $" {propertyName}=\"{EscapeAttributeValue(propertyValue)}\"");
        updatedText = text.Insert(selection.AttributeInsertionIndex, insertion);
        updatedAnchorIndex = AdjustAnchorIndex(selection.AnchorIndex, selection.AttributeInsertionIndex, 0, insertion.Length);
        return true;
    }

    private static IReadOnlyList<DesignerSourceInspectableProperty> GetOrCreatePropertyDefinitions(Type controlType)
    {
        lock (PropertyCache)
        {
            if (PropertyCache.TryGetValue(controlType, out var cached))
            {
                return cached;
            }

            if (Activator.CreateInstance(controlType) is not UIElement element)
            {
                cached = Array.Empty<DesignerSourceInspectableProperty>();
                PropertyCache[controlType] = cached;
                return cached;
            }

            cached = DependencyProperty
                .GetRegisteredProperties()
                .Where(property => ShouldIncludeInspectableProperty(element, property))
                .GroupBy(property => property.Name, StringComparer.Ordinal)
                .Select(
                    group => group
                        .OrderBy(property => GetOwnerTypeDistance(controlType, property.OwnerType))
                        .ThenBy(property => property.OwnerType.Name, StringComparer.Ordinal)
                        .First())
                .Where(static property => property.Name is not nameof(FrameworkElement.Name) and not nameof(FrameworkElement.Tag))
                .Select(
                    property => new DesignerSourceInspectableProperty(
                        property.Name,
                        property.PropertyType.Name,
                        FormatInspectorValue(element.GetValue(property)),
                        IsCurrentlySet: false,
                        GetEditorKind(property),
                        GetChoiceValues(property)))
                .ToArray();
            PropertyCache[controlType] = cached;
            return cached;
        }
    }

    private static bool ShouldIncludeInspectableProperty(UIElement element, DependencyProperty property)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(property);

        if (!property.IsApplicableTo(element))
        {
            return false;
        }

        if (!property.IsAttached)
        {
            return true;
        }

        return IsDirectTypographySurface(element, property.Name);
    }

    private static bool IsDirectTypographySurface(UIElement element, string propertyName)
    {
        if (element is not Control && element is not TextBlock)
        {
            return false;
        }

        return propertyName is "FontFamily" or "FontSize" or "FontWeight" or "FontStyle";
    }

    private static DesignerSourcePropertyEditorKind GetEditorKind(DependencyProperty property)
    {
        return GetChoiceValues(property).Count > 0
            ? DesignerSourcePropertyEditorKind.Choice
            : DesignerSourcePropertyEditorKind.Text;
    }

    private static IReadOnlyList<string> GetChoiceValues(DependencyProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (property.PropertyType == typeof(bool))
        {
            return BooleanChoiceValues;
        }

        if (property.PropertyType.IsEnum)
        {
            return Enum.GetNames(property.PropertyType);
        }

        if (property.PropertyType == typeof(string))
        {
            if (string.Equals(property.Name, "FontWeight", StringComparison.Ordinal))
            {
                return FontWeightChoiceValues;
            }

            if (string.Equals(property.Name, "FontStyle", StringComparison.Ordinal))
            {
                return FontStyleChoiceValues;
            }
        }

        return Array.Empty<string>();
    }

    private static bool TryParseTagSelection(string text, int anchorIndex, int tagStartIndex, int tagCloseIndex, out DesignerSourceTagSelection selection)
    {
        selection = default!;

        var scanIndex = tagStartIndex + 1;
        while (scanIndex < tagCloseIndex && char.IsWhiteSpace(text[scanIndex]))
        {
            scanIndex++;
        }

        var nameStartIndex = scanIndex;
        while (scanIndex < tagCloseIndex && IsTagNameCharacter(text[scanIndex]))
        {
            scanIndex++;
        }

        var nameLength = scanIndex - nameStartIndex;
        if (nameLength <= 0)
        {
            return false;
        }

        var elementName = text.Substring(nameStartIndex, nameLength);
        var slashIndex = tagCloseIndex;
        while (slashIndex > nameStartIndex && char.IsWhiteSpace(text[slashIndex - 1]))
        {
            slashIndex--;
        }

        var isSelfClosing = slashIndex > nameStartIndex && text[slashIndex - 1] == '/';
        var attributeScanEnd = isSelfClosing ? slashIndex - 1 : tagCloseIndex;
        var attributes = new Dictionary<string, DesignerSourceAttributeSpan>(StringComparer.Ordinal);
        while (scanIndex < attributeScanEnd)
        {
            var tokenStartIndex = scanIndex;
            while (scanIndex < attributeScanEnd && char.IsWhiteSpace(text[scanIndex]))
            {
                scanIndex++;
            }

            tokenStartIndex = Math.Min(tokenStartIndex, scanIndex);
            if (scanIndex >= attributeScanEnd)
            {
                break;
            }

            var attributeNameStart = scanIndex;
            while (scanIndex < attributeScanEnd && IsAttributeNameCharacter(text[scanIndex]))
            {
                scanIndex++;
            }

            if (scanIndex == attributeNameStart)
            {
                return false;
            }

            var attributeName = text.Substring(attributeNameStart, scanIndex - attributeNameStart);
            while (scanIndex < attributeScanEnd && char.IsWhiteSpace(text[scanIndex]))
            {
                scanIndex++;
            }

            if (scanIndex >= attributeScanEnd || text[scanIndex] != '=')
            {
                return false;
            }

            scanIndex++;
            while (scanIndex < attributeScanEnd && char.IsWhiteSpace(text[scanIndex]))
            {
                scanIndex++;
            }

            if (scanIndex >= attributeScanEnd)
            {
                return false;
            }

            var quoteCharacter = text[scanIndex];
            if (quoteCharacter != '\'' && quoteCharacter != '"')
            {
                return false;
            }

            scanIndex++;
            var valueStartIndex = scanIndex;
            while (scanIndex < attributeScanEnd && text[scanIndex] != quoteCharacter)
            {
                scanIndex++;
            }

            if (scanIndex >= attributeScanEnd)
            {
                return false;
            }

            var valueLength = scanIndex - valueStartIndex;
            var decodedValue = WebUtility.HtmlDecode(text.Substring(valueStartIndex, valueLength));
            scanIndex++;

            attributes[attributeName] = new DesignerSourceAttributeSpan(
                attributeName,
                decodedValue,
                tokenStartIndex,
                scanIndex - tokenStartIndex,
                valueStartIndex,
                valueLength,
                quoteCharacter);
        }

        selection = new DesignerSourceTagSelection(
            elementName,
            anchorIndex,
            tagStartIndex,
            nameStartIndex,
            nameLength,
            isSelfClosing ? slashIndex - 1 : tagCloseIndex,
            tagCloseIndex,
            isSelfClosing,
            attributes);
        return true;
    }

    private static bool TryFindTagClose(string text, int tagStartIndex, out int closeIndex)
    {
        closeIndex = -1;
        char quoteCharacter = '\0';
        for (var index = tagStartIndex + 1; index < text.Length; index++)
        {
            var current = text[index];
            if (quoteCharacter != '\0')
            {
                if (current == quoteCharacter)
                {
                    quoteCharacter = '\0';
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quoteCharacter = current;
                continue;
            }

            if (current == '>')
            {
                closeIndex = index;
                return true;
            }
        }

        return false;
    }

    private static bool IsTagNameCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '_' or '.' or ':';
    }

    private static bool IsAttributeNameCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '_' or '.' or ':';
    }

    private static int AdjustAnchorIndex(int anchorIndex, int operationStart, int removedLength, int insertedLength)
    {
        var operationEnd = operationStart + removedLength;
        if (anchorIndex <= operationStart)
        {
            return anchorIndex;
        }

        if (anchorIndex >= operationEnd)
        {
            return anchorIndex + insertedLength - removedLength;
        }

        return operationStart + insertedLength;
    }

    private static int GetOwnerTypeDistance(Type currentType, Type ownerType)
    {
        var distance = 0;
        for (var type = currentType; type != null; type = type.BaseType)
        {
            if (type == ownerType)
            {
                return distance;
            }

            distance++;
        }

        return int.MaxValue;
    }

    private static string EscapeAttributeValue(string value)
    {
        return SecurityElementEscape(value) ?? string.Empty;
    }

    private static string? SecurityElementEscape(string? value)
    {
        return System.Security.SecurityElement.Escape(value);
    }

    private static string FormatInspectorValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            bool boolean => boolean ? "True" : "False",
            float number => number.ToString("0.##", CultureInfo.InvariantCulture),
            double doubleNumber => doubleNumber.ToString("0.##", CultureInfo.InvariantCulture),
            Thickness thickness => FormatThickness(thickness),
            CornerRadius radius => FormatCornerRadius(radius),
            Color color => FormatColor(color),
            SolidColorBrush solidColorBrush => FormatColor(solidColorBrush.Color),
            Brush brush => FormatColor(brush.ToColor()),
            Enum enumValue => enumValue.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? string.Empty
        };
    }

    private static string FormatThickness(Thickness thickness)
    {
        if (AreClose(thickness.Left, thickness.Top) &&
            AreClose(thickness.Left, thickness.Right) &&
            AreClose(thickness.Left, thickness.Bottom))
        {
            return thickness.Left.ToString("0.##", CultureInfo.InvariantCulture);
        }

        if (AreClose(thickness.Left, thickness.Right) && AreClose(thickness.Top, thickness.Bottom))
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{thickness.Left:0.##},{thickness.Top:0.##}");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{thickness.Left:0.##},{thickness.Top:0.##},{thickness.Right:0.##},{thickness.Bottom:0.##}");
    }

    private static string FormatCornerRadius(CornerRadius radius)
    {
        if (AreClose(radius.TopLeft, radius.TopRight) &&
            AreClose(radius.TopLeft, radius.BottomRight) &&
            AreClose(radius.TopLeft, radius.BottomLeft))
        {
            return radius.TopLeft.ToString("0.##", CultureInfo.InvariantCulture);
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{radius.TopLeft:0.##},{radius.TopRight:0.##},{radius.BottomRight:0.##},{radius.BottomLeft:0.##}");
    }

    private static string FormatColor(Color color)
    {
        return color.A == byte.MaxValue
            ? string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}")
            : string.Create(CultureInfo.InvariantCulture, $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    private static bool AreClose(float left, float right)
    {
        return Math.Abs(left - right) <= 0.001f;
    }
}