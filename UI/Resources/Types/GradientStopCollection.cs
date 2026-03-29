using System;
using System.Collections;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class GradientStopCollection : Freezable, IList<GradientStop>, IList
{
    private readonly List<GradientStop> _items = [];

    public int Count => _items.Count;

    public bool IsReadOnly => IsFrozen;

    bool IList.IsFixedSize => IsFrozen;

    bool IList.IsReadOnly => IsFrozen;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    public GradientStop this[int index]
    {
        get
        {
            ReadPreamble();
            return _items[index];
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            WritePreamble();
            var previous = _items[index];
            if (ReferenceEquals(previous, value))
            {
                return;
            }

            Detach(previous);
            _items[index] = value;
            Attach(value);
            WritePostscript();
        }
    }

    object? IList.this[int index]
    {
        get => this[index];
        set => this[index] = ConvertToGradientStop(value);
    }

    public void Add(GradientStop item)
    {
        ArgumentNullException.ThrowIfNull(item);

        WritePreamble();
        _items.Add(item);
        Attach(item);
        WritePostscript();
    }

    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }

        WritePreamble();
        for (var i = 0; i < _items.Count; i++)
        {
            Detach(_items[i]);
        }

        _items.Clear();
        WritePostscript();
    }

    public bool Contains(GradientStop item)
    {
        ReadPreamble();
        return _items.Contains(item);
    }

    public void CopyTo(GradientStop[] array, int arrayIndex)
    {
        ReadPreamble();
        _items.CopyTo(array, arrayIndex);
    }

    public IEnumerator<GradientStop> GetEnumerator()
    {
        ReadPreamble();
        return _items.GetEnumerator();
    }

    public int IndexOf(GradientStop item)
    {
        ReadPreamble();
        return _items.IndexOf(item);
    }

    public void Insert(int index, GradientStop item)
    {
        ArgumentNullException.ThrowIfNull(item);

        WritePreamble();
        _items.Insert(index, item);
        Attach(item);
        WritePostscript();
    }

    public bool Remove(GradientStop item)
    {
        WritePreamble();
        var removed = _items.Remove(item);
        if (!removed)
        {
            return false;
        }

        Detach(item);
        WritePostscript();
        return true;
    }

    public void RemoveAt(int index)
    {
        WritePreamble();
        var item = _items[index];
        _items.RemoveAt(index);
        Detach(item);
        WritePostscript();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    int IList.Add(object? value)
    {
        Add(ConvertToGradientStop(value));
        return _items.Count - 1;
    }

    bool IList.Contains(object? value)
    {
        return value is GradientStop stop && Contains(stop);
    }

    int IList.IndexOf(object? value)
    {
        return value is GradientStop stop ? IndexOf(stop) : -1;
    }

    void IList.Insert(int index, object? value)
    {
        Insert(index, ConvertToGradientStop(value));
    }

    void IList.Remove(object? value)
    {
        if (value is GradientStop stop)
        {
            _ = Remove(stop);
        }
    }

    void ICollection.CopyTo(Array array, int index)
    {
        ReadPreamble();
        ((ICollection)_items).CopyTo(array, index);
    }

    protected override Freezable CreateInstanceCore()
    {
        return new GradientStopCollection();
    }

    protected override void CloneCore(Freezable source)
    {
        if (source is not GradientStopCollection collection)
        {
            return;
        }

        Clear();
        for (var i = 0; i < collection._items.Count; i++)
        {
            Add((GradientStop)collection._items[i].Clone());
        }
    }

    protected override void CloneCurrentValueCore(Freezable source)
    {
        if (source is not GradientStopCollection collection)
        {
            return;
        }

        Clear();
        for (var i = 0; i < collection._items.Count; i++)
        {
            Add((GradientStop)collection._items[i].CloneCurrentValue());
        }
    }

    protected override bool FreezeCore(bool isChecking)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (!FreezeValue(_items[i], isChecking))
            {
                return false;
            }
        }

        return true;
    }

    private void Attach(GradientStop item)
    {
        item.Changed += OnItemChanged;
    }

    private void Detach(GradientStop item)
    {
        item.Changed -= OnItemChanged;
    }

    private void OnItemChanged()
    {
        WritePostscript();
    }

    private static GradientStop ConvertToGradientStop(object? value)
    {
        return value as GradientStop
            ?? throw new ArgumentException("GradientStopCollection values must be GradientStop instances.", nameof(value));
    }
}