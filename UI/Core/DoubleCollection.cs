using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace InkkSlinger;

public sealed class DoubleCollection : Freezable, IList<double>, IList
{
    private readonly List<double> _items = [];

    public static DoubleCollection Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var collection = new DoubleCollection();
        var tokens = value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            collection.Add(double.Parse(token, CultureInfo.InvariantCulture));
        }

        return collection;
    }

    public int Count => _items.Count;

    public bool IsReadOnly => IsFrozen;

    bool IList.IsFixedSize => IsFrozen;

    bool IList.IsReadOnly => IsFrozen;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    public double this[int index]
    {
        get
        {
            ReadPreamble();
            return _items[index];
        }
        set
        {
            WritePreamble();
            _items[index] = value;
            WritePostscript();
        }
    }

    object? IList.this[int index]
    {
        get => this[index];
        set => this[index] = ConvertToDouble(value);
    }

    public void Add(double item)
    {
        WritePreamble();
        _items.Add(item);
        WritePostscript();
    }

    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }

        WritePreamble();
        _items.Clear();
        WritePostscript();
    }

    public bool Contains(double item)
    {
        ReadPreamble();
        return _items.Contains(item);
    }

    public void CopyTo(double[] array, int arrayIndex)
    {
        ReadPreamble();
        _items.CopyTo(array, arrayIndex);
    }

    public IEnumerator<double> GetEnumerator()
    {
        ReadPreamble();
        return _items.GetEnumerator();
    }

    public int IndexOf(double item)
    {
        ReadPreamble();
        return _items.IndexOf(item);
    }

    public void Insert(int index, double item)
    {
        WritePreamble();
        _items.Insert(index, item);
        WritePostscript();
    }

    public bool Remove(double item)
    {
        WritePreamble();
        var removed = _items.Remove(item);
        if (removed)
        {
            WritePostscript();
        }

        return removed;
    }

    public void RemoveAt(int index)
    {
        WritePreamble();
        _items.RemoveAt(index);
        WritePostscript();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    int IList.Add(object? value)
    {
        Add(ConvertToDouble(value));
        return _items.Count - 1;
    }

    bool IList.Contains(object? value)
    {
        return value is double numeric && Contains(numeric);
    }

    int IList.IndexOf(object? value)
    {
        return value is double numeric ? IndexOf(numeric) : -1;
    }

    void IList.Insert(int index, object? value)
    {
        Insert(index, ConvertToDouble(value));
    }

    void IList.Remove(object? value)
    {
        if (value is double numeric)
        {
            _ = Remove(numeric);
        }
    }

    void ICollection.CopyTo(Array array, int index)
    {
        ReadPreamble();
        ((ICollection)_items).CopyTo(array, index);
    }

    protected override Freezable CreateInstanceCore()
    {
        return new DoubleCollection();
    }

    protected override void CloneCore(Freezable source)
    {
        if (source is not DoubleCollection collection)
        {
            return;
        }

        _items.Clear();
        _items.AddRange(collection._items);
    }

    public override string ToString()
    {
        return string.Join(",", _items.ConvertAll(static value => value.ToString(CultureInfo.InvariantCulture)));
    }

    private static double ConvertToDouble(object? value)
    {
        return value switch
        {
            double numeric => numeric,
            float numeric => numeric,
            int numeric => numeric,
            long numeric => numeric,
            decimal numeric => (double)numeric,
            string text => double.Parse(text, CultureInfo.InvariantCulture),
            _ => throw new ArgumentException("DoubleCollection values must be numeric.", nameof(value))
        };
    }
}