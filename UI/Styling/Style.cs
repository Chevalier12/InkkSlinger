using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace InkkSlinger;

public class Style
{
    private static readonly ConditionalWeakTable<DependencyObject, StyleInstanceState> States = new();

    private readonly List<Setter> _setters = new();
    private readonly List<TriggerBase> _triggers = new();

    public Style(Type targetType)
    {
        TargetType = targetType;
    }

    public Type TargetType { get; }

    public Style? BasedOn { get; set; }

    public IList<Setter> Setters => _setters;

    public IList<TriggerBase> Triggers => _triggers;

    public void Apply(DependencyObject target)
    {
        if (!TargetType.IsInstanceOfType(target))
        {
            throw new InvalidOperationException($"Style target type {TargetType.Name} does not match {target.GetType().Name}.");
        }

        var state = States.GetValue(target, _ => new StyleInstanceState());
        state.ReapplyRequested = () => ApplyTriggers(target, state);

        ClearAppliedValues(target, state);
        DetachTriggers(target, state);

        ApplySettersRecursive(target, state);
        var activeTriggers = new List<TriggerBase>();
        CollectTriggersRecursive(activeTriggers);
        CollectConditionProperties(activeTriggers, state.ConditionProperties);
        AttachTriggers(target, state, activeTriggers);

        if (!state.IsSubscribed)
        {
            state.Handler = (_, args) =>
            {
                if (!state.ConditionProperties.Contains(args.Property))
                {
                    return;
                }

                ApplyTriggers(target, state);
            };

            target.DependencyPropertyChanged += state.Handler;
            state.IsSubscribed = true;
        }

        ApplyTriggers(target, state);
    }

    public void Detach(DependencyObject target)
    {
        if (!States.TryGetValue(target, out var state))
        {
            return;
        }

        if (state.IsSubscribed && state.Handler != null)
        {
            target.DependencyPropertyChanged -= state.Handler;
        }

        DetachTriggers(target, state);
        ClearAppliedValues(target, state);
        States.Remove(target);
    }

    private void ApplySettersRecursive(DependencyObject target, StyleInstanceState state)
    {
        BasedOn?.ApplySettersRecursive(target, state);

        foreach (var setter in _setters)
        {
            if (!string.IsNullOrWhiteSpace(setter.TargetName))
            {
                continue;
            }

            target.SetStyleValue(setter.Property, setter.Value);
            state.AppliedStyleProperties.Add(setter.Property);
        }
    }

    private void ApplyTriggers(DependencyObject target, StyleInstanceState state)
    {
        if (state.IsApplyingTriggers)
        {
            state.ReapplyPending = true;
            return;
        }

        state.IsApplyingTriggers = true;
        try
        {
            do
            {
                state.ReapplyPending = false;

                var desiredValues = new Dictionary<DependencyProperty, object?>();
                var currentTriggerMatches = new Dictionary<TriggerBase, bool>();
                CollectTriggeredValues(target, state.AttachedTriggers, desiredValues, currentTriggerMatches);

                foreach (var pair in state.ActiveTriggerValues)
                {
                    if (!desiredValues.ContainsKey(pair.Key))
                    {
                        target.ClearStyleTriggerValue(pair.Key);
                    }
                }

                foreach (var desired in desiredValues)
                {
                    if (state.ActiveTriggerValues.TryGetValue(desired.Key, out var activeValue) &&
                        Equals(activeValue, desired.Value))
                    {
                        continue;
                    }

                    target.SetStyleTriggerValue(desired.Key, desired.Value);
                }

                state.ActiveTriggerValues.Clear();
                foreach (var desired in desiredValues)
                {
                    state.ActiveTriggerValues[desired.Key] = desired.Value;
                }

                ApplyTriggerActions(target, state, currentTriggerMatches);
            }
            while (state.ReapplyPending);
        }
        finally
        {
            state.IsApplyingTriggers = false;
        }
    }

