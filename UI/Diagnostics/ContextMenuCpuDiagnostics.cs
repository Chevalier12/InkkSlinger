using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace InkkSlinger;

internal readonly record struct ContextMenuResolveStats(
    bool Resolved,
    string Path,
    double OverlayLookupMs,
    double MenuHitTestMs,
    int NodesVisited,
    int OpenBranchesVisited,
    int RootItemsVisited,
    int RowChecks,
    int BoundsChecks,
    int MaxDepth,
    int UninitializedBoundsFallbackCount,
    double InternalMenuHitTestMs);

internal readonly record struct ContextMenuOpenBreakdown(
    int MenuMeasureDelta,
    int MenuArrangeDelta,
    int MenuRenderDelta,
    int RootMeasureDelta,
    int RootArrangeDelta,
    int RootRenderDelta);

internal static class ContextMenuCpuDiagnostics
{
    private static readonly bool IsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_CONTEXTMENU_CPU_LOGS"), "1", StringComparison.Ordinal);

    private const int HoverFlushEvery = 120;
    private const int OpenFlushEvery = 40;
    private const int InvokeFlushEvery = 40;
    private const double HoverSlowThresholdMs = 1.25d;
    private const double HoverStallWarnThresholdMs = 16d;
    private const double HoverSpikeWarnThresholdMs = 50d;
    private const double OpenSlowThresholdMs = 2.50d;
    private const double InvokeSlowThresholdMs = 1.50d;
    private const int MaxSamples = 256;
    private const double SlowWarnThrottleMs = 1500d;

    private static readonly List<double> HoverSamples = new();
    private static readonly List<double> HoverResolveSamples = new();
    private static readonly List<double> HoverOverlayLookupSamples = new();
    private static readonly List<double> HoverMenuHitTestSamples = new();
    private static readonly List<double> HoverHandleSamples = new();
    private static readonly List<double> HoverNodesVisitedSamples = new();
    private static readonly List<double> HoverOpenBranchesVisitedSamples = new();
    private static readonly List<double> HoverRootsVisitedSamples = new();
    private static readonly List<double> HoverRowChecksSamples = new();
    private static readonly List<double> HoverBoundsChecksSamples = new();
    private static readonly List<double> HoverMaxDepthSamples = new();
    private static readonly List<double> HoverUninitializedBoundsFallbackSamples = new();
    private static readonly List<double> HoverInternalMenuHitTestSamples = new();
    private static readonly List<double> HoverMenuHitTestGapSamples = new();
    private static readonly List<double> OpenSamples = new();
    private static readonly List<double> OpenFirstSamples = new();
    private static readonly List<double> OpenWarmSamples = new();
    private static readonly List<double> OpenMenuMeasureDeltaSamples = new();
    private static readonly List<double> OpenMenuArrangeDeltaSamples = new();
    private static readonly List<double> OpenMenuRenderDeltaSamples = new();
    private static readonly List<double> OpenRootMeasureDeltaSamples = new();
    private static readonly List<double> OpenRootArrangeDeltaSamples = new();
    private static readonly List<double> OpenRootRenderDeltaSamples = new();
    private static readonly List<double> InvokeSamples = new();

    private static int _hoverCount;
    private static int _hoverSlowCount;
    private static int _hoverResolvedCount;
    private static int _hoverResolveHitTestTotal;
    private static int _openCount;
    private static int _openSuccessCount;
    private static int _openSlowCount;
    private static int _openFirstCount;
    private static int _openWarmCount;
    private static int _openFirstSlowCount;
    private static int _openWarmSlowCount;
    private static int _openSourcePointerCount;
    private static int _openSourceKeyboardCount;
    private static int _invokeCount;
    private static int _invokeHandledCount;
    private static int _invokeSlowCount;
    private static int _hoverPathTargetMenuCount;
    private static int _hoverPathOwnerMenuCount;
    private static int _hoverPathKnownOpenMenuCount;
    private static int _hoverPathOverlayLookupCount;
    private static int _hoverSlowAbove16MsCount;
    private static int _hoverSlowAbove50MsCount;
    private static long _lastSlowWarnTimestamp;
    private static readonly HashSet<ContextMenu> OpenedMenus = new(ReferenceEqualityComparer.Instance);

