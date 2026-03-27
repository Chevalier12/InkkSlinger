using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogSidebarButtonLeaveRetainedSyncTests
{
    private const int ViewportWidth = 1400;
    private const int ViewportHeight = 900;

    [Fact]
    public void ScrolledSidebarButton_ClickThenLeave_UsesButtonRetainedDirtyRoot()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();
            AnimationManager.Current.ResetForTests();
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();

            var clock = new TestFrameClock();
            var (catalog, uiRoot, viewer, targetButton, targetPoint) = CreateScrolledSidebarFixture(clock);
            HoverButton(uiRoot, targetButton, targetPoint, clock);
            Click(uiRoot, targetPoint);

            PrimeRetainedRenderStateForDiagnostics(catalog, uiRoot);
            uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight));

            var leavePoint = new Vector2(viewer.LayoutSlot.X + viewer.LayoutSlot.Width + 32f, targetPoint.Y);
            MovePointer(uiRoot, leavePoint);
            clock.Advance(uiRoot, 16);
            AdvanceFrames(uiRoot, clock, 12, 16);

            var transform = Assert.IsType<ScaleTransform>(targetButton.RenderTransform);
            var dirtyRootSummary = uiRoot.GetLastSynchronizedDirtyRootSummaryForTests();

            Assert.Equal(1f, transform.ScaleX, 3);
            Assert.False(targetButton.IsMouseOver);
            Assert.Contains("Button", dirtyRootSummary, StringComparison.Ordinal);
            Assert.DoesNotContain("ScrollViewer", dirtyRootSummary, StringComparison.Ordinal);
        }
        finally
        {
            RestoreApplicationResources(backup);
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();
        }
    }

    [Fact]
    public void ScrolledSidebarButton_ClickThenLeave_WritesRetainedSyncDiagnosticsLog()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();
            AnimationManager.Current.ResetForTests();
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();

            var clock = new TestFrameClock();
            var (catalog, uiRoot, viewer, targetButton, targetPoint) = CreateScrolledSidebarFixture(clock);
            HoverButton(uiRoot, targetButton, targetPoint, clock);
            Click(uiRoot, targetPoint);

            PrimeRetainedRenderStateForDiagnostics(catalog, uiRoot);
            uiRoot.SetDirtyRegionViewportForTests(new LayoutRect(0f, 0f, ViewportWidth, ViewportHeight));
            uiRoot.ClearDirtyBoundsEventTraceForTests();

            var beforeScale = Assert.IsType<ScaleTransform>(targetButton.RenderTransform).ScaleX;
            var leavePoint = new Vector2(viewer.LayoutSlot.X + viewer.LayoutSlot.Width + 32f, targetPoint.Y);

            MovePointer(uiRoot, leavePoint);
            var pointerTelemetry = uiRoot.GetPointerMoveTelemetrySnapshotForTests();
            clock.Advance(uiRoot, 16);
            AdvanceFrames(uiRoot, clock, 12, 16);

            var afterScale = Assert.IsType<ScaleTransform>(targetButton.RenderTransform).ScaleX;
            var invalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
            var performance = uiRoot.GetPerformanceTelemetrySnapshotForTests();
            var dirtyRootSummary = uiRoot.GetLastSynchronizedDirtyRootSummaryForTests();
            var dirtyRegions = uiRoot.GetDirtyRegionSummaryForTests();
            var dirtyTrace = string.Join(" | ", uiRoot.GetDirtyBoundsEventTraceForTests());

            var hotspotInference = dirtyRootSummary.Contains("ScrollViewer", StringComparison.Ordinal)
                ? "Exact hotspot: UiRoot.ResolveRetainedSyncSource promoted the clipped transformed button mutation to the ScrollViewer, so TryBuildUpdatedNodeWithCurrentChildren shallow-synced the ancestor and left the animated Button subtree stale in the retained tree."
                : "The old hotspot is gone: the leave transition now synchronizes the Button subtree directly, so the retained tree no longer holds a stale scaled button frame inside the scrolled ScrollViewer.";

            var logPath = GetDiagnosticsLogPath("controls-catalog-sidebar-button-leave-retained-sync");
            var lines = new List<string>
            {
                "scenario=Controls Catalog sidebar scrolled button click then leave retained sync diagnostics",
                $"timestamp_utc={DateTime.UtcNow:O}",
                $"log_path={logPath}",
                "step_1=load App.xml resources so catalog buttons use the animated template",
                "step_2=open Controls Catalog and scroll the sidebar down",
                "step_3=hover a visible sidebar button until its scale animation grows",
                "step_4=click the same button",
                "step_5=move the pointer outside the button while the sidebar remains scrolled",
                $"viewer_slot={FormatRect(viewer.LayoutSlot)}",
                $"button_slot={FormatRect(targetButton.LayoutSlot)}",
                $"target_point=({targetPoint.X:0.##},{targetPoint.Y:0.##})",
                $"leave_point=({leavePoint.X:0.##},{leavePoint.Y:0.##})",
                $"button_is_mouse_over={targetButton.IsMouseOver}",
                $"scale_before_leave={beforeScale:0.###}",
                $"scale_after_leave={afterScale:0.###}",
                $"pointer_resolve_path={pointerTelemetry.PointerResolvePath}",
                $"pointer_hover_update_ms={pointerTelemetry.HoverUpdateMilliseconds:0.###}",
                $"pointer_target_resolve_ms={pointerTelemetry.PointerTargetResolveMilliseconds:0.###}",
                $"render_invalidation_requested_source={invalidation.RequestedSourceType}:{invalidation.RequestedSourceName}",
                $"render_invalidation_effective_source={invalidation.EffectiveSourceType}:{invalidation.EffectiveSourceName}",
                $"dirty_bounds_visual={invalidation.DirtyBoundsVisualType}:{invalidation.DirtyBoundsVisualName}",
                $"dirty_root_count={performance.DirtyRootCount}",
                $"dirty_root_summary={dirtyRootSummary}",
                $"dirty_regions={dirtyRegions}",
                $"dirty_bounds_trace={dirtyTrace}",
                $"inference={hotspotInference}"
            };

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllLines(logPath, lines);

            Assert.True(File.Exists(logPath));
        }
        finally
        {
            RestoreApplicationResources(backup);
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();
        }
    }

    private static (ControlsCatalogView Catalog, UiRoot UiRoot, ScrollViewer Viewer, Button TargetButton, Vector2 TargetPoint) CreateScrolledSidebarFixture(TestFrameClock clock)
    {
        var catalog = new ControlsCatalogView
        {
            Width = ViewportWidth,
            Height = ViewportHeight
        };
        var uiRoot = new UiRoot(catalog);
        clock.Advance(uiRoot, 16);
        clock.Advance(uiRoot, 16);
        clock.Advance(uiRoot, 16);

        var viewer = FindSidebarScrollViewer(catalog);
        viewer.ScrollToVerticalOffset(320f);
        clock.Advance(uiRoot, 16);
        clock.Advance(uiRoot, 16);

        var host = Assert.IsType<StackPanel>(catalog.FindName("ControlButtonsHost"));
        var (targetButton, targetPoint) = FindVisibleSidebarButtonHit(catalog, host, viewer);
        return (catalog, uiRoot, viewer, targetButton, targetPoint);
    }

    private static void HoverButton(UiRoot uiRoot, Button button, Vector2 point, TestFrameClock clock)
    {
        var safePoint = new Vector2(8f, 8f);
        MovePointer(uiRoot, safePoint);
        MovePointer(uiRoot, point);
        AdvanceFrames(uiRoot, clock, 12, 16);

        var transform = Assert.IsType<ScaleTransform>(button.RenderTransform);
        Assert.True(transform.ScaleX > 1f);
        Assert.True(button.IsMouseOver);
    }

    private static void Click(UiRoot uiRoot, Vector2 point)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftReleased: true));
    }

    private static void MovePointer(UiRoot uiRoot, Vector2 point)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, pointerMoved: true));
    }

    private static (Button Button, Vector2 Point) FindVisibleSidebarButtonHit(UIElement root, StackPanel host, ScrollViewer viewer)
    {
        var viewport = GetViewerViewportRect(viewer);
        var verticalBar = viewer.GetVisualChildren()
            .OfType<ScrollBar>()
            .First(static bar => bar.Orientation == Orientation.Vertical && bar.IsVisible);

        var minX = Math.Max(0, (int)MathF.Floor(viewport.X));
        var maxX = Math.Max(minX, (int)MathF.Ceiling(MathF.Min(verticalBar.LayoutSlot.X - 1f, viewport.X + viewport.Width)));
        var minY = Math.Max(0, (int)MathF.Floor(viewport.Y));
        var maxY = Math.Max(minY, (int)MathF.Ceiling(viewport.Y + viewport.Height));

        for (var y = minY; y < maxY; y += 2)
        {
            for (var x = minX; x < maxX; x += 2)
            {
                var point = new Vector2(x, y);
                var hit = VisualTreeHelper.HitTest(root, point);
                var button = FindAncestor<Button>(hit);
                if (button != null && host.Children.OfType<Button>().Contains(button))
                {
                    return (button, point);
                }
            }
        }

        throw new InvalidOperationException("Could not locate a visible sidebar button hit point.");
    }

    private static ScrollViewer FindSidebarScrollViewer(ControlsCatalogView catalog)
    {
        return FindFirstVisualChild<ScrollViewer>(
                   catalog,
                   static viewer => viewer.Content is StackPanel host &&
                                    string.Equals(host.Name, "ControlButtonsHost", StringComparison.Ordinal))
               ?? throw new InvalidOperationException("Could not find the sidebar ScrollViewer.");
    }

    private static LayoutRect GetViewerViewportRect(ScrollViewer viewer)
    {
        if (viewer.TryGetContentViewportClipRect(out var viewport))
        {
            return viewport;
        }

        throw new InvalidOperationException("Sidebar ScrollViewer did not expose a viewport.");
    }

    private static void PrimeRetainedRenderStateForDiagnostics(UIElement root, UiRoot uiRoot)
    {
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
    }

    private static void AdvanceFrames(UiRoot uiRoot, TestFrameClock clock, int frameCount, int elapsedMs)
    {
        for (var i = 0; i < frameCount; i++)
        {
            clock.Advance(uiRoot, elapsedMs);
        }
    }

    private static void RunFrame(UiRoot uiRoot, int totalMs, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(totalMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, ViewportWidth, ViewportHeight));
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
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static TElement? FindFirstVisualChild<TElement>(UIElement root, Func<TElement, bool>? predicate = null)
        where TElement : UIElement
    {
        if (root is TElement match && (predicate == null || predicate(match)))
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild(child, predicate);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TElement? FindAncestor<TElement>(UIElement? start)
        where TElement : UIElement
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement match)
            {
                return match;
            }
        }

        return null;
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"({rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##})";
    }

    private static string GetDiagnosticsLogPath(string fileNameWithoutExtension)
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "artifacts", "diagnostics", $"{fileNameWithoutExtension}.txt");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (current.EnumerateFiles("InkkSlinger.sln").Any())
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from the test base directory.");
    }

    private static ResourceSnapshot CaptureApplicationResources()
    {
        var resources = UiApplication.Current.Resources;
        return new ResourceSnapshot(resources.ToList(), resources.MergedDictionaries.ToList());
    }

    private static void RestoreApplicationResources(ResourceSnapshot snapshot)
    {
        TestApplicationResources.Restore(snapshot.Entries, snapshot.MergedDictionaries);
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
        Assert.True(File.Exists(appPath), $"Expected App.xml at '{appPath}'.");
        XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);
    }

    private readonly record struct ResourceSnapshot(
        IReadOnlyList<KeyValuePair<object, object>> Entries,
        IReadOnlyList<ResourceDictionary> MergedDictionaries);

    private sealed class TestFrameClock
    {
        private int _totalMilliseconds;

        public void Advance(UiRoot uiRoot, int elapsedMilliseconds)
        {
            _totalMilliseconds += elapsedMilliseconds;
            RunFrame(uiRoot, _totalMilliseconds, elapsedMilliseconds);
        }
    }
}
