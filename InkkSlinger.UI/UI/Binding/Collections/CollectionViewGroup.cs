using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace InkkSlinger;

public sealed class CollectionViewGroup
{
    private readonly ReadOnlyCollection<object?> _items;
    private readonly ReadOnlyCollection<CollectionViewGroup> _subgroups;

    public CollectionViewGroup(object? name, IReadOnlyList<object?> items, IReadOnlyList<CollectionViewGroup>? subgroups = null)
    {
        Name = name;
        _items = new ReadOnlyCollection<object?>(new List<object?>(items));
        _subgroups = new ReadOnlyCollection<CollectionViewGroup>(new List<CollectionViewGroup>(subgroups ?? []));
    }

    public object? Name { get; }

    public ReadOnlyCollection<object?> Items => _items;

    public ReadOnlyCollection<CollectionViewGroup> Subgroups => _subgroups;

    public int ItemCount => _items.Count;
}
