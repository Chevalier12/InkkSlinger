using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal sealed class DataGridColumnHeadersPresenter
{
    private readonly List<DataGridColumnHeader> _headers = new();

    public IReadOnlyList<DataGridColumnHeader> Headers => _headers;

    public void SyncHeaders(
        DataGrid owner,
        IReadOnlyList<DataGridColumnState> displayColumns,
        EventHandler<RoutedSimpleEventArgs> clickHandler)
    {
        if (_headers.Count == displayColumns.Count)
        {
            for (var i = 0; i < displayColumns.Count; i++)
            {
                _headers[i].Owner = owner;
                _headers[i].BindState(displayColumns[i]);
            }

            return;
        }

        foreach (var header in _headers)
        {
            header.Click -= clickHandler;
            header.SetVisualParent(null);
            header.SetLogicalParent(null);
        }

        _headers.Clear();

        for (var i = 0; i < displayColumns.Count; i++)
        {
            var header = new DataGridColumnHeader
            {
                Owner = owner
            };
            header.BindState(displayColumns[i]);
            header.Click += clickHandler;
            header.SetVisualParent(owner);
            header.SetLogicalParent(owner);
            _headers.Add(header);
        }
    }

    public void MeasureHeaders(IReadOnlyList<DataGridColumnState> displayColumns, float headerHeight)
    {
        for (var i = 0; i < _headers.Count; i++)
        {
            var width = i < displayColumns.Count ? displayColumns[i].Width : 0f;
            _headers[i].Measure(new Vector2(width, headerHeight));
        }
    }

    public void ArrangeHeaders(
        DataGrid owner,
        float x,
        float y,
        float rowHeaderWidth,
        float headerHeight,
        int frozenColumnCount,
        float horizontalOffset,
        bool isVisible)
    {
        var runningFrozenX = x + rowHeaderWidth;
        var runningScrollableX = x + rowHeaderWidth - horizontalOffset;
        var displayColumns = owner.DisplayColumnsForTesting;

        for (var i = 0; i < _headers.Count; i++)
        {
            var width = i < displayColumns.Count ? displayColumns[i].Width : 0f;
            var isFrozen = i < frozenColumnCount;
            var headerX = isFrozen ? runningFrozenX : runningScrollableX;
            _headers[i].IsVisible = isVisible;
            _headers[i].Arrange(new LayoutRect(headerX, y, width, headerHeight));
            if (isFrozen)
            {
                runningFrozenX += width;
            }

            runningScrollableX += width;
        }
    }
}
