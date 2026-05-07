using InkkSlinger.Designer;
using Microsoft.Xna.Framework;
using System.Text.RegularExpressions;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DesignerHierarchyViewTests
{
    [Fact]
    public void ZoomLayout_WhenScaledGraphExceedsViewport_UsesScaledExtentWithoutCenterOffset()
    {
        var layout = DesignerHierarchyView.CalculateZoomLayout(
            logicalWidth: 2400f,
            logicalHeight: 1200f,
            zoom: 2.5f,
            viewportSize: new Vector2(1264f, 430f));

        Assert.Equal(2.5f, layout.Zoom);
        Assert.Equal(6000f, layout.CanvasWidth);
        Assert.Equal(3000f, layout.CanvasHeight);
        Assert.Equal(0f, layout.LeftOffset);
        Assert.Equal(0f, layout.TopOffset);
    }

    [Fact]
    public void ZoomLayout_WhenScaledGraphIsSmallerThanViewport_CentersWithinViewportSizedCanvas()
    {
        var layout = DesignerHierarchyView.CalculateZoomLayout(
            logicalWidth: 1200f,
            logicalHeight: 520f,
            zoom: 0.35f,
            viewportSize: new Vector2(1264f, 430f));

        Assert.Equal(0.35f, layout.Zoom);
        Assert.Equal(1264f, layout.CanvasWidth);
        Assert.Equal(430f, layout.CanvasHeight);
        Assert.Equal(422f, layout.LeftOffset);
        Assert.Equal(124f, layout.TopOffset);
    }

    [Fact]
    public void HierarchyMarkup_UsesSingleWorkspaceCanvasScrollContentWithoutViewbox()
    {
        var markup = File.ReadAllText(Path.Combine(
            TestApplicationResources.GetRepositoryRoot(),
            "InkkSlinger.Designer",
            "DesignerHierarchyView.xml"));

        Assert.Contains("<DesignerHierarchyWorkspaceCanvas x:Name=\"HierarchyCanvas\"", markup);
        Assert.DoesNotContain("<Viewbox", markup);
        Assert.DoesNotContain("x:Name=\"HierarchyWorkspace\"", markup);
        Assert.DoesNotContain("Width=\"1200\"", markup);
        Assert.DoesNotContain("Height=\"520\"", markup);
    }

    [Fact]
    public void WheelZoom_SourceUpdatesGraphScaleWithoutRebuildingOrRelayingOutChildren()
    {
        var source = File.ReadAllText(Path.Combine(
            TestApplicationResources.GetRepositoryRoot(),
            "InkkSlinger.Designer",
            "DesignerHierarchyView.xml.cs"));

        var match = Regex.Match(
            source,
            @"private void OnHierarchyScrollViewerPreviewMouseWheel[\s\S]*?\n    private void OnHierarchyScrollViewerLayoutUpdated",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, "Expected to find the hierarchy wheel handler in DesignerHierarchyView.xml.cs.");
        Assert.Contains("ApplyZoom();", match.Value);
        Assert.Contains("if (MathF.Abs(nextZoom - oldZoom) < 0.001f)", match.Value);
        Assert.DoesNotContain("if (MathF.Abs(nextZoom - oldZoom) < 0.001f)\r\n        {\r\n            args.Handled = true;", match.Value);
        Assert.DoesNotContain("if (MathF.Abs(nextZoom - oldZoom) < 0.001f)\n        {\n            args.Handled = true;", match.Value);
        Assert.DoesNotContain("UpdateGraphLayout();", match.Value);
        Assert.DoesNotContain("RealizeGraph();", match.Value);
        Assert.DoesNotContain("ClearCanvas();", match.Value);
        Assert.DoesNotContain("Dispatcher.EnqueueDeferred(() => ScrollToZoomAnchor", match.Value);
    }

    [Fact]
    public void ApplyZoom_ReportsZoomExtentWithoutChangingCanvasOrGraphLayerLayoutSizeToZoomExtent()
    {
        var source = File.ReadAllText(Path.Combine(
            TestApplicationResources.GetRepositoryRoot(),
            "InkkSlinger.Designer",
            "DesignerHierarchyView.xml.cs"));

        var match = Regex.Match(
            source,
            @"private void ApplyZoom\(\)[\s\S]*?\n    private void UpdateGraphLayout",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, "Expected to find ApplyZoom in DesignerHierarchyView.xml.cs.");
        Assert.Contains("HierarchyCanvas.ApplyZoomLayout(layout);", match.Value);
        Assert.DoesNotContain("HierarchyCanvas.Width = layout.CanvasWidth;", match.Value);
        Assert.DoesNotContain("HierarchyCanvas.Height = layout.CanvasHeight;", match.Value);
        Assert.Contains("_graphLayer.Width = _workspaceLogicalWidth;", match.Value);
        Assert.Contains("_graphLayer.Height = _workspaceLogicalHeight;", match.Value);
        Assert.DoesNotContain("_graphLayer.Width = layout.CanvasWidth;", match.Value);
        Assert.DoesNotContain("_graphLayer.Height = layout.CanvasHeight;", match.Value);
        Assert.Contains("_graphLayer.Zoom = layout.Zoom;", match.Value);
        Assert.Contains("Canvas.SetLeft(_graphLayer, layout.LeftOffset);", match.Value);
        Assert.Contains("Canvas.SetTop(_graphLayer, layout.TopOffset);", match.Value);
        Assert.DoesNotContain("UpdateNodeLayout", match.Value);
        Assert.DoesNotContain("UpdateConnectorLayout", match.Value);
    }

    [Fact]
    public void WorkspaceCanvas_MeasureReturnsZoomExtentWithoutMeasuringGraphChildren()
    {
        var source = File.ReadAllText(Path.Combine(
            TestApplicationResources.GetRepositoryRoot(),
            "InkkSlinger.Designer",
            "DesignerHierarchyView.xml.cs"));

        var match = Regex.Match(
            source,
            @"public sealed class DesignerHierarchyWorkspaceCanvas[\s\S]*?\n    private static bool AreClose",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, "Expected to find DesignerHierarchyWorkspaceCanvas in DesignerHierarchyView.xml.cs.");
        Assert.Contains("protected override Vector2 MeasureOverride(Vector2 availableSize)", match.Value);
        Assert.Contains("return new Vector2(_extentWidth, _extentHeight);", match.Value);
        Assert.DoesNotContain("frameworkChild.Measure", match.Value);
        Assert.DoesNotContain("child.Measure", match.Value);
        Assert.Contains("protected override Vector2 ArrangeOverride(Vector2 finalSize)", match.Value);
    }

    [Fact]
    public void GraphLayer_DoesNotClipScaledChildren()
    {
        var source = File.ReadAllText(Path.Combine(
            TestApplicationResources.GetRepositoryRoot(),
            "InkkSlinger.Designer",
            "DesignerHierarchyView.xml.cs"));

        Assert.Contains("private sealed class GraphLayerCanvas : Canvas", source);
        Assert.Contains("protected override bool TryGetClipRect(out LayoutRect clipRect)", source);
        Assert.Contains("return false;", source);
        Assert.Contains("Name = \"HierarchyGraphLayer\"", source);
        Assert.DoesNotContain("private readonly DottedWorkspaceCanvas _workspaceLayer = new();", source);
    }

    [Fact]
    public void GraphLayer_ScalesAroundItsOwnLayoutOriginSoScrollOffsetIsNotScaled()
    {
        var source = File.ReadAllText(Path.Combine(
            TestApplicationResources.GetRepositoryRoot(),
            "InkkSlinger.Designer",
            "DesignerHierarchyView.xml.cs"));

        Assert.Contains("protected override bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)", source);
        Assert.Contains("var originX = LayoutSlot.X;", source);
        Assert.Contains("var originY = LayoutSlot.Y;", source);
        Assert.Contains("Matrix.CreateTranslation(-originX, -originY, 0f) *", source);
        Assert.DoesNotContain("private readonly ScaleTransform _graphZoomTransform", source);
    }

    [Fact]
    public void GraphLayer_ExposesFocusedRuntimeDiagnosticsForZoomRetest()
    {
        var source = File.ReadAllText(Path.Combine(
            TestApplicationResources.GetRepositoryRoot(),
            "InkkSlinger.Designer",
            "DesignerHierarchyView.xml.cs"));

        Assert.Contains("GetDesignerHierarchyGraphLayerSnapshotForDiagnostics()", source);
        Assert.Contains("DesignerHierarchyGraphLayerRuntimeDiagnosticsSnapshot", source);
        Assert.Contains("LocalRenderTransformCallCount", source);
        Assert.Contains("LocalRenderTransformActiveCount", source);
        Assert.Contains("RenderCallCount", source);
        Assert.Contains("RenderMilliseconds", source);
    }
}
