using System.ComponentModel;

namespace InkkSlinger;

public sealed class SortDescription
{
    public SortDescription()
    {
    }

    public SortDescription(string propertyName, ListSortDirection direction)
    {
        PropertyName = propertyName;
        Direction = direction;
    }

    public string PropertyName { get; set; } = string.Empty;

    public ListSortDirection Direction { get; set; } = ListSortDirection.Ascending;
}
