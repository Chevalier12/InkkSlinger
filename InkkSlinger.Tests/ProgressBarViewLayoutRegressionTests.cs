using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ProgressBarViewLayoutRegressionTests
{
    [Fact]
    public void ProgressBarView_TwoCardSampleGrid_StaysWithinGridBoundsAtTabletWidth()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new ProgressBarView();
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 720, 620, 16);

            var sampleGrid = FindSampleGrid(view);
            Assert.NotNull(sampleGrid);

            var bounds = sampleGrid!.LayoutSlot;
            var cards = sampleGrid.GetVisualChildren()
                .OfType<Border>()
                .Where(static border => border.LayoutSlot.Width > 0f && Grid.GetColumn(border) is 0 or 2)
                .OrderBy(static border => border.LayoutSlot.X)
                .ToArray();

            Assert.Equal(2, cards.Length);

            foreach (var card in cards)
            {
                var right = card.LayoutSlot.X + card.LayoutSlot.Width;
                Assert.True(
                    card.LayoutSlot.X >= bounds.X - 0.5f,
                    $"Expected card to stay inside the sample grid on the left edge. cardX={card.LayoutSlot.X:0.###}, gridX={bounds.X:0.###}");
                Assert.True(
                    right <= bounds.X + bounds.Width + 0.5f,
                    $"Expected card to stay inside the sample grid on the right edge. cardRight={right:0.###}, gridRight={bounds.X + bounds.Width:0.###}");
            }
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    private static Grid? FindSampleGrid(UIElement root)
    {
        if (root is Grid grid &&
            grid.ColumnDefinitions.Count == 3 &&
            grid.ColumnDefinitions[0].Width.IsStar &&
            MathF.Abs(grid.ColumnDefinitions[0].Width.Value - 2f) <= 0.01f &&
            grid.ColumnDefinitions[1].Width.IsPixel &&
            MathF.Abs(grid.ColumnDefinitions[1].Width.Value - 16f) <= 0.01f &&
            grid.ColumnDefinitions[2].Width.IsStar &&
            MathF.Abs(grid.ColumnDefinitions[2].Width.Value - 1f) <= 0.01f)
        {
            return grid;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindSampleGrid(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
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
        TestApplicationResources.LoadDemoAppResources();
    }
}