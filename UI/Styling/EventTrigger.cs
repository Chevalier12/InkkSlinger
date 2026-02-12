using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace InkkSlinger;

public sealed class EventTrigger : TriggerBase
{
    private readonly ConditionalWeakTable<DependencyObject, EventTriggerState> _states = new();

    public string RoutedEvent { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public bool HandledEventsToo { get; set; } = true;

    public IList<TriggerAction> Actions => EnterActions;

    public override bool IsMatch(DependencyObject target)
    {
        return false;
    }

    public override void Attach(DependencyObject target, Action invalidate)
    {
        if (target is not UIElement uiTarget)
        {
            return;
        }

        var eventSource = ResolveEventSource(target, uiTarget);
        var routedEvent = ResolveRoutedEvent(eventSource.GetType(), RoutedEvent);
        if (routedEvent == null)
        {
            throw new InvalidOperationException(
                $"RoutedEvent '{RoutedEvent}' could not be resolved on '{eventSource.GetType().Name}'.");
        }

        var state = _states.GetValue(target, _ => new EventTriggerState());
        if (state.EventSource != null && state.Handler != null && state.RoutedEvent != null)
        {
            state.EventSource.RemoveHandler<RoutedEventArgs>(state.RoutedEvent, state.Handler);
        }

        EventHandler<RoutedEventArgs> handler = (_, _) =>
        {
            var scope = target as FrameworkElement ?? eventSource as FrameworkElement;
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

            foreach (var action in Actions)
            {
                action.Invoke(context);
            }
        };

        eventSource.AddHandler(routedEvent, handler, handledEventsToo: HandledEventsToo);
        state.EventSource = eventSource;
        state.RoutedEvent = routedEvent;
        state.Handler = handler;
    }

    public override void Detach(DependencyObject target)
    {
        if (!_states.TryGetValue(target, out var state))
        {
            return;
        }

        if (state.EventSource != null && state.Handler != null && state.RoutedEvent != null)
        {
            state.EventSource.RemoveHandler<RoutedEventArgs>(state.RoutedEvent, state.Handler);
        }

        _states.Remove(target);
    }

    private UIElement ResolveEventSource(DependencyObject target, UIElement fallback)
    {
        if (string.IsNullOrWhiteSpace(SourceName))
        {
            return fallback;
        }

        if (target is FrameworkElement frameworkTarget)
        {
            var scoped = NameScopeService.FindName(frameworkTarget, SourceName) as UIElement;
            if (scoped != null)
            {
                return scoped;
            }

            if (frameworkTarget is Control control)
            {
                control.ApplyTemplate();
                var part = ResolveTemplatePart(control, SourceName);
                if (part != null)
                {
                    return part;
                }
            }

            var found = frameworkTarget.FindName(SourceName);
            if (found != null)
            {
                return found;
            }
        }

        throw new InvalidOperationException(
            $"EventTrigger SourceName '{SourceName}' could not be resolved.");
    }

    private static RoutedEvent? ResolveRoutedEvent(Type sourceType, string routedEventName)
    {
        if (string.IsNullOrWhiteSpace(routedEventName))
        {
            return null;
        }

        var trimmed = routedEventName.Trim();
        var ownerQualifiedSeparator = trimmed.LastIndexOf('.');
        if (ownerQualifiedSeparator > 0 && ownerQualifiedSeparator < trimmed.Length - 1)
        {
            var ownerTypeName = trimmed[..ownerQualifiedSeparator];
            var eventName = trimmed[(ownerQualifiedSeparator + 1)..];
            var ownerType = ResolveType(ownerTypeName);
            if (ownerType != null)
            {
                var ownerResolved = ResolveRoutedEventByType(ownerType, eventName);
                if (ownerResolved != null)
                {
                    return ownerResolved;
                }
            }
        }

        return ResolveRoutedEventByType(sourceType, trimmed);
    }

    private static RoutedEvent? ResolveRoutedEventByType(Type sourceType, string eventName)
    {
        var candidates = new List<string>
        {
            eventName,
            eventName.EndsWith("Event", StringComparison.Ordinal) ? eventName : eventName + "Event"
        };

        for (var current = sourceType; current != null; current = current.BaseType)
        {
            foreach (var candidate in candidates)
            {
                var field = current.GetField(candidate, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (field?.FieldType == typeof(RoutedEvent))
                {
                    return (RoutedEvent?)field.GetValue(null);
                }
            }
        }

        return null;
    }

    private static Type? ResolveType(string typeName)
    {
        var assembly = typeof(UIElement).Assembly;
        return assembly.GetTypes()
            .FirstOrDefault(t => t.IsPublic && string.Equals(t.Name, typeName, StringComparison.Ordinal));
    }

    private static UIElement? ResolveTemplatePart(Control control, string sourceName)
    {
        var method = typeof(Control).GetMethod(
            "GetTemplateChild",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return method?.Invoke(control, new object?[] { sourceName }) as UIElement;
    }

    private sealed class EventTriggerState
    {
        public UIElement? EventSource;

        public RoutedEvent? RoutedEvent;

        public EventHandler<RoutedEventArgs>? Handler;
    }
}
