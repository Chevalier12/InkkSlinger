using System;

namespace InkkSlinger;

public sealed class EventSetter : SetterBase
{
    public EventSetter(string routedEvent, Delegate handler, bool handledEventsToo = false)
    {
        if (string.IsNullOrWhiteSpace(routedEvent))
        {
            throw new ArgumentException("EventSetter requires a non-empty routed event name.", nameof(routedEvent));
        }

        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Event = routedEvent.Trim();
        HandledEventsToo = handledEventsToo;
    }

    public string Event { get; }

    public Delegate Handler { get; }

    public bool HandledEventsToo { get; }
}
