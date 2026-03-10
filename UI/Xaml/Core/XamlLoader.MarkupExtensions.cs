using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace InkkSlinger;

public static partial class XamlLoader
{
    private static bool TryResolveXNull(string rawValue, out bool isXNull)
    {
        isXNull = false;
        if (!TryParseMarkupExtensionExpression(rawValue, out var expression))
        {
            return false;
        }

        if (!string.Equals(expression.Name, "x:Null", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (expression.Segments.Count > 0 || !string.IsNullOrWhiteSpace(expression.Body))
        {
            throw CreateXamlException("x:Null markup does not accept arguments.");
        }

        isXNull = true;
        return true;
    }

    private static bool TryResolveXType(string rawValue, out Type resolvedType, XObject? location = null)
    {
        resolvedType = typeof(object);
        if (!TryParseMarkupExtensionExpression(rawValue, out var expression) ||
            !string.Equals(expression.Name, "x:Type", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var typeText = expression.Body;
        if (TryParseNamedOrPositionalValue(expression, "TypeName", out var namedOrPositional))
        {
            typeText = namedOrPositional;
        }

        if (string.IsNullOrWhiteSpace(typeText))
        {
            throw CreateXamlException("x:Type markup requires a type name.", location);
        }

        resolvedType = ResolveTypeReference(typeText);
        return true;
    }


    private static bool TryResolveXReference(
        string rawValue,
        FrameworkElement? scope,
        out object resolved,
        XObject? location = null)
    {
        resolved = null!;
        if (!TryParseMarkupExtensionExpression(rawValue, out var expression) ||
            !string.Equals(expression.Name, "x:Reference", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryParseNamedOrPositionalValue(expression, "Name", out var name) ||
            string.IsNullOrWhiteSpace(name))
        {
            throw CreateXamlException("x:Reference markup requires a target name.", location);
        }

        var targetName = name.Trim();
        var resolvedNow = ResolveNameReference(scope, targetName);
        if (resolvedNow != null)
        {
            resolved = resolvedNow;
            return true;
        }

        resolved = new DeferredXReference(targetName);
        return true;
    }


    private static object? ResolveNameReference(FrameworkElement? scope, string name)
    {
        if (scope == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return NameScopeService.FindName(scope, name) ?? scope.FindName(name);
    }

    private static object ResolveDeferredXReference(DeferredXReference deferred, FrameworkElement? scope, XObject? location = null)
    {
        var resolved = ResolveNameReference(scope, deferred.Name);
        if (resolved != null)
        {
            return resolved;
        }

        if (CurrentLoadRootScope != null)
        {
            resolved = ResolveNameReference(CurrentLoadRootScope, deferred.Name);
            if (resolved != null)
            {
                return resolved;
            }
        }

        throw CreateXamlException($"x:Reference target '{deferred.Name}' could not be resolved.", location);
    }


    private static bool TryQueueDeferredBindingReferenceResolution(
        DependencyObject target,
        DependencyProperty dependencyProperty,
        BindingBase bindingBase,
        FrameworkElement? scope)
    {
        var deferredBindings = CollectBindingsWithDeferredReferences(bindingBase, accumulator: null);
        if (deferredBindings == null)
        {
            return false;
        }

        QueueDeferredFinalizeAction(() =>
        {
            foreach (var binding in deferredBindings)
            {
                if (binding.Source is DeferredXReference sourceDeferred)
                {
                    binding.Source = ResolveDeferredXReference(sourceDeferred, scope);
                }

                if (binding.ConverterParameter is DeferredXReference parameterDeferred)
                {
                    binding.ConverterParameter = ResolveDeferredXReference(parameterDeferred, scope);
                }
            }

            BindingOperations.SetBinding(target, dependencyProperty, bindingBase);
        });

        return true;
    }


    private static List<Binding>? CollectBindingsWithDeferredReferences(BindingBase bindingBase, List<Binding>? accumulator)
    {
        if (bindingBase is Binding binding)
        {
            if (binding.Source is DeferredXReference || binding.ConverterParameter is DeferredXReference)
            {
                accumulator ??= new List<Binding>();
                accumulator.Add(binding);
            }

            return accumulator;
        }

        if (bindingBase is MultiBinding multiBinding)
        {
            foreach (var childBinding in multiBinding.Bindings)
            {
                accumulator = CollectBindingsWithDeferredReferences(childBinding, accumulator);
            }

            return accumulator;
        }

        if (bindingBase is PriorityBinding priorityBinding)
        {
            foreach (var childBinding in priorityBinding.Bindings)
            {
                accumulator = CollectBindingsWithDeferredReferences(childBinding, accumulator);
            }
        }

        return accumulator;
    }


    private static T RunWithinResourceDictionarySourcePath<T>(string path, Func<T> action)
    {
        CurrentResourceDictionarySourcePaths ??= new Stack<string>();
        var normalized = Path.GetFullPath(path);
        CurrentResourceDictionarySourcePaths.Push(normalized);
        try
        {
            return action();
        }
        finally
        {
            _ = CurrentResourceDictionarySourcePaths.Pop();
            if (CurrentResourceDictionarySourcePaths.Count == 0)
            {
                CurrentResourceDictionarySourcePaths = null;
            }
        }
    }


    private static bool HasActiveResourceDictionarySourcePath(string path)
    {
        if (CurrentResourceDictionarySourcePaths == null || CurrentResourceDictionarySourcePaths.Count == 0)
        {
            return false;
        }

        var normalized = Path.GetFullPath(path);
        foreach (var activePath in CurrentResourceDictionarySourcePaths)
        {
            if (string.Equals(activePath, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    private static void QueueDeferredFinalizeAction(Action action)
    {
        if (CurrentDeferredFinalizeActions == null || CurrentDeferredFinalizeActions.Count == 0)
        {
            action();
            return;
        }

        CurrentDeferredFinalizeActions.Peek().Add(action);
    }


    private static void RunWithinDeferredFinalizeActions(Action action)
    {
        CurrentDeferredFinalizeActions ??= new Stack<List<Action>>();
        var queue = new List<Action>();
        CurrentDeferredFinalizeActions.Push(queue);
        try
        {
            action();

            // Keep draining in case a finalize action schedules more work.
            for (var i = 0; i < queue.Count; i++)
            {
                queue[i]();
            }
        }
        finally
        {
            CurrentDeferredFinalizeActions.Pop();
            if (CurrentDeferredFinalizeActions.Count == 0)
            {
                CurrentDeferredFinalizeActions = null;
            }
        }
    }


    private static string GetMarkupExtensionRequiredValue(string rawValue, string extensionName, string argumentLabel)
    {
        if (!TryParseMarkupExtensionExpression(rawValue, out var expression) ||
            !string.Equals(expression.Name, extensionName, StringComparison.OrdinalIgnoreCase))
        {
            throw CreateXamlException($"Markup extension '{extensionName}' is invalid.");
        }

        if (TryParseNamedOrPositionalValue(expression, "Key", out var key))
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(expression.Body))
        {
            return expression.Body.Trim();
        }

        throw CreateXamlException($"Markup extension '{extensionName}' requires {argumentLabel}.");
    }


    private static object ConvertAssignableTextValue(
        string rawValueText,
        Type targetType,
        FrameworkElement? resourceScope,
        XObject? location,
        string locationLabel)
    {
        if (TryResolveXNull(rawValueText, out var isXNull) && isXNull)
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
            {
                throw CreateXamlException(
                    $"x:Null is not assignable to '{targetType.Name}' for '{locationLabel}'.",
                    location);
            }

            return null!;
        }

        if (TryResolveXType(rawValueText, out var xType, location))
        {
            if (targetType != typeof(object) && targetType != typeof(Type))
            {
                throw CreateXamlException(
                    $"x:Type is not assignable to '{targetType.Name}' for '{locationLabel}'.",
                    location);
            }

            return xType;
        }

        if (TryResolveXReference(rawValueText, resourceScope, out var xReferenceValue, location))
        {
            if (xReferenceValue is DeferredXReference deferredReference)
            {
                return deferredReference;
            }

            return ResourceReferenceResolver.Coerce(xReferenceValue, targetType, locationLabel)!;
        }

        if (TryResolveXStatic(rawValueText, out var xStaticValue, location))
        {
            return ResourceReferenceResolver.Coerce(xStaticValue, targetType, locationLabel)!;
        }

        if (IsMarkupExtensionSyntax(rawValueText))
        {
            ThrowUnsupportedMarkupExtension(rawValueText, locationLabel, location);
        }

        return ConvertValue(rawValueText, targetType);
    }


    private static bool IsMarkupExtensionSyntax(string valueText)
    {
        var trimmed = valueText.Trim();
        return trimmed.StartsWith("{", StringComparison.Ordinal) &&
               trimmed.EndsWith("}", StringComparison.Ordinal);
    }

    private static void ThrowUnsupportedMarkupExtension(string rawValue, string context, XObject? location = null)
    {
        if (!TryParseMarkupExtensionExpression(rawValue, out var expression))
        {
            throw CreateXamlException(
                $"Markup extension expression '{rawValue}' is invalid for {context}.",
                location,
                code: XamlDiagnosticCode.InvalidValue);
        }

        throw CreateXamlException(
            $"Markup extension '{expression.Name}' is not supported for {context}.",
            location,
            code: XamlDiagnosticCode.UnsupportedConstruct);
    }


    private static bool TryParseMarkupExtensionExpression(string rawValue, out MarkupExtensionExpression expression)
    {
        expression = default;
        var trimmed = rawValue.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) ||
            !trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        var markupBody = trimmed[1..^1].Trim();
        if (string.IsNullOrWhiteSpace(markupBody))
        {
            throw CreateXamlException("Markup extension expression is empty.");
        }

        var separatorIndex = -1;
        for (var i = 0; i < markupBody.Length; i++)
        {
            if (char.IsWhiteSpace(markupBody[i]) || markupBody[i] == ',')
            {
                separatorIndex = i;
                break;
            }
        }

        var name = separatorIndex >= 0
            ? markupBody[..separatorIndex].Trim()
            : markupBody;
        var body = separatorIndex >= 0
            ? markupBody[separatorIndex..].TrimStart().TrimStart(',')
            : string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            throw CreateXamlException("Markup extension expression is missing an extension name.");
        }

        var segments = SplitTopLevelSegments(body);
        expression = new MarkupExtensionExpression(name, body, segments);
        return true;
    }


    private static List<string> SplitTopLevelSegments(string rawBody)
    {
        var body = rawBody.Trim();
        if (body.Length == 0)
        {
            return new List<string>();
        }

        var segments = new List<string>();
        var segmentStart = 0;
        var braceDepth = 0;

        for (var i = 0; i < body.Length; i++)
        {
            var ch = body[i];
            if (ch == '{')
            {
                braceDepth++;
                continue;
            }

            if (ch == '}')
            {
                braceDepth--;
                if (braceDepth < 0)
                {
                    throw CreateXamlException($"Markup extension body '{rawBody}' is invalid.");
                }

                continue;
            }

            if (ch != ',' || braceDepth != 0)
            {
                continue;
            }

            var segment = body[segmentStart..i].Trim();
            if (segment.Length > 0)
            {
                segments.Add(segment);
            }

            segmentStart = i + 1;
        }

        if (braceDepth != 0)
        {
            throw CreateXamlException($"Markup extension body '{rawBody}' is invalid.");
        }

        var tail = body[segmentStart..].Trim();
        if (tail.Length > 0)
        {
            segments.Add(tail);
        }

        return segments;
    }


    private static bool TryParseNamedOrPositionalValue(
        MarkupExtensionExpression expression,
        string namedKey,
        out string value)
    {
        value = string.Empty;
        var namedValue = default(string);
        var positionalValue = default(string);

        foreach (var segment in expression.Segments)
        {
            var equalsIndex = segment.IndexOf('=');
            if (equalsIndex > 0 && equalsIndex < segment.Length - 1)
            {
                var key = segment[..equalsIndex].Trim();
                var candidateValue = segment[(equalsIndex + 1)..].Trim();
                if (string.Equals(key, namedKey, StringComparison.OrdinalIgnoreCase))
                {
                    namedValue = candidateValue;
                }

                continue;
            }

            if (positionalValue == null)
            {
                positionalValue = segment.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(namedValue))
        {
            value = namedValue!;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(positionalValue))
        {
            value = positionalValue!;
            return true;
        }

        return false;
    }


    private static bool TryApplyAttachedPropertyElement(
        object target,
        Type ownerType,
        string propertyName,
        XElement propertyElement,
        object? codeBehind,
        FrameworkElement? resourceScope)
    {
        var contentElement = GetSingleChildElementOrThrow(
            propertyElement,
            $"Property element '{propertyElement.Name.LocalName}' must contain exactly one child element.",
            propertyElement);
        var value = BuildObject(contentElement, codeBehind, target as FrameworkElement ?? resourceScope);
        var setter = ResolveAttachedSetter(ownerType, target.GetType(), propertyName, value.GetType());
        if (setter == null)
        {
            return false;
        }

        setter.Invoke(null, new[] { target, value });
        return true;
    }


    private static MethodInfo? ResolveAttachedSetter(
        Type ownerType,
        Type targetType,
        string propertyName,
        Type valueType)
    {
        return XamlTypeResolver.ResolveAttachedSetter(ownerType, targetType, propertyName, valueType);
    }


    private static bool IsCompatibleAttachedSetter(MethodInfo method, string setterName, Type targetType, Type valueType)
    {
        if (method.Name != setterName)
        {
            return false;
        }

        var parameters = XamlTypeResolver.GetMethodParameters(method);
        if (parameters.Length != 2)
        {
            return false;
        }

        return parameters[0].ParameterType.IsAssignableFrom(targetType) &&
               parameters[1].ParameterType.IsAssignableFrom(valueType);
    }


    private readonly struct MarkupExtensionExpression
    {
        public MarkupExtensionExpression(string name, string body, List<string> segments)
        {
            Name = name;
            Body = body;
            Segments = segments;
        }

        public string Name { get; }

        public string Body { get; }

        public List<string> Segments { get; }
    }


    private sealed class DeferredXReference
    {
        public DeferredXReference(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

}
