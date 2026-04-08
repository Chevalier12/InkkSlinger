using System;
using System.Collections.Generic;

namespace InkkSlinger;

public static class EventManager
{
    private static readonly Dictionary<(Type Owner, RoutedEvent RoutedEvent), List<ClassHandlerEntry>> ClassHandlers = new();
    private static readonly Dictionary<(Type ElementType, RoutedEvent RoutedEvent), ClassHandlerEntry[]> ClassHandlerDispatchCache = new();

    public static void RegisterClassHandler<TElement, TArgs>(RoutedEvent routedEvent, Action<TElement, TArgs> handler)
        where TElement : UIElement
        where TArgs : RoutedEventArgs
    {
        RegisterClassHandler(routedEvent, handler, handledEventsToo: false);
    }

    public static void RegisterClassHandler<TElement, TArgs>(
        RoutedEvent routedEvent,
        Action<TElement, TArgs> handler,
        bool handledEventsToo)
        where TElement : UIElement
        where TArgs : RoutedEventArgs
    {
        var key = (typeof(TElement), routedEvent);
        if (!ClassHandlers.TryGetValue(key, out var handlers))
        {
            handlers = new List<ClassHandlerEntry>();
            ClassHandlers[key] = handlers;
        }

        handlers.Add(new ClassHandlerEntry(
            (element, args) =>
            {
                if (element is TElement typedElement && args is TArgs typedArgs)
                {
                    handler(typedElement, typedArgs);
                }
            },
            handledEventsToo));
        ClassHandlerDispatchCache.Clear();
    }

    internal static void InvokeClassHandlers(UIElement element, RoutedEvent routedEvent, RoutedEventArgs args)
    {
        var dispatchHandlers = GetDispatchHandlers(element.GetType(), routedEvent);
        for (var i = 0; i < dispatchHandlers.Length; i++)
        {
            var handler = dispatchHandlers[i];
            if (args.Handled && !handler.HandledEventsToo)
            {
                continue;
            }

            handler.Handler(element, args);
        }
    }

    internal static bool HasClassHandlers(Type elementType, RoutedEvent routedEvent)
    {
        return GetDispatchHandlers(elementType, routedEvent).Length > 0;
    }

    internal static int GetClassHandlerCount(Type elementType, RoutedEvent routedEvent)
    {
        return GetDispatchHandlers(elementType, routedEvent).Length;
    }

    private static ClassHandlerEntry[] GetDispatchHandlers(Type elementType, RoutedEvent routedEvent)
    {
        var key = (elementType, routedEvent);
        if (ClassHandlerDispatchCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var collected = new List<ClassHandlerEntry>();
        var type = elementType;
        while (type != null && typeof(UIElement).IsAssignableFrom(type))
        {
            if (ClassHandlers.TryGetValue((type, routedEvent), out var handlers))
            {
                collected.AddRange(handlers);
            }

            type = type.BaseType;
        }

        var flattened = collected.Count == 0 ? Array.Empty<ClassHandlerEntry>() : collected.ToArray();
        ClassHandlerDispatchCache[key] = flattened;
        return flattened;
    }

    private readonly struct ClassHandlerEntry
    {
        public ClassHandlerEntry(Action<UIElement, RoutedEventArgs> handler, bool handledEventsToo)
        {
            Handler = handler;
            HandledEventsToo = handledEventsToo;
        }

        public Action<UIElement, RoutedEventArgs> Handler { get; }

        public bool HandledEventsToo { get; }
    }
}
