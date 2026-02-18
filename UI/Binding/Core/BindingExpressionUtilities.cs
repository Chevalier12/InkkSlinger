using System;
using System.Globalization;
using System.Reflection;

namespace InkkSlinger;

internal static class BindingExpressionUtilities
{
    public static BindingMode ResolveEffectiveMode(BindingBase binding, DependencyObject target, DependencyProperty targetProperty)
    {
        if (binding.Mode != BindingMode.Default)
        {
            return binding.Mode;
        }

        var metadata = targetProperty.GetMetadata(target);
        return metadata.BindsTwoWayByDefault ? BindingMode.TwoWay : BindingMode.OneWay;
    }

    public static UpdateSourceTrigger ResolveEffectiveUpdateSourceTrigger(BindingBase binding, DependencyObject target, DependencyProperty targetProperty)
    {
        if (binding.UpdateSourceTrigger != UpdateSourceTrigger.Default)
        {
            return binding.UpdateSourceTrigger;
        }

        if (target is TextBox && ReferenceEquals(targetProperty, TextBox.TextProperty))
        {
            return UpdateSourceTrigger.LostFocus;
        }

        return UpdateSourceTrigger.PropertyChanged;
    }

    public static bool SupportsSourceToTarget(BindingMode mode)
    {
        return mode == BindingMode.OneWay || mode == BindingMode.TwoWay || mode == BindingMode.OneTime;
    }

    public static bool SupportsTargetToSource(BindingMode mode)
    {
        return mode == BindingMode.TwoWay || mode == BindingMode.OneWayToSource;
    }

    public static object? ResolveSource(DependencyObject target, Binding binding)
    {
        if (binding.Source != null)
        {
            return binding.Source;
        }

        if (!string.IsNullOrWhiteSpace(binding.ElementName))
        {
            return ResolveElementNameSource(target, binding.ElementName);
        }

        if (binding.RelativeSourceMode != RelativeSourceMode.None)
        {
            return ResolveRelativeSource(target, binding);
        }

        if (target is FrameworkElement element)
        {
            return element.DataContext;
        }

        return null;
    }

    public static object? ResolvePathValue(object source, string path)
    {
        var segments = GetPathSegments(path);
        if (segments.Length == 0)
        {
            return source;
        }

        object? current = source;
        foreach (var segment in segments)
        {
            if (current == null)
            {
                return null;
            }

            var property = current.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        return current;
    }

    public static bool TrySetPathValue(object source, string path, object? value)
    {
        var segments = GetPathSegments(path);
        if (segments.Length == 0)
        {
            return false;
        }

        object? current = source;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (current == null)
            {
                return false;
            }

            var navigation = current.GetType().GetProperty(segments[i], BindingFlags.Instance | BindingFlags.Public);
            if (navigation == null)
            {
                return false;
            }

            current = navigation.GetValue(current);
        }

        if (current == null)
        {
            return false;
        }

        var leaf = current.GetType().GetProperty(segments[^1], BindingFlags.Instance | BindingFlags.Public);
        if (leaf == null || !leaf.CanWrite)
        {
            return false;
        }

        var assignable = value == null || leaf.PropertyType.IsInstanceOfType(value);
        if (!assignable)
        {
            return false;
        }

        leaf.SetValue(current, value);
        return true;
    }

    public static bool TryGetPathSourceAndLeafProperty(object source, string path, out object? leafSource, out string? leafProperty)
    {
        leafSource = null;
        leafProperty = null;

        var segments = GetPathSegments(path);
        if (segments.Length == 0)
        {
            leafSource = source;
            return true;
        }

        object? current = source;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (current == null)
            {
                return false;
            }

            var navigation = current.GetType().GetProperty(segments[i], BindingFlags.Instance | BindingFlags.Public);
            if (navigation == null)
            {
                return false;
            }

            current = navigation.GetValue(current);
        }

        if (current == null)
        {
            return false;
        }

