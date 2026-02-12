using System;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

public static class FrameLoopDiagnostics
{
    private const int MaxRecentHitches = 20;
    private const double HitchThresholdMilliseconds = 50d;
    private static readonly Queue<string> RecentHitchQueue = new();
    private static readonly Dictionary<UiRedrawReason, int> RedrawReasonHistogram = new();

    private static int _updateSampleCount;
    private static double _updateTotalMilliseconds;
    private static double _updateLastMilliseconds;
    private static double _updateMaxMilliseconds;
    private static double _updateLastPreUiMilliseconds;
    private static double _updateLastUiRootMilliseconds;
    private static double _updateLastBaseMilliseconds;
    private static UiRootUpdateTiming _lastUiRootUpdateTiming;

    private static int _drawSampleCount;
    private static double _drawTotalMilliseconds;
    private static double _drawLastMilliseconds;
    private static double _drawMaxMilliseconds;
    private static double _drawLastClearMilliseconds;
    private static double _drawLastBeginMilliseconds;
    private static double _drawLastUiRootMilliseconds;
    private static double _drawLastEndMilliseconds;
    private static double _drawLastBaseMilliseconds;
    private static UiRootDrawTiming _lastUiRootDrawTiming;
    private static int _drawLastDirtyRectCount;
    private static double _drawDirtyRectTotalCount;
    private static int _drawMaxDirtyRectCount;
    private static double _drawLastDirtyPixelArea;
    private static double _drawDirtyPixelAreaTotal;
    private static double _drawMaxDirtyPixelArea;
    private static double _drawLastDirtyViewportCoverage;
    private static double _drawDirtyViewportCoverageTotal;
    private static double _drawMaxDirtyViewportCoverage;
    private static int _drawLastFullRedrawFallbackCount;
    private static int _drawMaxFullRedrawFallbackCount;

    private static int _hitchCount;
    private static string _lastHitch = "None";

    public static void Reset()
    {
        _updateSampleCount = 0;
        _updateTotalMilliseconds = 0d;
        _updateLastMilliseconds = 0d;
        _updateMaxMilliseconds = 0d;
        _updateLastPreUiMilliseconds = 0d;
        _updateLastUiRootMilliseconds = 0d;
        _updateLastBaseMilliseconds = 0d;
        _lastUiRootUpdateTiming = default;

        _drawSampleCount = 0;
        _drawTotalMilliseconds = 0d;
        _drawLastMilliseconds = 0d;
        _drawMaxMilliseconds = 0d;
        _drawLastClearMilliseconds = 0d;
        _drawLastBeginMilliseconds = 0d;
        _drawLastUiRootMilliseconds = 0d;
        _drawLastEndMilliseconds = 0d;
        _drawLastBaseMilliseconds = 0d;
        _lastUiRootDrawTiming = default;
        _drawLastDirtyRectCount = 0;
        _drawDirtyRectTotalCount = 0d;
        _drawMaxDirtyRectCount = 0;
        _drawLastDirtyPixelArea = 0d;
        _drawDirtyPixelAreaTotal = 0d;
        _drawMaxDirtyPixelArea = 0d;
        _drawLastDirtyViewportCoverage = 0d;
        _drawDirtyViewportCoverageTotal = 0d;
        _drawMaxDirtyViewportCoverage = 0d;
        _drawLastFullRedrawFallbackCount = 0;
        _drawMaxFullRedrawFallbackCount = 0;

        _hitchCount = 0;
        _lastHitch = "None";
        RecentHitchQueue.Clear();
        RedrawReasonHistogram.Clear();
    }

    public static void RecordUpdate(
        long frameIndex,
        TimeSpan totalGameTime,
        double totalMilliseconds,
        double preUiMilliseconds,
        double uiRootMilliseconds,
        double baseMilliseconds,
        UiRootUpdateTiming uiRootTiming)
    {
        _updateSampleCount++;
        _updateTotalMilliseconds += totalMilliseconds;
        _updateLastMilliseconds = totalMilliseconds;
        _updateMaxMilliseconds = Math.Max(_updateMaxMilliseconds, totalMilliseconds);
        _updateLastPreUiMilliseconds = preUiMilliseconds;
        _updateLastUiRootMilliseconds = uiRootMilliseconds;
        _updateLastBaseMilliseconds = baseMilliseconds;
        _lastUiRootUpdateTiming = uiRootTiming;

        if (totalMilliseconds >= HitchThresholdMilliseconds ||
            preUiMilliseconds >= HitchThresholdMilliseconds ||
            uiRootMilliseconds >= HitchThresholdMilliseconds ||
            baseMilliseconds >= HitchThresholdMilliseconds ||
            uiRootTiming.TotalMilliseconds >= HitchThresholdMilliseconds)
        {
            AddHitch(
                $"U#{frameIndex} t={totalGameTime.TotalSeconds:0.000}s total={totalMilliseconds:0.###}ms " +
                $"pre={preUiMilliseconds:0.###} ui={uiRootMilliseconds:0.###} base={baseMilliseconds:0.###} " +
                $"[input {uiRootTiming.InputMilliseconds:0.###}, anim {uiRootTiming.AnimationMilliseconds:0.###}, layout {uiRootTiming.LayoutMilliseconds:0.###}, element {uiRootTiming.ElementUpdateMilliseconds:0.###}]");
        }
    }