    internal static void ObserveHoverDispatch(ContextMenuResolveStats resolveStats, double handleMs)
    {
        if (!IsEnabled)
        {
            return;
        }

        var resolveMs = resolveStats.OverlayLookupMs + resolveStats.MenuHitTestMs;
        var elapsedMs = resolveMs + handleMs;
        _hoverCount++;
        if (resolveStats.Resolved)
        {
            _hoverResolvedCount++;
        }
        _hoverResolveHitTestTotal += Math.Max(0, resolveStats.NodesVisited);
        switch (resolveStats.Path)
        {
            case "TargetContextMenu":
                _hoverPathTargetMenuCount++;
                break;
            case "OwnerContextMenu":
                _hoverPathOwnerMenuCount++;
                break;
            case "KnownOpenContextMenu":
                _hoverPathKnownOpenMenuCount++;
                break;
            case "OverlayLookup":
                _hoverPathOverlayLookupCount++;
                break;
        }

        if (elapsedMs >= HoverSlowThresholdMs)
        {
            _hoverSlowCount++;
            if (elapsedMs >= HoverStallWarnThresholdMs)
            {
                _hoverSlowAbove16MsCount++;
            }

            if (elapsedMs >= HoverSpikeWarnThresholdMs)
            {
                _hoverSlowAbove50MsCount++;
            }

            EmitThrottledSlowHover(elapsedMs, resolveMs, handleMs, resolveStats);
        }

        AddSample(HoverSamples, elapsedMs);
        AddSample(HoverResolveSamples, resolveMs);
        AddSample(HoverOverlayLookupSamples, resolveStats.OverlayLookupMs);
        AddSample(HoverMenuHitTestSamples, resolveStats.MenuHitTestMs);
        AddSample(HoverHandleSamples, handleMs);
        AddSample(HoverNodesVisitedSamples, resolveStats.NodesVisited);
        AddSample(HoverOpenBranchesVisitedSamples, resolveStats.OpenBranchesVisited);
        AddSample(HoverRootsVisitedSamples, resolveStats.RootItemsVisited);
        AddSample(HoverRowChecksSamples, resolveStats.RowChecks);
        AddSample(HoverBoundsChecksSamples, resolveStats.BoundsChecks);
        AddSample(HoverMaxDepthSamples, resolveStats.MaxDepth);
        AddSample(HoverUninitializedBoundsFallbackSamples, resolveStats.UninitializedBoundsFallbackCount);
        AddSample(HoverInternalMenuHitTestSamples, resolveStats.InternalMenuHitTestMs);
        AddSample(HoverMenuHitTestGapSamples, Math.Max(0d, resolveStats.MenuHitTestMs - resolveStats.InternalMenuHitTestMs));
        if (_hoverCount % HoverFlushEvery == 0)
        {
            FlushHover();
        }
    }

    internal static void ObserveOpenAttempt(
        double elapsedMs,
        bool opened,
        string source,
        ContextMenu? menu = null,
        ContextMenuOpenBreakdown? breakdown = null)
    {
        if (!IsEnabled)
        {
            return;
        }

        _openCount++;
        if (opened)
        {
            _openSuccessCount++;
        }

        var isFirstOpen = false;
        if (opened && menu != null)
        {
            isFirstOpen = OpenedMenus.Add(menu);
            if (isFirstOpen)
            {
                _openFirstCount++;
                AddSample(OpenFirstSamples, elapsedMs);
            }
            else
            {
                _openWarmCount++;
                AddSample(OpenWarmSamples, elapsedMs);
            }
        }

        if (string.Equals(source, "Pointer", StringComparison.Ordinal))
        {
            _openSourcePointerCount++;
        }
        else if (string.Equals(source, "Keyboard", StringComparison.Ordinal))
        {
            _openSourceKeyboardCount++;
        }

        if (opened && breakdown is { } openBreakdown)
        {
            AddSample(OpenMenuMeasureDeltaSamples, openBreakdown.MenuMeasureDelta);
            AddSample(OpenMenuArrangeDeltaSamples, openBreakdown.MenuArrangeDelta);
            AddSample(OpenMenuRenderDeltaSamples, openBreakdown.MenuRenderDelta);
            AddSample(OpenRootMeasureDeltaSamples, openBreakdown.RootMeasureDelta);
            AddSample(OpenRootArrangeDeltaSamples, openBreakdown.RootArrangeDelta);
            AddSample(OpenRootRenderDeltaSamples, openBreakdown.RootRenderDelta);
        }

        if (elapsedMs >= OpenSlowThresholdMs)
        {
            _openSlowCount++;
            if (opened && isFirstOpen)
            {
                _openFirstSlowCount++;
            }
            else if (opened)
            {
                _openWarmSlowCount++;
            }

            EmitSlowOpenEvent(elapsedMs, OpenSlowThresholdMs, source, opened, isFirstOpen, breakdown);
        }

        AddSample(OpenSamples, elapsedMs);
        if (_openCount % OpenFlushEvery == 0)
        {
            FlushOpen();
        }
    }

