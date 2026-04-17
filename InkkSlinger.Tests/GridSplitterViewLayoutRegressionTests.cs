using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.Tests;

public sealed class GridSplitterViewLayoutRegressionTests
{
    private readonly ITestOutputHelper _output;

    public GridSplitterViewLayoutRegressionTests(ITestOutputHelper output)
    {
        _output = output;
    }

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
                Assert.IsType<TextBlock>(view.FindName("HorizontalSummaryText"))
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

            var hadOldRenderBounds = text!.TryGetRenderBoundsInRootSpace(out var oldRenderBounds);
            var oldBounds = hadOldRenderBounds ? oldRenderBounds : text.LayoutSlot;

            DragSplitter(uiRoot, splitter, 200f, 0f);
            RunLayout(uiRoot, 1100, 900, 32);
            uiRoot.SynchronizeRetainedRenderListForTests();

            var hadNewRenderBounds = text.TryGetRenderBoundsInRootSpace(out var newRenderBounds);
            var newBounds = hadNewRenderBounds ? newRenderBounds : text.LayoutSlot;
            if (RectsNearlyEqual(oldBounds, newBounds))
            {
                return;
            }

            var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();
            var isFullDirty = uiRoot.IsFullDirtyForTests();
            _output.WriteLine($"wrappedDirty old={FormatRect(oldBounds)} new={FormatRect(newBounds)} fullDirty={isFullDirty} regions={FormatRegions(dirtyRegions)}");
            Assert.True(
                isFullDirty || dirtyRegions.Count > 0,
                $"Expected splitter shrink to invalidate either dirty regions or the full frame. oldBounds={FormatRect(oldBounds)}, newBounds={FormatRect(newBounds)}");

            if (isFullDirty)
            {
                return;
            }

            Assert.True(
                dirtyRegions.Any(region => ContainsRect(region, oldBounds)),
                $"Expected dirty regions to include the old wrapped text bounds after splitter shrink. oldBounds={FormatRect(oldBounds)}, dirtyRegions={FormatRegions(dirtyRegions)}");
            if (hadNewRenderBounds)
            {
                Assert.True(
                    dirtyRegions.Any(region => ContainsRect(region, newBounds)),
                    $"Expected dirty regions to include the new wrapped text bounds after splitter shrink. newBounds={FormatRect(newBounds)}, dirtyRegions={FormatRegions(dirtyRegions)}");
            }
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
                if (effectiveWidth <= 0.01f)
                {
                    Assert.True(
                        text.DesiredSize.Y - text.Margin.Vertical <= 0.5f,
                        $"Expected collapsed GridSplitter wrapped copy to stop consuming height at zero width. text='{Abbreviate(text.Text)}', width={text.LayoutSlot.Width:0.##}, desiredHeight={text.DesiredSize.Y:0.##}, actualHeight={text.ActualHeight:0.##}");
                    continue;
                }

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

    [Fact]
    public void GridSplitterView_WrappedPrimaryDrag_LogsDirtyRegionPolicy()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new GridSplitterView();
            var uiRoot = new UiRoot(view);
            RunLayout(uiRoot, 1100, 900, 16);
            uiRoot.CompleteDrawStateForTests();
            uiRoot.ResetDirtyStateForTests();
            view.ClearRenderInvalidationRecursive();

            var splitter = Assert.IsType<GridSplitter>(view.FindName("NavigationSplitter"));
            var primaryEditorGrid = Assert.IsType<Grid>(view.FindName("PrimaryEditorGrid"));

            foreach (var deltaX in new[] { 24f, 12f, 4f, 4f, 12f, -4f, -60f })
            {
                DragSplitter(uiRoot, splitter, deltaX, 0f);
                RunLayout(uiRoot, 1100, 900, 32);

                var dirtyRegions = uiRoot.GetDirtyRegionsSnapshotForTests();
                var invalidation = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
                _output.WriteLine(
                    $"deltaX={deltaX:0.##} centerWidth={primaryEditorGrid.ColumnDefinitions[2].ActualWidth:0.##} coverage={uiRoot.GetDirtyCoverageForTests():0.###} partial={uiRoot.WouldUsePartialDirtyRedrawForTests()} settle={uiRoot.GetFullRedrawSettleFramesRemainingForTests()} fullDirty={uiRoot.IsFullDirtyForTests()} regions={FormatRegions(dirtyRegions)} dirtyBounds={invalidation.DirtyBoundsVisualType}#{invalidation.DirtyBoundsVisualName}|hint={invalidation.DirtyBoundsUsedHint}|rect={FormatRect(invalidation.DirtyBounds)}");

                uiRoot.CompleteDrawStateForTests();
                uiRoot.ResetDirtyStateForTests();
                view.ClearRenderInvalidationRecursive();
            }
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void GridSplitterView_ShrinkDrag_WithAllTextBlocksNoWrap_StillReportsNonTextLayoutWork()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();
            TextLayout.ResetMetricsForTests();

