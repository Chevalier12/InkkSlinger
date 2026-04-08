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
    private int _notificationDeferralDepth;
    private bool _hasDeferredChange;
    private bool _suppressChangedNotifications;

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

    public void ReplaceContents(
        IEnumerable<KeyValuePair<object, object>> entries,
        IEnumerable<ResourceDictionary>? mergedDictionaries = null,
        bool notifyChanged = true)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var previousSuppression = _suppressChangedNotifications;
        _suppressChangedNotifications = !notifyChanged;
        try
        {
            using var _ = new NotificationDeferral(this);

            ClearMergedDictionariesCore();
            _resources.Clear();

            foreach (var entry in entries)
            {
                _resources[entry.Key] = entry.Value;
            }

            if (mergedDictionaries != null)
            {
                foreach (var dictionary in mergedDictionaries)
                {
                    AddMergedDictionaryCore(dictionary);
                }
            }

            if (notifyChanged)
            {
                _hasDeferredChange = true;
            }
        }
        finally
        {
            _suppressChangedNotifications = previousSuppression;
            if (!notifyChanged)
            {
                _hasDeferredChange = false;
            }
        }
    }

    public void AddMergedDictionary(ResourceDictionary dictionary)
    {
        AddMergedDictionaryCore(dictionary);
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
        for (var index = _mergedDictionaries.Count - 1; index >= 0; index--)
        {
            var dictionary = _mergedDictionaries[index];
            if (dictionary.TryGetValue(key, out var value))
            {
                found = true;
                return value;
            }
        }

        found = false;
        return null;
    }

    private bool ContainsMergedDictionaryReference(ResourceDictionary target)
    {
        var visited = new HashSet<ResourceDictionary>();
        return ContainsMergedDictionaryReferenceCore(this, target, visited);
    }

    private static bool ContainsMergedDictionaryReferenceCore(
        ResourceDictionary current,
        ResourceDictionary target,
        HashSet<ResourceDictionary> visited)
    {
        if (!visited.Add(current))
        {
            return false;
        }

        foreach (var merged in current._mergedDictionaries)
        {
            if (ReferenceEquals(merged, target))
            {
                return true;
            }

            if (ContainsMergedDictionaryReferenceCore(merged, target, visited))
            {
                return true;
            }
        }

        return false;
    }

    private void OnMergedDictionaryChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        OnChanged(new ResourceDictionaryChangedEventArgs(ResourceDictionaryChangeAction.MergeChanged, e.Key));
    }

    private void OnChanged(ResourceDictionaryChangedEventArgs args)
    {
        if (_suppressChangedNotifications)
        {
            return;
        }

        if (_notificationDeferralDepth > 0)
        {
            _hasDeferredChange = true;
            return;
        }

        Changed?.Invoke(this, args);
    }

    private void AddMergedDictionaryCore(ResourceDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        if (ReferenceEquals(dictionary, this))
        {
            throw new InvalidOperationException("A ResourceDictionary cannot merge itself.");
        }

        if (dictionary.ContainsMergedDictionaryReference(this))
        {
            throw new InvalidOperationException("A ResourceDictionary merge cannot introduce a cycle.");
        }

        _mergedDictionaries.Add(dictionary);
        dictionary.Changed += OnMergedDictionaryChanged;
    }

    private void ClearMergedDictionariesCore()
    {
        foreach (var dictionary in _mergedDictionaries)
        {
            dictionary.Changed -= OnMergedDictionaryChanged;
        }

        _mergedDictionaries.Clear();
    }

    private void BeginNotificationDeferral()
    {
        _notificationDeferralDepth++;
    }

    private void EndNotificationDeferral()
    {
        if (_notificationDeferralDepth == 0)
        {
            return;
        }

        _notificationDeferralDepth--;
        if (_notificationDeferralDepth != 0 || !_hasDeferredChange || _suppressChangedNotifications)
        {
            return;
        }

        _hasDeferredChange = false;
        Changed?.Invoke(this, new ResourceDictionaryChangedEventArgs(ResourceDictionaryChangeAction.MergeChanged, null));
    }

    private sealed class NotificationDeferral : IDisposable
    {
        private readonly ResourceDictionary _owner;
        private bool _disposed;

        public NotificationDeferral(ResourceDictionary owner)
        {
            _owner = owner;
            _owner.BeginNotificationDeferral();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner.EndNotificationDeferral();
        }
    }
}