    internal static void ObserveInvoke(double elapsedMs, bool handled)
    {
        if (!IsEnabled)
        {
            return;
        }

        _invokeCount++;
        if (handled)
        {
            _invokeHandledCount++;
        }

        if (elapsedMs >= InvokeSlowThresholdMs)
        {
            _invokeSlowCount++;
            EmitSlowEvent("Invoke", elapsedMs, InvokeSlowThresholdMs);
        }

        AddSample(InvokeSamples, elapsedMs);
        if (_invokeCount % InvokeFlushEvery == 0)
        {
            Flush("Invoke", InvokeSamples, _invokeCount, _invokeSlowCount, _invokeHandledCount, metricLabel: "handled");
        }
    }

    private static void Flush(
        string stage,
        List<double> samples,
        int totalCount,
        int slowCount,
        int positiveCount,
        string metricLabel)
    {
        if (samples.Count == 0)
        {
            return;
        }

        var ordered = new List<double>(samples);
        ordered.Sort();
        var p50 = Percentile(ordered, 0.50d);
        var p95 = Percentile(ordered, 0.95d);
        var max = ordered[^1];
        var positiveRate = (double)positiveCount / Math.Max(1, totalCount);
        var slowRate = (double)slowCount / Math.Max(1, totalCount);

        var line =
            $"[ContextMenuCpu.{stage}] count={totalCount} {metricLabel}Rate={positiveRate:P1} " +
            $"slowRate={slowRate:P1} p50={p50:0.###}ms p95={p95:0.###}ms max={max:0.###}ms";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    private static void FlushHover()
    {
        if (HoverSamples.Count == 0)
        {
            return;
        }

        var total = new List<double>(HoverSamples);
        total.Sort();
        var resolve = new List<double>(HoverResolveSamples);
        resolve.Sort();
        var overlayLookup = new List<double>(HoverOverlayLookupSamples);
        overlayLookup.Sort();
        var menuHitTest = new List<double>(HoverMenuHitTestSamples);
        menuHitTest.Sort();
        var handle = new List<double>(HoverHandleSamples);
        handle.Sort();
        var nodes = new List<double>(HoverNodesVisitedSamples);
        nodes.Sort();
        var openBranches = new List<double>(HoverOpenBranchesVisitedSamples);
        openBranches.Sort();
        var roots = new List<double>(HoverRootsVisitedSamples);
        roots.Sort();
        var rowChecks = new List<double>(HoverRowChecksSamples);
        rowChecks.Sort();
        var boundsChecks = new List<double>(HoverBoundsChecksSamples);
        boundsChecks.Sort();
        var maxDepth = new List<double>(HoverMaxDepthSamples);
        maxDepth.Sort();
        var uninitializedBoundsFallback = new List<double>(HoverUninitializedBoundsFallbackSamples);
        uninitializedBoundsFallback.Sort();
        var internalMenuHitTest = new List<double>(HoverInternalMenuHitTestSamples);
        internalMenuHitTest.Sort();
        var menuHitTestGap = new List<double>(HoverMenuHitTestGapSamples);
        menuHitTestGap.Sort();

        var totalP50 = Percentile(total, 0.50d);
        var totalP95 = Percentile(total, 0.95d);
        var totalMax = total[^1];
        var resolveP95 = Percentile(resolve, 0.95d);
        var overlayLookupP95 = Percentile(overlayLookup, 0.95d);
        var menuHitTestP95 = Percentile(menuHitTest, 0.95d);
        var handleP95 = Percentile(handle, 0.95d);
        var nodesP95 = Percentile(nodes, 0.95d);
        var openBranchesP95 = Percentile(openBranches, 0.95d);
        var rootsP95 = Percentile(roots, 0.95d);
        var rowChecksP95 = Percentile(rowChecks, 0.95d);
        var boundsChecksP95 = Percentile(boundsChecks, 0.95d);
        var maxDepthP95 = Percentile(maxDepth, 0.95d);
        var uninitializedBoundsFallbackP95 = Percentile(uninitializedBoundsFallback, 0.95d);
        var internalMenuHitTestP95 = Percentile(internalMenuHitTest, 0.95d);
        var menuHitTestGapP95 = Percentile(menuHitTestGap, 0.95d);
        var resolvedRate = (double)_hoverResolvedCount / Math.Max(1, _hoverCount);
        var slowRate = (double)_hoverSlowCount / Math.Max(1, _hoverCount);
        var avgResolveHitTests = (double)_hoverResolveHitTestTotal / Math.Max(1, _hoverCount);
        var pathTargetRate = (double)_hoverPathTargetMenuCount / Math.Max(1, _hoverCount);
        var pathOwnerRate = (double)_hoverPathOwnerMenuCount / Math.Max(1, _hoverCount);
        var pathKnownOpenRate = (double)_hoverPathKnownOpenMenuCount / Math.Max(1, _hoverCount);
        var pathOverlayRate = (double)_hoverPathOverlayLookupCount / Math.Max(1, _hoverCount);
        var over16Rate = (double)_hoverSlowAbove16MsCount / Math.Max(1, _hoverCount);
        var over50Rate = (double)_hoverSlowAbove50MsCount / Math.Max(1, _hoverCount);

        var line =
            $"[ContextMenuCpu.Hover] count={_hoverCount} resolvedRate={resolvedRate:P1} slowRate={slowRate:P1} " +
            $"total(p50={totalP50:0.###}ms p95={totalP95:0.###}ms max={totalMax:0.###}ms) " +
            $"resolve(p95={resolveP95:0.###}ms overlayLookupP95={overlayLookupP95:0.###}ms menuHitTestP95={menuHitTestP95:0.###}ms internalMenuHitTestP95={internalMenuHitTestP95:0.###}ms menuHitTestGapP95={menuHitTestGapP95:0.###}ms) " +
            $"tree(avgNodes={avgResolveHitTests:0.##} p95Roots={rootsP95:0.##} p95Nodes={nodesP95:0.##} p95OpenBranches={openBranchesP95:0.##} p95Depth={maxDepthP95:0.##} p95RowChecks={rowChecksP95:0.##} p95BoundsChecks={boundsChecksP95:0.##} p95UninitBoundsFallback={uninitializedBoundsFallbackP95:0.##}) " +
            $"paths(target={pathTargetRate:P1} owner={pathOwnerRate:P1} knownOpen={pathKnownOpenRate:P1} overlayLookup={pathOverlayRate:P1}) " +
            $"stalls(>16ms={over16Rate:P1} >50ms={over50Rate:P1}) " +
            $"handle(p95={handleP95:0.###}ms)";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    private static void FlushOpen()
    {
        if (OpenSamples.Count == 0)
        {
            return;
        }

        var total = new List<double>(OpenSamples);
        total.Sort();
        var totalP50 = Percentile(total, 0.50d);
        var totalP95 = Percentile(total, 0.95d);
        var totalMax = total[^1];
        var successRate = (double)_openSuccessCount / Math.Max(1, _openCount);
        var slowRate = (double)_openSlowCount / Math.Max(1, _openCount);
        var pointerRate = (double)_openSourcePointerCount / Math.Max(1, _openCount);
        var keyboardRate = (double)_openSourceKeyboardCount / Math.Max(1, _openCount);

        var firstP95 = OpenFirstSamples.Count == 0 ? 0d : Percentile(Sorted(OpenFirstSamples), 0.95d);
        var warmP95 = OpenWarmSamples.Count == 0 ? 0d : Percentile(Sorted(OpenWarmSamples), 0.95d);
        var firstSlowRate = (double)_openFirstSlowCount / Math.Max(1, _openFirstCount);
        var warmSlowRate = (double)_openWarmSlowCount / Math.Max(1, _openWarmCount);

        var menuMeasureP95 = OpenMenuMeasureDeltaSamples.Count == 0 ? 0d : Percentile(Sorted(OpenMenuMeasureDeltaSamples), 0.95d);
        var menuArrangeP95 = OpenMenuArrangeDeltaSamples.Count == 0 ? 0d : Percentile(Sorted(OpenMenuArrangeDeltaSamples), 0.95d);
        var menuRenderP95 = OpenMenuRenderDeltaSamples.Count == 0 ? 0d : Percentile(Sorted(OpenMenuRenderDeltaSamples), 0.95d);
        var rootMeasureP95 = OpenRootMeasureDeltaSamples.Count == 0 ? 0d : Percentile(Sorted(OpenRootMeasureDeltaSamples), 0.95d);
        var rootArrangeP95 = OpenRootArrangeDeltaSamples.Count == 0 ? 0d : Percentile(Sorted(OpenRootArrangeDeltaSamples), 0.95d);
        var rootRenderP95 = OpenRootRenderDeltaSamples.Count == 0 ? 0d : Percentile(Sorted(OpenRootRenderDeltaSamples), 0.95d);

        var line =
            $"[ContextMenuCpu.Open] count={_openCount} successRate={successRate:P1} slowRate={slowRate:P1} " +
            $"total(p50={totalP50:0.###}ms p95={totalP95:0.###}ms max={totalMax:0.###}ms) " +
            $"buckets(first count={_openFirstCount} slowRate={firstSlowRate:P1} p95={firstP95:0.###}ms warm count={_openWarmCount} slowRate={warmSlowRate:P1} p95={warmP95:0.###}ms) " +
            $"source(pointer={pointerRate:P1} keyboard={keyboardRate:P1}) " +
            $"invalidation(menu p95[m={menuMeasureP95:0.##} a={menuArrangeP95:0.##} r={menuRenderP95:0.##}] root p95[m={rootMeasureP95:0.##} a={rootArrangeP95:0.##} r={rootRenderP95:0.##}])";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    private static void EmitThrottledSlowHover(double totalMs, double resolveMs, double handleMs, ContextMenuResolveStats resolveStats)
    {
        var now = Stopwatch.GetTimestamp();
        if (_lastSlowWarnTimestamp != 0 &&
            Stopwatch.GetElapsedTime(_lastSlowWarnTimestamp).TotalMilliseconds < SlowWarnThrottleMs)
        {
            return;
        }

        _lastSlowWarnTimestamp = now;
        var line =
            $"[ContextMenuCpu.Hover.Warn] total={totalMs:0.###}ms resolve={resolveMs:0.###}ms " +
            $"(overlayLookup={resolveStats.OverlayLookupMs:0.###}ms menuHitTest={resolveStats.MenuHitTestMs:0.###}ms) " +
            $"handle={handleMs:0.###}ms path={resolveStats.Path} roots={resolveStats.RootItemsVisited} nodes={resolveStats.NodesVisited} openBranches={resolveStats.OpenBranchesVisited} depth={resolveStats.MaxDepth} rowChecks={resolveStats.RowChecks} boundsChecks={resolveStats.BoundsChecks} uninitBoundsFallback={resolveStats.UninitializedBoundsFallbackCount} internalMenuHitTest={resolveStats.InternalMenuHitTestMs:0.###}ms gap={Math.Max(0d, resolveStats.MenuHitTestMs - resolveStats.InternalMenuHitTestMs):0.###}ms";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    private static void EmitSlowOpenEvent(
        double elapsedMs,
        double thresholdMs,
        string source,
        bool opened,
        bool isFirstOpen,
        ContextMenuOpenBreakdown? breakdown)
    {
        var bucket = opened ? (isFirstOpen ? "First" : "Warm") : "Miss";
        var breakdownText = breakdown is { } b
            ? $" invalidation(menu m={b.MenuMeasureDelta} a={b.MenuArrangeDelta} r={b.MenuRenderDelta}; root m={b.RootMeasureDelta} a={b.RootArrangeDelta} r={b.RootRenderDelta})"
            : string.Empty;
        var line = $"[ContextMenuCpu.Open.{bucket}.Slow] elapsed={elapsedMs:0.###}ms threshold={thresholdMs:0.###}ms source={source}{breakdownText}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    private static void EmitSlowEvent(string stage, double elapsedMs, double thresholdMs)
    {
        var line = $"[ContextMenuCpu.{stage}.Slow] elapsed={elapsedMs:0.###}ms threshold={thresholdMs:0.###}ms";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    private static List<double> Sorted(List<double> samples)
    {
        var sorted = new List<double>(samples);
        sorted.Sort();
        return sorted;
    }

    private static void AddSample(List<double> bucket, double value)
    {
        bucket.Add(value);
        if (bucket.Count > MaxSamples)
        {
            bucket.RemoveAt(0);
        }
    }

    private static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0)
        {
            return 0d;
        }

        var rank = p * (sorted.Count - 1);
        var low = (int)Math.Floor(rank);
        var high = (int)Math.Ceiling(rank);
        if (low == high)
        {
            return sorted[low];
        }

        var weight = rank - low;
        return (sorted[low] * (1d - weight)) + (sorted[high] * weight);
    }
}
