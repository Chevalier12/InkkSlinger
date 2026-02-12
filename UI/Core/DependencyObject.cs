using System;
using System.Collections.Generic;

namespace InkkSlinger;

public abstract class DependencyObject
{
    public static readonly object UnsetValue = new();

    private readonly Dictionary<DependencyProperty, EffectiveValueEntry> _values = new();

    public event EventHandler<DependencyPropertyChangedEventArgs>? DependencyPropertyChanged;

    public object? GetValue(DependencyProperty dependencyProperty)
    {
        ValidatePropertyApplicability(dependencyProperty);
        return GetValueWithSource(dependencyProperty).Value;
    }

    public T GetValue<T>(DependencyProperty dependencyProperty)
    {
        var value = GetValue(dependencyProperty);
        if (value is T typed)
        {
            return typed;
        }

        if (value == null)
        {
            return default!;
        }

        return (T)value;
    }

    public DependencyPropertyValueSource GetValueSource(DependencyProperty dependencyProperty)
    {
        ValidatePropertyApplicability(dependencyProperty);
        return GetValueWithSource(dependencyProperty).Source;
    }

    public void SetValue(DependencyProperty dependencyProperty, object? value)
    {
        Dispatcher.VerifyAccess();
        ValidatePropertyApplicability(dependencyProperty);
        SetValueInternal(dependencyProperty, ValueLayer.Local, value);
    }

    public void ClearValue(DependencyProperty dependencyProperty)
    {
        Dispatcher.VerifyAccess();
        ValidatePropertyApplicability(dependencyProperty);
        SetValueInternal(dependencyProperty, ValueLayer.Local, UnsetValue);
    }

    public object? ReadLocalValue(DependencyProperty dependencyProperty)
    {
        ValidatePropertyApplicability(dependencyProperty);

        if (_values.TryGetValue(dependencyProperty, out var entry) && entry.HasLocalValue)
        {
            return entry.LocalValue;
        }

        return UnsetValue;
    }

    public bool HasLocalValue(DependencyProperty dependencyProperty)
    {
        ValidatePropertyApplicability(dependencyProperty);
        return _values.TryGetValue(dependencyProperty, out var entry) && entry.HasLocalValue;
    }

