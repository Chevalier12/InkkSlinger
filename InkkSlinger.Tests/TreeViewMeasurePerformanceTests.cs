using System;
using System.Collections.Generic;
using System.Linq;
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

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool pointerMoved = false, int wheelDelta = 0)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved,
            WheelDelta = wheelDelta,
            LeftPressed = false,
            LeftReleased = false,
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

    private static void PopulateProjectTree(FakeProjectFileStore store, string projectRoot)
    {
        store.CreateDirectory(projectRoot);

        foreach (var topLevelFolder in new[] { "Views", "Controls", "Resources", "Themes", "Documents", "Samples" })
        {
            var topLevelPath = $"{projectRoot}/{topLevelFolder}";
            store.CreateDirectory(topLevelPath);

            for (var section = 0; section < 8; section++)
            {
                var sectionPath = $"{topLevelPath}/Section{section:00}";
                store.CreateDirectory(sectionPath);

                for (var fileIndex = 0; fileIndex < 12; fileIndex++)
                {
                    var extension = fileIndex % 3 == 0 ? "xml" : fileIndex % 3 == 1 ? "cs" : "json";
                    store.WriteAllText(
                        $"{sectionPath}/Item{fileIndex:00}.{extension}",
                        $"// {topLevelFolder}/Section{section:00}/Item{fileIndex:00}.{extension}");
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
}
