using System.Collections.Generic;

namespace InkkSlinger;

public sealed class Trigger : TriggerBase
{
    public Trigger(DependencyProperty property, object? value)
    {
        Property = property;
        Value = value;
    }

    public DependencyProperty Property { get; }

    public object? Value { get; }

    public override bool IsMatch(DependencyObject target)
    {
        return Equals(target.GetValue(Property), Value);
    }

    public override void CollectConditionProperties(ISet<DependencyProperty> conditionProperties)
    {
        conditionProperties.Add(Property);
    }
}
