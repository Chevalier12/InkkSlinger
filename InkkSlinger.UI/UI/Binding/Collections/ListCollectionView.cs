using System.Collections;

namespace InkkSlinger;

public sealed class ListCollectionView : CollectionView
{
    public ListCollectionView(IEnumerable source)
        : base(source)
    {
    }
}
