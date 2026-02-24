using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace InkkSlinger;

public class CollectionView : ICollectionView
{
    private static readonly ReadOnlyCollection<CollectionViewGroup> EmptyGroups = new([]);
    private readonly IEnumerable _source;
    private readonly List<object?> _viewItems = [];
    private ReadOnlyCollection<CollectionViewGroup> _groups = EmptyGroups;
    private Predicate<object?>? _filter;
    private int _currentPosition = -1;
    private object? _currentItem;
    private INotifyCollectionChanged? _sourceNotifier;

    public CollectionView(IEnumerable source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        SortDescriptions = new ObservableCollection<SortDescription>();
        GroupDescriptions = new ObservableCollection<GroupDescription>();
        SortDescriptions.CollectionChanged += OnSortDescriptionsChanged;
        GroupDescriptions.CollectionChanged += OnGroupDescriptionsChanged;
        AttachSourceCollection(_source);
        Refresh();
    }

    public IEnumerable SourceCollection => _source;

    public Predicate<object?>? Filter
    {
        get => _filter;
        set
        {
            if (ReferenceEquals(_filter, value))
            {
                return;
            }

            _filter = value;
            OnPropertyChanged(nameof(Filter));
            Refresh();
        }
    }

    public ObservableCollection<SortDescription> SortDescriptions { get; }

    public ObservableCollection<GroupDescription> GroupDescriptions { get; }

    public ReadOnlyCollection<CollectionViewGroup> Groups => _groups;

    public object? CurrentItem => _currentItem;

    public int CurrentPosition => _currentPosition;

    public bool IsCurrentBeforeFirst => _currentPosition < 0;

    public bool IsCurrentAfterLast => _viewItems.Count > 0 && _currentPosition >= _viewItems.Count;

    public event EventHandler? CurrentChanged;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IEnumerator GetEnumerator()
    {
        return _viewItems.GetEnumerator();
    }

    public virtual void Refresh()
    {
        RefreshCore();
    }

    private void RefreshCore()
    {
        var previousCurrentItem = _currentItem;
        var previousCurrentPosition = _currentPosition;
        var previousGroups = _groups;

        var sourceItems = BuildSourceSnapshot();
        var filtered = ApplyFilter(sourceItems);
        ApplySort(filtered);

        _viewItems.Clear();
        _viewItems.AddRange(filtered);
        _groups = BuildGroupsProjection(_viewItems);
        var groupsChanged = !ReferenceEquals(previousGroups, _groups);

        RepairCurrent(previousCurrentItem);
        RaiseRefreshNotifications(previousCurrentItem, previousCurrentPosition, groupsChanged);
    }

    public bool MoveCurrentTo(object? item)
    {
        var index = _viewItems.IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        return MoveCurrentToPosition(index);
    }

    public bool MoveCurrentToFirst()
    {
        return _viewItems.Count > 0 && MoveCurrentToPosition(0);
    }

    public bool MoveCurrentToLast()
    {
        return _viewItems.Count > 0 && MoveCurrentToPosition(_viewItems.Count - 1);
    }

    public bool MoveCurrentToNext()
    {
        if (_viewItems.Count == 0)
        {
            return false;
        }

        return MoveCurrentToPosition(Math.Min(_currentPosition + 1, _viewItems.Count));
    }

    public bool MoveCurrentToPrevious()
    {
        if (_viewItems.Count == 0)
        {
            return false;
        }

        return MoveCurrentToPosition(Math.Max(_currentPosition - 1, -1));
    }

    public bool MoveCurrentToPosition(int position)
    {
        if (_viewItems.Count == 0)
        {
            return false;
        }

        if (position < -1 || position > _viewItems.Count)
        {
            return false;
        }

        var oldItem = _currentItem;
        var oldPosition = _currentPosition;

        _currentPosition = position;
        _currentItem = position >= 0 && position < _viewItems.Count ? _viewItems[position] : null;
        RaiseCurrentChangedIfNeeded(oldItem, oldPosition);
        return true;
    }

