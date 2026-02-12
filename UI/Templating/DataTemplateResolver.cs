using System;
using System.Collections.Generic;

namespace InkkSlinger;

internal static class DataTemplateResolver
{
    public static DataTemplate? ResolveTemplateForContent(
        FrameworkElement? scope,
        object? content,
        DataTemplate? explicitTemplate,
        DataTemplateSelector? selector,
        DependencyObject container)
    {
        if (explicitTemplate != null)
        {
            return explicitTemplate;
        }

        if (selector != null)
        {
            var selected = selector.SelectTemplate(content, container);
            if (selected != null)
            {
                return selected;
            }
        }

        return ResolveImplicitTemplate(scope, content);
    }

    public static DataTemplate? ResolveImplicitTemplate(FrameworkElement? scope, object? item)
    {
        if (item == null || scope == null)
        {
            return null;
        }

        var itemType = item.GetType();
        foreach (var key in EnumerateTypeLookupKeys(itemType))
        {
            if (scope.TryFindResource(key, out var resource) && resource is DataTemplate byKey)
            {
                return byKey;
            }
        }

        foreach (var dictionary in EnumerateResourceScopes(scope))
        {
            DataTemplate? best = null;
            var bestDistance = int.MaxValue;
            foreach (var entry in dictionary)
            {
                if (entry.Value is not DataTemplate template || template.DataType == null)
                {
                    continue;
                }

                if (!template.DataType.IsAssignableFrom(itemType))
                {
                    continue;
                }

                var distance = GetInheritanceDistance(itemType, template.DataType);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = template;
                }
            }

            if (best != null)
            {
                return best;
            }
        }

        return null;
    }

    private static IEnumerable<Type> EnumerateTypeLookupKeys(Type itemType)
    {
        for (var current = itemType; current != null; current = current.BaseType)
        {
            yield return current;
        }
    }

    private static IEnumerable<ResourceDictionary> EnumerateResourceScopes(FrameworkElement scope)
    {
        var visited = new HashSet<ResourceDictionary>();
        for (UIElement? current = scope; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is not FrameworkElement framework)
            {
                continue;
            }

            if (visited.Add(framework.Resources))
            {
                yield return framework.Resources;
            }
        }

        if (visited.Add(UiApplication.Current.Resources))
        {
            yield return UiApplication.Current.Resources;
        }
    }

    private static int GetInheritanceDistance(Type type, Type candidate)
    {
        if (type == candidate)
        {
            return 0;
        }

        var distance = 0;
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current == candidate)
            {
                return distance;
            }

            distance++;
        }

        return int.MaxValue / 2;
    }
}
