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
        if (IsHierarchicalDataMode)
        {
            return new VirtualizingTreeDataHost(this);
        }

        if (ItemsPanel != null)
        {
            return ItemsPanel.Build(this);
        }

        return new VirtualizingTreeItemsHost
        {
            Orientation = Orientation.Vertical,
            CacheLength = 2f
        };
    }

    protected override void OnItemsChanged()
    {
        base.OnItemsChanged();
        if (IsHierarchicalDataMode)
        {
            RefreshHierarchicalDataRows();
            return;
        }

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
    }

    private void OnActiveScrollViewerViewportChanged(object? sender, EventArgs args)
    {
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
                dataHost.InvalidateMeasure();
                dataHost.InvalidateArrange();
            }
            else
            {
                dataHost.RefreshForStableViewportOffsetChange();
            }
        }
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
    }

    private void UpdateItemsHost()
    {
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
