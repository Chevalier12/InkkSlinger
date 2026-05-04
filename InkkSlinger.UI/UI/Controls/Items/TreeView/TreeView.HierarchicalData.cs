using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

public partial class TreeView
{
    private readonly record struct VisibleTreeDataEntry(object Item, int Depth, bool HasChildren, bool IsExpanded);

    private void RefreshHierarchicalDataMode()
    {
        var needsDataHost = IsHierarchicalDataMode;
        if (needsDataHost != (_itemsHost is VirtualizingTreeDataHost))
        {
            UpdateItemsHost();
            return;
        }

        if (needsDataHost)
        {
            RefreshHierarchicalDataRows();
        }
    }

    private void RefreshHierarchicalDataRows()
    {
        if (_itemsHost is VirtualizingTreeDataHost dataHost)
        {
            _hierarchicalData.RefreshRows(dataHost);
        }
    }

    private int FindHierarchicalRowIndex(object item)
    {
        return _hierarchicalData.FindRowIndex(item);
    }

    private VisibleTreeDataEntry GetHierarchicalRow(int rowIndex)
    {
        return _hierarchicalData.Rows[rowIndex];
    }

    private int HierarchicalRowCount => _hierarchicalData.Rows.Count;

    internal TreeViewHierarchicalRuntimeDiagnosticsSnapshot GetTreeViewHierarchicalSnapshotForDiagnostics()
    {
        var viewer = ActiveScrollViewer;
        var host = _itemsHost as VirtualizingTreeDataHost;
        return new TreeViewHierarchicalRuntimeDiagnosticsSnapshot(
            viewer.VerticalOffset,
            viewer.ExtentHeight,
            viewer.ViewportHeight,
            host?.AverageRowHeight ?? 0f,
            host?.MeasuredRowHeightAverageForDiagnostics ?? 0f,
            host?.MeasuredRowHeightCountForDiagnostics ?? 0,
            host?.FirstRealizedIndexForDiagnostics ?? -1,
            host?.LastRealizedIndexForDiagnostics ?? -1,
            host?.TotalExtentHeightForDiagnostics ?? 0f);
    }

    private void ScrollHierarchicalRowIntoView(int rowIndex)
    {
        _diagScrollHierarchicalRowIntoViewCallCount++;
        if (_itemsHost is not VirtualizingTreeDataHost dataHost)
        {
            _diagScrollHierarchicalRowIntoViewNoHostCount++;
            return;
        }

        var viewer = ActiveScrollViewer;
        var rowTop = dataHost.GetRowOffset(rowIndex);
        var rowHeight = dataHost.GetRowHeight(rowIndex);
        var rowBottom = rowTop + rowHeight;
        var viewportTop = viewer.VerticalOffset;
        var viewportBottom = viewportTop + MathF.Max(0f, viewer.ViewportHeight);

        if (rowTop < viewportTop)
        {
            _diagScrollHierarchicalRowIntoViewScrollUpCount++;
            viewer.ScrollToVerticalOffset(rowTop);
        }
        else if (rowBottom > viewportBottom)
        {
            _diagScrollHierarchicalRowIntoViewScrollDownCount++;
            viewer.ScrollToVerticalOffset(rowBottom - MathF.Max(rowHeight, viewer.ViewportHeight));
        }
        else
        {
            _diagScrollHierarchicalRowIntoViewAlreadyVisibleCount++;
        }
    }

    private void ApplyHierarchicalItemTemplate(TreeViewItem container, object item)
    {
        var selectedTemplate = DataTemplateResolver.ResolveTemplateForContent(
            this,
            item,
            ItemTemplate,
            ItemTemplateSelector,
            container);
        if (selectedTemplate == null)
        {
            container.SetVirtualizedHeaderElement(null);
            return;
        }

        container.SetVirtualizedHeaderElement(selectedTemplate.Build(item, this));
    }

    private TreeViewItem RealizeHierarchicalDataContainer(VisibleTreeDataEntry row, int rowIndex)
    {
        return _hierarchicalData.RealizeContainer(row, rowIndex);
    }

