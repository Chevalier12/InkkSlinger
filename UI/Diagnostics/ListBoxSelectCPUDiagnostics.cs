using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal static class ListBoxSelectCPUDiagnostics
{
    private static readonly bool IsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_LISTBOX_SELECT_CPU_LOGS"), "1", StringComparison.Ordinal);
    private const int FlushEverySelections = 10;
    private const int MaxLatencySamples = 128;

    private static readonly Dictionary<ListBox, PendingSelectionContext> PendingByListBox = new();
    private static readonly List<double> ClickToSelectionSamples = new();
    private static int _pointerDownCount;
    private static int _selectionChangedCount;
    private static int _unmatchedPointerDownCount;
    private static double _totalClickToSelectionMs;
    private static double _maxClickToSelectionMs;

    internal static void ObservePointerDownCandidate(
        UIElement? target,
        Vector2 pointerPosition,
        double resolveMs,
        int clickHitTests,
        string resolvePath,
        HitTestMetrics? hitTestMetrics)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (target == null || !TryFindAncestorListBox(target, out var listBox) || listBox == null)
        {
            return;
        }

        if (PendingByListBox.ContainsKey(listBox))
        {
            _unmatchedPointerDownCount++;
        }

        _pointerDownCount++;
        PendingByListBox[listBox] = new PendingSelectionContext(
            Stopwatch.GetTimestamp(),
            target.GetType().Name,
            BuildAncestry(target),
            pointerPosition.X,
            pointerPosition.Y,
            listBox.SelectedIndex,
            listBox.SelectionMode.ToString(),
            resolveMs,
            Math.Max(0, clickHitTests),
            resolvePath,
            hitTestMetrics);
    }

    internal static void ObserveSelectionChanged(ListBox listBox, int selectedIndex, int addedCount, int removedCount)
    {
        if (!IsEnabled)
        {
            return;
        }

        _selectionChangedCount++;

        var hadPendingPointer = PendingByListBox.TryGetValue(listBox, out var pending);
        var clickToSelectionMs = hadPendingPointer
            ? Stopwatch.GetElapsedTime(pending.PointerDownTimestamp).TotalMilliseconds
            : -1d;
        if (hadPendingPointer)
        {
            _totalClickToSelectionMs += clickToSelectionMs;
            _maxClickToSelectionMs = Math.Max(_maxClickToSelectionMs, clickToSelectionMs);
            ClickToSelectionSamples.Add(clickToSelectionMs);
            if (ClickToSelectionSamples.Count > MaxLatencySamples)
            {
                ClickToSelectionSamples.RemoveAt(0);
            }

            PendingByListBox.Remove(listBox);
        }

        var matchedSelections = Math.Max(1, _selectionChangedCount - _unmatchedPointerDownCount);
        var avgClickToSelectionMs = _totalClickToSelectionMs / matchedSelections;
        var sourceType = hadPendingPointer ? pending.TargetTypeName : "<unmatched>";
        var sourcePath = hadPendingPointer ? pending.TargetAncestry : "<unmatched>";
        var pointerX = hadPendingPointer ? pending.PointerX : 0f;
        var pointerY = hadPendingPointer ? pending.PointerY : 0f;
        var selectedBefore = hadPendingPointer ? pending.SelectedIndexBefore : -1;
        var selectionMode = hadPendingPointer ? pending.SelectionMode : "<unmatched>";
        var resolveMs = hadPendingPointer ? pending.ResolveMs : 0d;
        var hitTests = hadPendingPointer ? pending.ClickHitTests : 0;
        var resolvePath = hadPendingPointer ? pending.ResolvePath : "<unmatched>";
        var hitMetrics = hadPendingPointer ? pending.HitTestMetrics : null;
        var hitMetricsSummary = hitMetrics.HasValue
            ? $"nodes={hitMetrics.Value.NodesVisited} depth={hitMetrics.Value.MaxDepth} hitMs={hitMetrics.Value.TotalMilliseconds:0.###} " +
              $"top=[{hitMetrics.Value.TopLevelSubtreeSummary}] hot=[{hitMetrics.Value.HottestTypeSummary}]"
            : "nodes=<n/a>";

        var summary =
            $"[ListBoxSelectCpu] mode={selectionMode} selected(before={selectedBefore},after={selectedIndex}) added={addedCount} removed={removedCount} " +
            $"target={sourceType} path={sourcePath} pointer=({pointerX:0.#},{pointerY:0.#}) " +
            $"resolve(path={resolvePath},hitTests={hitTests},ms={resolveMs:0.000}) " +
            $"hitTest({hitMetricsSummary}) " +
            $"clickToSelection={(clickToSelectionMs >= 0d ? $"{clickToSelectionMs:0.000}ms" : "<unmatched>")} " +
            $"totals(pointerDowns={_pointerDownCount} selections={_selectionChangedCount} unmatched={_unmatchedPointerDownCount} " +
            $"avgClickToSelection={avgClickToSelectionMs:0.000}ms max={_maxClickToSelectionMs:0.000}ms)";
        Debug.WriteLine(summary);
        Console.WriteLine(summary);

        if (_selectionChangedCount % FlushEverySelections == 0)
        {
            FlushRollingSummary();
        }
    }

    private readonly record struct PendingSelectionContext(
        long PointerDownTimestamp,
        string TargetTypeName,
        string TargetAncestry,
        float PointerX,
        float PointerY,
        int SelectedIndexBefore,
        string SelectionMode,
        double ResolveMs,
        int ClickHitTests,
        string ResolvePath,
        HitTestMetrics? HitTestMetrics);

    private static bool TryFindAncestorListBox(UIElement start, out ListBox? listBox)
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is ListBox matched)
            {
                listBox = matched;
                return true;
            }
        }

        listBox = null;
        return false;
    }

    private static string BuildAncestry(UIElement source)
    {
        var parts = new List<string>(8);
        for (var current = source; current != null && parts.Count < 8; current = current.VisualParent ?? current.LogicalParent)
        {
            parts.Add(current.GetType().Name);
            if (current is ListBox)
            {
                break;
            }
        }

        return string.Join("->", parts);
    }

    private static void FlushRollingSummary()
    {
        if (ClickToSelectionSamples.Count == 0)
        {
            return;
        }

        var ordered = new List<double>(ClickToSelectionSamples);
        ordered.Sort();
        var p50 = Percentile(ordered, 0.50);
        var p95 = Percentile(ordered, 0.95);
        var summary =
            $"[ListBoxSelectCpu.Rolling] samples={ordered.Count} p50={p50:0.000}ms p95={p95:0.000}ms " +
            $"avg={(_totalClickToSelectionMs / Math.Max(1, _selectionChangedCount - _unmatchedPointerDownCount)):0.000}ms";
        Debug.WriteLine(summary);
        Console.WriteLine(summary);
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
