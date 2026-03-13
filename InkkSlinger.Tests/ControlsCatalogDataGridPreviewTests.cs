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
        view.ShowControl("DataGrid");

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

        var uiRoot = new UiRoot(view);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1280, 820));
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        view.ShowControl("DataGrid");
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

    [Fact]
    public void CatalogPreviewTypographyApplication_ShouldUseInheritedTypographyForDataGridBranch()
    {
        var view = new ControlsCatalogView
        {
            FontFamily = "Segoe UI",
            FontSize = 17f,
            FontWeight = "SemiBold",
            FontStyle = "Italic"
        };
        view.ShowControl("DataGrid");

        var uiRoot = new UiRoot(view);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1280, 820));

        var dataGrid = FindFirstVisualChild<DataGrid>(view);
        Assert.NotNull(dataGrid);
        AssertTypography(dataGrid!, view);
        Assert.Equal(DependencyPropertyValueSource.Inherited, dataGrid.GetValueSource(FrameworkElement.FontFamilyProperty));
        Assert.Equal(DependencyPropertyValueSource.Inherited, dataGrid.GetValueSource(FrameworkElement.FontSizeProperty));
        Assert.Equal(DependencyPropertyValueSource.Inherited, dataGrid.GetValueSource(FrameworkElement.FontWeightProperty));
        Assert.Equal(DependencyPropertyValueSource.Inherited, dataGrid.GetValueSource(FrameworkElement.FontStyleProperty));

        var scrollViewer = dataGrid.ScrollViewerForTesting;
        AssertTypography(scrollViewer, view);
        Assert.Equal(DependencyPropertyValueSource.Inherited, scrollViewer.GetValueSource(FrameworkElement.FontFamilyProperty));
        var verticalBar = GetPrivateScrollBar(scrollViewer, "_verticalBar");
        var horizontalBar = GetPrivateScrollBar(scrollViewer, "_horizontalBar");
        Assert.Equal(DependencyPropertyValueSource.Default, verticalBar.GetValueSource(UIElement.IsVisibleProperty));
        Assert.Equal(DependencyPropertyValueSource.Default, horizontalBar.GetValueSource(UIElement.IsVisibleProperty));

        var header = Assert.Single(dataGrid.ColumnHeadersForTesting, static item => item.Text == "Id");
        AssertTypography(header, view);
        Assert.Equal(DependencyPropertyValueSource.Inherited, header.GetValueSource(FrameworkElement.FontFamilyProperty));

        Assert.NotEmpty(dataGrid.RowsForTesting);
        var firstRow = dataGrid.RowsForTesting[0];
        Assert.Equal(DependencyPropertyValueSource.Default, firstRow.GetValueSource(FrameworkElement.HeightProperty));
        var firstCell = firstRow.Cells[0];
        AssertTypography(firstCell, view);
        Assert.Equal(DependencyPropertyValueSource.Inherited, firstCell.GetValueSource(FrameworkElement.FontFamilyProperty));

        var rowHeader = firstRow.RowHeaderForTesting;
        AssertTypography(rowHeader, view);
        Assert.Equal(DependencyPropertyValueSource.Inherited, rowHeader.GetValueSource(FrameworkElement.FontFamilyProperty));
    }

    private static void AssertTypography(FrameworkElement element, FrameworkElement expected)
    {
        Assert.Equal(expected.FontFamily, element.FontFamily);
        Assert.Equal(expected.FontSize, element.FontSize);
        Assert.Equal(expected.FontWeight, element.FontWeight);
        Assert.Equal(expected.FontStyle, element.FontStyle);
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

    private static ScrollBar GetPrivateScrollBar(ScrollViewer viewer, string fieldName)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = field!.GetValue(viewer);
        Assert.IsType<ScrollBar>(value);
        return (ScrollBar)value!;
    }
}
