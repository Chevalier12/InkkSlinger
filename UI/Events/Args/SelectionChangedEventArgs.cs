using System.Collections.Generic;

namespace InkkSlinger;

public sealed class SelectionChangedEventArgs : RoutedEventArgs
{
    public SelectionChangedEventArgs(
        RoutedEvent routedEvent,
        IReadOnlyList<object> removedItems,
        IReadOnlyList<object> addedItems)
        : base(routedEvent)
    {
        RemovedItems = removedItems;
        AddedItems = addedItems;
    }

    public IReadOnlyList<object> RemovedItems { get; }

    public IReadOnlyList<object> AddedItems { get; }
}
