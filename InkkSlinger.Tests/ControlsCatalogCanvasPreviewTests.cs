using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogCanvasPreviewTests
{
    [Fact]
    public void SelectingCanvasPreview_ShouldLoadWorkbenchWithMixedAnchorsAndTelemetry()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var catalog = new ControlsCatalogView();
            catalog.ShowControl("Canvas");

            var uiRoot = new UiRoot(catalog);
            RunLayoutFrames(uiRoot, 1280, 820, 3);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var canvasView = Assert.IsType<CanvasView>(previewHost.Content);
            var workbench = Assert.IsType<Canvas>(canvasView.FindName("CanvasWorkbench"));
            var focusCard = Assert.IsType<Border>(canvasView.FindName("CanvasSceneRootCard"));
            var legendCard = Assert.IsType<Border>(canvasView.FindName("CanvasLegendCard"));
            var cornerChip = Assert.IsType<Border>(canvasView.FindName("CanvasCornerChip"));
            var inspector = Assert.IsType<Border>(canvasView.FindName("CanvasAnchoredInspector"));
            var anchorModeText = Assert.IsType<TextBlock>(canvasView.FindName("AnchorModeValueText"));
            var mixedAnchorText = Assert.IsType<TextBlock>(canvasView.FindName("MixedAnchorValueText"));
            var guideText = Assert.IsType<TextBlock>(canvasView.FindName("GuideValueText"));

            Assert.Equal(10, workbench.Children.Count);
            Assert.Equal(18f, Canvas.GetLeft(legendCard), 0.01f);
            Assert.Equal(18f, Canvas.GetBottom(legendCard), 0.01f);
            Assert.Equal(24f, Canvas.GetRight(inspector), 0.01f);
            Assert.Equal(22f, Canvas.GetBottom(inspector), 0.01f);
            Assert.Equal(24f, Canvas.GetRight(cornerChip), 0.01f);
            Assert.Equal(18f, Canvas.GetTop(cornerChip), 0.01f);
            Assert.True(focusCard.LayoutSlot.Width >= 220f, $"Expected the focus card to render at its configured size, got {focusCard.LayoutSlot}.");
            Assert.Contains("Left=52", anchorModeText.Text, StringComparison.Ordinal);
            Assert.Contains("Left+Bottom", mixedAnchorText.Text, StringComparison.Ordinal);
            Assert.Contains("Guides: visible", guideText.Text, StringComparison.Ordinal);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CanvasPreview_TogglingAnchorModeAndLayering_ShouldReanchorFocusCardAndUpdateTelemetry()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var catalog = new ControlsCatalogView();
            catalog.ShowControl("Canvas");

            var uiRoot = new UiRoot(catalog);
            RunLayoutFrames(uiRoot, 1280, 820, 3);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var canvasView = Assert.IsType<CanvasView>(previewHost.Content);
            var workbench = Assert.IsType<Canvas>(canvasView.FindName("CanvasWorkbench"));
            var focusCard = Assert.IsType<Border>(canvasView.FindName("CanvasSceneRootCard"));
            var badge = Assert.IsType<Border>(canvasView.FindName("CanvasSceneBadge"));
            var anchorToggle = Assert.IsType<CheckBox>(canvasView.FindName("AnchorFromRightBottomCheckBox"));
            var layerToggle = Assert.IsType<CheckBox>(canvasView.FindName("BringBadgeToFrontCheckBox"));
            var anchorModeText = Assert.IsType<TextBlock>(canvasView.FindName("AnchorModeValueText"));
            var layerText = Assert.IsType<TextBlock>(canvasView.FindName("LayerValueText"));

            anchorToggle.IsChecked = true;
            layerToggle.IsChecked = false;
            RunLayoutFrames(uiRoot, 1280, 820, 3);

            var expectedX = workbench.LayoutSlot.X + workbench.ActualWidth - focusCard.ActualWidth - 52f;
            var expectedY = workbench.LayoutSlot.Y + workbench.ActualHeight - focusCard.ActualHeight - 68f;

            Assert.Equal(expectedX, focusCard.LayoutSlot.X, 1.5f);
            Assert.Equal(expectedY, focusCard.LayoutSlot.Y, 1.5f);
            Assert.Equal(1, Panel.GetZIndex(badge));
            Assert.True(Panel.GetZIndex(badge) < Panel.GetZIndex(focusCard));
            Assert.Contains("Right=52", anchorModeText.Text, StringComparison.Ordinal);
            Assert.Contains("behind focus card", layerText.Text, StringComparison.Ordinal);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CanvasPreview_DraggingFocusHandle_ShouldMoveFocusCardAndUpdateTelemetry()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var catalog = new ControlsCatalogView();
            catalog.ShowControl("Canvas");

            var uiRoot = new UiRoot(catalog);
            RunLayoutFrames(uiRoot, 1280, 820, 3);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var canvasView = Assert.IsType<CanvasView>(previewHost.Content);
            var focusCard = Assert.IsType<Border>(canvasView.FindName("CanvasSceneRootCard"));
            var dragThumb = Assert.IsType<Thumb>(canvasView.FindName("CanvasSceneDragThumb"));
            var anchorModeText = Assert.IsType<TextBlock>(canvasView.FindName("AnchorModeValueText"));

            var dragStart = GetCenter(dragThumb.LayoutSlot);
            var dragEnd = dragStart + new Vector2(36f, 24f);

            Assert.True(dragThumb.HandlePointerDownFromInput(dragStart));
            Assert.True(dragThumb.HandlePointerMoveFromInput(dragEnd));
            Assert.True(dragThumb.HandlePointerUpFromInput());

            RunLayoutFrames(uiRoot, 1280, 820, 3);

            Assert.Equal(88f, Canvas.GetLeft(focusCard), 0.5f);
            Assert.Equal(92f, Canvas.GetTop(focusCard), 0.5f);
            Assert.Equal(88f, focusCard.LayoutSlot.X - Assert.IsType<Canvas>(canvasView.FindName("CanvasWorkbench")).LayoutSlot.X, 1.5f);
            Assert.Equal(92f, focusCard.LayoutSlot.Y - Assert.IsType<Canvas>(canvasView.FindName("CanvasWorkbench")).LayoutSlot.Y, 1.5f);
            Assert.Contains("Left=88", anchorModeText.Text, StringComparison.Ordinal);
            Assert.Contains("Top=92", anchorModeText.Text, StringComparison.Ordinal);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CanvasPreview_DraggingFocusHandle_WithStableTelemetrySize_DoesNotRemeasureInfoRail()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var catalog = new ControlsCatalogView();
            catalog.ShowControl("Canvas");

            var uiRoot = new UiRoot(catalog);
            RunLayoutFrames(uiRoot, 1280, 820, 3);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var canvasView = Assert.IsType<CanvasView>(previewHost.Content);
            var focusCard = Assert.IsType<Border>(canvasView.FindName("CanvasSceneRootCard"));
            var dragThumb = Assert.IsType<Thumb>(canvasView.FindName("CanvasSceneDragThumb"));
            var infoViewer = Assert.IsType<ScrollViewer>(canvasView.FindName("CanvasViewInfoScrollViewer"));
            var positionText = Assert.IsType<TextBlock>(canvasView.FindName("PositionValueText"));

            var beforeInfoViewerMeasureWork = infoViewer.MeasureWorkCount;
            var beforeInfoViewerMeasureInvalidations = infoViewer.MeasureInvalidationCount;
            var beforePositionMeasureWork = positionText.MeasureWorkCount;
            var beforePositionMeasureInvalidations = positionText.MeasureInvalidationCount;
            var beforePositionRenderInvalidations = positionText.RenderInvalidationCount;
            var beforePositionText = positionText.Text;
            var beforeDesired = positionText.DesiredSize;

            var dragStart = GetCenter(dragThumb.LayoutSlot);
            var dragEnd = dragStart + new Vector2(36f, 24f);

            Assert.True(dragThumb.HandlePointerDownFromInput(dragStart));
            Assert.True(dragThumb.HandlePointerMoveFromInput(dragEnd));
            Assert.True(dragThumb.HandlePointerUpFromInput());

            RunLayoutFrames(uiRoot, 1280, 820, 3);

            Assert.NotEqual(beforePositionText, positionText.Text);
            Assert.True(positionText.RenderInvalidationCount > beforePositionRenderInvalidations);
            Assert.Equal(beforePositionMeasureInvalidations, positionText.MeasureInvalidationCount);
            Assert.Equal(beforeInfoViewerMeasureInvalidations, infoViewer.MeasureInvalidationCount);
            Assert.Equal(beforePositionMeasureWork, positionText.MeasureWorkCount);
            Assert.Equal(beforeInfoViewerMeasureWork, infoViewer.MeasureWorkCount);
            Assert.Equal(beforeDesired.X, positionText.DesiredSize.X, 0.01f);
            Assert.Equal(beforeDesired.Y, positionText.DesiredSize.Y, 0.01f);
            Assert.Equal(88f, Canvas.GetLeft(focusCard), 0.5f);
            Assert.Equal(92f, Canvas.GetTop(focusCard), 0.5f);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CanvasPreview_DraggingFocusHandleInRightBottomMode_ShouldPreserveFarEdgeInsets()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var catalog = new ControlsCatalogView();
            catalog.ShowControl("Canvas");

            var uiRoot = new UiRoot(catalog);
            RunLayoutFrames(uiRoot, 1280, 820, 3);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var canvasView = Assert.IsType<CanvasView>(previewHost.Content);
            var workbench = Assert.IsType<Canvas>(canvasView.FindName("CanvasWorkbench"));
            var focusCard = Assert.IsType<Border>(canvasView.FindName("CanvasSceneRootCard"));
            var anchorToggle = Assert.IsType<CheckBox>(canvasView.FindName("AnchorFromRightBottomCheckBox"));
            var anchorModeText = Assert.IsType<TextBlock>(canvasView.FindName("AnchorModeValueText"));

            anchorToggle.IsChecked = true;
            RunLayoutFrames(uiRoot, 1280, 820, 6);

            var dragThumb = Assert.IsType<Thumb>(canvasView.FindName("CanvasSceneDragThumb"));
            var dragStart = GetCenter(dragThumb.LayoutSlot);
            var dragEnd = dragStart + new Vector2(36f, 24f);

            InvokeCanvasViewMethod(
                canvasView,
                "HandleFocusCardDragDelta",
                dragThumb,
                new DragDeltaEventArgs(Thumb.DragDeltaEvent, dragEnd.X - dragStart.X, dragEnd.Y - dragStart.Y));

            RunLayoutFrames(uiRoot, 1280, 820, 3);

            var expectedRight = 16f;
            var expectedBottom = 44f;
            var expectedX = workbench.LayoutSlot.X + workbench.ActualWidth - focusCard.ActualWidth - expectedRight;
            var expectedY = workbench.LayoutSlot.Y + workbench.ActualHeight - focusCard.ActualHeight - expectedBottom;

            Assert.Equal(expectedRight, Canvas.GetRight(focusCard), 0.5f);
            Assert.Equal(expectedBottom, Canvas.GetBottom(focusCard), 0.5f);
            Assert.Equal(expectedX, focusCard.LayoutSlot.X, 1.5f);
            Assert.Equal(expectedY, focusCard.LayoutSlot.Y, 1.5f);
            Assert.Contains("Right=16", anchorModeText.Text, StringComparison.Ordinal);
            Assert.Contains("Bottom=44", anchorModeText.Text, StringComparison.Ordinal);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CanvasPreview_NarrowWidth_ShouldStackInfoRailBelowWorkbench()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var catalog = new ControlsCatalogView();
            catalog.ShowControl("Canvas");

            var uiRoot = new UiRoot(catalog);
            RunLayoutFrames(uiRoot, 860, 820, 3);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var canvasView = Assert.IsType<CanvasView>(previewHost.Content);
            var bodyBorder = Assert.IsType<Border>(canvasView.FindName("CanvasViewBodyBorder"));
            var infoViewer = Assert.IsType<ScrollViewer>(canvasView.FindName("CanvasViewInfoScrollViewer"));
            var workbench = Assert.IsType<Canvas>(canvasView.FindName("CanvasWorkbench"));
            var stageMetricsText = Assert.IsType<TextBlock>(canvasView.FindName("StageMetricsText"));

            Assert.Equal(0, Grid.GetColumn(infoViewer));
            Assert.Equal(1, Grid.GetRow(infoViewer));
            Assert.True(
                infoViewer.LayoutSlot.Y >= bodyBorder.LayoutSlot.Y + bodyBorder.LayoutSlot.Height - 0.5f,
                $"Expected the info rail to stack below the workbench body, got body={bodyBorder.LayoutSlot} info={infoViewer.LayoutSlot}.");
            Assert.True(infoViewer.LayoutSlot.Height <= 250.5f, $"Expected the stacked info rail height cap, got {infoViewer.LayoutSlot}.");
            Assert.True(workbench.Children.Count > 0, "Expected the wrapped Canvas to keep its scene content.");
            Assert.Contains("stacked info rail", stageMetricsText.Text, StringComparison.OrdinalIgnoreCase);
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

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static void InvokeCanvasViewMethod(CanvasView view, string methodName, params object?[] arguments)
    {
        var method = typeof(CanvasView).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(view, arguments);
    }
}