using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogGridPreviewTests
{
    [Fact]
    public void SelectingGridPreview_ShouldLoadWorkbenchAndSharedSizeSamples()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new ControlsCatalogView();
            view.ShowControl("Grid");

            var uiRoot = new UiRoot(view);
            RunLayoutFrames(uiRoot, 1280, 820, 3);

            var previewHost = Assert.IsType<ContentControl>(view.FindName("PreviewHost"));
            var gridView = Assert.IsType<GridView>(previewHost.Content);
            var workbenchGrid = Assert.IsType<Grid>(gridView.FindName("LayoutWorkbenchGrid"));
            var scheduleCard = Assert.IsType<Border>(gridView.FindName("ScheduleCard"));
            var statusRailCard = Assert.IsType<Border>(gridView.FindName("StatusRailCard"));
            var inspectorCard = Assert.IsType<Border>(gridView.FindName("InspectorCard"));
            var sharedScopeHost = Assert.IsType<StackPanel>(gridView.FindName("SharedSizeScopeHost"));
            var sharedPrimaryGrid = Assert.IsType<Grid>(gridView.FindName("SharedSizePrimaryGrid"));
            var sharedSecondaryGrid = Assert.IsType<Grid>(gridView.FindName("SharedSizeSecondaryGrid"));
            var layoutModeText = Assert.IsType<TextBlock>(gridView.FindName("LayoutModeValueText"));
            var columnMetricsText = Assert.IsType<TextBlock>(gridView.FindName("ColumnMetricsText"));
            var sharedMetricsText = Assert.IsType<TextBlock>(gridView.FindName("SharedMetricsText"));

            Assert.True(workbenchGrid.ShowGridLines);
            Assert.Equal(4, workbenchGrid.ColumnDefinitions.Count);
            Assert.Equal(4, workbenchGrid.RowDefinitions.Count);
            Assert.Equal(3, Grid.GetColumnSpan(scheduleCard));
            Assert.Equal(2, Grid.GetRowSpan(statusRailCard));
            Assert.Equal(2, Grid.GetColumnSpan(inspectorCard));
            Assert.True(Grid.GetIsSharedSizeScope(sharedScopeHost));
            Assert.True(sharedPrimaryGrid.ColumnDefinitions[0].ActualWidth > 0f);
            Assert.Equal(sharedPrimaryGrid.ColumnDefinitions[0].ActualWidth, sharedSecondaryGrid.ColumnDefinitions[0].ActualWidth, 0.01f);
            Assert.Equal(sharedPrimaryGrid.ColumnDefinitions[2].ActualWidth, sharedSecondaryGrid.ColumnDefinitions[2].ActualWidth, 0.01f);
            Assert.Contains("Wide rail", layoutModeText.Text);
            Assert.Contains("C0=", columnMetricsText.Text);
            Assert.Contains("Label=", sharedMetricsText.Text);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CatalogPreviewGrid_NarrowWidth_StacksInfoRailBelowWorkbench()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new ControlsCatalogView();
            view.ShowControl("Grid");

            var uiRoot = new UiRoot(view);
            RunLayoutFrames(uiRoot, 856, 820, 3);

            var previewHost = Assert.IsType<ContentControl>(view.FindName("PreviewHost"));
            var gridView = Assert.IsType<GridView>(previewHost.Content);
            var bodyBorder = Assert.IsType<Border>(gridView.FindName("GridViewBodyBorder"));
            var infoViewer = Assert.IsType<ScrollViewer>(gridView.FindName("GridViewInfoScrollViewer"));
            var workbenchGrid = Assert.IsType<Grid>(gridView.FindName("LayoutWorkbenchGrid"));
            var layoutModeText = Assert.IsType<TextBlock>(gridView.FindName("LayoutModeValueText"));

            var bodyRight = bodyBorder.LayoutSlot.X + bodyBorder.LayoutSlot.Width + 0.5f;
            var workbenchRight = workbenchGrid.LayoutSlot.X + workbenchGrid.LayoutSlot.Width;

            Assert.Equal(0, Grid.GetColumn(infoViewer));
            Assert.Equal(1, Grid.GetRow(infoViewer));
            Assert.True(
                infoViewer.LayoutSlot.Y >= bodyBorder.LayoutSlot.Y + bodyBorder.LayoutSlot.Height - 0.5f,
                $"Expected the info rail to stack below the body at narrow width, got body={bodyBorder.LayoutSlot} info={infoViewer.LayoutSlot}.");
            Assert.True(
                bodyBorder.LayoutSlot.Height >= 340f,
                $"Expected the body border to keep enough height for the workbench, got body={bodyBorder.LayoutSlot}.");
            Assert.True(
                infoViewer.LayoutSlot.Height <= 220.5f,
                $"Expected the stacked info rail to be height-capped, got info={infoViewer.LayoutSlot}.");
            Assert.True(
                workbenchRight <= bodyRight,
                $"Expected the workbench grid to stay inside the body border, got body={bodyBorder.LayoutSlot} workbench={workbenchGrid.LayoutSlot}.");
            Assert.Contains("Stacked info rail", layoutModeText.Text);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CatalogPreviewGrid_WideWidth_KeepsSideRailBesideWorkbenchAndUpdatesTelemetry()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new ControlsCatalogView();
            view.ShowControl("Grid");

            var uiRoot = new UiRoot(view);
            RunLayoutFrames(uiRoot, 1920, 991, 3);

            var previewHost = Assert.IsType<ContentControl>(view.FindName("PreviewHost"));
            var gridView = Assert.IsType<GridView>(previewHost.Content);
            var infoViewer = Assert.IsType<ScrollViewer>(gridView.FindName("GridViewInfoScrollViewer"));
            var gridLinesText = Assert.IsType<TextBlock>(gridView.FindName("GridLinesValueText"));
            var rowMetricsText = Assert.IsType<TextBlock>(gridView.FindName("RowMetricsText"));
            var sharedMetricsText = Assert.IsType<TextBlock>(gridView.FindName("SharedMetricsText"));
            var canvasCard = Assert.IsType<Border>(gridView.FindName("CanvasCard"));
            var inspectorCard = Assert.IsType<Border>(gridView.FindName("InspectorCard"));
            var scheduleCard = Assert.IsType<Border>(gridView.FindName("ScheduleCard"));
            var statusRailCard = Assert.IsType<Border>(gridView.FindName("StatusRailCard"));

            Assert.Equal(1, Grid.GetColumn(infoViewer));
            Assert.Equal(0, Grid.GetRow(infoViewer));
            Assert.Contains("Grid lines: On", gridLinesText.Text);
            Assert.Contains("R2=", rowMetricsText.Text);
            Assert.Contains("Meta=", sharedMetricsText.Text);
            Assert.True(scheduleCard.LayoutSlot.Height >= 80f, $"Expected the header span card to keep a visible row after wrapping changes, got {scheduleCard.LayoutSlot}.");
            Assert.True(statusRailCard.LayoutSlot.Height >= 250f, $"Expected the spanning status rail to retain substantial two-row height, got {statusRailCard.LayoutSlot}.");
            Assert.True(canvasCard.LayoutSlot.Height > 100f, $"Expected the nested canvas lane to retain meaningful height, got {canvasCard.LayoutSlot}.");
            Assert.True(inspectorCard.LayoutSlot.Height > 100f, $"Expected the inspector lane to retain meaningful height, got {inspectorCard.LayoutSlot}.");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CatalogPreviewGrid_MediumWidth_KeepsWorkbenchStarRowVisible()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new ControlsCatalogView();
            view.ShowControl("Grid");

            var uiRoot = new UiRoot(view);
            RunLayoutFrames(uiRoot, 1280, 820, 3);

            var previewHost = Assert.IsType<ContentControl>(view.FindName("PreviewHost"));
            var gridView = Assert.IsType<GridView>(previewHost.Content);
            var infoViewer = Assert.IsType<ScrollViewer>(gridView.FindName("GridViewInfoScrollViewer"));
            var workbenchViewer = Assert.IsType<ScrollViewer>(gridView.FindName("LayoutWorkbenchScrollViewer"));
            var workbenchGrid = Assert.IsType<Grid>(gridView.FindName("LayoutWorkbenchGrid"));
            var rowMetricsText = Assert.IsType<TextBlock>(gridView.FindName("RowMetricsText"));
            var canvasCard = Assert.IsType<Border>(gridView.FindName("CanvasCard"));
            var inspectorCard = Assert.IsType<Border>(gridView.FindName("InspectorCard"));

            Assert.Equal(1, Grid.GetColumn(infoViewer));
            Assert.Equal(0, Grid.GetRow(infoViewer));
            Assert.True(workbenchViewer.LayoutSlot.Height >= 280f, $"Expected the workbench viewport to retain usable height, got {workbenchViewer.LayoutSlot}.");
            Assert.True(workbenchGrid.RowDefinitions[2].ActualHeight > 20f, $"Expected the star row to remain visible at medium width, got R2={workbenchGrid.RowDefinitions[2].ActualHeight:0.##}.");
            Assert.DoesNotContain("R2=0", rowMetricsText.Text, StringComparison.Ordinal);
            Assert.True(canvasCard.LayoutSlot.Height > 20f, $"Expected the nested canvas lane to stay measurable, got {canvasCard.LayoutSlot}.");
            Assert.True(inspectorCard.LayoutSlot.Height > 80f, $"Expected the inspector lane to stay measurable, got {inspectorCard.LayoutSlot}.");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static void RunLayoutFrames(UiRoot uiRoot, int width, int height, int frames)
    {
        for (var frame = 1; frame <= frames; frame++)
        {
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(frame * 16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, width, height));
        }
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