        leafSource = current;
        leafProperty = segments[^1];
        return true;
    }

    public static bool TryGetPropertyValue(object source, string propertyName, out object? value)
    {
        var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property == null)
        {
            value = null;
            return false;
        }

        value = property.GetValue(source);
        return true;
    }

    public static string[] GetPathSegments(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        return path.Split('.', StringSplitOptions.RemoveEmptyEntries);
    }

    public static bool ShouldReactToObservedPropertyChange(string? observedPropertyName, string? changedPropertyName)
    {
        if (string.IsNullOrWhiteSpace(observedPropertyName) || string.IsNullOrWhiteSpace(changedPropertyName))
        {
            return true;
        }

        return string.Equals(observedPropertyName, changedPropertyName, StringComparison.Ordinal);
    }

    public static BindingGroup? ResolveBindingGroup(DependencyObject target, string? bindingGroupName)
    {
        if (target is not UIElement targetElement)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(bindingGroupName))
        {
            return target switch
            {
                FrameworkElement targetFrameworkElement => targetFrameworkElement.GetValue<BindingGroup>(FrameworkElement.BindingGroupProperty),
                _ => FindNearestBindingGroup(targetElement)
            };
        }

        return FindNamedBindingGroup(targetElement, bindingGroupName);
    }

    public static ValidationError CreateExceptionValidationError(
        BindingBase binding,
        object bindingExpression,
        Exception exception,
        UpdateSourceExceptionFilterCallback? exceptionFilter)
    {
        if (exceptionFilter != null)
        {
            var filtered = exceptionFilter(bindingExpression, exception);
            if (filtered is ValidationError validationError)
            {
                return validationError;
            }

            if (filtered != null)
            {
                return new ValidationError(null, binding, filtered);
            }
        }

        return new ValidationError(null, binding, exception);
    }

    private static object? ResolveElementNameSource(DependencyObject target, string name)
    {
        if (target is not FrameworkElement targetElement)
        {
            return null;
        }

        var root = GetElementTreeRoot(targetElement);
        return root?.FindName(name);
    }

    private static object? ResolveRelativeSource(DependencyObject target, Binding binding)
    {
        if (target is not UIElement targetElement)
        {
            return null;
        }

        if (binding.RelativeSourceMode == RelativeSourceMode.Self)
        {
            return target;
        }

        if (binding.RelativeSourceMode == RelativeSourceMode.TemplatedParent)
        {
            for (var current = targetElement.VisualParent; current != null; current = current.VisualParent)
            {
                if (current is Control control)
                {
                    return control;
                }
            }

            return null;
        }

        if (binding.RelativeSourceMode == RelativeSourceMode.FindAncestor)
        {
            var ancestorType = binding.RelativeSourceAncestorType ?? typeof(UIElement);
            var remainingMatches = Math.Max(1, binding.RelativeSourceAncestorLevel);

            for (var current = targetElement.VisualParent; current != null; current = current.VisualParent)
            {
                if (!ancestorType.IsInstanceOfType(current))
                {
                    continue;
                }

                remainingMatches--;
                if (remainingMatches == 0)
                {
                    return current;
                }
            }
        }

        return null;
    }

    private static FrameworkElement? GetElementTreeRoot(FrameworkElement element)
    {
        UIElement current = element;
        UIElement? next = GetTreeParent(current);
        while (next != null)
        {
            current = next;
            next = GetTreeParent(current);
        }

        return current as FrameworkElement;
    }

    private static BindingGroup? FindNearestBindingGroup(UIElement targetElement)
    {
        for (var current = targetElement; current != null; current = GetTreeParent(current))
        {
            if (current is not FrameworkElement frameworkElement)
            {
                continue;
            }

            var group = frameworkElement.GetValue<BindingGroup>(FrameworkElement.BindingGroupProperty);
            if (group != null)
            {
                return group;
            }
        }

        return null;
    }

    private static BindingGroup? FindNamedBindingGroup(UIElement targetElement, string bindingGroupName)
    {
        for (var current = targetElement; current != null; current = GetTreeParent(current))
        {
            if (current is not FrameworkElement frameworkElement)
            {
                continue;
            }

            var group = frameworkElement.GetValue<BindingGroup>(FrameworkElement.BindingGroupProperty);
            if (group != null && string.Equals(group.Name, bindingGroupName, StringComparison.Ordinal))
            {
                return group;
            }
        }

        return null;
    }

    private static UIElement? GetTreeParent(UIElement element)
    {
        return element.VisualParent ?? element.LogicalParent;
    }
}