    protected virtual void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        DependencyPropertyChanged?.Invoke(this, args);
    }

    internal void SetStyleValue(DependencyProperty dependencyProperty, object? value)
    {
        ValidatePropertyApplicability(dependencyProperty);
        SetValueInternal(dependencyProperty, ValueLayer.Style, value);
    }

    internal void ClearStyleValue(DependencyProperty dependencyProperty)
    {
        ValidatePropertyApplicability(dependencyProperty);
        SetValueInternal(dependencyProperty, ValueLayer.Style, UnsetValue);
    }

    internal void SetStyleTriggerValue(DependencyProperty dependencyProperty, object? value)
    {
        ValidatePropertyApplicability(dependencyProperty);
        SetValueInternal(dependencyProperty, ValueLayer.StyleTrigger, value);
    }

    internal void ClearStyleTriggerValue(DependencyProperty dependencyProperty)
    {
        ValidatePropertyApplicability(dependencyProperty);
        SetValueInternal(dependencyProperty, ValueLayer.StyleTrigger, UnsetValue);
    }

    internal void SetTemplateValue(DependencyProperty dependencyProperty, object? value)
    {
        ValidatePropertyApplicability(dependencyProperty);
        SetValueInternal(dependencyProperty, ValueLayer.Template, value);
    }

    internal void ClearTemplateValue(DependencyProperty dependencyProperty)
    {
        ValidatePropertyApplicability(dependencyProperty);
        SetValueInternal(dependencyProperty, ValueLayer.Template, UnsetValue);
    }

    internal void SetTemplateTriggerValue(DependencyProperty dependencyProperty, object? value)
    {
        ValidatePropertyApplicability(dependencyProperty);
        SetValueInternal(dependencyProperty, ValueLayer.TemplateTrigger, value);
    }

    internal void ClearTemplateTriggerValue(DependencyProperty dependencyProperty)
    {
        ValidatePropertyApplicability(dependencyProperty);
        SetValueInternal(dependencyProperty, ValueLayer.TemplateTrigger, UnsetValue);
    }

    internal void SetAnimationValue(DependencyProperty dependencyProperty, object? value)
    {
        ValidatePropertyApplicability(dependencyProperty);
        SetValueInternal(dependencyProperty, ValueLayer.Animation, value);
    }

    internal void ClearAnimationValue(DependencyProperty dependencyProperty)
    {
        ValidatePropertyApplicability(dependencyProperty);
        SetValueInternal(dependencyProperty, ValueLayer.Animation, UnsetValue);
    }

    internal void NotifyInheritedPropertyChanged(DependencyProperty dependencyProperty)
    {
        if (!dependencyProperty.IsApplicableTo(this))
        {
            return;
        }

        _values.TryGetValue(dependencyProperty, out var previousEntry);
        EvaluateEffectiveValue(dependencyProperty, previousEntry);
    }

    private void SetValueInternal(DependencyProperty dependencyProperty, ValueLayer valueLayer, object? value)
    {
        Dispatcher.VerifyAccess();

        if (!ReferenceEquals(value, UnsetValue))
        {
            ValidatePropertyValue(dependencyProperty, value);
            value = CoerceValue(dependencyProperty, value);
            ValidatePropertyValue(dependencyProperty, value);
        }

        _values.TryGetValue(dependencyProperty, out var entry);
        if (IsLayerValueUnchanged(entry, valueLayer, value))
        {
            return;
        }

        var (oldValue, oldSource) = GetValueWithSource(dependencyProperty);
        var previousEntry = entry;
        previousEntry.EffectiveValue = oldValue;
        previousEntry.EffectiveSource = oldSource;
        SetLayerValue(ref entry, valueLayer, value);

        if (!entry.HasAnyValue)
        {
            _values.Remove(dependencyProperty);
        }
        else
        {
            _values[dependencyProperty] = entry;
        }

        EvaluateEffectiveValue(dependencyProperty, previousEntry);
    }

    private void EvaluateEffectiveValue(DependencyProperty dependencyProperty, EffectiveValueEntry previousEntry)
    {
        _values.TryGetValue(dependencyProperty, out var entry);

        var oldEffective = previousEntry.EffectiveValue;
        var oldSource = previousEntry.EffectiveSource;

        var (newEffective, newSource) = ComputeEffectiveValue(dependencyProperty, entry);
        if (Equals(oldEffective, newEffective) && oldSource == newSource)
        {
            return;
        }

        entry.EffectiveValue = newEffective;
        entry.EffectiveSource = newSource;

        if (entry.HasAnyValue)
        {
            _values[dependencyProperty] = entry;
        }

        var args = new DependencyPropertyChangedEventArgs(dependencyProperty, oldEffective, newEffective);
        var metadata = dependencyProperty.GetMetadata(this);
        metadata.PropertyChangedCallback?.Invoke(this, args);
        OnDependencyPropertyChanged(args);
    }

    private (object? Value, DependencyPropertyValueSource Source) ComputeEffectiveValue(
        DependencyProperty dependencyProperty,
        EffectiveValueEntry entry)
    {
        if (entry.HasAnimationValue)
        {
            return (entry.AnimationValue, DependencyPropertyValueSource.Animation);
        }

        if (entry.HasLocalValue)
        {
            return (entry.LocalValue, DependencyPropertyValueSource.Local);
        }

        if (entry.HasTemplateTriggerValue)
        {
            return (entry.TemplateTriggerValue, DependencyPropertyValueSource.TemplateTrigger);
        }

        if (entry.HasStyleTriggerValue)
        {
            return (entry.StyleTriggerValue, DependencyPropertyValueSource.StyleTrigger);
        }

        if (entry.HasTemplateValue)
        {
            return (entry.TemplateValue, DependencyPropertyValueSource.Template);
        }

        if (entry.HasStyleValue)
        {
            return (entry.StyleValue, DependencyPropertyValueSource.Style);
        }

        var metadata = dependencyProperty.GetMetadata(this);
        if (metadata.Inherits && this is UIElement element)
        {
            for (var parent = element.VisualParent; parent != null; parent = parent.VisualParent)
            {
                if (!dependencyProperty.IsApplicableTo(parent))
                {
                    continue;
                }

                return (parent.GetValue(dependencyProperty), DependencyPropertyValueSource.Inherited);
            }
        }

        return (metadata.DefaultValue, DependencyPropertyValueSource.Default);
    }

    private (object? Value, DependencyPropertyValueSource Source) GetValueWithSource(DependencyProperty dependencyProperty)
    {
        _values.TryGetValue(dependencyProperty, out var entry);
        var (effective, source) = ComputeEffectiveValue(dependencyProperty, entry);

        if (entry.HasAnyValue)
        {
            entry.EffectiveValue = effective;
            entry.EffectiveSource = source;
            _values[dependencyProperty] = entry;
        }

        return (effective, source);
    }

    private object? CoerceValue(DependencyProperty dependencyProperty, object? value)
    {
        var metadata = dependencyProperty.GetMetadata(this);
        if (metadata.CoerceValueCallback != null)
        {
            return metadata.CoerceValueCallback(this, value);
        }

        return value;
    }

    private void ValidatePropertyApplicability(DependencyProperty dependencyProperty)
    {
        if (!dependencyProperty.IsApplicableTo(this))
        {
            throw new InvalidOperationException(
                $"Dependency property {dependencyProperty} is not applicable to {GetType().Name}.");
        }
    }

    private static void SetLayerValue(ref EffectiveValueEntry entry, ValueLayer layer, object? value)
    {
        var hasValue = !ReferenceEquals(value, UnsetValue);

        switch (layer)
        {
            case ValueLayer.Local:
                entry.HasLocalValue = hasValue;
                entry.LocalValue = hasValue ? value : null;
                break;
            case ValueLayer.Style:
                entry.HasStyleValue = hasValue;
                entry.StyleValue = hasValue ? value : null;
                break;
            case ValueLayer.StyleTrigger:
                entry.HasStyleTriggerValue = hasValue;
                entry.StyleTriggerValue = hasValue ? value : null;
                break;
            case ValueLayer.TemplateTrigger:
                entry.HasTemplateTriggerValue = hasValue;
                entry.TemplateTriggerValue = hasValue ? value : null;
                break;
            case ValueLayer.Animation:
                entry.HasAnimationValue = hasValue;
                entry.AnimationValue = hasValue ? value : null;
                break;
            case ValueLayer.Template:
                entry.HasTemplateValue = hasValue;
                entry.TemplateValue = hasValue ? value : null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(layer), layer, null);
        }
    }

    private static void ValidatePropertyValue(DependencyProperty dependencyProperty, object? value)
    {
        if (value == null)
        {
            if (dependencyProperty.PropertyType.IsValueType && Nullable.GetUnderlyingType(dependencyProperty.PropertyType) == null)
            {
                throw new ArgumentException(
                    $"Null is not a valid value for {dependencyProperty} of type {dependencyProperty.PropertyType.Name}.");
            }
        }
        else if (!dependencyProperty.PropertyType.IsInstanceOfType(value))
        {
            throw new ArgumentException(
                $"Value for {dependencyProperty} must be of type {dependencyProperty.PropertyType.Name}.");
        }

        if (dependencyProperty.ValidateValueCallback != null &&
            !dependencyProperty.ValidateValueCallback(value))
        {
            throw new ArgumentException($"Validation failed for dependency property {dependencyProperty}.");
        }
    }

    private static bool IsLayerValueUnchanged(EffectiveValueEntry entry, ValueLayer layer, object? value)
    {
        var hasValue = !ReferenceEquals(value, UnsetValue);
        return layer switch
        {
            ValueLayer.Local => entry.HasLocalValue == hasValue && Equals(entry.LocalValue, value),
            ValueLayer.Style => entry.HasStyleValue == hasValue && Equals(entry.StyleValue, value),
            ValueLayer.StyleTrigger => entry.HasStyleTriggerValue == hasValue && Equals(entry.StyleTriggerValue, value),
            ValueLayer.TemplateTrigger => entry.HasTemplateTriggerValue == hasValue && Equals(entry.TemplateTriggerValue, value),
            ValueLayer.Animation => entry.HasAnimationValue == hasValue && Equals(entry.AnimationValue, value),
            ValueLayer.Template => entry.HasTemplateValue == hasValue && Equals(entry.TemplateValue, value),
            _ => false
        };
    }

    private enum ValueLayer
    {
        Local,
        Style,
        StyleTrigger,
        TemplateTrigger,
        Animation,
        Template
    }

    private struct EffectiveValueEntry
    {
        public bool HasLocalValue;
        public object? LocalValue;

        public bool HasStyleValue;
        public object? StyleValue;

        public bool HasStyleTriggerValue;
        public object? StyleTriggerValue;

        public bool HasTemplateTriggerValue;
        public object? TemplateTriggerValue;

        public bool HasAnimationValue;
        public object? AnimationValue;

        public bool HasTemplateValue;
        public object? TemplateValue;

        public object? EffectiveValue;
        public DependencyPropertyValueSource EffectiveSource;

        public bool HasAnyValue =>
            HasLocalValue || HasStyleValue || HasStyleTriggerValue || HasTemplateTriggerValue || HasAnimationValue || HasTemplateValue;
    }
}
