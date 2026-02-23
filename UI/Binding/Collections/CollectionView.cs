using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
        RefreshCore("CollectionView.Refresh", null);
    }

    private void RefreshCore(string source, NotifyCollectionChangedAction? sourceAction)
    {
        var previousCurrentItem = _currentItem;
        var previousCurrentPosition = _currentPosition;
        var previousGroups = _groups;
        var sourceBreakdownEnabled =
            SourceCollectionDispatchDiagnostics.Enabled &&
            sourceAction.HasValue &&
            string.Equals(source, "CollectionView.OnSourceCollectionChanged", StringComparison.Ordinal);
        var diagnosticsEnabled = CollectionViewCpuDiagnostics.Enabled || sourceBreakdownEnabled;
        var totalStart = diagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        var phaseStart = diagnosticsEnabled ? totalStart : 0L;

        var sourceItems = BuildSourceSnapshot();
        var materializeMs = diagnosticsEnabled ? Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds : 0d;
        phaseStart = diagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;

        var filtered = ApplyFilter(sourceItems);
        var filterMs = diagnosticsEnabled ? Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds : 0d;
        phaseStart = diagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;

        ApplySort(filtered);
        var sortMs = diagnosticsEnabled ? Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds : 0d;
        phaseStart = diagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;

        _viewItems.Clear();
        _viewItems.AddRange(filtered);
        _groups = BuildGroupsProjection(_viewItems);
        var groupsChanged = !ReferenceEquals(previousGroups, _groups);
        var groupMs = diagnosticsEnabled ? Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds : 0d;
        phaseStart = diagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;

        RepairCurrent(previousCurrentItem);
        var currentRepairMs = diagnosticsEnabled ? Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds : 0d;
        var notifyDiagnostics = RaiseRefreshNotifications(previousCurrentItem, previousCurrentPosition, groupsChanged, diagnosticsEnabled);

        if (!diagnosticsEnabled)
        {
            return;
        }

        var totalMs = Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds;
        if (CollectionViewCpuDiagnostics.Enabled)
        {
            CollectionViewCpuDiagnostics.ObserveRefresh(
                totalMs,
                materializeMs,
                filterMs,
                sortMs,
                groupMs,
                currentRepairMs,
                notifyDiagnostics,
                _viewItems.Count,
                _groups.Count,
                source);
        }

        if (sourceBreakdownEnabled)
        {
            var action = sourceAction.GetValueOrDefault(NotifyCollectionChangedAction.Reset);
            SourceCollectionDispatchDiagnostics.ObserveCollectionViewRefreshBreakdown(
                source,
                action,
                totalMs,
                materializeMs,
                filterMs,
                sortMs,
                groupMs,
                currentRepairMs,
                notifyDiagnostics.TotalMs,
                notifyDiagnostics.CollectionChangedMs,
                notifyDiagnostics.PropertyGroupsMs,
                notifyDiagnostics.PropertyCurrentPositionMs,
                notifyDiagnostics.PropertyCurrentItemMs,
                notifyDiagnostics.PropertyBeforeFirstMs,
                notifyDiagnostics.PropertyAfterLastMs,
                notifyDiagnostics.CurrentChangedMs,
                FormatTopHandlers(notifyDiagnostics.CollectionChangedHandlerTimings),
                FormatTopHandlers(notifyDiagnostics.PropertyGroupsHandlerTimings),
                FormatTopHandlers(notifyDiagnostics.PropertyCurrentPositionHandlerTimings),
                FormatTopHandlers(notifyDiagnostics.PropertyCurrentItemHandlerTimings),
                FormatTopHandlers(notifyDiagnostics.CurrentChangedHandlerTimings),
                _viewItems.Count,
                _groups.Count);
        }
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

        RefreshCore("CollectionView.OnSourceCollectionChanged", e.Action);
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

        var sourceDiagnosticsEnabled = SourceCollectionDispatchDiagnostics.Enabled;
        var totalStart = sourceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        var phaseStart = sourceDiagnosticsEnabled ? totalStart : 0L;
        var previousCurrentItem = _currentItem;
        var previousCurrentPosition = _currentPosition;
        var previousBeforeFirst = IsCurrentBeforeFirst;
        var previousAfterLast = IsCurrentAfterLast;

        for (var i = 0; i < e.NewItems.Count; i++)
        {
            _viewItems.Insert(insertIndex + i, e.NewItems[i]);
        }
        var insertMs = sourceDiagnosticsEnabled ? Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds : 0d;
        phaseStart = sourceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;

        RepairCurrent(previousCurrentItem);
        var currentRepairMs = sourceDiagnosticsEnabled ? Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds : 0d;

        if (!sourceDiagnosticsEnabled)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, e.NewItems, insertIndex));
            RaiseCurrencyNotificationsIfNeeded(previousCurrentItem, previousCurrentPosition, previousBeforeFirst, previousAfterLast);
            return true;
        }

        phaseStart = Stopwatch.GetTimestamp();
        var collectionChanged = CollectionChanged;
        var propertyChanged = PropertyChanged;
        var currentChanged = CurrentChanged;
        var collectionChangedTimings = new List<CollectionViewHandlerTiming>(collectionChanged?.GetInvocationList().Length ?? 0);
        var propertyCurrentPositionTimings = new List<CollectionViewHandlerTiming>(propertyChanged?.GetInvocationList().Length ?? 0);
        var propertyCurrentItemTimings = new List<CollectionViewHandlerTiming>(propertyChanged?.GetInvocationList().Length ?? 0);
        var propertyBeforeFirstTimings = new List<CollectionViewHandlerTiming>(propertyChanged?.GetInvocationList().Length ?? 0);
        var propertyAfterLastTimings = new List<CollectionViewHandlerTiming>(propertyChanged?.GetInvocationList().Length ?? 0);
        var currentChangedTimings = new List<CollectionViewHandlerTiming>(currentChanged?.GetInvocationList().Length ?? 0);

        var addArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, e.NewItems, insertIndex);
        var collectionChangedMs = InvokeCollectionChangedHandlers(collectionChanged, this, addArgs, collectionChangedTimings);

        var propertyCurrentPositionMs = 0d;
        if (previousCurrentPosition != _currentPosition)
        {
            propertyCurrentPositionMs = InvokePropertyChangedHandlers(
                propertyChanged,
                this,
                new PropertyChangedEventArgs(nameof(CurrentPosition)),
                propertyCurrentPositionTimings);
        }

        var propertyCurrentItemMs = 0d;
        if (!Equals(previousCurrentItem, _currentItem))
        {
            propertyCurrentItemMs = InvokePropertyChangedHandlers(
                propertyChanged,
                this,
                new PropertyChangedEventArgs(nameof(CurrentItem)),
                propertyCurrentItemTimings);
        }

        var propertyBeforeFirstMs = 0d;
        if (previousBeforeFirst != IsCurrentBeforeFirst)
        {
            propertyBeforeFirstMs = InvokePropertyChangedHandlers(
                propertyChanged,
                this,
                new PropertyChangedEventArgs(nameof(IsCurrentBeforeFirst)),
                propertyBeforeFirstTimings);
        }

        var propertyAfterLastMs = 0d;
        if (previousAfterLast != IsCurrentAfterLast)
        {
            propertyAfterLastMs = InvokePropertyChangedHandlers(
                propertyChanged,
                this,
                new PropertyChangedEventArgs(nameof(IsCurrentAfterLast)),
                propertyAfterLastTimings);
        }

        var currentChangedMs = 0d;
        if (!Equals(previousCurrentItem, _currentItem) || previousCurrentPosition != _currentPosition)
        {
            currentChangedMs = InvokeEventHandlers(currentChanged, this, EventArgs.Empty, currentChangedTimings);
        }

        var notifyMs = Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds;
        var totalMs = Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds;
        SourceCollectionDispatchDiagnostics.ObserveCollectionViewRefreshBreakdown(
            "CollectionView.OnSourceCollectionChanged.IncrementalAdd",
            NotifyCollectionChangedAction.Add,
            totalMs,
            insertMs,
            0d,
            0d,
            0d,
            currentRepairMs,
            notifyMs,
            collectionChangedMs,
            0d,
            propertyCurrentPositionMs,
            propertyCurrentItemMs,
            propertyBeforeFirstMs,
            propertyAfterLastMs,
            currentChangedMs,
            FormatTopHandlers(collectionChangedTimings),
            "none",
            FormatTopHandlers(propertyCurrentPositionTimings),
            FormatTopHandlers(propertyCurrentItemTimings),
            FormatTopHandlers(currentChangedTimings),
            _viewItems.Count,
            _groups.Count);
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

    private CollectionViewNotificationDiagnostics RaiseRefreshNotifications(
        object? previousCurrentItem,
        int previousCurrentPosition,
        bool groupsChanged,
        bool diagnosticsEnabled)
    {
        if (!diagnosticsEnabled)
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
            return new CollectionViewNotificationDiagnostics(
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0,
                0,
                0,
                Array.Empty<CollectionViewHandlerTiming>(),
                Array.Empty<CollectionViewHandlerTiming>(),
                Array.Empty<CollectionViewHandlerTiming>(),
                Array.Empty<CollectionViewHandlerTiming>(),
                Array.Empty<CollectionViewHandlerTiming>(),
                Array.Empty<CollectionViewHandlerTiming>(),
                Array.Empty<CollectionViewHandlerTiming>());
        }

        var totalStart = diagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;

        var collectionChanged = CollectionChanged;
        var propertyChanged = PropertyChanged;
        var currentChanged = CurrentChanged;
        var collectionChangedSubscriberCount = collectionChanged?.GetInvocationList().Length ?? 0;
        var propertyChangedSubscriberCount = propertyChanged?.GetInvocationList().Length ?? 0;
        var currentChangedSubscriberCount = currentChanged?.GetInvocationList().Length ?? 0;
        var collectionChangedTimings = new List<CollectionViewHandlerTiming>(collectionChangedSubscriberCount);
        var propertyGroupsTimings = new List<CollectionViewHandlerTiming>(propertyChangedSubscriberCount);
        var propertyCurrentPositionTimings = new List<CollectionViewHandlerTiming>(propertyChangedSubscriberCount);
        var propertyCurrentItemTimings = new List<CollectionViewHandlerTiming>(propertyChangedSubscriberCount);
        var propertyBeforeFirstTimings = new List<CollectionViewHandlerTiming>(propertyChangedSubscriberCount);
        var propertyAfterLastTimings = new List<CollectionViewHandlerTiming>(propertyChangedSubscriberCount);
        var currentChangedTimings = new List<CollectionViewHandlerTiming>(currentChangedSubscriberCount);

        var resetArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
        var groupsArgs = new PropertyChangedEventArgs(nameof(Groups));
        var currentPositionArgs = new PropertyChangedEventArgs(nameof(CurrentPosition));
        var currentItemArgs = new PropertyChangedEventArgs(nameof(CurrentItem));
        var beforeFirstArgs = new PropertyChangedEventArgs(nameof(IsCurrentBeforeFirst));
        var afterLastArgs = new PropertyChangedEventArgs(nameof(IsCurrentAfterLast));

        var collectionChangedMs = InvokeCollectionChangedHandlers(collectionChanged, this, resetArgs, collectionChangedTimings);

        var propertyGroupsMs = groupsChanged
            ? InvokePropertyChangedHandlers(propertyChanged, this, groupsArgs, propertyGroupsTimings)
            : 0d;

        var propertyCurrentPositionMs = InvokePropertyChangedHandlers(propertyChanged, this, currentPositionArgs, propertyCurrentPositionTimings);

        var propertyCurrentItemMs = InvokePropertyChangedHandlers(propertyChanged, this, currentItemArgs, propertyCurrentItemTimings);

        var propertyBeforeFirstMs = InvokePropertyChangedHandlers(propertyChanged, this, beforeFirstArgs, propertyBeforeFirstTimings);

        var propertyAfterLastMs = InvokePropertyChangedHandlers(propertyChanged, this, afterLastArgs, propertyAfterLastTimings);

        var currentChangedMs = 0d;
        if (!Equals(previousCurrentItem, _currentItem) || previousCurrentPosition != _currentPosition)
        {
            currentChangedMs = InvokeEventHandlers(currentChanged, this, EventArgs.Empty, currentChangedTimings);
        }

        var totalMs = Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds;
        return new CollectionViewNotificationDiagnostics(
            totalMs,
            collectionChangedMs,
            propertyGroupsMs,
            propertyCurrentPositionMs,
            propertyCurrentItemMs,
            propertyBeforeFirstMs,
            propertyAfterLastMs,
            currentChangedMs,
            collectionChangedSubscriberCount,
            propertyChangedSubscriberCount,
            currentChangedSubscriberCount,
            collectionChangedTimings,
            propertyGroupsTimings,
            propertyCurrentPositionTimings,
            propertyCurrentItemTimings,
            propertyBeforeFirstTimings,
            propertyAfterLastTimings,
            currentChangedTimings);
    }

    private static double InvokeCollectionChangedHandlers(
        NotifyCollectionChangedEventHandler? handlers,
        object sender,
        NotifyCollectionChangedEventArgs args,
        List<CollectionViewHandlerTiming>? timings)
    {
        if (handlers == null)
        {
            return 0d;
        }

        var start = Stopwatch.GetTimestamp();
        var invocationList = handlers.GetInvocationList();
        for (var i = 0; i < invocationList.Length; i++)
        {
            var handler = (NotifyCollectionChangedEventHandler)invocationList[i];
            var handlerStart = timings != null ? Stopwatch.GetTimestamp() : 0L;
            handler(sender, args);
            if (timings != null)
            {
                timings.Add(new CollectionViewHandlerTiming(GetHandlerKey(invocationList[i]), Stopwatch.GetElapsedTime(handlerStart).TotalMilliseconds));
            }
        }

        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    private static double InvokePropertyChangedHandlers(
        PropertyChangedEventHandler? handlers,
        object sender,
        PropertyChangedEventArgs args,
        List<CollectionViewHandlerTiming>? timings)
    {
        if (handlers == null)
        {
            return 0d;
        }

        var start = Stopwatch.GetTimestamp();
        var invocationList = handlers.GetInvocationList();
        for (var i = 0; i < invocationList.Length; i++)
        {
            var handler = (PropertyChangedEventHandler)invocationList[i];
            var handlerStart = timings != null ? Stopwatch.GetTimestamp() : 0L;
            handler(sender, args);
            if (timings != null)
            {
                timings.Add(new CollectionViewHandlerTiming(GetHandlerKey(invocationList[i]), Stopwatch.GetElapsedTime(handlerStart).TotalMilliseconds));
            }
        }

        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    private static double InvokeEventHandlers(
        EventHandler? handlers,
        object sender,
        EventArgs args,
        List<CollectionViewHandlerTiming>? timings)
    {
        if (handlers == null)
        {
            return 0d;
        }

        var start = Stopwatch.GetTimestamp();
        var invocationList = handlers.GetInvocationList();
        for (var i = 0; i < invocationList.Length; i++)
        {
            var handler = (EventHandler)invocationList[i];
            var handlerStart = timings != null ? Stopwatch.GetTimestamp() : 0L;
            handler(sender, args);
            if (timings != null)
            {
                timings.Add(new CollectionViewHandlerTiming(GetHandlerKey(invocationList[i]), Stopwatch.GetElapsedTime(handlerStart).TotalMilliseconds));
            }
        }

        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    private static string GetHandlerKey(Delegate handler)
    {
        var targetType = handler.Target?.GetType().Name ?? "static";
        return $"{targetType}.{handler.Method.Name}";
    }

    private static string FormatTopHandlers(IReadOnlyList<CollectionViewHandlerTiming> timings)
    {
        if (timings.Count == 0)
        {
            return "none";
        }

        return string.Join(
            " | ",
            timings
                .OrderByDescending(static t => t.ElapsedMs)
                .Take(3)
                .Select(static t => $"{t.HandlerKey}:{t.ElapsedMs:0.###}ms"));
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
