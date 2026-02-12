namespace InkkSlinger;

public static class FrameworkElementExtensions
{
    public static FrameworkElement? FindName(this FrameworkElement element, string name)
    {
        if (NameScopeService.FindName(element, name) is FrameworkElement inScope)
        {
            return inScope;
        }

        if (string.Equals(element.Name, name, System.StringComparison.Ordinal))
        {
            return element;
        }

        foreach (var child in element.GetLogicalChildren())
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            var found = frameworkChild.FindName(name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
