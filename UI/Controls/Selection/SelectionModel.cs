using System;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

public sealed class SelectionModel
{
    private readonly List<object> _items = new();
    private readonly SortedSet<int> _selectedIndices = new();
    private int _anchorIndex = -1;

    public SelectionMode Mode { get; set; } = SelectionMode.Single;

    public event EventHandler<SelectionModelChangedEventArgs>? Changed;

    public int SelectedIndex => _selectedIndices.Count == 0 ? -1 : _selectedIndices.Min;
    public int AnchorIndex => _anchorIndex;

    public object? SelectedItem
    {
        get
        {
            var index = SelectedIndex;
            return index < 0 || index >= _items.Count ? null : _items[index];
        }
    }

    public IReadOnlyList<int> SelectedIndices => _selectedIndices.ToList();

    public void ReplaceItems(IEnumerable<object> items)
    {
        _items.Clear();
        _items.AddRange(items);

        var removed = new List<int>();
        foreach (var selected in _selectedIndices.ToList())
        {
            if (selected >= 0 && selected < _items.Count)
            {
                continue;
            }

            removed.Add(selected);
            _selectedIndices.Remove(selected);
        }

        if (removed.Count > 0)
        {
            RaiseChanged(removed, Array.Empty<int>());
        }

        if (_anchorIndex >= _items.Count)
        {
            _anchorIndex = -1;
        }
    }

    public void Clear()
    {
        if (_selectedIndices.Count == 0)
        {
            _anchorIndex = -1;
            return;
        }

        var removed = _selectedIndices.ToList();
        _selectedIndices.Clear();
        _anchorIndex = -1;
        RaiseChanged(removed, Array.Empty<int>());
    }

    public void SelectIndex(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            Clear();
            return;
        }

        if (Mode == SelectionMode.Single)
        {
            var removed = _selectedIndices.Where(value => value != index).ToList();
            if (_selectedIndices.Count == 1 && _selectedIndices.Contains(index))
            {
                _anchorIndex = index;
                return;
            }

            _selectedIndices.Clear();
            _selectedIndices.Add(index);
            _anchorIndex = index;
            RaiseChanged(removed, new[] { index });
            return;
        }

        if (_selectedIndices.Add(index))
        {
            _anchorIndex = index;
            RaiseChanged(Array.Empty<int>(), new[] { index });
        }
    }

    public void SelectItem(object? item)
    {
        if (item == null)
        {
            Clear();
            return;
        }

        var index = _items.IndexOf(item);
        SelectIndex(index);
    }

    public void UnselectIndex(int index)
    {
        if (!_selectedIndices.Remove(index))
        {
            return;
        }

        if (_anchorIndex == index)
        {
            _anchorIndex = _selectedIndices.Count == 0 ? -1 : _selectedIndices.Min;
        }

        RaiseChanged(new[] { index }, Array.Empty<int>());
    }

    public void ToggleIndex(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        if (Mode == SelectionMode.Single)
        {
            SelectIndex(index);
            return;
        }

        if (_selectedIndices.Contains(index))
        {
            UnselectIndex(index);
            return;
        }

        _selectedIndices.Add(index);
        _anchorIndex = index;
        RaiseChanged(Array.Empty<int>(), new[] { index });
    }

    public void SelectRange(int startIndex, int endIndex, bool clearExisting)
    {
        if (startIndex < 0 || endIndex < 0 || startIndex >= _items.Count || endIndex >= _items.Count)
        {
            return;
        }

        if (Mode == SelectionMode.Single)
        {
            SelectIndex(endIndex);
            return;
        }

        var lower = Math.Min(startIndex, endIndex);
        var upper = Math.Max(startIndex, endIndex);

        var removed = new List<int>();
        if (clearExisting)
        {
            foreach (var selected in _selectedIndices.ToList())
            {
                if (selected >= lower && selected <= upper)
                {
                    continue;
                }

                _selectedIndices.Remove(selected);
                removed.Add(selected);
            }
        }

        var added = new List<int>();
        for (var index = lower; index <= upper; index++)
        {
            if (_selectedIndices.Add(index))
            {
                added.Add(index);
            }
        }

        RaiseChanged(removed, added);
    }

    public void SetAnchorIndex(int index)
    {
        _anchorIndex = index >= 0 && index < _items.Count ? index : -1;
    }

    private void RaiseChanged(IReadOnlyList<int> removedIndices, IReadOnlyList<int> addedIndices)
    {
        if (removedIndices.Count == 0 && addedIndices.Count == 0)
        {
            return;
        }

        var removedItems = removedIndices
            .Where(index => index >= 0 && index < _items.Count)
            .Select(index => _items[index])
            .ToList();
        var addedItems = addedIndices
            .Where(index => index >= 0 && index < _items.Count)
            .Select(index => _items[index])
            .ToList();

        Changed?.Invoke(this, new SelectionModelChangedEventArgs(removedIndices, addedIndices, removedItems, addedItems));
    }
}
