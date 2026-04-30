using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InkkSlinger.UI.Telemetry;
using Xunit;
using Vector2 = System.Numerics.Vector2;

namespace InkkSlinger.Tests;

/// <summary>
/// Runtime investigation: TreeView scroll-while-hovered causes FPS drop from 60â†'30.
///
/// Phase 1 (RUN test): Measure counter deltas via InkkOopsTestHost.
/// Phase 2 (CLI probe): Launch real Designer + use CLI to measure actual frame times.
/// </summary>
public sealed class TreeViewWheelFpsInvestigationTests
{
    /// <summary>
    /// Verify the ScrollViewer responds to HandleMouseWheelFromInput directly.
    /// </summary>
    [Fact]
    public void ScrollViewer_HandleMouseWheelFromInput_ScrollsContent()
    {
        using var host = CreateTreeViewHost(out var treeView, out var viewer);

        // Direct call to verify the viewer scrolls
        var before = viewer.VerticalOffset;
        var handled = viewer.HandleMouseWheelFromInput(-120);
        // Advance frame to process layout
        host.AdvanceFrameAsync(1).GetAwaiter().GetResult();
        var after = viewer.VerticalOffset;

        Assert.True(handled, "HandleMouseWheelFromInput should return true (scroll was handled)");
        Assert.True(after > before, $"ScrollViewer should have scrolled after direct wheel call: before={before}, after={after}");
    }

    /// <summary>
    /// Primary investigation: measure counter deltas for
    ///   (a) hover items + wheel (via direct HandleMouseWheelFromInput call)
    ///   (b) hover scrollbar + wheel
    /// Compares path distribution to identify what changes per wheel tick
    /// based on hover target context (even though the wheel call is the same).
    /// </summary>
    [Fact]
    public async Task ScrollWheel_ItemHovered_ScrollBarHovered_CounterComparison()
    {
        using var host = CreateTreeViewHost(out var treeView, out var viewer);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "treeview-wheel-deltas");
        var session = new InkkOopsSession(host, artifacts);

        var rootItem = treeView.GetItemContainersForPresenter()
            .OfType<TreeViewItem>()
            .First();
        var itemCenter = new Vector2(
            rootItem.LayoutSlot.X + rootItem.LayoutSlot.Width / 2f,
            rootItem.LayoutSlot.Y + rootItem.LayoutSlot.Height / 2f);

        // ScrollViewer right edge (where scrollbar would be)
        var scrollbarArea = new Vector2(
            viewer.LayoutSlot.X + viewer.LayoutSlot.Width - 8f,
            viewer.LayoutSlot.Y + 40f);

        // ============ SCENARIO A: Hover items, then scroll directly ============
        ScrollViewer.GetTelemetryAndReset();

        // Hover TreeView item
        await new InkkOopsMovePointerCommand(itemCenter).ExecuteAsync(session);
        await host.AdvanceFrameAsync(3);
        Assert.True(rootItem.IsMouseOver, "Root item should be hovered before wheel");

        var preItemSnap = viewer.GetScrollViewerSnapshotForDiagnostics();
        var preItemOffset = viewer.VerticalOffset;

        // Use direct HandleMouseWheelFromInput for 10 ticks
        for (var i = 0; i < 10; i++)
        {
            viewer.HandleMouseWheelFromInput(-120);
            await host.AdvanceFrameAsync(1);
        }

        var postItemSnap = viewer.GetScrollViewerSnapshotForDiagnostics();
        var postItemOffset = viewer.VerticalOffset;
        Assert.True(postItemOffset > preItemOffset, "Items-hover: viewer should scroll");

        // ============ SCENARIO B: Hover scrollbar area, scroll ============
        // Leave items
        await new InkkOopsMovePointerCommand(new Vector2(0f, 0f)).ExecuteAsync(session);
        await host.AdvanceFrameAsync(2);
        Assert.False(rootItem.IsMouseOver, "Root should not be hovered after leaving");

        // Reset scroll
        viewer.ScrollToVerticalOffset(0);
        await host.AdvanceFrameAsync(3);

        var preBarSnap = viewer.GetScrollViewerSnapshotForDiagnostics();
        var preBarOffset = viewer.VerticalOffset;

        // Hover scrollbar
        await new InkkOopsMovePointerCommand(scrollbarArea).ExecuteAsync(session);
        await host.AdvanceFrameAsync(2);

        // Same direct scroll
        for (var i = 0; i < 10; i++)
        {
            viewer.HandleMouseWheelFromInput(-120);
            await host.AdvanceFrameAsync(1);
        }

