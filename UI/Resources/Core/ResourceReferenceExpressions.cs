using System;

namespace InkkSlinger;

internal sealed class DynamicResourceReferenceExpression
{
    public DynamicResourceReferenceExpression(object key)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
    }

    public object Key { get; }
}

internal static class ResourceReferenceResolver
{
    public static bool TryResolve(
        DependencyObject target,
        DependencyProperty property,
        object? candidateValue,
        out object? resolvedValue)
    {
        resolvedValue = candidateValue;
        if (candidateValue is not DynamicResourceReferenceExpression dynamicReference)
        {
            return true;
        }

        if (target is not FrameworkElement frameworkElement)
        {
            throw new InvalidOperationException(
                $"DynamicResource for '{target.GetType().Name}.{property.Name}' requires a FrameworkElement target.");
        }

        if (!frameworkElement.TryFindResource(dynamicReference.Key, out var resource))
        {
            resolvedValue = DependencyObject.UnsetValue;
            return false;
        }

        resolvedValue = Coerce(resource, property.PropertyType, $"{target.GetType().Name}.{property.Name}");
        return true;
    }

    public static bool TryResolveForType(
        FrameworkElement? scope,
        object? candidateValue,
        Type targetType,
        string location,
        out object? resolvedValue)
    {
        resolvedValue = candidateValue;
        if (candidateValue is not DynamicResourceReferenceExpression dynamicReference)
        {
            return true;
        }

        if (scope == null)
        {
            throw new InvalidOperationException(
                $"DynamicResource for '{location}' requires a FrameworkElement scope.");
        }

        if (!scope.TryFindResource(dynamicReference.Key, out var resource))
        {
            resolvedValue = DependencyObject.UnsetValue;
            return false;
        }

        resolvedValue = Coerce(resource, targetType, location);
        return true;
    }

    public static object? Coerce(object? resourceValue, Type targetType, string location)
    {
        if (targetType == typeof(object) || resourceValue == null)
        {
            return resourceValue;
        }

        if (targetType.IsInstanceOfType(resourceValue))
        {
            return resourceValue;
        }

        if (DependencyValueCoercion.TryCoerce(resourceValue, targetType, out var coerced))
        {
            return coerced;
        }

        throw new InvalidOperationException(
            $"Resource value for '{location}' is of type '{resourceValue.GetType().Name}' and cannot be assigned to '{targetType.Name}'.");
    }
}
