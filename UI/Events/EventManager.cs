using System;
using System.Collections.Generic;

namespace InkkSlinger;

public static class EventManager
{
    private static readonly Dictionary<(Type Owner, RoutedEvent RoutedEvent), List<ClassHandlerEntry>> ClassHandlers = new();

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
    }

    internal static void InvokeClassHandlers(UIElement element, RoutedEvent routedEvent, RoutedEventArgs args)
    {
        var type = element.GetType();
        while (type != null && typeof(UIElement).IsAssignableFrom(type))
        {
            var key = (type, routedEvent);
            if (ClassHandlers.TryGetValue(key, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    if (args.Handled && !handler.HandledEventsToo)
                    {
                        continue;
                    }

                    handler.Handler(element, args);
                }
            }

            type = type.BaseType;
        }
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
