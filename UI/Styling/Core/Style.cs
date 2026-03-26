using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace InkkSlinger;

public class Style
{
    private static readonly ConditionalWeakTable<DependencyObject, StyleInstanceState> States = new();
    private static long s_applyCallCount;
    private static long s_applyTicks;
    private static long s_applySettersTicks;
    private static long s_applyTriggersTicks;
    private static long s_collectTriggeredValuesTicks;
    private static long s_triggerMatchCount;
    private static long s_matchedTriggerCount;
    private static long s_setStyleValueCount;
    private static long s_setStyleTriggerValueCount;
    private static long s_clearStyleTriggerValueCount;
    private static long s_applyTriggerActionsTicks;
    private static long s_invokeActionsCount;
    private static long s_invokeActionsTicks;

    private readonly List<SetterBase> _setters = new();
    private readonly List<TriggerBase> _triggers = new();

    public Style(Type targetType)
    {
        TargetType = targetType;
    }

    public Type TargetType { get; }

    public Style? BasedOn { get; set; }

    public IList<SetterBase> Setters => _setters;

    public IList<TriggerBase> Triggers => _triggers;

    internal static void ResetTelemetryForTests()
    {
        s_applyCallCount = 0;
        s_applyTicks = 0;
        s_applySettersTicks = 0;
        s_applyTriggersTicks = 0;
        s_collectTriggeredValuesTicks = 0;
        s_triggerMatchCount = 0;
        s_matchedTriggerCount = 0;
        s_setStyleValueCount = 0;
        s_setStyleTriggerValueCount = 0;
        s_clearStyleTriggerValueCount = 0;
        s_applyTriggerActionsTicks = 0;
        s_invokeActionsCount = 0;
        s_invokeActionsTicks = 0;
    }

    internal static StyleTelemetrySnapshot GetTelemetrySnapshotForTests()
    {
        return new StyleTelemetrySnapshot(
            s_applyCallCount,
            TicksToMilliseconds(s_applyTicks),
            TicksToMilliseconds(s_applySettersTicks),
            TicksToMilliseconds(s_applyTriggersTicks),
            TicksToMilliseconds(s_collectTriggeredValuesTicks),
            s_triggerMatchCount,
            s_matchedTriggerCount,
            s_setStyleValueCount,
            s_setStyleTriggerValueCount,
            s_clearStyleTriggerValueCount,
            TicksToMilliseconds(s_applyTriggerActionsTicks),
            s_invokeActionsCount,
            TicksToMilliseconds(s_invokeActionsTicks));
    }

