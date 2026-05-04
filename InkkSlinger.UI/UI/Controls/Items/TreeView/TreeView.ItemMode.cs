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
    private void OnTrackedTreeItemExpandedStateChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        RefreshVirtualizedItemsHost();
    }

    private void RefreshTreeItemTracking()
    {
        _diagRefreshTreeItemTrackingCallCount++;
        _runtimeRefreshTreeItemTrackingCallCount++;
        var current = EnumerateAllTreeItems().ToHashSet();
        foreach (var removed in _trackedTreeItems.Where(item => !current.Contains(item)).ToArray())
        {
            removed.ExpandedStateChanged -= OnTrackedTreeItemExpandedStateChanged;
            removed.UseVirtualizedTreeLayout = false;
            removed.VirtualizedTreeDepth = 0;
            _trackedTreeItems.Remove(removed);
            _diagRefreshTreeItemTrackingRemovedCount++;
        }

        foreach (var item in current)
        {
            if (_trackedTreeItems.Add(item))
            {
                item.ExpandedStateChanged += OnTrackedTreeItemExpandedStateChanged;
                _diagRefreshTreeItemTrackingAddedCount++;
            }
        }
    }

    private IEnumerable<TreeViewItem> EnumerateAllTreeItems()
    {
        foreach (var container in ItemContainers)
        {
            if (container is not TreeViewItem root)
            {
                continue;
            }

            foreach (var item in EnumerateAllTreeItems(root))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<TreeViewItem> EnumerateAllTreeItems(TreeViewItem item)
    {
        yield return item;
        foreach (var child in item.GetChildTreeItems())
        {
            foreach (var descendant in EnumerateAllTreeItems(child))
            {
                yield return descendant;
            }
        }
    }

    private void RefreshVirtualizedItemsHost()
    {
        _diagRefreshVirtualizedItemsHostCallCount++;
        _runtimeRefreshVirtualizedItemsHostCallCount++;
        if (_itemsHost is not VirtualizingTreeItemsHost virtualizingHost)
        {
            _diagRefreshVirtualizedItemsHostNonVirtualizingPathCount++;
            foreach (var item in _trackedTreeItems)
            {
                item.UseVirtualizedTreeLayout = false;
                item.VirtualizedTreeDepth = 0;
            }

            return;
        }

        _diagRefreshVirtualizedItemsHostVirtualizingPathCount++;
        var visibleItems = GetVisibleItemEntries();
        var visibleSet = new HashSet<TreeViewItem>();
        foreach (var entry in visibleItems)
        {
            entry.Item.UseVirtualizedTreeLayout = true;
            entry.Item.VirtualizedTreeDepth = entry.Depth;
            visibleSet.Add(entry.Item);
        }

        foreach (var item in _trackedTreeItems)
        {
            if (visibleSet.Contains(item))
            {
                continue;
            }

            item.UseVirtualizedTreeLayout = true;
            item.VirtualizedTreeDepth = 0;
            DetachVirtualizedTreeItem(item);
        }

        virtualizingHost.SetVisibleItems(visibleItems);
    }

    private void DetachVirtualizedTreeItem(TreeViewItem item)
    {
        if (ReferenceEquals(item.VisualParent, _itemsHost))
        {
            return;
        }

        if (item.VisualParent is Panel visualPanel)
        {
            visualPanel.RemoveChild(item);
        }
        else if (item.VisualParent != null)
        {
            item.SetVisualParent(null);
        }

        item.InvalidateMeasure();
        item.InvalidateArrange();
    }

    private List<TreeViewItem> GetVisibleItems()
    {
        return GetVisibleItemEntries().Select(static entry => entry.Item).ToList();
    }

    private List<VisibleTreeItemEntry> GetVisibleItemEntries()
    {
        _diagGetVisibleItemEntriesCallCount++;
        _runtimeGetVisibleItemEntriesCallCount++;
        var result = new List<VisibleTreeItemEntry>();
        foreach (var container in ItemContainers)
        {
            if (container is not TreeViewItem root)
            {
                continue;
            }

            AddVisible(root, depth: 0, result);
        }

        return result;
    }

    private static void AddVisible(TreeViewItem item, int depth, IList<VisibleTreeItemEntry> output)
    {
        output.Add(new VisibleTreeItemEntry(item, depth));

        if (!item.IsExpanded)
        {
            return;
        }

        foreach (var childItem in item.GetChildTreeItems())
        {
            AddVisible(childItem, depth + 1, output);
        }
    }

    private static TreeViewItem? GetFirstChild(TreeViewItem item)
    {
        foreach (var childItem in item.GetChildTreeItems())
        {
            return childItem;
        }

        return null;
    }

    private static TreeViewItem? GetParentTreeItem(TreeViewItem item)
    {
        for (var current = item.VisualParent ?? item.LogicalParent; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TreeViewItem parent)
            {
                return parent;
            }
        }

        return null;
    }

    private void PropagateTypographyFromTree(
        Color? oldForeground,
        Color? newForeground)
    {
        _diagPropagateTypographyFromTreeCallCount++;
        foreach (var container in ItemContainers)
        {
            if (container is not TreeViewItem item)
            {
                continue;
            }

            ApplyTypographyRecursive(item, oldForeground, newForeground);
        }
    }

    private static void ApplyTypographyRecursive(
        TreeViewItem item,
        Color? oldForeground,
        Color? newForeground)
    {
        ApplyTypographyToItem(item, oldForeground, newForeground);
        foreach (var child in item.GetChildTreeItems())
        {
            ApplyTypographyRecursive(child, oldForeground, newForeground);
        }
    }

    private static void ApplyTypographyToItem(
        TreeViewItem item,
        Color? oldForeground,
        Color? newForeground)
    {
        item.ApplyPropagatedForeground(oldForeground, newForeground);
    }

    private sealed class ScrollContentStackPanel : StackPanel, IScrollTransformContent
    {
    }

    private readonly record struct VisibleTreeItemEntry(TreeViewItem Item, int Depth);

}