    public static void RecordDraw(
        long frameIndex,
        TimeSpan totalGameTime,
        double totalMilliseconds,
        double clearMilliseconds,
        double beginMilliseconds,
        double uiRootMilliseconds,
        double endMilliseconds,
        double baseMilliseconds,
        UiRootDrawTiming uiRootTiming)
    {
        _drawSampleCount++;
        _drawTotalMilliseconds += totalMilliseconds;
        _drawLastMilliseconds = totalMilliseconds;
        _drawMaxMilliseconds = Math.Max(_drawMaxMilliseconds, totalMilliseconds);
        _drawLastClearMilliseconds = clearMilliseconds;
        _drawLastBeginMilliseconds = beginMilliseconds;
        _drawLastUiRootMilliseconds = uiRootMilliseconds;
        _drawLastEndMilliseconds = endMilliseconds;
        _drawLastBaseMilliseconds = baseMilliseconds;
        _lastUiRootDrawTiming = uiRootTiming;
        _drawLastDirtyRectCount = uiRootTiming.DirtyRectCount;
        _drawDirtyRectTotalCount += uiRootTiming.DirtyRectCount;
        _drawMaxDirtyRectCount = Math.Max(_drawMaxDirtyRectCount, uiRootTiming.DirtyRectCount);
        _drawLastDirtyPixelArea = uiRootTiming.DirtyPixelArea;
        _drawDirtyPixelAreaTotal += uiRootTiming.DirtyPixelArea;
        _drawMaxDirtyPixelArea = Math.Max(_drawMaxDirtyPixelArea, uiRootTiming.DirtyPixelArea);
        _drawLastDirtyViewportCoverage = uiRootTiming.DirtyViewportCoverage;
        _drawDirtyViewportCoverageTotal += uiRootTiming.DirtyViewportCoverage;
        _drawMaxDirtyViewportCoverage = Math.Max(_drawMaxDirtyViewportCoverage, uiRootTiming.DirtyViewportCoverage);
        _drawLastFullRedrawFallbackCount = uiRootTiming.FullRedrawFallbackCount;
        _drawMaxFullRedrawFallbackCount = Math.Max(_drawMaxFullRedrawFallbackCount, uiRootTiming.FullRedrawFallbackCount);
        RecordRedrawReasons(uiRootTiming.DrawReasons);

        if (totalMilliseconds >= HitchThresholdMilliseconds ||
            clearMilliseconds >= HitchThresholdMilliseconds ||
            beginMilliseconds >= HitchThresholdMilliseconds ||
            uiRootMilliseconds >= HitchThresholdMilliseconds ||
            endMilliseconds >= HitchThresholdMilliseconds ||
            baseMilliseconds >= HitchThresholdMilliseconds ||
            uiRootTiming.TotalMilliseconds >= HitchThresholdMilliseconds)
        {
            AddHitch(
                $"D#{frameIndex} t={totalGameTime.TotalSeconds:0.000}s total={totalMilliseconds:0.###}ms " +
                $"clear={clearMilliseconds:0.###} begin={beginMilliseconds:0.###} ui={uiRootMilliseconds:0.###} end={endMilliseconds:0.###} base={baseMilliseconds:0.###} " +
                $"[reset {uiRootTiming.ResetStateMilliseconds:0.###}, element {uiRootTiming.ElementDrawMilliseconds:0.###}]");
        }
    }

