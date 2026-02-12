namespace InkkSlinger;

internal static class NameScopeService
{
    public static object? FindName(FrameworkElement? start, string name)
    {
        if (start == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        for (FrameworkElement? current = start; current != null; current = current.VisualParent as FrameworkElement ?? current.LogicalParent as FrameworkElement)
        {
            var found = current.GetLocalNameScope()?.FindName(name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