        var postBarSnap = viewer.GetScrollViewerSnapshotForDiagnostics();
        var postBarOffset = viewer.VerticalOffset;

        // ============ ANALYSIS ============
        var analysis = BuildAnalysis(preItemSnap, postItemSnap, preBarSnap, postBarSnap,
            preItemOffset, postItemOffset, preBarOffset, postBarOffset,
            host.ArtifactRoot);
        var artifactDir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "inkkoops", "investigation");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Combine(artifactDir, "analysis.txt"), analysis);

        var actionLogPath = artifacts.GetActionLogPath();
        if (File.Exists(actionLogPath))
        {
            File.WriteAllText(Path.Combine(artifactDir, "action-log.txt"), File.ReadAllText(actionLogPath));
        }

        Console.WriteLine(analysis);

        // Assert both scenarios scrolled
        Assert.True(postItemOffset > preItemOffset, "Items-hover scenario should have scrolled");
        Assert.True(postBarOffset > preBarOffset, "Scrollbar scenario should have scrolled or stayed same");

        // Assert VirtualizingArrangeOnly path was used (not measure invalidation)
        var itemVirtPath = postItemSnap.SetOffsetsVirtualizingArrangeOnlyPathCount - preItemSnap.SetOffsetsVirtualizingArrangeOnlyPathCount;
        Assert.True(itemVirtPath > 0, "Items-hover wheel should take virtualizing arrange-only path");
    }

    /// <summary>
    /// Baseline: verify counter deltas for a single direct wheel call.
    /// </summary>
    [Fact]
    public void MeasureBaselineTreeViewScrollCounters()
    {
        using var host = CreateTreeViewHost(out var treeView, out var viewer);

        ScrollViewer.GetTelemetryAndReset();
        var preSnap = viewer.GetScrollViewerSnapshotForDiagnostics();
        Assert.Equal(0, preSnap.HandleMouseWheelCallCount);

        var handled = viewer.HandleMouseWheelFromInput(-120);
        host.AdvanceFrameAsync(1).GetAwaiter().GetResult();

        var postSnap = viewer.GetScrollViewerSnapshotForDiagnostics();

        var sb = new StringBuilder();
        sb.AppendLine("=== Baseline Single-Wheel Counter Snapshot ===");
        sb.AppendLine($"handled={handled}");
        sb.AppendLine($"WheelEvents: {postSnap.WheelEvents}");
        sb.AppendLine($"WheelHandled: {postSnap.WheelHandled}");
        sb.AppendLine($"HandleMouseWheelCallCount: {postSnap.HandleMouseWheelCallCount}");
        sb.AppendLine($"HandleMouseWheelElapsedMs: {postSnap.HandleMouseWheelMilliseconds:F4}");
        sb.AppendLine($"SetOffsetsCallCount: {postSnap.SetOffsetsCallCount}");
        sb.AppendLine($"SetOffsetsElapsedMs: {postSnap.SetOffsetsMilliseconds:F4}");
        sb.AppendLine($"SetOffsetsVirtualizingArrangeOnlyPathCount: {postSnap.SetOffsetsVirtualizingArrangeOnlyPathCount}");
        sb.AppendLine($"SetOffsetsVirtualizingMeasureInvalidationPathCount: {postSnap.SetOffsetsVirtualizingMeasureInvalidationPathCount}");
        sb.AppendLine($"SetOffsetsTransformInvalidationPathCount: {postSnap.SetOffsetsTransformInvalidationPathCount}");
        sb.AppendLine($"MeasureOverrideCallCount: {postSnap.MeasureOverrideCallCount}");
        sb.AppendLine($"MeasureOverrideElapsedMs: {postSnap.MeasureOverrideMilliseconds:F4}");
        sb.AppendLine($"ArrangeOverrideCallCount: {postSnap.ArrangeOverrideCallCount}");
        sb.AppendLine($"ArrangeOverrideElapsedMs: {postSnap.ArrangeOverrideMilliseconds:F4}");
        sb.AppendLine($"ResolveBarsAndMeasureContentCallCount: {postSnap.ResolveBarsAndMeasureContentCallCount}");
        sb.AppendLine($"ResolveBarsAndMeasureContentElapsedMs: {postSnap.ResolveBarsAndMeasureContentMilliseconds:F4}");
        sb.AppendLine($"ArrangeContentForCurrentOffsetsCallCount: {postSnap.ArrangeContentForCurrentOffsetsCallCount}");
        sb.AppendLine($"ArrangeContentForCurrentOffsetsElapsedMs: {postSnap.ArrangeContentForCurrentOffsetsMilliseconds:F4}");
        sb.AppendLine($"UpdateScrollBarValuesCallCount: {postSnap.UpdateScrollBarValuesCallCount}");
        sb.AppendLine($"VerticalValueChangedCallCount: {postSnap.VerticalValueChangedCallCount}");
        sb.AppendLine($"VerticalOffset after: {viewer.VerticalOffset}");

        var artifactDir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "inkkoops", "investigation");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Combine(artifactDir, "baseline.txt"), sb.ToString());
        Console.WriteLine(sb.ToString());

        Assert.True(handled, "HandleMouseWheelFromInput should return true");
        Assert.True(postSnap.HandleMouseWheelCallCount > 0, "HandleMouseWheel should have been called");
        Assert.True(viewer.VerticalOffset > 0f, "ScrollViewer vertical offset should have changed");
    }

    // ---- Helpers ----

    private static InkkOopsTestHost CreateTreeViewHost(
        out TreeView treeView,
        out ScrollViewer viewer)
    {
        treeView = new TreeView
        {
            Name = "ProjectExplorerTree",
            Width = 260f,
            Height = 400f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        // Build a multi-level tree like Project Explorer with enough items
        // to exceed the viewport height and enable scrolling
        var root = new TreeViewItem
        {
            Header = "Root",
            IsExpanded = true
        };
        root.Items.Add(new TreeViewItem { Header = "Documents", IsExpanded = true });
        root.Items.Add(new TreeViewItem { Header = "Media", IsExpanded = true });
        root.Items.Add(new TreeViewItem { Header = "Scripts", IsExpanded = true });
        // Add many more items to exceed viewport height (viewer ~390px, each row ~22px)
        for (var i = 1; i <= 30; i++)
        {
            root.Items.Add(new TreeViewItem { Header = $"Item {i}" });
        }
        treeView.Items.Add(root);

        // Find the _fallbackScrollViewer from TreeView's visual children
        viewer = (ScrollViewer)treeView.GetVisualChildren()
            .First(c => c is ScrollViewer);

        var container = new Canvas { Width = 800f, Height = 600f };
        container.AddChild(treeView);

        return new InkkOopsTestHost(container);
    }

    private static string BuildAnalysis(
        ScrollViewerRuntimeDiagnosticsSnapshot preItem,
        ScrollViewerRuntimeDiagnosticsSnapshot postItem,
        ScrollViewerRuntimeDiagnosticsSnapshot preBar,
        ScrollViewerRuntimeDiagnosticsSnapshot postBar,
        float preItemOffset,
        float postItemOffset,
        float preBarOffset,
        float postBarOffset,
        string artifactRoot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("===========================================================");
        sb.AppendLine(" TreeView Scroll Wheel FPS Investigation - Counter Analysis");
        sb.AppendLine("===========================================================");
        sb.AppendLine($"Artifact root: {artifactRoot}");
        sb.AppendLine();

        sb.AppendLine("--- Scenario A: Hover TreeView Items + Wheel (10 ticks) ---");
        AppendDeltas(sb, preItem, postItem, preItemOffset, postItemOffset);

        sb.AppendLine();
        sb.AppendLine("--- Scenario B: Hover ScrollBar Area + Wheel (10 ticks) ---");
        AppendDeltas(sb, preBar, postBar, preBarOffset, postBarOffset);

        sb.AppendLine();
        sb.AppendLine("--- Key Observations ---");

        var itemWheelDelta = postItem.HandleMouseWheelCallCount - preItem.HandleMouseWheelCallCount;
        var barWheelDelta = postBar.HandleMouseWheelCallCount - preBar.HandleMouseWheelCallCount;
        var itemSetOffDelta = postItem.SetOffsetsCallCount - preItem.SetOffsetsCallCount;
        var barSetOffDelta = postBar.SetOffsetsCallCount - preBar.SetOffsetsCallCount;
        var itemMeasDelta = postItem.MeasureOverrideCallCount - preItem.MeasureOverrideCallCount;
        var barMeasDelta = postBar.MeasureOverrideCallCount - preBar.MeasureOverrideCallCount;
        var itemArrDelta = postItem.ArrangeOverrideCallCount - preItem.ArrangeOverrideCallCount;
        var barArrDelta = postBar.ArrangeOverrideCallCount - preBar.ArrangeOverrideCallCount;
        var itemResolveDelta = postItem.ResolveBarsAndMeasureContentCallCount - preItem.ResolveBarsAndMeasureContentCallCount;
        var barResolveDelta = postBar.ResolveBarsAndMeasureContentCallCount - preBar.ResolveBarsAndMeasureContentCallCount;

        sb.AppendLine($"  Items-hover wheel ticks: {itemWheelDelta}");
        sb.AppendLine($"  Scrollbar wheel ticks: {barWheelDelta}");
        sb.AppendLine($"  SetOffsets calls (items): {itemSetOffDelta}  (scrollbar): {barSetOffDelta}");
        sb.AppendLine($"  MeasureOverride (items): {itemMeasDelta}  (scrollbar): {barMeasDelta}");
        sb.AppendLine($"  ArrangeOverride (items): {itemArrDelta}  (scrollbar): {barArrDelta}");
        sb.AppendLine($"  ResolveBarsAndMeasureContent (items): {itemResolveDelta}  (scrollbar): {barResolveDelta}");

        var itemVirtArrPath = postItem.SetOffsetsVirtualizingArrangeOnlyPathCount - preItem.SetOffsetsVirtualizingArrangeOnlyPathCount;
        var barVirtArrPath = postBar.SetOffsetsVirtualizingArrangeOnlyPathCount - preBar.SetOffsetsVirtualizingArrangeOnlyPathCount;
        var itemVirtMeasPath = postItem.SetOffsetsVirtualizingMeasureInvalidationPathCount - preItem.SetOffsetsVirtualizingMeasureInvalidationPathCount;
        var barVirtMeasPath = postBar.SetOffsetsVirtualizingMeasureInvalidationPathCount - preBar.SetOffsetsVirtualizingMeasureInvalidationPathCount;

        sb.AppendLine($"  VirtualizingArrangeOnlyPath (items): {itemVirtArrPath}  (scrollbar): {barVirtArrPath}");
        sb.AppendLine($"  VirtualizingMeasureInvalidationPath (items): {itemVirtMeasPath}  (scrollbar): {barVirtMeasPath}");

        var itemWheelMs = postItem.HandleMouseWheelMilliseconds - preItem.HandleMouseWheelMilliseconds;
        var barWheelMs = postBar.HandleMouseWheelMilliseconds - preBar.HandleMouseWheelMilliseconds;
        sb.AppendLine($"  HandleMouseWheel total ms (items): {itemWheelMs:F4}  (scrollbar): {barWheelMs:F4}");

        sb.AppendLine();
        sb.AppendLine("--- Hypothesis Evaluation ---");

        if (itemMeasDelta == barMeasDelta && itemArrDelta == barArrDelta)
            sb.AppendLine("[REJECTED] Layout storm: measure/arrange counts identical between scenarios");
        else
            sb.AppendLine($"[POSSIBLE] Layout storm: diff in measure ({itemMeasDelta - barMeasDelta}) or arrange ({itemArrDelta - barArrDelta})");

        sb.AppendLine("[NEEDS CLI PROBE] Hover re-evaluation: RefreshHoverAfterWheelContentMutation hit-test cost.");
        sb.AppendLine("[NEEDS CLI PROBE] Render invalidation: only measurable in real game loop with FPS.");
        sb.AppendLine("[NEEDS CLI PROBE] Real FPS measurement from game loop delta.");

        if (itemVirtArrPath == barVirtArrPath && itemVirtMeasPath == barVirtMeasPath)
            sb.AppendLine("[REJECTED] Path divergence: same Virtualizing path distribution");
        else
            sb.AppendLine($"[POSSIBLE] Path divergence: diff arrange-only={itemVirtArrPath - barVirtArrPath}, meas-invalidation={itemVirtMeasPath - barVirtMeasPath}");

        sb.AppendLine();
        sb.AppendLine("--- Next Steps for CLI Probe ---");
        sb.AppendLine("1. dotnet run --project InkkSlinger.Designer -- --designer-start-workspace <project> --inkkoops-pipe InkkOops");
        sb.AppendLine("2. CLI: get-host-info, get-telemetry --artifact pre-scroll");
        sb.AppendLine("3. CLI: get-target-diagnostics --target ProjectExplorerTree --compact --counters ...");
        sb.AppendLine("4. CLI: wheel --target ProjectExplorerTree --delta -120");
        sb.AppendLine("5. CLI: get-telemetry --artifact post-scroll");
        sb.AppendLine("6. Compare with scrollbar-hover scenario");

        return sb.ToString();
    }

    private static void AppendDeltas(StringBuilder sb,
        ScrollViewerRuntimeDiagnosticsSnapshot pre,
        ScrollViewerRuntimeDiagnosticsSnapshot post,
        float preOffset,
        float postOffset)
    {
        void Delta(string name, long p, long q) =>
            sb.AppendLine($"  {name}: {p} -> {q} (Δ={q - p})");
        void DeltaMs(string name, double p, double q) =>
            sb.AppendLine($"  {name}: {p:F4}ms -> {q:F4}ms (Δ={q - p:F4}ms)");

        Delta("WheelEvents", pre.WheelEvents, post.WheelEvents);
        Delta("WheelHandled", pre.WheelHandled, post.WheelHandled);
        Delta("HandleMouseWheelCallCount", pre.HandleMouseWheelCallCount, post.HandleMouseWheelCallCount);
        DeltaMs("HandleMouseWheelElapsedMs", pre.HandleMouseWheelMilliseconds, post.HandleMouseWheelMilliseconds);
        Delta("SetOffsetsCallCount", pre.SetOffsetsCallCount, post.SetOffsetsCallCount);
        DeltaMs("SetOffsetsElapsedMs", pre.SetOffsetsMilliseconds, post.SetOffsetsMilliseconds);
        Delta("SetOffsetsVirtualizingArrangeOnlyPathCount",
            pre.SetOffsetsVirtualizingArrangeOnlyPathCount, post.SetOffsetsVirtualizingArrangeOnlyPathCount);
        Delta("SetOffsetsVirtualizingMeasureInvalidationPathCount",
            pre.SetOffsetsVirtualizingMeasureInvalidationPathCount, post.SetOffsetsVirtualizingMeasureInvalidationPathCount);
        Delta("SetOffsetsTransformInvalidationPathCount",
            pre.SetOffsetsTransformInvalidationPathCount, post.SetOffsetsTransformInvalidationPathCount);
        Delta("SetOffsetsManualArrangePathCount",
            pre.SetOffsetsManualArrangePathCount, post.SetOffsetsManualArrangePathCount);
        Delta("MeasureOverrideCallCount", pre.MeasureOverrideCallCount, post.MeasureOverrideCallCount);
        DeltaMs("MeasureOverrideElapsedMs", pre.MeasureOverrideMilliseconds, post.MeasureOverrideMilliseconds);
        Delta("ArrangeOverrideCallCount", pre.ArrangeOverrideCallCount, post.ArrangeOverrideCallCount);
        DeltaMs("ArrangeOverrideElapsedMs", pre.ArrangeOverrideMilliseconds, post.ArrangeOverrideMilliseconds);
        Delta("ResolveBarsAndMeasureContentCallCount",
            pre.ResolveBarsAndMeasureContentCallCount, post.ResolveBarsAndMeasureContentCallCount);
        DeltaMs("ResolveBarsAndMeasureContentElapsedMs",
            pre.ResolveBarsAndMeasureContentMilliseconds, post.ResolveBarsAndMeasureContentMilliseconds);
        Delta("ArrangeContentForCurrentOffsetsCallCount",
            pre.ArrangeContentForCurrentOffsetsCallCount, post.ArrangeContentForCurrentOffsetsCallCount);
        DeltaMs("ArrangeContentForCurrentOffsetsElapsedMs",
            pre.ArrangeContentForCurrentOffsetsMilliseconds, post.ArrangeContentForCurrentOffsetsMilliseconds);
        Delta("UpdateScrollBarValuesCallCount",
            pre.UpdateScrollBarValuesCallCount, post.UpdateScrollBarValuesCallCount);
        DeltaMs("UpdateScrollBarValuesElapsedMs",
            pre.UpdateScrollBarValuesMilliseconds, post.UpdateScrollBarValuesMilliseconds);
        Delta("VerticalValueChangedCallCount",
            pre.VerticalValueChangedCallCount, post.VerticalValueChangedCallCount);
        DeltaMs("VerticalValueChangedElapsedMs",
            pre.VerticalValueChangedMilliseconds, post.VerticalValueChangedMilliseconds);
        Delta("HorizontalValueChangedCallCount",
            pre.HorizontalValueChangedCallCount, post.HorizontalValueChangedCallCount);
        sb.AppendLine($"  VerticalOffset: {preOffset:F1} -> {postOffset:F1} (Î={postOffset - preOffset:F1})");
    }
}