    public static FrameLoopDiagnosticsSnapshot GetSnapshot()
    {
        var avgUpdate = _updateSampleCount > 0 ? _updateTotalMilliseconds / _updateSampleCount : 0d;
        var avgDraw = _drawSampleCount > 0 ? _drawTotalMilliseconds / _drawSampleCount : 0d;
        var avgDirtyRectCount = _drawSampleCount > 0 ? _drawDirtyRectTotalCount / _drawSampleCount : 0d;
        var avgDirtyPixelArea = _drawSampleCount > 0 ? _drawDirtyPixelAreaTotal / _drawSampleCount : 0d;
        var avgDirtyViewportCoverage = _drawSampleCount > 0 ? _drawDirtyViewportCoverageTotal / _drawSampleCount : 0d;
        var recentHitches = RecentHitchQueue.Count == 0
            ? "None"
            : string.Join("\n", RecentHitchQueue);
        var redrawReasonTopText = BuildTopRedrawReasonText();

        return new FrameLoopDiagnosticsSnapshot(
            _updateSampleCount,
            _updateLastMilliseconds,
            avgUpdate,
            _updateMaxMilliseconds,
            _updateLastPreUiMilliseconds,
            _updateLastUiRootMilliseconds,
            _updateLastBaseMilliseconds,
            _lastUiRootUpdateTiming.InputMilliseconds,
            _lastUiRootUpdateTiming.AnimationMilliseconds,
            _lastUiRootUpdateTiming.LayoutMilliseconds,
            _lastUiRootUpdateTiming.ElementUpdateMilliseconds,
            _lastUiRootUpdateTiming.LayoutExecutedFrames,
            _lastUiRootUpdateTiming.LayoutSkippedFrames,
            _lastUiRootUpdateTiming.LayoutSkipRatio,
            _drawSampleCount,
            _drawLastMilliseconds,
            avgDraw,
            _drawMaxMilliseconds,
            _drawLastClearMilliseconds,
            _drawLastBeginMilliseconds,
            _drawLastUiRootMilliseconds,
            _drawLastEndMilliseconds,
            _drawLastBaseMilliseconds,
            _lastUiRootDrawTiming.ResetStateMilliseconds,
            _lastUiRootDrawTiming.ElementDrawMilliseconds,
            _lastUiRootDrawTiming.DrawExecutedFrames,
            _lastUiRootDrawTiming.DrawSkippedFrames,
            _lastUiRootDrawTiming.DrawSkipRatio,
            _drawLastDirtyRectCount,
            avgDirtyRectCount,
            _drawMaxDirtyRectCount,
            _drawLastDirtyPixelArea,
            avgDirtyPixelArea,
            _drawMaxDirtyPixelArea,
            _drawLastDirtyViewportCoverage,
            avgDirtyViewportCoverage,
            _drawMaxDirtyViewportCoverage,
            _drawLastFullRedrawFallbackCount,
            _drawMaxFullRedrawFallbackCount,
            _lastUiRootDrawTiming.DrawReasons,
            redrawReasonTopText,
            _hitchCount,
            _lastHitch,
            recentHitches);
    }

    private static void RecordRedrawReasons(UiRedrawReason drawReasons)
    {
        if (drawReasons == UiRedrawReason.None)
        {
            return;
        }

        foreach (UiRedrawReason reason in Enum.GetValues(typeof(UiRedrawReason)))
        {
            if (reason == UiRedrawReason.None)
            {
                continue;
            }

            if ((drawReasons & reason) == 0)
            {
                continue;
            }

            RedrawReasonHistogram.TryGetValue(reason, out var count);
            RedrawReasonHistogram[reason] = count + 1;
        }
    }

    private static string BuildTopRedrawReasonText()
    {
        if (RedrawReasonHistogram.Count == 0)
        {
            return "None";
        }

        var top = RedrawReasonHistogram
            .OrderByDescending(static pair => pair.Value)
            .ThenBy(static pair => pair.Key.ToString(), StringComparer.Ordinal)
            .Take(5)
            .Select(static pair => $"{pair.Key}:{pair.Value}");

        return string.Join(", ", top);
    }

    private static void AddHitch(string entry)
    {
        _hitchCount++;
        _lastHitch = entry;

        if (RecentHitchQueue.Count >= MaxRecentHitches)
        {
            RecentHitchQueue.Dequeue();
        }

        RecentHitchQueue.Enqueue(entry);
    }
}

public readonly record struct FrameLoopDiagnosticsSnapshot(
    int UpdateSampleCount,
    double LastUpdateMilliseconds,
    double AverageUpdateMilliseconds,
    double MaxUpdateMilliseconds,
    double LastUpdatePreUiMilliseconds,
    double LastUpdateUiRootMilliseconds,
    double LastUpdateBaseMilliseconds,
    double LastUiInputMilliseconds,
    double LastUiAnimationMilliseconds,
    double LastUiLayoutMilliseconds,
    double LastUiElementUpdateMilliseconds,
    int LayoutExecutedFrames,
    int LayoutSkippedFrames,
    double LayoutSkipRatio,
    int DrawSampleCount,
    double LastDrawMilliseconds,
    double AverageDrawMilliseconds,
    double MaxDrawMilliseconds,
    double LastDrawClearMilliseconds,
    double LastDrawBeginMilliseconds,
    double LastDrawUiRootMilliseconds,
    double LastDrawEndMilliseconds,
    double LastDrawBaseMilliseconds,
    double LastUiResetStateMilliseconds,
    double LastUiElementDrawMilliseconds,
    int DrawExecutedFrames,
    int DrawSkippedFrames,
    double DrawSkipRatio,
    int LastDirtyRectCount,
    double AverageDirtyRectCount,
    int MaxDirtyRectCount,
    double LastDirtyPixelArea,
    double AverageDirtyPixelArea,
    double MaxDirtyPixelArea,
    double LastDirtyViewportCoverage,
    double AverageDirtyViewportCoverage,
    double MaxDirtyViewportCoverage,
    int LastFullRedrawFallbackCount,
    int MaxFullRedrawFallbackCount,
    UiRedrawReason LastDrawReasons,
    string TopRedrawReasonsText,
    int HitchCount,
    string LastHitch,
    string RecentHitchesText);
