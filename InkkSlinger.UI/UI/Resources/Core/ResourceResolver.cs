using System.Collections.Generic;

namespace InkkSlinger;

public static class ResourceResolver
{
    public static bool TryFindResource(FrameworkElement element, object key, out object? resource)
    {
        return TryFindResource(element, key, out resource, includeApplicationResources: true);
    }

    internal static bool TryFindResource(
        FrameworkElement element,
        object key,
        out object? resource,
        bool includeApplicationResources)
    {
        if (element.Resources.TryGetValue(key, out var local))
        {
            resource = local;
            return true;
        }

        var visited = new HashSet<UIElement> { element };

        for (var current = element.VisualParent; current != null; current = current.VisualParent)
        {
            if (!visited.Add(current))
            {
                break;
            }

            if (current is FrameworkElement framework && framework.Resources.TryGetValue(key, out var value))
            {
                resource = value;
                return true;
            }
        }

        for (var current = element.LogicalParent; current != null; current = current.LogicalParent)
        {
            if (!visited.Add(current))
            {
                break;
            }

            if (current is not FrameworkElement framework)
            {
                continue;
            }

            if (framework.Resources.TryGetValue(key, out var value))
            {
                resource = value;
                return true;
            }
        }

        if (includeApplicationResources &&
            UiApplication.Current.Resources.TryGetValue(key, out var applicationResource))
        {
            resource = applicationResource;
            return true;
        }

        resource = null;
        return false;
    }
}
