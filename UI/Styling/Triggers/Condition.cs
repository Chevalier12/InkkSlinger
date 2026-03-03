using System;
using System.Threading;

namespace InkkSlinger;

public sealed class Condition
{
    private static int _propertyId;
    private DependencyProperty? _observedValueProperty;

    public DependencyProperty? Property { get; set; }

    public Binding? Binding { get; set; }

    public object? Value { get; set; }

    internal void ValidateForMultiTrigger(int index)
    {
        if (Property == null || Binding != null)
        {
            throw new InvalidOperationException(
                $"MultiTrigger condition at index {index} requires Property and forbids Binding.");
        }
    }

    internal void ValidateForMultiDataTrigger(int index)
    {
        if (Binding == null || Property != null)
        {
            throw new InvalidOperationException(
                $"MultiDataTrigger condition at index {index} requires Binding and forbids Property.");
        }
    }

    internal DependencyProperty GetOrCreateObservedValueProperty()
    {
        if (_observedValueProperty != null)
        {
            return _observedValueProperty;
        }

        _observedValueProperty = DependencyProperty.RegisterAttached(
            "ConditionObservedValue_" + Interlocked.Increment(ref _propertyId),
            typeof(object),
            typeof(Condition),
            new FrameworkPropertyMetadata(null));
        return _observedValueProperty;
    }

    internal static Binding CloneBinding(Binding source)
    {
        return new Binding
        {
            Path = source.Path,
            Source = source.Source,
            ElementName = source.ElementName,
            RelativeSourceMode = source.RelativeSourceMode,
            RelativeSourceAncestorType = source.RelativeSourceAncestorType,
            RelativeSourceAncestorLevel = source.RelativeSourceAncestorLevel,
            Mode = source.Mode,
            UpdateSourceTrigger = source.UpdateSourceTrigger,
            FallbackValue = source.FallbackValue,
            TargetNullValue = source.TargetNullValue
        };
    }
}
