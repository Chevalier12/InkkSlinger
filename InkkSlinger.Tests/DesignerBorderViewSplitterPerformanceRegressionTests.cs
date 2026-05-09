using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using InkkSlinger.Designer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.Tests;

public sealed class DesignerBorderViewSplitterPerformanceRegressionTests
{
    private const int ViewportWidth = 1280;
    private const int ViewportHeight = 820;
    private const double InteractiveStepBudgetMilliseconds = 16.7d;
    private readonly ITestOutputHelper _output;

    public DesignerBorderViewSplitterPerformanceRegressionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BorderViewPreviewAfterF5_PreviewSourceSplitterFullDrag_StaysWithinRealtimeBudget()
    {
        var snapshot = CaptureApplicationResources();
        try
        {
            LoadDesignerApplicationResources();

            var repositoryRoot = FindRepositoryRoot();
            var demoAppRoot = Path.Combine(repositoryRoot, "InkkSlinger.DemoApp");
            var shell = new DesignerShellView(projectSession: DesignerProjectSession.Open(demoAppRoot, new PhysicalDesignerProjectFileStore()))
            {
                SourceText = File.ReadAllText(Path.Combine(demoAppRoot, "Views", "BorderView.xml")),
                AppResourcesText = File.ReadAllText(Path.Combine(demoAppRoot, "App.xml"))
            };
            var uiRoot = new UiRoot(shell);

            RunFrames(uiRoot, 6);
            shell.ViewModel.SelectedEditorTabIndex = 1;
            var executedRefresh = InputGestureService.Execute(Keys.F5, ModifierKeys.None, shell, shell);
            RunFrames(uiRoot, 12);

            Assert.True(executedRefresh);
            Assert.True(shell.Controller.LastRefreshSucceeded, FormatDiagnostics(shell));
            Assert.NotNull(shell.Controller.PreviewRoot);
            Assert.Equal(1, shell.ViewModel.SelectedEditorTabIndex);

            var previewSourceSplitter = Assert.IsType<GridSplitter>(shell.FindName("PreviewSourceSplitter"));
            Assert.True(previewSourceSplitter.ActualHeight > 0f);
            var hierarchyScrollViewer = Assert.IsType<ScrollViewer>(shell.FindName("HierarchyScrollViewer"));
            _output.WriteLine(
                $"beforeDrag hierarchyViewport={hierarchyScrollViewer.ViewportWidth:0.###}x{hierarchyScrollViewer.ViewportHeight:0.###}, " +
                $"hierarchyExtent={hierarchyScrollViewer.ExtentWidth:0.###}x{hierarchyScrollViewer.ExtentHeight:0.###}, " +
                $"hierarchyBars={hierarchyScrollViewer.ComputedHorizontalScrollBarVisibility}/{hierarchyScrollViewer.ComputedVerticalScrollBarVisibility}");

            var metrics = DragSplitterWithTelemetry(
                uiRoot,
                previewSourceSplitter,
                hierarchyScrollViewer,
                new Vector2(96f, -560f),
                travelFrames: 8);

            _output.WriteLine(
                $"summary peakInputMs={metrics.PeakInputPhaseMilliseconds:0.###}, peakInputWallMs={metrics.PeakInputWallMilliseconds:0.###}, " +
                $"peakFrameWallMs={metrics.PeakFrameWallMilliseconds:0.###}, peakPointerRouteMs={metrics.PeakPointerRouteMilliseconds:0.###}, " +
                $"peakLayoutMs={metrics.PeakLayoutMilliseconds:0.###}, peakMeasureWorkMs={metrics.PeakMeasureWorkMilliseconds:0.###}, " +
                $"peakArrangeWorkMs={metrics.PeakArrangeWorkMilliseconds:0.###}, peakVisualUpdateMs={metrics.PeakVisualUpdateMilliseconds:0.###}, " +
                $"peakHitTests={metrics.PeakHitTests}, peakMeasureWork={metrics.PeakMeasureWork}, peakArrangeWork={metrics.PeakArrangeWork}, " +
                $"applyResizeMs={metrics.ApplyResizeMilliseconds:0.###}, applyResizeCalls={metrics.ApplyResizeCallCount}, " +
                $"lastPointerPath={metrics.LastPointerPath}");

            Assert.True(
                metrics.PeakFrameWallMilliseconds <= InteractiveStepBudgetMilliseconds,
                $"BorderView F5 preview bottom splitter drag exceeded a 60fps frame budget. {metrics}");
            Assert.True(
                metrics.PeakLayoutMilliseconds <= InteractiveStepBudgetMilliseconds,
                $"BorderView F5 preview bottom splitter drag exceeded a 60fps layout budget. {metrics}");
            Assert.True(
                metrics.PeakPointerRouteMilliseconds <= InteractiveStepBudgetMilliseconds,
                $"BorderView F5 preview bottom splitter route work exceeded a 60fps budget. {metrics}");
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    private DragMetrics DragSplitterWithTelemetry(
        UiRoot uiRoot,
        GridSplitter splitter,
        ScrollViewer hierarchyScrollViewer,
        Vector2 delta,
        int travelFrames)
    {
        var start = GetCenter(splitter.LayoutSlot);
        var end = start + delta;
        var beforeRuntime = splitter.GetGridSplitterSnapshotForDiagnostics();
        var peakInputMs = 0d;
        var peakInputWallMs = 0d;
        var peakFrameWallMs = 0d;
        var peakPointerRouteMs = 0d;
        var peakLayoutMs = 0d;
        var peakMeasureWorkMs = 0d;
        var peakArrangeWorkMs = 0d;
        var peakVisualUpdateMs = 0d;
        var peakHitTests = 0;
        var peakMeasureWork = 0L;
        var peakArrangeWork = 0L;
        var lastPointerPath = string.Empty;
        var stopwatch = new Stopwatch();

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        RunFrames(uiRoot, 1);

        for (var step = 1; step <= travelFrames; step++)
        {
            var pointer = Vector2.Lerp(start, end, step / (float)travelFrames);

            stopwatch.Restart();
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
            stopwatch.Stop();
            var inputWallMs = stopwatch.Elapsed.TotalMilliseconds;
            var input = uiRoot.GetInputMetricsSnapshot();
            var pointerPath = uiRoot.LastPointerResolvePathForDiagnostics;
            var dirtyQueueBeforeFrame = uiRoot.GetDirtyRenderQueueSummaryForTests(limit: 16);

            stopwatch.Restart();
            RunFrames(uiRoot, 1);
            stopwatch.Stop();
            var frameWallMs = stopwatch.Elapsed.TotalMilliseconds;

            var perf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
            var treeWork = uiRoot.GetVisualTreeWorkMetricsSnapshotForTests();
            var retained = uiRoot.GetRetainedRenderControllerTelemetrySnapshotForTests();
            var invalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
            var hierarchyScroll = hierarchyScrollViewer.GetScrollViewerSnapshotForDiagnostics();
            var compositionRefreshNodes = uiRoot.GetRetainedCompositionRefreshNodeCountForTests();

            peakInputMs = Math.Max(peakInputMs, input.LastInputPhaseMilliseconds);
            peakInputWallMs = Math.Max(peakInputWallMs, inputWallMs);
            peakFrameWallMs = Math.Max(peakFrameWallMs, frameWallMs);
            peakPointerRouteMs = Math.Max(peakPointerRouteMs, input.LastInputPointerRouteMilliseconds);
            peakLayoutMs = Math.Max(peakLayoutMs, perf.LayoutPhaseMilliseconds);
            peakMeasureWorkMs = Math.Max(peakMeasureWorkMs, perf.LayoutMeasureWorkMilliseconds);
            peakArrangeWorkMs = Math.Max(peakArrangeWorkMs, perf.LayoutArrangeWorkMilliseconds);
            peakVisualUpdateMs = Math.Max(peakVisualUpdateMs, perf.VisualUpdateMilliseconds);
            peakHitTests = Math.Max(peakHitTests, input.HitTestCount);
            peakMeasureWork = Math.Max(peakMeasureWork, treeWork.MeasureWorkCount);
            peakArrangeWork = Math.Max(peakArrangeWork, treeWork.ArrangeWorkCount);
            lastPointerPath = pointerPath;

            _output.WriteLine(
                $"step={step:00} inputMs={input.LastInputPhaseMilliseconds:0.###}, inputWallMs={inputWallMs:0.###}, " +
                $"frameWallMs={frameWallMs:0.###}, pointerRouteMs={input.LastInputPointerRouteMilliseconds:0.###}, " +
                $"layoutMs={perf.LayoutPhaseMilliseconds:0.###}, measureWorkMs={perf.LayoutMeasureWorkMilliseconds:0.###}, " +
                $"arrangeWorkMs={perf.LayoutArrangeWorkMilliseconds:0.###}, visualUpdateMs={perf.VisualUpdateMilliseconds:0.###}, " +
                $"renderSchedulingMs={perf.RenderSchedulingPhaseMilliseconds:0.###}, compBuildMs={retained.LastCompositionSyncBuildMilliseconds:0.###}, " +
                $"compRebuilds={retained.CompositionRebuildCount}, compMode={retained.LastCompositionPrimaryMode}/{retained.LastCompositionPrimaryReason}, " +
                $"subtreeSyncMs={perf.RetainedSubtreeUpdateMilliseconds:0.###}, shallowMs={perf.RetainedShallowSyncMilliseconds:0.###}, deepMs={perf.RetainedDeepSyncMilliseconds:0.###}, " +
                $"ancestorMs={perf.RetainedAncestorRefreshMilliseconds:0.###}, forceDeep={perf.RetainedForceDeepSyncCount}, shallowOk={perf.RetainedShallowSuccessCount}, " +
                $"rejectState={perf.RetainedShallowRejectRenderStateCount}, rejectStruct={perf.RetainedShallowRejectStructureCount}, rejectVis={perf.RetainedShallowRejectVisibilityCount}, blocked={perf.RetainedForcedDeepDowngradeBlockedCount}, " +
                $"lastInvalidation={retained.LastInvalidationKind}:{retained.LastInvalidationSource}, lastMetadata={retained.LastCompositionMetadataUpdateKind}:{retained.LastCompositionMetadataUpdateSource}, " +
                $"syncRoots={uiRoot.GetLastSynchronizedDirtyRootSummaryForTests()}, " +
                $"measureWork={treeWork.MeasureWorkCount}, arrangeWork={treeWork.ArrangeWorkCount}, hitTests={input.HitTestCount}, " +
                $"pointerPath={pointerPath}, dirtyQueueBefore={dirtyQueueBeforeFrame}, dirtyRoots={perf.DirtyRootCount}, retainedTraversal={perf.RetainedTraversalCount}, " +
                $"compositionRefreshNodes={compositionRefreshNodes}, " +
                $"hierarchyViewport={hierarchyScrollViewer.ViewportWidth:0.###}x{hierarchyScrollViewer.ViewportHeight:0.###}, " +
                $"hierarchyExtent={hierarchyScrollViewer.ExtentWidth:0.###}x{hierarchyScrollViewer.ExtentHeight:0.###}, " +
                $"hierarchyBars={hierarchyScrollViewer.ComputedHorizontalScrollBarVisibility}/{hierarchyScrollViewer.ComputedVerticalScrollBarVisibility}, " +
                $"hierarchyArrangeContentMs={hierarchyScroll.ArrangeContentForCurrentOffsetsMilliseconds:0.###}, " +
                $"retainedSource={invalidation.RetainedSyncSourceType}#{invalidation.RetainedSyncSourceName}:{invalidation.RetainedSyncSourceResolution}, " +
                $"hottestMeasure={perf.HottestLayoutMeasureElementType}#{perf.HottestLayoutMeasureElementName}:{perf.HottestLayoutMeasureElementMilliseconds:0.###}ms, " +
                $"hottestArrange={perf.HottestLayoutArrangeElementType}#{perf.HottestLayoutArrangeElementName}:{perf.HottestLayoutArrangeElementMilliseconds:0.###}ms");
        }

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));
        RunFrames(uiRoot, 1);
        var afterRuntime = splitter.GetGridSplitterSnapshotForDiagnostics();

        return new DragMetrics(
            peakInputMs,
            peakInputWallMs,
            peakFrameWallMs,
            peakPointerRouteMs,
            peakLayoutMs,
            peakMeasureWorkMs,
            peakArrangeWorkMs,
            peakVisualUpdateMs,
            peakHitTests,
            peakMeasureWork,
            peakArrangeWork,
            afterRuntime.ApplyResizeMilliseconds - beforeRuntime.ApplyResizeMilliseconds,
            afterRuntime.ApplyResizeCallCount - beforeRuntime.ApplyResizeCallCount,
            lastPointerPath);
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
            PressedKeys = [],
            ReleasedKeys = [],
            TextInput = [],
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

    private static void RunFrames(UiRoot uiRoot, int frameCount)
    {
        for (var frame = 0; frame < frameCount; frame++)
        {
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, ViewportWidth, ViewportHeight));
        }
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static Dictionary<object, object> CaptureApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
    }

    private static void RestoreApplicationResources(Dictionary<object, object> snapshot)
    {
        TestApplicationResources.Restore(snapshot);
    }

    private static void LoadDesignerApplicationResources()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appXmlPath = Path.Combine(repositoryRoot, "InkkSlinger.Designer", "App.xml");
        Assert.True(File.Exists(appXmlPath), $"Expected Designer App.xml to exist at '{appXmlPath}'.");
        XamlLoader.LoadApplicationResourcesFromFile(appXmlPath, clearExisting: true);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "InkkSlinger.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate InkkSlinger.sln from test assembly base directory.");
    }

    private static string FormatDiagnostics(DesignerShellView shell)
    {
        return string.Join(
            Environment.NewLine,
            shell.Controller.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Source}:{diagnostic.Code}:{diagnostic.LocationText}:{diagnostic.Message}"));
    }

    private readonly record struct DragMetrics(
        double PeakInputPhaseMilliseconds,
        double PeakInputWallMilliseconds,
        double PeakFrameWallMilliseconds,
        double PeakPointerRouteMilliseconds,
        double PeakLayoutMilliseconds,
        double PeakMeasureWorkMilliseconds,
        double PeakArrangeWorkMilliseconds,
        double PeakVisualUpdateMilliseconds,
        int PeakHitTests,
        long PeakMeasureWork,
        long PeakArrangeWork,
        double ApplyResizeMilliseconds,
        long ApplyResizeCallCount,
        string LastPointerPath);
}