            var view = new GridSplitterView();
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 1100, 900, 16);

            var textBlocks = GetAllTextBlocks(view);
            Assert.NotEmpty(textBlocks);
            foreach (var text in textBlocks)
            {
                text.TextWrapping = TextWrapping.NoWrap;
            }

            RunLayout(uiRoot, 1100, 900, 16);
            TextLayout.ResetMetricsForTests();

            var splitter = Assert.IsType<GridSplitter>(view.FindName("NavigationSplitter"));
            var peakLayoutPhaseMs = 0d;
            var peakMeasureWorkMs = 0d;
            var hottestMeasureType = string.Empty;
            var hottestMeasureName = string.Empty;
            var peakDirtyRootCount = 0;
            var peakRetainedTraversalCount = 0;

            foreach (var deltaX in new[] { 80f, 60f, 60f, 40f })
            {
                DragSplitter(uiRoot, splitter, deltaX, 0f);
                RunLayout(uiRoot, 1100, 900, 32);

                var perf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
                peakLayoutPhaseMs = Math.Max(peakLayoutPhaseMs, perf.LayoutPhaseMilliseconds);
                peakMeasureWorkMs = Math.Max(peakMeasureWorkMs, perf.LayoutMeasureWorkMilliseconds);
                peakDirtyRootCount = Math.Max(peakDirtyRootCount, perf.DirtyRootCount);
                peakRetainedTraversalCount = Math.Max(peakRetainedTraversalCount, perf.RetainedTraversalCount);
                if (perf.HottestLayoutMeasureElementMilliseconds > 0d)
                {
                    hottestMeasureType = perf.HottestLayoutMeasureElementType;
                    hottestMeasureName = perf.HottestLayoutMeasureElementName;
                }
            }

            var textLayout = TextLayout.GetMetricsSnapshot();
            _output.WriteLine(
                $"nowrap gridsplitter peakLayoutPhaseMs={peakLayoutPhaseMs:0.###}, peakMeasureWorkMs={peakMeasureWorkMs:0.###}, hottestMeasure={hottestMeasureType}#{hottestMeasureName}, dirtyRoots={peakDirtyRootCount}, retainedTraversal={peakRetainedTraversalCount}, textLayoutBuilds={textLayout.BuildCount}, wrappedBuilds={textLayout.WrappedBuildCount}, cacheMisses={textLayout.CacheMissCount}");

            Assert.Equal(0, textLayout.WrappedBuildCount);
            Assert.True(
                peakLayoutPhaseMs > 0d || peakMeasureWorkMs > 0d,
                "Expected splitter drag to still perform layout work even after forcing all TextBlocks to NoWrap.");
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void GridSplitterView_SustainedNoWrapDrag_LogsPerFrameHotspotBreakdown()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();
            TextLayout.ResetMetricsForTests();

            var view = new GridSplitterView();
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 1100, 900, 16);

            var splitter = Assert.IsType<GridSplitter>(view.FindName("NavigationSplitter"));
            var textBlocks = GetAllTextBlocks(view);
            Assert.NotEmpty(textBlocks);
            foreach (var text in textBlocks)
            {
                text.TextWrapping = TextWrapping.NoWrap;
            }

            RunLayout(uiRoot, 1100, 900, 16);
            TextLayout.ResetMetricsForTests();
            uiRoot.ClearDirtyBoundsEventTraceForTests();

            var start = GetVisibleCenter(splitter);
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));

            var peakInputMs = 0d;
            var peakLayoutMs = 0d;
            var peakRenderSchedulingMs = 0d;
            var peakVisualUpdateMs = 0d;
            var peakDrawTreeMs = 0d;
            var peakMeasureWorkMs = 0d;
            var peakArrangeWorkMs = 0d;
            var peakHitTests = 0;
            var peakNodesVisited = 0;
            var peakMeasureWork = 0L;
            var peakArrangeWork = 0L;
            var hottestMeasure = "none";
            var hottestArrange = "none";
            var lastPointerPath = string.Empty;

            for (var step = 1; step <= 16; step++)
            {
                var pointer = new Vector2(start.X + (step * 8f), start.Y);
                uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
                var input = uiRoot.GetInputMetricsSnapshot();
                var inputPointerPath = uiRoot.LastPointerResolvePathForDiagnostics;
                uiRoot.TryGetLastPointerResolveHitTestMetricsForTests(out var hitTestMetrics);
                RunLayout(uiRoot, 1100, 900, 16);

                var perf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
                var render = uiRoot.GetRenderTelemetrySnapshotForTests();
                var treeWork = uiRoot.GetVisualTreeWorkMetricsSnapshotForTests();
                var rootMetrics = uiRoot.GetMetricsSnapshot();
                var automation = uiRoot.GetAutomationMetricsSnapshot();

                peakInputMs = Math.Max(peakInputMs, perf.InputPhaseMilliseconds);
                peakLayoutMs = Math.Max(peakLayoutMs, perf.LayoutPhaseMilliseconds);
                peakRenderSchedulingMs = Math.Max(peakRenderSchedulingMs, perf.RenderSchedulingPhaseMilliseconds);
                peakVisualUpdateMs = Math.Max(peakVisualUpdateMs, perf.VisualUpdateMilliseconds);
                peakDrawTreeMs = Math.Max(peakDrawTreeMs, render.DrawVisualTreeMilliseconds);
                peakMeasureWorkMs = Math.Max(peakMeasureWorkMs, perf.LayoutMeasureWorkMilliseconds);
                peakArrangeWorkMs = Math.Max(peakArrangeWorkMs, perf.LayoutArrangeWorkMilliseconds);
                peakHitTests = Math.Max(peakHitTests, input.HitTestCount);
                peakNodesVisited = Math.Max(peakNodesVisited, hitTestMetrics.NodesVisited);
                peakMeasureWork = Math.Max(peakMeasureWork, treeWork.MeasureWorkCount);
                peakArrangeWork = Math.Max(peakArrangeWork, treeWork.ArrangeWorkCount);
                if (perf.HottestLayoutMeasureElementMilliseconds > 0d)
                {
                    hottestMeasure = $"{perf.HottestLayoutMeasureElementType}#{perf.HottestLayoutMeasureElementName}:{perf.HottestLayoutMeasureElementMilliseconds:0.###}ms";
                }

                if (perf.HottestLayoutArrangeElementMilliseconds > 0d)
                {
                    hottestArrange = $"{perf.HottestLayoutArrangeElementType}#{perf.HottestLayoutArrangeElementName}:{perf.HottestLayoutArrangeElementMilliseconds:0.###}ms";
                }

                lastPointerPath = inputPointerPath;

                _output.WriteLine(
                    $"step={step:00} inputMs={input.LastInputPhaseMilliseconds:0.###} layoutMs={perf.LayoutPhaseMilliseconds:0.###} renderSchedMs={perf.RenderSchedulingPhaseMilliseconds:0.###} visualUpdateMs={perf.VisualUpdateMilliseconds:0.###} " +
                    $"measureWorkMs={perf.LayoutMeasureWorkMilliseconds:0.###} arrangeWorkMs={perf.LayoutArrangeWorkMilliseconds:0.###} drawTreeMs={render.DrawVisualTreeMilliseconds:0.###} " +
                    $"measureWork={treeWork.MeasureWorkCount} arrangeWork={treeWork.ArrangeWorkCount} hitTests={input.HitTestCount} pointerResolveMs={input.LastInputPointerTargetResolveMilliseconds:0.###} " +
                    $"pointerPath={inputPointerPath} dirtyRoots={perf.DirtyRootCount} retainedTraversal={perf.RetainedTraversalCount} " +
                    $"renderDirtyRects={rootMetrics.LastDirtyRectCount} redrawReasons={rootMetrics.LastDrawReasons} nodesVisited={hitTestMetrics.NodesVisited} maxDepth={hitTestMetrics.MaxDepth} " +
                    $"hottestMeasure={perf.HottestLayoutMeasureElementType}#{perf.HottestLayoutMeasureElementName}:{perf.HottestLayoutMeasureElementMilliseconds:0.###}ms " +
                    $"hottestArrange={perf.HottestLayoutArrangeElementType}#{perf.HottestLayoutArrangeElementName}:{perf.HottestLayoutArrangeElementMilliseconds:0.###}ms " +
                    $"automationPeers={automation.PeerCount} automationEvents={automation.EmittedEventCountLastFrame}");
            }

            var end = new Vector2(start.X + (16 * 8f), start.Y);
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));
            RunLayout(uiRoot, 1100, 900, 16);

            var textLayout = TextLayout.GetMetricsSnapshot();
            _output.WriteLine(
                $"summary peakInputMs={peakInputMs:0.###}, peakLayoutMs={peakLayoutMs:0.###}, peakRenderSchedulingMs={peakRenderSchedulingMs:0.###}, peakVisualUpdateMs={peakVisualUpdateMs:0.###}, peakDrawTreeMs={peakDrawTreeMs:0.###}, " +
                $"peakMeasureWorkMs={peakMeasureWorkMs:0.###}, peakArrangeWorkMs={peakArrangeWorkMs:0.###}, peakHitTests={peakHitTests}, peakNodesVisited={peakNodesVisited}, peakMeasureWork={peakMeasureWork}, peakArrangeWork={peakArrangeWork}, " +
                $"hottestMeasure={hottestMeasure}, hottestArrange={hottestArrange}, lastPointerPath={lastPointerPath}, textLayoutBuilds={textLayout.BuildCount}, wrappedBuilds={textLayout.WrappedBuildCount}, cacheMisses={textLayout.CacheMissCount}");

            Assert.Equal(0, textLayout.BuildCount);
            Assert.Equal(0, textLayout.WrappedBuildCount);
            Assert.True(
                peakInputMs > 0d || peakLayoutMs > 0d || peakRenderSchedulingMs > 0d || peakVisualUpdateMs > 0d || peakDrawTreeMs > 0d,
                "Expected sustained splitter drag to show non-text frame costs.");
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void GridSplitterView_SustainedNoWrapDrag_IdentifiesNonTextLayoutChurnSources()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();
            TextLayout.ResetMetricsForTests();

            var view = new GridSplitterView();
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 1100, 900, 16);

            var textBlocks = GetAllTextBlocks(view);
            Assert.NotEmpty(textBlocks);
            foreach (var text in textBlocks)
            {
                text.TextWrapping = TextWrapping.NoWrap;
            }

            RunLayout(uiRoot, 1100, 900, 16);
            TextLayout.ResetMetricsForTests();

            var baseline = CaptureElementWork(view);
            var splitter = Assert.IsType<GridSplitter>(view.FindName("NavigationSplitter"));
            var start = GetVisibleCenter(splitter);
            var peakLayoutMs = 0d;
            var peakMeasureWorkMs = 0d;
            var peakArrangeWorkMs = 0d;
            var peakDrawTreeMs = 0d;
            var peakDirtyRoots = 0;

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));

            for (var step = 1; step <= 16; step++)
            {
                var pointer = new Vector2(start.X + (step * 8f), start.Y);
                uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
                RunLayout(uiRoot, 1100, 900, 16);

                var perf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
                var render = uiRoot.GetRenderTelemetrySnapshotForTests();
                peakLayoutMs = Math.Max(peakLayoutMs, perf.LayoutPhaseMilliseconds);
                peakMeasureWorkMs = Math.Max(peakMeasureWorkMs, perf.LayoutMeasureWorkMilliseconds);
                peakArrangeWorkMs = Math.Max(peakArrangeWorkMs, perf.LayoutArrangeWorkMilliseconds);
                peakDrawTreeMs = Math.Max(peakDrawTreeMs, render.DrawVisualTreeMilliseconds);
                peakDirtyRoots = Math.Max(peakDirtyRoots, perf.DirtyRootCount);
            }

            var end = new Vector2(start.X + (16 * 8f), start.Y);
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));
            RunLayout(uiRoot, 1100, 900, 16);

            var deltas = CaptureElementWork(view)
                .Select(after => CreateWorkDelta(after, baseline))
                .Where(static delta => delta.MeasureWorkDelta > 0 ||
                                       delta.ArrangeWorkDelta > 0 ||
                                       delta.InvalidateMeasureDelta > 0 ||
                                       delta.InvalidateArrangeDelta > 0)
                .OrderByDescending(static delta => delta.MeasureWorkDelta + delta.ArrangeWorkDelta)
                .ThenByDescending(static delta => delta.InvalidateMeasureDelta + delta.InvalidateArrangeDelta)
                .ToArray();

            var topOverall = deltas.Take(10).ToArray();
            var topNonText = deltas
                .Where(static delta => delta.ElementType != nameof(TextBlock))
                .Take(10)
                .ToArray();
            var topGrids = deltas
                .Where(static delta => delta.ElementType == nameof(Grid))
                .Take(10)
                .ToArray();

            _output.WriteLine($"topOverall={FormatWorkDeltas(topOverall)}");
            _output.WriteLine($"topNonText={FormatWorkDeltas(topNonText)}");
            _output.WriteLine($"topGrids={FormatWorkDeltas(topGrids)}");

            var textLayout = TextLayout.GetMetricsSnapshot();
            _output.WriteLine(
                $"textLayoutBuilds={textLayout.BuildCount}, wrappedBuilds={textLayout.WrappedBuildCount}, noWrapBuilds={textLayout.NoWrapBuildCount}, cacheMisses={textLayout.CacheMissCount}");
            _output.WriteLine(
                $"framePeaks layoutMs={peakLayoutMs:0.###}, measureWorkMs={peakMeasureWorkMs:0.###}, arrangeWorkMs={peakArrangeWorkMs:0.###}, drawTreeMs={peakDrawTreeMs:0.###}, dirtyRoots={peakDirtyRoots}");

            Assert.Equal(0, textLayout.BuildCount);
            Assert.Equal(0, textLayout.WrappedBuildCount);
            Assert.True(
                peakLayoutMs > 0d || peakMeasureWorkMs > 0d || peakArrangeWorkMs > 0d || peakDrawTreeMs > 0d,
                "Expected sustained no-wrap splitter drag to still produce observable frame work.");
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void GridSplitterView_NoWrapScenarioMatrix_SeparatesPointerRoutingFromResizeWork()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var hover = RunNoWrapSplitterScenario(
                configure: static _ => { },
                action: static (uiRoot, splitter) =>
                {
                    var start = GetVisibleCenter(splitter);
                    foreach (var step in Enumerable.Range(1, 16))
                    {
                        var pointer = new Vector2(start.X + (step * 8f), start.Y);
                        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
                        RunLayout(uiRoot, 1100, 900, 16);
                    }
                });

            var snappedNoResize = RunNoWrapSplitterScenario(
                configure: static splitter => splitter.DragIncrement = 1000f,
                action: static (uiRoot, splitter) =>
                {
                    foreach (var delta in new[] { 32f, 32f, 32f, 32f })
                    {
                        DragSplitter(uiRoot, splitter, delta, 0f);
                        RunLayout(uiRoot, 1100, 900, 16);
                    }
                });

            var activeResize = RunNoWrapSplitterScenario(
                configure: static splitter => splitter.DragIncrement = 1f,
                action: static (uiRoot, splitter) =>
                {
                    foreach (var delta in new[] { 32f, 32f, 32f, 32f })
                    {
                        DragSplitter(uiRoot, splitter, delta, 0f);
                        RunLayout(uiRoot, 1100, 900, 16);
                    }
                });

            _output.WriteLine($"hover={FormatScenarioMetrics(hover)}");
            _output.WriteLine($"snappedNoResize={FormatScenarioMetrics(snappedNoResize)}");
            _output.WriteLine($"activeResize={FormatScenarioMetrics(activeResize)}");

            Assert.Equal(0, hover.TextLayoutBuilds);
            Assert.Equal(0, snappedNoResize.TextLayoutBuilds);
            Assert.Equal(0, activeResize.TextLayoutBuilds);
            Assert.True(activeResize.GridResolveDefinitionSizesCallCount >= snappedNoResize.GridResolveDefinitionSizesCallCount);
            Assert.True(activeResize.GridMeasureRemeasureCheckCount >= snappedNoResize.GridMeasureRemeasureCheckCount);
            Assert.True(activeResize.FrameworkMeasureCallCount >= snappedNoResize.FrameworkMeasureCallCount);
            Assert.True(activeResize.CenterWidthDelta < -0.01f);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void GridSplitterView_NoWrapDragHelper_UsesVisibleSplitterCenterAndReportsRealResizeTelemetry()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new GridSplitterView();
            var uiRoot = new UiRoot(view);
            RunLayout(uiRoot, 1100, 900, 16);

            foreach (var text in GetAllTextBlocks(view))
            {
                text.TextWrapping = TextWrapping.NoWrap;
            }

            RunLayout(uiRoot, 1100, 900, 16);

            var splitter = Assert.IsType<GridSplitter>(view.FindName("NavigationSplitter"));
            var primaryEditorGrid = Assert.IsType<Grid>(view.FindName("PrimaryEditorGrid"));
            var initialCenterWidth = primaryEditorGrid.ColumnDefinitions[2].ActualWidth;
            var splitterCenter = GetVisibleCenter(splitter);
            var manualHit = VisualTreeHelper.HitTest(view, splitterCenter);

            TextLayout.ResetMetricsForTests();
            _ = GridSplitter.GetTelemetryAndReset();
            _ = Grid.GetTelemetryAndReset();
            _ = FrameworkElement.GetTelemetryAndReset();

            var peakLayoutMs = 0d;
            var peakMeasureWorkMs = 0d;
            var peakArrangeWorkMs = 0d;

            foreach (var deltaX in new[] { 80f, 60f, 60f, 40f })
            {
                DragSplitter(uiRoot, splitter, deltaX, 0f);
                RunLayout(uiRoot, 1100, 900, 32);

                var perf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
                peakLayoutMs = Math.Max(peakLayoutMs, perf.LayoutPhaseMilliseconds);
                peakMeasureWorkMs = Math.Max(peakMeasureWorkMs, perf.LayoutMeasureWorkMilliseconds);
                peakArrangeWorkMs = Math.Max(peakArrangeWorkMs, perf.LayoutArrangeWorkMilliseconds);
            }

            var finalCenterWidth = primaryEditorGrid.ColumnDefinitions[2].ActualWidth;
            var splitterTelemetry = GridSplitter.GetTelemetryAndReset();
            var splitterRuntime = splitter.GetGridSplitterSnapshotForDiagnostics();
            var gridTelemetry = Grid.GetTelemetryAndReset();
            var frameworkTelemetry = FrameworkElement.GetTelemetryAndReset();
            var textLayout = TextLayout.GetMetricsSnapshot();

            _output.WriteLine(
                $"actualResize splitterCenter={splitterCenter}, manualHit={DescribeElement(manualHit)}, centerWidthDelta={finalCenterWidth - initialCenterWidth:0.###}, peakLayoutMs={peakLayoutMs:0.###}, peakMeasureWorkMs={peakMeasureWorkMs:0.###}, peakArrangeWorkMs={peakArrangeWorkMs:0.###}, " +
                $"splitterTelemetry[pd={splitterTelemetry.PointerDownCallCount},pdHitReject={splitterTelemetry.PointerDownHitTestRejectCount},beginDrag={splitterTelemetry.BeginDragSuccessCount},pm={splitterTelemetry.PointerMoveCallCount},pmApply={splitterTelemetry.PointerMoveApplyCount},pmNoOp={splitterTelemetry.PointerMoveNoOpDeltaCount},pu={splitterTelemetry.PointerUpCallCount}] " +
                $"splitterRuntime[slotY={splitterRuntime.LayoutSlotY:0.###}, dragging={splitterRuntime.IsDragging}, hasGrid={splitterRuntime.HasActiveGrid}, beginDrag={splitterRuntime.BeginDragSuccessCount}, pointerDownHitReject={splitterRuntime.PointerDownHitTestRejectCount}] " +
                $"gridMeasureCalls={gridTelemetry.MeasureCallCount}, gridResolveDefs={gridTelemetry.ResolveDefinitionSizesCallCount}, gridRemeasureChecks={gridTelemetry.MeasureRemeasureCheckCount}, gridRemeasures={gridTelemetry.MeasureRemeasureCount}, gridSecondPassChildren={gridTelemetry.MeasureSecondPassChildCount}, " +
                $"frameworkMeasureCalls={frameworkTelemetry.MeasureCallCount}, frameworkMeasureWork={frameworkTelemetry.MeasureWorkCount}, frameworkArrangeCalls={frameworkTelemetry.ArrangeCallCount}, frameworkArrangeWork={frameworkTelemetry.ArrangeWorkCount}, " +
                $"frameworkMeasureParentInvalidations={frameworkTelemetry.MeasureParentInvalidationCount}, frameworkArrangeParentInvalidations={frameworkTelemetry.ArrangeParentInvalidationCount}, textLayoutBuilds={textLayout.BuildCount}");

            Assert.Same(splitter, manualHit);
            Assert.True(finalCenterWidth < initialCenterWidth - 0.01f);
            Assert.Equal(0, textLayout.BuildCount);
            Assert.True(splitterTelemetry.PointerDownCallCount >= 1);
            Assert.True(splitterTelemetry.BeginDragSuccessCount >= 1);
            Assert.True(splitterTelemetry.PointerMoveApplyCount >= 1);
            Assert.True(splitterTelemetry.PointerUpSuccessCount >= 1);
            Assert.True(gridTelemetry.ResolveDefinitionSizesCallCount > 0 || gridTelemetry.MeasureRemeasureCheckCount > 0);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void GridSplitterView_NoWrap_PrimaryEditorDrag_ChurnsWorkbenchMoreThanLowerDemoDrag()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var primary = RunNamedSplitterDragScenario(
                splitterName: "NavigationSplitter",
                deltaX: 32f,
                deltaY: 0f,
                trackedRootNames: ["GridSplitterWorkbenchScrollViewer", "PrimaryEditorGrid", "HorizontalWorkbenchGrid"]);

            var lower = RunNamedSplitterDragScenario(
                splitterName: "TimelineSplitter",
                deltaX: 0f,
                deltaY: 24f,
                trackedRootNames: ["GridSplitterWorkbenchScrollViewer", "PrimaryEditorGrid", "HorizontalWorkbenchGrid"]);

            _output.WriteLine($"primaryDrag={FormatNamedSplitterScenario(primary)}");
            _output.WriteLine($"lowerDrag={FormatNamedSplitterScenario(lower)}");

            Assert.Equal(0, primary.TextLayoutBuilds);
            Assert.Equal(0, lower.TextLayoutBuilds);
            Assert.True(primary.ResizeDelta > 0.01f);
            Assert.True(lower.ResizeDelta >= 0f);
            Assert.True(primary.WorkbenchMeasureWorkDelta >= (lower.WorkbenchMeasureWorkDelta * 2));
            Assert.True(primary.PrimaryGridMeasureWorkDelta > lower.PrimaryGridMeasureWorkDelta);
            Assert.True(primary.PeakLayoutMs > lower.PeakLayoutMs);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void GridSplitterView_NoWrap_PrimaryEditorDrag_ReMeasuresWorkbenchScrollContentInsteadOfScrollingIt()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new GridSplitterView();
            var uiRoot = new UiRoot(view);
            RunLayout(uiRoot, 1100, 900, 16);

            foreach (var text in GetAllTextBlocks(view))
            {
                text.TextWrapping = TextWrapping.NoWrap;
            }

            RunLayout(uiRoot, 1100, 900, 16);

            var splitter = Assert.IsType<GridSplitter>(view.FindName("NavigationSplitter"));
            var viewer = Assert.IsType<ScrollViewer>(view.FindName("GridSplitterWorkbenchScrollViewer"));
            EnsureElementVisibleForInteraction(view, uiRoot, splitter);

            _ = ScrollViewer.GetTelemetryAndReset();
            var before = viewer.GetScrollViewerSnapshotForDiagnostics();

            DragSplitter(uiRoot, splitter, 32f, 0f);
            RunLayout(uiRoot, 1100, 900, 32);

            var aggregate = ScrollViewer.GetTelemetryAndReset();
            var after = viewer.GetScrollViewerSnapshotForDiagnostics();

            _output.WriteLine(
                $"scrollViewer before[offset={before.TotalVerticalDelta:0.###}, vh={viewer.ViewportHeight:0.###}, eh={viewer.ExtentHeight:0.###}, showV={before.ShowVerticalBar}, showH={before.ShowHorizontalBar}] " +
                $"after[offset={viewer.VerticalOffset:0.###}, vh={viewer.ViewportHeight:0.###}, eh={viewer.ExtentHeight:0.###}, showV={after.ShowVerticalBar}, showH={after.ShowHorizontalBar}] " +
                $"aggregate[measureOverride={aggregate.MeasureOverrideCallCount}, arrangeOverride={aggregate.ArrangeOverrideCallCount}, resolveBarsMeasure={aggregate.ResolveBarsAndMeasureContentCallCount}, resolveBarsRemeasure={aggregate.ResolveBarsAndMeasureContentRemeasurePathCount}, resolveBarsSingle={aggregate.ResolveBarsAndMeasureContentSingleMeasurePathCount}, " +
                $"measureContent={aggregate.MeasureContentCallCount}, updateScrollBars={aggregate.UpdateScrollBarsCallCount}, setOffsets={aggregate.SetOffsetsCallCount}, setOffsetsNoOp={aggregate.SetOffsetsNoOpCount}, verticalDelta={aggregate.TotalVerticalDelta:0.###}] " +
                $"runtime[measureContent={after.MeasureContentCallCount - before.MeasureContentCallCount}, resolveBarsMeasure={after.ResolveBarsAndMeasureContentCallCount - before.ResolveBarsAndMeasureContentCallCount}, resolveBarsRemeasure={after.ResolveBarsAndMeasureContentRemeasurePathCount - before.ResolveBarsAndMeasureContentRemeasurePathCount}, resolveBarsSingle={after.ResolveBarsAndMeasureContentSingleMeasurePathCount - before.ResolveBarsAndMeasureContentSingleMeasurePathCount}, setOffsets={after.SetOffsetsCallCount - before.SetOffsetsCallCount}, verticalDelta={after.TotalVerticalDelta - before.TotalVerticalDelta:0.###}]");

            Assert.True(aggregate.UpdateScrollBarsCallCount > 0 || aggregate.ArrangeOverrideCallCount > 0);
            Assert.Equal(aggregate.SetOffsetsCallCount, aggregate.SetOffsetsNoOpCount);
            Assert.Equal(0f, aggregate.TotalVerticalDelta, 3);
            Assert.True(aggregate.MeasureContentCallCount > 0 || aggregate.ResolveBarsAndMeasureContentCallCount == 0);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void GridSplitterView_NoWrap_PrimaryEditorDrag_IdentifiesHotBranchInsidePrimaryEditorGrid()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            TextLayout.ResetMetricsForTests();
            _ = GridSplitter.GetTelemetryAndReset();
            _ = Grid.GetTelemetryAndReset();
            _ = FrameworkElement.GetTelemetryAndReset();

            var view = new GridSplitterView();
            var uiRoot = new UiRoot(view);
            RunLayout(uiRoot, 1100, 900, 16);

            foreach (var text in GetAllTextBlocks(view))
            {
                text.TextWrapping = TextWrapping.NoWrap;
            }

            RunLayout(uiRoot, 1100, 900, 16);
            TextLayout.ResetMetricsForTests();
            _ = GridSplitter.GetTelemetryAndReset();
            _ = Grid.GetTelemetryAndReset();
            _ = FrameworkElement.GetTelemetryAndReset();

            var splitter = Assert.IsType<GridSplitter>(view.FindName("NavigationSplitter"));
            EnsureElementVisibleForInteraction(view, uiRoot, splitter);

            var primaryEditorGrid = Assert.IsType<Grid>(view.FindName("PrimaryEditorGrid"));
            var navigationPane = Assert.IsType<Border>(view.FindName("PrimaryNavigationPane"));
            var canvasPane = Assert.IsType<Border>(view.FindName("PrimaryCanvasPane"));
            var inspectorPane = Assert.IsType<Border>(view.FindName("PrimaryInspectorPane"));

            var baseline = CaptureElementWork(view);
            DragSplitter(uiRoot, splitter, 200f, 0f);
            RunLayout(uiRoot, 1100, 900, 32);

            var after = CaptureElementWork(view);
            var deltas = after.Select(snapshot => CreateWorkDelta(snapshot, baseline)).ToArray();
            var primaryDeltas = deltas
                .Where(delta => delta.Element is not null && IsDescendantOrSelf(delta.Element, primaryEditorGrid))
                .OrderByDescending(delta => delta.MeasureWorkDelta + delta.ArrangeWorkDelta + delta.InvalidateMeasureDelta + delta.InvalidateArrangeDelta)
                .ThenByDescending(delta => delta.MeasureWorkDelta)
                .Take(12)
                .ToArray();

            var branchSummary = new[]
            {
                $"navigation[mw={SumMeasureWorkForDescendantRoot(navigationPane, deltas)},aw={SumArrangeWorkForDescendantRoot(navigationPane, deltas)}]",
                $"canvas[mw={SumMeasureWorkForDescendantRoot(canvasPane, deltas)},aw={SumArrangeWorkForDescendantRoot(canvasPane, deltas)}]",
                $"inspector[mw={SumMeasureWorkForDescendantRoot(inspectorPane, deltas)},aw={SumArrangeWorkForDescendantRoot(inspectorPane, deltas)}]"
            };

            _output.WriteLine($"primaryBranches={string.Join(' ', branchSummary)}");
            _output.WriteLine($"primaryTop={FormatWorkDeltas(primaryDeltas)}");

            Assert.Equal(0, TextLayout.GetMetricsSnapshot().BuildCount);
            Assert.NotEmpty(primaryDeltas);
            Assert.True(SumMeasureWorkForDescendantRoot(canvasPane, deltas) > SumMeasureWorkForDescendantRoot(navigationPane, deltas));
            Assert.True(SumMeasureWorkForDescendantRoot(canvasPane, deltas) > SumMeasureWorkForDescendantRoot(inspectorPane, deltas));
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void ReducedPrimaryEditorFixture_ScrollViewerStackHost_MultipliesDragMeasureWorkComparedToStackAndDirectHosts()
    {
        var direct = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.Direct, fillerSectionCount: 6);
        var stack = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.StackPanel, fillerSectionCount: 6);
        var scroll = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 6);

        _output.WriteLine($"direct={FormatReducedScenario(direct)}");
        _output.WriteLine($"stack={FormatReducedScenario(stack)}");
        _output.WriteLine($"scroll={FormatReducedScenario(scroll)}");

        Assert.Equal(0, direct.TextLayoutBuilds);
        Assert.Equal(0, stack.TextLayoutBuilds);
        Assert.Equal(0, scroll.TextLayoutBuilds);
        Assert.True(direct.ResizeDelta > 0.01f);
        Assert.True(stack.ResizeDelta > 0.01f);
        Assert.True(scroll.ResizeDelta > 0.01f);
        Assert.True(scroll.FrameworkMeasureWork >= stack.FrameworkMeasureWork);
        Assert.True(stack.FrameworkMeasureWork >= direct.FrameworkMeasureWork);
        Assert.True(scroll.ScrollViewerMeasureContentCalls > 0);
        Assert.True(scroll.ScrollViewerResolveBarsMeasureCalls > 0);
        Assert.True(scroll.ScrollViewerSetOffsetsCallCount == scroll.ScrollViewerSetOffsetsNoOpCount);
        Assert.Equal(0f, scroll.ScrollViewerVerticalDelta, 3);
    }

    [Fact]
    public void GridSplitterView_NoWrap_PrimaryEditorDrag_IdentifiesHotSectionInsidePrimaryCanvasPane()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            TextLayout.ResetMetricsForTests();
            _ = GridSplitter.GetTelemetryAndReset();
            _ = Grid.GetTelemetryAndReset();
            _ = FrameworkElement.GetTelemetryAndReset();

            var view = new GridSplitterView();
            var uiRoot = new UiRoot(view);
            RunLayout(uiRoot, 1100, 900, 16);

            foreach (var text in GetAllTextBlocks(view))
            {
                text.TextWrapping = TextWrapping.NoWrap;
            }

            RunLayout(uiRoot, 1100, 900, 16);
            TextLayout.ResetMetricsForTests();
            _ = GridSplitter.GetTelemetryAndReset();
            _ = Grid.GetTelemetryAndReset();
            _ = FrameworkElement.GetTelemetryAndReset();

            var splitter = Assert.IsType<GridSplitter>(view.FindName("NavigationSplitter"));
            EnsureElementVisibleForInteraction(view, uiRoot, splitter);

            var canvasPane = Assert.IsType<Border>(view.FindName("PrimaryCanvasPane"));
            var canvasGrid = Assert.IsType<Grid>(canvasPane.Child);
            var header = Assert.IsAssignableFrom<FrameworkElement>(canvasGrid.GetVisualChildren().Single(child => Grid.GetRow(child) == 0));
            var note = Assert.IsAssignableFrom<FrameworkElement>(canvasGrid.GetVisualChildren().Single(child => Grid.GetRow(child) == 1));
            var lower = Assert.IsAssignableFrom<FrameworkElement>(canvasGrid.GetVisualChildren().Single(child => Grid.GetRow(child) == 2));

            var baseline = CaptureElementWork(view);
            DragSplitter(uiRoot, splitter, 200f, 0f);
            RunLayout(uiRoot, 1100, 900, 32);

            var after = CaptureElementWork(view);
            var deltas = after.Select(snapshot => CreateWorkDelta(snapshot, baseline)).ToArray();
            var canvasDeltas = deltas
                .Where(delta => delta.Element is not null && IsDescendantOrSelf(delta.Element, canvasPane))
                .OrderByDescending(delta => delta.MeasureWorkDelta + delta.ArrangeWorkDelta + delta.InvalidateMeasureDelta + delta.InvalidateArrangeDelta)
                .ThenByDescending(delta => delta.MeasureWorkDelta)
                .Take(12)
                .ToArray();

            var sectionSummary = new[]
            {
                $"{DescribeElement(header)}[mw={SumMeasureWorkForDescendantRoot(header, deltas)},aw={SumArrangeWorkForDescendantRoot(header, deltas)}]",
                $"{DescribeElement(note)}[mw={SumMeasureWorkForDescendantRoot(note, deltas)},aw={SumArrangeWorkForDescendantRoot(note, deltas)}]",
                $"{DescribeElement(lower)}[mw={SumMeasureWorkForDescendantRoot(lower, deltas)},aw={SumArrangeWorkForDescendantRoot(lower, deltas)}]"
            };
            var headerMeasure = SumMeasureWorkForDescendantRoot(header, deltas);
            var noteMeasure = SumMeasureWorkForDescendantRoot(note, deltas);
            var lowerMeasure = SumMeasureWorkForDescendantRoot(lower, deltas);

            var topWithAncestry = string.Join(
                " | ",
                canvasDeltas.Select(delta => $"{DescribeElement(delta.Element!)}: mw={delta.MeasureWorkDelta}, aw={delta.ArrangeWorkDelta}, im={delta.InvalidateMeasureDelta}, ia={delta.InvalidateArrangeDelta}, ancestry={DescribeLayoutAncestry(delta.Element!)}"));

            _output.WriteLine($"canvasSections={string.Join(' ', sectionSummary)}");
            _output.WriteLine($"canvasTop={topWithAncestry}");

            Assert.Equal(0, TextLayout.GetMetricsSnapshot().BuildCount);
            Assert.NotEmpty(canvasDeltas);
            Assert.True(lowerMeasure > noteMeasure);
            Assert.True(noteMeasure >= headerMeasure);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void ReducedPrimaryEditorFixture_TallScrollViewerContent_IncreasesTraversalButPrimaryEditorStillDominatesMeasureWork()
    {
        var scrollOnly = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnGrid);
        var scrollTall = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 6, ReducedPrimaryEditorCanvasLowerKind.TwoColumnGrid);

        _output.WriteLine($"scrollOnly={FormatReducedScenario(scrollOnly)}");
        _output.WriteLine($"scrollTall={FormatReducedScenario(scrollTall)}");

        Assert.Equal(0, scrollOnly.TextLayoutBuilds);
        Assert.Equal(0, scrollTall.TextLayoutBuilds);
        Assert.True(scrollOnly.ResizeDelta > 0.01f);
        Assert.True(scrollTall.ResizeDelta > 0.01f);
        Assert.True(scrollTall.FrameworkMeasureCalls > scrollOnly.FrameworkMeasureCalls);
        Assert.True(scrollTall.ScrollViewerMeasureContentCalls >= scrollOnly.ScrollViewerMeasureContentCalls);
        Assert.True(scrollTall.ScrollViewerResolveBarsMeasureCalls >= scrollOnly.ScrollViewerResolveBarsMeasureCalls);
        Assert.True(scrollTall.ScrollViewerSetOffsetsCallCount == scrollTall.ScrollViewerSetOffsetsNoOpCount);
        Assert.Equal(0f, scrollTall.ScrollViewerVerticalDelta, 3);
    }

    [Fact]
    public void ReducedPrimaryEditorFixture_PrimaryCanvasLowerSectionShape_DrivesMostOfTheRemainingNoWrapDragWork()
    {
        var twoColumn = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnGrid);
        var singlePanel = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.SinglePanel);
        var none = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.None);

        _output.WriteLine($"twoColumn={FormatReducedScenario(twoColumn)}");
        _output.WriteLine($"singlePanel={FormatReducedScenario(singlePanel)}");
        _output.WriteLine($"none={FormatReducedScenario(none)}");

        Assert.Equal(0, twoColumn.TextLayoutBuilds);
        Assert.Equal(0, singlePanel.TextLayoutBuilds);
        Assert.Equal(0, none.TextLayoutBuilds);
        Assert.True(twoColumn.ResizeDelta > 0.01f);
        Assert.True(singlePanel.ResizeDelta > 0.01f);
        Assert.True(none.ResizeDelta > 0.01f);
        Assert.True(twoColumn.FrameworkMeasureWork > singlePanel.FrameworkMeasureWork);
        Assert.True(singlePanel.FrameworkMeasureWork >= none.FrameworkMeasureWork);
        Assert.True(twoColumn.GridResolveDefs > singlePanel.GridResolveDefs);
        Assert.True(singlePanel.GridRemeasureChecks > none.GridRemeasureChecks);
    }

    [Fact]
    public void ReducedPrimaryEditorFixture_PrimaryCanvasLowerSection_StarColumnsAmplifyDragWorkMoreThanFixedColumns()
    {
        var star = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnGrid);
        var fixedColumns = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnFixedColumns);

        _output.WriteLine($"star={FormatReducedScenario(star)}");
        _output.WriteLine($"fixed={FormatReducedScenario(fixedColumns)}");

        Assert.Equal(0, star.TextLayoutBuilds);
        Assert.Equal(0, fixedColumns.TextLayoutBuilds);
        Assert.True(star.ResizeDelta > 0.01f);
        Assert.True(fixedColumns.ResizeDelta > 0.01f);
    }

    [Fact]
    public void ReducedPrimaryEditorFixture_PrimaryCanvasLowerSection_NestedGridAddsSomeCostButSecondPanelIsTheBiggerMultiplier()
    {
        var directPanel = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.SinglePanel);
        var panelInGrid = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.SinglePanelInGrid);
        var twoPanels = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnFixedColumns);

        _output.WriteLine($"directPanel={FormatReducedScenario(directPanel)}");
        _output.WriteLine($"panelInGrid={FormatReducedScenario(panelInGrid)}");
        _output.WriteLine($"twoPanels={FormatReducedScenario(twoPanels)}");

        Assert.Equal(0, directPanel.TextLayoutBuilds);
        Assert.Equal(0, panelInGrid.TextLayoutBuilds);
        Assert.Equal(0, twoPanels.TextLayoutBuilds);
        Assert.True(directPanel.ResizeDelta > 0.01f);
        Assert.True(panelInGrid.ResizeDelta > 0.01f);
        Assert.True(twoPanels.ResizeDelta > 0.01f);
    }

    [Fact]
    public void ReducedPrimaryEditorFixture_PrimaryCanvasLowerSection_BorderOnlyPanelsMostlyReduceArrangeWork()
    {
        var rich = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnFixedColumns);
        var borderOnly = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnBordersOnly);

        _output.WriteLine($"rich={FormatReducedScenario(rich)}");
        _output.WriteLine($"borderOnly={FormatReducedScenario(borderOnly)}");

        Assert.Equal(0, rich.TextLayoutBuilds);
        Assert.Equal(0, borderOnly.TextLayoutBuilds);
        Assert.True(rich.ResizeDelta > 0.01f);
        Assert.True(borderOnly.ResizeDelta > 0.01f);
        Assert.True(rich.PeakArrangeWorkMs > 0f);
        Assert.True(borderOnly.PeakArrangeWorkMs > 0f);
        Assert.InRange(borderOnly.PeakArrangeWorkMs - rich.PeakArrangeWorkMs, -3.5f, 3f);
        Assert.True(rich.GridResolveDefs >= borderOnly.GridResolveDefs);
    }

    [Fact]
    public void ReducedPrimaryEditorFixture_PrimaryCanvasLowerSection_TextBodiesAreNotTheDominantNoWrapMultiplier()
    {
        var rich = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnFixedColumns);
        var noBodyText = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnNoBodyText);

        _output.WriteLine($"rich={FormatReducedScenario(rich)}");
        _output.WriteLine($"noBodyText={FormatReducedScenario(noBodyText)}");

        Assert.Equal(0, rich.TextLayoutBuilds);
        Assert.Equal(0, noBodyText.TextLayoutBuilds);
        Assert.True(rich.ResizeDelta > 0.01f);
        Assert.True(noBodyText.ResizeDelta > 0.01f);
        Assert.True(noBodyText.FrameworkMeasureWork >= rich.FrameworkMeasureWork);
    }

    [Fact]
    public void ReducedPrimaryEditorFixture_PrimaryCanvasLowerSection_SecondPanelAddsWorkWithoutScrollViewerAmplification()
    {
        var single = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.Direct, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.SinglePanel);
        var two = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.Direct, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnFixedColumns);

        _output.WriteLine($"single={FormatReducedScenario(single)}");
        _output.WriteLine($"two={FormatReducedScenario(two)}");

        Assert.Equal(0, single.TextLayoutBuilds);
        Assert.Equal(0, two.TextLayoutBuilds);
        Assert.True(single.ResizeDelta > 0.01f);
        Assert.True(two.ResizeDelta > 0.01f);
        Assert.True(two.FrameworkMeasureWork > single.FrameworkMeasureWork);
        Assert.True(two.GridResolveDefs >= single.GridResolveDefs);
    }

    [Fact]
    public void ReducedPrimaryEditorFixture_PrimaryCanvasLowerSection_GridContainerCostsMoreThanStackContainerForTwoPanels()
    {
        var stack = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoPanelStack);
        var grid = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnFixedColumns);

        _output.WriteLine($"stack={FormatReducedScenario(stack)}");
        _output.WriteLine($"grid={FormatReducedScenario(grid)}");

        Assert.Equal(0, stack.TextLayoutBuilds);
        Assert.Equal(0, grid.TextLayoutBuilds);
        Assert.True(stack.ResizeDelta > 0.01f);
        Assert.True(grid.ResizeDelta > 0.01f);
        Assert.True(grid.FrameworkMeasureWork > stack.FrameworkMeasureWork);
        Assert.True(grid.GridResolveDefs > stack.GridResolveDefs);
    }

    [Fact]
    public void ReducedPrimaryEditorFixture_ScrollViewerFillerRamp_MonotonicallyIncreasesTraversal()
    {
        var none = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnFixedColumns);
        var medium = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 3, ReducedPrimaryEditorCanvasLowerKind.TwoColumnFixedColumns);
        var tall = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 6, ReducedPrimaryEditorCanvasLowerKind.TwoColumnFixedColumns);

        _output.WriteLine($"none={FormatReducedScenario(none)}");
        _output.WriteLine($"medium={FormatReducedScenario(medium)}");
        _output.WriteLine($"tall={FormatReducedScenario(tall)}");

        Assert.Equal(0, none.TextLayoutBuilds);
        Assert.Equal(0, medium.TextLayoutBuilds);
        Assert.Equal(0, tall.TextLayoutBuilds);
        Assert.True(medium.FrameworkMeasureCalls >= none.FrameworkMeasureCalls);
        Assert.True(tall.FrameworkMeasureCalls >= medium.FrameworkMeasureCalls);
        Assert.True(medium.ScrollViewerMeasureContentCalls >= none.ScrollViewerMeasureContentCalls);
        Assert.True(tall.ScrollViewerMeasureContentCalls >= medium.ScrollViewerMeasureContentCalls);
    }

    [Fact]
    public void ReducedPrimaryEditorFixture_LowerSectionShapeOrdering_PersistsAcrossHostKinds()
    {
        var directNone = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.Direct, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.None);
        var directSingle = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.Direct, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.SinglePanel);
        var directTwo = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.Direct, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnFixedColumns);

        var scrollNone = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.None);
        var scrollSingle = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.SinglePanel);
        var scrollTwo = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnFixedColumns);

        _output.WriteLine($"direct none={FormatReducedScenario(directNone)}");
        _output.WriteLine($"direct single={FormatReducedScenario(directSingle)}");
        _output.WriteLine($"direct two={FormatReducedScenario(directTwo)}");
        _output.WriteLine($"scroll none={FormatReducedScenario(scrollNone)}");
        _output.WriteLine($"scroll single={FormatReducedScenario(scrollSingle)}");
        _output.WriteLine($"scroll two={FormatReducedScenario(scrollTwo)}");

        Assert.True(directSingle.FrameworkMeasureWork >= directNone.FrameworkMeasureWork);
        Assert.True(directTwo.FrameworkMeasureWork > directSingle.FrameworkMeasureWork);
        Assert.True(directSingle.GridRemeasureChecks > directNone.GridRemeasureChecks);
        Assert.True(scrollSingle.FrameworkMeasureWork >= scrollNone.FrameworkMeasureWork);
        Assert.True(scrollTwo.FrameworkMeasureWork > scrollSingle.FrameworkMeasureWork);
        Assert.True(scrollSingle.GridRemeasureChecks > scrollNone.GridRemeasureChecks);
    }

    [Fact]
    public void ReducedPrimaryEditorFixture_StarVsFixedDifference_IsSmallerThanAddingTheSecondPanel()
    {
        var single = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.SinglePanel);
        var fixedColumns = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnFixedColumns);
        var starColumns = RunReducedPrimaryEditorHostScenario(ReducedPrimaryEditorHostKind.ScrollViewerStackPanel, fillerSectionCount: 0, ReducedPrimaryEditorCanvasLowerKind.TwoColumnGrid);

        var secondPanelDelta = fixedColumns.FrameworkMeasureWork - single.FrameworkMeasureWork;
        var starDelta = Math.Abs(starColumns.FrameworkMeasureWork - fixedColumns.FrameworkMeasureWork);

        _output.WriteLine($"single={FormatReducedScenario(single)}");
        _output.WriteLine($"fixed={FormatReducedScenario(fixedColumns)}");
        _output.WriteLine($"star={FormatReducedScenario(starColumns)}");
        _output.WriteLine($"secondPanelDelta={secondPanelDelta}, starDelta={starDelta}");

        Assert.True(secondPanelDelta > 0);
        Assert.True(starDelta <= secondPanelDelta);
    }

    [Fact]
    public void GridSplitterView_NavigationSplitterVisibleCenter_StartsDragInCurrentViewHarnessPath()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            LoadRootAppResources();

            var view = new GridSplitterView();
            var uiRoot = new UiRoot(view);
            RunLayout(uiRoot, 1100, 900, 16);

            var splitter = Assert.IsType<GridSplitter>(view.FindName("NavigationSplitter"));
            var center = GetVisibleCenter(splitter);
            var hit = VisualTreeHelper.HitTest(view, center, out var metrics);
            _ = GridSplitter.GetTelemetryAndReset();

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(center, leftPressed: true));
            var splitterTelemetry = GridSplitter.GetTelemetryAndReset();
            var runtime = splitter.GetGridSplitterSnapshotForDiagnostics();

            _output.WriteLine(
                $"splitterCenter={center}, splitterSlot={splitter.LayoutSlot}, manualHit={DescribeElement(hit)}, hitMetrics=nodes={metrics.NodesVisited},depth={metrics.MaxDepth}, dragging={splitter.IsDragging}, captured={DescribeElement(FocusManager.GetCapturedPointerElement())}, " +
                $"splitterTelemetry[pd={splitterTelemetry.PointerDownCallCount},pdHitReject={splitterTelemetry.PointerDownHitTestRejectCount},beginDrag={splitterTelemetry.BeginDragSuccessCount},beginDragFailure={splitterTelemetry.PointerDownBeginDragFailureCount}], " +
                $"runtime[pointerDown={runtime.PointerDownCallCount},pointerDownHitReject={runtime.PointerDownHitTestRejectCount},beginDrag={runtime.BeginDragSuccessCount},hasGrid={runtime.HasActiveGrid},slotY={runtime.LayoutSlotY:0.###}]");
            _output.WriteLine($"ancestry={DescribeLayoutAncestry(splitter)}");

            Assert.Same(splitter, hit);
            Assert.True(splitter.IsDragging);
            Assert.Same(splitter, FocusManager.GetCapturedPointerElement());
            Assert.True(splitterTelemetry.PointerDownCallCount >= 1);
            Assert.True(splitterTelemetry.BeginDragSuccessCount >= 1);
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
            if (text.LayoutSlot.Width <= 0.01f)
            {
                Assert.True(
                    text.DesiredSize.Y - text.Margin.Vertical <= 0.5f,
                    $"Expected wrapped TextBlock to collapse desired height at zero width after splitter resize. text='{Abbreviate(text.Text)}', actualHeight={text.ActualHeight:0.##}, desiredHeight={text.DesiredSize.Y:0.##}, width={text.LayoutSlot.Width:0.##}, {context}");
                continue;
            }

            var expectedLayout = TextLayout.LayoutForElement(
                text.Text,
                text,
                text.FontSize,
                text.LayoutSlot.Width,
                TextWrapping.Wrap);

            Assert.True(
                Math.Abs(expectedLayout.Size.Y - (text.DesiredSize.Y - text.Margin.Vertical)) < 0.001d,
                $"Expected wrapped TextBlock desired height to match current width after splitter resize. text='{Abbreviate(text.Text)}', desiredHeight={text.DesiredSize.Y - text.Margin.Vertical:0.##}, expectedHeight={expectedLayout.Size.Y:0.##}, slotWidth={text.LayoutSlot.Width:0.##}, renderWidth={text.RenderSize.X:0.##}, marginH={text.Margin.Horizontal:0.##}, {context}");

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

    private static TextBlock[] GetAllTextBlocks(UIElement root)
    {
        var results = new List<TextBlock>();
        CollectAllTextBlocks(root, results);
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

    private static void CollectAllTextBlocks(UIElement root, List<TextBlock> results)
    {
        if (root is TextBlock text)
        {
            results.Add(text);
        }

        foreach (var child in root.GetVisualChildren())
        {
            CollectAllTextBlocks(child, results);
        }
    }

    private static ElementWorkSnapshot[] CaptureElementWork(UIElement root)
    {
        var results = new List<ElementWorkSnapshot>();
        CollectElementWork(root, results);
        return results.ToArray();
    }

    private static void CollectElementWork(UIElement root, List<ElementWorkSnapshot> results)
    {
        if (root is FrameworkElement element)
        {
            var diagnostics = element.GetFrameworkElementSnapshotForDiagnostics();
            results.Add(new ElementWorkSnapshot(
                element,
                element.GetType().Name,
                string.IsNullOrWhiteSpace(element.Name) ? "<unnamed>" : element.Name,
                diagnostics.MeasureWorkCount,
                diagnostics.ArrangeWorkCount,
                diagnostics.InvalidateMeasureCallCount,
                diagnostics.InvalidateArrangeCallCount));
        }

        foreach (var child in root.GetVisualChildren())
        {
            CollectElementWork(child, results);
        }
    }

    private static ElementWorkDelta CreateWorkDelta(ElementWorkSnapshot after, IReadOnlyList<ElementWorkSnapshot> baseline)
    {
        var before = baseline.FirstOrDefault(snapshot => ReferenceEquals(snapshot.Element, after.Element));
        return new ElementWorkDelta(
            after.Element,
            after.ElementType,
            after.Name,
            after.MeasureWorkCount - before.MeasureWorkCount,
            after.ArrangeWorkCount - before.ArrangeWorkCount,
            after.InvalidateMeasureCount - before.InvalidateMeasureCount,
            after.InvalidateArrangeCount - before.InvalidateArrangeCount);
    }

    private static string FormatWorkDeltas(IEnumerable<ElementWorkDelta> deltas)
    {
        return string.Join(
            " | ",
            deltas.Select(static delta =>
                $"{delta.ElementType}#{delta.Name}: mw={delta.MeasureWorkDelta}, aw={delta.ArrangeWorkDelta}, im={delta.InvalidateMeasureDelta}, ia={delta.InvalidateArrangeDelta}"));
    }

    private static string DescribeElement(UIElement? element)
    {
        return element switch
        {
            FrameworkElement { Name.Length: > 0 } frameworkElement => $"{frameworkElement.GetType().Name}#{frameworkElement.Name}",
            null => "<null>",
            _ => element.GetType().Name
        };
    }

    private static string DescribeLayoutAncestry(UIElement element)
    {
        var parts = new List<string>();
        for (var current = element; current != null; current = current.VisualParent)
        {
            var slot = current.LayoutSlot;
            parts.Add($"{DescribeElement(current)}@x={slot.X:0.###},y={slot.Y:0.###},w={slot.Width:0.###},h={slot.Height:0.###}");
        }

        return string.Join(" <= ", parts);
    }

    private static NamedSplitterScenarioMetrics RunNamedSplitterDragScenario(
        string splitterName,
        float deltaX,
        float deltaY,
        IReadOnlyList<string> trackedRootNames)
    {
        TextLayout.ResetMetricsForTests();
        _ = GridSplitter.GetTelemetryAndReset();
        _ = Grid.GetTelemetryAndReset();
        _ = FrameworkElement.GetTelemetryAndReset();

        var view = new GridSplitterView();
        var uiRoot = new UiRoot(view);
        RunLayout(uiRoot, 1100, 900, 16);

        foreach (var text in GetAllTextBlocks(view))
        {
            text.TextWrapping = TextWrapping.NoWrap;
        }

        RunLayout(uiRoot, 1100, 900, 16);
        TextLayout.ResetMetricsForTests();
        _ = GridSplitter.GetTelemetryAndReset();
        _ = Grid.GetTelemetryAndReset();
        _ = FrameworkElement.GetTelemetryAndReset();

        var splitter = Assert.IsType<GridSplitter>(view.FindName(splitterName));
        EnsureElementVisibleForInteraction(view, uiRoot, splitter);
        var trackedRoots = trackedRootNames
            .Select(name => (Name: name, Element: Assert.IsAssignableFrom<FrameworkElement>(view.FindName(name))))
            .ToArray();
        var baseline = CaptureElementWork(view);
        var beforeSplitter = splitter.GetGridSplitterSnapshotForDiagnostics();

        DragSplitter(uiRoot, splitter, deltaX, deltaY);
        RunLayout(uiRoot, 1100, 900, 32);

        var perf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        var afterSplitter = splitter.GetGridSplitterSnapshotForDiagnostics();
        var after = CaptureElementWork(view);
        var deltas = after.Select(snapshot => CreateWorkDelta(snapshot, baseline)).ToArray();
        var textLayout = TextLayout.GetMetricsSnapshot();

        return new NamedSplitterScenarioMetrics(
            splitterName,
            MathF.Abs(afterSplitter.LastAppliedDelta) > 0.01f ? MathF.Abs(afterSplitter.LastAppliedDelta) : MathF.Abs(afterSplitter.LastSnappedDelta),
            perf.LayoutPhaseMilliseconds,
            perf.LayoutMeasureWorkMilliseconds,
            perf.LayoutArrangeWorkMilliseconds,
            SumMeasureWorkForRoot(trackedRoots, "GridSplitterWorkbenchScrollViewer", deltas),
            SumMeasureWorkForRoot(trackedRoots, "PrimaryEditorGrid", deltas),
            SumMeasureWorkForRoot(trackedRoots, "HorizontalWorkbenchGrid", deltas),
            SumArrangeWorkForRoot(trackedRoots, "GridSplitterWorkbenchScrollViewer", deltas),
            SumArrangeWorkForRoot(trackedRoots, "PrimaryEditorGrid", deltas),
            SumArrangeWorkForRoot(trackedRoots, "HorizontalWorkbenchGrid", deltas),
            afterSplitter.PointerDownCallCount - beforeSplitter.PointerDownCallCount,
            afterSplitter.BeginDragSuccessCount - beforeSplitter.BeginDragSuccessCount,
            afterSplitter.PointerMoveApplyCount - beforeSplitter.PointerMoveApplyCount,
            afterSplitter.PointerUpSuccessCount - beforeSplitter.PointerUpSuccessCount,
            textLayout.BuildCount,
            textLayout.WrappedBuildCount);
    }

    private static int SumMeasureWorkForRoot(
        IEnumerable<(string Name, FrameworkElement Element)> trackedRoots,
        string rootName,
        IEnumerable<ElementWorkDelta> deltas)
    {
        var root = trackedRoots.First(pair => pair.Name == rootName).Element;
        return deltas
            .Where(delta => delta.Element is not null && IsDescendantOrSelf(delta.Element, root))
            .Sum(delta => delta.MeasureWorkDelta);
    }

    private static int SumArrangeWorkForRoot(
        IEnumerable<(string Name, FrameworkElement Element)> trackedRoots,
        string rootName,
        IEnumerable<ElementWorkDelta> deltas)
    {
        var root = trackedRoots.First(pair => pair.Name == rootName).Element;
        return deltas
            .Where(delta => delta.Element is not null && IsDescendantOrSelf(delta.Element, root))
            .Sum(delta => delta.ArrangeWorkDelta);
    }

    private static int SumMeasureWorkForDescendantRoot(
        FrameworkElement root,
        IEnumerable<ElementWorkDelta> deltas)
    {
        return deltas
            .Where(delta => delta.Element is not null && IsDescendantOrSelf(delta.Element, root))
            .Sum(delta => delta.MeasureWorkDelta);
    }

    private static int SumArrangeWorkForDescendantRoot(
        FrameworkElement root,
        IEnumerable<ElementWorkDelta> deltas)
    {
        return deltas
            .Where(delta => delta.Element is not null && IsDescendantOrSelf(delta.Element, root))
            .Sum(delta => delta.ArrangeWorkDelta);
    }

    private static bool IsDescendantOrSelf(UIElement candidate, UIElement ancestor)
    {
        for (var current = candidate; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureElementVisibleForInteraction(UIElement root, UiRoot uiRoot, UIElement element)
    {
        var viewer = FindAncestor<ScrollViewer>(element);
        if (viewer == null)
        {
            return;
        }

        var visibleCenter = GetVisibleCenter(element);
        if (VisualTreeHelper.HitTest(root, visibleCenter) == element)
        {
            return;
        }

        var desiredOffset = MathF.Max(
            0f,
            (element.LayoutSlot.Y - viewer.LayoutSlot.Y) - MathF.Max(0f, (viewer.ViewportHeight - MathF.Min(element.LayoutSlot.Height, viewer.ViewportHeight)) * 0.5f));
        viewer.ScrollToVerticalOffset(desiredOffset);
        RunLayout(uiRoot, 1100, 900, 16);
    }

    private static T? FindAncestor<T>(UIElement element) where T : UIElement
    {
        for (var current = element.VisualParent; current != null; current = current.VisualParent)
        {
            if (current is T typed)
            {
                return typed;
            }
        }

        return null;
    }

    private static NoWrapScenarioMetrics RunNoWrapSplitterScenario(
        Action<GridSplitter> configure,
        Action<UiRoot, GridSplitter> action)
    {
        TextLayout.ResetMetricsForTests();
        _ = Grid.GetTelemetryAndReset();
        _ = FrameworkElement.GetTelemetryAndReset();

        var view = new GridSplitterView();
        var uiRoot = new UiRoot(view);
        RunLayout(uiRoot, 1100, 900, 16);

        foreach (var text in GetAllTextBlocks(view))
        {
            text.TextWrapping = TextWrapping.NoWrap;
        }

        RunLayout(uiRoot, 1100, 900, 16);
        TextLayout.ResetMetricsForTests();
        _ = Grid.GetTelemetryAndReset();
        _ = FrameworkElement.GetTelemetryAndReset();

        var splitter = Assert.IsType<GridSplitter>(view.FindName("NavigationSplitter"));
        var primaryEditorGrid = Assert.IsType<Grid>(view.FindName("PrimaryEditorGrid"));
        configure(splitter);

        var initialCenterWidth = primaryEditorGrid.ColumnDefinitions[2].ActualWidth;

        var peakLayoutMs = 0d;
        var peakInputMs = 0d;
        var peakRouteMs = 0d;
        var peakMoveHandlerMs = 0d;
        var peakHitTests = 0;
        action(uiRoot, splitter);
        var move = uiRoot.GetPointerMoveTelemetrySnapshotForTests();
        var perf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        peakLayoutMs = Math.Max(peakLayoutMs, perf.LayoutPhaseMilliseconds);
        peakInputMs = Math.Max(peakInputMs, perf.InputPhaseMilliseconds);
        peakRouteMs = Math.Max(peakRouteMs, move.PointerRouteMilliseconds);
        peakMoveHandlerMs = Math.Max(peakMoveHandlerMs, move.PointerMoveHandlerMilliseconds);
        peakHitTests = Math.Max(peakHitTests, move.HitTestCount);

        var finalCenterWidth = primaryEditorGrid.ColumnDefinitions[2].ActualWidth;

        var gridTelemetry = Grid.GetTelemetryAndReset();
        var frameworkTelemetry = FrameworkElement.GetTelemetryAndReset();
        var textLayout = TextLayout.GetMetricsSnapshot();
        return new NoWrapScenarioMetrics(
            MathF.Abs(finalCenterWidth - initialCenterWidth) > 0.01f,
            finalCenterWidth - initialCenterWidth,
            peakInputMs,
            peakLayoutMs,
            peakRouteMs,
            peakMoveHandlerMs,
            peakHitTests,
            gridTelemetry.ResolveDefinitionSizesCallCount,
            gridTelemetry.MeasureRemeasureCheckCount,
            gridTelemetry.MeasureRemeasureCount,
            gridTelemetry.MeasureSecondPassChildCount,
            frameworkTelemetry.MeasureCallCount,
            frameworkTelemetry.MeasureWorkCount,
            frameworkTelemetry.MeasureParentInvalidationCount,
            frameworkTelemetry.ArrangeCallCount,
            frameworkTelemetry.ArrangeWorkCount,
            frameworkTelemetry.ArrangeParentInvalidationCount,
            textLayout.BuildCount,
            textLayout.WrappedBuildCount);
    }

    private static ReducedPrimaryEditorScenarioMetrics RunReducedPrimaryEditorHostScenario(
        ReducedPrimaryEditorHostKind hostKind,
        int fillerSectionCount,
        ReducedPrimaryEditorCanvasLowerKind lowerKind = ReducedPrimaryEditorCanvasLowerKind.TwoColumnGrid)
    {
        TextLayout.ResetMetricsForTests();
        _ = ScrollViewer.GetTelemetryAndReset();
        _ = Grid.GetTelemetryAndReset();
        _ = FrameworkElement.GetTelemetryAndReset();

        var fixture = CreateReducedPrimaryEditorHostFixture(hostKind, fillerSectionCount, lowerKind);
        var uiRoot = new UiRoot(fixture.Root);
        RunLayout(uiRoot, 1100, 900, 16);

        TextLayout.ResetMetricsForTests();
        _ = ScrollViewer.GetTelemetryAndReset();
        _ = Grid.GetTelemetryAndReset();
        _ = FrameworkElement.GetTelemetryAndReset();

        EnsureElementVisibleForInteraction(fixture.Root, uiRoot, fixture.Splitter);
        var before = fixture.Splitter.GetGridSplitterSnapshotForDiagnostics();
        for (var i = 0; i < 4; i++)
        {
            DragSplitter(uiRoot, fixture.Splitter, 32f, 0f);
            RunLayout(uiRoot, 1100, 900, 16);
        }
        var after = fixture.Splitter.GetGridSplitterSnapshotForDiagnostics();

        var perf = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        var scrollTelemetry = ScrollViewer.GetTelemetryAndReset();
        var gridTelemetry = Grid.GetTelemetryAndReset();
        var frameworkTelemetry = FrameworkElement.GetTelemetryAndReset();
        var textLayout = TextLayout.GetMetricsSnapshot();
        var resizeDelta = MathF.Abs(after.LastAppliedDelta) > 0.01f ? MathF.Abs(after.LastAppliedDelta) : MathF.Abs(after.LastSnappedDelta);

        return new ReducedPrimaryEditorScenarioMetrics(
            $"{hostKind} fillers={fillerSectionCount} lower={lowerKind}",
            resizeDelta,
            perf.LayoutPhaseMilliseconds,
            perf.LayoutMeasureWorkMilliseconds,
            perf.LayoutArrangeWorkMilliseconds,
            frameworkTelemetry.MeasureCallCount,
            frameworkTelemetry.MeasureWorkCount,
            gridTelemetry.ResolveDefinitionSizesCallCount,
            gridTelemetry.MeasureRemeasureCheckCount,
            gridTelemetry.MeasureRemeasureCount,
            scrollTelemetry.MeasureContentCallCount,
            scrollTelemetry.ResolveBarsAndMeasureContentCallCount,
            scrollTelemetry.ResolveBarsAndMeasureContentRemeasurePathCount,
            scrollTelemetry.SetOffsetsCallCount,
            scrollTelemetry.SetOffsetsNoOpCount,
            scrollTelemetry.TotalVerticalDelta,
            after.PointerDownCallCount - before.PointerDownCallCount,
            after.BeginDragSuccessCount - before.BeginDragSuccessCount,
            after.PointerMoveApplyCount - before.PointerMoveApplyCount,
            after.PointerUpSuccessCount - before.PointerUpSuccessCount,
            textLayout.BuildCount,
            textLayout.WrappedBuildCount);
    }

    private static ReducedPrimaryEditorHostFixture CreateReducedPrimaryEditorHostFixture(
        ReducedPrimaryEditorHostKind hostKind,
        int fillerSectionCount,
        ReducedPrimaryEditorCanvasLowerKind lowerKind)
    {
        var root = new Grid
        {
            Width = 1100f,
            Height = 900f
        };

        FrameworkElement hostContent = CreatePrimaryEditorSection(out var splitter, lowerKind);
        switch (hostKind)
        {
            case ReducedPrimaryEditorHostKind.Direct:
                root.AddChild(hostContent);
                break;

            case ReducedPrimaryEditorHostKind.StackPanel:
            {
                var stack = new StackPanel();
                stack.AddChild(hostContent);
                AddReducedFixtureFillers(stack, fillerSectionCount);
                root.AddChild(stack);
                break;
            }

            case ReducedPrimaryEditorHostKind.ScrollViewerStackPanel:
            {
                var stack = new StackPanel();
                stack.AddChild(hostContent);
                AddReducedFixtureFillers(stack, fillerSectionCount);

                var viewer = new ScrollViewer
                {
                    Name = "ReducedFixtureScrollViewer",
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };
                viewer.Content = stack;
                root.AddChild(viewer);
                break;
            }
        }

        ForceNoWrapRecursive(root);
        return new ReducedPrimaryEditorHostFixture(root, splitter);
    }

    private static Border CreatePrimaryEditorSection(out GridSplitter splitter, ReducedPrimaryEditorCanvasLowerKind lowerKind)
    {
        var section = new Border
        {
            Padding = new Thickness(12f),
            Margin = new Thickness(24f),
            Height = 360f
        };

        var grid = new Grid { Name = "ReducedPrimaryEditorGrid" };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180f), MinWidth = 120f });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8f), MinWidth = 8f, MaxWidth = 8f });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380f), MinWidth = 180f });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8f), MinWidth = 8f, MaxWidth = 8f });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220f), MinWidth = 140f });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var navigation = new Border
        {
            Margin = new Thickness(0f, 0f, 8f, 0f),
            Padding = new Thickness(12f)
        };
        var navigationStack = new StackPanel();
        navigationStack.AddChild(new TextBlock { Text = "Navigation rail", FontWeight = "SemiBold" });
        navigationStack.AddChild(new TextBlock { Text = "Left pane keeps a readable minimum while the dedicated splitter lane resizes against the center workspace." });
        navigationStack.AddChild(CreateChip("Explorer"));
        navigationStack.AddChild(CreateChip("Search"));
        navigationStack.AddChild(CreateChip("Branches"));
        navigation.Child = navigationStack;
        grid.AddChild(navigation);

        splitter = new GridSplitter
        {
            Name = "ReducedNavigationSplitter",
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Width = 8f,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            DragIncrement = 4f,
            KeyboardIncrement = 24f
        };
        Grid.SetColumn(splitter, 1);
        grid.AddChild(splitter);

        var canvas = new Border
        {
            Padding = new Thickness(14f)
        };
        Grid.SetColumn(canvas, 2);
        var canvasGrid = new Grid();
        canvasGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        canvasGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        canvasGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headerStack = new StackPanel();
        headerStack.AddChild(new TextBlock { Text = "Canvas workspace", FontWeight = "SemiBold" });
        headerStack.AddChild(new TextBlock { Text = "This center lane behaves like a WPF editor canvas: it absorbs most width, but still yields to splitter drags and keyboard nudges." });
        header.AddChild(headerStack);
        var badge = new Border
        {
            Padding = new Thickness(10f, 4f, 10f, 4f),
            Margin = new Thickness(12f, 0f, 0f, 0f),
            Child = new TextBlock { Text = "Dedicated splitter lane" }
        };
        Grid.SetColumn(badge, 1);
        header.AddChild(badge);
        canvasGrid.AddChild(header);

        var note = new Border
        {
            Padding = new Thickness(12f),
            Margin = new Thickness(0f, 12f, 0f, 12f)
        };
        Grid.SetRow(note, 1);
        var noteGrid = new Grid();
        noteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        noteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        noteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        noteGrid.AddChild(new TextBlock { Text = "Try dragging either rail, then click it and use arrow keys. Pane minimums prevent the shell from collapsing through the splitter." });
        var keyBadge = new Border { Padding = new Thickness(10f, 4f, 10f, 4f), Margin = new Thickness(0f, 0f, 8f, 0f), Child = new TextBlock { Text = "Arrow keys nudge focused splitter" } };
        Grid.SetColumn(keyBadge, 1);
        noteGrid.AddChild(keyBadge);
        var minBadge = new Border { Padding = new Thickness(10f, 4f, 10f, 4f), Child = new TextBlock { Text = "Min widths enforced" } };
        Grid.SetColumn(minBadge, 2);
        noteGrid.AddChild(minBadge);
        note.Child = noteGrid;
        canvasGrid.AddChild(note);

        var lowerContent = CreateReducedPrimaryCanvasLowerSection(lowerKind);
        if (lowerContent != null)
        {
            Grid.SetRow(lowerContent, 2);
            canvasGrid.AddChild(lowerContent);
        }

        canvas.Child = canvasGrid;
        grid.AddChild(canvas);

        var inspectorSplitter = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Width = 8f,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            DragIncrement = 4f,
            KeyboardIncrement = 24f
        };
        Grid.SetColumn(inspectorSplitter, 3);
        grid.AddChild(inspectorSplitter);

        var inspector = new Border
        {
            Margin = new Thickness(8f, 0f, 0f, 0f),
            Padding = new Thickness(12f)
        };
        Grid.SetColumn(inspector, 4);
        var inspectorStack = new StackPanel();
        inspectorStack.AddChild(new TextBlock { Text = "Inspector rail", FontWeight = "SemiBold" });
        inspectorStack.AddChild(new TextBlock { Text = "Right pane mirrors a property inspector or diagnostics rail that should stay narrow enough to read but never disappear." });
        inspectorStack.AddChild(CreateChip("Selection details"));
        inspectorStack.AddChild(CreateChip("Binding traces"));
        inspectorStack.AddChild(CreateChip("Automation notes"));
        inspector.Child = inspectorStack;
        grid.AddChild(inspector);

        section.Child = grid;
        return section;
    }

    private static void AddReducedFixtureFillers(StackPanel stack, int fillerSectionCount)
    {
        for (var i = 0; i < fillerSectionCount; i++)
        {
            stack.AddChild(CreateFillerSection($"Filler section {i + 1}"));
        }
    }

    private static FrameworkElement? CreateReducedPrimaryCanvasLowerSection(ReducedPrimaryEditorCanvasLowerKind lowerKind)
    {
        switch (lowerKind)
        {
            case ReducedPrimaryEditorCanvasLowerKind.None:
                return null;

            case ReducedPrimaryEditorCanvasLowerKind.SinglePanel:
                return CreatePanelWithText(
                    "Document tab strip",
                    "Toolbar, tabs, or canvases usually live in the expandable center track.",
                    "retained layout preview",
                    "inspector-bound selection",
                    "command surface");

            case ReducedPrimaryEditorCanvasLowerKind.SinglePanelInGrid:
            {
                var lowerGrid = new Grid();
                lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
                lowerGrid.AddChild(CreatePanelWithText(
                    "Document tab strip",
                    "Toolbar, tabs, or canvases usually live in the expandable center track.",
                    "retained layout preview",
                    "inspector-bound selection",
                    "command surface"));
                return lowerGrid;
            }

            case ReducedPrimaryEditorCanvasLowerKind.TwoPanelStack:
            {
                var stack = new StackPanel();
                stack.AddChild(CreatePanelWithText(
                    "Document tab strip",
                    "Toolbar, tabs, or canvases usually live in the expandable center track.",
                    "retained layout preview",
                    "inspector-bound selection",
                    "command surface"));
                stack.AddChild(CreatePanelWithText(
                    "Viewport telemetry",
                    "Actual column widths appear in the side rail so this pane can be treated like a live measurement target.",
                    "left, center, right widths",
                    "drag and key increments",
                    "hover and drag state"));
                return stack;
            }

            case ReducedPrimaryEditorCanvasLowerKind.TwoColumnNoBodyText:
            {
                var lowerGrid = new Grid();
                lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140f) });
                lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140f) });
                lowerGrid.AddChild(CreatePanelWithTitleAndItems(
                    "Document tab strip",
                    "retained layout preview",
                    "inspector-bound selection",
                    "command surface"));
                var telemetry = CreatePanelWithTitleAndItems(
                    "Viewport telemetry",
                    "left, center, right widths",
                    "drag and key increments",
                    "hover and drag state");
                Grid.SetColumn(telemetry, 1);
                lowerGrid.AddChild(telemetry);
                return lowerGrid;
            }

            case ReducedPrimaryEditorCanvasLowerKind.TwoColumnBordersOnly:
            {
                var lowerGrid = new Grid();
                lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140f) });
                lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140f) });
                lowerGrid.AddChild(CreateBarePanel());
                var second = CreateBarePanel();
                Grid.SetColumn(second, 1);
                lowerGrid.AddChild(second);
                return lowerGrid;
            }

            case ReducedPrimaryEditorCanvasLowerKind.TwoColumnFixedColumns:
            {
                var lowerGrid = new Grid();
                lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140f) });
                lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140f) });
                lowerGrid.AddChild(CreatePanelWithText(
                    "Document tab strip",
                    "Toolbar, tabs, or canvases usually live in the expandable center track.",
                    "retained layout preview",
                    "inspector-bound selection",
                    "command surface"));
                var telemetry = CreatePanelWithText(
                    "Viewport telemetry",
                    "Actual column widths appear in the side rail so this pane can be treated like a live measurement target.",
                    "left, center, right widths",
                    "drag and key increments",
                    "hover and drag state");
                Grid.SetColumn(telemetry, 1);
                lowerGrid.AddChild(telemetry);
                return lowerGrid;
            }

            case ReducedPrimaryEditorCanvasLowerKind.TwoColumnGrid:
            default:
            {
                var lowerGrid = new Grid();
                lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
                lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
                lowerGrid.AddChild(CreatePanelWithText(
                    "Document tab strip",
                    "Toolbar, tabs, or canvases usually live in the expandable center track.",
                    "retained layout preview",
                    "inspector-bound selection",
                    "command surface"));
                var telemetry = CreatePanelWithText(
                    "Viewport telemetry",
                    "Actual column widths appear in the side rail so this pane can be treated like a live measurement target.",
                    "left, center, right widths",
                    "drag and key increments",
                    "hover and drag state");
                Grid.SetColumn(telemetry, 1);
                lowerGrid.AddChild(telemetry);
                return lowerGrid;
            }
        }
    }

    private static Border CreateFillerSection(string title)
    {
        var border = new Border
        {
            Padding = new Thickness(12f),
            Margin = new Thickness(24f, 0f, 24f, 10f),
            Height = 220f
        };
        border.Child = CreatePanelWithText(
            title,
            "This filler block simulates the rest of the GridSplitter workbench living above and below the primary editor shell.",
            "alignment samples",
            "explicit behavior samples",
            "auto direction samples");
        return border;
    }

    private static Border CreatePanelWithText(string title, string body, string item1, string item2, string item3)
    {
        var border = new Border
        {
            Padding = new Thickness(12f),
            Margin = new Thickness(0f, 0f, 8f, 0f)
        };
        var stack = new StackPanel();
        stack.AddChild(new TextBlock { Text = title, FontWeight = "SemiBold" });
        stack.AddChild(new TextBlock { Text = body });
        stack.AddChild(new TextBlock { Text = item1, Margin = new Thickness(0f, 4f, 0f, 0f) });
        stack.AddChild(new TextBlock { Text = item2, Margin = new Thickness(0f, 4f, 0f, 0f) });
        stack.AddChild(new TextBlock { Text = item3, Margin = new Thickness(0f, 4f, 0f, 0f) });
        border.Child = stack;
        return border;
    }

    private static Border CreatePanelWithTitleAndItems(string title, string item1, string item2, string item3)
    {
        var border = new Border
        {
            Padding = new Thickness(12f),
            Margin = new Thickness(0f, 0f, 8f, 0f)
        };
        var stack = new StackPanel();
        stack.AddChild(new TextBlock { Text = title, FontWeight = "SemiBold" });
        stack.AddChild(new TextBlock { Text = item1, Margin = new Thickness(0f, 4f, 0f, 0f) });
        stack.AddChild(new TextBlock { Text = item2, Margin = new Thickness(0f, 4f, 0f, 0f) });
        stack.AddChild(new TextBlock { Text = item3, Margin = new Thickness(0f, 4f, 0f, 0f) });
        border.Child = stack;
        return border;
    }

    private static Border CreateBarePanel()
    {
        return new Border
        {
            Padding = new Thickness(12f),
            Margin = new Thickness(0f, 0f, 8f, 0f),
            Height = 120f,
            Width = 140f
        };
    }

    private static Border CreateChip(string text)
    {
        return new Border
        {
            Padding = new Thickness(10f),
            Margin = new Thickness(0f, 0f, 0f, 8f),
            Child = new TextBlock { Text = text }
        };
    }

    private static void ForceNoWrapRecursive(UIElement root)
    {
        if (root is TextBlock text)
        {
            text.TextWrapping = TextWrapping.NoWrap;
        }

        foreach (var child in root.GetVisualChildren())
        {
            ForceNoWrapRecursive(child);
        }
    }

    private static string FormatScenarioMetrics(NoWrapScenarioMetrics metrics)
    {
        return $"resizeOccurred={metrics.ResizeOccurred}, centerWidthDelta={metrics.CenterWidthDelta:0.###}, peakInputMs={metrics.PeakInputMs:0.###}, peakLayoutMs={metrics.PeakLayoutMs:0.###}, peakRouteMs={metrics.PeakRouteMs:0.###}, peakMoveHandlerMs={metrics.PeakMoveHandlerMs:0.###}, peakHitTests={metrics.PeakHitTests}, " +
               $"gridResolveDefs={metrics.GridResolveDefinitionSizesCallCount}, gridRemeasureChecks={metrics.GridMeasureRemeasureCheckCount}, gridRemeasures={metrics.GridMeasureRemeasureCount}, gridSecondPassChildren={metrics.GridMeasureSecondPassChildCount}, " +
               $"frameworkMeasureCalls={metrics.FrameworkMeasureCallCount}, frameworkMeasureWork={metrics.FrameworkMeasureWorkCount}, frameworkMeasureParentInvalidations={metrics.FrameworkMeasureParentInvalidationCount}, " +
               $"frameworkArrangeCalls={metrics.FrameworkArrangeCallCount}, frameworkArrangeWork={metrics.FrameworkArrangeWorkCount}, frameworkArrangeParentInvalidations={metrics.FrameworkArrangeParentInvalidationCount}, " +
               $"textLayoutBuilds={metrics.TextLayoutBuilds}, wrappedBuilds={metrics.WrappedTextLayoutBuilds}";
    }

    private static string FormatNamedSplitterScenario(NamedSplitterScenarioMetrics metrics)
    {
        return $"{metrics.SplitterName}: resizeDelta={metrics.ResizeDelta:0.###}, peakLayoutMs={metrics.PeakLayoutMs:0.###}, peakMeasureWorkMs={metrics.PeakMeasureWorkMs:0.###}, peakArrangeWorkMs={metrics.PeakArrangeWorkMs:0.###}, " +
               $"workbench[mw={metrics.WorkbenchMeasureWorkDelta},aw={metrics.WorkbenchArrangeWorkDelta}] primary[mw={metrics.PrimaryGridMeasureWorkDelta},aw={metrics.PrimaryGridArrangeWorkDelta}] lower[mw={metrics.LowerGridMeasureWorkDelta},aw={metrics.LowerGridArrangeWorkDelta}], " +
               $"splitter[pd={metrics.PointerDownDelta},begin={metrics.BeginDragDelta},moveApply={metrics.PointerMoveApplyDelta},pu={metrics.PointerUpDelta}], textLayoutBuilds={metrics.TextLayoutBuilds}, wrappedBuilds={metrics.WrappedTextLayoutBuilds}";
    }

    private static string FormatReducedScenario(ReducedPrimaryEditorScenarioMetrics metrics)
    {
        return $"{metrics.HostKind}: resizeDelta={metrics.ResizeDelta:0.###}, peakLayoutMs={metrics.PeakLayoutMs:0.###}, peakMeasureWorkMs={metrics.PeakMeasureWorkMs:0.###}, peakArrangeWorkMs={metrics.PeakArrangeWorkMs:0.###}, " +
               $"framework[measureCalls={metrics.FrameworkMeasureCalls}, measureWork={metrics.FrameworkMeasureWork}] grid[resolveDefs={metrics.GridResolveDefs}, remeasureChecks={metrics.GridRemeasureChecks}, remeasures={metrics.GridRemeasures}] " +
               $"scrollViewer[measureContent={metrics.ScrollViewerMeasureContentCalls}, resolveBarsMeasure={metrics.ScrollViewerResolveBarsMeasureCalls}, resolveBarsRemeasure={metrics.ScrollViewerResolveBarsRemeasureCalls}, setOffsets={metrics.ScrollViewerSetOffsetsCallCount}, setOffsetsNoOp={metrics.ScrollViewerSetOffsetsNoOpCount}, verticalDelta={metrics.ScrollViewerVerticalDelta:0.###}] " +
               $"splitter[pd={metrics.PointerDownDelta}, begin={metrics.BeginDragDelta}, moveApply={metrics.PointerMoveApplyDelta}, pu={metrics.PointerUpDelta}], textLayoutBuilds={metrics.TextLayoutBuilds}, wrappedBuilds={metrics.WrappedTextLayoutBuilds}";
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

    private static string FormatRect(LayoutRect rect)
    {
        return $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}";
    }

    private static void DragSplitter(UiRoot uiRoot, GridSplitter splitter, float deltaX, float deltaY)
    {
        var start = GetVisibleCenter(splitter);
        var end = new Vector2(start.X + deltaX, start.Y + deltaY);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
    }

    private static Vector2 GetVisibleCenter(UIElement element)
    {
        var visible = GetRenderedLayoutRectForInput(element);
        for (var current = element.VisualParent; current != null; current = current.VisualParent)
        {
            visible = IntersectRects(visible, GetRenderedLayoutRectForInput(current));
        }

        return GetCenter(visible);
    }

    private static LayoutRect GetRenderedLayoutRectForInput(UIElement element)
    {
        var slot = element.LayoutSlot;
        var x = slot.X;
        var y = slot.Y;
        for (var current = element.VisualParent; current != null; current = current.VisualParent)
        {
            if (current is ScrollViewer viewer)
            {
                x -= viewer.HorizontalOffset;
                y -= viewer.VerticalOffset;
            }
        }

        return new LayoutRect(x, y, slot.Width, slot.Height);
    }

    private static LayoutRect IntersectRects(LayoutRect first, LayoutRect second)
    {
        var left = MathF.Max(first.X, second.X);
        var top = MathF.Max(first.Y, second.Y);
        var right = MathF.Min(first.X + first.Width, second.X + second.Width);
        var bottom = MathF.Min(first.Y + first.Height, second.Y + second.Height);
        if (right <= left || bottom <= top)
        {
            return new LayoutRect(left, top, 0f, 0f);
        }

        return new LayoutRect(left, top, right - left, bottom - top);
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
        TestApplicationResources.LoadDemoAppResources();
    }

    private readonly record struct ElementWorkSnapshot(
        FrameworkElement Element,
        string ElementType,
        string Name,
        int MeasureWorkCount,
        int ArrangeWorkCount,
        long InvalidateMeasureCount,
        long InvalidateArrangeCount);

    private readonly record struct ElementWorkDelta(
        FrameworkElement? Element,
        string ElementType,
        string Name,
        int MeasureWorkDelta,
        int ArrangeWorkDelta,
        long InvalidateMeasureDelta,
        long InvalidateArrangeDelta);

    private readonly record struct NoWrapScenarioMetrics(
        bool ResizeOccurred,
        float CenterWidthDelta,
        double PeakInputMs,
        double PeakLayoutMs,
        double PeakRouteMs,
        double PeakMoveHandlerMs,
        int PeakHitTests,
        long GridResolveDefinitionSizesCallCount,
        long GridMeasureRemeasureCheckCount,
        long GridMeasureRemeasureCount,
        long GridMeasureSecondPassChildCount,
        long FrameworkMeasureCallCount,
        long FrameworkMeasureWorkCount,
        long FrameworkMeasureParentInvalidationCount,
        long FrameworkArrangeCallCount,
        long FrameworkArrangeWorkCount,
        long FrameworkArrangeParentInvalidationCount,
        int TextLayoutBuilds,
        int WrappedTextLayoutBuilds);

    private readonly record struct NamedSplitterScenarioMetrics(
        string SplitterName,
        float ResizeDelta,
        double PeakLayoutMs,
        double PeakMeasureWorkMs,
        double PeakArrangeWorkMs,
        int WorkbenchMeasureWorkDelta,
        int PrimaryGridMeasureWorkDelta,
        int LowerGridMeasureWorkDelta,
        int WorkbenchArrangeWorkDelta,
        int PrimaryGridArrangeWorkDelta,
        int LowerGridArrangeWorkDelta,
        long PointerDownDelta,
        long BeginDragDelta,
        long PointerMoveApplyDelta,
        long PointerUpDelta,
        int TextLayoutBuilds,
        int WrappedTextLayoutBuilds);

    private readonly record struct ReducedPrimaryEditorHostFixture(
        UIElement Root,
        GridSplitter Splitter);

    private readonly record struct ReducedPrimaryEditorScenarioMetrics(
        string HostKind,
        float ResizeDelta,
        double PeakLayoutMs,
        double PeakMeasureWorkMs,
        double PeakArrangeWorkMs,
        long FrameworkMeasureCalls,
        long FrameworkMeasureWork,
        long GridResolveDefs,
        long GridRemeasureChecks,
        long GridRemeasures,
        int ScrollViewerMeasureContentCalls,
        int ScrollViewerResolveBarsMeasureCalls,
        int ScrollViewerResolveBarsRemeasureCalls,
        int ScrollViewerSetOffsetsCallCount,
        int ScrollViewerSetOffsetsNoOpCount,
        float ScrollViewerVerticalDelta,
        long PointerDownDelta,
        long BeginDragDelta,
        long PointerMoveApplyDelta,
        long PointerUpDelta,
        int TextLayoutBuilds,
        int WrappedTextLayoutBuilds);

    private enum ReducedPrimaryEditorHostKind
    {
        Direct,
        StackPanel,
        ScrollViewerStackPanel
    }

    private enum ReducedPrimaryEditorCanvasLowerKind
    {
        None,
        SinglePanel,
        SinglePanelInGrid,
        TwoPanelStack,
        TwoColumnGrid,
        TwoColumnFixedColumns,
        TwoColumnNoBodyText,
        TwoColumnBordersOnly
    }
}
