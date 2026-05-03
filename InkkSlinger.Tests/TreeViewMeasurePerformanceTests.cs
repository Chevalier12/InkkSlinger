using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

/// <summary>
/// Performance regression test: the ProjectExplorerTree (a plain TreeView without a template)
/// takes ~818ms to measure because all measure-reuse attempts are rejected when HasTemplateRoot
/// is false. This test asserts that a realistic project-style TreeView layout completes
/// within a 60fps frame budget (16.6ms).
/// 
/// Current failure: ~818ms vs 16.6ms budget.
/// Root cause: CanReuseMeasureNoTemplateRootRejectedCount > 0 because HasTemplateRoot=false.
/// </summary>
public sealed class TreeViewMeasurePerformanceTests
{
    [Fact]
    public void TreeView_EagerExpandedItems_MaterializationCostIsVisibleBeforeLayout()
    {
        const int folderCount = 2078;
        const int fileCount = 15329;
        var stopwatch = Stopwatch.StartNew();
        var root = CreateExpandedProjectTree(folderCount, fileCount);
        stopwatch.Stop();

        var buildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        var totalItems = CountTreeItems(root);

        Assert.Equal(folderCount + fileCount + 1, totalItems);
        Assert.True(
            buildMilliseconds < 2500d,
            $"Building an eager TreeViewItem hierarchy should not be slower than WPF's same eager-control path by multiple seconds. " +
            $"buildMilliseconds={buildMilliseconds:0.###}, totalItems={totalItems}.");
    }

    [Fact]
    public void TreeView_HierarchicalDataSource_RealizesOnlyViewportContainers()
    {
        const int folderCount = 2078;
        const int fileCount = 15329;
        var root = CreateProjectNodeTree(folderCount, fileCount);
        var treeView = new TreeView
        {
            Width = 800f,
            Height = 420f,
            HierarchicalChildrenSelector = static item => item is ProjectNode node ? node.Children : Array.Empty<ProjectNode>(),
            HierarchicalHeaderSelector = static item => item is ProjectNode node ? node.Name : string.Empty,
            HierarchicalExpandedSelector = static item => item is ProjectNode { IsFolder: true },
            HierarchicalItemsSource = new[] { root }
        };

        treeView.Measure(new Vector2(800f, 420f));
        treeView.Arrange(new LayoutRect(0f, 0f, 800f, 420f));

        Assert.Equal(folderCount + fileCount + 1, CountProjectNodes(root));
        Assert.True(
            treeView.RealizedHierarchicalContainerCount < 200,
            $"Hierarchical TreeView data virtualization should realize only the viewport/cache rows, not every model node. " +
            $"realized={treeView.RealizedHierarchicalContainerCount}, modelNodes={CountProjectNodes(root)}.");
    }

    [Fact]
    public void TreeView_HierarchicalHasChildrenSelector_DoesNotEnumerateCollapsedChildren()
    {
        var root = new LazyProjectNode("Root", isFolder: true);
        for (var folderIndex = 0; folderIndex < 100; folderIndex++)
        {
            root.Children.Add(new LazyProjectNode($"Folder {folderIndex:000}", isFolder: true));
        }

        var childSelectorCallCount = 0;
        var treeView = new TreeView
        {
            Width = 800f,
            Height = 420f,
            HierarchicalChildrenSelector = item =>
            {
                childSelectorCallCount++;
                return item is LazyProjectNode node ? node.Children : Array.Empty<LazyProjectNode>();
            },
            HierarchicalHasChildrenSelector = static item => item is LazyProjectNode { IsFolder: true },
            HierarchicalHeaderSelector = static item => item is LazyProjectNode node ? node.Name : string.Empty,
            HierarchicalExpandedSelector = item => ReferenceEquals(item, root),
            HierarchicalItemsSource = new[] { root }
        };

        treeView.Measure(new Vector2(800f, 420f));
        treeView.Arrange(new LayoutRect(0f, 0f, 800f, 420f));

        Assert.Equal(1, childSelectorCallCount);
        Assert.True(treeView.RealizedHierarchicalContainerCount < 150);
    }

    [Fact]
    public void DesignerProjectExplorerTree_WheelScroll_ShouldStayWithinFrameBudget()
    {
        var store = new FakeProjectFileStore();
        const string projectRoot = "C:/projects/PerfTree";
        PopulateProjectTree(store, projectRoot);

        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl />", store);
        var projectSession = InkkSlinger.Designer.DesignerProjectSession.Open(projectRoot, store);
        var shell = new InkkSlinger.Designer.DesignerShellView(
            documentController: documentController,
            projectSession: projectSession);

        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, width: 1280, height: 820);
        RunLayout(uiRoot, width: 1280, height: 820);