    private void ApplyHierarchicalDataContainer(TreeViewItem container, VisibleTreeDataEntry row, int rowIndex)
    {
        _hierarchicalData.ApplyContainer(container, row, rowIndex);
    }

    private void RecycleHierarchicalDataContainer(TreeViewItem container)
    {
        _hierarchicalData.RecycleContainer(container);
    }

    private string GetHierarchicalHeader(object item)
    {
        return _hierarchicalData.GetHeader(item);
    }

    private bool IsHierarchicalDataItemSelected(object item)
    {
        return _hierarchicalData.IsSelected(item);
    }

    private sealed class HierarchicalDataController
    {
        private readonly TreeView _owner;
        private readonly List<VisibleTreeDataEntry> _rows = new();
        private readonly Dictionary<object, TreeViewItem> _containers = new();
        private readonly Queue<TreeViewItem> _recycledContainers = new();
        private readonly Dictionary<object, bool> _expansionOverrides = new();
        private readonly Dictionary<object, IReadOnlyList<object>> _lazyLoadedChildren = new();
        private readonly HashSet<INotifyCollectionChanged> _subscribedCollections = new();
        private object? _itemsSource;
        private Func<object, IEnumerable<object>>? _childrenSelector;
        private Func<object, bool>? _hasChildrenSelector;
        private Func<object, string>? _headerSelector;
        private Func<object, bool>? _expandedSelector;
        private Func<object, IEnumerable<object>?>? _lazyChildrenLoader;

        public HierarchicalDataController(TreeView owner)
        {
            _owner = owner;
        }

        public IReadOnlyList<VisibleTreeDataEntry> Rows => _rows;

        public int RealizedContainerCount => _containers.Count;

        public bool IsActive => _itemsSource != null && _childrenSelector != null;

        public object? ItemsSource
        {
            get => _itemsSource;
            set
            {
                if (ReferenceEquals(_itemsSource, value))
                {
                    return;
                }

                _itemsSource = value;
                _expansionOverrides.Clear();
                _containers.Clear();
                _recycledContainers.Clear();
                _owner.RefreshHierarchicalDataMode();
            }
        }

        public Func<object, IEnumerable<object>>? ChildrenSelector
        {
            get => _childrenSelector;
            set
            {
                _childrenSelector = value;
                _owner.RefreshHierarchicalDataMode();
            }
        }

        public Func<object, bool>? HasChildrenSelector
        {
            get => _hasChildrenSelector;
            set
            {
                _hasChildrenSelector = value;
                _owner.RefreshHierarchicalDataMode();
            }
        }

        public Func<object, string>? HeaderSelector
        {
            get => _headerSelector;
            set
            {
                _headerSelector = value;
                _owner.RefreshHierarchicalDataMode();
            }
        }

        public Func<object, bool>? ExpandedSelector
        {
            get => _expandedSelector;
            set
            {
                _expandedSelector = value;
                _owner.RefreshHierarchicalDataMode();
            }
        }

        public Func<object, IEnumerable<object>?>? LazyChildrenLoader
        {
            get => _lazyChildrenLoader;
            set => _lazyChildrenLoader = value;
        }

        public void RefreshRows(VirtualizingTreeDataHost dataHost)
        {
            _diagHierarchicalRefreshRowsCallCount++;
            _owner._runtimeHierarchicalRefreshRowsCallCount++;
            if (_childrenSelector == null)
            {
                return;
            }

            UnsubscribeCollections();
            _rows.Clear();
            foreach (var item in GetRootItems())
            {
                AddRow(item, depth: 0);
            }

            dataHost.SetRows(_rows);
            SyncSelectedContainer();
        }

        public int FindRowIndex(object item)
        {
            _diagHierarchicalFindRowIndexCallCount++;
            return _rows.FindIndex(row => ReferenceEquals(row.Item, item) || Equals(row.Item, item));
        }