    protected virtual void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        if (TryHandleSourceCollectionChangedIncrementally(e))
        {
            return;
        }

        RefreshCore();
    }

    private bool TryHandleSourceCollectionChangedIncrementally(NotifyCollectionChangedEventArgs e)
    {
        if (_filter != null || SortDescriptions.Count > 0 || GroupDescriptions.Count > 0)
        {
            return false;
        }

        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems == null || e.NewItems.Count == 0)
        {
            return false;
        }

        var insertIndex = e.NewStartingIndex;
        if (insertIndex < 0 || insertIndex > _viewItems.Count)
        {
            return false;
        }

        var previousCurrentItem = _currentItem;
        var previousCurrentPosition = _currentPosition;
        var previousBeforeFirst = IsCurrentBeforeFirst;
        var previousAfterLast = IsCurrentAfterLast;

        for (var i = 0; i < e.NewItems.Count; i++)
        {
            _viewItems.Insert(insertIndex + i, e.NewItems[i]);
        }

        RepairCurrent(previousCurrentItem);
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, e.NewItems, insertIndex));
        RaiseCurrencyNotificationsIfNeeded(previousCurrentItem, previousCurrentPosition, previousBeforeFirst, previousAfterLast);
        return true;
    }

    private List<object?> BuildSourceSnapshot()
    {
        var result = new List<object?>();
        foreach (var item in _source)
        {
            result.Add(item);
        }

        return result;
    }

    private List<object?> ApplyFilter(IReadOnlyList<object?> sourceItems)
    {
        if (_filter == null)
        {
            return new List<object?>(sourceItems);
        }

        var filtered = new List<object?>(sourceItems.Count);
        for (var i = 0; i < sourceItems.Count; i++)
        {
            var item = sourceItems[i];
            if (!_filter(item))
            {
                continue;
            }

            filtered.Add(item);
        }

        return filtered;
    }

    private void ApplySort(List<object?> items)
    {
        if (SortDescriptions.Count == 0)
        {
            return;
        }

        items.Sort(CompareItems);
    }

    private int CompareItems(object? left, object? right)
    {
        for (var i = 0; i < SortDescriptions.Count; i++)
        {
            var description = SortDescriptions[i];
            var leftValue = ReadPathValue(left, description.PropertyName);
            var rightValue = ReadPathValue(right, description.PropertyName);
            var compared = CompareValues(leftValue, rightValue);
            if (compared == 0)
            {
                continue;
            }

            return description.Direction == ListSortDirection.Descending
                ? -compared
                : compared;
        }

        return 0;
    }

    private static int CompareValues(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        if (left is IComparable comparable)
        {
            try
            {
                return comparable.CompareTo(right);
            }
            catch
            {
            }
        }

        return string.Compare(
            Convert.ToString(left, CultureInfo.InvariantCulture),
            Convert.ToString(right, CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static object? ReadPathValue(object? source, string? path)
    {
        if (source == null || string.IsNullOrWhiteSpace(path))
        {
            return source;
        }

        object? current = source;
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            if (current == null)
            {
                return null;
            }

            var property = current.GetType().GetProperty(
                segments[i],
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property == null)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        return current;
    }

    private ReadOnlyCollection<CollectionViewGroup> BuildGroupsProjection(IReadOnlyList<object?> items)
    {
        if (GroupDescriptions.Count == 0)
        {
            return EmptyGroups;
        }

        return new ReadOnlyCollection<CollectionViewGroup>(BuildGroupsRecursive(items, depth: 0));
    }

    private List<CollectionViewGroup> BuildGroupsRecursive(IReadOnlyList<object?> items, int depth)
    {
        if (depth >= GroupDescriptions.Count)
        {
            return [];
        }

        var groupDescription = GroupDescriptions[depth];
        var buckets = new List<GroupBucket>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var key = groupDescription.GroupNameFromItem(item);
            var bucket = FindBucket(buckets, key);
            if (bucket == null)
            {
                bucket = new GroupBucket(key);
                buckets.Add(bucket);
            }

            bucket.Items.Add(item);
        }

        var result = new List<CollectionViewGroup>(buckets.Count);
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucket = buckets[i];
            var subgroups = depth < GroupDescriptions.Count - 1
                ? BuildGroupsRecursive(bucket.Items, depth + 1)
                : [];
            result.Add(new CollectionViewGroup(bucket.Key, bucket.Items, subgroups));
        }

        return result;
    }

    private static GroupBucket? FindBucket(IReadOnlyList<GroupBucket> buckets, object? key)
    {
        for (var i = 0; i < buckets.Count; i++)
        {
            if (Equals(buckets[i].Key, key))
            {
                return buckets[i];
            }
        }

        return null;
    }

    private void RepairCurrent(object? previousCurrentItem)
    {
        if (_viewItems.Count == 0)
        {
            _currentPosition = -1;
            _currentItem = null;
            return;
        }

        if (previousCurrentItem != null)
        {
            var preservedIndex = _viewItems.IndexOf(previousCurrentItem);
            if (preservedIndex >= 0)
            {
                _currentPosition = preservedIndex;
                _currentItem = previousCurrentItem;
                return;
            }
        }

        if (_currentPosition < 0 || _currentPosition >= _viewItems.Count)
        {
            _currentPosition = 0;
        }

        _currentItem = _viewItems[_currentPosition];
    }

    private void RaiseRefreshNotifications(
        object? previousCurrentItem,
        int previousCurrentPosition,
        bool groupsChanged)
    {
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        if (groupsChanged)
        {
            OnPropertyChanged(nameof(Groups));
        }

        OnPropertyChanged(nameof(CurrentPosition));
        OnPropertyChanged(nameof(CurrentItem));
        OnPropertyChanged(nameof(IsCurrentBeforeFirst));
        OnPropertyChanged(nameof(IsCurrentAfterLast));
        RaiseCurrentChangedIfNeeded(previousCurrentItem, previousCurrentPosition);
    }

    private void RaiseCurrentChangedIfNeeded(object? previousCurrentItem, int previousCurrentPosition)
    {
        if (Equals(previousCurrentItem, _currentItem) && previousCurrentPosition == _currentPosition)
        {
            return;
        }

        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseCurrencyNotificationsIfNeeded(
        object? previousCurrentItem,
        int previousCurrentPosition,
        bool previousBeforeFirst,
        bool previousAfterLast)
    {
        if (previousCurrentPosition != _currentPosition)
        {
            OnPropertyChanged(nameof(CurrentPosition));
        }

        if (!Equals(previousCurrentItem, _currentItem))
        {
            OnPropertyChanged(nameof(CurrentItem));
        }

        if (previousBeforeFirst != IsCurrentBeforeFirst)
        {
            OnPropertyChanged(nameof(IsCurrentBeforeFirst));
        }

        if (previousAfterLast != IsCurrentAfterLast)
        {
            OnPropertyChanged(nameof(IsCurrentAfterLast));
        }

        RaiseCurrentChangedIfNeeded(previousCurrentItem, previousCurrentPosition);
    }

    private void OnSortDescriptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        Refresh();
    }

    private void OnGroupDescriptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        Refresh();
    }

    private void AttachSourceCollection(IEnumerable source)
    {
        _sourceNotifier = source as INotifyCollectionChanged;
        if (_sourceNotifier != null)
        {
            _sourceNotifier.CollectionChanged += OnSourceCollectionChanged;
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class GroupBucket
    {
        public GroupBucket(object? key)
        {
            Key = key;
        }

        public object? Key { get; }

        public List<object?> Items { get; } = [];
    }
}
