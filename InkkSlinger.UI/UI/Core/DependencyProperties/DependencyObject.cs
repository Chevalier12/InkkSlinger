using System;
using System.Collections.Generic;

namespace InkkSlinger;

public abstract class DependencyObject
{
    public static readonly object UnsetValue = new();

    [ThreadStatic]
    private static List<ActiveValueResolution>? _activeValueResolutions;
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

    internal bool HasStoredValueEntry(DependencyProperty dependencyProperty)
    {
        ValidatePropertyApplicability(dependencyProperty);
        return _values.ContainsKey(dependencyProperty);
    }

    private void SetValueInternal(DependencyProperty dependencyProperty, ValueLayer valueLayer, object? value)
    {
        Dispatcher.VerifyAccess();

        if (!ReferenceEquals(value, UnsetValue))
        {
            if (DependencyValueCoercion.TryCoerce(value, dependencyProperty.PropertyType, out var coercedValue))
            {
                value = coercedValue;
            }

            ValidatePropertyValue(dependencyProperty, value);
            value = CoerceValue(dependencyProperty, value);
            if (DependencyValueCoercion.TryCoerce(value, dependencyProperty.PropertyType, out coercedValue))
            {
                value = coercedValue;
            }

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

        if (!ShouldPersistEntry(entry))
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
        entry.HasCachedEffectiveValue = true;

        if (ShouldPersistEntry(entry))
        {
            _values[dependencyProperty] = entry;
        }
        else
        {
            _values.Remove(dependencyProperty);
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
        if (TryComputeNonInheritedEffectiveValue(entry, out var value, out var source))
        {
        }
        else
        {
            var metadata = dependencyProperty.GetMetadata(this);
            if (metadata.Inherits && this is UIElement element)
            {
                var foundParentValue = false;
                value = null;
                source = DependencyPropertyValueSource.Default;
                var visited = new HashSet<UIElement> { element };

                for (var parent = UIElement.GetTreeParent(element); parent != null; parent = UIElement.GetTreeParent(parent))
                {
                    if (!visited.Add(parent))
                    {
                        break;
                    }

                    if (!dependencyProperty.IsApplicableTo(parent))
                    {
                        continue;
                    }

                    value = parent.GetValue(dependencyProperty);
                    source = DependencyPropertyValueSource.Inherited;
                    foundParentValue = true;
                    break;
                }

                if (!foundParentValue)
                {
                    value = metadata.DefaultValue;
                    source = DependencyPropertyValueSource.Default;
                }
            }
            else
            {
                value = metadata.DefaultValue;
                source = DependencyPropertyValueSource.Default;
            }
        }

        // WPF-like behavior: IsEnabled cannot be true if any visual ancestor is disabled.
        // (Without this, a child can SetValue(IsEnabled=true) and re-enable itself under a disabled parent.)
        if (ReferenceEquals(dependencyProperty, UIElement.IsEnabledProperty) &&
            this is UIElement enabledElement &&
            value is bool isEnabled &&
            isEnabled)
        {
            // If this value already came from inheritance, parent effective IsEnabled already
            // encodes the full ancestor chain constraint, so no extra walk is needed.
            if (source == DependencyPropertyValueSource.Inherited)
            {
                return (value, source);
            }

            // For non-inherited true values (local/style/template), immediate parent is sufficient:
            // parent effective IsEnabled itself already reflects all of its ancestors.
            var parent = UIElement.GetTreeParent(enabledElement);
            if (parent != null && !parent.IsEnabled)
            {
                value = false;
                source = DependencyPropertyValueSource.Inherited;
            }
        }

        return (value, source);
    }

    private (object? Value, DependencyPropertyValueSource Source) GetValueWithSource(DependencyProperty dependencyProperty)
    {
        _values.TryGetValue(dependencyProperty, out var entry);

        if (entry.HasCachedEffectiveValue)
        {
            return (entry.EffectiveValue, entry.EffectiveSource);
        }

        var activeValueResolutions = _activeValueResolutions ??= new List<ActiveValueResolution>();
        if (IsValueResolutionActive(activeValueResolutions, dependencyProperty))
        {
            return ResolveReentrantEffectiveValue(dependencyProperty, entry);
        }

        activeValueResolutions.Add(new ActiveValueResolution(this, dependencyProperty));
        (object? Value, DependencyPropertyValueSource Source) resolved;
        try
        {
            resolved = ComputeEffectiveValue(dependencyProperty, entry);
        }
        finally
        {
            activeValueResolutions.RemoveAt(activeValueResolutions.Count - 1);
        }

        var (effective, source) = resolved;

        entry.EffectiveValue = effective;
        entry.EffectiveSource = source;
        entry.HasCachedEffectiveValue = true;

        if (ShouldPersistEntry(entry))
        {
            _values[dependencyProperty] = entry;
        }
        else
        {
            _values.Remove(dependencyProperty);
        }

        return (effective, source);
    }

    private (object? Value, DependencyPropertyValueSource Source) ResolveReentrantEffectiveValue(
        DependencyProperty dependencyProperty,
        EffectiveValueEntry entry)
    {
        if (TryComputeNonInheritedEffectiveValue(entry, out var value, out var source))
        {
            return (value, source);
        }

        if (entry.HasCachedEffectiveValue)
        {
            return (entry.EffectiveValue, entry.EffectiveSource);
        }

        var metadata = dependencyProperty.GetMetadata(this);
        return (metadata.DefaultValue, DependencyPropertyValueSource.Default);
    }

    private static bool TryComputeNonInheritedEffectiveValue(
        EffectiveValueEntry entry,
        out object? value,
        out DependencyPropertyValueSource source)
    {
        if (entry.HasAnimationValue)
        {
            value = entry.AnimationValue;
            source = DependencyPropertyValueSource.Animation;
            return true;
        }

        if (entry.HasLocalValue)
        {
            value = entry.LocalValue;
            source = DependencyPropertyValueSource.Local;
            return true;
        }

        if (entry.HasTemplateTriggerValue)
        {
            value = entry.TemplateTriggerValue;
            source = DependencyPropertyValueSource.TemplateTrigger;
            return true;
        }

        if (entry.HasStyleTriggerValue)
        {
            value = entry.StyleTriggerValue;
            source = DependencyPropertyValueSource.StyleTrigger;
            return true;
        }

        if (entry.HasTemplateValue)
        {
            value = entry.TemplateValue;
            source = DependencyPropertyValueSource.Template;
            return true;
        }

        if (entry.HasStyleValue)
        {
            value = entry.StyleValue;
            source = DependencyPropertyValueSource.Style;
            return true;
        }

        value = null;
        source = DependencyPropertyValueSource.Default;
        return false;
    }

    private bool IsValueResolutionActive(List<ActiveValueResolution> activeValueResolutions, DependencyProperty dependencyProperty)
    {
        for (var i = 0; i < activeValueResolutions.Count; i++)
        {
            var active = activeValueResolutions[i];
            if (ReferenceEquals(active.Object, this) && ReferenceEquals(active.Property, dependencyProperty))
            {
                return true;
            }
        }

        return false;
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

    private static bool ShouldPersistEntry(EffectiveValueEntry entry)
    {
        return entry.HasAnyValue || entry.HasCachedEffectiveValue;
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
        public bool HasCachedEffectiveValue;

        public bool HasAnyValue =>
            HasLocalValue || HasStyleValue || HasStyleTriggerValue || HasTemplateTriggerValue || HasAnimationValue || HasTemplateValue;
    }

    private readonly struct ActiveValueResolution
    {
        public ActiveValueResolution(DependencyObject obj, DependencyProperty property)
        {
            Object = obj;
            Property = property;
        }

        public DependencyObject Object { get; }

        public DependencyProperty Property { get; }
    }
}
