using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class GridSplitterViewLayoutRegressionTests
{
    [Fact]
    public void GridSplitterView_ShrinkingPrimaryCanvasPane_RemeasuresWrappedText()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new GridSplitterView();
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 1100, 900, 16);

            var splitter = Assert.IsType<GridSplitter>(view.FindName("NavigationSplitter"));
            var primaryEditorGrid = Assert.IsType<Grid>(view.FindName("PrimaryEditorGrid"));
            var canvasPane = Assert.IsType<Border>(view.FindName("PrimaryCanvasPane"));
            var telemetryTexts = new[]
            {
                Assert.IsType<TextBlock>(view.FindName("PrimaryPairSummaryText")),
                Assert.IsType<TextBlock>(view.FindName("IncrementSummaryText")),
                Assert.IsType<TextBlock>(view.FindName("InteractionSummaryText")),
                Assert.IsType<TextBlock>(view.FindName("PrimaryMetricsText")),
                Assert.IsType<TextBlock>(view.FindName("SecondaryMetricsText"))
            };

            var wrappedTexts = GetWrappedTextBlocks(canvasPane)
                .Where(static text => !string.IsNullOrWhiteSpace(text.Text))
                .ToArray();
            Assert.NotEmpty(wrappedTexts);

            foreach (var viewportWidth in new[] { 1100, 980, 900 })
            {
                RunLayout(uiRoot, viewportWidth, 900, 32);

                foreach (var deltaX in new[] { 80f, 60f, 60f })
                {
                    DragSplitter(uiRoot, splitter, deltaX, 0f);
                    RunLayout(uiRoot, viewportWidth, 900, 48);

                    var context = $"viewportWidth={viewportWidth}, deltaX={deltaX:0.##}, centerWidth={primaryEditorGrid.ColumnDefinitions[2].ActualWidth:0.##}";
                    AssertWrappedTextMatchesCurrentWidth(wrappedTexts, context);
                    AssertWrappedTextMatchesCurrentWidth(telemetryTexts, context);
                }
            }
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void GridSplitterView_ShrinkingPrimaryCanvasPane_TracksOldAndNewDirtyBoundsForWrappedText()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new GridSplitterView();
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 1100, 900, 16);

            var splitter = Assert.IsType<GridSplitter>(view.FindName("NavigationSplitter"));
            var text = FindTextBlockByExactText(
                view,
                "This center lane behaves like a WPF editor canvas: it absorbs most width, but still yields to splitter drags and keyboard nudges.");
            Assert.NotNull(text);

            uiRoot.RebuildRenderListForTests();
            uiRoot.ResetDirtyStateForTests();
            view.ClearRenderInvalidationRecursive();

            var oldBounds = text!.LayoutSlot;

            DragSplitter(uiRoot, splitter, 200f, 0f);
            RunLayout(uiRoot, 1100, 900, 32);

            var newBounds = text.LayoutSlot;
            if (RectsNearlyEqual(oldBounds, newBounds))
            {
                return;
            }

            var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();
            var isFullDirty = uiRoot.IsFullDirtyForTests();
            Assert.True(
                isFullDirty || dirtyRegions.Count > 0,
                $"Expected splitter shrink to invalidate either dirty regions or the full frame. oldBounds={oldBounds}, newBounds={newBounds}");

            if (isFullDirty)
            {
                return;
            }

            Assert.True(
                dirtyRegions.Any(region => ContainsRect(region, oldBounds)),
                $"Expected dirty regions to include the old wrapped text bounds after splitter shrink. oldBounds={oldBounds}, dirtyRegions={FormatRegions(dirtyRegions)}");
            Assert.True(
                dirtyRegions.Any(region => ContainsRect(region, newBounds)),
                $"Expected dirty regions to include the new wrapped text bounds after splitter shrink. newBounds={newBounds}, dirtyRegions={FormatRegions(dirtyRegions)}");
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void GridSplitterView_ShrinkingPrimaryCanvasPane_KeepsExactWrappedCopyMultiLineAtNarrowWidths()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new GridSplitterView();
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 860, 900, 16);

            var splitter = Assert.IsType<GridSplitter>(view.FindName("NavigationSplitter"));
            var exactTexts = new[]
            {
                Assert.IsType<TextBlock>(FindTextBlockByExactText(
                    view,
                    "This center lane behaves like a WPF editor canvas: it absorbs most width, but still yields to splitter drags and keyboard nudges.")),
                Assert.IsType<TextBlock>(FindTextBlockByExactText(
                    view,
                    "Try dragging either rail, then click it and use arrow keys. Pane minimums prevent the shell from collapsing through the splitter."))
            };

            foreach (var deltaX in new[] { 140f, 120f, 120f })
            {
                DragSplitter(uiRoot, splitter, deltaX, 0f);
                RunLayout(uiRoot, 860, 900, 32);
            }

            foreach (var text in exactTexts)
            {
                var effectiveWidth = MathF.Max(text.LayoutSlot.Width, 0f);
                var expectedLayout = TextLayout.LayoutForElement(
                    text.Text,
                    text,
                    text.FontSize,
                    effectiveWidth,
                    TextWrapping.Wrap);

                Assert.True(
                    expectedLayout.Lines.Count > 1,
                    $"Expected exact GridSplitter wrapped copy to stay multiline after shrink. text='{Abbreviate(text.Text)}', width={text.LayoutSlot.Width:0.##}, desiredHeight={text.DesiredSize.Y:0.##}, actualHeight={text.ActualHeight:0.##}");
                Assert.True(
                    text.DesiredSize.Y - text.Margin.Vertical > UiTextRenderer.GetLineHeight(text, text.FontSize) + 0.5f,
                    $"Expected exact GridSplitter wrapped copy to measure taller than one line after shrink. text='{Abbreviate(text.Text)}', width={text.LayoutSlot.Width:0.##}, desiredHeight={text.DesiredSize.Y:0.##}, renderText='{string.Join("\\n", expectedLayout.Lines)}'");
            }
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    private static void AssertWrappedTextMatchesCurrentWidth(IEnumerable<TextBlock> wrappedTexts, string context)
    {
        foreach (var text in wrappedTexts)
        {
            var expectedLayout = TextLayout.LayoutForElement(
                text.Text,
                text,
                text.FontSize,
                text.LayoutSlot.Width,
                TextWrapping.Wrap);

            Assert.Equal(
                expectedLayout.Size.Y,
                text.DesiredSize.Y - text.Margin.Vertical,
                3);

            Assert.True(
                text.ActualHeight + 3f >= expectedLayout.Size.Y,
                $"Expected wrapped TextBlock to keep enough actual height after splitter resize. text='{Abbreviate(text.Text)}', actualHeight={text.ActualHeight:0.##}, desiredHeight={text.DesiredSize.Y:0.##}, expectedHeight={expectedLayout.Size.Y:0.##}, width={text.LayoutSlot.Width:0.##}, {context}");
        }
    }

    private static TextBlock[] GetWrappedTextBlocks(UIElement root)
    {
        var results = new List<TextBlock>();
        CollectWrappedTextBlocks(root, results);
        return results.ToArray();
    }

    private static void CollectWrappedTextBlocks(UIElement root, List<TextBlock> results)
    {
        if (root is TextBlock text && text.TextWrapping == TextWrapping.Wrap)
        {
            results.Add(text);
        }

        foreach (var child in root.GetVisualChildren())
        {
            CollectWrappedTextBlocks(child, results);
        }
    }

    private static string Abbreviate(string text)
    {
        return text.Length <= 48 ? text : text[..48] + "...";
    }

    private static TextBlock? FindTextBlockByExactText(UIElement root, string text)
    {
        if (root is TextBlock textBlock && string.Equals(textBlock.Text, text, StringComparison.Ordinal))
        {
            return textBlock;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var match = FindTextBlockByExactText(child, text);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool ContainsRect(LayoutRect outer, LayoutRect inner)
    {
        return outer.X <= inner.X + 0.5f &&
               outer.Y <= inner.Y + 0.5f &&
               outer.X + outer.Width >= inner.X + inner.Width - 0.5f &&
               outer.Y + outer.Height >= inner.Y + inner.Height - 0.5f;
    }

    private static bool RectsNearlyEqual(LayoutRect first, LayoutRect second)
    {
        return MathF.Abs(first.X - second.X) < 0.5f &&
               MathF.Abs(first.Y - second.Y) < 0.5f &&
               MathF.Abs(first.Width - second.Width) < 0.5f &&
               MathF.Abs(first.Height - second.Height) < 0.5f;
    }

    private static string FormatRegions(IReadOnlyList<LayoutRect> regions)
    {
        return string.Join(", ", regions.Select(static region => region.ToString()));
    }

    private static void DragSplitter(UiRoot uiRoot, GridSplitter splitter, float deltaX, float deltaY)
    {
        var start = GetCenter(splitter.LayoutSlot);
        var end = new Vector2(start.X + deltaX, start.Y + deltaY);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Microsoft.Xna.Framework.Input.Keys>(),
            ReleasedKeys = new List<Microsoft.Xna.Framework.Input.Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private static Dictionary<object, object> SnapshotApplicationResources()
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