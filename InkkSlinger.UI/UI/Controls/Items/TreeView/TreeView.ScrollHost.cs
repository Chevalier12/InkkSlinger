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
    private Panel CreateItemsHost()
    {
        _diagCreateItemsHostCallCount++;
        if (IsHierarchicalDataMode)
        {
            _diagCreateItemsHostHierarchicalPathCount++;
            return new VirtualizingTreeDataHost(this);
        }

        if (ItemsPanel != null)
        {
            _diagCreateItemsHostItemsPanelPathCount++;
            return ItemsPanel.Build(this);
        }

        _diagCreateItemsHostVirtualizingPathCount++;
        return new VirtualizingTreeItemsHost
        {
            Orientation = Orientation.Vertical,
            CacheLength = 2f
        };
    }

    protected override void OnItemsChanged()
    {
        _diagOnItemsChangedCallCount++;
        _runtimeOnItemsChangedCallCount++;
        base.OnItemsChanged();
        if (IsHierarchicalDataMode)
        {
            _diagOnItemsChangedHierarchicalPathCount++;
            RefreshHierarchicalDataRows();
            return;
        }

        _diagOnItemsChangedTreeItemPathCount++;
        RefreshTreeItemTracking();
        RefreshVirtualizedItemsHost();
    }

    private void ConfigureScrollViewer(ScrollViewer viewer)
    {
        viewer.HorizontalScrollBarVisibility = HorizontalScrollBarVisibility;
        viewer.VerticalScrollBarVisibility = VerticalScrollBarVisibility;
        UpdateScrollViewerViewportSubscription(viewer);
    }

    private void UpdateScrollViewerViewportSubscription(ScrollViewer viewer)
    {
        if (ReferenceEquals(_viewportSubscribedScrollViewer, viewer))
        {
            return;
        }

        if (_viewportSubscribedScrollViewer != null)
        {
            _viewportSubscribedScrollViewer.ViewportChanged -= OnActiveScrollViewerViewportChanged;
        }

        _viewportSubscribedScrollViewer = viewer;
        _viewportSubscribedScrollViewer.ViewportChanged += OnActiveScrollViewerViewportChanged;
        ResetDataHostViewportCache();
    }

    private void OnActiveScrollViewerViewportChanged(object? sender, EventArgs args)
    {
        _diagOnActiveScrollViewerViewportChangedCallCount++;
        _runtimeOnActiveScrollViewerViewportChangedCallCount++;
        _ = sender;
        _ = args;
        if (_itemsHost is VirtualizingTreeDataHost dataHost)
        {
            var activeScrollViewer = ActiveScrollViewer;
            var viewportWidth = activeScrollViewer.ViewportWidth;
            var viewportHeight = activeScrollViewer.ViewportHeight;
            var viewportSizeChanged =
                !AreClose(_lastDataHostViewportWidth, viewportWidth) ||
                !AreClose(_lastDataHostViewportHeight, viewportHeight);

            _lastDataHostViewportWidth = viewportWidth;
            _lastDataHostViewportHeight = viewportHeight;
            if (viewportSizeChanged)
            {
                _diagOnActiveScrollViewerViewportChangedSizeChangedCount++;
                dataHost.InvalidateMeasure();
                dataHost.InvalidateArrange();
            }
            else
            {
                _diagOnActiveScrollViewerViewportChangedOffsetChangedCount++;
                dataHost.RefreshForStableViewportOffsetChange();
            }
        }
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }

    private void ResetDataHostViewportCache()
    {
        _lastDataHostViewportWidth = float.NaN;
        _lastDataHostViewportHeight = float.NaN;
    }

    private void RememberActiveDataHostViewport()
    {
        if (_itemsHost is not VirtualizingTreeDataHost)
        {
            ResetDataHostViewportCache();
            return;
        }

        var activeScrollViewer = ActiveScrollViewer;
        _lastDataHostViewportWidth = activeScrollViewer.ViewportWidth;
        _lastDataHostViewportHeight = activeScrollViewer.ViewportHeight;
    }

    private void UpdateItemsHost()
    {
        _diagUpdateItemsHostCallCount++;
        var nextHost = CreateItemsHost();
        if (ReferenceEquals(nextHost, _itemsHost))
        {
            return;
        }

        _itemsHost = nextHost;
        ActiveScrollViewer.Content = _itemsHost;
        AttachItemsHost(_itemsHost);
        if (IsHierarchicalDataMode)
        {
            RefreshHierarchicalDataRows();
        }
        else
        {
            RefreshTreeItemTracking();
            RefreshVirtualizedItemsHost();
        }
        InvalidateMeasure();
    }

    private void AttachItemsHostToActiveScrollViewer()
    {
        _diagAttachItemsHostToActiveScrollViewerCallCount++;
        if (!ReferenceEquals(_fallbackScrollViewer, ActiveScrollViewer) && ReferenceEquals(_fallbackScrollViewer.Content, _itemsHost))
        {
            _fallbackScrollViewer.Content = null;
        }

        if (_templatedScrollViewer != null &&
            !ReferenceEquals(_templatedScrollViewer, ActiveScrollViewer) &&
            ReferenceEquals(_templatedScrollViewer.Content, _itemsHost))
        {
            _templatedScrollViewer.Content = null;
        }

        if (!ReferenceEquals(ActiveScrollViewer.Content, _itemsHost))
        {
            ActiveScrollViewer.Content = _itemsHost;
        }

        AttachItemsHost(_itemsHost);
        if (IsHierarchicalDataMode)
        {
            RefreshHierarchicalDataRows();
        }
        else
        {
            RefreshTreeItemTracking();
            RefreshVirtualizedItemsHost();
        }
    }

    private void RestoreFallbackScrollViewer()
    {
        _templatedScrollViewer = null;
        if (!ReferenceEquals(_fallbackScrollViewer.VisualParent, this))
        {
            _fallbackScrollViewer.SetVisualParent(this);
            _fallbackScrollViewer.SetLogicalParent(this);
        }

        ConfigureScrollViewer(_fallbackScrollViewer);
        AttachItemsHostToActiveScrollViewer();
    }

    private void DetachFallbackScrollViewer()
    {
        if (ReferenceEquals(_fallbackScrollViewer.Content, _itemsHost))
        {
            _fallbackScrollViewer.Content = null;
        }

        _fallbackScrollViewer.SetVisualParent(null);
        _fallbackScrollViewer.SetLogicalParent(null);
    }

}
