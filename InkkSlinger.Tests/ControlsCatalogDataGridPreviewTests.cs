using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogDataGridPreviewTests
{
    [Fact]
    public void SelectingDataGridPreview_ShouldPopulateRetainedRows()
    {
        var view = new ControlsCatalogView();
        var showControl = typeof(ControlsCatalogView).GetMethod("ShowControl", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(showControl);
        showControl!.Invoke(view, ["DataGrid"]);

        var uiRoot = new UiRoot(view);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1280, 820));

        var dataGrid = FindFirstVisualChild<DataGrid>(view);
        Assert.NotNull(dataGrid);
        Assert.NotEmpty(dataGrid!.RowsForTesting);
        Assert.True(dataGrid.ScrollViewerForTesting.ViewportHeight > 0f);
        Assert.True(dataGrid.ScrollViewerForTesting.ViewportWidth > 0f);
        var retainedOrder = uiRoot.GetRetainedVisualOrderForTests().ToList();
        Assert.Contains(retainedOrder, static element => element is DataGridRow);
        Assert.Contains(retainedOrder, static element => element is DataGridCell);
        var dataGridIndex = retainedOrder.FindIndex(element => ReferenceEquals(element, dataGrid));
        var firstRowIndex = retainedOrder.FindIndex(static element => element is DataGridRow);
        var firstCellIndex = retainedOrder.FindIndex(static element => element is DataGridCell);
        Assert.True(dataGridIndex >= 0 && dataGridIndex < firstRowIndex);
        Assert.True(firstRowIndex >= 0 && firstRowIndex < firstCellIndex);

        var firstRow = dataGrid.RowsForTesting.First();
        Assert.Equal("1", firstRow.Cells[0].Value?.ToString());
        Assert.Equal("Alpha", firstRow.Cells[1].Value?.ToString());
    }

    [Fact]
    public void SwitchingPreviewToDataGrid_ShouldDirtyRowRegion()
    {
        var view = new ControlsCatalogView();
        var showControl = typeof(ControlsCatalogView).GetMethod("ShowControl", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(showControl);

        var uiRoot = new UiRoot(view);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1280, 820));
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        showControl!.Invoke(view, ["DataGrid"]);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1280, 820));

        var dataGrid = FindFirstVisualChild<DataGrid>(view);
        Assert.NotNull(dataGrid);
        var rows = dataGrid!.RowsForTesting;
        Assert.NotEmpty(rows);
        var rowBounds = rows[0].LayoutSlot;
        var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();

        Assert.True(
            uiRoot.IsFullDirtyForTests() ||
            dirtyRegions.Any(region => Intersects(region, rowBounds)));
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root)
        where TElement : UIElement
    {
        if (root is TElement match)
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild<TElement>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool Intersects(LayoutRect left, LayoutRect right)
    {
        return left.X < right.X + right.Width &&
               left.X + left.Width > right.X &&
               left.Y < right.Y + right.Height &&
               left.Y + left.Height > right.Y;
    }
}
