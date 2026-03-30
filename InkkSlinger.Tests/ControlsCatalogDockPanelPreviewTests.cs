using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogDockPanelPreviewTests
{
    [Fact]
    public void CatalogPreviewDockPanel_DefaultFillMode_KeepsCenterSeparatedFromRightRailAtMediumWidth()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var catalog = new ControlsCatalogView();
            catalog.ShowControl("DockPanel");

            var uiRoot = new UiRoot(catalog);
            RunLayoutFrames(uiRoot, 1280, 820, 3);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var dockPanelView = Assert.IsType<DockPanelView>(previewHost.Content);
            var stageScrollViewer = Assert.IsType<ScrollViewer>(dockPanelView.FindName("DockWorkbenchScrollViewer"));
            var leftRailCard = Assert.IsType<Border>(dockPanelView.FindName("LeftRailCard"));
            var rightRailCard = Assert.IsType<Border>(dockPanelView.FindName("RightRailCard"));
            var centerCanvasCard = Assert.IsType<Border>(dockPanelView.FindName("CenterCanvasCard"));
            var fillBehaviorText = Assert.IsType<TextBlock>(dockPanelView.FindName("FillBehaviorValueText"));

            var centerRight = centerCanvasCard.LayoutSlot.X + centerCanvasCard.LayoutSlot.Width;
            var rightLeft = rightRailCard.LayoutSlot.X;

            Assert.Contains("owns the remaining rectangle", fillBehaviorText.Text, StringComparison.Ordinal);
            Assert.True(stageScrollViewer.ViewportWidth > 0f, $"Expected a positive stage viewport width, got {stageScrollViewer.ViewportWidth:0.###}.");
            Assert.True(
                centerRight <= rightLeft + 0.5f,
                $"Expected the center workspace to stop before the right rail. center={centerCanvasCard.LayoutSlot}, right={rightRailCard.LayoutSlot}, left={leftRailCard.LayoutSlot}, viewportWidth={stageScrollViewer.ViewportWidth:0.###}.");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void CatalogPreviewDockPanel_DisablingLastChildFill_UsesScrollableStageAndCompactLaneModeWithoutOverflow()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var catalog = new ControlsCatalogView();
            catalog.ShowControl("DockPanel");

            var uiRoot = new UiRoot(catalog);
            RunLayoutFrames(uiRoot, 1280, 820, 3);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var dockPanelView = Assert.IsType<DockPanelView>(previewHost.Content);
            var lastChildFillCheckBox = Assert.IsType<CheckBox>(dockPanelView.FindName("LastChildFillCheckBox"));

            lastChildFillCheckBox.IsChecked = false;
            RunLayoutFrames(uiRoot, 1280, 820, 3);

            var stageBorder = Assert.IsType<Border>(dockPanelView.FindName("DockWorkbenchStageBorder"));
            var stageScrollViewer = Assert.IsType<ScrollViewer>(dockPanelView.FindName("DockWorkbenchScrollViewer"));
            var leftRailCard = Assert.IsType<Border>(dockPanelView.FindName("LeftRailCard"));
            var leftRailInboxCard = Assert.IsType<Border>(dockPanelView.FindName("LeftRailInboxCard"));
            var leftRailPinnedViewsCard = Assert.IsType<Border>(dockPanelView.FindName("LeftRailPinnedViewsCard"));
            var leftRailFiltersCard = Assert.IsType<Border>(dockPanelView.FindName("LeftRailFiltersCard"));
            var rightRailCard = Assert.IsType<Border>(dockPanelView.FindName("RightRailCard"));
            var rightRailMetadataCard = Assert.IsType<Border>(dockPanelView.FindName("RightRailMetadataCard"));
            var rightRailDiagnosticsCard = Assert.IsType<Border>(dockPanelView.FindName("RightRailDiagnosticsCard"));
            var centerCanvasCard = Assert.IsType<Border>(dockPanelView.FindName("CenterCanvasCard"));
            var centerModeCard = Assert.IsType<Border>(dockPanelView.FindName("CenterModeCard"));
            var centerBehaviorCard = Assert.IsType<Border>(dockPanelView.FindName("CenterBehaviorCard"));
            var centerTelemetryCard = Assert.IsType<Border>(dockPanelView.FindName("CenterTelemetryCard"));
            var centerFooterBadge = Assert.IsType<Border>(dockPanelView.FindName("CenterFooterBadge"));
            var dockOrderBorder = Assert.IsType<Border>(dockPanelView.FindName("DockOrderBorder"));
            var fillBehaviorText = Assert.IsType<TextBlock>(dockPanelView.FindName("FillBehaviorValueText"));
            var sequenceText = Assert.IsType<TextBlock>(dockPanelView.FindName("SequenceValueText"));

            Assert.False(lastChildFillCheckBox.IsChecked);
            Assert.Equal(Visibility.Collapsed, leftRailPinnedViewsCard.Visibility);
            Assert.Equal(Visibility.Collapsed, leftRailFiltersCard.Visibility);
            Assert.Equal(Visibility.Collapsed, rightRailDiagnosticsCard.Visibility);
            Assert.Equal(Visibility.Collapsed, centerTelemetryCard.Visibility);
            Assert.Equal(Visibility.Collapsed, centerFooterBadge.Visibility);
            Assert.Contains("no longer fills", fillBehaviorText.Text, StringComparison.Ordinal);
            Assert.Contains("dock left", sequenceText.Text, StringComparison.OrdinalIgnoreCase);

            AssertRectInside(stageScrollViewer.LayoutSlot, stageBorder.LayoutSlot, "stage scroll viewer");
            AssertRectInside(leftRailInboxCard.LayoutSlot, leftRailCard.LayoutSlot, "left inbox card");
            AssertRectInside(rightRailMetadataCard.LayoutSlot, rightRailCard.LayoutSlot, "right metadata card");
            AssertRectInside(centerModeCard.LayoutSlot, centerCanvasCard.LayoutSlot, "center mode card");
            AssertRectInside(centerBehaviorCard.LayoutSlot, centerCanvasCard.LayoutSlot, "center behavior card");
            Assert.True(stageScrollViewer.ViewportHeight > 0f, $"Expected the workbench stage to expose a positive viewport height, got {stageScrollViewer.ViewportHeight:0.###}.");
            Assert.True(
                dockOrderBorder.LayoutSlot.Y >= stageBorder.LayoutSlot.Y + stageBorder.LayoutSlot.Height - 0.5f,
                $"Expected the child-order rail to remain below the preview stage, got stage={stageBorder.LayoutSlot} order={dockOrderBorder.LayoutSlot}.");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static void AssertRectInside(LayoutRect inner, LayoutRect outer, string label)
    {
        Assert.True(inner.X >= outer.X - 0.5f, $"Expected {label} to stay inside left bound. inner={inner} outer={outer}");
        Assert.True(inner.Y >= outer.Y - 0.5f, $"Expected {label} to stay inside top bound. inner={inner} outer={outer}");
        Assert.True(inner.X + inner.Width <= outer.X + outer.Width + 0.5f, $"Expected {label} to stay inside right bound. inner={inner} outer={outer}");
        Assert.True(inner.Y + inner.Height <= outer.Y + outer.Height + 0.5f, $"Expected {label} to stay inside bottom bound. inner={inner} outer={outer}");
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