    public void Apply(DependencyObject target)
    {
        if (!TargetType.IsInstanceOfType(target))
        {
            throw new InvalidOperationException($"Style target type {TargetType.Name} does not match {target.GetType().Name}.");
        }

        s_applyCallCount++;
        var applyStart = Stopwatch.GetTimestamp();
        var state = States.GetValue(target, _ => new StyleInstanceState());
        if (state.IsApplyingStyle)
        {
            state.StyleReapplyPending = true;
            return;
        }

        state.IsApplyingStyle = true;
        try
        {
            do
            {
                state.StyleReapplyPending = false;
                state.ReapplyRequested = () => ApplyTriggers(target, state);

                ClearAppliedValues(target, state);
                DetachTriggers(target, state);
                AttachResourceScopeHandler(target, state);

                var applySettersStart = Stopwatch.GetTimestamp();
                ApplySettersRecursive(target, state);
                s_applySettersTicks += Stopwatch.GetTimestamp() - applySettersStart;
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
            while (state.StyleReapplyPending);
        }
        finally
        {
            s_applyTicks += Stopwatch.GetTimestamp() - applyStart;
            state.IsApplyingStyle = false;
        }
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

        if (state.ResourceScopeHandler != null && target is FrameworkElement frameworkElement)
        {
            frameworkElement.ResourceScopeInvalidated -= state.ResourceScopeHandler;
        }

        DetachTriggers(target, state);
        ClearAppliedValues(target, state);
        States.Remove(target);
    }

    private void ApplySettersRecursive(DependencyObject target, StyleInstanceState state)
    {
        BasedOn?.ApplySettersRecursive(target, state);

        foreach (var setterBase in _setters)
        {
            if (setterBase is Setter setter)
            {
                if (!string.IsNullOrWhiteSpace(setter.TargetName))
                {
                    continue;
                }

                if (!ResourceReferenceResolver.TryResolve(target, setter.Property, setter.Value, out var resolvedValue))
                {
                    continue;
                }

                target.SetStyleValue(setter.Property, StyleValueCloneUtility.CloneForAssignment(resolvedValue));
                s_setStyleValueCount++;
                state.AppliedStyleProperties.Add(setter.Property);
                continue;
            }

            if (setterBase is EventSetter eventSetter && target is UIElement uiElement)
            {
                var routedEvent = EventTrigger.ResolveRoutedEvent(uiElement.GetType(), eventSetter.Event);
                if (routedEvent == null)
                {
                    throw new InvalidOperationException(
                        $"RoutedEvent '{eventSetter.Event}' could not be resolved on '{uiElement.GetType().Name}'.");
                }

                var wrappedHandler = WrapEventSetterHandler(
                    eventSetter.Handler,
                    eventSetter.Event,
                    TargetType,
                    uiElement.GetType());
                uiElement.AddHandler(routedEvent, wrappedHandler, eventSetter.HandledEventsToo);
                state.AppliedEventHandlers.Add((uiElement, routedEvent, wrappedHandler));
            }
        }
    }

    private void ApplyTriggers(DependencyObject target, StyleInstanceState state)
    {
        if (state.IsApplyingTriggers)
        {
            state.ReapplyPending = true;
            return;
        }

        var applyTriggersStart = Stopwatch.GetTimestamp();
        state.IsApplyingTriggers = true;
        try
        {
            do
            {
                state.ReapplyPending = false;

                var desiredValues = new Dictionary<DependencyProperty, object?>();
                var currentTriggerMatches = new Dictionary<TriggerBase, bool>();
                var collectTriggeredValuesStart = Stopwatch.GetTimestamp();
                CollectTriggeredValues(target, state.AttachedTriggers, desiredValues, currentTriggerMatches);
                s_collectTriggeredValuesTicks += Stopwatch.GetTimestamp() - collectTriggeredValuesStart;

                foreach (var pair in state.ActiveTriggerValues)
                {
                    if (!desiredValues.ContainsKey(pair.Key))
                    {
                        target.ClearStyleTriggerValue(pair.Key);
                        s_clearStyleTriggerValueCount++;
                    }
                }

                foreach (var desired in desiredValues)
                {
                    if (state.ActiveTriggerValues.TryGetValue(desired.Key, out var activeValue) &&
                        Equals(activeValue, desired.Value))
                    {
                        continue;
                    }

                    target.SetStyleTriggerValue(desired.Key, StyleValueCloneUtility.CloneForAssignment(desired.Value));
                    s_setStyleTriggerValueCount++;
                }

                state.ActiveTriggerValues.Clear();
                foreach (var desired in desiredValues)
                {
                    state.ActiveTriggerValues[desired.Key] = desired.Value;
                }

                var applyTriggerActionsStart = Stopwatch.GetTimestamp();
                ApplyTriggerActions(target, state, currentTriggerMatches);
                s_applyTriggerActionsTicks += Stopwatch.GetTimestamp() - applyTriggerActionsStart;
            }
            while (state.ReapplyPending);
        }
        finally
        {
            s_applyTriggersTicks += Stopwatch.GetTimestamp() - applyTriggersStart;
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
            s_triggerMatchCount++;
            triggerMatches[trigger] = isMatch;
            if (!isMatch)
            {
                continue;
            }

            s_matchedTriggerCount++;

            foreach (var setter in trigger.Setters)
            {
                if (!string.IsNullOrWhiteSpace(setter.TargetName))
                {
                    continue;
                }

                if (!ResourceReferenceResolver.TryResolve(target, setter.Property, setter.Value, out var resolvedValue))
                {
                    continue;
                }

                accumulator[setter.Property] = resolvedValue;
            }
        }
    }

    private static void AttachResourceScopeHandler(DependencyObject target, StyleInstanceState state)
    {
        if (target is not FrameworkElement frameworkElement)
        {
            return;
        }

        if (state.ResourceScopeHandler != null)
        {
            frameworkElement.ResourceScopeInvalidated -= state.ResourceScopeHandler;
        }

        state.ResourceScopeHandler = (_, _) =>
        {
            if (state.IsApplyingStyle)
            {
                state.StyleReapplyPending = true;
                return;
            }

            if (target.GetValue(FrameworkElement.StyleProperty) is Style style)
            {
                style.Apply(target);
            }
        };

        frameworkElement.ResourceScopeInvalidated += state.ResourceScopeHandler;
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
        var invokeActionsStart = Stopwatch.GetTimestamp();
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
            s_invokeActionsCount++;
            action.Invoke(context);
        }

        s_invokeActionsTicks += Stopwatch.GetTimestamp() - invokeActionsStart;
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

        foreach (var appliedHandler in state.AppliedEventHandlers)
        {
            appliedHandler.Element.RemoveHandler(appliedHandler.RoutedEvent, appliedHandler.Handler);
        }

        state.ActiveTriggerValues.Clear();
        state.ActiveTriggerMatches.Clear();
        state.AppliedStyleProperties.Clear();
        state.AppliedEventHandlers.Clear();
        state.ConditionProperties.Clear();
    }

