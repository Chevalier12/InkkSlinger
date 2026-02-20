using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double AllocationGcIdleFlushMs = 750d;
    private const int AllocationGcMinEventCount = 4;
    private static readonly bool IsAllocationGcDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_ALLOC_GC_LOGS"), "1", StringComparison.Ordinal);

    private bool _allocGcWindowActive;
    private long _allocGcWindowStartTimestamp;
    private long _allocGcLastActivityTimestamp;
    private long _allocGcBaselineAllocatedBytes;
    private int _allocGcBaselineGen0;
    private int _allocGcBaselineGen1;
    private int _allocGcBaselineGen2;
    private int _allocGcEventCount;
    private int _allocGcUpdateSampleCount;
    private int _allocGcDrawSampleCount;
    private double _allocGcUpdateMsTotal;
    private double _allocGcDrawMsTotal;
    private readonly Dictionary<string, int> _allocGcEventKinds = new(StringComparer.Ordinal);

    private void ObserveAllocationGcInteraction(string eventKind)
    {
        if (!IsAllocationGcDiagnosticsEnabled)
        {
            return;
        }

        if (!_allocGcWindowActive)
        {
            _allocGcWindowActive = true;
            _allocGcWindowStartTimestamp = Stopwatch.GetTimestamp();
            _allocGcBaselineAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
            _allocGcBaselineGen0 = GC.CollectionCount(0);
            _allocGcBaselineGen1 = GC.CollectionCount(1);
            _allocGcBaselineGen2 = GC.CollectionCount(2);
        }

        _allocGcLastActivityTimestamp = Stopwatch.GetTimestamp();
        _allocGcEventCount++;
        _allocGcEventKinds.TryGetValue(eventKind, out var count);
        _allocGcEventKinds[eventKind] = count + 1;
    }

    private void ObserveAllocationGcAfterUpdate()
    {
        if (!IsAllocationGcDiagnosticsEnabled || !_allocGcWindowActive)
        {
            return;
        }

        _allocGcUpdateSampleCount++;
        _allocGcUpdateMsTotal += LastUpdateMs;
        TryFlushAllocationGcDiagnostics();
    }

    private void ObserveAllocationGcAfterDraw()
    {
        if (!IsAllocationGcDiagnosticsEnabled || !_allocGcWindowActive)
        {
            return;
        }

        _allocGcDrawSampleCount++;
        _allocGcDrawMsTotal += LastDrawMs;
        TryFlushAllocationGcDiagnostics();
    }

    private void TryFlushAllocationGcDiagnostics()
    {
        if (!_allocGcWindowActive || _allocGcEventCount < AllocationGcMinEventCount || _allocGcLastActivityTimestamp == 0)
        {
            return;
        }

        var idleMs = Stopwatch.GetElapsedTime(_allocGcLastActivityTimestamp).TotalMilliseconds;
        if (idleMs < AllocationGcIdleFlushMs)
        {
            return;
        }

        var durationMs = Stopwatch.GetElapsedTime(_allocGcWindowStartTimestamp).TotalMilliseconds;
        var allocatedDeltaBytes = GC.GetTotalAllocatedBytes(precise: false) - _allocGcBaselineAllocatedBytes;
        var gen0Delta = GC.CollectionCount(0) - _allocGcBaselineGen0;
        var gen1Delta = GC.CollectionCount(1) - _allocGcBaselineGen1;
        var gen2Delta = GC.CollectionCount(2) - _allocGcBaselineGen2;
        var avgUpdateMs = _allocGcUpdateSampleCount > 0 ? _allocGcUpdateMsTotal / _allocGcUpdateSampleCount : 0d;
        var avgDrawMs = _allocGcDrawSampleCount > 0 ? _allocGcDrawMsTotal / _allocGcDrawSampleCount : 0d;

        var eventKinds = BuildEventKindsSummary(_allocGcEventKinds);
        var summary =
            $"[AllocGc] events={_allocGcEventCount} kinds={eventKinds} dur={durationMs:0}ms allocDelta={allocatedDeltaBytes}B " +
            $"gc(gen0={gen0Delta},gen1={gen1Delta},gen2={gen2Delta}) avg(update={avgUpdateMs:0.0}ms,draw={avgDrawMs:0.0}ms)";

        Debug.WriteLine(summary);
        Console.WriteLine(summary);
        ResetAllocationGcDiagnostics();
    }

    private static string BuildEventKindsSummary(Dictionary<string, int> kinds)
    {
        if (kinds.Count == 0)
        {
            return "none";
        }

        var entries = new List<KeyValuePair<string, int>>(kinds);
        entries.Sort(static (left, right) =>
        {
            var cmp = right.Value.CompareTo(left.Value);
            return cmp != 0 ? cmp : string.Compare(left.Key, right.Key, StringComparison.Ordinal);
        });

        var parts = new string[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            parts[i] = $"{entries[i].Key}:{entries[i].Value}";
        }

        return string.Join(",", parts);
    }

    private void ResetAllocationGcDiagnostics()
    {
        _allocGcWindowActive = false;
        _allocGcWindowStartTimestamp = 0L;
        _allocGcLastActivityTimestamp = 0L;
        _allocGcBaselineAllocatedBytes = 0L;
        _allocGcBaselineGen0 = 0;
        _allocGcBaselineGen1 = 0;
        _allocGcBaselineGen2 = 0;
        _allocGcEventCount = 0;
        _allocGcUpdateSampleCount = 0;
        _allocGcDrawSampleCount = 0;
        _allocGcUpdateMsTotal = 0d;
        _allocGcDrawMsTotal = 0d;
        _allocGcEventKinds.Clear();
    }
}