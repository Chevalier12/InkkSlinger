using System;

namespace InkkSlinger;

public sealed class SetValueAction : TriggerAction
{
    public SetValueAction(DependencyProperty property, object? value)
    {
        Property = property ?? throw new ArgumentNullException(nameof(property));
        Value = value;
    }

    public DependencyProperty Property { get; }

    public object? Value { get; }

    public override void Invoke(DependencyObject target)
    {
        target.SetValue(Property, Value);
    }
}
