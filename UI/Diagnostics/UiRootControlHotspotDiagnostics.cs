using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double ControlHotspotIdleFlushMs = 1000d;
    private const int ControlHotspotMinSamples = 12;
    private static readonly bool IsControlHotspotDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_CONTROL_HOTSPOT_LOGS"), "1", StringComparison.Ordinal);

    private sealed class ControlHotspotCounter
    {
        public double LayoutMs;
        public double DrawMs;
        public double DispatchMs;
        public int LayoutSamples;
        public int DrawSamples;
        public int DispatchSamples;

        public double TotalMs => LayoutMs + DrawMs + DispatchMs;
    }

    private readonly Dictionary<string, ControlHotspotCounter> _controlHotspots = new(StringComparer.Ordinal);
    private int _controlHotspotSampleCount;
    private long _controlHotspotLastActivityTimestamp;

    private void ObserveControlHotspotLayout(UIElement? control, double elapsedMs)
    {
        if (!IsControlHotspotDiagnosticsEnabled || control == null)
        {
            return;
        }

        var counter = GetOrCreateControlHotspotCounter(control);
        counter.LayoutMs += Math.Max(0d, elapsedMs);
        counter.LayoutSamples++;
        _controlHotspotSampleCount++;
        _controlHotspotLastActivityTimestamp = Stopwatch.GetTimestamp();
    }

    private void ObserveControlHotspotDraw(UIElement? control, double elapsedMs)
    {
        if (!IsControlHotspotDiagnosticsEnabled || control == null)
        {
            return;
        }

        var counter = GetOrCreateControlHotspotCounter(control);
        counter.DrawMs += Math.Max(0d, elapsedMs);
        counter.DrawSamples++;
        _controlHotspotSampleCount++;
        _controlHotspotLastActivityTimestamp = Stopwatch.GetTimestamp();
    }

    private void ObserveControlHotspotDispatch(UIElement? control, double elapsedMs)
    {
        if (!IsControlHotspotDiagnosticsEnabled || control == null)
        {
            return;
        }

        var counter = GetOrCreateControlHotspotCounter(control);
        counter.DispatchMs += Math.Max(0d, elapsedMs);
        counter.DispatchSamples++;
        _controlHotspotSampleCount++;
        _controlHotspotLastActivityTimestamp = Stopwatch.GetTimestamp();
    }

    private void ObserveControlHotspotAfterUpdate()
    {
        if (!IsControlHotspotDiagnosticsEnabled)
        {
            return;
        }

        if (LayoutPasses > 0 && _layoutRoot != null && LastLayoutPhaseMs > 0d)
        {
            ObserveControlHotspotLayout(_layoutRoot, LastLayoutPhaseMs);
        }

        TryFlushControlHotspotDiagnostics();
    }

    private void ObserveControlHotspotAfterDraw()
    {
        if (!IsControlHotspotDiagnosticsEnabled)
        {
            return;
        }

        TryFlushControlHotspotDiagnostics();
    }

    private void TryFlushControlHotspotDiagnostics()
    {
        if (_controlHotspotSampleCount < ControlHotspotMinSamples || _controlHotspotLastActivityTimestamp == 0)
        {
            return;
        }

        var idleMs = Stopwatch.GetElapsedTime(_controlHotspotLastActivityTimestamp).TotalMilliseconds;
        if (idleMs < ControlHotspotIdleFlushMs)
        {
            return;
        }

        if (_controlHotspots.Count == 0)
        {
            ResetControlHotspotDiagnostics();
            return;
        }

        var entries = new List<KeyValuePair<string, ControlHotspotCounter>>(_controlHotspots);
        entries.Sort(static (left, right) =>
        {
            var cmp = right.Value.TotalMs.CompareTo(left.Value.TotalMs);
            return cmp != 0 ? cmp : string.Compare(left.Key, right.Key, StringComparison.Ordinal);
        });

        var topCount = Math.Min(5, entries.Count);
        var parts = new string[topCount];
        for (var i = 0; i < topCount; i++)
        {
            var entry = entries[i];
            var counter = entry.Value;
            parts[i] =
                $"{entry.Key}(total={counter.TotalMs:0.00}ms,layout={counter.LayoutMs:0.00}ms,draw={counter.DrawMs:0.00}ms,dispatch={counter.DispatchMs:0.00}ms,samples=L{counter.LayoutSamples}/D{counter.DrawSamples}/P{counter.DispatchSamples})";
        }

        var summary = $"[ControlHotspots] samples={_controlHotspotSampleCount} top={string.Join(";", parts)}";
        Debug.WriteLine(summary);
        Console.WriteLine(summary);
        ResetControlHotspotDiagnostics();
    }

    private ControlHotspotCounter GetOrCreateControlHotspotCounter(UIElement control)
    {
        var key = control.GetType().Name;
        if (_controlHotspots.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var created = new ControlHotspotCounter();
        _controlHotspots[key] = created;
        return created;
    }

    private void ResetControlHotspotDiagnostics()
    {
        _controlHotspots.Clear();
        _controlHotspotSampleCount = 0;
        _controlHotspotLastActivityTimestamp = 0L;
    }
}