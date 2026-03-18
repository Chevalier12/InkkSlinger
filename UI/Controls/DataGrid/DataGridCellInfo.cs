using System;

namespace InkkSlinger;

public readonly struct DataGridCellInfo : IEquatable<DataGridCellInfo>
{
    public DataGridCellInfo(object? item, DataGridColumn? column)
    {
        Item = item;
        Column = column;
    }

    public object? Item { get; }

    public DataGridColumn? Column { get; }

    public bool IsValid => Column != null;

    public bool Equals(DataGridCellInfo other)
    {
        return Equals(Item, other.Item) && Equals(Column, other.Column);
    }

    public override bool Equals(object? obj)
    {
        return obj is DataGridCellInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Item, Column);
    }

    public static bool operator ==(DataGridCellInfo left, DataGridCellInfo right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(DataGridCellInfo left, DataGridCellInfo right)
    {
        return !left.Equals(right);
    }
}