using System;
using System.Collections.Generic;
using System.IO;
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
        Assert.Equal("101", firstRow.Cells[0].Value?.ToString());
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

        var header = Assert.Single(dataGrid.ColumnHeadersForTesting, static item => item.GetContentText() == "Ticket");
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

    [Fact]
    public void CatalogPreviewDataGrid_NarrowWidth_StacksInfoRailAndKeepsDemoGridInsideBody()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new ControlsCatalogView();
            view.ShowControl("DataGrid");

            var uiRoot = new UiRoot(view);
            for (var frame = 1; frame <= 3; frame++)
            {
                uiRoot.Update(
                    new GameTime(TimeSpan.FromMilliseconds(frame * 16), TimeSpan.FromMilliseconds(16)),
                    new Viewport(0, 0, 856, 820));
            }

            var previewHost = Assert.IsType<ContentControl>(view.FindName("PreviewHost"));
            var dataGridView = Assert.IsType<DataGridView>(previewHost.Content);
            var bodyBorder = Assert.IsType<Border>(dataGridView.FindName("DataGridViewBodyBorder"));
            var infoViewer = Assert.IsType<ScrollViewer>(dataGridView.FindName("DataGridViewInfoScrollViewer"));
            var demoGrid = Assert.IsType<DataGrid>(dataGridView.FindName("DemoGrid"));
            var horizontalBar = GetPrivateScrollBar(demoGrid.ScrollViewerForTesting, "_horizontalBar");

            var bodyRight = bodyBorder.LayoutSlot.X + bodyBorder.LayoutSlot.Width + 0.5f;
            var gridRight = demoGrid.LayoutSlot.X + demoGrid.LayoutSlot.Width;

            Assert.Equal(0, Grid.GetColumn(infoViewer));
            Assert.Equal(1, Grid.GetRow(infoViewer));
            Assert.True(
                infoViewer.LayoutSlot.Y >= bodyBorder.LayoutSlot.Y + bodyBorder.LayoutSlot.Height - 0.5f,
                $"Expected the info rail to stack below the body at narrow width, got body={bodyBorder.LayoutSlot} info={infoViewer.LayoutSlot}.");
            Assert.True(
                bodyBorder.LayoutSlot.Height >= 390f,
                $"Expected the body border to retain enough height for a usable demo grid, got body={bodyBorder.LayoutSlot}.");
            Assert.True(
                infoViewer.LayoutSlot.Height <= 180.5f,
                $"Expected the stacked info rail to be height-capped, got info={infoViewer.LayoutSlot}.");
            Assert.True(
                gridRight <= bodyRight,
                $"Expected the demo grid to stay inside the body border at narrow width, got body={bodyBorder.LayoutSlot} grid={demoGrid.LayoutSlot}.");
            Assert.True(
                demoGrid.ScrollViewerForTesting.ExtentWidth > demoGrid.ScrollViewerForTesting.ViewportWidth,
                $"Expected the demo grid to require horizontal scrolling at narrow width. Extent={demoGrid.ScrollViewerForTesting.ExtentWidth}, Viewport={demoGrid.ScrollViewerForTesting.ViewportWidth}.");
            Assert.True(
                horizontalBar.IsVisible && horizontalBar.LayoutSlot.Height > 0f,
                $"Expected the demo grid horizontal scrollbar to be visible at narrow width, got bar={horizontalBar.LayoutSlot} visible={horizontalBar.IsVisible}.");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CatalogPreviewDataGrid_WideWidth_WrapsInfoRailCopyAndKeepsFooterTextInsideBorder()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new ControlsCatalogView();
            view.ShowControl("DataGrid");

            var uiRoot = new UiRoot(view);
            for (var frame = 1; frame <= 3; frame++)
            {
                uiRoot.Update(
                    new GameTime(TimeSpan.FromMilliseconds(frame * 16), TimeSpan.FromMilliseconds(16)),
                    new Viewport(0, 0, 1280, 820));
            }

            var previewHost = Assert.IsType<ContentControl>(view.FindName("PreviewHost"));
            var dataGridView = Assert.IsType<DataGridView>(previewHost.Content);
            var demoGrid = Assert.IsType<DataGrid>(dataGridView.FindName("DemoGrid"));
            var footerBorder = FindFirstVisualChild<Border>(
                dataGridView,
                static border => border.GetVisualChildren().OfType<TextBlock>().Any(static block =>
                    block.Text.StartsWith("The hosted grid is stretched", StringComparison.Ordinal)));
            var wrappedInfoText = FindFirstVisualChild<TextBlock>(
                dataGridView,
                static block => block.Text.StartsWith("The blue lane on the far left", StringComparison.Ordinal));
            var bulletText = FindFirstVisualChild<TextBlock>(
                dataGridView,
                static block => block.Text.StartsWith("- Drag the Name or Status", StringComparison.Ordinal));
            var footerText = FindFirstVisualChild<TextBlock>(
                dataGridView,
                static block => block.Text.StartsWith("The hosted grid is stretched", StringComparison.Ordinal));

            Assert.NotNull(footerBorder);
            Assert.NotNull(wrappedInfoText);
            Assert.NotNull(bulletText);
            Assert.NotNull(footerText);

            Assert.True(
                wrappedInfoText!.ActualHeight > 23f,
                $"Expected info rail advisory text to wrap, got text={wrappedInfoText.LayoutSlot}.");
            Assert.True(
                bulletText!.ActualHeight > 23f,
                $"Expected info rail bullet text to wrap, got text={bulletText.LayoutSlot}.");

            var footerBottom = footerBorder!.LayoutSlot.Y + footerBorder.LayoutSlot.Height + 0.5f;
            var footerTop = footerBorder.LayoutSlot.Y;
            var footerTextBottom = footerText!.LayoutSlot.Y + footerText.LayoutSlot.Height;
            var gridBottom = demoGrid.LayoutSlot.Y + demoGrid.LayoutSlot.Height;
            Assert.True(
                footerTop >= gridBottom + 12f,
                $"Expected footer border to stay visually separated from the grid, got grid={demoGrid.LayoutSlot} footer={footerBorder.LayoutSlot}.");
            Assert.True(
                footerTextBottom <= footerBottom - 4f,
                $"Expected footer text to stay inside its border, got border={footerBorder.LayoutSlot} text={footerText.LayoutSlot}.");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CatalogPreviewDataGrid_WideWidth_FooterText_ArrangedHeightMatchesMeasuredHeight()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new ControlsCatalogView();
            view.ShowControl("DataGrid");

            var uiRoot = new UiRoot(view);
            for (var frame = 1; frame <= 3; frame++)
            {
                uiRoot.Update(
                    new GameTime(TimeSpan.FromMilliseconds(frame * 16), TimeSpan.FromMilliseconds(16)),
                    new Viewport(0, 0, 1280, 820));
            }

            var previewHost = Assert.IsType<ContentControl>(view.FindName("PreviewHost"));
            var dataGridView = Assert.IsType<DataGridView>(previewHost.Content);
            var footerBorder = FindFirstVisualChild<Border>(
                dataGridView,
                static border => border.GetVisualChildren().OfType<TextBlock>().Any(static block =>
                    block.Text.StartsWith("The hosted grid is stretched", StringComparison.Ordinal)));
            var footerText = FindFirstVisualChild<TextBlock>(
                dataGridView,
                static block => block.Text.StartsWith("The hosted grid is stretched", StringComparison.Ordinal));

            Assert.NotNull(footerBorder);
            Assert.NotNull(footerText);

            Assert.True(
                footerText!.ActualHeight >= footerText.DesiredSize.Y - 0.5f,
                $"Expected footer text arranged height to keep up with its measured height, got desired={footerText.DesiredSize} actual={footerText.ActualHeight:0.###} slot={footerText.LayoutSlot}.");
            Assert.True(
                footerBorder!.ActualHeight >= footerText.ActualHeight + 22f - 0.5f,
                $"Expected footer border height to include text plus padding and border, got border={footerBorder.LayoutSlot} text={footerText.LayoutSlot} desired={footerText.DesiredSize}.");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
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
        return FindFirstVisualChild<TElement>(root, static _ => true);
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root, Func<TElement, bool> predicate)
        where TElement : UIElement
    {
        if (root is TElement match && predicate(match))
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild(child, predicate);
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

    private static Dictionary<object, object> CaptureApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
    }

    private static void RestoreApplicationResources(Dictionary<object, object> snapshot)
    {
        TestApplicationResources.Restore(snapshot);
    }

    private static void LoadRootAppResources()
    {
        var appPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "App.xml"));
        Assert.True(File.Exists(appPath), $"Expected App.xml to exist at '{appPath}'.");
        XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);
    }

}
