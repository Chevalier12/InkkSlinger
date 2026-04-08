using System;
using System.Collections.Generic;
using System.Threading;

namespace InkkSlinger;

public sealed class DataTrigger : TriggerBase
{
    private static readonly object SyncRoot = new();
    private static int _propertyId;
    private static readonly Dictionary<DependencyObject, TriggerState> States = new();

    private readonly DependencyProperty _observedValueProperty;

    public DataTrigger(Binding binding, object? value)
    {
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        Value = value;
        _observedValueProperty = DependencyProperty.RegisterAttached(
            "DataTriggerObservedValue_" + Interlocked.Increment(ref _propertyId),
            typeof(object),
            typeof(DataTrigger),
            new FrameworkPropertyMetadata(null));
    }

    public Binding Binding { get; }

    public object? Value { get; }

    public override bool IsMatch(DependencyObject target)
    {
        return Equals(target.GetValue(_observedValueProperty), Value);
    }

    public override void Attach(DependencyObject target, Action invalidate)
    {
        lock (SyncRoot)
        {
            if (States.TryGetValue(target, out var existing))
            {
                if (existing.Handler != null)
                {
                    target.DependencyPropertyChanged -= existing.Handler;
                }

                BindingOperations.ClearBinding(target, _observedValueProperty);
                target.ClearValue(_observedValueProperty);
            }

            var binding = Condition.CloneBinding(Binding);
            binding.Mode = BindingMode.OneWay;
            binding.UpdateSourceTrigger = UpdateSourceTrigger.Explicit;

            EventHandler<DependencyPropertyChangedEventArgs>? handler = (_, args) =>
            {
                if (ReferenceEquals(args.Property, _observedValueProperty))
                {
                    invalidate();
                }
            };

            target.DependencyPropertyChanged += handler;
            BindingOperations.SetBinding(target, _observedValueProperty, binding);

            States[target] = new TriggerState(handler);
        }
    }

    public override void Detach(DependencyObject target)
    {
        lock (SyncRoot)
        {
            if (!States.TryGetValue(target, out var state))
            {
                BindingOperations.ClearBinding(target, _observedValueProperty);
                target.ClearValue(_observedValueProperty);
                return;
            }

            if (state.Handler != null)
            {
                target.DependencyPropertyChanged -= state.Handler;
            }

            BindingOperations.ClearBinding(target, _observedValueProperty);
            target.ClearValue(_observedValueProperty);
            States.Remove(target);
        }
    }
    private sealed class TriggerState
    {
        public TriggerState(EventHandler<DependencyPropertyChangedEventArgs>? handler)
        {
            Handler = handler;
        }

        public EventHandler<DependencyPropertyChangedEventArgs>? Handler { get; }
    }
}