    private static void CollectTriggeredValues(
        DependencyObject target,
        IEnumerable<TriggerBase> triggers,
        IDictionary<DependencyProperty, object?> accumulator,
        IDictionary<TriggerBase, bool> triggerMatches)
    {
        foreach (var trigger in triggers)
        {
            var isMatch = trigger.IsMatch(target);
            triggerMatches[trigger] = isMatch;
            if (!isMatch)
            {
                continue;
            }

            foreach (var setter in trigger.Setters)
            {
                if (!string.IsNullOrWhiteSpace(setter.TargetName))
                {
                    continue;
                }

                accumulator[setter.Property] = setter.Value;
            }
        }
    }

    private static void ApplyTriggerActions(
        DependencyObject target,
        StyleInstanceState state,
        IDictionary<TriggerBase, bool> currentTriggerMatches)
    {
        foreach (var pair in currentTriggerMatches)
        {
            var trigger = pair.Key;
            var isMatch = pair.Value;
            var wasMatch = state.ActiveTriggerMatches.TryGetValue(trigger, out var previousMatch) && previousMatch;

            if (!wasMatch && isMatch)
            {
                InvokeActions(trigger.EnterActions, target);
            }
            else if (wasMatch && !isMatch)
            {
                InvokeActions(trigger.ExitActions, target);
            }
        }

        state.ActiveTriggerMatches.Clear();
        foreach (var pair in currentTriggerMatches)
        {
            state.ActiveTriggerMatches[pair.Key] = pair.Value;
        }
    }

    private static void InvokeActions(IEnumerable<TriggerAction> actions, DependencyObject target)
    {
        var scope = target as FrameworkElement;
        var context = new TriggerActionContext(
            target,
            scope,
            name =>
            {
                if (scope == null || string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                return NameScopeService.FindName(scope, name) ?? scope.FindName(name);
            });

        foreach (var action in actions)
        {
            action.Invoke(context);
        }
    }

    private void CollectTriggersRecursive(ICollection<TriggerBase> accumulator)
    {
        BasedOn?.CollectTriggersRecursive(accumulator);
        foreach (var trigger in _triggers)
        {
            accumulator.Add(trigger);
        }
    }

    private static void CollectConditionProperties(IEnumerable<TriggerBase> triggers, ISet<DependencyProperty> conditionProperties)
    {
        conditionProperties.Clear();
        foreach (var trigger in triggers)
        {
            trigger.CollectConditionProperties(conditionProperties);
        }
    }

    private static void AttachTriggers(DependencyObject target, StyleInstanceState state, IReadOnlyList<TriggerBase> triggers)
    {
        foreach (var trigger in triggers)
        {
            trigger.Attach(target, () => state.ReapplyRequested?.Invoke());
            state.AttachedTriggers.Add(trigger);
        }
    }

    private static void DetachTriggers(DependencyObject target, StyleInstanceState state)
    {
        foreach (var trigger in state.AttachedTriggers)
        {
            trigger.Detach(target);
        }

        state.AttachedTriggers.Clear();
    }

    private static void ClearAppliedValues(DependencyObject target, StyleInstanceState state)
    {
        foreach (var property in state.ActiveTriggerValues.Keys)
        {
            target.ClearStyleTriggerValue(property);
        }

        foreach (var property in state.AppliedStyleProperties)
        {
            target.ClearStyleValue(property);
        }

        state.ActiveTriggerValues.Clear();
        state.ActiveTriggerMatches.Clear();
        state.AppliedStyleProperties.Clear();
        state.ConditionProperties.Clear();
    }

    private sealed class StyleInstanceState
    {
        public bool IsSubscribed;
        public bool IsApplyingTriggers;
        public bool ReapplyPending;

        public EventHandler<DependencyPropertyChangedEventArgs>? Handler;
        public Action? ReapplyRequested;

        public HashSet<DependencyProperty> AppliedStyleProperties { get; } = new();

        public HashSet<DependencyProperty> ConditionProperties { get; } = new();

        public Dictionary<DependencyProperty, object?> ActiveTriggerValues { get; } = new();

        public Dictionary<TriggerBase, bool> ActiveTriggerMatches { get; } = new();

        public List<TriggerBase> AttachedTriggers { get; } = new();
    }
}
