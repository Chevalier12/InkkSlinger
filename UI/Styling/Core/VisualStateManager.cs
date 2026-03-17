namespace InkkSlinger;

public static class VisualStateManager
{
    public static readonly DependencyProperty VisualStateGroupsProperty =
        DependencyProperty.RegisterAttached(
            "VisualStateGroups",
            typeof(VisualStateGroupCollection),
            typeof(VisualStateManager),
            new FrameworkPropertyMetadata(null));

    public static VisualStateGroupCollection GetVisualStateGroups(DependencyObject dependencyObject)
    {
        if (dependencyObject.GetValue(VisualStateGroupsProperty) is VisualStateGroupCollection groups)
        {
            return groups;
        }

        groups = new VisualStateGroupCollection();
        dependencyObject.SetValue(VisualStateGroupsProperty, groups);
        return groups;
    }

    public static void SetVisualStateGroups(DependencyObject dependencyObject, VisualStateGroupCollection? value)
    {
        dependencyObject.SetValue(VisualStateGroupsProperty, value);
    }

    public static bool GoToState(Control control, string stateName, bool useTransitions = true)
    {
        _ = useTransitions;

        if (TryGetStateHost(control, out var stateHost) && stateHost != null)
        {
            var context = new TriggerActionContext(
                control,
                stateHost,
                name => control.FindTemplateNamedObject(name));
            if (TryGoToState(stateHost, context, stateName))
            {
                return true;
            }
        }

        if (control is FrameworkElement frameworkElement)
        {
            return GoToElementState(frameworkElement, stateName, useTransitions);
        }

        return false;
    }

    public static bool GoToElementState(FrameworkElement element, string stateName, bool useTransitions = true)
    {
        _ = useTransitions;

        var context = new TriggerActionContext(
            element,
            element,
            name => NameScopeService.FindName(element, name) ?? element.FindName(name));
        return TryGoToState(element, context, stateName);
    }

    internal static void ClearState(FrameworkElement? stateHost)
    {
        if (stateHost == null)
        {
            return;
        }

        if (stateHost.GetValue(VisualStateGroupsProperty) is not VisualStateGroupCollection groups)
        {
            return;
        }

        for (var i = 0; i < groups.Count; i++)
        {
            groups[i].ClearState(stateHost);
        }
    }

    private static bool TryGetStateHost(Control control, out FrameworkElement? stateHost)
    {
        foreach (var child in control.GetVisualChildren())
        {
            if (child is FrameworkElement frameworkElement)
            {
                stateHost = frameworkElement;
                return true;
            }
        }

        stateHost = null;
        return false;
    }

    private static bool TryGoToState(DependencyObject target, TriggerActionContext context, string stateName)
    {
        if (target.GetValue(VisualStateGroupsProperty) is not VisualStateGroupCollection groups)
        {
            return false;
        }

        var matched = false;
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (!group.ContainsState(stateName))
            {
                continue;
            }

            matched = group.GoToState(context, stateName) || matched;
        }

        return matched;
    }
}