        var projectExplorerTree = Assert.IsType<TreeView>(shell.FindName("ProjectExplorerTree"));
        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(projectExplorerTree.GetVisualChildren()));
        Assert.True(
            scrollViewer.ExtentHeight > scrollViewer.ViewportHeight + 0.01f,
            $"Expected project explorer to overflow vertically, but extent={scrollViewer.ExtentHeight:0.###} viewport={scrollViewer.ViewportHeight:0.###}.");

        var pointer = GetCenter(projectExplorerTree.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, width: 1280, height: 820);

        var beforeControl = projectExplorerTree.GetControlSnapshotForDiagnostics();
        var beforeFramework = projectExplorerTree.GetFrameworkElementSnapshotForDiagnostics();
        _ = Control.GetTelemetryAndReset();
        uiRoot.GetTelemetryAndReset();

        const int wheelTicks = 12;
        for (var i = 0; i < wheelTicks; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, width: 1280, height: 820);
        }

        var afterControl = projectExplorerTree.GetControlSnapshotForDiagnostics();
        var afterFramework = projectExplorerTree.GetFrameworkElementSnapshotForDiagnostics();
        var controlTelemetry = Control.GetTelemetryAndReset();
        var uiRootTelemetry = uiRoot.GetUiRootTelemetrySnapshot();

        var averageUpdateMs = uiRootTelemetry.UpdateElapsedMs / wheelTicks;
        var frameBudgetMs = 16.6;
        var visualChildrenDelta = afterControl.GetVisualChildrenCallCount - beforeControl.GetVisualChildrenCallCount;
        var traversalCountDelta = afterControl.GetVisualChildCountForTraversalCallCount - beforeControl.GetVisualChildCountForTraversalCallCount;
        var treeMeasureCallDelta = afterFramework.MeasureCallCount - beforeFramework.MeasureCallCount;
        var treeArrangeCallDelta = afterFramework.ArrangeCallCount - beforeFramework.ArrangeCallCount;

        Assert.True(scrollViewer.VerticalOffset > 0f, $"Expected project explorer to scroll, but offset stayed {scrollViewer.VerticalOffset:0.###}.");
        Assert.True(
            averageUpdateMs <= frameBudgetMs,
            $"Designer project explorer average wheel frame cost {averageUpdateMs:0.###}ms exceeds {frameBudgetMs}ms 60fps budget. " +
            $"wheelTicks={wheelTicks}, verticalOffset={scrollViewer.VerticalOffset:0.###}, " +
            $"treeVisualChildrenDelta={visualChildrenDelta}, treeTraversalCountDelta={traversalCountDelta}, " +
            $"treeMeasureCallDelta={treeMeasureCallDelta}, treeArrangeCallDelta={treeArrangeCallDelta}, " +
            $"aggregateControlVisualChildren={controlTelemetry.GetVisualChildrenCallCount}.");
    }

    [Fact]
    public void DesignerProjectExplorerTree_ScrollbarThumbDrag_ShouldNotRemeasureTreePerDragFrame()
    {
        var store = new FakeProjectFileStore();
        const string projectRoot = "C:/projects/PerfTree";
        PopulateProjectTree(store, projectRoot, sectionCount: 75, filesPerSection: 25, longFileNames: true);

        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl />", store);
        var projectSession = InkkSlinger.Designer.DesignerProjectSession.Open(projectRoot, store);
        var shell = new InkkSlinger.Designer.DesignerShellView(
            documentController: documentController,
            projectSession: projectSession);

        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, width: 1280, height: 820);
        RunLayout(uiRoot, width: 1280, height: 820);

        var projectExplorerTree = Assert.IsType<TreeView>(shell.FindName("ProjectExplorerTree"));
        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(projectExplorerTree.GetVisualChildren()));
        var verticalBar = GetPrivateScrollBar(scrollViewer, "_verticalBar");
        var track = Assert.IsType<Track>(FindNamedVisualChild<Track>(verticalBar, "PART_Track"));
        var thumb = Assert.IsType<Thumb>(FindNamedVisualChild<Thumb>(verticalBar, "PART_Thumb"));

        Assert.True(
            scrollViewer.ExtentHeight > scrollViewer.ViewportHeight + 0.01f,
            $"Expected project explorer to overflow vertically, but extent={scrollViewer.ExtentHeight:0.###} viewport={scrollViewer.ViewportHeight:0.###}.");
        Assert.True(verticalBar.LayoutSlot.Height > 0f, "Expected the Project Explorer vertical scrollbar to be arranged.");
        Assert.True(thumb.LayoutSlot.Height > 0f, "Expected the Project Explorer scrollbar thumb to be arranged.");

        var thumbRect = verticalBar.GetThumbRectForInput();
        var start = GetCenter(thumbRect);
        var maxThumbCenterY = track.LayoutSlot.Y + track.LayoutSlot.Height - (thumbRect.Height / 2f) - 1f;
        var dragDistance = MathF.Max(0f, maxThumbCenterY - start.Y);
        Assert.True(dragDistance > 100f, $"Expected enough scrollbar travel for a meaningful drag, but travel={dragDistance:0.###}.");

        var beforeTree = projectExplorerTree.GetFrameworkElementSnapshotForDiagnostics();
        var beforeViewer = scrollViewer.GetScrollViewerSnapshotForDiagnostics();
        var beforeBar = verticalBar.GetScrollBarSnapshotForDiagnostics();
        var headersBeforeDrag = GetVisibleTreeRowHeaders(scrollViewer);

        _ = FrameworkElement.GetTelemetryAndReset();
        _ = Control.GetTelemetryAndReset();
        _ = ScrollViewer.GetTelemetryAndReset();
        _ = ScrollBar.GetTelemetryAndReset();
        _ = Track.GetTelemetryAndReset();
        uiRoot.GetTelemetryAndReset();

        const int dragFrames = 60;
        var previous = start;
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(previous, previous, leftPressed: true));
        RunLayout(uiRoot, width: 1280, height: 820);

        for (var frame = 1; frame <= dragFrames; frame++)
        {
            var current = new Vector2(start.X, start.Y + (dragDistance * frame / dragFrames));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(previous, current, pointerMoved: true));
            RunLayout(uiRoot, width: 1280, height: 820);
            previous = current;
        }

        var headersWhileHeld = GetVisibleTreeRowHeaders(scrollViewer);
        var renderedHeadersWhileHeld = EnumerateVisualDescendants<TreeViewItem>(scrollViewer)
            .Where(static row => row.HasVirtualizedDisplaySnapshotForDiagnostics)
            .Select(static row => (Full: row.DisplayHeaderForDiagnostics, Rendered: row.RenderedHeaderForDiagnostics))
            .Where(static row => row.Full.Length > row.Rendered.Length)
            .ToArray();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(previous, previous, leftReleased: true));
        RunLayout(uiRoot, width: 1280, height: 820);

        var afterTree = projectExplorerTree.GetFrameworkElementSnapshotForDiagnostics();
        var afterViewer = scrollViewer.GetScrollViewerSnapshotForDiagnostics();
        var afterBar = verticalBar.GetScrollBarSnapshotForDiagnostics();
        var frameworkTelemetry = FrameworkElement.GetTelemetryAndReset();
        var controlTelemetry = Control.GetTelemetryAndReset();
        var uiRootTelemetry = uiRoot.GetUiRootTelemetrySnapshot();

        var treeMeasureDelta = afterTree.MeasureCallCount - beforeTree.MeasureCallCount;
        var treeMeasureWorkDelta = afterTree.MeasureWorkCount - beforeTree.MeasureWorkCount;
        var treeMeasureMillisecondsDelta = afterTree.MeasureMilliseconds - beforeTree.MeasureMilliseconds;
        var treeMeasureInvalidatedDuringMeasureDelta =
            afterTree.MeasureInvalidatedDuringMeasureCount - beforeTree.MeasureInvalidatedDuringMeasureCount;
        var treeInvalidateMeasureDelta = afterTree.InvalidateMeasureCallCount - beforeTree.InvalidateMeasureCallCount;
        var treeArrangeDelta = afterTree.ArrangeCallCount - beforeTree.ArrangeCallCount;
        var treeArrangeMillisecondsDelta = afterTree.ArrangeMilliseconds - beforeTree.ArrangeMilliseconds;
        var viewerVerticalScrollBarSetOffsetsDelta =
            afterViewer.SetOffsetsVerticalScrollBarSourceCount - beforeViewer.SetOffsetsVerticalScrollBarSourceCount;
        var viewerDeferredDelta = afterViewer.SetOffsetsDeferredLayoutPathCount - beforeViewer.SetOffsetsDeferredLayoutPathCount;
        var viewerMeasurePathDelta =
            afterViewer.SetOffsetsVirtualizingMeasureInvalidationPathCount - beforeViewer.SetOffsetsVirtualizingMeasureInvalidationPathCount;
        var viewerArrangeOnlyPathDelta =
            afterViewer.SetOffsetsVirtualizingArrangeOnlyPathCount - beforeViewer.SetOffsetsVirtualizingArrangeOnlyPathCount;
        var barDragDelta = afterBar.OnThumbDragDeltaCallCount - beforeBar.OnThumbDragDeltaCallCount;
        var averageUpdateMs = uiRootTelemetry.UpdateElapsedMs / dragFrames;

        Assert.True(scrollViewer.VerticalOffset > 0f, $"Expected thumb drag to scroll Project Explorer, but offset stayed {scrollViewer.VerticalOffset:0.###}.");
        Assert.NotEmpty(headersBeforeDrag);
        Assert.NotEmpty(headersWhileHeld);
        Assert.NotEqual(headersBeforeDrag[0], headersWhileHeld[0]);
        Assert.Contains(
            renderedHeadersWhileHeld,
            static row => row.Rendered.EndsWith("...", StringComparison.Ordinal) &&
                          !string.Equals(row.Full, row.Rendered, StringComparison.Ordinal));
        Assert.True(viewerVerticalScrollBarSetOffsetsDelta > 0, "Expected the repro to exercise ScrollViewer's VerticalScrollBar SetOffsets path.");
        Assert.True(barDragDelta > 0, "Expected the repro to exercise ScrollBar thumb drag deltas.");
        Assert.True(
            treeMeasureDelta <= 12,
            $"Dragging the Project Explorer scrollbar thumb should not remeasure the TreeView every drag frame. " +
            $"treeMeasureDelta={treeMeasureDelta}, treeMeasureWorkDelta={treeMeasureWorkDelta}, treeMeasureMsDelta={treeMeasureMillisecondsDelta:0.###}, " +
            $"treeInvalidateMeasureDelta={treeInvalidateMeasureDelta}, treeMeasureInvalidatedDuringMeasureDelta={treeMeasureInvalidatedDuringMeasureDelta}, " +
            $"dragFrames={dragFrames}, verticalOffset={scrollViewer.VerticalOffset:0.###}, " +
            $"scrollbarSetOffsets={viewerVerticalScrollBarSetOffsetsDelta}, scrollBarDragDeltas={barDragDelta}, " +
            $"viewerDeferred={viewerDeferredDelta}, viewerMeasurePath={viewerMeasurePathDelta}, viewerArrangeOnlyPath={viewerArrangeOnlyPathDelta}.");
        Assert.True(
            treeInvalidateMeasureDelta <= 12,
            $"Dragging the Project Explorer scrollbar thumb should not keep invalidating TreeView measure. " +
            $"treeInvalidateMeasureDelta={treeInvalidateMeasureDelta}, treeMeasureDelta={treeMeasureDelta}, " +
            $"treeMeasureInvalidatedDuringMeasureDelta={treeMeasureInvalidatedDuringMeasureDelta}, dragFrames={dragFrames}.");
        Assert.True(
            treeMeasureMillisecondsDelta <= 100d,
            $"Dragging the Project Explorer scrollbar thumb should keep TreeView measure cost bounded. " +
            $"treeMeasureDelta={treeMeasureDelta}, treeMeasureWorkDelta={treeMeasureWorkDelta}, treeMeasureMsDelta={treeMeasureMillisecondsDelta:0.###}, treeArrangeDelta={treeArrangeDelta}, " +
            $"treeInvalidateMeasureDelta={treeInvalidateMeasureDelta}, treeMeasureInvalidatedDuringMeasureDelta={treeMeasureInvalidatedDuringMeasureDelta}, " +
            $"dragFrames={dragFrames}, verticalOffset={scrollViewer.VerticalOffset:0.###}, " +
            $"scrollbarSetOffsets={viewerVerticalScrollBarSetOffsetsDelta}, scrollBarDragDeltas={barDragDelta}, " +
            $"viewerDeferred={viewerDeferredDelta}, viewerMeasurePath={viewerMeasurePathDelta}, viewerArrangeOnlyPath={viewerArrangeOnlyPathDelta}, " +
            $"averageUpdateMs={averageUpdateMs:0.###}, aggregateFrameworkMeasure={frameworkTelemetry.MeasureCallCount}, " +
            $"aggregateFrameworkMeasureMs={frameworkTelemetry.MeasureMilliseconds:0.###}, " +
            $"aggregateControlDpChanges={controlTelemetry.DependencyPropertyChangedCallCount}.");
        Assert.True(
            treeArrangeMillisecondsDelta <= 100d,
            $"Dragging the Project Explorer scrollbar thumb should keep TreeView arrange cost bounded even across a large project extent. " +
            $"treeArrangeDelta={treeArrangeDelta}, treeArrangeMsDelta={treeArrangeMillisecondsDelta:0.###}, " +
            $"treeMeasureDelta={treeMeasureDelta}, treeMeasureMsDelta={treeMeasureMillisecondsDelta:0.###}, " +
            $"treeInvalidateMeasureDelta={treeInvalidateMeasureDelta}, treeMeasureInvalidatedDuringMeasureDelta={treeMeasureInvalidatedDuringMeasureDelta}, " +
            $"dragFrames={dragFrames}, verticalOffset={scrollViewer.VerticalOffset:0.###}, extent={scrollViewer.ExtentHeight:0.###}, viewport={scrollViewer.ViewportHeight:0.###}, " +
            $"scrollbarSetOffsets={viewerVerticalScrollBarSetOffsetsDelta}, scrollBarDragDeltas={barDragDelta}, " +
            $"viewerDeferred={viewerDeferredDelta}, viewerMeasurePath={viewerMeasurePathDelta}, viewerArrangeOnlyPath={viewerArrangeOnlyPathDelta}, " +
            $"averageUpdateMs={averageUpdateMs:0.###}, aggregateFrameworkArrange={frameworkTelemetry.ArrangeCallCount}, " +
            $"aggregateFrameworkArrangeMs={frameworkTelemetry.ArrangeMilliseconds:0.###}, " +
            $"aggregateControlDpChanges={controlTelemetry.DependencyPropertyChangedCallCount}.");
    }

    [Fact]
    public void DesignerProjectExplorerTree_ThumbDragBackToTop_ShouldReplaceSnapshotRowsWithCurrentRange()
    {
        var store = new FakeProjectFileStore();
        const string projectRoot = "C:/projects/PerfTree";
        PopulateProjectTree(store, projectRoot, sectionCount: 75, filesPerSection: 25, longFileNames: true);

        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl />", store);
        var projectSession = InkkSlinger.Designer.DesignerProjectSession.Open(projectRoot, store);
        var shell = new InkkSlinger.Designer.DesignerShellView(
            documentController: documentController,
            projectSession: projectSession);

        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, width: 1280, height: 820);
        RunLayout(uiRoot, width: 1280, height: 820);

        var projectExplorerTree = Assert.IsType<TreeView>(shell.FindName("ProjectExplorerTree"));
        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(projectExplorerTree.GetVisualChildren()));
        var verticalBar = GetPrivateScrollBar(scrollViewer, "_verticalBar");
        var track = Assert.IsType<Track>(FindNamedVisualChild<Track>(verticalBar, "PART_Track"));
        var thumb = Assert.IsType<Thumb>(FindNamedVisualChild<Thumb>(verticalBar, "PART_Thumb"));

        var initialRows = EnumerateVisualDescendants<TreeViewItem>(scrollViewer).ToArray();
        Assert.NotEmpty(initialRows);
        Assert.True(CountRowsIntersecting(initialRows, projectExplorerTree.LayoutSlot) > 0);

        var thumbRect = verticalBar.GetThumbRectForInput();
        var start = GetCenter(thumbRect);
        var maxThumbCenterY = track.LayoutSlot.Y + track.LayoutSlot.Height - (thumbRect.Height / 2f) - 1f;
        var dragDistance = MathF.Max(0f, maxThumbCenterY - start.Y);
        Assert.True(dragDistance > 100f, $"Expected enough scrollbar travel for a meaningful drag, but travel={dragDistance:0.###}.");

        const int dragFrames = 16;
        var previous = start;
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(previous, previous, leftPressed: true));
        RunLayout(uiRoot, width: 1280, height: 820);

        for (var frame = 1; frame <= dragFrames; frame++)
        {
            var current = new Vector2(start.X, start.Y + (dragDistance * frame / dragFrames));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(previous, current, pointerMoved: true));
            RunLayout(uiRoot, width: 1280, height: 820);
            previous = current;
        }

        var farRowsWhileHeld = EnumerateVisualDescendants<TreeViewItem>(scrollViewer).ToArray();
        Assert.True(scrollViewer.VerticalOffset > scrollViewer.ViewportHeight);
        Assert.Contains(farRowsWhileHeld, static row => row.HasVirtualizedDisplaySnapshotForDiagnostics);

        var backStart = previous;
        var previousBack = backStart;
        for (var frame = 1; frame <= dragFrames; frame++)
        {
            var current = new Vector2(start.X, backStart.Y + ((start.Y - backStart.Y) * frame / dragFrames));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(previousBack, current, pointerMoved: true));
            RunLayout(uiRoot, width: 1280, height: 820);
            previousBack = current;
        }

        Assert.True(scrollViewer.VerticalOffset <= 1f, $"Expected thumb drag back to the top to return offset near 0, but offset={scrollViewer.VerticalOffset:0.###}.");
        var rowsWhileHeldAtTop = EnumerateVisualDescendants<TreeViewItem>(scrollViewer).ToArray();
        Assert.DoesNotContain(rowsWhileHeldAtTop, static row => row.HasVirtualizedDisplaySnapshotForDiagnostics);
        Assert.True(
            CountRowsIntersecting(rowsWhileHeldAtTop, projectExplorerTree.LayoutSlot) > 0,
            $"Expected current top rows to intersect the Project Explorer viewport while the thumb is still held. offset={scrollViewer.VerticalOffset:0.###}.");

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, start, leftReleased: true));
        RunLayout(uiRoot, width: 1280, height: 820);

        var rowsAfterRelease = EnumerateVisualDescendants<TreeViewItem>(scrollViewer).ToArray();
        Assert.DoesNotContain(rowsAfterRelease, static row => row.HasVirtualizedDisplaySnapshotForDiagnostics);
        Assert.True(
            CountRowsIntersecting(rowsAfterRelease, projectExplorerTree.LayoutSlot) > 0,
            $"Expected committed top rows to intersect the Project Explorer viewport after thumb release. offset={scrollViewer.VerticalOffset:0.###}.");
    }

    [Fact]
    public void DesignerProjectExplorerTree_TemplatedTrimmedRows_ShouldStayBoundedAndWithinFrameBudget()
    {
        var store = new FakeProjectFileStore();
        const string projectRoot = "C:/projects/PerfTree";
        PopulateProjectTree(store, projectRoot);

        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl />", store);
        var projectSession = InkkSlinger.Designer.DesignerProjectSession.Open(projectRoot, store);
        var shell = new InkkSlinger.Designer.DesignerShellView(
            documentController: documentController,
            projectSession: projectSession);

        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot, width: 1280, height: 820);
        RunLayout(uiRoot, width: 1280, height: 820);

        var projectExplorerTree = Assert.IsType<TreeView>(shell.FindName("ProjectExplorerTree"));
        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(projectExplorerTree.GetVisualChildren()));
        var realizedRowsBeforeWheel = EnumerateVisualDescendants<TreeViewItem>(scrollViewer).ToArray();

        Assert.NotEmpty(realizedRowsBeforeWheel);
        Assert.True(
            realizedRowsBeforeWheel.Length < 120,
            $"Designer project explorer should only realize the visible TreeView rows plus a small cache, not hundreds of templated rows. " +
            $"realizedRows={realizedRowsBeforeWheel.Length}, cachedContainers={projectExplorerTree.RealizedHierarchicalContainerCount}.");

        foreach (var row in realizedRowsBeforeWheel)
        {
            var snapshot = row.GetControlSnapshotForDiagnostics();
            Assert.True(snapshot.HasTemplateAssigned, $"Expected realized project rows to have a TreeViewItem template. header={row.Header}");
            Assert.True(snapshot.HasTemplateRoot, $"Expected realized project rows to expose a template root. header={row.Header}");
            Assert.Equal("TextBlock", snapshot.TemplateRootType);

            var textBlocks = EnumerateVisualDescendants<TextBlock>(row).ToArray();
            Assert.Single(textBlocks);
            Assert.Equal(TextTrimming.CharacterEllipsis, textBlocks[0].TextTrimming);
        }

        var pointer = GetCenter(projectExplorerTree.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, width: 1280, height: 820);

        _ = Control.GetTelemetryAndReset();
        uiRoot.GetTelemetryAndReset();

        const int wheelTicks = 12;
        for (var i = 0; i < wheelTicks; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, width: 1280, height: 820);
        }

        var uiRootTelemetry = uiRoot.GetUiRootTelemetrySnapshot();
        var averageUpdateMs = uiRootTelemetry.UpdateElapsedMs / wheelTicks;
        var realizedRowsAfterWheel = EnumerateVisualDescendants<TreeViewItem>(scrollViewer).ToArray();
        var realizedTextBlocksAfterWheel = EnumerateVisualDescendants<TextBlock>(scrollViewer).ToArray();

        Assert.True(scrollViewer.VerticalOffset > 0f, $"Expected project explorer to scroll, but offset stayed {scrollViewer.VerticalOffset:0.###}.");
        Assert.True(
            averageUpdateMs <= 16.6,
            $"Designer project explorer average wheel frame cost with templated trimmed rows {averageUpdateMs:0.###}ms exceeds 16.6ms. " +
            $"wheelTicks={wheelTicks}, verticalOffset={scrollViewer.VerticalOffset:0.###}, realizedRows={realizedRowsAfterWheel.Length}, textBlocks={realizedTextBlocksAfterWheel.Length}.");
        Assert.True(
            realizedRowsAfterWheel.Length < 120,
            $"Templated trimmed project rows should stay bounded to the viewport cache during scroll. " +
            $"realizedRows={realizedRowsAfterWheel.Length}, verticalOffset={scrollViewer.VerticalOffset:0.###}.");
        Assert.Equal(realizedRowsAfterWheel.Length, realizedTextBlocksAfterWheel.Length);
    }

    [Fact]
    public void TreeViewVirtualizingHost_WheelBurstWithinInitialCache_ShouldNotRealizeRowsDuringWheel()
    {
        var treeView = new TreeView
        {
            Name = "ProjectExplorerTree",
            Width = 258f,
            Height = 492f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var root = CreateTreeViewItem("Root", isExpanded: true);
        for (var i = 0; i < 200; i++)
        {
            root.Items.Add(CreateTreeViewItem($"Item {i}", isExpanded: false));
        }

        treeView.Items.Add(root);

        var host = new Canvas { Width = 500f, Height = 600f };
        host.AddChild(treeView);
        var uiRoot = new UiRoot(host);

        RunLayout(uiRoot, width: 500, height: 600);
        RunLayout(uiRoot, width: 500, height: 600);

        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(treeView.GetVisualChildren()));
        var virtualizingHost = Assert.IsAssignableFrom<VirtualizingStackPanel>(scrollViewer.Content);
        var before = virtualizingHost.GetVirtualizingStackPanelSnapshotForDiagnostics();

        Assert.True(
            before.RealizedEnd >= (before.ViewportHeight * 3f) - 1f,
            $"Initial realization should use the arranged viewport plus two cache pages. " +
            $"realizedEnd={before.RealizedEnd:0.###}, viewport={before.ViewportHeight:0.###}, " +
            $"lastContextViewport={before.LastViewportContextViewportPrimary:0.###}.");

        var pointer = GetCenter(treeView.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        RunLayout(uiRoot, width: 500, height: 600);

        const int wheelTicks = 10;
        for (var i = 0; i < wheelTicks; i++)
        {
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));
            RunLayout(uiRoot, width: 500, height: 600);
        }

        var after = virtualizingHost.GetVirtualizingStackPanelSnapshotForDiagnostics();

        Assert.True(scrollViewer.VerticalOffset > 0f, "Expected the TreeView to scroll during the wheel burst.");
        Assert.Equal(before.FirstRealizedIndex, after.FirstRealizedIndex);
        Assert.Equal(before.LastRealizedIndex, after.LastRealizedIndex);
        Assert.Equal(before.MeasureRangeCallCount, after.MeasureRangeCallCount);
        Assert.Equal(before.ArrangeRangeCallCount, after.ArrangeRangeCallCount);
        Assert.Equal(wheelTicks, after.ViewerOwnedOffsetDecisionWithinRealizedWindowCount - before.ViewerOwnedOffsetDecisionWithinRealizedWindowCount);
        Assert.Equal(before.ViewerOwnedOffsetDecisionBeforeGuardBandCount, after.ViewerOwnedOffsetDecisionBeforeGuardBandCount);
        Assert.Equal(before.ViewerOwnedOffsetDecisionAfterGuardBandCount, after.ViewerOwnedOffsetDecisionAfterGuardBandCount);
    }

    [Fact]
    public void TreeViewVirtualizingHost_AttachingLargeExpandedHierarchy_CoalescesHostInvalidations()
    {
        var treeView = new TreeView
        {
            Name = "ProjectExplorerTree",
            Width = 258f,
            Height = 492f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var root = CreateTreeViewItem("Root", isExpanded: true);
        for (var folderIndex = 0; folderIndex < 40; folderIndex++)
        {
            var folder = CreateTreeViewItem($"Folder {folderIndex:00}", isExpanded: true);
            for (var fileIndex = 0; fileIndex < 20; fileIndex++)
            {
                folder.Items.Add(CreateTreeViewItem($"File {folderIndex:00}-{fileIndex:00}.xml", isExpanded: false));
            }

            root.Items.Add(folder);
        }

        _ = FrameworkElement.GetTelemetryAndReset();
        treeView.Items.Add(root);
        var frameworkTelemetry = FrameworkElement.GetTelemetryAndReset();
        var treeSnapshot = treeView.GetFrameworkElementSnapshotForDiagnostics();

        Assert.True(
            frameworkTelemetry.InvalidateMeasureCallCount <= 8,
            $"Attaching one already-expanded TreeView root should reconcile the virtualized host as one structural update, " +
            $"not invalidate once per visible row. frameworkInvalidateMeasure={frameworkTelemetry.InvalidateMeasureCallCount}, " +
            $"treeInvalidateMeasure={treeSnapshot.InvalidateMeasureCallCount}.");
    }

    [Fact]
    public void TreeView_InheritedPropertyChange_DoesNotNotifyVisualLogicalChildrenTwice()
    {
        var treeView = new TreeView
        {
            Name = "ProjectExplorerTree",
            Width = 258f,
            Height = 492f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var root = CreateTreeViewItem("Root", isExpanded: true);
        for (var folderIndex = 0; folderIndex < 20; folderIndex++)
        {
            var folder = CreateTreeViewItem($"Folder {folderIndex:00}", isExpanded: true);
            for (var fileIndex = 0; fileIndex < 10; fileIndex++)
            {
                folder.Items.Add(CreateTreeViewItem($"File {folderIndex:00}-{fileIndex:00}.xml", isExpanded: false));
            }

            root.Items.Add(folder);
        }

        treeView.Items.Add(root);
        _ = FrameworkElement.GetTelemetryAndReset();

        treeView.IsEnabled = false;

        var frameworkTelemetry = FrameworkElement.GetTelemetryAndReset();
        Assert.True(
            frameworkTelemetry.DependencyPropertyChangedCallCount <= 260,
            $"One inherited IsEnabled change should notify each TreeView element once, not once through the visual tree and again through the logical tree. " +
            $"frameworkDpChanges={frameworkTelemetry.DependencyPropertyChangedCallCount}.");
    }

    [Fact]
    public void TreeView_ForegroundChange_PropagatesTypographyLinearly()
    {
        var treeView = new TreeView
        {
            Name = "ProjectExplorerTree",
            Width = 258f,
            Height = 492f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var root = CreateTreeViewItem("Root", isExpanded: true);
        for (var folderIndex = 0; folderIndex < 40; folderIndex++)
        {
            var folder = CreateTreeViewItem($"Folder {folderIndex:00}", isExpanded: true);
            for (var fileIndex = 0; fileIndex < 20; fileIndex++)
            {
                folder.Items.Add(CreateTreeViewItem($"File {folderIndex:00}-{fileIndex:00}.xml", isExpanded: false));
            }

            root.Items.Add(folder);
        }

        treeView.Items.Add(root);
        var allItems = EnumerateTreeItems(root).ToArray();
        var newForeground = new Color(229, 231, 234);

        _ = FrameworkElement.GetTelemetryAndReset();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        treeView.Foreground = newForeground;

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var frameworkTelemetry = FrameworkElement.GetTelemetryAndReset();

        Assert.All(allItems, item => Assert.Equal(newForeground, item.Foreground));
        Assert.True(
            allocatedBytes <= 1_500_000,
            $"TreeView foreground propagation should visit the expanded hierarchy linearly, not restart recursive child propagation from every item. " +
            $"allocatedBytes={allocatedBytes}, itemCount={allItems.Length}, frameworkDpChanges={frameworkTelemetry.DependencyPropertyChangedCallCount}.");
        Assert.True(
            frameworkTelemetry.DependencyPropertyChangedCallCount <= allItems.Length + 4,
            $"TreeView foreground propagation should change each TreeViewItem foreground once. " +
            $"itemCount={allItems.Length}, frameworkDpChanges={frameworkTelemetry.DependencyPropertyChangedCallCount}.");
    }

    [Fact]
    public void TreeViewVirtualizingHost_TransformWheelScroll_ShouldDirtyViewportInsteadOfFullFrame()
    {
        var treeView = new TreeView
        {
            Name = "ProjectExplorerTree",
            Width = 258f,
            Height = 492f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var root = CreateTreeViewItem("Root", isExpanded: true);
        for (var i = 0; i < 200; i++)
        {
            root.Items.Add(CreateTreeViewItem($"Item {i}", isExpanded: false));
        }

        treeView.Items.Add(root);

        var host = new Canvas { Width = 1280f, Height = 820f };
        host.AddChild(treeView);
        var uiRoot = new UiRoot(host);

        RunLayout(uiRoot, width: 1280, height: 820);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        host.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        var pointer = GetCenter(treeView.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
        uiRoot.ResetDirtyStateForTests();
        host.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));

        var coverage = uiRoot.GetDirtyCoverageForTests();
        Assert.False(uiRoot.IsFullDirtyForTests(), $"Wheel-scroll should not mark the full frame dirty. dirty={uiRoot.GetDirtyRegionSummaryForTests(limit: 12)}");
        Assert.True(
            coverage < 0.12d,
            $"Virtualized transform scroll should use the ScrollViewer viewport as the dirty hint. coverage={coverage:0.###}; dirty={uiRoot.GetDirtyRegionSummaryForTests(limit: 12)}");
    }

    /// <summary>
    /// Creates a TreeView populated with an expanded project-folder hierarchy
    /// (~180 items), measures it, and asserts the total MeasureMilliseconds
    /// stays within the 60fps frame budget.
    /// </summary>
    [Fact]
    public void ProjectStyleTreeView_WithManyExpandedItems_ShouldMeasureWithinFrameBudget()
    {
        // Arrange: host surface matching live Designer tree dimensions (~258x458)
        var host = new Canvas
        {
            Width = 460f,
            Height = 520f
        };

        var treeView = new TreeView
        {
            Width = 258f,
            Height = 458f,
            Background = new Color(0x18, 0x19, 0x1B),
            BorderBrush = new Color(0x18, 0x19, 0x1B),
            BorderThickness = 0f,
            Foreground = new Color(0xE5, 0xE7, 0xEA),
            Padding = new Thickness(6f)
        };

        // Build a realistic project-folder tree (~180 items, all expanded)
        var root = CreateTreeViewItem("SampleProject (DesignerRoot)", isExpanded: true);
        foreach (var folderName in new[] { "src", "Views", "Resources", "Components", "Images" })
        {
            var folder = CreateTreeViewItem(folderName, isExpanded: true);
            // Each folder gets 5 sub-folders, each with 6 files
            for (var sf = 0; sf < 5; sf++)
            {
                var subFolder = CreateTreeViewItem($"Sub{sf}", isExpanded: true);
                for (var f = 0; f < 6; f++)
                {
                    subFolder.Items.Add(CreateTreeViewItem($"File{f}.xml", isExpanded: false));
                }
                folder.Items.Add(subFolder);
            }
            root.Items.Add(folder);
        }
        treeView.Items.Add(root);

        // Attach to host
        host.AddChild(treeView);

        var uiRoot = new UiRoot(host);

        // Act: run a single complete layout pass
        RunLayout(uiRoot, width: 460, height: 520);

        // Capture instance-level diagnostics
        var snapshot = treeView.GetFrameworkElementSnapshotForDiagnostics();
        var controlSnapshot = treeView.GetControlSnapshotForDiagnostics();

        // Assert: total measure time must be within 60fps frame budget (16.6ms)
        var budgetMs = 16.6;
        Assert.True(
            snapshot.MeasureMilliseconds <= budgetMs,
            $"TreeView total measure time {snapshot.MeasureMilliseconds:0.###}ms exceeds {budgetMs}ms " +
            $"60fps budget. MeasureCallCount={snapshot.MeasureCallCount}, " +
            $"MeasureExclusive={snapshot.MeasureExclusiveMilliseconds:0.###}ms, " +
            $"InvalidateMeasureCallCount={snapshot.InvalidateMeasureCallCount}, " +
            $"GetVisualChildrenCallCount={controlSnapshot.GetVisualChildrenCallCount}");

        // Assert: measure reuse failure is the root cause — no template means every reuse is rejected
        Assert.True(
            controlSnapshot.CanReuseMeasureNoTemplateRootRejectedCount <= 0,
            $"All {controlSnapshot.CanReuseMeasureNoTemplateRootRejectedCount} measure-reuse attempts were " +
            $"rejected (HasTemplateRoot={controlSnapshot.HasTemplateRoot}). " +
            "This forces every measure pass through the full MeasureOverride path on all visible items.");
    }

    /// <summary>
    /// Baseline: a plain TreeView with a single root item should measure trivially fast.
    /// </summary>
    [Fact]
    public void EmptyTreeView_ShouldMeasureUnderBudget()
    {
        var host = new Canvas { Width = 300f, Height = 200f };
        var treeView = new TreeView { Width = 258f, Height = 200f };
        host.AddChild(treeView);
        var uiRoot = new UiRoot(host);

        RunLayout(uiRoot, width: 300, height: 200);

        var snapshot = treeView.GetFrameworkElementSnapshotForDiagnostics();
        Assert.True(
            snapshot.MeasureMilliseconds <= 16.6,
            $"Empty tree measured {snapshot.MeasureMilliseconds:0.###}ms, expected <= 16.6ms (60fps budget)");
    }

    /// <summary>
    /// Proof test: when the available size changes, FrameworkElement enters the
    /// CanReuseMeasureForAvailableSizeChange path. Control's override rejects
    /// reuse because HasTemplateRoot is false, incrementing
    /// CanReuseMeasureNoTemplateRootRejectedCount. This proves the tree cannot
    /// avoid a full re-measure even when only the parent's allocation changed.
    /// </summary>
    [Fact]
    public void NoTemplateRoot_ShouldRejectMeasureReuse_WhenSizeChanges()
    {
        // No explicit Width on TreeView — available size comes from the parent
        var host = new Border { Width = 300f, Height = 400f };
        var treeView = new TreeView();

        for (var i = 0; i < 10; i++)
        {
            treeView.Items.Add(CreateTreeViewItem($"Item {i}", isExpanded: true));
        }

        host.Child = treeView;
        var uiRoot = new UiRoot(host);

        // First layout
        RunLayout(uiRoot, width: 300, height: 400);

        var controlBefore = treeView.GetControlSnapshotForDiagnostics();

        Assert.False(controlBefore.HasTemplateRoot,
            "Structural: TreeView has no template root");
        Assert.False(controlBefore.HasTemplateAssigned,
            "Structural: TreeView has no template assigned");

        // Change host width so available size differs. This forces
        // FrameworkElement.Measure into the CanReuseMeasureForAvailableSizeChange
        // path (line 893) instead of the short-circuit (line 887).
        host.Width = 350f;
        host.InvalidateMeasure();
        RunLayout(uiRoot, width: 350, height: 400);

        var controlAfter = treeView.GetControlSnapshotForDiagnostics();

        Assert.True(
            controlAfter.CanReuseMeasureNoTemplateRootRejectedCount > 0,
            $"Control rejected reuse because HasTemplateRoot=false, but " +
            $"CanReuseMeasureNoTemplateRootRejectedCount=0. " +
            $"HasTemplateRoot={controlAfter.HasTemplateRoot}");
    }

    /// <summary>
    /// Proof test: TreeView without explicit Width/Height receives its available
    /// size from the Canvas. Changing the Canvas width should change the TreeView's
    /// available size, triggering the reuse-size-change path. This verifies the
    /// test setup works before testing the rejection path.
    /// </summary>
    [Fact]
    public void AvailableSize_ShouldChange_WhenParentWidthChanges()
    {
        var host = new Border { Width = 300f, Height = 400f };
        var treeView = new TreeView();
        host.Child = treeView;
        var uiRoot = new UiRoot(host);

        RunLayout(uiRoot, width: 300, height: 400);
        var size1 = treeView.PreviousAvailableSizeForTests;

        host.Width = 350f;
        host.InvalidateMeasure();
        RunLayout(uiRoot, width: 350, height: 400);
        var size2 = treeView.PreviousAvailableSizeForTests;

        Assert.NotEqual(size1, size2);
    }

    /// <summary>
    /// Proof test: the TreeView's visual children include the fallback ScrollViewer
    /// (not a template root), confirming every GetVisualChildren traversal hits the
    /// no-template path. In the live Designer this accumulated 4344-8001 calls.
    /// </summary>
    [Fact]
    public void VisualChildren_ShouldIncludeFallbackScrollViewer_NotTemplateRoot()
    {
        var treeView = new TreeView();
        // Add one item so the tree is non-empty
        treeView.Items.Add(CreateTreeViewItem("Test", isExpanded: false));

        var snapshot = treeView.GetControlSnapshotForDiagnostics();

        // Has a template? No — this is the structural reason for performance issues
        Assert.False(snapshot.HasTemplateRoot);
        Assert.Empty(snapshot.TemplateRootType);

        // GetVisualChildren should yield the fallback ScrollViewer (not null)
        var children = treeView.GetVisualChildren();
        Assert.Contains(children, child => child is ScrollViewer);
    }

    private static TreeViewItem CreateTreeViewItem(string header, bool isExpanded)
    {
        return new TreeViewItem
        {
            Header = header,
            IsExpanded = isExpanded
        };
    }

    private static TreeViewItem CreateExpandedProjectTree(int folderCount, int fileCount)
    {
        var root = CreateTreeViewItem("InkkSlinger", isExpanded: true);
        var filesPerFolder = fileCount / folderCount;
        var extraFiles = fileCount % folderCount;
        for (var folderIndex = 0; folderIndex < folderCount; folderIndex++)
        {
            var folder = CreateTreeViewItem($"Folder {folderIndex:0000}", isExpanded: true);
            var count = filesPerFolder + (folderIndex < extraFiles ? 1 : 0);
            for (var fileIndex = 0; fileIndex < count; fileIndex++)
            {
                folder.Items.Add(CreateTreeViewItem($"File {folderIndex:0000}-{fileIndex:000}.xml", isExpanded: false));
            }

            root.Items.Add(folder);
        }

        return root;
    }

    private static ProjectNode CreateProjectNodeTree(int folderCount, int fileCount)
    {
        var root = new ProjectNode("InkkSlinger", isFolder: true);
        var filesPerFolder = fileCount / folderCount;
        var extraFiles = fileCount % folderCount;
        for (var folderIndex = 0; folderIndex < folderCount; folderIndex++)
        {
            var folder = new ProjectNode($"Folder {folderIndex:0000}", isFolder: true);
            var count = filesPerFolder + (folderIndex < extraFiles ? 1 : 0);
            for (var fileIndex = 0; fileIndex < count; fileIndex++)
            {
                folder.Children.Add(new ProjectNode($"File {folderIndex:0000}-{fileIndex:000}.xml", isFolder: false));
            }

            root.Children.Add(folder);
        }

        return root;
    }

    private static int CountProjectNodes(ProjectNode node)
    {
        var count = 1;
        foreach (var child in node.Children)
        {
            count += CountProjectNodes(child);
        }

        return count;
    }

    private static int CountTreeItems(TreeViewItem item)
    {
        var count = 1;
        foreach (var child in item.GetChildTreeItems())
        {
            count += CountTreeItems(child);
        }

        return count;
    }

    private static IEnumerable<TreeViewItem> EnumerateTreeItems(TreeViewItem item)
    {
        yield return item;
        foreach (var child in item.GetChildTreeItems())
        {
            foreach (var descendant in EnumerateTreeItems(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<TElement> EnumerateVisualDescendants<TElement>(UIElement root)
        where TElement : UIElement
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is TElement match)
            {
                yield return match;
            }

            foreach (var descendant in EnumerateVisualDescendants<TElement>(child))
            {
                yield return descendant;
            }
        }
    }

    private static string[] GetVisibleTreeRowHeaders(ScrollViewer scrollViewer)
    {
        return EnumerateVisualDescendants<TreeViewItem>(scrollViewer)
            .OrderBy(row => row.LayoutSlot.Y)
            .Select(row => row.DisplayHeaderForDiagnostics)
            .Where(static header => !string.IsNullOrEmpty(header))
            .ToArray();
    }

    private static int CountRowsIntersecting(IEnumerable<TreeViewItem> rows, LayoutRect bounds)
    {
        return rows.Count(row => row.TryGetRenderBoundsInRootSpace(out var rowBounds) && Intersects(rowBounds, bounds));
    }

    private static bool Intersects(LayoutRect first, LayoutRect second)
    {
        return first.X < second.X + second.Width &&
               first.X + first.Width > second.X &&
               first.Y < second.Y + second.Height &&
               first.Y + first.Height > second.Y;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }

    private static ScrollBar GetPrivateScrollBar(ScrollViewer viewer, string fieldName)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ScrollBar>(field!.GetValue(viewer));
    }

    private static TElement? FindNamedVisualChild<TElement>(UIElement root, string name)
        where TElement : FrameworkElement
    {
        if (root is TElement typed && typed.Name == name)
        {
            return typed;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindNamedVisualChild<TElement>(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool pointerMoved = false, int wheelDelta = 0)
    {
        return CreatePointerDelta(pointer, pointer, pointerMoved, wheelDelta);
    }

    private static InputDelta CreatePointerDelta(
        Vector2 previous,
        Vector2 current,
        bool pointerMoved = false,
        int wheelDelta = 0,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, previous),
            Current = new InputSnapshot(default, default, current),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = wheelDelta,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreatePointerWheelDelta(Vector2 pointer, int wheelDelta)
    {
        return CreatePointerDelta(pointer, pointerMoved: true, wheelDelta: wheelDelta);
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
    }

    private static void PopulateProjectTree(
        FakeProjectFileStore store,
        string projectRoot,
        int sectionCount = 8,
        int filesPerSection = 12,
        bool longFileNames = false)
    {
        store.CreateDirectory(projectRoot);

        foreach (var topLevelFolder in new[] { "Views", "Controls", "Resources", "Themes", "Documents", "Samples" })
        {
            var topLevelPath = $"{projectRoot}/{topLevelFolder}";
            store.CreateDirectory(topLevelPath);

            for (var section = 0; section < sectionCount; section++)
            {
                var sectionPath = $"{topLevelPath}/Section{section:00}";
                store.CreateDirectory(sectionPath);

                for (var fileIndex = 0; fileIndex < filesPerSection; fileIndex++)
                {
                    var extension = fileIndex % 3 == 0 ? "xml" : fileIndex % 3 == 1 ? "cs" : "json";
                    var fileName = longFileNames
                        ? $"Item{fileIndex:00}.ThisIsAnIntentionallyLongProjectExplorerFileNameThatMustTrimWhileTheScrollbarThumbIsHeld.{extension}"
                        : $"Item{fileIndex:00}.{extension}";
                    store.WriteAllText(
                        $"{sectionPath}/{fileName}",
                        $"// {topLevelFolder}/Section{section:00}/{fileName}");
                }
            }
        }
    }

    private sealed class FakeProjectFileStore : InkkSlinger.Designer.IDesignerProjectFileStore, InkkSlinger.Designer.IDesignerDocumentFileStore
    {
        private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

        public bool Exists(string path)
        {
            var normalized = NormalizePath(path);
            return _directories.Contains(normalized) || _files.ContainsKey(normalized);
        }

        public bool DirectoryExists(string path)
        {
            return _directories.Contains(NormalizePath(path));
        }

        public bool FileExists(string path)
        {
            return _files.ContainsKey(NormalizePath(path));
        }

        public IReadOnlyList<string> EnumerateDirectories(string path)
        {
            var prefix = NormalizePath(path) + "/";
            return _directories
                .Where(directory => directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(directory => !directory[prefix.Length..].Contains('/', StringComparison.Ordinal))
                .OrderBy(GetName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<string> EnumerateFiles(string path)
        {
            var prefix = NormalizePath(path) + "/";
            return _files.Keys
                .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(file => !file[prefix.Length..].Contains('/', StringComparison.Ordinal))
                .OrderBy(GetName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public void CreateDirectory(string path)
        {
            _directories.Add(NormalizePath(path));
        }

        public string ReadAllText(string path)
        {
            return _files[NormalizePath(path)];
        }

        public void WriteAllText(string path, string text)
        {
            var normalized = NormalizePath(path);
            var parent = GetParent(normalized);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                _directories.Add(parent);
            }

            _files[normalized] = text;
        }

        public void Rename(string path, string newPath)
        {
            var normalizedPath = NormalizePath(path);
            var normalizedNewPath = NormalizePath(newPath);
            if (_files.Remove(normalizedPath, out var text))
            {
                _files[normalizedNewPath] = text;
                return;
            }

            if (!_directories.Remove(normalizedPath))
            {
                return;
            }

            _directories.Add(normalizedNewPath);
            var directoryPrefix = normalizedPath + "/";
            foreach (var directory in _directories.Where(directory => directory.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                _directories.Remove(directory);
                _directories.Add(normalizedNewPath + "/" + directory[directoryPrefix.Length..]);
            }

            foreach (var file in _files.Keys.Where(file => file.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                var value = _files[file];
                _files.Remove(file);
                _files[normalizedNewPath + "/" + file[directoryPrefix.Length..]] = value;
            }
        }

        public void Delete(string path)
        {
            var normalized = NormalizePath(path);
            _files.Remove(normalized);
            var prefix = normalized + "/";
            foreach (var file in _files.Keys.Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                _files.Remove(file);
            }

            foreach (var directory in _directories.Where(directory => string.Equals(directory, normalized, StringComparison.OrdinalIgnoreCase) || directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                _directories.Remove(directory);
            }
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }

        private static string GetName(string path)
        {
            var normalized = NormalizePath(path);
            var index = normalized.LastIndexOf('/');
            return index < 0 ? normalized : normalized[(index + 1)..];
        }

        private static string? GetParent(string path)
        {
            var normalized = NormalizePath(path);
            var index = normalized.LastIndexOf('/');
            return index < 0 ? null : normalized[..index];
        }
    }

    private sealed class ProjectNode(string name, bool isFolder)
    {
        public string Name { get; } = name;

        public bool IsFolder { get; } = isFolder;

        public List<ProjectNode> Children { get; } = new();
    }

    private sealed class LazyProjectNode(string name, bool isFolder)
    {
        public string Name { get; } = name;

        public bool IsFolder { get; } = isFolder;

        public List<LazyProjectNode> Children { get; } = new();
    }
}
