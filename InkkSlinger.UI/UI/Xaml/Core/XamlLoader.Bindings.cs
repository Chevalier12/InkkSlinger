using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace InkkSlinger;

public static partial class XamlLoader
{
    private static Binding ParseBindingMarkup(string rawMarkup, FrameworkElement? resourceScope)
    {
        if (!TryParseMarkupExtensionExpression(rawMarkup, out var expression) ||
            !string.Equals(expression.Name, "Binding", StringComparison.OrdinalIgnoreCase))
        {
            throw CreateXamlException($"Binding markup '{rawMarkup}' is invalid.");
        }

        return ParseBinding(expression.Body, typeof(object), resourceScope);
    }


    private static Type ResolveStyleTargetType(string targetTypeText)
    {
        if (TryResolveXType(targetTypeText, out var xType))
        {
            return xType;
        }

        var trimmed = targetTypeText.Trim();
        return ResolveElementType(trimmed);
    }


    private static string GetRequiredAttributeValue(XElement element, string attributeName)
    {
        var value = GetOptionalAttributeValue(element, attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw CreateXamlException(
                $"Element '{element.Name.LocalName}' requires attribute '{attributeName}'.",
                element);
        }

        return value;
    }


    private static string? GetOptionalAttributeValue(XElement element, string attributeName)
    {
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration || !string.IsNullOrEmpty(attribute.Name.NamespaceName))
            {
                continue;
            }

