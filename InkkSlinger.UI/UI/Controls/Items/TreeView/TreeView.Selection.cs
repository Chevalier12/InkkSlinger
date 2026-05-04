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
    private bool HandleHierarchicalKeyDown(Keys key)
    {
        if (HierarchicalRowCount == 0)
        {
            return false;
        }

        var selectedIndex = _selectedDataItem == null ? -1 : FindHierarchicalRowIndex(_selectedDataItem);
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        switch (key)
        {
            case Keys.Up:
                return SelectHierarchicalRow(Math.Max(0, selectedIndex - 1));
            case Keys.Down:
                return SelectHierarchicalRow(Math.Min(HierarchicalRowCount - 1, selectedIndex + 1));
            case Keys.Home:
                return SelectHierarchicalRow(0);
            case Keys.End:
                return SelectHierarchicalRow(HierarchicalRowCount - 1);
            case Keys.PageUp:
                return SelectHierarchicalRow(Math.Max(0, selectedIndex - EstimateHierarchicalPageStep()));
            case Keys.PageDown:
                return SelectHierarchicalRow(Math.Min(HierarchicalRowCount - 1, selectedIndex + EstimateHierarchicalPageStep()));
            case Keys.Right:
                return ExpandOrEnterHierarchicalRow(selectedIndex);
            case Keys.Left:
                return CollapseOrSelectHierarchicalParent(selectedIndex);
            default:
                return false;
        }
    }

    private bool HandleTreeItemKeyDown(Keys key)
    {
        var visibleItems = GetVisibleItems();
        if (visibleItems.Count == 0)
        {
            return false;
        }

        var selectedIndex = SelectedItem == null ? -1 : visibleItems.FindIndex(item => ReferenceEquals(item, SelectedItem));
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        switch (key)
        {
            case Keys.Up:
                ApplySelectedItem(visibleItems[Math.Max(0, selectedIndex - 1)]);
                return true;
            case Keys.Down:
                ApplySelectedItem(visibleItems[Math.Min(visibleItems.Count - 1, selectedIndex + 1)]);
                return true;
            case Keys.Home:
                ApplySelectedItem(visibleItems[0]);
                return true;
            case Keys.End:
                ApplySelectedItem(visibleItems[^1]);
                return true;
            case Keys.Right:
                if (SelectedItem is { } selected && selected.HasChildItems())
                {
                    if (!selected.IsExpanded)
                    {
                        selected.IsExpanded = true;
                        RefreshVirtualizedItemsHost();
                    }
                    else if (GetFirstChild(selected) is { } child)
                    {
                        ApplySelectedItem(child);
                    }

                    return true;
                }

                return false;
            case Keys.Left:
                if (SelectedItem is { } current)
                {
                    if (current.IsExpanded)
                    {
                        current.IsExpanded = false;
                        RefreshVirtualizedItemsHost();
                        return true;
                    }

                    if (GetParentTreeItem(current) is { } parent)
                    {
                        ApplySelectedItem(parent);
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    private TreeViewItem? FindItemFromSource(UIElement? source)
    {
        for (var current = source; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TreeViewItem item)
            {
                return item;
            }

            if (ReferenceEquals(current, this))
            {
                break;
            }
        }

        return null;
    }

    private void OnMouseLeftButtonDownSelectItem(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (!IsEnabled || args.Button != MouseButton.Left)
        {
            return;
        }

        var clickedItem = FindItemFromSource(args.OriginalSource as UIElement);
        if (clickedItem == null)
        {
            return;
        }

        FocusManager.SetFocus(this);
        if (clickedItem.HitExpander(GetExpanderHitTestPoint(clickedItem, args.Position)))
        {
            if (IsHierarchicalDataMode && clickedItem.VirtualizedTreeDataItem != null)
            {
                _hierarchicalData.ToggleExpanded(clickedItem);
            }
            else
            {
                clickedItem.IsExpanded = !clickedItem.IsExpanded;
                RefreshVirtualizedItemsHost();
            }
        }

        ApplySelectedItem(clickedItem);
        args.Handled = true;
    }

    private void OnPreviewMouseLeftButtonUpRefreshDeferredScroll(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_itemsHost is VirtualizingTreeDataHost dataHost)
        {
            dataHost.RefreshPendingStableViewportOffsetChange();
        }
    }

    private bool SelectHierarchicalRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= HierarchicalRowCount)
        {
            return false;
        }

        var row = GetHierarchicalRow(rowIndex);
        ScrollHierarchicalRowIntoView(rowIndex);
        ApplySelectedItem(RealizeHierarchicalDataContainer(row, rowIndex));
        return true;
    }

    private bool ExpandOrEnterHierarchicalRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= HierarchicalRowCount)
        {
            return false;
        }

        var row = GetHierarchicalRow(rowIndex);
        if (!row.HasChildren)
        {
            return false;
        }

        if (!row.IsExpanded)
        {
            SetHierarchicalItemExpanded(row.Item, true);
            SelectHierarchicalRow(FindHierarchicalRowIndex(row.Item));
            return true;
        }

        var next = rowIndex + 1;
        if (next < HierarchicalRowCount && GetHierarchicalRow(next).Depth > row.Depth)
        {
            return SelectHierarchicalRow(next);
        }

        return true;
    }

    private bool CollapseOrSelectHierarchicalParent(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= HierarchicalRowCount)
        {
            return false;
        }

        var row = GetHierarchicalRow(rowIndex);
        if (row.HasChildren && row.IsExpanded)
        {
            SetHierarchicalItemExpanded(row.Item, false);
            SelectHierarchicalRow(FindHierarchicalRowIndex(row.Item));
            return true;
        }

        for (var i = rowIndex - 1; i >= 0; i--)
        {
            if (GetHierarchicalRow(i).Depth < row.Depth)
            {
                return SelectHierarchicalRow(i);
            }
        }

        return true;
    }

    private int EstimateHierarchicalPageStep()
    {
        if (_itemsHost is VirtualizingTreeDataHost dataHost)
        {
            return Math.Max(1, (int)MathF.Floor(ActiveScrollViewer.ViewportHeight / MathF.Max(1f, dataHost.AverageRowHeight)));
        }

        return 10;
    }

    private bool IsActiveScrollViewerThumbCaptured()
    {
        if (FocusManager.GetCapturedPointerElement() is not Thumb capturedThumb)
        {
            return false;
        }

        var activeViewer = ActiveScrollViewer;
        for (UIElement? current = capturedThumb; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, activeViewer))
            {
                return true;
            }
        }

        return false;
    }

    private Vector2 GetExpanderHitTestPoint(TreeViewItem item, Vector2 pointerPosition)
    {
        if (item.HitExpander(pointerPosition))
        {
            return pointerPosition;
        }

        var scrollViewer = ActiveScrollViewer;
        if (scrollViewer.Content is not UIElement content ||
            content is not IScrollTransformContent ||
            !ScrollViewer.GetUseTransformContentScrolling(content) ||
            !IsDescendantOrSelf(content, item))
        {
            return pointerPosition;
        }

        return new Vector2(
            pointerPosition.X + scrollViewer.HorizontalOffset,
            pointerPosition.Y + scrollViewer.VerticalOffset);
    }

    private static bool IsDescendantOrSelf(UIElement ancestor, UIElement candidate)
    {
        for (UIElement? current = candidate; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplySelectedItem(TreeViewItem? item)
    {
        if (ReferenceEquals(item, SelectedItem))
        {
            if (item == null)
            {
                _selectedDataItem = null;
            }

            return;
        }

        if (SelectedItem != null)
        {
            SelectedItem.IsSelected = false;
        }

        SelectedItem = item;
        _selectedDataItem = IsHierarchicalDataMode ? item?.VirtualizedTreeDataItem : item;
        if (SelectedItem != null)
        {
            SelectedItem.IsSelected = true;
        }

        RaiseRoutedEvent(SelectedItemChangedEvent, new RoutedSimpleEventArgs(SelectedItemChangedEvent));
    }

}
