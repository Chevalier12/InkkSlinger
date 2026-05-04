using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public partial class TreeView
{
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

    private IEnumerable<object> GetHierarchicalRootItems()
    {
        if (_hierarchicalItemsSource is IEnumerable<object> enumerable)
        {
            return enumerable;
        }

        if (_hierarchicalItemsSource is System.Collections.IEnumerable nonGeneric && _hierarchicalItemsSource is not string)
        {
            return nonGeneric.Cast<object>();
        }

        return _hierarchicalItemsSource != null
            ? new[] { _hierarchicalItemsSource }
            : Array.Empty<object>();
    }

    private void RefreshHierarchicalDataRows()
    {
        if (_itemsHost is not VirtualizingTreeDataHost dataHost || _hierarchicalChildrenSelector == null)
        {
            return;
        }

        UnsubscribeHierarchicalCollections();
        _hierarchicalDataRows.Clear();
        foreach (var item in GetHierarchicalRootItems())
        {
            AddHierarchicalDataRow(item, depth: 0);
        }

        dataHost.SetRows(_hierarchicalDataRows);
        SyncSelectedHierarchicalContainer();
    }

    private void AddHierarchicalDataRow(object item, int depth)
    {
        var hasChildren = HasHierarchicalChildren(item);
        var expanded = hasChildren && IsHierarchicalItemExpandedCore(item);
        _hierarchicalDataRows.Add(new VisibleTreeDataEntry(item, depth, hasChildren, expanded));

        if (!expanded)
        {
            return;
        }

        var children = GetHierarchicalChildren(item);
        foreach (var child in children)
        {
            AddHierarchicalDataRow(child, depth + 1);
        }
    }

    private bool HasHierarchicalChildren(object item)
    {
        if (_hierarchicalHasChildrenSelector != null)
        {
            return _hierarchicalHasChildrenSelector(item);
        }

        return GetHierarchicalChildren(item).Count > 0;
    }

    private IReadOnlyList<object> GetHierarchicalChildren(object item)
    {
        if (_lazyLoadedHierarchicalChildren.TryGetValue(item, out var lazyChildren))
        {
            return lazyChildren;
        }

        if (_hierarchicalChildrenSelector?.Invoke(item) is not { } children)
        {
            return Array.Empty<object>();
        }

        if (children is INotifyCollectionChanged notifying)
        {
            SubscribeHierarchicalCollection(notifying);
        }

        return children as IReadOnlyList<object> ?? children.ToArray();
    }

    private bool IsHierarchicalItemExpandedCore(object item)
    {
        if (_hierarchicalExpansionOverrides.TryGetValue(item, out var expanded))
        {
            return expanded;
        }

        return _hierarchicalExpandedSelector?.Invoke(item) ?? false;
    }

    private string GetHierarchicalHeader(object item)
    {
        return _hierarchicalHeaderSelector?.Invoke(item) ?? item.ToString() ?? string.Empty;
    }

    private int FindHierarchicalRowIndex(object item)
    {
        return _hierarchicalDataRows.FindIndex(row => ReferenceEquals(row.Item, item) || Equals(row.Item, item));
    }

    private void ScrollHierarchicalRowIntoView(int rowIndex)
    {
        if (_itemsHost is not VirtualizingTreeDataHost dataHost)
        {
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
            viewer.ScrollToVerticalOffset(rowTop);
        }
        else if (rowBottom > viewportBottom)
        {
            viewer.ScrollToVerticalOffset(rowBottom - MathF.Max(rowHeight, viewer.ViewportHeight));
        }
    }

    private void EnsureLazyHierarchicalChildrenLoaded(object item)
    {
        if (_hierarchicalLazyChildrenLoader == null ||
            _lazyLoadedHierarchicalChildren.ContainsKey(item) ||
            GetHierarchicalChildren(item).Count > 0)
        {
            return;
        }

        var loaded = _hierarchicalLazyChildrenLoader(item);
        if (loaded == null)
        {
            return;
        }

        var children = loaded as IReadOnlyList<object> ?? loaded.ToArray();
        if (!TryAppendLazyChildrenToMutableSource(item, children))
        {
            _lazyLoadedHierarchicalChildren[item] = children;
        }
    }

    private bool TryAppendLazyChildrenToMutableSource(object item, IReadOnlyList<object> children)
    {
        if (children.Count == 0 ||
            _hierarchicalChildrenSelector?.Invoke(item) is not { } existing)
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

    private void SubscribeHierarchicalCollection(INotifyCollectionChanged collection)
    {
        if (_subscribedHierarchicalCollections.Add(collection))
        {
            collection.CollectionChanged += OnHierarchicalCollectionChanged;
        }
    }

    private void UnsubscribeHierarchicalCollections()
    {
        foreach (var collection in _subscribedHierarchicalCollections)
        {
            collection.CollectionChanged -= OnHierarchicalCollectionChanged;
        }

        _subscribedHierarchicalCollections.Clear();
    }

    private void OnHierarchicalCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        RefreshHierarchicalDataRows();
    }

    private TreeViewItem RealizeHierarchicalDataContainer(VisibleTreeDataEntry row, int rowIndex)
    {
        if (!_hierarchicalDataContainers.TryGetValue(row.Item, out var container))
        {
            container = _recycledHierarchicalDataContainers.Count > 0
                ? _recycledHierarchicalDataContainers.Dequeue()
                : new TreeViewItem();
            _hierarchicalDataContainers[row.Item] = container;
            ApplyTypographyToItem(container, null, Foreground);
        }

        ApplyHierarchicalDataContainer(container, row, rowIndex);
        return container;
    }

    private void ApplyHierarchicalDataContainer(TreeViewItem container, VisibleTreeDataEntry row, int rowIndex)
    {
        container.ClearVirtualizedDisplaySnapshot();
        container.Header = GetHierarchicalHeader(row.Item);
        container.Tag = row.Item;
        container.DataContext = row.Item;
        container.IsExpanded = row.IsExpanded;
        container.UseVirtualizedTreeLayout = true;
        container.VirtualizedTreeDepth = row.Depth;
        container.VirtualizedTreeRowIndex = rowIndex;
        container.HasVirtualizedChildItems = row.HasChildren;
        container.IsSelected = IsHierarchicalDataItemSelected(row.Item);
        PrepareContainerForItemOverride(container, row.Item, rowIndex);
        ApplyHierarchicalItemTemplate(container, row.Item);
        if (container.IsSelected)
        {
            SelectedItem = container;
        }
    }

    private void RecycleHierarchicalDataContainer(TreeViewItem container)
    {
        if (ReferenceEquals(container, SelectedItem))
        {
            return;
        }

        if (container.Tag is { } item)
        {
            _hierarchicalDataContainers.Remove(item);
        }

        container.IsSelected = false;
        container.ClearVirtualizedDisplaySnapshot();
        container.SetVirtualizedHeaderElement(null);
        container.Tag = null;
        container.DataContext = null;
        container.VirtualizedTreeRowIndex = -1;
        _recycledHierarchicalDataContainers.Enqueue(container);
    }

}
