using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace InkkSlinger;

internal static class CollectionViewCpuDiagnostics
{
    private static readonly bool IsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_COLLECTIONVIEW_CPU_LOGS"), "1", StringComparison.Ordinal);

    private const int FlushEvery = 60;
    private const int MaxSamples = 512;
    private const double SlowThresholdMs = 4d;
    private const double WarnThrottleMs = 1200d;

    private static readonly List<double> TotalSamples = [];
    private static readonly List<double> MaterializeSamples = [];
    private static readonly List<double> FilterSamples = [];
    private static readonly List<double> SortSamples = [];
    private static readonly List<double> GroupSamples = [];
    private static readonly List<double> CurrentRepairSamples = [];
    private static readonly List<double> NotifySamples = [];
    private static readonly List<double> NotifyCollectionChangedSamples = [];
    private static readonly List<double> NotifyGroupsSamples = [];
    private static readonly List<double> NotifyCurrentPositionSamples = [];
    private static readonly List<double> NotifyCurrentItemSamples = [];
    private static readonly List<double> NotifyBeforeFirstSamples = [];
    private static readonly List<double> NotifyAfterLastSamples = [];
    private static readonly List<double> NotifyCurrentChangedSamples = [];
    private static readonly List<double> NotifyPropertySubscriberCountSamples = [];
    private static readonly List<double> NotifyCollectionSubscriberCountSamples = [];
    private static readonly List<double> NotifyCurrentChangedSubscriberCountSamples = [];
    private static readonly List<double> ItemCountSamples = [];
    private static readonly List<double> GroupCountSamples = [];

    private static readonly Dictionary<string, HandlerAggregate> CollectionChangedAggregates = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, HandlerAggregate> GroupsPropertyAggregates = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, HandlerAggregate> CurrentPositionPropertyAggregates = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, HandlerAggregate> CurrentItemPropertyAggregates = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, HandlerAggregate> BeforeFirstPropertyAggregates = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, HandlerAggregate> AfterLastPropertyAggregates = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, HandlerAggregate> CurrentChangedAggregates = new(StringComparer.Ordinal);

    private static int _operationCount;
    private static int _slowCount;
    private static long _lastWarnTimestamp;

    internal static bool Enabled => IsEnabled;

    internal static void ObserveRefresh(
        double totalMs,
        double materializeMs,
        double filterMs,
        double sortMs,
        double groupMs,
        double currentRepairMs,
        CollectionViewNotificationDiagnostics notification,
        int itemCount,
        int groupCount,
        string source = "Unknown")
    {
        if (!IsEnabled)
        {
            return;
        }

        _operationCount++;
        AddSample(TotalSamples, totalMs);
        AddSample(MaterializeSamples, materializeMs);
        AddSample(FilterSamples, filterMs);
        AddSample(SortSamples, sortMs);
        AddSample(GroupSamples, groupMs);
        AddSample(CurrentRepairSamples, currentRepairMs);
        AddSample(NotifySamples, notification.TotalMs);
        AddSample(NotifyCollectionChangedSamples, notification.CollectionChangedMs);
        AddSample(NotifyGroupsSamples, notification.PropertyGroupsMs);
        AddSample(NotifyCurrentPositionSamples, notification.PropertyCurrentPositionMs);
        AddSample(NotifyCurrentItemSamples, notification.PropertyCurrentItemMs);
        AddSample(NotifyBeforeFirstSamples, notification.PropertyBeforeFirstMs);
        AddSample(NotifyAfterLastSamples, notification.PropertyAfterLastMs);
        AddSample(NotifyCurrentChangedSamples, notification.CurrentChangedMs);
        AddSample(NotifyPropertySubscriberCountSamples, notification.PropertyChangedSubscriberCount);
        AddSample(NotifyCollectionSubscriberCountSamples, notification.CollectionChangedSubscriberCount);
        AddSample(NotifyCurrentChangedSubscriberCountSamples, notification.CurrentChangedSubscriberCount);
        AddSample(ItemCountSamples, itemCount);
        AddSample(GroupCountSamples, groupCount);

        ObserveHandlerTimings(CollectionChangedAggregates, notification.CollectionChangedHandlerTimings);
        ObserveHandlerTimings(GroupsPropertyAggregates, notification.PropertyGroupsHandlerTimings);
        ObserveHandlerTimings(CurrentPositionPropertyAggregates, notification.PropertyCurrentPositionHandlerTimings);
        ObserveHandlerTimings(CurrentItemPropertyAggregates, notification.PropertyCurrentItemHandlerTimings);
        ObserveHandlerTimings(BeforeFirstPropertyAggregates, notification.PropertyBeforeFirstHandlerTimings);
        ObserveHandlerTimings(AfterLastPropertyAggregates, notification.PropertyAfterLastHandlerTimings);
        ObserveHandlerTimings(CurrentChangedAggregates, notification.CurrentChangedHandlerTimings);

        if (totalMs >= SlowThresholdMs)
        {
            _slowCount++;
            EmitSlowEvent(
                source,
                totalMs,
                materializeMs,
                filterMs,
                sortMs,
                groupMs,
                currentRepairMs,
                notification,
                itemCount,
                groupCount);
        }

        if (_operationCount % FlushEvery == 0)
        {
            FlushSummary();
        }
    }

