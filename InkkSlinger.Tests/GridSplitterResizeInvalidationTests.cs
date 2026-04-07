using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class GridSplitterResizeInvalidationTests
{
    [Fact]
    public void SplitterColumnResize_UsesArrangeOnlyWhenGridDesiredSizeStaysStable()
    {
        var (uiRoot, viewer, grid) = CreateViewerFixture(CreateStableGridContent());
        RunLayout(uiRoot, 640, 480, 16);

        Assert.False(grid.NeedsMeasure);
        Assert.False(viewer.NeedsMeasure);

        var changed = grid.ApplySplitterColumnResize(0, 1, 120f, 180f);

        Assert.True(changed);
        Assert.False(grid.NeedsMeasure);
        Assert.True(grid.NeedsArrange);
        Assert.False(viewer.NeedsMeasure);
        Assert.True(viewer.NeedsArrange);
    }

    [Fact]
    public void SplitterColumnResize_WrappedTextInsideScrollViewer_StaysArrangeOnlyWhenViewerDesiredSizeStaysStable()
    {
        var (uiRoot, viewer, grid) = CreateViewerFixture(CreateWrappedTextGridContent());
        RunLayout(uiRoot, 640, 480, 16);

        Assert.False(grid.NeedsMeasure);
        Assert.False(viewer.NeedsMeasure);

        var changed = grid.ApplySplitterColumnResize(0, 1, 60f, 240f);

        Assert.True(changed);
        Assert.False(grid.NeedsMeasure);
        Assert.True(grid.NeedsArrange);
        Assert.False(viewer.NeedsMeasure);
        Assert.True(viewer.NeedsArrange);
    }

    [Fact]
    public void SplitterColumnResize_RearrangesDirtyGridWithoutRerunningStableAncestorArrangeOverrides()
    {
        var root = new Panel();
        var grid = CreateStableGridContent();
        var contentBorder = new CountingBorder
        {
            Child = grid
        };
        var contentStack = new CountingStackPanel();
        contentStack.AddChild(contentBorder);

        var viewer = new CountingScrollViewer
        {
            Width = 320f,
            Height = 220f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = contentStack
        };

        root.AddChild(viewer);
        var uiRoot = new UiRoot(root);

        RunLayout(uiRoot, 640, 480, 16);

        var viewerArrangeBefore = viewer.ArrangeOverrideCount;
        var stackArrangeBefore = contentStack.ArrangeOverrideCount;
        var borderArrangeBefore = contentBorder.ArrangeOverrideCount;
        var gridArrangeBefore = grid.ArrangeCallCount;

        var changed = grid.ApplySplitterColumnResize(0, 1, 120f, 180f);

        Assert.True(changed);
        RunLayout(uiRoot, 640, 480, 32);

        Assert.Equal(viewerArrangeBefore, viewer.ArrangeOverrideCount);
        Assert.Equal(stackArrangeBefore, contentStack.ArrangeOverrideCount);
        Assert.Equal(borderArrangeBefore, contentBorder.ArrangeOverrideCount);
        Assert.True(grid.ArrangeCallCount > gridArrangeBefore);
    }

    [Fact]
    public void DirectArrangeInvalidation_StillRerunsScrollViewerArrangeOverride()
    {
        var root = new Panel();
        var viewer = CreateCountingViewer(CreateStableGridContent());
        root.AddChild(viewer);
        var uiRoot = new UiRoot(root);

        RunLayout(uiRoot, 640, 480, 16);

        var arrangeBefore = viewer.ArrangeOverrideCount;

        viewer.InvalidateArrange();
        RunLayout(uiRoot, 640, 480, 32);

        Assert.True(viewer.ArrangeOverrideCount > arrangeBefore);
    }

    [Fact]
    public void GridArrange_TranslatesNestedAutoChildWithoutRerunningChildArrangeOverride()
    {
        var innerGrid = new CountingGrid();
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var flexiblePane = new Border
        {
            Height = 32f,
            MinWidth = 80f
        };
        var pinnedBadge = new CountingBorder
        {
            Width = 72f,
            Height = 24f
        };

        Grid.SetColumn(flexiblePane, 0);
        Grid.SetColumn(pinnedBadge, 1);
        innerGrid.AddChild(flexiblePane);
        innerGrid.AddChild(pinnedBadge);

        var uiRoot = new UiRoot(innerGrid);
        RunLayout(uiRoot, 320, 240, 16);

        var innerArrangeBefore = innerGrid.ArrangeCallCount;
        var pinnedArrangeBefore = pinnedBadge.ArrangeOverrideCount;
        var pinnedXBefore = pinnedBadge.LayoutSlot.X;

        RunLayout(uiRoot, 260, 240, 32);

        Assert.True(innerGrid.ArrangeCallCount > innerArrangeBefore);
        Assert.Equal(pinnedArrangeBefore, pinnedBadge.ArrangeOverrideCount);
        Assert.True(pinnedBadge.LayoutSlot.X < pinnedXBefore);
    }

    [Fact]
    public void DescendantMeasureChange_ThatKeepsGridDesiredSizeStable_DoesNotRebubbleParentMeasure()
    {
        var dynamicChild = new DynamicDesiredSizeElement(80f, 32f);
        var grid = CreateStableDesiredSizeGrid(dynamicChild, stableHeight: 72f);
        var host = new CountingBorder
        {
            Child = grid
        };

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 640, 480, 16);

        var hostMeasureBefore = host.MeasureOverrideCount;
        var gridMeasureBefore = grid.MeasureCallCount;

        dynamicChild.SetDesiredHeight(56f);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.Equal(hostMeasureBefore, host.MeasureOverrideCount);
        Assert.Equal(gridMeasureBefore, grid.MeasureCallCount);
        Assert.False(host.NeedsMeasure);
        Assert.False(grid.NeedsMeasure);
        Assert.False(grid.NeedsArrange);
    }

    [Fact]
    public void DescendantMeasureChange_ThatChangesGridDesiredSize_StillRebubblesParentMeasure()
    {
        var dynamicChild = new DynamicDesiredSizeElement(80f, 32f);
        var grid = CreateStableDesiredSizeGrid(dynamicChild, stableHeight: 40f);
        var host = new CountingBorder
        {
            Child = grid
        };

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 640, 480, 16);

        var hostMeasureBefore = host.MeasureOverrideCount;

        dynamicChild.SetDesiredHeight(96f);

        Assert.True(host.NeedsMeasure);
        Assert.True(grid.NeedsMeasure);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.True(host.MeasureOverrideCount > hostMeasureBefore);
    }

    private static Grid CreateStableGridContent()
    {
        var grid = CreateBaseGrid();
        var left = new Border
        {
            Width = 90f,
            Height = 48f
        };
        var right = new Border
        {
            Width = 90f,
            Height = 48f
        };

        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        grid.AddChild(left);
        grid.AddChild(right);
        return grid;
    }

    private static Grid CreateWrappedTextGridContent()
    {
        var grid = CreateBaseGrid();
        var left = new TextBlock
        {
            Text = "The inspector summary wraps when the first column narrows, which changes the grid's desired height.",
            TextWrapping = TextWrapping.Wrap
        };
        var right = new Border
        {
            Width = 90f,
            Height = 48f
        };

        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        grid.AddChild(left);
        grid.AddChild(right);
        return grid;
    }

    private static Grid CreateBaseGrid()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150f, GridUnitType.Pixel), MinWidth = 60f });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150f, GridUnitType.Pixel), MinWidth = 60f });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        return grid;
    }

    private static Grid CreateStableDesiredSizeGrid(FrameworkElement dynamicChild, float stableHeight)
    {
        var grid = CreateBaseGrid();
        var stableChild = new Border
        {
            Width = 90f,
            Height = stableHeight
        };

        Grid.SetColumn(dynamicChild, 0);
        Grid.SetColumn(stableChild, 1);
        grid.AddChild(dynamicChild);
        grid.AddChild(stableChild);
        return grid;
    }

    private static (UiRoot UiRoot, ScrollViewer Viewer, Grid Grid) CreateViewerFixture(Grid grid)
    {
        var root = new Panel();
        var stack = new StackPanel();
        stack.AddChild(grid);

        var viewer = new ScrollViewer
        {
            Width = 320f,
            Height = 220f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        };

        root.AddChild(viewer);
        return (new UiRoot(root), viewer, grid);
    }

    private static CountingScrollViewer CreateCountingViewer(Grid grid)
    {
        var stack = new StackPanel();
        stack.AddChild(grid);
        return new CountingScrollViewer
        {
            Width = 320f,
            Height = 220f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private sealed class CountingScrollViewer : ScrollViewer
    {
        public int ArrangeOverrideCount { get; private set; }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            ArrangeOverrideCount++;
            return base.ArrangeOverride(finalSize);
        }
    }

    private sealed class CountingGrid : Grid
    {
        public int ArrangeCallCount { get; private set; }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            ArrangeCallCount++;
            return base.ArrangeOverride(finalSize);
        }
    }

    private sealed class CountingStackPanel : StackPanel
    {
        public int ArrangeOverrideCount { get; private set; }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            ArrangeOverrideCount++;
            return base.ArrangeOverride(finalSize);
        }
    }

    private sealed class CountingBorder : Border
    {
        public int MeasureOverrideCount { get; private set; }
        public int ArrangeOverrideCount { get; private set; }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            MeasureOverrideCount++;
            return base.MeasureOverride(availableSize);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            ArrangeOverrideCount++;
            return base.ArrangeOverride(finalSize);
        }
    }

    private sealed class DynamicDesiredSizeElement : FrameworkElement
    {
        private float _desiredWidth;
        private float _desiredHeight;

        public DynamicDesiredSizeElement(float desiredWidth, float desiredHeight)
        {
            _desiredWidth = desiredWidth;
            _desiredHeight = desiredHeight;
        }

        public void SetDesiredHeight(float desiredHeight)
        {
            if (MathF.Abs(_desiredHeight - desiredHeight) <= 0.01f)
            {
                return;
            }

            _desiredHeight = desiredHeight;
            InvalidateMeasure();
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            var width = float.IsFinite(availableSize.X)
                ? MathF.Min(_desiredWidth, MathF.Max(0f, availableSize.X))
                : _desiredWidth;
            return new Vector2(width, _desiredHeight);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            return finalSize;
        }
    }
}