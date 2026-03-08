using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal sealed class DataGridRowsPresenter
{
    public DataGridRowsPresenter(DataGrid owner)
    {
        RowsHost = new VirtualizingStackPanel
        {
            Orientation = Orientation.Vertical,
            IsVirtualizing = true,
            CacheLength = 1f,
            CacheLengthUnit = VirtualizationCacheLengthUnit.Page
        };
        owner.AttachItemsHost(RowsHost);

        ScrollViewer = new ScrollViewer
        {
            Content = RowsHost,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            LineScrollAmount = 24f,
            BorderThickness = 0f,
            Background = Color.Transparent
        };
        ScrollViewer.SetVisualParent(owner);
        ScrollViewer.SetLogicalParent(owner);
    }

    public ScrollViewer ScrollViewer { get; }

    public VirtualizingStackPanel RowsHost { get; }

    public void SyncRows(
        DataGrid owner,
        IReadOnlyList<DataGridRowState> rows,
        IReadOnlyList<DataGridColumnState> displayColumns,
        int startIndex,
        bool invalidateMeasure = true)
    {
        var itemContainers = owner.GetItemContainersForPresenter();
        if (rows.Count == 0 || itemContainers.Count == 0)
        {
            RowsHost.IsVirtualizing = owner.EnableRowVirtualization;
            return;
        }

        var begin = Math.Clamp(startIndex, 0, itemContainers.Count - 1);
        for (var i = begin; i < itemContainers.Count; i++)
        {
            if (itemContainers[i] is not DataGridRow row)
            {
                continue;
            }

            row.Configure(
                owner,
                rows[i],
                displayColumns,
                owner.RowHeadersVisibleForLayout,
                owner.HorizontalGridLinesVisibleForLayout,
                owner.VerticalGridLinesVisibleForLayout,
                owner.HorizontalGridLinesBrush,
                owner.VerticalGridLinesBrush);
            row.RowBackground = (i % 2 == 1) ? owner.AlternatingRowBackground : owner.RowBackground;
            row.UpdateSelectionState(
                owner.SelectionUnit,
                owner.SelectedRowIndex,
                owner.SelectedColumnIndex,
                owner.CurrentRowIndexForTesting,
                owner.CurrentColumnIndexForTesting,
                rows[i].AreDetailsVisible);
        }

        RowsHost.IsVirtualizing = owner.EnableRowVirtualization;
        if (invalidateMeasure)
        {
            RowsHost.InvalidateMeasure();
        }
        else
        {
            RowsHost.InvalidateArrange();
        }
    }

    public IReadOnlyList<DataGridRow> GetRows(IReadOnlyList<UIElement> itemContainers)
    {
        var rows = new List<DataGridRow>();
        for (var i = 0; i < itemContainers.Count; i++)
        {
            if (itemContainers[i] is DataGridRow row)
            {
                rows.Add(row);
            }
        }

        return rows;
    }
}