            if (string.Equals(attribute.Name.LocalName, attributeName, StringComparison.OrdinalIgnoreCase))
            {
                return attribute.Value;
            }
        }

        return null;
    }


    private static Binding ParseBinding(string bindingBody, Type targetPropertyType, FrameworkElement? resourceScope)
    {
        var binding = new Binding();
        var hasSourceSelector = false;
        var hasElementNameSelector = false;
        var hasRelativeSourceSelector = false;
        if (string.IsNullOrWhiteSpace(bindingBody))
        {
            return binding;
        }

        var segments = SplitBindingSegments(bindingBody);

        var index = 0;
        if (segments.Count > 0 && !segments[0].Contains('=', StringComparison.Ordinal))
        {
            binding.Path = segments[0];
            index = 1;
        }

        for (var i = index; i < segments.Count; i++)
        {
            var segment = segments[i];
            var equalsIndex = segment.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex == segment.Length - 1)
            {
                throw CreateXamlException($"Binding segment '{segment}' is invalid.");
            }

            var key = segment[..equalsIndex].Trim();
            var rawValue = segment[(equalsIndex + 1)..].Trim();

            if (string.Equals(key, nameof(Binding.Source), StringComparison.OrdinalIgnoreCase))
            {
                hasSourceSelector = true;
            }
            else if (string.Equals(key, nameof(Binding.ElementName), StringComparison.OrdinalIgnoreCase))
            {
                hasElementNameSelector = true;
            }
            else if (string.Equals(key, "RelativeSource", StringComparison.OrdinalIgnoreCase))
            {
                hasRelativeSourceSelector = true;
            }

            if (TryApplyBindingOption(binding, key, rawValue, targetPropertyType, resourceScope))
            {
                continue;
            }

            throw CreateXamlException($"Binding key '{key}' is not supported.");
        }

        ValidateBindingSourceSelectorConflict(
            binding,
            sourceSelectorSpecified: hasSourceSelector,
            elementNameSelectorSpecified: hasElementNameSelector,
            relativeSourceSelectorSpecified: hasRelativeSourceSelector);
        return binding;
    }


    private static bool TryApplyBindingOption(
        Binding binding,
        string key,
        string rawValue,
        Type targetPropertyType,
        FrameworkElement? resourceScope)
    {
        if (string.Equals(key, nameof(Binding.Path), StringComparison.OrdinalIgnoreCase))
        {
            binding.Path = rawValue;
            return true;
        }

        if (string.Equals(key, nameof(Binding.Mode), StringComparison.OrdinalIgnoreCase))
        {
            binding.Mode = ParseEnumValue<BindingMode>(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(Binding.UpdateSourceTrigger), StringComparison.OrdinalIgnoreCase))
        {
            binding.UpdateSourceTrigger = ParseEnumValue<UpdateSourceTrigger>(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(Binding.FallbackValue), StringComparison.OrdinalIgnoreCase))
        {
            binding.FallbackValue = targetPropertyType == typeof(object)
                ? ParseLooseValue(rawValue)
                : ConvertValue(rawValue, targetPropertyType);
            return true;
        }

        if (string.Equals(key, nameof(Binding.TargetNullValue), StringComparison.OrdinalIgnoreCase))
        {
            binding.TargetNullValue = targetPropertyType == typeof(object)
                ? ParseLooseValue(rawValue)
                : ConvertValue(rawValue, targetPropertyType);
            return true;
        }

        if (string.Equals(key, nameof(Binding.Source), StringComparison.OrdinalIgnoreCase))
        {
            binding.Source = ResolveBindingSourceValue(rawValue, resourceScope);
            return true;
        }

        if (string.Equals(key, nameof(Binding.ElementName), StringComparison.OrdinalIgnoreCase))
        {
            binding.ElementName = rawValue;
            return true;
        }

        if (string.Equals(key, "RelativeSource", StringComparison.OrdinalIgnoreCase))
        {
            ApplyRelativeSource(binding, rawValue);
            return true;
        }

        if (string.Equals(key, nameof(Binding.Converter), StringComparison.OrdinalIgnoreCase))
        {
            binding.Converter = ResolveBindingResource<IValueConverter>(rawValue, resourceScope, nameof(Binding.Converter));
            return true;
        }

        if (string.Equals(key, nameof(Binding.ConverterParameter), StringComparison.OrdinalIgnoreCase))
        {
            binding.ConverterParameter = ResolveBindingSourceValue(rawValue, resourceScope);
            return true;
        }

        if (string.Equals(key, nameof(Binding.ConverterCulture), StringComparison.OrdinalIgnoreCase))
        {
            binding.ConverterCulture = CultureInfo.GetCultureInfo(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(Binding.ValidatesOnDataErrors), StringComparison.OrdinalIgnoreCase))
        {
            binding.ValidatesOnDataErrors = bool.Parse(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(Binding.ValidatesOnNotifyDataErrors), StringComparison.OrdinalIgnoreCase))
        {
            binding.ValidatesOnNotifyDataErrors = bool.Parse(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(Binding.ValidatesOnExceptions), StringComparison.OrdinalIgnoreCase))
        {
            binding.ValidatesOnExceptions = bool.Parse(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(Binding.BindingGroupName), StringComparison.OrdinalIgnoreCase))
        {
            binding.BindingGroupName = rawValue;
            return true;
        }

        if (string.Equals(key, nameof(Binding.UpdateSourceExceptionFilter), StringComparison.OrdinalIgnoreCase))
        {
            binding.UpdateSourceExceptionFilter = ResolveBindingResource<UpdateSourceExceptionFilterCallback>(
                rawValue,
                resourceScope,
                nameof(Binding.UpdateSourceExceptionFilter));
            return true;
        }

        return false;
    }


    private static bool TryApplyMultiBindingOption(
        MultiBinding multiBinding,
        string key,
        string rawValue,
        Type targetPropertyType,
        FrameworkElement? resourceScope)
    {
        if (string.Equals(key, nameof(MultiBinding.Mode), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.Mode = ParseEnumValue<BindingMode>(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.UpdateSourceTrigger), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.UpdateSourceTrigger = ParseEnumValue<UpdateSourceTrigger>(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.FallbackValue), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.FallbackValue = targetPropertyType == typeof(object)
                ? ParseLooseValue(rawValue)
                : ConvertValue(rawValue, targetPropertyType);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.TargetNullValue), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.TargetNullValue = targetPropertyType == typeof(object)
                ? ParseLooseValue(rawValue)
                : ConvertValue(rawValue, targetPropertyType);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.Converter), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.Converter = ResolveBindingResource<IMultiValueConverter>(rawValue, resourceScope, nameof(MultiBinding.Converter));
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.ConverterParameter), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.ConverterParameter = ResolveBindingSourceValue(rawValue, resourceScope);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.ConverterCulture), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.ConverterCulture = CultureInfo.GetCultureInfo(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.ValidatesOnDataErrors), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.ValidatesOnDataErrors = bool.Parse(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.ValidatesOnNotifyDataErrors), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.ValidatesOnNotifyDataErrors = bool.Parse(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.ValidatesOnExceptions), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.ValidatesOnExceptions = bool.Parse(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.BindingGroupName), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.BindingGroupName = rawValue;
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.UpdateSourceExceptionFilter), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.UpdateSourceExceptionFilter = ResolveBindingResource<UpdateSourceExceptionFilterCallback>(
                rawValue,
                resourceScope,
                nameof(MultiBinding.UpdateSourceExceptionFilter));
            return true;
        }

        return false;
    }


    private static bool TryApplyPriorityBindingOption(
        PriorityBinding priorityBinding,
        string key,
        string rawValue,
        Type targetPropertyType,
        FrameworkElement? resourceScope)
    {
        if (string.Equals(key, nameof(PriorityBinding.Mode), StringComparison.OrdinalIgnoreCase))
        {
            priorityBinding.Mode = ParseEnumValue<BindingMode>(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(PriorityBinding.UpdateSourceTrigger), StringComparison.OrdinalIgnoreCase))
        {
            priorityBinding.UpdateSourceTrigger = ParseEnumValue<UpdateSourceTrigger>(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(PriorityBinding.FallbackValue), StringComparison.OrdinalIgnoreCase))
        {
            priorityBinding.FallbackValue = targetPropertyType == typeof(object)
                ? ParseLooseValue(rawValue)
                : ConvertValue(rawValue, targetPropertyType);
            return true;
        }

        if (string.Equals(key, nameof(PriorityBinding.TargetNullValue), StringComparison.OrdinalIgnoreCase))
        {
            priorityBinding.TargetNullValue = targetPropertyType == typeof(object)
                ? ParseLooseValue(rawValue)
                : ConvertValue(rawValue, targetPropertyType);
            return true;
        }

        if (string.Equals(key, nameof(PriorityBinding.BindingGroupName), StringComparison.OrdinalIgnoreCase))
        {
            priorityBinding.BindingGroupName = rawValue;
            return true;
        }

        if (string.Equals(key, nameof(PriorityBinding.UpdateSourceExceptionFilter), StringComparison.OrdinalIgnoreCase))
        {
            priorityBinding.UpdateSourceExceptionFilter = ResolveBindingResource<UpdateSourceExceptionFilterCallback>(
                rawValue,
                resourceScope,
                nameof(PriorityBinding.UpdateSourceExceptionFilter));
            return true;
        }

        return false;
    }


    private static List<string> SplitBindingSegments(string bindingBody)
    {
        return SplitTopLevelSegments(bindingBody);
    }


    private static void ApplyRelativeSource(Binding binding, string rawValue)
    {
        ResetBindingRelativeSource(binding);
        var trimmed = rawValue.Trim();
        var segments = new List<string>();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            segments = SplitBindingSegments(trimmed);
        }
        else
        {
            if (!TryParseMarkupExtensionExpression(trimmed, out var expression) ||
                !string.Equals(expression.Name, "RelativeSource", StringComparison.OrdinalIgnoreCase))
            {
                throw CreateXamlException($"RelativeSource expression '{rawValue}' is invalid.");
            }

            if (string.IsNullOrWhiteSpace(expression.Body))
            {
                throw CreateXamlException("RelativeSource requires a mode.");
            }

            segments = expression.Segments;
        }

        ApplyRelativeSourceSegments(binding, segments);
    }


    private static void ApplyRelativeSourceSegments(Binding binding, List<string> segments)
    {
        if (segments.Count == 0)
        {
            throw CreateXamlException("RelativeSource requires a mode.");
        }

        var hasMode = false;
        var hasAncestorType = false;
        var hasAncestorLevel = false;
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var equalsIndex = segment.IndexOf('=');
            if (equalsIndex < 0)
            {
                if (i > 0)
                {
                    throw CreateXamlException("RelativeSource requires the positional mode segment to appear first.");
                }

                if (hasMode)
                {
                    throw CreateXamlException($"RelativeSource segment '{segment}' is invalid.");
                }

                binding.RelativeSourceMode = ParseRelativeSourceMode(segment);
                hasMode = true;
                continue;
            }

            if (equalsIndex == 0 || equalsIndex == segment.Length - 1)
            {
                throw CreateXamlException($"RelativeSource segment '{segment}' is invalid.");
            }

            var key = segment[..equalsIndex].Trim();
            var value = segment[(equalsIndex + 1)..].Trim();

            if (string.Equals(key, "Mode", StringComparison.OrdinalIgnoreCase))
            {
                binding.RelativeSourceMode = ParseRelativeSourceMode(value);
                hasMode = true;
                continue;
            }

            if (string.Equals(key, "AncestorType", StringComparison.OrdinalIgnoreCase))
            {
                binding.RelativeSourceAncestorType = ResolveTypeReference(value);
                hasAncestorType = true;
                continue;
            }

            if (string.Equals(key, "AncestorLevel", StringComparison.OrdinalIgnoreCase))
            {
                binding.RelativeSourceAncestorLevel = int.Parse(value, CultureInfo.InvariantCulture);
                hasAncestorLevel = true;
                continue;
            }

            throw CreateXamlException($"RelativeSource key '{key}' is not supported.");
        }

        ValidateRelativeSourceConfiguration(binding, hasMode, hasAncestorType, hasAncestorLevel);
    }


    private static void ApplyBindingRelativeSourcePropertyElement(Binding binding, XElement propertyElement)
    {
        var relativeSourceElement = GetSingleChildElementOrThrow(
            propertyElement,
            "Binding.RelativeSource must contain exactly one RelativeSource element.",
            propertyElement);
        if (!string.Equals(relativeSourceElement.Name.LocalName, "RelativeSource", StringComparison.Ordinal))
        {
            throw CreateXamlException(
                "Binding.RelativeSource must contain exactly one RelativeSource element.",
                propertyElement);
        }

        ResetBindingRelativeSource(binding);
        var hasMode = false;
        var hasAncestorType = false;
        var hasAncestorLevel = false;
        foreach (var attribute in relativeSourceElement.Attributes())
        {
            if (attribute.IsNamespaceDeclaration || !string.IsNullOrEmpty(attribute.Name.NamespaceName))
            {
                continue;
            }

            var key = attribute.Name.LocalName;
            var value = attribute.Value;
            if (string.Equals(key, "Mode", StringComparison.OrdinalIgnoreCase))
            {
                binding.RelativeSourceMode = ParseRelativeSourceMode(value);
                hasMode = true;
                continue;
            }

            if (string.Equals(key, "AncestorType", StringComparison.OrdinalIgnoreCase))
            {
                binding.RelativeSourceAncestorType = ResolveTypeReference(value);
                hasAncestorType = true;
                continue;
            }

            if (string.Equals(key, "AncestorLevel", StringComparison.OrdinalIgnoreCase))
            {
                binding.RelativeSourceAncestorLevel = int.Parse(value, CultureInfo.InvariantCulture);
                hasAncestorLevel = true;
                continue;
            }

            throw CreateXamlException($"RelativeSource attribute '{key}' is not supported.", attribute);
        }

        ValidateRelativeSourceConfiguration(binding, hasMode, hasAncestorType, hasAncestorLevel, relativeSourceElement);
    }


    private static void ValidateRelativeSourceConfiguration(
        Binding binding,
        bool hasMode,
        bool hasAncestorType,
        bool hasAncestorLevel,
        XObject? location = null)
    {
        if (!hasMode)
        {
            throw CreateXamlException("RelativeSource requires a mode.", location);
        }

        if (binding.RelativeSourceMode == RelativeSourceMode.FindAncestor && !hasAncestorType)
        {
            throw CreateXamlException("RelativeSource FindAncestor mode requires AncestorType.", location);
        }

        if (binding.RelativeSourceMode != RelativeSourceMode.FindAncestor && (hasAncestorType || hasAncestorLevel))
        {
            throw CreateXamlException(
                "RelativeSource AncestorType and AncestorLevel are only valid when Mode=FindAncestor.",
                location);
        }

        if (hasAncestorLevel && binding.RelativeSourceAncestorLevel < 1)
        {
            throw CreateXamlException("RelativeSource AncestorLevel must be greater than or equal to 1.", location);
        }
    }


    private static void ResetBindingRelativeSource(Binding binding)
    {
        binding.RelativeSourceMode = RelativeSourceMode.None;
        binding.RelativeSourceAncestorType = null;
        binding.RelativeSourceAncestorLevel = 1;
    }


    private static void ValidateBindingSourceSelectorConflict(
        Binding binding,
        XObject? location = null,
        bool? sourceSelectorSpecified = null,
        bool? elementNameSelectorSpecified = null,
        bool? relativeSourceSelectorSpecified = null)
    {
        var selectedSources = 0;
        var hasSourceSelector = sourceSelectorSpecified ?? binding.Source != null;
        if (hasSourceSelector)
        {
            selectedSources++;
        }

        var hasElementNameSelector = elementNameSelectorSpecified ?? !string.IsNullOrWhiteSpace(binding.ElementName);
        if (hasElementNameSelector)
        {
            selectedSources++;
        }

        var hasRelativeSourceSelector = relativeSourceSelectorSpecified ?? binding.RelativeSourceMode != RelativeSourceMode.None;
        if (hasRelativeSourceSelector)
        {
            selectedSources++;
        }

        if (selectedSources > 1)
        {
            throw CreateXamlException(
                "Binding source selectors Source, ElementName, and RelativeSource are mutually exclusive.",
                location);
        }
    }


    private static RelativeSourceMode ParseRelativeSourceMode(string rawValue)
    {
        return ParseEnumValue<RelativeSourceMode>(rawValue);
    }


    private static T ResolveBindingResource<T>(string rawValue, FrameworkElement? resourceScope, string optionName)
        where T : class
    {
        object resolved;
        if (TryParseStaticResourceKey(rawValue, out var staticResourceKey))
        {
            resolved = ResolveStaticResourceValue(staticResourceKey, resourceScope);
        }
        else if (TryResolveXStatic(rawValue, out var xStaticResolved))
        {
            resolved = xStaticResolved;
        }
        else if (TryResolveXReference(rawValue, resourceScope, out var xReferenceValue))
        {
            if (xReferenceValue is DeferredXReference)
            {
                throw CreateXamlException(
                    $"{optionName} does not support unresolved forward x:Reference values.");
            }

            resolved = xReferenceValue;
        }
        else
        {
            if (IsMarkupExtensionSyntax(rawValue))
            {
                ThrowUnsupportedMarkupExtension(rawValue, $"binding option '{optionName}'");
            }

            resolved = ResolveStaticResourceValue(rawValue.Trim(), resourceScope);
        }

        if (resolved is T typed)
        {
            return typed;
        }

        throw CreateXamlException(
            $"{optionName} requires a resource assignable to '{typeof(T).Name}', but resolved '{resolved.GetType().Name}'.");
    }


    private static DependencyProperty? ResolveDependencyProperty(Type targetType, string propertyName)
    {
        return XamlTypeResolver.ResolveDependencyProperty(targetType, propertyName);
    }


}
