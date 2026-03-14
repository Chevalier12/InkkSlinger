using System;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

namespace InkkSlinger;

public static partial class XamlLoader
{
    private static void AssignName(object target, string name, object? codeBehind)
    {
        if (target is FrameworkElement frameworkElement)
        {
            frameworkElement.Name = name;
        }

        var nameScopeOwner = GetCurrentNameScopeOwner();
        if (nameScopeOwner != null)
        {
            nameScopeOwner.RegisterNameInLocalScope(name, target);
        }

        if (codeBehind == null)
        {
            return;
        }

        var codeBehindType = codeBehind.GetType();
        var assigner = XamlTypeResolver.GetCodeBehindNameAssigner(codeBehindType, name);
        if (assigner != null)
        {
            try
            {
                assigner(codeBehind, target);
                return;
            }
            catch (InvalidCastException)
            {
                // Fall through when the cached member exists but isn't assignable from the actual target.
            }
        }
    }


    private static FrameworkElement? GetCurrentNameScopeOwner()
    {
        return CurrentConstructionRootScope ?? CurrentLoadRootScope;
    }


    private static bool TryAttachEventHandler(object target, string eventName, string handlerName, object? codeBehind)
    {
        var eventInfo = XamlTypeResolver.GetEvent(target.GetType(), eventName);
        if (eventInfo == null)
        {
            return false;
        }

        if (codeBehind == null)
        {
            throw new InvalidOperationException(
                $"Event '{eventName}' requires a code-behind instance to resolve handler '{handlerName}'.");
        }

        var method = ResolveCodeBehindHandlerMethod(codeBehind, handlerName);

        var delegateInstance = Delegate.CreateDelegate(eventInfo.EventHandlerType!, codeBehind, method);
        eventInfo.AddEventHandler(target, delegateInstance);
        return true;
    }


    private static MethodInfo ResolveCodeBehindHandlerMethod(object codeBehind, string handlerName)
    {
        var method = XamlTypeResolver.GetInstanceMethod(codeBehind.GetType(), handlerName);

        if (method == null)
        {
            throw new InvalidOperationException(
                $"Handler method '{handlerName}' was not found on code-behind type '{codeBehind.GetType().Name}'.");
        }

        return method;
    }


    private static bool IsSupportedEventSetterHandlerSignature(MethodInfo method, Type eventSourceType)
    {
        var parameters = XamlTypeResolver.GetMethodParameters(method);
        if (parameters.Length > 2)
        {
            return false;
        }

        if (parameters.Length == 0)
        {
            return true;
        }

        foreach (var parameter in parameters)
        {
            var parameterType = parameter.ParameterType;
            if (parameterType.IsValueType)
            {
                return false;
            }

            if (parameterType == typeof(object) ||
                typeof(RoutedEventArgs).IsAssignableFrom(parameterType) ||
                parameterType.IsAssignableFrom(eventSourceType))
            {
                continue;
            }

            return false;
        }

        return true;
    }


    private static void ApplyProperty(
        object target,
        string propertyName,
        string valueText,
        FrameworkElement? resourceScope,
        XObject? location = null)
    {
        var property = XamlTypeResolver.GetWritableProperty(target.GetType(), propertyName);

        if (property == null || !property.CanWrite)
        {
            throw CreateXamlException(
                $"Property '{propertyName}' was not found on type '{target.GetType().Name}'.",
                code: XamlDiagnosticCode.UnknownProperty,
                propertyName: propertyName,
                elementName: target.GetType().Name,
                hint: $"Check whether '{propertyName}' exists as a writable property on '{target.GetType().Name}'.");
        }

        object converted;
        if (TryParseStaticResourceKey(valueText, out var staticResourceKey))
        {
            var scopedOwner = target as FrameworkElement;
            var resolved = ResolveStaticResourceValue(staticResourceKey, scopedOwner, resourceScope);
            converted = CoerceResolvedResourceValue(
                resolved,
                property.PropertyType,
                $"{target.GetType().Name}.{propertyName}");
        }
        else if (TryResolveXStatic(valueText, out var xStaticResolved))
        {
            converted = CoerceResolvedResourceValue(
                xStaticResolved,
                property.PropertyType,
                $"{target.GetType().Name}.{propertyName}");
        }
        else if (TryResolveXType(valueText, out var xType))
        {
            if (property.PropertyType != typeof(object) && property.PropertyType != typeof(Type))
            {
                throw CreateXamlException(
                    $"x:Type is not assignable to property '{target.GetType().Name}.{propertyName}' of type '{property.PropertyType.Name}'.",
                    location);
            }

            converted = xType;
        }
        else if (TryResolveXNull(valueText, out var isXNull) && isXNull)
        {
            if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) == null)
            {
                throw CreateXamlException(
                    $"x:Null is not assignable to property '{target.GetType().Name}.{propertyName}' of type '{property.PropertyType.Name}'.",
                    location);
            }