    internal static void FlushSummaryNow()
    {
        if (!IsEnabled)
        {
            return;
        }

        FlushSummary();
    }

    private static void FlushSummary()
    {
        if (TotalSamples.Count == 0)
        {
            return;
        }

        var total = Sorted(TotalSamples);
        var materialize = Sorted(MaterializeSamples);
        var filter = Sorted(FilterSamples);
        var sort = Sorted(SortSamples);
        var group = Sorted(GroupSamples);
        var currentRepair = Sorted(CurrentRepairSamples);
        var notify = Sorted(NotifySamples);
        var notifyCollectionChanged = Sorted(NotifyCollectionChangedSamples);
        var notifyGroups = Sorted(NotifyGroupsSamples);
        var notifyCurrentPosition = Sorted(NotifyCurrentPositionSamples);
        var notifyCurrentItem = Sorted(NotifyCurrentItemSamples);
        var notifyBeforeFirst = Sorted(NotifyBeforeFirstSamples);
        var notifyAfterLast = Sorted(NotifyAfterLastSamples);
        var notifyCurrentChanged = Sorted(NotifyCurrentChangedSamples);
        var notifyPropertySubscribers = Sorted(NotifyPropertySubscriberCountSamples);
        var notifyCollectionSubscribers = Sorted(NotifyCollectionSubscriberCountSamples);
        var notifyCurrentSubscribers = Sorted(NotifyCurrentChangedSubscriberCountSamples);
        var itemCounts = Sorted(ItemCountSamples);
        var groupCounts = Sorted(GroupCountSamples);

        var line =
            $"[CollectionViewCpu] count={_operationCount} slowRate={((double)_slowCount / Math.Max(1, _operationCount)):P1} " +
            $"total(p50={Percentile(total, 0.50d):0.###}ms p95={Percentile(total, 0.95d):0.###}ms max={total[^1]:0.###}ms) " +
            $"phases(p95 materialize={Percentile(materialize, 0.95d):0.###}ms filter={Percentile(filter, 0.95d):0.###}ms sort={Percentile(sort, 0.95d):0.###}ms group={Percentile(group, 0.95d):0.###}ms currentRepair={Percentile(currentRepair, 0.95d):0.###}ms notify={Percentile(notify, 0.95d):0.###}ms) " +
            $"notifySplit(p95 cc={Percentile(notifyCollectionChanged, 0.95d):0.###}ms groups={Percentile(notifyGroups, 0.95d):0.###}ms curPos={Percentile(notifyCurrentPosition, 0.95d):0.###}ms curItem={Percentile(notifyCurrentItem, 0.95d):0.###}ms before={Percentile(notifyBeforeFirst, 0.95d):0.###}ms after={Percentile(notifyAfterLast, 0.95d):0.###}ms currentChanged={Percentile(notifyCurrentChanged, 0.95d):0.###}ms) " +
            $"subs(p95 cc={Percentile(notifyCollectionSubscribers, 0.95d):0} prop={Percentile(notifyPropertySubscribers, 0.95d):0} curChanged={Percentile(notifyCurrentSubscribers, 0.95d):0}) " +
            $"shape(p95 items={Percentile(itemCounts, 0.95d):0} groups={Percentile(groupCounts, 0.95d):0}) " +
            $"hotHandlers(cc={FormatAggregateTop(CollectionChangedAggregates)} groups={FormatAggregateTop(GroupsPropertyAggregates)} curPos={FormatAggregateTop(CurrentPositionPropertyAggregates)} curItem={FormatAggregateTop(CurrentItemPropertyAggregates)} before={FormatAggregateTop(BeforeFirstPropertyAggregates)} after={FormatAggregateTop(AfterLastPropertyAggregates)} currentChanged={FormatAggregateTop(CurrentChangedAggregates)})";

        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    private static void EmitSlowEvent(
        string source,
        double totalMs,
        double materializeMs,
        double filterMs,
        double sortMs,
        double groupMs,
        double currentRepairMs,
        CollectionViewNotificationDiagnostics notification,
        int itemCount,
        int groupCount)
    {
        var now = Stopwatch.GetTimestamp();
        if (_lastWarnTimestamp != 0 &&
            Stopwatch.GetElapsedTime(_lastWarnTimestamp).TotalMilliseconds < WarnThrottleMs)
        {
            return;
        }

        _lastWarnTimestamp = now;
        var line =
            $"[CollectionViewCpu.Slow] source={source} total={totalMs:0.###}ms " +
            $"materialize={materializeMs:0.###}ms filter={filterMs:0.###}ms sort={sortMs:0.###}ms group={groupMs:0.###}ms currentRepair={currentRepairMs:0.###}ms " +
            $"notify(total={notification.TotalMs:0.###}ms cc={notification.CollectionChangedMs:0.###}ms groups={notification.PropertyGroupsMs:0.###}ms curPos={notification.PropertyCurrentPositionMs:0.###}ms curItem={notification.PropertyCurrentItemMs:0.###}ms before={notification.PropertyBeforeFirstMs:0.###}ms after={notification.PropertyAfterLastMs:0.###}ms currentChanged={notification.CurrentChangedMs:0.###}ms) " +
            $"subs(cc={notification.CollectionChangedSubscriberCount} prop={notification.PropertyChangedSubscriberCount} currentChanged={notification.CurrentChangedSubscriberCount}) " +
            $"top(cc={FormatTopHandlers(notification.CollectionChangedHandlerTimings)} groups={FormatTopHandlers(notification.PropertyGroupsHandlerTimings)} curPos={FormatTopHandlers(notification.PropertyCurrentPositionHandlerTimings)} curItem={FormatTopHandlers(notification.PropertyCurrentItemHandlerTimings)} before={FormatTopHandlers(notification.PropertyBeforeFirstHandlerTimings)} after={FormatTopHandlers(notification.PropertyAfterLastHandlerTimings)} currentChanged={FormatTopHandlers(notification.CurrentChangedHandlerTimings)}) " +
            $"items={itemCount} groups={groupCount}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    private static void ObserveHandlerTimings(Dictionary<string, HandlerAggregate> aggregates, IReadOnlyList<CollectionViewHandlerTiming> timings)
    {
        for (var i = 0; i < timings.Count; i++)
        {
            var timing = timings[i];
            if (!aggregates.TryGetValue(timing.HandlerKey, out var aggregate))
            {
                aggregate = new HandlerAggregate();
                aggregates[timing.HandlerKey] = aggregate;
            }

            aggregate.Count++;
            aggregate.TotalMs += timing.ElapsedMs;
            aggregate.MaxMs = Math.Max(aggregate.MaxMs, timing.ElapsedMs);
        }
    }

    private static string FormatTopHandlers(IReadOnlyList<CollectionViewHandlerTiming> timings)
    {
        if (timings.Count == 0)
        {
            return "none";
        }

        var copy = new List<CollectionViewHandlerTiming>(timings);
        copy.Sort(static (a, b) => b.ElapsedMs.CompareTo(a.ElapsedMs));
        var limit = Math.Min(3, copy.Count);
        var parts = new List<string>(limit);
        for (var i = 0; i < limit; i++)
        {
            parts.Add($"{copy[i].HandlerKey}:{copy[i].ElapsedMs:0.###}ms");
        }

        return string.Join(" | ", parts);
    }

    private static string FormatAggregateTop(Dictionary<string, HandlerAggregate> aggregates)
    {
        if (aggregates.Count == 0)
        {
            return "none";
        }

        var pairs = new List<KeyValuePair<string, HandlerAggregate>>(aggregates);
        pairs.Sort(static (a, b) => b.Value.TotalMs.CompareTo(a.Value.TotalMs));
        var limit = Math.Min(3, pairs.Count);
        var parts = new List<string>(limit);
        for (var i = 0; i < limit; i++)
        {
            var pair = pairs[i];
            var avg = pair.Value.Count > 0 ? pair.Value.TotalMs / pair.Value.Count : 0d;
            parts.Add($"{pair.Key}:sum={pair.Value.TotalMs:0.###}ms avg={avg:0.###}ms max={pair.Value.MaxMs:0.###}ms n={pair.Value.Count}");
        }

        return string.Join(" | ", parts);
    }

    private static void AddSample(List<double> bucket, double value)
    {
        bucket.Add(Math.Max(0d, value));
        if (bucket.Count > MaxSamples)
        {
            bucket.RemoveAt(0);
        }
    }

    private static List<double> Sorted(List<double> samples)
    {
        var sorted = new List<double>(samples);
        sorted.Sort();
        return sorted;
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 0)
        {
            return 0d;
        }

        var rank = percentile * (sorted.Count - 1);
        var low = (int)Math.Floor(rank);
        var high = (int)Math.Ceiling(rank);
        if (low == high)
        {
            return sorted[low];
        }

        var weight = rank - low;
        return (sorted[low] * (1d - weight)) + (sorted[high] * weight);
    }

    private sealed class HandlerAggregate
    {
        public int Count { get; set; }

        public double TotalMs { get; set; }

        public double MaxMs { get; set; }
    }
}

internal readonly record struct CollectionViewHandlerTiming(
    string HandlerKey,
    double ElapsedMs);

internal readonly record struct CollectionViewNotificationDiagnostics(
    double TotalMs,
    double CollectionChangedMs,
    double PropertyGroupsMs,
    double PropertyCurrentPositionMs,
    double PropertyCurrentItemMs,
    double PropertyBeforeFirstMs,
    double PropertyAfterLastMs,
    double CurrentChangedMs,
    int CollectionChangedSubscriberCount,
    int PropertyChangedSubscriberCount,
    int CurrentChangedSubscriberCount,
    IReadOnlyList<CollectionViewHandlerTiming> CollectionChangedHandlerTimings,
    IReadOnlyList<CollectionViewHandlerTiming> PropertyGroupsHandlerTimings,
    IReadOnlyList<CollectionViewHandlerTiming> PropertyCurrentPositionHandlerTimings,
    IReadOnlyList<CollectionViewHandlerTiming> PropertyCurrentItemHandlerTimings,
    IReadOnlyList<CollectionViewHandlerTiming> PropertyBeforeFirstHandlerTimings,
    IReadOnlyList<CollectionViewHandlerTiming> PropertyAfterLastHandlerTimings,
    IReadOnlyList<CollectionViewHandlerTiming> CurrentChangedHandlerTimings);