        public bool SetExpanded(object item, bool isExpanded)
        {
            _diagHierarchicalSetExpandedCallCount++;
            if (!IsActive)
            {
                return false;
            }

            if (isExpanded)
            {
                EnsureLazyChildrenLoaded(item);
            }

            _expansionOverrides[item] = isExpanded;
            _owner.RefreshHierarchicalDataRows();
            return true;
        }

        public void ToggleExpanded(TreeViewItem clickedItem)
        {
            _diagHierarchicalToggleExpandedCallCount++;
            var item = clickedItem.VirtualizedTreeDataItem;
            if (item == null)
            {
                return;
            }

            if (!clickedItem.IsExpanded)
            {
                EnsureLazyChildrenLoaded(item);
            }

            _expansionOverrides[item] = !clickedItem.IsExpanded;
            _owner.RefreshHierarchicalDataRows();
        }

        public bool IsExpanded(object item)
        {
            return IsActive && IsExpandedCore(item);
        }

        public string GetHeader(object item)
        {
            return _headerSelector?.Invoke(item) ?? item.ToString() ?? string.Empty;
        }

        public bool IsSelected(object item)
        {
            return _owner.SelectedDataItem is { } selectedItem &&
                   (ReferenceEquals(selectedItem, item) || Equals(selectedItem, item));
        }

        public TreeViewItem? ContainerFromItem(object item, Panel itemsHost)
        {
            if (!_containers.TryGetValue(item, out var container))
            {
                return null;
            }

            return container.VirtualizedTreeRowIndex >= 0 && ReferenceEquals(container.VisualParent, itemsHost)
                ? container
                : null;
        }

        public TreeViewItem RealizeContainer(VisibleTreeDataEntry row, int rowIndex)
        {
            _diagHierarchicalRealizeContainerCallCount++;
            _owner._runtimeHierarchicalRealizeContainerCallCount++;
            if (!_containers.TryGetValue(row.Item, out var container))
            {
                _diagHierarchicalRealizeContainerNewCount++;
                container = _recycledContainers.Count > 0
                    ? _recycledContainers.Dequeue()
                    : new TreeViewItem();
                _containers[row.Item] = container;
                ApplyTypographyToItem(container, null, _owner.Foreground);
            }
            else
            {
                _diagHierarchicalRealizeContainerRecycledCount++;
            }

            ApplyContainer(container, row, rowIndex);
            return container;
        }

        public void ApplyContainer(TreeViewItem container, VisibleTreeDataEntry row, int rowIndex)
        {
            _diagHierarchicalApplyContainerCallCount++;
            container.ClearVirtualizedDisplaySnapshot();
            container.Header = GetHeader(row.Item);
            container.VirtualizedTreeDataItem = row.Item;
            container.DataContext = row.Item;
            container.UseVirtualizedTreeLayout = true;
            container.VirtualizedTreeDepth = row.Depth;
            container.VirtualizedTreeRowIndex = rowIndex;
            container.ApplyVirtualizedBranchState(row.HasChildren, row.IsExpanded);
            container.IsSelected = IsSelected(row.Item);
            _owner.PrepareContainerForItemOverride(container, row.Item, rowIndex);
            _owner.ApplyHierarchicalItemTemplate(container, row.Item);
            if (container.IsSelected)
            {
                _owner.SelectedItem = container;
            }
        }

        public void RecycleContainer(TreeViewItem container)
        {
            _diagHierarchicalRecycleContainerCallCount++;
            _owner._runtimeHierarchicalRecycleContainerCallCount++;
            if (ReferenceEquals(container, _owner.SelectedItem))
            {
                _diagHierarchicalRecycleContainerKeptSelectedCount++;
                return;
            }

            _diagHierarchicalRecycleContainerRecycledCount++;
            if (container.VirtualizedTreeDataItem is { } item)
            {
                _containers.Remove(item);
            }

            container.IsSelected = false;
            container.ClearVirtualizedDisplaySnapshot(updateHasItems: false);
            container.SetVirtualizedHeaderElement(null);
            container.VirtualizedTreeDataItem = null;
            container.ClearVirtualizedBranchStateForRecycle();
            container.DataContext = null;
            container.VirtualizedTreeRowIndex = -1;
            _recycledContainers.Enqueue(container);
        }

