using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double RenderCacheChurnIdleFlushMs = 900d;
    private const int RenderCacheChurnMinFrameSamples = 6;
    private static readonly bool IsRenderCacheChurnDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_RENDER_CACHE_CHURN_LOGS"), "1", StringComparison.Ordinal);

    private int _cacheChurnFrameCount;
    private int _cacheChurnActiveFrameCount;
    private int _cacheChurnHitCount;
    private int _cacheChurnMissCount;
    private int _cacheChurnRebuildCount;
    private long _cacheChurnLastActivityTimestamp;
    private readonly Dictionary<string, int> _cacheChurnInvalidationSourceCounts = new(StringComparer.Ordinal);

    private void ObserveRenderCacheInvalidationSource(UIElement? source)
    {
        if (!IsRenderCacheChurnDiagnosticsEnabled || source == null)
        {
            return;
        }

        var key = source.GetType().Name;
        _cacheChurnInvalidationSourceCounts.TryGetValue(key, out var count);
        _cacheChurnInvalidationSourceCounts[key] = count + 1;
        _cacheChurnLastActivityTimestamp = Stopwatch.GetTimestamp();
    }

    private void ObserveRenderCacheChurnAfterDraw()
    {
        if (!IsRenderCacheChurnDiagnosticsEnabled)
        {
            return;
        }

        _cacheChurnFrameCount++;
        var frameOps = LastFrameCacheHitCount + LastFrameCacheMissCount + LastFrameCacheRebuildCount;
        if (frameOps > 0)
        {
            _cacheChurnActiveFrameCount++;
            _cacheChurnHitCount += LastFrameCacheHitCount;
            _cacheChurnMissCount += LastFrameCacheMissCount;
            _cacheChurnRebuildCount += LastFrameCacheRebuildCount;
            _cacheChurnLastActivityTimestamp = Stopwatch.GetTimestamp();
        }

        TryFlushRenderCacheChurnDiagnostics();
    }

    private void ObserveRenderCacheChurnAfterUpdate()
    {
        if (!IsRenderCacheChurnDiagnosticsEnabled)
        {
            return;
        }

        TryFlushRenderCacheChurnDiagnostics();
    }

    private void TryFlushRenderCacheChurnDiagnostics()
    {
        if (_cacheChurnFrameCount < RenderCacheChurnMinFrameSamples || _cacheChurnLastActivityTimestamp == 0)
        {
            return;
        }

        var idleMs = Stopwatch.GetElapsedTime(_cacheChurnLastActivityTimestamp).TotalMilliseconds;
        if (idleMs < RenderCacheChurnIdleFlushMs)
        {
            return;
        }

        var totalOps = _cacheChurnHitCount + _cacheChurnMissCount + _cacheChurnRebuildCount;
        var hitPct = totalOps > 0 ? (_cacheChurnHitCount * 100d) / totalOps : 0d;
        var missPct = totalOps > 0 ? (_cacheChurnMissCount * 100d) / totalOps : 0d;
        var rebuildPct = totalOps > 0 ? (_cacheChurnRebuildCount * 100d) / totalOps : 0d;
        var activeFramePct = _cacheChurnFrameCount > 0
            ? (_cacheChurnActiveFrameCount * 100d) / _cacheChurnFrameCount
            : 0d;

        var topSources = GetTopSources(_cacheChurnInvalidationSourceCounts, 5);
        var summary =
            $"[RenderCacheChurn] frames={_cacheChurnFrameCount} activeFrames={_cacheChurnActiveFrameCount} activeRate={activeFramePct:0.0}% " +
            $"ops(hit={_cacheChurnHitCount},miss={_cacheChurnMissCount},rebuild={_cacheChurnRebuildCount},hitRate={hitPct:0.0}%,missRate={missPct:0.0}%,rebuildRate={rebuildPct:0.0}%) " +
            $"topInvalidationSources={topSources}";

        Debug.WriteLine(summary);
        Console.WriteLine(summary);
        ResetRenderCacheChurnDiagnostics();
    }

    private static string GetTopSources(Dictionary<string, int> sourceCounts, int take)
    {
        if (sourceCounts.Count == 0)
        {
            return "none";
        }

        var entries = new List<KeyValuePair<string, int>>(sourceCounts);
        entries.Sort(static (left, right) =>
        {
            var cmp = right.Value.CompareTo(left.Value);
            return cmp != 0 ? cmp : string.Compare(left.Key, right.Key, StringComparison.Ordinal);
        });

        var count = Math.Min(take, entries.Count);
        var parts = new string[count];
        for (var i = 0; i < count; i++)
        {
            var entry = entries[i];
            parts[i] = $"{entry.Key}:{entry.Value}";
        }

        return string.Join(",", parts);
    }

    private void ResetRenderCacheChurnDiagnostics()
    {
        _cacheChurnFrameCount = 0;
        _cacheChurnActiveFrameCount = 0;
        _cacheChurnHitCount = 0;
        _cacheChurnMissCount = 0;
        _cacheChurnRebuildCount = 0;
        _cacheChurnLastActivityTimestamp = 0L;
        _cacheChurnInvalidationSourceCounts.Clear();
    }
}