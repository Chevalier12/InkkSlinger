using System;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

public sealed class SelectionModel
{
    private readonly List<object> _items = new();
    private readonly SortedSet<int> _selectedIndices = new();
    private int _anchorIndex = -1;
    private SelectionMode _mode = SelectionMode.Single;

    public SelectionMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            CoerceSelectionForMode();
        }
    }

    public event EventHandler<SelectionModelChangedEventArgs>? Changed;

    public int SelectedIndex => _selectedIndices.Count == 0 ? -1 : _selectedIndices.Min;
    public int AnchorIndex => _anchorIndex;
    public int Count => _items.Count;

    public object? SelectedItem
    {
        get
        {
            var index = SelectedIndex;
            return index < 0 || index >= _items.Count ? null : _items[index];
        }
    }

    public IReadOnlyList<int> SelectedIndices => _selectedIndices.ToList();

    public IReadOnlyList<object> SelectedItems => _selectedIndices
        .Where(index => index >= 0 && index < _items.Count)
        .Select(index => _items[index])
        .ToList();

    public void ReplaceItems(IEnumerable<object> items)
    {
        var previouslySelectedItems = SelectedItems;
        var previousAnchorItem = _anchorIndex >= 0 && _anchorIndex < _items.Count
            ? _items[_anchorIndex]
            : null;

        _items.Clear();
        _items.AddRange(items);

        _selectedIndices.Clear();
        for (var i = 0; i < previouslySelectedItems.Count; i++)
        {
            var index = _items.IndexOf(previouslySelectedItems[i]);
            if (index >= 0)
            {
                _selectedIndices.Add(index);
            }
        }

        if (previousAnchorItem != null)
        {
            _anchorIndex = _items.IndexOf(previousAnchorItem);
        }
        else if (_anchorIndex >= _items.Count)
        {
            _anchorIndex = -1;
        }

        if (_anchorIndex < 0 && _selectedIndices.Count > 0)
        {
            _anchorIndex = _selectedIndices.Min;
        }

        CoerceSelectionForMode();
    }

    public void SelectOnlyIndex(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            Clear();
            return;
        }

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
    }

    public void InsertItems(int index, System.Collections.IList newItems)
    {
        if (newItems.Count == 0)
        {
            return;
        }

        var insertIndex = Math.Clamp(index, 0, _items.Count);
        for (var i = 0; i < newItems.Count; i++)
        {
            _items.Insert(insertIndex + i, newItems[i]!);
        }

        var shift = newItems.Count;
        if (shift <= 0)
        {
            return;
        }

        var shifted = new SortedSet<int>();
        foreach (var selectedIndex in _selectedIndices)
        {
            shifted.Add(selectedIndex >= insertIndex ? selectedIndex + shift : selectedIndex);
        }

        _selectedIndices.Clear();
        foreach (var selectedIndex in shifted)
        {
            _selectedIndices.Add(selectedIndex);
        }

        if (_anchorIndex >= insertIndex)
        {
            _anchorIndex += shift;
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

        if (Mode is SelectionMode.Single or SelectionMode.Extended)
        {
            SelectOnlyIndex(index);
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

    public void SelectOnlyItem(object? item)
    {
        if (item == null)
        {
            Clear();
            return;
        }

        var index = _items.IndexOf(item);
        SelectOnlyIndex(index);
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

    public void SelectAll()
    {
        if (Mode == SelectionMode.Single || _items.Count == 0)
        {
            return;
        }

        var added = new List<int>();
        for (var i = 0; i < _items.Count; i++)
        {
            if (_selectedIndices.Add(i))
            {
                added.Add(i);
            }
        }

        if (_anchorIndex < 0)
        {
            _anchorIndex = 0;
        }

        RaiseChanged(Array.Empty<int>(), added);
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

    public bool IsSelected(int index)
    {
        return _selectedIndices.Contains(index);
    }

    public void SetAnchorIndex(int index)
    {
        _anchorIndex = index >= 0 && index < _items.Count ? index : -1;
    }

    private void CoerceSelectionForMode()
    {
        if (_mode != SelectionMode.Single || _selectedIndices.Count <= 1)
        {
            return;
        }

        var retainedIndex = _anchorIndex >= 0 && _selectedIndices.Contains(_anchorIndex)
            ? _anchorIndex
            : _selectedIndices.Min;
        var removed = _selectedIndices.Where(index => index != retainedIndex).ToList();

        _selectedIndices.Clear();
        _selectedIndices.Add(retainedIndex);
        _anchorIndex = retainedIndex;
        RaiseChanged(removed, Array.Empty<int>());
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
