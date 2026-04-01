using System;
using System.Collections;
using System.Collections.Generic;

namespace InkkSlinger;

/// <summary>
/// An observable collection of <see cref="InkStroke"/> instances.
/// Provides change notifications so that rendering controls can invalidate minimally.
/// Modeled after WPF <c>System.Windows.Ink.StrokeCollection</c>.
/// </summary>
public sealed class InkStrokeCollection : IList<InkStroke>
{
    private readonly List<InkStroke> _strokes = new();

    /// <summary>Raised when strokes are added, removed, or the collection is cleared.</summary>
    public event EventHandler? Changed;

    public int Count => _strokes.Count;

    public bool IsReadOnly => false;

    public InkStroke this[int index]
    {
        get => _strokes[index];
        set
        {
            _strokes[index] = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Add(InkStroke item)
    {
        _strokes.Add(item);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void AddRange(IEnumerable<InkStroke> items)
    {
        bool any = false;
        foreach (var item in items)
        {
            _strokes.Add(item);
            any = true;
        }

        if (any)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear()
    {
        if (_strokes.Count == 0)
        {
            return;
        }

        _strokes.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Contains(InkStroke item) => _strokes.Contains(item);

    public void CopyTo(InkStroke[] array, int arrayIndex) => _strokes.CopyTo(array, arrayIndex);

    public int IndexOf(InkStroke item) => _strokes.IndexOf(item);

    public void Insert(int index, InkStroke item)
    {
        _strokes.Insert(index, item);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Remove(InkStroke item)
    {
        bool removed = _strokes.Remove(item);
        if (removed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    public void RemoveAt(int index)
    {
        _strokes.RemoveAt(index);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IEnumerator<InkStroke> GetEnumerator() => _strokes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
