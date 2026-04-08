using System;
using System.Collections;

namespace InkkSlinger;

public static class CollectionViewFactory
{
    public static ICollectionView? GetDefaultView(object? source)
    {
        if (source == null)
        {
            return null;
        }

        if (source is ICollectionView collectionView)
        {
            return collectionView;
        }

        if (source is IEnumerable enumerable)
        {
            return new ListCollectionView(enumerable);
        }

        throw new InvalidOperationException(
            $"ItemsSource must implement IEnumerable or ICollectionView, but was '{source.GetType().Name}'.");
    }
}