    private static EventHandler<RoutedEventArgs> WrapEventSetterHandler(
        Delegate handler,
        string eventName,
        Type styleTargetType,
        Type runtimeTargetType)
    {
        if (handler is EventHandler<RoutedEventArgs> routedHandler)
        {
            return routedHandler;
        }

        var parameters = handler.Method.GetParameters();
        if (parameters.Length == 0)
        {
            return (_, _) => handler.DynamicInvoke();
        }

        if (parameters.Length == 1)
        {
            var parameterType = parameters[0].ParameterType;
            return (sender, args) =>
            {
                try
                {
                    var value = ResolveEventSetterArgument(parameterType, sender, args, preferSender: false);
                    handler.DynamicInvoke(value);
                }
                catch (InvalidOperationException ex)
                {
                    throw CreateEventSetterHandlerContextException(ex, handler, eventName, styleTargetType, runtimeTargetType);
                }
            };
        }

        if (parameters.Length == 2)
        {
            var firstType = parameters[0].ParameterType;
            var secondType = parameters[1].ParameterType;
            return (sender, args) =>
            {
                try
                {
                    var first = ResolveEventSetterArgument(firstType, sender, args, preferSender: true);
                    var second = ResolveEventSetterArgument(secondType, sender, args, preferSender: false);
                    handler.DynamicInvoke(first, second);
                }
                catch (InvalidOperationException ex)
                {
                    throw CreateEventSetterHandlerContextException(ex, handler, eventName, styleTargetType, runtimeTargetType);
                }
            };
        }

        throw new InvalidOperationException(
            $"EventSetter handler '{handler.Method.Name}' is not supported. Expected 0, 1, or 2 parameters.");
    }

    private static object? ResolveEventSetterArgument(Type parameterType, object? sender, RoutedEventArgs args, bool preferSender)
    {
        if (preferSender)
        {
            if (sender == null)
            {
                if (!parameterType.IsValueType)
                {
                    return null;
                }
            }
            else if (parameterType.IsAssignableFrom(sender.GetType()))
            {
                return sender;
            }

            if (parameterType.IsAssignableFrom(args.GetType()))
            {
                return args;
            }
        }
        else
        {
            if (parameterType.IsAssignableFrom(args.GetType()))
            {
                return args;
            }

            if (sender == null)
            {
                if (!parameterType.IsValueType)
                {
                    return null;
                }
            }
            else if (parameterType.IsAssignableFrom(sender.GetType()))
            {
                return sender;
            }
        }

        throw new InvalidOperationException(
            $"EventSetter handler parameter type '{parameterType.Name}' is not compatible with sender/args.");
    }

    private static InvalidOperationException CreateEventSetterHandlerContextException(
        InvalidOperationException inner,
        Delegate handler,
        string eventName,
        Type styleTargetType,
        Type runtimeTargetType)
    {
        return new InvalidOperationException(
            $"EventSetter handler invocation failed for Style.TargetType '{styleTargetType.Name}', runtime target '{runtimeTargetType.Name}', event '{eventName}', handler '{handler.Method.DeclaringType?.Name}.{handler.Method.Name}'.",
            inner);
    }

    private sealed class StyleInstanceState
    {
        public bool IsSubscribed;
        public bool IsApplyingTriggers;
        public bool IsApplyingStyle;
        public bool ReapplyPending;
        public bool StyleReapplyPending;

        public EventHandler<DependencyPropertyChangedEventArgs>? Handler;
        public EventHandler? ResourceScopeHandler;
        public Action? ReapplyRequested;

        public HashSet<DependencyProperty> AppliedStyleProperties { get; } = new();

        public HashSet<DependencyProperty> ConditionProperties { get; } = new();

        public Dictionary<DependencyProperty, object?> ActiveTriggerValues { get; } = new();

        public Dictionary<TriggerBase, bool> ActiveTriggerMatches { get; } = new();

        public List<TriggerBase> AttachedTriggers { get; } = new();

        public List<(UIElement Element, RoutedEvent RoutedEvent, EventHandler<RoutedEventArgs> Handler)> AppliedEventHandlers { get; } = new();
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }
}

internal readonly record struct StyleTelemetrySnapshot(
    long ApplyCallCount,
    double ApplyMilliseconds,
    double ApplySettersMilliseconds,
    double ApplyTriggersMilliseconds,
    double CollectTriggeredValuesMilliseconds,
    long TriggerMatchCount,
    long MatchedTriggerCount,
    long SetStyleValueCount,
    long SetStyleTriggerValueCount,
    long ClearStyleTriggerValueCount,
    double ApplyTriggerActionsMilliseconds,
    long InvokeActionsCount,
    double InvokeActionsMilliseconds);
