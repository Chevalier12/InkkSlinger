using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
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

internal sealed record DesignerSourcePropertyProjection(
    Type TargetType,
    string PropertyAttributeName,
    string ValueAttributeName);

internal sealed record DesignerSourceTagSelection(
    string ElementName,
    int AnchorIndex,
    int TagStartIndex,
    int NameStartIndex,
    int NameLength,
    int AttributeInsertionIndex,
    int TagCloseIndex,
    bool IsSelfClosing,
    IReadOnlyDictionary<string, DesignerSourceAttributeSpan> Attributes,
    DesignerSourcePropertyProjection? PropertyProjection = null)
{
    public bool TryGetAttribute(string propertyName, out DesignerSourceAttributeSpan attribute)
    {
        return Attributes.TryGetValue(propertyName, out attribute!);
    }

    public bool IsProjectedPropertySelection => PropertyProjection != null;

    public bool IsPropertySet(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (PropertyProjection == null)
        {
            return Attributes.ContainsKey(propertyName);
        }

        return TryGetAttribute(PropertyProjection.PropertyAttributeName, out var projectedProperty) &&
               string.Equals(projectedProperty.Value, propertyName, StringComparison.Ordinal);
    }

    public bool TryGetEditablePropertyValue(string propertyName, out string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (PropertyProjection == null)
        {
            if (TryGetAttribute(propertyName, out var attribute))
            {
                value = attribute.Value;
                return true;
            }

            value = string.Empty;
            return false;
        }

        if (TryGetAttribute(PropertyProjection.PropertyAttributeName, out var projectedProperty) &&
            string.Equals(projectedProperty.Value, propertyName, StringComparison.Ordinal) &&
            TryGetAttribute(PropertyProjection.ValueAttributeName, out var projectedValue))
        {
            value = projectedValue.Value;
            return true;
        }

        value = string.Empty;
        return false;
    }
}

internal enum DesignerSourcePropertyEditorKind
{
    Text,
    Choice,
    Color,
    Composite
}

internal enum DesignerSourceCompositeValueKind
{
    None,
    Thickness,
    CornerRadius
}

internal sealed record DesignerSourceInspectableProperty(
    string Name,
    string TypeName,
    string DefaultValueDisplay,
    bool IsCurrentlySet,
    DesignerSourcePropertyEditorKind EditorKind,
    IReadOnlyList<string> ChoiceValues,
    DesignerSourceCompositeValueKind CompositeValueKind);

internal sealed record DesignerSourcePropertyDefinition(
    string Name,
    string TypeName,
    string DefaultValueDisplay,
    DesignerSourcePropertyEditorKind EditorKind,
    IReadOnlyList<string> ChoiceValues,
    DesignerSourceCompositeValueKind CompositeValueKind);

internal static class DesignerSourcePropertyInspector
{
    private static readonly Dictionary<string, Type> ControlTypeCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<Type, IReadOnlyList<DesignerSourcePropertyDefinition>> PropertyCache = [];
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
    private static readonly IReadOnlyList<string> CursorChoiceValues =
    [
        "Arrow",
        "Hand",
        "IBeam",
        "Cross",
        "Help",
        "Wait",
        "AppStarting",
        "No",
        "SizeAll",
        "SizeNESW",
        "SizeNS",
        "SizeNWSE",
        "SizeWE",
        "UpArrow"
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

            selection = EnrichTagSelection(text, selection);

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
        }

        var resolved = XamlLoader.GetKnownTypes()
            .Where(typeInfo => string.Equals(typeInfo.Name, elementName, StringComparison.Ordinal))
            .Select(static typeInfo => typeInfo.Type)
            .FirstOrDefault(IsInspectableType);
        if (resolved != null)
        {
            lock (ControlTypeCache)
            {
                ControlTypeCache[elementName] = resolved;
            }
        }

