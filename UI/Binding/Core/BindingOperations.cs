using System.Collections.Generic;

namespace InkkSlinger;

public static class BindingOperations
{
    private static readonly Dictionary<(DependencyObject Target, DependencyProperty Property), BindingExpression> ActiveBindings = new();

    public static BindingExpression SetBinding(
        DependencyObject target,
        DependencyProperty dependencyProperty,
        Binding binding)
    {
        ClearBinding(target, dependencyProperty);

        var expression = new BindingExpression(target, dependencyProperty, binding);
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
        var matchingExpressions = new List<BindingExpression>();

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
}
