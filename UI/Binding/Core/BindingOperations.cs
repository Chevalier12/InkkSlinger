using System.Collections.Generic;

namespace InkkSlinger;

public static class BindingOperations
{
    private static readonly Dictionary<(DependencyObject Target, DependencyProperty Property), IBindingExpression> ActiveBindings = new();

    public static BindingExpression SetBinding(
        DependencyObject target,
        DependencyProperty dependencyProperty,
        Binding binding)
    {
        return (BindingExpression)SetBinding(target, dependencyProperty, (BindingBase)binding);
    }

    public static IBindingExpression SetBinding(
        DependencyObject target,
        DependencyProperty dependencyProperty,
        BindingBase binding)
    {
        ClearBinding(target, dependencyProperty);

        IBindingExpression expression = binding switch
        {
            Binding singleBinding => new BindingExpression(target, dependencyProperty, singleBinding),
            MultiBinding multiBinding => new MultiBindingExpression(target, dependencyProperty, multiBinding),
            PriorityBinding priorityBinding => new PriorityBindingExpression(target, dependencyProperty, priorityBinding),
            _ => throw new System.NotSupportedException($"Binding type '{binding.GetType().Name}' is not supported.")
        };

        ActiveBindings[(target, dependencyProperty)] = expression;
        return expression;
    }

    public static void ClearBinding(DependencyObject target, DependencyProperty dependencyProperty)
    {
        if (ActiveBindings.Remove((target, dependencyProperty), out var expression))
        {
            expression.Dispose();
        }
    }

    public static void UpdateTarget(DependencyObject target, DependencyProperty dependencyProperty)
    {
        if (ActiveBindings.TryGetValue((target, dependencyProperty), out var expression))
        {
            expression.UpdateTarget();
        }
    }

    public static void UpdateSource(DependencyObject target, DependencyProperty dependencyProperty)
    {
        if (ActiveBindings.TryGetValue((target, dependencyProperty), out var expression))
        {
            expression.UpdateSource();
        }
    }

    internal static void NotifyTargetTreeChanged(DependencyObject target)
    {
        var matchingExpressions = new List<IBindingExpression>();

        foreach (var pair in ActiveBindings)
        {
            if (!ReferenceEquals(pair.Key.Target, target))
            {
                continue;
            }

            matchingExpressions.Add(pair.Value);
        }

        foreach (var expression in matchingExpressions)
        {
            expression.OnTargetTreeChanged();
        }
    }

    internal static void NotifyTargetTreeChangedRecursive(UIElement root)
    {
        var visited = new HashSet<UIElement>();
        NotifyTargetTreeChangedRecursive(root, visited);
    }

    private static void NotifyTargetTreeChangedRecursive(UIElement element, HashSet<UIElement> visited)
    {
        if (!visited.Add(element))
        {
            return;
        }

        NotifyTargetTreeChanged(element);

        foreach (var child in element.GetVisualChildren())
        {
            NotifyTargetTreeChangedRecursive(child, visited);
        }

        foreach (var child in element.GetLogicalChildren())
        {
            NotifyTargetTreeChangedRecursive(child, visited);
        }
    }
}
