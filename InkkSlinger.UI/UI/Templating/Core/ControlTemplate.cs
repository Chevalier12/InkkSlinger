using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class ControlTemplate
{
    private readonly List<TemplateBinding> _bindings = new();
    private readonly List<TriggerBase> _triggers = new();

    public ControlTemplate(System.Func<Control, UIElement> factory)
    {
        Factory = factory;
    }

    public Type? TargetType { get; set; }

    public System.Func<Control, UIElement> Factory { get; }

    public IReadOnlyList<TemplateBinding> Bindings => _bindings;

    public IList<TriggerBase> Triggers => _triggers;

    public UIElement Build(Control owner)
    {
        return Factory(owner);
    }

    public ControlTemplate BindTemplate(
        string targetName,
        DependencyProperty targetProperty,
        DependencyProperty sourceProperty)
    {
        _bindings.Add(new TemplateBinding(targetName, targetProperty, sourceProperty, null, null));
        return this;
    }

    public ControlTemplate BindTemplate(
        string targetName,
        DependencyProperty targetProperty,
        DependencyProperty sourceProperty,
        object? fallbackValue,
        object? targetNullValue = null)
    {
        _bindings.Add(new TemplateBinding(targetName, targetProperty, sourceProperty, fallbackValue, targetNullValue));
        return this;
    }
}
