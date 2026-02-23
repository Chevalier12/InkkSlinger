using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;

namespace InkkSlinger;

internal readonly record struct SourceCollectionHandlerTiming(string HandlerKey, double ElapsedMs);

internal static class SourceCollectionDispatchDiagnostics
{
    private static readonly bool IsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_SOURCE_COLLECTION_HANDLER_LOGS"), "1", StringComparison.Ordinal);

    internal static bool Enabled => IsEnabled;

    internal static void Observe(
        string sourceName,
        NotifyCollectionChangedAction action,
        int itemCount,
        double totalMs,
        IReadOnlyList<SourceCollectionHandlerTiming> handlerTimings)
    {
        if (!IsEnabled)
        {
            return;
        }

        var sorted = new List<SourceCollectionHandlerTiming>(handlerTimings);
        sorted.Sort(static (a, b) => b.ElapsedMs.CompareTo(a.ElapsedMs));

        var topCount = Math.Min(8, sorted.Count);
        var parts = new string[topCount];
        for (var i = 0; i < topCount; i++)
        {
            var timing = sorted[i];
            parts[i] = $"{timing.HandlerKey}:{timing.ElapsedMs:0.###}ms";
        }

        var line =
            $"[SourceCollectionDispatch] source={sourceName} action={action} handlers={handlerTimings.Count} " +
            $"total={totalMs:0.###}ms count={itemCount} top={string.Join(" | ", parts)}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    internal static void ObserveCollectionViewRefreshBreakdown(
        string source,
        NotifyCollectionChangedAction action,
        double totalMs,
        double materializeMs,
        double filterMs,
        double sortMs,
        double groupMs,
        double currentRepairMs,
        double notifyMs,
        double notifyCollectionChangedMs,
        double notifyGroupsMs,
        double notifyCurrentPositionMs,
        double notifyCurrentItemMs,
        double notifyBeforeFirstMs,
        double notifyAfterLastMs,
        double notifyCurrentChangedMs,
        string topCollectionChangedHandlers,
        string topGroupsHandlers,
        string topCurrentPositionHandlers,
        string topCurrentItemHandlers,
        string topCurrentChangedHandlers,
        int itemCount,
        int groupCount)
    {
        if (!IsEnabled)
        {
            return;
        }

        var line =
            $"[SourceCollectionDispatch.CvRefresh] source={source} action={action} total={totalMs:0.###}ms " +
            $"phases(materialize={materializeMs:0.###}ms filter={filterMs:0.###}ms sort={sortMs:0.###}ms group={groupMs:0.###}ms currentRepair={currentRepairMs:0.###}ms notify={notifyMs:0.###}ms) " +
            $"notifySplit(cc={notifyCollectionChangedMs:0.###}ms groups={notifyGroupsMs:0.###}ms curPos={notifyCurrentPositionMs:0.###}ms curItem={notifyCurrentItemMs:0.###}ms before={notifyBeforeFirstMs:0.###}ms after={notifyAfterLastMs:0.###}ms currentChanged={notifyCurrentChangedMs:0.###}ms) " +
            $"top(cc={topCollectionChangedHandlers} groups={topGroupsHandlers} curPos={topCurrentPositionHandlers} curItem={topCurrentItemHandlers} currentChanged={topCurrentChangedHandlers}) " +
            $"shape(items={itemCount} groups={groupCount})";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    internal static void ObserveItemsSourceViewChangedBreakdown(
        string controlType,
        NotifyCollectionChangedAction action,
        string path,
        double totalMs,
        double applyMs,
        double finalizeMs,
        double reconcileMs,
        double regenerateMs,
        int realizedContainers,
        bool groupedProjectionActive)
    {
        if (!IsEnabled)
        {
            return;
        }

        var line =
            $"[SourceCollectionDispatch.ItemsControl] control={controlType} action={action} path={path} total={totalMs:0.###}ms " +
            $"steps(apply={applyMs:0.###}ms finalize={finalizeMs:0.###}ms reconcile={reconcileMs:0.###}ms regenerate={regenerateMs:0.###}ms) " +
            $"state(realized={realizedContainers} grouped={groupedProjectionActive})";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }
}
