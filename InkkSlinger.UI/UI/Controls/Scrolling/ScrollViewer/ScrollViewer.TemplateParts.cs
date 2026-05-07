using System;

namespace InkkSlinger;

public partial class ScrollViewer
{
    private void AttachTemplatePartPresenter(ScrollContentPresenter? presenter)
    {
        if (ReferenceEquals(_templateContentPresenter, presenter))
        {
            return;
        }

        _templateContentPresenter?.DetachScrollOwner(this);
        _templateContentPresenter = presenter;
        _templateContentPresenter?.AttachScrollOwner(this);
    }

    private void AttachTemplateScrollBars(ScrollBar? horizontalBar, ScrollBar? verticalBar)
    {
        if (horizontalBar != null && !ReferenceEquals(horizontalBar, _horizontalBar))
        {
            DetachInternalScrollBar(_horizontalBar, OnHorizontalScrollBarValueChanged);
            _horizontalBar = horizontalBar;
            _horizontalBar.Orientation = Orientation.Horizontal;
            _horizontalBar.ValueChanged += OnHorizontalScrollBarValueChanged;
            _horizontalBar.ThumbDragCompleted += OnScrollBarThumbDragCompleted;
        }

        if (verticalBar != null && !ReferenceEquals(verticalBar, _verticalBar))
        {
            DetachInternalScrollBar(_verticalBar, OnVerticalScrollBarValueChanged);
            _verticalBar = verticalBar;
            _verticalBar.Orientation = Orientation.Vertical;
            _verticalBar.ValueChanged += OnVerticalScrollBarValueChanged;
            _verticalBar.ThumbDragCompleted += OnScrollBarThumbDragCompleted;
        }

        SyncInternalScrollBarParents();
        UpdateScrollBars();
    }

    private void DetachInternalScrollBar(ScrollBar scrollBar, EventHandler<RoutedSimpleEventArgs> valueChangedHandler)
    {
        scrollBar.ValueChanged -= valueChangedHandler;
        scrollBar.ThumbDragCompleted -= OnScrollBarThumbDragCompleted;
        scrollBar.SetVisualParent(null);
        scrollBar.SetLogicalParent(null);
        if (scrollBar.IsLoaded)
        {
            scrollBar.RaiseUnloaded();
        }
    }

}
