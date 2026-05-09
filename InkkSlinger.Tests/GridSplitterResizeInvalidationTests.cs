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
    public void SplitterColumnResize_StableGridScopesBoundsMetadataToChangedChildSlots()
    {
        var grid = new Grid
        {
            Name = "metadataRootGrid"
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150f, GridUnitType.Pixel), MinWidth = 60f });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150f, GridUnitType.Pixel), MinWidth = 60f });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(80f, GridUnitType.Pixel) });

        var left = new Border
        {
            Name = "metadataLeftSlot"
        };
        var right = new Border
        {
            Name = "metadataRightSlot"
        };

        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        grid.AddChild(left);
        grid.AddChild(right);

        var root = new Panel();
        root.AddChild(grid);
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 360, 160, 16);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        grid.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        uiRoot.GetTelemetryAndReset();

        var changed = grid.ApplySplitterColumnResize(0, 1, 120f, 180f);

        Assert.True(changed);
        Assert.False(grid.NeedsMeasure);
        Assert.True(grid.NeedsArrange);

        RunLayout(uiRoot, 360, 160, 32);

        var telemetry = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();

        Assert.Equal(0, telemetry.DirtyRootCount);
        Assert.Equal(0, telemetry.CompositionMetadataUpdateMissCount);
        Assert.Equal(2, telemetry.CompositionMetadataUpdateCount);
        Assert.Equal("Bounds", telemetry.LastCompositionMetadataUpdateKind);
        Assert.NotEqual("Grid#metadataRootGrid", telemetry.LastCompositionMetadataUpdateSource);
        Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
    }

    [Fact]
    public void GridCellAttachedProperty_InvalidatesParentLayoutWithoutInvalidatingChildContent()
    {
        var parent = new Grid();
        parent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100f) });
        parent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100f) });
        parent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40f) });

        var child = new CountingGrid();
        child.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        child.AddChild(new Border { Width = 20f, Height = 20f });
        parent.AddChild(child);

        var uiRoot = new UiRoot(parent);
        RunLayout(uiRoot, 240, 80, 16);
        uiRoot.ResetDirtyStateForTests();
        parent.ClearRenderInvalidationRecursive();

        var childMeasureBefore = child.MeasureOverrideCount;
        var childRenderBefore = child.RenderInvalidationCount;

        Grid.SetColumn(child, 1);

        Assert.False(child.NeedsMeasure);
        Assert.Equal(childMeasureBefore, child.MeasureOverrideCount);
        Assert.Equal(childRenderBefore, child.RenderInvalidationCount);
        Assert.True(parent.NeedsMeasure || parent.NeedsArrange);
    }


    [Fact]
    public void SplitterRowResize_RepeatedMicroDeltasWithStableNoWrapContent_AvoidsRepeatedMeasureInvalidation()
    {
        var root = new Panel();
        var grid = CreateStableNoWrapRowGridContent();
        var viewer = CreateCountingViewer(grid);
        root.AddChild(viewer);
        var uiRoot = new UiRoot(root);

        RunLayout(uiRoot, 640, 480, 16);

        var viewerMeasureWorkBefore = viewer.MeasureWorkCount;
        var viewerMeasureInvalidationsBefore = viewer.GetFrameworkElementSnapshotForDiagnostics().InvalidateMeasureCallCount;
        var gridMeasureWorkBefore = grid.MeasureWorkCount;
        var gridMeasureInvalidationsBefore = grid.GetFrameworkElementSnapshotForDiagnostics().InvalidateMeasureCallCount;
        var gridArrangeWorkBefore = grid.ArrangeWorkCount;

        var resizeSequence = new (float Top, float Bottom)[]
        {
            (141f, 139f),
            (142f, 138f),
            (143f, 137f),
            (144f, 136f),
            (145f, 135f),
            (146f, 134f),
            (147f, 133f),
            (148f, 132f),
            (149f, 131f),
            (150f, 130f),
            (151f, 129f),
            (152f, 128f)
        };

        foreach (var step in resizeSequence)
        {
            var changed = grid.ApplySplitterRowResize(0, 1, step.Top, step.Bottom);

            Assert.True(changed);
            Assert.False(grid.NeedsMeasure);
            Assert.True(grid.NeedsArrange);
            Assert.False(viewer.NeedsMeasure);
            RunLayout(uiRoot, 640, 480, 32);
        }

        var viewerMeasureInvalidationsAfter = viewer.GetFrameworkElementSnapshotForDiagnostics().InvalidateMeasureCallCount;
        var gridMeasureInvalidationsAfter = grid.GetFrameworkElementSnapshotForDiagnostics().InvalidateMeasureCallCount;

        Assert.Equal(viewerMeasureWorkBefore, viewer.MeasureWorkCount);
        Assert.Equal(viewerMeasureInvalidationsBefore, viewerMeasureInvalidationsAfter);
        Assert.Equal(gridMeasureWorkBefore, grid.MeasureWorkCount);
        Assert.Equal(gridMeasureInvalidationsBefore, gridMeasureInvalidationsAfter);
        Assert.True(grid.ArrangeWorkCount > gridArrangeWorkBefore);
    }

    [Fact]
    public void SplitterRowResize_NoTemplateRootControlWithUnchangedConstraint_UsesArrangeOnly()
    {
        var root = new Panel();
        var grid = CreateNoTemplateRootControlRowGridContent(out var picker);
        var viewer = CreateCountingViewer(grid);
        root.AddChild(viewer);
        var uiRoot = new UiRoot(root);

        RunLayout(uiRoot, 640, 480, 16);

        var pickerMeasureWorkBefore = picker.MeasureWorkCount;
        var gridMeasureWorkBefore = grid.MeasureWorkCount;
        var viewerMeasureWorkBefore = viewer.MeasureWorkCount;

        var changed = grid.ApplySplitterRowResize(0, 1, 144f, 136f);

        Assert.True(changed);
        Assert.False(grid.NeedsMeasure);
        Assert.True(grid.NeedsArrange);
        Assert.False(viewer.NeedsMeasure);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.Equal(pickerMeasureWorkBefore, picker.MeasureWorkCount);
        Assert.Equal(gridMeasureWorkBefore, grid.MeasureWorkCount);
        Assert.Equal(viewerMeasureWorkBefore, viewer.MeasureWorkCount);
    }

    [Fact]
    public void SplitterRowResize_CollapsedNoTemplateRootControlInAutoRow_UsesArrangeOnly()
    {
        var root = new Panel();
        var grid = CreateCollapsedAutoRowNoTemplateRootControlGridContent(out var pathTextBox);
        var viewer = CreateCountingViewer(grid);
        root.AddChild(viewer);
        var uiRoot = new UiRoot(root);

        RunLayout(uiRoot, 640, 480, 16);

        pathTextBox.Measure(new Vector2(pathTextBox.PreviousAvailableSizeForTests.X, 0f));

        Assert.Equal(Vector2.Zero, pathTextBox.DesiredSize);
        Assert.True(
            pathTextBox.CanReuseMeasureForAvailableSizeChangeForParentLayout(
                pathTextBox.PreviousAvailableSizeForTests,
                new Vector2(pathTextBox.PreviousAvailableSizeForTests.X, float.PositiveInfinity)));

        var pathTextBoxMeasureWorkBefore = pathTextBox.MeasureWorkCount;
        var gridMeasureWorkBefore = grid.MeasureWorkCount;
        var viewerMeasureWorkBefore = viewer.MeasureWorkCount;

        var changed = grid.ApplySplitterRowResize(0, 1, 144f, 136f);

        Assert.True(changed);
        Assert.False(grid.NeedsMeasure);
        Assert.True(grid.NeedsArrange);
        Assert.False(viewer.NeedsMeasure);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.Equal(pathTextBoxMeasureWorkBefore, pathTextBox.MeasureWorkCount);
        Assert.Equal(gridMeasureWorkBefore, grid.MeasureWorkCount);
        Assert.Equal(viewerMeasureWorkBefore, viewer.MeasureWorkCount);
    }

    [Fact]
    public void SplitterRowResize_TreeViewFallbackScrollHostWithStableDesiredSize_UsesArrangeOnly()
    {
        var root = new Panel();
        var grid = CreateTreeViewFallbackScrollHostRowGridContent(out var treeView);
        var viewer = CreateCountingViewer(grid);
        root.AddChild(viewer);
        var uiRoot = new UiRoot(root);

        RunLayout(uiRoot, 640, 480, 16);

        var treeViewMeasureWorkBefore = treeView.MeasureWorkCount;
        var gridMeasureWorkBefore = grid.MeasureWorkCount;
        var viewerMeasureWorkBefore = viewer.MeasureWorkCount;

        var changed = grid.ApplySplitterRowResize(0, 1, 144f, 136f);

        Assert.True(changed);
        Assert.False(grid.NeedsMeasure);
        Assert.True(grid.NeedsArrange);
        Assert.False(viewer.NeedsMeasure);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.Equal(treeViewMeasureWorkBefore, treeView.MeasureWorkCount);
        Assert.Equal(gridMeasureWorkBefore, grid.MeasureWorkCount);
        Assert.Equal(viewerMeasureWorkBefore, viewer.MeasureWorkCount);
    }

    [Fact]
    public void SplitterRowResize_ItemsControlGeneratedChildrenWithStableDesiredSize_UsesArrangeOnly()
    {
        var root = new Panel();
        var grid = CreateItemsControlRowGridContent(out var inspectorPanel);
        var viewer = CreateCountingViewer(grid);
        root.AddChild(viewer);
        var uiRoot = new UiRoot(root);

        RunLayout(uiRoot, 640, 480, 16);

        var inspectorMeasureWorkBefore = inspectorPanel.MeasureWorkCount;
        var gridMeasureWorkBefore = grid.MeasureWorkCount;
        var viewerMeasureWorkBefore = viewer.MeasureWorkCount;

        var changed = grid.ApplySplitterRowResize(0, 1, 144f, 136f);

        Assert.True(changed);
        Assert.False(grid.NeedsMeasure);
        Assert.True(grid.NeedsArrange);
        Assert.False(viewer.NeedsMeasure);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.Equal(inspectorMeasureWorkBefore, inspectorPanel.MeasureWorkCount);
        Assert.Equal(gridMeasureWorkBefore, grid.MeasureWorkCount);
        Assert.Equal(viewerMeasureWorkBefore, viewer.MeasureWorkCount);
    }

    [Fact]
    public void SplitterColumnResize_WrappedTextInsideScrollViewer_ReconcilesDesiredSizeWithoutViewerMeasureRebubble()
    {
        var (uiRoot, viewer, grid) = CreateViewerFixture(CreateWrappedTextGridContent());
        RunLayout(uiRoot, 640, 480, 16);

        Assert.False(grid.NeedsMeasure);
        Assert.False(viewer.NeedsMeasure);
        var desiredHeightBefore = grid.DesiredSize.Y;

        var changed = grid.ApplySplitterColumnResize(0, 1, 60f, 240f);

        Assert.True(changed);
        Assert.False(grid.NeedsMeasure);
        Assert.False(viewer.NeedsMeasure);
        Assert.True(grid.DesiredSize.Y > desiredHeightBefore);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.True(grid.DesiredSize.Y > desiredHeightBefore);
    }

    [Fact]
    public void SplitterColumnResize_WrappedItemsControlText_RemeasuresWhenDesiredSizeChanges()
    {
        var (uiRoot, viewer, grid) = CreateViewerFixture(CreateWrappedItemsControlGridContent());
        RunLayout(uiRoot, 640, 480, 16);

        Assert.False(grid.NeedsMeasure);
        Assert.False(viewer.NeedsMeasure);
        var desiredHeightBefore = grid.DesiredSize.Y;

        var changed = grid.ApplySplitterColumnResize(0, 1, 60f, 240f);

        Assert.True(changed);
        Assert.True(grid.NeedsMeasure, grid.GetGridSnapshotForDiagnostics().ToString());
        Assert.True(viewer.NeedsMeasure);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.True(grid.DesiredSize.Y > desiredHeightBefore);
    }

    [Fact]
    public void SplitterColumnResize_RearrangesDirtyGridAndRerunsStableAncestorArrangeOverrides()
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

        Assert.Equal(viewerArrangeBefore + 1, viewer.ArrangeOverrideCount);
        Assert.Equal(stackArrangeBefore + 1, contentStack.ArrangeOverrideCount);
        Assert.Equal(borderArrangeBefore + 1, contentBorder.ArrangeOverrideCount);
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
        Assert.Equal(pinnedArrangeBefore + 1, pinnedBadge.ArrangeOverrideCount);
        Assert.True(pinnedBadge.LayoutSlot.X < pinnedXBefore);
    }

    [Fact]
    public void SplitterDragSequence_KeepsWorkbenchOnCheapLayoutPath()
    {
        var shellGrid = new Grid();
        shellGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(196f, GridUnitType.Pixel), MinWidth = 96f });
        shellGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(404f, GridUnitType.Pixel), MinWidth = 220f });
        shellGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var navigationPane = new Border
        {
            Width = 180f,
            Height = 360f
        };
        shellGrid.AddChild(navigationPane);

        var workbenchViewer = new CountingScrollViewer
        {
            Width = 404f,
            Height = 260f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = CreateWorkbenchContent(out var headerBadge, out var wrappedSummary, out var wrappedHint, out var wrappedLeft, out var wrappedRight)
        };
        Grid.SetColumn(workbenchViewer, 1);
        shellGrid.AddChild(workbenchViewer);

        var uiRoot = new UiRoot(shellGrid);
        RunLayout(uiRoot, 720, 420, 16);

        var badgeArrangeBefore = headerBadge.ArrangeOverrideCount;
        var summaryMeasureWorkBefore = wrappedSummary.MeasureWorkCount;
        var hintMeasureWorkBefore = wrappedHint.MeasureWorkCount;
        var leftMeasureWorkBefore = wrappedLeft.MeasureWorkCount;
        var rightMeasureWorkBefore = wrappedRight.MeasureWorkCount;

        var resizeSequence = new (float Navigation, float Workbench)[]
        {
            (236f, 364f),
            (128f, 472f),
            (220f, 380f),
            (144f, 456f),
            (208f, 392f)
        };

        foreach (var step in resizeSequence)
        {
            var changed = shellGrid.ApplySplitterColumnResize(0, 1, step.Navigation, step.Workbench);
            Assert.True(changed);
            RunLayout(uiRoot, 720, 420, 32);
        }

        var summaryMeasureWork = wrappedSummary.MeasureWorkCount - summaryMeasureWorkBefore;
        var hintMeasureWork = wrappedHint.MeasureWorkCount - hintMeasureWorkBefore;
        var leftMeasureWork = wrappedLeft.MeasureWorkCount - leftMeasureWorkBefore;
        var rightMeasureWork = wrappedRight.MeasureWorkCount - rightMeasureWorkBefore;

        Assert.Equal(badgeArrangeBefore, headerBadge.ArrangeOverrideCount);
        Assert.InRange(summaryMeasureWork, 0, resizeSequence.Length - 1);
        Assert.InRange(hintMeasureWork, 0, resizeSequence.Length - 1);
        Assert.InRange(leftMeasureWork, 0, resizeSequence.Length - 1);
        Assert.InRange(rightMeasureWork, 0, resizeSequence.Length - 1);
    }

    [Fact]
    public void SplitterColumnResize_StarAndPixelPair_ReexpandsFlexibleColumnWhenAvailableWidthGrows()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star, MinWidth = 160f });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320f, GridUnitType.Pixel), MinWidth = 220f });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var left = new Border();
        var right = new Border();
        Grid.SetColumn(right, 1);
        grid.AddChild(left);
        grid.AddChild(right);

        var uiRoot = new UiRoot(grid);
        RunLayout(uiRoot, 1280, 820, 16);

        Assert.True(grid.ApplySplitterColumnResize(0, 1, 900f, 380f));
        RunLayout(uiRoot, 1280, 820, 32);

        var flexibleWidthBeforeGrowth = grid.ColumnDefinitions[0].ActualWidth;
        var pinnedRightBeforeGrowth = right.LayoutSlot.X + right.LayoutSlot.Width;

        RunLayout(uiRoot, 1600, 820, 48);

        Assert.True(grid.ColumnDefinitions[0].Width.IsStar);
        Assert.True(grid.ColumnDefinitions[1].Width.IsPixel);
        Assert.True(grid.ColumnDefinitions[0].ActualWidth > flexibleWidthBeforeGrowth + 100f);
        Assert.Equal(1600f, right.LayoutSlot.X + right.LayoutSlot.Width, 0.01f);
    }

    [Fact]
    public void SplitterRowResize_StarAndPixelPair_ReanchorsTrailingRowWhenAvailableHeightGrows()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star, MinHeight = 160f });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(220f, GridUnitType.Pixel), MinHeight = 120f });

        var top = new Border();
        var bottom = new Border();
        Grid.SetRow(bottom, 1);
        grid.AddChild(top);
        grid.AddChild(bottom);

        var uiRoot = new UiRoot(grid);
        RunLayout(uiRoot, 1280, 820, 16);

        Assert.True(grid.ApplySplitterRowResize(0, 1, 520f, 300f));
        RunLayout(uiRoot, 1280, 820, 32);

        var topHeightBeforeGrowth = grid.RowDefinitions[0].ActualHeight;
        var bottomEdgeBeforeGrowth = bottom.LayoutSlot.Y + bottom.LayoutSlot.Height;

        RunLayout(uiRoot, 1280, 1000, 48);

        Assert.True(grid.RowDefinitions[0].Height.IsStar);
        Assert.True(grid.RowDefinitions[1].Height.IsPixel);
        Assert.True(grid.RowDefinitions[0].ActualHeight > topHeightBeforeGrowth + 100f);
        Assert.Equal(1000f, bottom.LayoutSlot.Y + bottom.LayoutSlot.Height, 0.01f);
        Assert.True(bottom.LayoutSlot.Y + bottom.LayoutSlot.Height > bottomEdgeBeforeGrowth + 100f);
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

        Assert.False(host.NeedsMeasure);
        Assert.False(grid.NeedsMeasure);

        RunLayout(uiRoot, 640, 480, 32);

        Assert.Equal(hostMeasureBefore, host.MeasureOverrideCount);
        Assert.Equal(gridMeasureBefore, grid.MeasureCallCount);
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

    private static Grid CreateStableNoWrapRowGridContent()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140f, GridUnitType.Pixel), MinHeight = 80f });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140f, GridUnitType.Pixel), MinHeight = 80f });

        var top = new TextBlock
        {
            Text = "Inspector",
            TextWrapping = TextWrapping.NoWrap,
            Height = 32f
        };
        var bottom = new TextBlock
        {
            Text = "Workbench",
            TextWrapping = TextWrapping.NoWrap,
            Height = 32f
        };

        Grid.SetRow(bottom, 1);
        grid.AddChild(top);
        grid.AddChild(bottom);
        return grid;
    }

    private static Grid CreateNoTemplateRootControlRowGridContent(out ComboBox picker)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140f, GridUnitType.Pixel), MinHeight = 80f });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140f, GridUnitType.Pixel), MinHeight = 80f });

        var top = new Border
        {
            Height = 32f
        };

        var bottomStack = new StackPanel();
        picker = new ComboBox
        {
            SelectedItem = "Root template"
        };
        bottomStack.AddChild(picker);

        Grid.SetRow(bottomStack, 1);
        grid.AddChild(top);
        grid.AddChild(bottomStack);
        return grid;
    }

    private static Grid CreateCollapsedAutoRowNoTemplateRootControlGridContent(out TextBox pathTextBox)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140f, GridUnitType.Pixel), MinHeight = 80f });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140f, GridUnitType.Pixel), MinHeight = 80f });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var top = new Border
        {
            Height = 32f
        };
        var bottom = new Border
        {
            Height = 32f
        };
        pathTextBox = new TextBox
        {
            Name = "DocumentPathTextBox",
            Text = "C:\\Work\\Document.inkk",
            Visibility = Visibility.Collapsed
        };

        Grid.SetRow(bottom, 1);
        Grid.SetRow(pathTextBox, 2);
        grid.AddChild(top);
        grid.AddChild(bottom);
        grid.AddChild(pathTextBox);
        return grid;
    }

    private static Grid CreateTreeViewFallbackScrollHostRowGridContent(out TreeView projectExplorerTree)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140f, GridUnitType.Pixel), MinHeight = 80f });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140f, GridUnitType.Pixel), MinHeight = 80f });

        var top = new Border
        {
            Height = 32f
        };
        projectExplorerTree = new TreeView
        {
            Name = "ProjectExplorerTree",
            BorderThickness = 0f,
            Padding = new Thickness(0f)
        };
        projectExplorerTree.Items.Add(new TreeViewItem { Header = "Project" });

        Grid.SetRow(projectExplorerTree, 1);
        grid.AddChild(top);
        grid.AddChild(projectExplorerTree);
        return grid;
    }

    private static Grid CreateItemsControlRowGridContent(out ItemsControl inspectorPanel)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140f, GridUnitType.Pixel), MinHeight = 80f });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140f, GridUnitType.Pixel), MinHeight = 80f });

        var top = new Border
        {
            Height = 32f
        };
        inspectorPanel = new ItemsControl
        {
            Name = "InspectorPanel"
        };
        inspectorPanel.Items.Add(new StableMeasureReuseElement(96f, 24f));
        inspectorPanel.Items.Add(new StableMeasureReuseElement(128f, 24f));

        Grid.SetRow(inspectorPanel, 1);
        grid.AddChild(top);
        grid.AddChild(inspectorPanel);
        return grid;
    }

    private static Grid CreateWrappedItemsControlGridContent()
    {
        var grid = CreateBaseGrid();
        var left = new ItemsControl();
        left.Items.Add(new TextBlock
        {
            Text = "The inspector item wraps when the first column narrows, which changes the generated item host desired height.",
            TextWrapping = TextWrapping.Wrap
        });
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

    private static FrameworkElement CreateWorkbenchContent(
        out CountingBorder headerBadge,
        out TextBlock wrappedSummary,
        out TextBlock wrappedHint,
        out TextBlock wrappedLeft,
        out TextBlock wrappedRight)
    {
        var stack = new StackPanel();

        var headerGrid = new CountingGrid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerTextHost = new StackPanel();
        var title = new TextBlock
        {
            Text = "Canvas workspace",
            TextWrapping = TextWrapping.Wrap
        };
        wrappedSummary = new TextBlock
        {
            Text = "This center lane behaves like a WPF editor canvas: it absorbs most width, but still yields to splitter drags and keyboard nudges.",
            TextWrapping = TextWrapping.Wrap
        };
        headerTextHost.AddChild(title);
        headerTextHost.AddChild(wrappedSummary);
        headerGrid.AddChild(headerTextHost);

        headerBadge = new CountingBorder
        {
            Width = 132f,
            Height = 28f
        };
        Grid.SetColumn(headerBadge, 1);
        headerGrid.AddChild(headerBadge);
        stack.AddChild(headerGrid);

        wrappedHint = new TextBlock
        {
            Text = "Try dragging either rail, then click it and use arrow keys. Pane minimums prevent the shell from collapsing through the splitter.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0f, 12f, 0f, 12f)
        };
        stack.AddChild(wrappedHint);

        var lowerGrid = new CountingGrid();
        lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        lowerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var leftPanel = new StackPanel();
        leftPanel.AddChild(new TextBlock
        {
            Text = "Document tab strip",
            TextWrapping = TextWrapping.Wrap
        });
        wrappedLeft = new TextBlock
        {
            Text = "Toolbar, tabs, or canvases usually live in the expandable center track.",
            TextWrapping = TextWrapping.Wrap
        };
        leftPanel.AddChild(wrappedLeft);

        var rightPanel = new StackPanel();
        rightPanel.AddChild(new TextBlock
        {
            Text = "Viewport guide",
            TextWrapping = TextWrapping.Wrap
        });
        wrappedRight = new TextBlock
        {
            Text = "Actual column widths stay visible in the side rail so this pane can act as a quick reference while the shell shifts.",
            TextWrapping = TextWrapping.Wrap
        };
        rightPanel.AddChild(wrappedRight);

        lowerGrid.AddChild(leftPanel);
        Grid.SetColumn(rightPanel, 1);
        lowerGrid.AddChild(rightPanel);
        stack.AddChild(lowerGrid);

        return stack;
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

    private sealed class CountingGrid : Grid
    {
        public int MeasureOverrideCount { get; private set; }
        public new int ArrangeCallCount { get; private set; }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            MeasureOverrideCount++;
            return base.MeasureOverride(availableSize);
        }

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

    private sealed class StableMeasureReuseElement : FrameworkElement
    {
        private readonly Vector2 _desiredSize;

        public StableMeasureReuseElement(float desiredWidth, float desiredHeight)
        {
            _desiredSize = new Vector2(desiredWidth, desiredHeight);
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return new Vector2(ResolveDesiredAxis(_desiredSize.X, availableSize.X), ResolveDesiredAxis(_desiredSize.Y, availableSize.Y));
        }

        protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
        {
            return FitsDesiredSize(previousAvailableSize) && FitsDesiredSize(nextAvailableSize);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            return finalSize;
        }

        private bool FitsDesiredSize(Vector2 availableSize)
        {
            return FitsDesiredAxis(_desiredSize.X, availableSize.X) &&
                   FitsDesiredAxis(_desiredSize.Y, availableSize.Y);
        }

        private static bool FitsDesiredAxis(float desired, float available)
        {
            return !float.IsFinite(available) || desired <= MathF.Max(0f, available) + 0.01f;
        }

        private static float ResolveDesiredAxis(float desired, float available)
        {
            return float.IsFinite(available) ? MathF.Min(desired, MathF.Max(0f, available)) : desired;
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
