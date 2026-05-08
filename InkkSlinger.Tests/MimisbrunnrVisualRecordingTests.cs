using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class MimisbrunnrVisualRecordingTests
{
    [Fact]
    public void VisualRecords_CreatedForRetainedVisuals()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var border = CreateRecordedBorder();
        root.AddChild(border);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
        Assert.Equal(2, telemetry.VisualRecordCount);
        Assert.Equal(2, telemetry.VisualRecordRebuildCount);
        Assert.Equal(0, telemetry.VisualRecordReuseCount);
        Assert.Equal("Border", telemetry.LastRecordedVisualType);
        Assert.Equal("recordedBorder", telemetry.LastRecordedVisualName);
        Assert.Equal(5, telemetry.LastVisualRecordCommandCount);
    }

    [Fact]
    public void VisualRecords_RectangularBorderCommands_AreDeterministicAndLocal()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var border = CreateRecordedBorder();
        root.AddChild(border);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var firstRecord = uiRoot.GetVisualRecordForTests(border).Commands;
        uiRoot.UpdateVisualRecordsForTests();
        var secondRecord = uiRoot.GetVisualRecordForTests(border).Commands;

        Assert.Equal(firstRecord, secondRecord);
        Assert.Collection(
            firstRecord,
            command =>
            {
                Assert.Equal(VisualCommandKind.FilledRect, command.Kind);
                Assert.Equal(new LayoutRect(0f, 0f, 20f, 20f), command.Rect);
                Assert.Equal(Color.Red, command.Color);
            },
            command => Assert.Equal(new LayoutRect(0f, 0f, 1f, 20f), command.Rect),
            command => Assert.Equal(new LayoutRect(19f, 0f, 1f, 20f), command.Rect),
            command => Assert.Equal(new LayoutRect(0f, 0f, 20f, 1f), command.Rect),
            command => Assert.Equal(new LayoutRect(0f, 19f, 20f, 1f), command.Rect));
    }

    [Fact]
    public void VisualRecords_PanelBackground_RecordsReplayableLocalFill()
    {
        var panel = new Panel { Background = Color.CornflowerBlue };
        panel.SetLayoutSlot(new LayoutRect(25f, 30f, 75f, 50f));

        var uiRoot = new UiRoot(panel);
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var commands = uiRoot.GetVisualRecordForTests(panel);
        Assert.Equal(1, commands.Count);
        Assert.Equal(0, commands.UnsupportedCommandCount);
        Assert.Equal(VisualCommandKind.FilledRect, commands.Commands[0].Kind);
        Assert.Equal(new LayoutRect(0f, 0f, 75f, 50f), commands.Commands[0].Rect);
        Assert.True(VisualCommandReplayer.CanReplay(commands));
    }

    [Fact]
    public void VisualRecords_TextPlaceholder_RemainsExplicitUnsupportedFallback()
    {
        var textBlock = new TextBlock
        {
            Text = "hello",
            Foreground = Color.Yellow
        };
        textBlock.SetLayoutSlot(new LayoutRect(40f, 50f, 80f, 16f));
        textBlock.Arrange(textBlock.LayoutSlot);

        var uiRoot = new UiRoot(textBlock);
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var commands = uiRoot.GetVisualRecordForTests(textBlock);
        Assert.Equal(1, commands.Count);
        Assert.Equal(1, commands.UnsupportedCommandCount);
        Assert.Equal(VisualCommandKind.TextPlaceholder, commands.Commands[0].Kind);
        Assert.Equal(new LayoutRect(0f, 0f, 80f, 16f), commands.Commands[0].Rect);
        Assert.False(VisualCommandReplayer.CanReplay(commands));
    }

    [Fact]
    public void VisualRecords_ReusedUntilContentVersionChanges()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var border = CreateRecordedBorder();
        root.AddChild(border);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.GetTelemetryAndReset();

        uiRoot.UpdateVisualRecordsForTests();
        var reused = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
        Assert.Equal(0, reused.VisualRecordRebuildCount);
        Assert.Equal(0, reused.VisualRecordReuseCount);

        uiRoot.GetTelemetryAndReset();
        border.Background = new SolidColorBrush(Color.Blue);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var rebuilt = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
        Assert.Equal(1, rebuilt.VisualRecordRebuildCount);
        Assert.Equal(0, rebuilt.VisualRecordReuseCount);
        Assert.Equal("Border", rebuilt.LastRecordedVisualType);
    }

    [Fact]
    public void VisualRecords_RefreshDirtyDescendantRecords_WhenDirtyRootsCoalesceToAncestor()
    {
        var root = new Panel
        {
            Background = new Color(10, 10, 10)
        };
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));

        var child = CreateRecordedBorder();
        child.Background = new SolidColorBrush(new Color(20, 20, 20));
        root.AddChild(child);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.GetTelemetryAndReset();

        root.Background = new Color(30, 30, 30);
        child.Background = new SolidColorBrush(new Color(40, 40, 40));

        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var childRecord = uiRoot.GetVisualRecordForTests(child);
        Assert.Equal(new Color(40, 40, 40), childRecord.Commands[0].Color);

        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
        Assert.True(
            telemetry.VisualRecordRebuildCount >= 2,
            $"Expected the coalesced ancestor span to refresh both ancestor and descendant records. Rebuilds={telemetry.VisualRecordRebuildCount}, last={telemetry.LastRecordedVisualType}#{telemetry.LastRecordedVisualName}.");
    }

    [Fact]
    public void VisualRecords_TransformInvalidationReusesContentRecords()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var border = CreateRecordedBorder();
        root.AddChild(border);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();
        var compositionRebuildCount = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests().CompositionRebuildCount;
        uiRoot.GetTelemetryAndReset();

        uiRoot.NotifyDirectRenderInvalidation(border, RenderInvalidationKind.Transform);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
        Assert.Equal(1, telemetry.TransformInvalidationCount);
        Assert.Equal(0, telemetry.ContentInvalidationCount);
        Assert.Equal(1, telemetry.CompositionMetadataUpdateCount);
        Assert.Equal(1, telemetry.TransformMetadataUpdateCount);
        Assert.Equal(0, telemetry.CompositionMetadataUpdateMissCount);
        Assert.Equal(compositionRebuildCount, telemetry.CompositionRebuildCount);
        Assert.Equal("Transform", telemetry.LastCompositionMetadataUpdateKind);
        Assert.Equal("Border#recordedBorder", telemetry.LastCompositionMetadataUpdateSource);
        Assert.Equal(0, telemetry.VisualRecordRebuildCount);
        Assert.Equal(0, telemetry.VisualRecordReuseCount);
    }

    [Fact]
    public void VisualRecords_OpacityInvalidationIsRepresentedAsMetadataUpdate()
    {
        AssertCompositorMetadataInvalidationDoesNotRebuildRecord(RenderInvalidationKind.Opacity);
    }

    [Fact]
    public void VisualRecords_VisibilityInvalidationIsRepresentedAsMetadataUpdate()
    {
        AssertCompositorMetadataInvalidationDoesNotRebuildRecord(RenderInvalidationKind.Visibility);
    }

    [Fact]
    public void VisualRecords_ClipInvalidationIsRepresentedAsMetadataUpdate()
    {
        AssertCompositorMetadataInvalidationDoesNotRebuildRecord(RenderInvalidationKind.Clip);
    }

    [Theory]
    [InlineData((int)RenderInvalidationKind.Transform)]
    [InlineData((int)RenderInvalidationKind.Opacity)]
    [InlineData((int)RenderInvalidationKind.Clip)]
    [InlineData((int)RenderInvalidationKind.Visibility)]
    public void VisualRecords_MetadataPropertyChange_DoesNotRebuildContentRecord(int kindValue)
    {
        var kind = (RenderInvalidationKind)kindValue;
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var visual = new RecordingElement { Name = "recordedVisual" };
        visual.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));
        root.AddChild(visual);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.GetTelemetryAndReset();

        ApplyMetadataPropertyChange(visual, kind);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
        Assert.Equal(0, telemetry.ContentInvalidationCount);
        Assert.Equal(1, telemetry.CompositionMetadataUpdateCount);
        Assert.Equal(0, telemetry.CompositionMetadataUpdateMissCount);
        Assert.Equal(kind.ToString(), telemetry.LastCompositionMetadataUpdateKind);
        Assert.Equal(0, telemetry.VisualRecordRebuildCount);
        Assert.Equal(0, telemetry.VisualRecordReuseCount);
    }

    [Fact]
    public void VisualRecords_TransformPropertyChangeOnParent_DoesNotRerecordChildContent()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 200f));
        var parent = new Panel { Name = "transformedParent" };
        parent.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var child = new RecordingElement { Name = "recordedChild" };
        child.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));
        parent.AddChild(child);
        root.AddChild(parent);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.GetTelemetryAndReset();

        parent.RenderTransform = new TranslateTransform { X = 12f, Y = 8f };
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
        Assert.Equal(1, telemetry.TransformInvalidationCount);
        Assert.Equal(1, telemetry.TransformMetadataUpdateCount);
        Assert.Equal(0, telemetry.ContentInvalidationCount);
        Assert.Equal(0, telemetry.VisualRecordRebuildCount);
        Assert.Equal(0, telemetry.VisualRecordReuseCount);
        Assert.Equal("MetadataOnly", telemetry.LastCompositionPrimaryMode);
        Assert.Equal("composition-metadata-only", telemetry.LastCompositionPrimaryReason);
    }

    private static void AssertCompositorMetadataInvalidationDoesNotRebuildRecord(RenderInvalidationKind kind)
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));
        var border = CreateRecordedBorder();
        root.AddChild(border);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();
        uiRoot.GetTelemetryAndReset();

        uiRoot.NotifyDirectRenderInvalidation(border, kind);
        uiRoot.SynchronizeRetainedRenderListForTests();
        uiRoot.UpdateVisualRecordsForTests();

        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
        Assert.Equal(1, telemetry.CompositionMetadataUpdateCount);
        Assert.Equal(0, telemetry.CompositionMetadataUpdateMissCount);
        Assert.Equal(kind.ToString(), telemetry.LastCompositionMetadataUpdateKind);
        Assert.Equal("Border#recordedBorder", telemetry.LastCompositionMetadataUpdateSource);
        Assert.Equal(0, telemetry.VisualRecordRebuildCount);
        Assert.Equal(0, telemetry.VisualRecordReuseCount);

        Assert.Equal(kind == RenderInvalidationKind.Opacity ? 1 : 0, telemetry.OpacityMetadataUpdateCount);
        Assert.Equal(kind == RenderInvalidationKind.Visibility ? 1 : 0, telemetry.VisibilityMetadataUpdateCount);
        Assert.Equal(kind == RenderInvalidationKind.Clip ? 1 : 0, telemetry.ClipMetadataUpdateCount);
    }

    private static Border CreateRecordedBorder()
    {
        var border = new Border
        {
            Name = "recordedBorder",
            Background = new SolidColorBrush(Color.Red),
            BorderBrush = new SolidColorBrush(Color.White),
            BorderThickness = new Thickness(1f)
        };
        border.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));
        return border;
    }

    private static void ApplyMetadataPropertyChange(RecordingElement visual, RenderInvalidationKind kind)
    {
        switch (kind)
        {
            case RenderInvalidationKind.Transform:
                visual.RenderTransform = new TranslateTransform { X = 5f, Y = 3f };
                break;
            case RenderInvalidationKind.Opacity:
                visual.Opacity = 0.5f;
                break;
            case RenderInvalidationKind.Clip:
                visual.ClipToBounds = true;
                break;
            case RenderInvalidationKind.Visibility:
                visual.IsVisible = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private sealed class RecordingElement : UIElement
    {
        public string Name { get; set; } = string.Empty;

        internal override void RecordVisual(VisualRecordingContext context)
        {
            context.DrawFilledRect(new LayoutRect(0f, 0f, LayoutSlot.Width, LayoutSlot.Height), Color.Red, Opacity);
        }
    }
}
