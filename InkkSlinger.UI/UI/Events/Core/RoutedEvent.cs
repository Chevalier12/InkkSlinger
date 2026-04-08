namespace InkkSlinger;

public sealed class RoutedEvent
{
    public RoutedEvent(string name, RoutingStrategy routingStrategy)
    {
        Name = name;
        RoutingStrategy = routingStrategy;
    }

    public string Name { get; }

    public RoutingStrategy RoutingStrategy { get; }
}
