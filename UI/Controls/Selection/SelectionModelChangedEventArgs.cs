using System.Collections.Generic;

namespace InkkSlinger;

public sealed class SelectionModelChangedEventArgs : System.EventArgs
{
    public SelectionModelChangedEventArgs(
        IReadOnlyList<int> removedIndices,
        IReadOnlyList<int> addedIndices,
        IReadOnlyList<object> removedItems,
        IReadOnlyList<object> addedItems)
    {
        RemovedIndices = removedIndices;
        AddedIndices = addedIndices;
        RemovedItems = removedItems;
        AddedItems = addedItems;
    }

    public IReadOnlyList<int> RemovedIndices { get; }

    public IReadOnlyList<int> AddedIndices { get; }

    public IReadOnlyList<object> RemovedItems { get; }

    public IReadOnlyList<object> AddedItems { get; }
}
