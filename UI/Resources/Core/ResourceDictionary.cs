using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace InkkSlinger;

public class ResourceDictionary : IDictionary<object, object>
{
    private readonly Dictionary<object, object> _resources = new();
    private readonly List<ResourceDictionary> _mergedDictionaries = new();
    private readonly ReadOnlyCollection<ResourceDictionary> _readonlyMergedDictionaries;

    public ResourceDictionary()
    {
        _readonlyMergedDictionaries = _mergedDictionaries.AsReadOnly();
    }

    public event EventHandler<ResourceDictionaryChangedEventArgs>? Changed;

    public IReadOnlyList<ResourceDictionary> MergedDictionaries => _readonlyMergedDictionaries;

    public object this[object key]
    {
        get => _resources[key];
        set
        {
            var action = _resources.ContainsKey(key)
                ? ResourceDictionaryChangeAction.Update
                : ResourceDictionaryChangeAction.Add;

            _resources[key] = value;
            OnChanged(new ResourceDictionaryChangedEventArgs(action, key));
        }
    }

    public ICollection<object> Keys => _resources.Keys;

    public ICollection<object> Values => _resources.Values;

    public int Count => _resources.Count;

    public bool IsReadOnly => false;

    public void AddMergedDictionary(ResourceDictionary dictionary)
    {
        _mergedDictionaries.Add(dictionary);
        dictionary.Changed += OnMergedDictionaryChanged;
        OnChanged(new ResourceDictionaryChangedEventArgs(ResourceDictionaryChangeAction.MergeChanged, null));
    }

    public bool RemoveMergedDictionary(ResourceDictionary dictionary)
    {
        if (!_mergedDictionaries.Remove(dictionary))
        {
            return false;
        }

        dictionary.Changed -= OnMergedDictionaryChanged;
        OnChanged(new ResourceDictionaryChangedEventArgs(ResourceDictionaryChangeAction.MergeChanged, null));
        return true;
    }

    public void Add(object key, object value)
    {
        _resources.Add(key, value);
        OnChanged(new ResourceDictionaryChangedEventArgs(ResourceDictionaryChangeAction.Add, key));
    }

    public void Add(KeyValuePair<object, object> item)
    {
        Add(item.Key, item.Value);
    }

    public void Clear()
    {
        if (_resources.Count == 0)
        {
            return;
        }

        _resources.Clear();
        OnChanged(new ResourceDictionaryChangedEventArgs(ResourceDictionaryChangeAction.Clear, null));
    }

    public bool Contains(KeyValuePair<object, object> item)
    {
        return _resources.ContainsKey(item.Key);
    }

    public bool ContainsKey(object key)
    {
        if (_resources.ContainsKey(key))
        {
            return true;
        }

        FindInMergedDictionaries(key, out var found);
        return found;
    }

    public void CopyTo(KeyValuePair<object, object>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<object, object>>)_resources).CopyTo(array, arrayIndex);
    }

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
    {
        return _resources.GetEnumerator();
    }

    public bool Remove(object key)
    {
        if (!_resources.Remove(key))
        {
            return false;
        }

        OnChanged(new ResourceDictionaryChangedEventArgs(ResourceDictionaryChangeAction.Remove, key));
        return true;
    }

    public bool Remove(KeyValuePair<object, object> item)
    {
        return Remove(item.Key);
    }

    public bool TryGetValue(object key, out object value)
    {
        if (_resources.TryGetValue(key, out var directValue))
        {
            value = directValue!;
            return true;
        }

        var merged = FindInMergedDictionaries(key, out var found);
        value = merged!;
        return found;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _resources.GetEnumerator();
    }

    private object? FindInMergedDictionaries(object key, out bool found)
    {
        foreach (var dictionary in _mergedDictionaries)
        {
            if (dictionary.TryGetValue(key, out var value))
            {
                found = true;
                return value;
            }
        }

        found = false;
        return null;
    }

    private void OnMergedDictionaryChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        OnChanged(new ResourceDictionaryChangedEventArgs(ResourceDictionaryChangeAction.MergeChanged, e.Key));
    }

    private void OnChanged(ResourceDictionaryChangedEventArgs args)
    {
        Changed?.Invoke(this, args);
    }
}