        public void EnsureLazyChildrenLoaded(object item)
        {
            if (_lazyChildrenLoader == null ||
                _lazyLoadedChildren.ContainsKey(item) ||
                GetChildren(item).Count > 0)
            {
                return;
            }

            var loaded = _lazyChildrenLoader(item);
            if (loaded == null)
            {
                return;
            }

            var children = loaded as IReadOnlyList<object> ?? loaded.ToArray();
            if (!TryAppendLazyChildrenToMutableSource(item, children))
            {
                _lazyLoadedChildren[item] = children;
            }
        }

        private IEnumerable<object> GetRootItems()
        {
            if (_itemsSource is IEnumerable<object> enumerable)
            {
                return enumerable;
            }

            if (_itemsSource is System.Collections.IEnumerable nonGeneric && _itemsSource is not string)
            {
                return nonGeneric.Cast<object>();
            }

            return _itemsSource != null
                ? new[] { _itemsSource }
                : Array.Empty<object>();
        }

        private void AddRow(object item, int depth)
        {
            var hasChildren = HasChildren(item);
            var expanded = hasChildren && IsExpandedCore(item);
            _rows.Add(new VisibleTreeDataEntry(item, depth, hasChildren, expanded));

            if (!expanded)
            {
                return;
            }

            foreach (var child in GetChildren(item))
            {
                AddRow(child, depth + 1);
            }
        }

        private bool HasChildren(object item)
        {
            if (_hasChildrenSelector != null)
            {
                return _hasChildrenSelector(item);
            }

            return GetChildren(item).Count > 0;
        }

        private IReadOnlyList<object> GetChildren(object item)
        {
            if (_lazyLoadedChildren.TryGetValue(item, out var lazyChildren))
            {
                return lazyChildren;
            }

            if (_childrenSelector?.Invoke(item) is not { } children)
            {
                return Array.Empty<object>();
            }

            if (children is INotifyCollectionChanged notifying)
            {
                SubscribeCollection(notifying);
            }

            return children as IReadOnlyList<object> ?? children.ToArray();
        }

        private bool IsExpandedCore(object item)
        {
            if (_expansionOverrides.TryGetValue(item, out var expanded))
            {
                return expanded;
            }

            return _expandedSelector?.Invoke(item) ?? false;
        }

        private bool TryAppendLazyChildrenToMutableSource(object item, IReadOnlyList<object> children)
        {
            if (children.Count == 0 ||
                _childrenSelector?.Invoke(item) is not { } existing)
            {
                return false;
            }

            if (existing is not System.Collections.IList mutableChildren)
            {
                return false;
            }

            for (var i = 0; i < children.Count; i++)
            {
                mutableChildren.Add(children[i]);
            }

            return true;
        }

        private void SubscribeCollection(INotifyCollectionChanged collection)
        {
            if (_subscribedCollections.Add(collection))
            {
                collection.CollectionChanged += OnCollectionChanged;
            }
        }

        private void UnsubscribeCollections()
        {
            foreach (var collection in _subscribedCollections)
            {
                collection.CollectionChanged -= OnCollectionChanged;
            }

            _subscribedCollections.Clear();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
            _ = sender;
            _ = args;
            _owner.RefreshHierarchicalDataRows();
        }

        private void SyncSelectedContainer()
        {
            if (_owner.SelectedDataItem == null)
            {
                return;
            }

            var rowIndex = FindRowIndex(_owner.SelectedDataItem);
            if (rowIndex < 0)
            {
                if (_owner.SelectedItem != null)
                {
                    _owner.SelectedItem.IsSelected = false;
                }

                _owner.SelectedItem = null;
                return;
            }

            if (_containers.TryGetValue(_owner.SelectedDataItem, out var container))
            {
                container.IsSelected = true;
                _owner.SelectedItem = container;
            }
        }
    }
}
