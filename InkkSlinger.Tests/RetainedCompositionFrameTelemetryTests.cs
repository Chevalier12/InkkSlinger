using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RetainedCompositionFrameTelemetryTests
{
    [Fact]
    public void TransformMetadataInvalidation_ProducesMetadataOnlyCompositionPass()
    {
        var (uiRoot, border) = CreatePreparedRoot();

        uiRoot.NotifyDirectRenderInvalidation(border, RenderInvalidationKind.Transform);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
        Assert.Equal("MetadataOnly", telemetry.LastCompositionPrimaryMode);
        Assert.Equal("composition-metadata-only", telemetry.LastCompositionPrimaryReason);
        Assert.Equal(0, telemetry.LastCompositionRecordPassCount);
        Assert.Equal(1, telemetry.LastCompositionMetadataPassCount);
        Assert.Equal(0, telemetry.LastCompositionFullPassCount);
        Assert.Equal(1, telemetry.CompositorOnlyFrameCount);
    }

    [Fact]
    public void ContentInvalidation_ProducesRecordAndFullCompositionPass()
    {
        var (uiRoot, border) = CreatePreparedRoot();

        border.Background = new SolidColorBrush(Color.Blue);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
        Assert.Equal("FullComposition", telemetry.LastCompositionPrimaryMode);
        Assert.Equal("full-composition", telemetry.LastCompositionPrimaryReason);
        Assert.Equal(1, telemetry.LastCompositionRecordPassCount);
        Assert.Equal(1, telemetry.LastCompositionFullPassCount);
        Assert.Equal(1, telemetry.FullCompositionFrameCount);
    }

    [Fact]
    public void FullDirtyFrame_ProducesFullCompositionPass()
    {
        var (uiRoot, _) = CreatePreparedRoot();

        uiRoot.ForceFullRedrawForSurfaceReset();
        uiRoot.UpdateVisualRecordsForTests();

        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
        Assert.Equal("FullComposition", telemetry.LastCompositionPrimaryMode);
        Assert.Equal("full-dirty", telemetry.LastCompositionPrimaryReason);
        Assert.Equal(1, telemetry.LastCompositionFullPassCount);
        Assert.Equal(1, telemetry.FullCompositionFrameCount);
    }

    private static (UiRoot UiRoot, Border Border) CreatePreparedRoot()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var border = new Border
        {
            Name = "compositionTelemetryBorder",
            Background = new SolidColorBrush(Color.Red),
            BorderBrush = new SolidColorBrush(Color.White),
            BorderThickness = new Thickness(1f)
        };
        border.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));
        root.AddChild(border);

        var uiRoot = new UiRoot(root);
        uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, 100f, 100f));
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.GetTelemetryAndReset();
        return (uiRoot, border);
    }
}