            converted = null!;
        }
        else if (TryResolveXReference(valueText, target as FrameworkElement ?? resourceScope, out var xReferenceValue, location))
        {
            if (xReferenceValue is DeferredXReference deferredReference)
            {
                QueueDeferredFinalizeAction(() =>
                {
                    var resolvedReference = ResolveDeferredXReference(
                        deferredReference,
                        target as FrameworkElement ?? resourceScope,
                        location);
                    var coerced = CoerceResolvedResourceValue(
                        resolvedReference,
                        property.PropertyType,
                        $"{target.GetType().Name}.{propertyName}");
                    property.SetValue(target, coerced);
                });
                return;
            }

            converted = CoerceResolvedResourceValue(
                xReferenceValue,
                property.PropertyType,
                $"{target.GetType().Name}.{propertyName}");
        }
        else if (TryApplyRawNameReferenceProperty(target, property, valueText, target as FrameworkElement ?? resourceScope, location))
        {
            return;
        }
        else
        {
            if (IsMarkupExtensionSyntax(valueText))
            {
                ThrowUnsupportedMarkupExtension(
                    valueText,
                    $"property '{target.GetType().Name}.{propertyName}'");
            }

            converted = ConvertValue(valueText, property.PropertyType);
        }

        property.SetValue(target, converted);
    }


    private static bool TryApplyRawNameReferenceProperty(
        object target,
        PropertyInfo property,
        string rawValue,
        FrameworkElement? scope,
        XObject? location)
    {
        if (!SupportsRawNameReferenceProperty(target, property) ||
            string.IsNullOrWhiteSpace(rawValue) ||
            IsMarkupExtensionSyntax(rawValue))
        {
            return false;
        }

        var name = rawValue.Trim();
        var resolved = ResolveNameReference(scope, name);
        if (resolved != null)
        {
            var converted = CoerceResolvedResourceValue(
                resolved,
                property.PropertyType,
                $"{target.GetType().Name}.{property.Name}");
            property.SetValue(target, converted);
            return true;
        }

        QueueDeferredFinalizeAction(() =>
        {
            var deferred = ResolveDeferredXReference(new DeferredXReference(name), scope, location);
            var converted = CoerceResolvedResourceValue(
                deferred,
                property.PropertyType,
                $"{target.GetType().Name}.{property.Name}");
            property.SetValue(target, converted);
        });

        return true;
    }


    private static bool SupportsRawNameReferenceProperty(object target, PropertyInfo property)
    {
        return target is Label &&
               string.Equals(property.Name, nameof(Label.Target), StringComparison.Ordinal) &&
               typeof(UIElement).IsAssignableFrom(property.PropertyType);
    }


    private static bool TryApplyBindingExpression(object target, string propertyName, string valueText, FrameworkElement? resourceScope)
    {
        if (!TryParseMarkupExtensionExpression(valueText, out var expression) ||
            !string.Equals(expression.Name, "Binding", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (target is not DependencyObject dependencyObject)
        {
            throw CreateXamlException(
                $"Binding markup on '{target.GetType().Name}.{propertyName}' requires a DependencyObject target.");
        }

        var dependencyProperty = ResolveDependencyProperty(target.GetType(), propertyName);
        if (dependencyProperty == null)
        {
            throw CreateXamlException(
                $"Binding markup on '{target.GetType().Name}.{propertyName}' requires a dependency property named '{propertyName}Property'.");
        }

        var bindingScope = target as FrameworkElement ?? resourceScope;
        var binding = ParseBinding(expression.Body, dependencyProperty.PropertyType, bindingScope);

        if (TryQueueDeferredBindingReferenceResolution(
                dependencyObject,
                dependencyProperty,
                binding,
                bindingScope))
        {
            return true;
        }

        BindingOperations.SetBinding(dependencyObject, dependencyProperty, binding);
        return true;
    }


    private static bool TryApplyDynamicResourceExpression(object target, string propertyName, string valueText)
    {
        if (!TryParseDynamicResourceKey(valueText, out var dynamicResourceKey))
        {
            return false;
        }

        if (target is not FrameworkElement frameworkElement)
        {
            throw CreateXamlException(
                $"DynamicResource markup on '{target.GetType().Name}.{propertyName}' requires a FrameworkElement target.");
        }

        var dependencyProperty = ResolveDependencyProperty(target.GetType(), propertyName);
        if (dependencyProperty == null)
        {
            throw CreateXamlException(
                $"DynamicResource markup on '{target.GetType().Name}.{propertyName}' requires a dependency property named '{propertyName}Property'.");
        }

        frameworkElement.SetResourceReference(dependencyProperty, dynamicResourceKey);
        return true;
    }


    private static void ApplyAttachedProperty(
        object target,
        string attachedPropertyName,
        string valueText,
        FrameworkElement? resourceScope,
        XObject? location = null)
    {
        var separatorIndex = attachedPropertyName.IndexOf('.');
        var ownerTypeName = attachedPropertyName[..separatorIndex];
        var propertyName = attachedPropertyName[(separatorIndex + 1)..];
        var ownerType = ResolveElementType(ownerTypeName);

        if (TryParseDynamicResourceKey(valueText, out var dynamicResourceKey))
        {
            var dependencyProperty = ResolveDependencyProperty(ownerType, propertyName);
            if (dependencyProperty == null)
            {
                throw CreateXamlException(
                    $"Attached property '{ownerType.Name}.{propertyName}' requires a dependency property named '{propertyName}Property' for DynamicResource usage.");
            }

            if (target is not FrameworkElement frameworkElement)
            {
                throw CreateXamlException(
                    $"DynamicResource markup on attached property '{ownerType.Name}.{propertyName}' requires a FrameworkElement target.");
            }

            frameworkElement.SetResourceReference(dependencyProperty, dynamicResourceKey);
            return;
        }

        var setter = XamlTypeResolver.ResolveAttachedSetterForTarget(ownerType, target.GetType(), propertyName);

        if (setter == null)
        {
            throw CreateXamlException(
                $"Attached property setter '{ownerType.Name}.Set{propertyName}(..., ...)' was not found.");
        }

        var setterParameters = XamlTypeResolver.GetMethodParameters(setter);
        var firstParameter = setterParameters[0].ParameterType;
        if (!firstParameter.IsAssignableFrom(target.GetType()))
        {
            throw CreateXamlException(
                $"Attached property '{ownerType.Name}.{propertyName}' is not applicable to '{target.GetType().Name}'.");
        }

        var valueType = setterParameters[1].ParameterType;
        object converted;
        if (TryParseStaticResourceKey(valueText, out var staticResourceKey))
        {
            var resolved = ResolveStaticResourceValue(staticResourceKey, target as FrameworkElement, resourceScope);
            if (valueType.IsInstanceOfType(resolved))
            {
                converted = resolved;
            }
            else if (DependencyValueCoercion.TryCoerce(resolved, valueType, out var coerced))
            {
                converted = coerced!;
            }
            else
            {
                throw CreateXamlException(
                    $"Attached property '{ownerType.Name}.{propertyName}' requires a value assignable to '{valueType.Name}', but resource resolved to '{resolved.GetType().Name}'.");
            }
        }
        else if (TryResolveXStatic(valueText, out var xStaticResolved))
        {
            converted = CoerceResolvedResourceValue(
                xStaticResolved,
                valueType,
                $"attached property '{ownerType.Name}.{propertyName}'");
        }
        else if (TryResolveXType(valueText, out var xType))
        {
            if (valueType != typeof(object) && valueType != typeof(Type))
            {
                throw CreateXamlException(
                    $"Attached property '{ownerType.Name}.{propertyName}' requires a value assignable to '{valueType.Name}', but x:Type was provided.",
                    location);
            }

            converted = xType;
        }
        else if (TryResolveXNull(valueText, out var isXNull) && isXNull)
        {
            if (valueType.IsValueType && Nullable.GetUnderlyingType(valueType) == null)
            {
                throw CreateXamlException(
                    $"Attached property '{ownerType.Name}.{propertyName}' does not accept x:Null for non-nullable type '{valueType.Name}'.",
                    location);
            }

            converted = null!;
        }
        else if (TryResolveXReference(valueText, target as FrameworkElement ?? resourceScope, out var xReferenceValue, location))
        {
            if (xReferenceValue is DeferredXReference deferredReference)
            {
                QueueDeferredFinalizeAction(() =>
                {
                    var resolvedReference = ResolveDeferredXReference(
                        deferredReference,
                        target as FrameworkElement ?? resourceScope,
                        location);
                    var resolvedCoerced = CoerceResolvedResourceValue(
                        resolvedReference,
                        valueType,
                        $"attached property '{ownerType.Name}.{propertyName}'");
                    setter.Invoke(null, new[] { target, resolvedCoerced });
                });
                return;
            }

            converted = CoerceResolvedResourceValue(
                xReferenceValue,
                valueType,
                $"attached property '{ownerType.Name}.{propertyName}'");
        }
        else
        {
            if (IsMarkupExtensionSyntax(valueText))
            {
                ThrowUnsupportedMarkupExtension(
                    valueText,
                    $"attached property '{ownerType.Name}.{propertyName}'");
            }

            converted = ConvertValue(valueText, valueType);
        }

        setter.Invoke(null, new[] { target, converted });
    }

    private static bool TryResolveXStatic(string rawValue, out object resolved, XObject? location = null)
    {
        resolved = null!;
        if (!TryParseMarkupExtensionExpression(rawValue, out var expression) ||
            !string.Equals(expression.Name, "x:Static", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryParseNamedOrPositionalValue(expression, "Member", out var memberReference) ||
            string.IsNullOrWhiteSpace(memberReference))
        {
            throw CreateXamlException(
                "x:Static markup requires a member reference, e.g. {x:Static EditingCommands.Copy}.",
                location,
                code: XamlDiagnosticCode.InvalidValue);
        }

        var trimmedMemberReference = memberReference.Trim();
        var separatorIndex = trimmedMemberReference.LastIndexOf('.');
        if (separatorIndex <= 0 || separatorIndex == trimmedMemberReference.Length - 1)
        {
            throw CreateXamlException(
                $"x:Static member reference '{trimmedMemberReference}' is invalid. Expected 'TypeName.MemberName'.",
                location,
                code: XamlDiagnosticCode.InvalidValue);
        }

        var typeReference = trimmedMemberReference[..separatorIndex].Trim();
        var memberName = trimmedMemberReference[(separatorIndex + 1)..].Trim();

        Type ownerType;
        try
        {
            ownerType = ResolveTypeReference(typeReference);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
        {
            throw CreateXamlException(
                $"x:Static type '{typeReference}' could not be resolved.",
                location,
                ex,
                code: XamlDiagnosticCode.UnknownElement);
        }

        if (XamlTypeResolver.TryResolveStaticMember(ownerType, memberName, out resolved))
        {
            return true;
        }

        throw CreateXamlException(
            $"x:Static member '{memberName}' was not found on type '{ownerType.Name}'.",
            location,
            code: XamlDiagnosticCode.UnknownProperty);
    }


}
