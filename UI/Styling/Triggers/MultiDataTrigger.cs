using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class MultiDataTrigger : TriggerBase
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<DependencyObject, TriggerState> States = new();
    private readonly List<Condition> _conditions = new();

    public IList<Condition> Conditions => _conditions;

    public override bool IsMatch(DependencyObject target)
    {
        ValidateConditions();

        if (_conditions.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < _conditions.Count; i++)
        {
            var condition = _conditions[i];
            var observedProperty = condition.GetOrCreateObservedValueProperty();
            var currentValue = target.GetValue(observedProperty);
            if (!Equals(currentValue, condition.Value))
            {
                return false;
            }
        }

        return true;
    }

    public override void Attach(DependencyObject target, Action invalidate)
    {
        ValidateConditions();

        lock (SyncRoot)
        {
            if (States.TryGetValue(target, out var existing))
            {
                DetachInternal(target, existing);
                States.Remove(target);
            }

            var state = new TriggerState();
            for (var i = 0; i < _conditions.Count; i++)
            {
                var condition = _conditions[i];

                var observedProperty = condition.GetOrCreateObservedValueProperty();
                var binding = Condition.CloneBinding(condition.Binding!);
                binding.Mode = BindingMode.OneWay;
                binding.UpdateSourceTrigger = UpdateSourceTrigger.Explicit;

                EventHandler<DependencyPropertyChangedEventArgs>? handler = (_, args) =>
                {
                    if (ReferenceEquals(args.Property, observedProperty))
                    {
                        invalidate();
                    }
                };

                target.DependencyPropertyChanged += handler;
                BindingOperations.SetBinding(target, observedProperty, binding);
                state.Entries.Add(new TriggerConditionEntry(observedProperty, handler));
            }

            States[target] = state;
        }
    }

    private void ValidateConditions()
    {
        if (_conditions.Count == 0)
        {
            throw new InvalidOperationException("MultiDataTrigger requires at least one Condition.");
        }

        for (var i = 0; i < _conditions.Count; i++)
        {
            if (_conditions[i].Binding == null)
            {
                throw new InvalidOperationException($"MultiDataTrigger condition at index {i} requires a Binding.");
            }
        }
    }

    public override void Detach(DependencyObject target)
    {
        lock (SyncRoot)
        {
            if (!States.TryGetValue(target, out var state))
            {
                for (var i = 0; i < _conditions.Count; i++)
                {
                    var observedProperty = _conditions[i].GetOrCreateObservedValueProperty();
                    BindingOperations.ClearBinding(target, observedProperty);
                    target.ClearValue(observedProperty);
                }

                return;
            }

            DetachInternal(target, state);
            States.Remove(target);
        }
    }

    private static void DetachInternal(DependencyObject target, TriggerState state)
    {
        for (var i = 0; i < state.Entries.Count; i++)
        {
            var entry = state.Entries[i];
            if (entry.Handler != null)
            {
                target.DependencyPropertyChanged -= entry.Handler;
            }

            BindingOperations.ClearBinding(target, entry.ObservedProperty);
            target.ClearValue(entry.ObservedProperty);
        }
    }

    private sealed class TriggerState
    {
        public List<TriggerConditionEntry> Entries { get; } = new();
    }

    private readonly struct TriggerConditionEntry
    {
        public TriggerConditionEntry(
            DependencyProperty observedProperty,
            EventHandler<DependencyPropertyChangedEventArgs>? handler)
        {
            ObservedProperty = observedProperty;
            Handler = handler;
        }

        public DependencyProperty ObservedProperty { get; }

        public EventHandler<DependencyPropertyChangedEventArgs>? Handler { get; }
    }
}
