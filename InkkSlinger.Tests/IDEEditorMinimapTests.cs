using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class IDEEditorMinimapTests
{
    [Fact]
    public void HoverFade_RunsThroughUiRootFrameUpdates()
    {
        var minimap = new IDEEditorMinimap
        {
            Width = 96f,
            Height = 240f,
            SourceText = "<Grid>\n  <TextBlock Text=\"One\" />\n</Grid>"
        };
        var root = new Grid();
        root.AddChild(minimap);
        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 320, 260);

        uiRoot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)), viewport);
        Assert.Equal(0f, minimap.ViewportOverlayOpacityForTests);

        minimap.SetMouseOverFromInput(true);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), viewport);

        Assert.True(minimap.ViewportOverlayOpacityForTests > 0f);
        Assert.Equal(1, uiRoot.GetPerformanceTelemetrySnapshotForTests().FrameUpdateParticipantCount);
    }

    [Fact]
    public void HoverFade_DecaysAfterMouseLeavesThroughUiRootFrameUpdates()
    {
        var minimap = new IDEEditorMinimap
        {
            Width = 96f,
            Height = 240f,
            SourceText = "<Grid>\n  <TextBlock Text=\"One\" />\n</Grid>"
        };
        var root = new Grid();
        root.AddChild(minimap);
        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 320, 260);

        minimap.SetMouseOverFromInput(true);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(200)), viewport);
        var hoveredOpacity = minimap.ViewportOverlayOpacityForTests;
        Assert.True(hoveredOpacity > 0f);

        minimap.SetMouseOverFromInput(false);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(216), TimeSpan.FromMilliseconds(16)), viewport);

        Assert.True(minimap.ViewportOverlayOpacityForTests < hoveredOpacity);
        Assert.True(uiRoot.GetPerformanceTelemetrySnapshotForTests().FrameUpdateParticipantCount >= 1);
    }

    [Fact]
    public void PointerNavigation_UsesStableMappingDuringDragDespiteEditorOffsetFeedback()
    {
        var sourceBuilder = new System.Text.StringBuilder();
        for (var i = 0; i < 1200; i++)
        {
            sourceBuilder.AppendLine($"<TextBlock Text=\"Line {i:0000}\" />");
        }

        var minimap = new IDEEditorMinimap
        {
            Width = 96f,
            Height = 300f,
            SourceText = sourceBuilder.ToString(),
            EditorEstimatedLineHeight = 16f,
            EditorViewportHeight = 480f
        };
        minimap.Measure(new Vector2(96f, 300f));
        minimap.Arrange(new LayoutRect(0f, 0f, 96f, 300f));

        var pointer = new Vector2(48f, 150f);
        Assert.True(minimap.HandlePointerDownFromInput(pointer, extendSelection: false));
        var firstLine = minimap.LastRequestedLineNumber;
        Assert.True(firstLine > 1);

        minimap.EditorVerticalOffset = MathF.Max(0f, (firstLine - 1) * minimap.EditorEstimatedLineHeight);
        Assert.True(minimap.HandlePointerMoveFromInput(pointer));
        var secondLine = minimap.LastRequestedLineNumber;

        Assert.InRange(secondLine, firstLine - 1, firstLine + 1);
    }

    [Fact]
    public void PointerNavigation_MapsMinimapTrackPositionToEditorScrollOffset()
    {
        var sourceText = string.Join(
            "\n",
            System.Linq.Enumerable.Range(0, 1000).Select(index => $"<TextBlock Text=\"Line {index:0000}\" />"));
        var minimap = new IDEEditorMinimap
        {
            Width = 96f,
            Height = 300f,
            SourceText = sourceText,
            EditorEstimatedLineHeight = 20f,
            EditorViewportHeight = 400f
        };
        minimap.Measure(new Vector2(96f, 300f));
        minimap.Arrange(new LayoutRect(0f, 0f, 96f, 300f));

        Assert.True(minimap.HandlePointerDownFromInput(new Vector2(48f, 150f), extendSelection: false));

        var expectedMaxOffset = (1000 * minimap.EditorEstimatedLineHeight) - minimap.EditorViewportHeight;
        Assert.Equal(expectedMaxOffset * 0.5f, minimap.LastRequestedVerticalOffset, precision: 3);
        Assert.Equal(491, minimap.LastRequestedLineNumber);
    }

    [Fact]
    public void PointerNavigation_ReusesTrackRatioWhenEditorOffsetChangesDuringDrag()
    {
        var sourceText = string.Join(
            "\n",
            System.Linq.Enumerable.Range(0, 1000).Select(index => $"<TextBlock Text=\"Line {index:0000}\" />"));
        var minimap = new IDEEditorMinimap
        {
            Width = 96f,
            Height = 300f,
            SourceText = sourceText,
            EditorEstimatedLineHeight = 20f,
            EditorViewportHeight = 400f
        };
        minimap.Measure(new Vector2(96f, 300f));
        minimap.Arrange(new LayoutRect(0f, 0f, 96f, 300f));

        var pointer = new Vector2(48f, 150f);
        Assert.True(minimap.HandlePointerDownFromInput(pointer, extendSelection: false));
        var firstOffset = minimap.LastRequestedVerticalOffset;

        minimap.EditorVerticalOffset = firstOffset;
        Assert.True(minimap.HandlePointerMoveFromInput(pointer));

        Assert.Equal(firstOffset, minimap.LastRequestedVerticalOffset, precision: 3);
    }

    [Fact]
    public void EditorScrollOffset_MapsToMinimapContentScrollOffsetOneToOne()
    {
        var sourceText = string.Join(
            "\n",
            System.Linq.Enumerable.Range(0, 1000).Select(index => $"<TextBlock Text=\"Line {index:0000}\" />"));
        var minimap = new IDEEditorMinimap
        {
            Width = 96f,
            Height = 300f,
            SourceText = sourceText,
            EditorEstimatedLineHeight = 20f,
            EditorViewportHeight = 400f
        };
        minimap.Measure(new Vector2(96f, 300f));
        minimap.Arrange(new LayoutRect(0f, 0f, 96f, 300f));

        var maxEditorOffset = (1000 * minimap.EditorEstimatedLineHeight) - minimap.EditorViewportHeight;
        var maxMinimapOffset = (1000 * 6f) - (300f - 12f);
        minimap.EditorVerticalOffset = maxEditorOffset * 0.5f;

        Assert.Equal(maxMinimapOffset * 0.5f, minimap.MinimapVerticalOffsetForTests, precision: 3);
    }

    [Fact]
    public void ViewportOverlayTop_MovesMonotonicallyWithSmoothEditorScroll()
    {
        var sourceText = string.Join(
            "\n",
            System.Linq.Enumerable.Range(0, 1200).Select(index => $"<TextBlock Text=\"Line {index:0000}\" />"));
        var minimap = new IDEEditorMinimap
        {
            Width = 96f,
            Height = 300f,
            SourceText = sourceText,
            EditorEstimatedLineHeight = 20f,
            EditorViewportHeight = 400f
        };
        minimap.Measure(new Vector2(96f, 300f));
        minimap.Arrange(new LayoutRect(0f, 0f, 96f, 300f));

        minimap.EditorVerticalOffset = 0f;
        var previousTop = minimap.ViewportOverlayRectForTests.Y;
        for (var step = 1; step <= 120; step++)
        {
            minimap.EditorVerticalOffset = step * 5f;
            var currentTop = minimap.ViewportOverlayRectForTests.Y;

            Assert.True(
                currentTop >= previousTop - 0.001f,
                $"Overlay moved upward at editor offset {minimap.EditorVerticalOffset}: previous={previousTop}, current={currentTop}");
            previousTop = currentTop;
        }
    }

    [Fact]
    public void ViewportOverlayHeight_KeepsReadableMinimumForLargeDocuments()
    {
        var sourceText = string.Join(
            "\n",
            System.Linq.Enumerable.Range(0, 3000).Select(index => $"<TextBlock Text=\"Line {index:0000}\" />"));
        var minimap = new IDEEditorMinimap
        {
            Width = 96f,
            Height = 300f,
            SourceText = sourceText,
            EditorEstimatedLineHeight = 20f,
            EditorViewportHeight = 400f
        };
        minimap.Measure(new Vector2(96f, 300f));
        minimap.Arrange(new LayoutRect(0f, 0f, 96f, 300f));

        Assert.Equal(24f, minimap.ViewportOverlayRectForTests.Height, precision: 3);
    }
}