        return resolved;
    }

    public static Type? ResolveInspectableType(DesignerSourceTagSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return selection.PropertyProjection?.TargetType ?? ResolveControlType(selection.ElementName);
    }

    public static IReadOnlyList<DesignerSourceInspectableProperty> GetInspectableProperties(
        Type controlType,
        DesignerSourceTagSelection selection)
    {
        var definitions = GetOrCreatePropertyDefinitions(controlType);
        return definitions
            .Select(
                property => new DesignerSourceInspectableProperty(
                    property.Name,
                    property.TypeName,
                    property.DefaultValueDisplay,
                    selection.IsPropertySet(property.Name),
                    property.EditorKind,
                    property.ChoiceValues,
                    property.CompositeValueKind))
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

        if (selection.PropertyProjection != null)
        {
            return TryApplyProjectedPropertyEdit(text, selection, propertyName, propertyValue, out updatedText, out updatedAnchorIndex);
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

    private static DesignerSourceTagSelection EnrichTagSelection(string sourceText, DesignerSourceTagSelection selection)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(selection);

        if (!string.Equals(selection.ElementName, nameof(Setter), StringComparison.Ordinal))
        {
            return selection;
        }

        var projectedTargetType = ResolveSetterTargetType(sourceText, selection);
        if (projectedTargetType == null)
        {
            return selection;
        }

        return selection with
        {
            PropertyProjection = new DesignerSourcePropertyProjection(
                projectedTargetType,
                nameof(Setter.Property),
                nameof(Setter.Value))
        };
    }

    private static Type? ResolveSetterTargetType(string sourceText, DesignerSourceTagSelection selection)
    {
        var ancestors = GetOpenAncestorSelections(sourceText, selection.TagStartIndex);
        for (var index = ancestors.Count - 1; index >= 0; index--)
        {
            var ancestor = ancestors[index];
            if (string.Equals(ancestor.ElementName, nameof(Style), StringComparison.Ordinal) &&
                ancestor.TryGetAttribute(nameof(Style.TargetType), out var targetTypeAttribute))
            {
                var styleTargetType = ResolveStyleTargetType(targetTypeAttribute.Value);
                if (styleTargetType != null && IsInspectableType(styleTargetType))
                {
                    return styleTargetType;
                }
            }
        }

        for (var index = ancestors.Count - 1; index >= 0; index--)
        {
            var ancestor = ancestors[index];
            if (!TryParsePropertyElementName(ancestor.ElementName, out var ownerTypeName, out var propertyElementName))
            {
                continue;
            }

            if (!string.Equals(propertyElementName, nameof(FrameworkElement.Style), StringComparison.Ordinal) &&
                !string.Equals(propertyElementName, "Setters", StringComparison.Ordinal))
            {
                continue;
            }

            var ownerType = ResolveControlType(ownerTypeName);
            if (ownerType != null)
            {
                return ownerType;
            }
        }

        return null;
    }

    private static List<DesignerSourceTagSelection> GetOpenAncestorSelections(string text, int descendantTagStartIndex)
    {
        var ancestors = new List<DesignerSourceTagSelection>();
        for (var index = 0; index < descendantTagStartIndex; index++)
        {
            if (text[index] != '<' || index + 1 >= text.Length)
            {
                continue;
            }

            var next = text[index + 1];
            if (next is '!' or '?')
            {
                if (!TryFindTagClose(text, index, out var skippedCloseIndex))
                {
                    break;
                }

                index = skippedCloseIndex;
                continue;
            }

            if (!TryFindTagClose(text, index, out var closeIndex) || closeIndex >= descendantTagStartIndex)
            {
                break;
            }

            if (next == '/')
            {
                var closingName = ReadTagName(text, index + 2, closeIndex);
                if (!string.IsNullOrEmpty(closingName))
                {
                    for (var stackIndex = ancestors.Count - 1; stackIndex >= 0; stackIndex--)
                    {
                        if (string.Equals(ancestors[stackIndex].ElementName, closingName, StringComparison.Ordinal))
                        {
                            ancestors.RemoveRange(stackIndex, ancestors.Count - stackIndex);
                            break;
                        }
                    }
                }

                index = closeIndex;
                continue;
            }

            if (!TryParseTagSelection(text, index + 1, index, closeIndex, out var selection))
            {
                index = closeIndex;
                continue;
            }

            if (!selection.IsSelfClosing)
            {
                ancestors.Add(selection);
            }

            index = closeIndex;
        }

        return ancestors;
    }

    private static string ReadTagName(string text, int startIndex, int endIndex)
    {
        var index = startIndex;
        while (index < endIndex && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        var nameStart = index;
        while (index < endIndex && IsTagNameCharacter(text[index]))
        {
            index++;
        }

        return index > nameStart ? text.Substring(nameStart, index - nameStart) : string.Empty;
    }

    private static bool TryParsePropertyElementName(string elementName, out string ownerTypeName, out string propertyName)
    {
        ownerTypeName = string.Empty;
        propertyName = string.Empty;
        var separatorIndex = elementName.IndexOf('.');
        if (separatorIndex <= 0 || separatorIndex >= elementName.Length - 1)
        {
            return false;
        }

        ownerTypeName = elementName[..separatorIndex];
        propertyName = elementName[(separatorIndex + 1)..];
        return true;
    }

    private static Type? ResolveStyleTargetType(string targetTypeText)
    {
        var trimmed = targetTypeText.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            var markupBody = trimmed[1..^1].Trim();
            if (markupBody.StartsWith("x:Type", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = markupBody["x:Type".Length..].Trim();
            }
        }

        if (trimmed.StartsWith("x:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        return ResolveControlType(trimmed);
    }

    private static bool TryApplyProjectedPropertyEdit(
        string text,
        DesignerSourceTagSelection selection,
        string propertyName,
        string? propertyValue,
        out string updatedText,
        out int updatedAnchorIndex)
    {
        updatedText = text;
        updatedAnchorIndex = selection.AnchorIndex;

        var projection = selection.PropertyProjection!;
        selection.TryGetAttribute(projection.PropertyAttributeName, out var projectedPropertyAttribute);
        selection.TryGetAttribute(projection.ValueAttributeName, out var projectedValueAttribute);
        var currentlySelectedProperty = projectedPropertyAttribute?.Value;

        if (string.IsNullOrEmpty(propertyValue))
        {
            if (!string.Equals(currentlySelectedProperty, propertyName, StringComparison.Ordinal))
            {
                return false;
            }

            var spansToRemove = new List<DesignerSourceAttributeSpan>();
            if (projectedPropertyAttribute != null)
            {
                spansToRemove.Add(projectedPropertyAttribute);
            }

            if (projectedValueAttribute != null)
            {
                spansToRemove.Add(projectedValueAttribute);
            }

            if (spansToRemove.Count == 0)
            {
                return false;
            }

            spansToRemove.Sort(static (left, right) => right.TokenStartIndex.CompareTo(left.TokenStartIndex));
            foreach (var span in spansToRemove)
            {
                updatedText = updatedText.Remove(span.TokenStartIndex, span.TokenLength);
                updatedAnchorIndex = AdjustAnchorIndex(updatedAnchorIndex, span.TokenStartIndex, span.TokenLength, 0);
            }

            return true;
        }

        var escapedValue = EscapeAttributeValue(propertyValue);

        var replacements = new List<(int Start, int Length, string Replacement)>();
        if (projectedPropertyAttribute != null &&
            !string.Equals(projectedPropertyAttribute.Value, propertyName, StringComparison.Ordinal))
        {
            replacements.Add((projectedPropertyAttribute.ValueStartIndex, projectedPropertyAttribute.ValueLength, propertyName));
        }

        if (projectedValueAttribute != null)
        {
            replacements.Add((projectedValueAttribute.ValueStartIndex, projectedValueAttribute.ValueLength, escapedValue));
        }

        replacements.Sort(static (left, right) => right.Start.CompareTo(left.Start));
        foreach (var replacement in replacements)
        {
            updatedText = updatedText.Remove(replacement.Start, replacement.Length)
                .Insert(replacement.Start, replacement.Replacement);
            updatedAnchorIndex = AdjustAnchorIndex(
                updatedAnchorIndex,
                replacement.Start,
                replacement.Length,
                replacement.Replacement.Length);
        }

        if (projectedPropertyAttribute == null)
        {
            var insertion = string.Create(CultureInfo.InvariantCulture, $" {projection.PropertyAttributeName}=\"{propertyName}\"");
            updatedText = updatedText.Insert(selection.AttributeInsertionIndex, insertion);
            updatedAnchorIndex = AdjustAnchorIndex(updatedAnchorIndex, selection.AttributeInsertionIndex, 0, insertion.Length);
        }

        if (projectedValueAttribute == null)
        {
            var valueInsertionIndex = FindTagAttributeInsertionIndex(updatedText, selection.TagStartIndex);

            var insertion = string.Create(CultureInfo.InvariantCulture, $" {projection.ValueAttributeName}=\"{escapedValue}\"");
            updatedText = updatedText.Insert(valueInsertionIndex, insertion);
            updatedAnchorIndex = AdjustAnchorIndex(updatedAnchorIndex, valueInsertionIndex, 0, insertion.Length);
        }

        if (selection.IsSelfClosing)
        {
            NormalizeSelfClosingTagWhitespace(ref updatedText, ref updatedAnchorIndex, selection.TagStartIndex, selection.NameStartIndex + selection.NameLength);
        }

        return true;
    }

    private static int FindTagAttributeInsertionIndex(string text, int tagStartIndex)
    {
        if (!TryFindTagClose(text, tagStartIndex, out var closeIndex))
        {
            return text.Length;
        }

        var insertionIndex = closeIndex;
        while (insertionIndex > tagStartIndex && char.IsWhiteSpace(text[insertionIndex - 1]))
        {
            insertionIndex--;
        }

        if (insertionIndex > tagStartIndex && text[insertionIndex - 1] == '/')
        {
            insertionIndex--;
            while (insertionIndex > tagStartIndex && char.IsWhiteSpace(text[insertionIndex - 1]))
            {
                insertionIndex--;
            }
        }

        return insertionIndex;
    }

    private static void NormalizeSelfClosingTagWhitespace(ref string text, ref int anchorIndex, int tagStartIndex, int nameEndIndex)
    {
        if (!TryFindTagClose(text, tagStartIndex, out var closeIndex))
        {
            return;
        }

        var firstContentIndex = nameEndIndex;
        while (firstContentIndex < closeIndex && char.IsWhiteSpace(text[firstContentIndex]))
        {
            firstContentIndex++;
        }

        if (firstContentIndex < closeIndex && text[firstContentIndex] != '/' && firstContentIndex > nameEndIndex + 1)
        {
            var removalStart = nameEndIndex + 1;
            var removalLength = firstContentIndex - removalStart;
            text = text.Remove(removalStart, removalLength);
            anchorIndex = AdjustAnchorIndex(anchorIndex, removalStart, removalLength, 0);
            closeIndex -= removalLength;
            firstContentIndex = removalStart;
        }

        var slashIndex = closeIndex - 1;
        while (slashIndex > tagStartIndex && char.IsWhiteSpace(text[slashIndex]))
        {
            slashIndex--;
        }

        if (slashIndex <= tagStartIndex || text[slashIndex] != '/')
        {
            return;
        }

        var slashWhitespaceStart = slashIndex;
        while (slashWhitespaceStart > tagStartIndex && char.IsWhiteSpace(text[slashWhitespaceStart - 1]))
        {
            slashWhitespaceStart--;
        }

        var whitespaceLength = slashIndex - slashWhitespaceStart;
        if (whitespaceLength == 1)
        {
            return;
        }

        if (whitespaceLength == 0)
        {
            text = text.Insert(slashIndex, " ");
            anchorIndex = AdjustAnchorIndex(anchorIndex, slashIndex, 0, 1);
            return;
        }

        var replacementStart = slashWhitespaceStart + 1;
        var replacementLength = slashIndex - replacementStart;
        text = text.Remove(replacementStart, replacementLength);
        anchorIndex = AdjustAnchorIndex(anchorIndex, replacementStart, replacementLength, 0);
    }

    private static IReadOnlyList<DesignerSourcePropertyDefinition> GetOrCreatePropertyDefinitions(Type controlType)
    {
        lock (PropertyCache)
        {
            if (PropertyCache.TryGetValue(controlType, out var cached))
            {
                return cached;
            }

            if (!TryCreateInspectableInstance(controlType, out var instance) || instance == null)
            {
                cached = Array.Empty<DesignerSourcePropertyDefinition>();
                PropertyCache[controlType] = cached;
                return cached;
            }

            cached = instance is UIElement element
                ? CreateDependencyPropertyDefinitions(controlType, element)
                : CreateClrPropertyDefinitions(controlType, instance);
            PropertyCache[controlType] = cached;
            return cached;
        }
    }

    private static bool TryCreateInspectableInstance(Type controlType, out object? instance)
    {
        try
        {
            instance = Activator.CreateInstance(controlType);
            return instance != null;
        }
        catch
        {
            instance = null;
            return false;
        }
    }

    private static IReadOnlyList<DesignerSourcePropertyDefinition> CreateDependencyPropertyDefinitions(Type controlType, UIElement element)
    {
        return DependencyProperty
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
                property => new DesignerSourcePropertyDefinition(
                    property.Name,
                    property.PropertyType.Name,
                    FormatInspectorValue(element.GetValue(property)),
                    GetEditorKind(property.PropertyType, property.Name),
                    GetChoiceValues(property.PropertyType, property.Name),
                    GetCompositeValueKind(property.PropertyType)))
            .ToArray();
    }

    private static IReadOnlyList<DesignerSourcePropertyDefinition> CreateClrPropertyDefinitions(Type controlType, object instance)
    {
        return controlType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(ShouldIncludeInspectableClrProperty)
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .Select(
                group => group
                    .OrderBy(property => GetOwnerTypeDistance(controlType, property.DeclaringType))
                    .ThenBy(property => property.DeclaringType?.Name, StringComparer.Ordinal)
                    .First())
            .Select(
                property => new DesignerSourcePropertyDefinition(
                    property.Name,
                    property.PropertyType.Name,
                    FormatInspectorValue(GetClrDefaultValue(instance, property)),
                    GetEditorKind(property.PropertyType, property.Name),
                    GetChoiceValues(property.PropertyType, property.Name),
                    GetCompositeValueKind(property.PropertyType)))
            .ToArray();
    }

    private static bool ShouldIncludeInspectableClrProperty(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (property.GetIndexParameters().Length != 0 ||
            property.GetMethod?.IsPublic != true ||
            property.SetMethod?.IsPublic != true)
        {
            return false;
        }

        return IsInspectablePropertyType(property.PropertyType);
    }

    private static bool IsInspectablePropertyType(Type propertyType)
    {
        ArgumentNullException.ThrowIfNull(propertyType);

        if (propertyType == typeof(string) ||
            propertyType.IsEnum ||
            propertyType.IsValueType ||
            typeof(Brush).IsAssignableFrom(propertyType))
        {
            return true;
        }

        if (propertyType == typeof(object) ||
            propertyType == typeof(Type) ||
            typeof(IEnumerable).IsAssignableFrom(propertyType))
        {
            return false;
        }

        return false;
    }

    private static object? GetClrDefaultValue(object instance, PropertyInfo property)
    {
        try
        {
            return property.GetValue(instance);
        }
        catch
        {
            return null;
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

    private static DesignerSourcePropertyEditorKind GetEditorKind(Type propertyType, string propertyName)
    {
        if (GetCompositeValueKind(propertyType) != DesignerSourceCompositeValueKind.None)
        {
            return DesignerSourcePropertyEditorKind.Composite;
        }

        if (IsColorLikeProperty(propertyType))
        {
            return DesignerSourcePropertyEditorKind.Color;
        }

        return GetChoiceValues(propertyType, propertyName).Count > 0
            ? DesignerSourcePropertyEditorKind.Choice
            : DesignerSourcePropertyEditorKind.Text;
    }

    internal static IReadOnlyList<string> ExpandCompositeEditorValues(
        DesignerSourceCompositeValueKind compositeValueKind,
        string? currentValue,
        string defaultValueDisplay)
    {
        var primaryExpansion = TryExpandCompositeEditorValues(compositeValueKind, currentValue);
        if (primaryExpansion != null)
        {
            return primaryExpansion;
        }

        var defaultExpansion = TryExpandCompositeEditorValues(compositeValueKind, defaultValueDisplay);
        if (defaultExpansion != null)
        {
            return defaultExpansion;
        }

        return [string.Empty, string.Empty, string.Empty, string.Empty];
    }

    internal static bool TryParseColorValue(string? value, out Color color)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            color = default;
            return false;
        }

        if (trimmed[0] == '#')
        {
            var hex = trimmed.AsSpan(1);
            if (hex.Length == 6)
            {
                color = new Color(
                    ParseHexByte(hex[0..2]),
                    ParseHexByte(hex[2..4]),
                    ParseHexByte(hex[4..6]));
                return true;
            }

            if (hex.Length == 8)
            {
                color = new Color(
                    ParseHexByte(hex[2..4]),
                    ParseHexByte(hex[4..6]),
                    ParseHexByte(hex[6..8]),
                    ParseHexByte(hex[0..2]));
                return true;
            }
        }

        var namedColorProperty = typeof(Color).GetProperty(
            trimmed,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
        if (namedColorProperty?.PropertyType == typeof(Color) &&
            namedColorProperty.GetIndexParameters().Length == 0 &&
            namedColorProperty.GetValue(null) is Color namedColor)
        {
            color = namedColor;
            return true;
        }

        color = default;
        return false;
    }

    internal static string FormatColorValue(Color color)
    {
        return color.A == byte.MaxValue
            ? string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}")
            : string.Create(CultureInfo.InvariantCulture, $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    private static IReadOnlyList<string> GetChoiceValues(Type propertyType, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (propertyType == typeof(bool))
        {
            return BooleanChoiceValues;
        }

        if (propertyType.IsEnum)
        {
            return Enum.GetNames(propertyType);
        }

        if (propertyType == typeof(string))
        {
            if (string.Equals(propertyName, nameof(FrameworkElement.Cursor), StringComparison.Ordinal))
            {
                return CursorChoiceValues;
            }

            if (string.Equals(propertyName, "FontWeight", StringComparison.Ordinal))
            {
                return FontWeightChoiceValues;
            }

            if (string.Equals(propertyName, "FontStyle", StringComparison.Ordinal))
            {
                return FontStyleChoiceValues;
            }
        }

        return Array.Empty<string>();
    }

    private static DesignerSourceCompositeValueKind GetCompositeValueKind(Type propertyType)
    {
        ArgumentNullException.ThrowIfNull(propertyType);

        if (propertyType == typeof(Thickness))
        {
            return DesignerSourceCompositeValueKind.Thickness;
        }

        if (propertyType == typeof(CornerRadius))
        {
            return DesignerSourceCompositeValueKind.CornerRadius;
        }

        return DesignerSourceCompositeValueKind.None;
    }

    private static bool IsColorLikeProperty(Type propertyType)
    {
        ArgumentNullException.ThrowIfNull(propertyType);

        return propertyType == typeof(Color) ||
               typeof(Brush).IsAssignableFrom(propertyType);
    }

    private static string[]? TryExpandCompositeEditorValues(DesignerSourceCompositeValueKind compositeValueKind, string? value)
    {
        if (compositeValueKind == DesignerSourceCompositeValueKind.None || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value
            .Split(',', StringSplitOptions.TrimEntries)
            .Where(static part => part.Length > 0)
            .ToArray();

        if (parts.Length == 1)
        {
            return [parts[0], parts[0], parts[0], parts[0]];
        }

        if (compositeValueKind == DesignerSourceCompositeValueKind.Thickness && parts.Length == 2)
        {
            return [parts[0], parts[1], parts[0], parts[1]];
        }

        if (parts.Length == 4)
        {
            return parts;
        }

        return null;
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

    private static int GetOwnerTypeDistance(Type currentType, Type? ownerType)
    {
        if (ownerType == null)
        {
            return int.MaxValue;
        }

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
            GridLength gridLength => FormatGridLength(gridLength),
            Thickness thickness => FormatThickness(thickness),
            CornerRadius radius => FormatCornerRadius(radius),
            Vector2 vector => FormatVector2(vector),
            Color color => FormatColor(color),
            SolidColorBrush solidColorBrush => FormatColor(solidColorBrush.Color),
            Brush brush => FormatColor(brush.ToColor()),
            Enum enumValue => enumValue.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? string.Empty
        };
    }

    private static bool IsInspectableType(Type candidateType)
    {
        ArgumentNullException.ThrowIfNull(candidateType);

        if (candidateType.IsAbstract || candidateType.ContainsGenericParameters)
        {
            return false;
        }

        return candidateType.IsValueType || candidateType.GetConstructor(Type.EmptyTypes) != null;
    }

    private static string FormatGridLength(GridLength gridLength)
    {
        if (gridLength.IsAuto)
        {
            return "Auto";
        }

        if (gridLength.IsStar)
        {
            return AreClose(gridLength.Value, 1f)
                ? "*"
                : string.Create(CultureInfo.InvariantCulture, $"{gridLength.Value:0.##}*");
        }

        return gridLength.Value.ToString("0.##", CultureInfo.InvariantCulture);
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

    private static string FormatVector2(Vector2 value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{value.X:0.##},{value.Y:0.##}");
    }

    private static string FormatColor(Color color)
    {
        return FormatColorValue(color);
    }

    private static byte ParseHexByte(ReadOnlySpan<char> value)
    {
        return byte.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static bool AreClose(float left, float right)
    {
        return Math.Abs(left - right) <= 0.001f;
    }
}