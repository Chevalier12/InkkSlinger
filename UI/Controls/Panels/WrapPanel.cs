using System;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class WrapPanel : Panel
{
    private static long _diagMeasureCallCount;
    private static long _diagMeasureElapsedTicks;
    private static long _diagMeasuredChildCount;
    private static long _diagMeasureSkippedChildCount;
    private static long _diagMeasureHorizontalCount;
    private static long _diagMeasureVerticalCount;
    private static long _diagMeasureInfiniteLineLimitCount;
    private static long _diagMeasureNaNLineLimitCount;
    private static long _diagMeasureNonPositiveLineLimitCount;
    private static long _diagMeasureWrapCount;
    private static long _diagMeasureCommittedLineCount;
    private static long _diagMeasureEmptyCount;
    private static long _diagMeasureExplicitItemWidthCount;
    private static long _diagMeasureAvailableWidthCount;
    private static long _diagMeasureExplicitItemHeightCount;
    private static long _diagMeasureAvailableHeightCount;
    private static long _diagArrangeCallCount;
    private static long _diagArrangeElapsedTicks;
    private static long _diagArrangedChildCount;
    private static long _diagArrangeSkippedChildCount;
    private static long _diagArrangeHorizontalCount;
    private static long _diagArrangeVerticalCount;
    private static long _diagArrangeInfiniteLineLimitCount;
    private static long _diagArrangeNaNLineLimitCount;
    private static long _diagArrangeNonPositiveLineLimitCount;
    private static long _diagArrangeWrapCount;
    private static long _diagArrangeCommittedLineCount;
    private static long _diagArrangeEmptyCount;
    private static long _diagGetChildSizeCallCount;
    private static long _diagGetChildSizeElapsedTicks;
    private static long _diagGetChildSizeFromMeasureCount;
    private static long _diagGetChildSizeFromArrangeCount;
    private static long _diagGetChildSizeExplicitWidthCount;
    private static long _diagGetChildSizeDesiredWidthCount;
    private static long _diagGetChildSizeExplicitHeightCount;
    private static long _diagGetChildSizeDesiredHeightCount;

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(WrapPanel),
            new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(
            nameof(ItemWidth),
            typeof(float),
            typeof(WrapPanel),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(
            nameof(ItemHeight),
            typeof(float),
            typeof(WrapPanel),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public float ItemWidth
    {
        get => GetValue<float>(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public float ItemHeight
    {
        get => GetValue<float>(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        IncrementAggregate(ref _diagMeasureCallCount);

        var horizontal = Orientation == Orientation.Horizontal;
        if (horizontal)
        {
            IncrementAggregate(ref _diagMeasureHorizontalCount);
        }
        else
        {
            IncrementAggregate(ref _diagMeasureVerticalCount);
        }

        var itemWidth = ItemWidth;
        var itemHeight = ItemHeight;
        var lineLimit = horizontal ? availableSize.X : availableSize.Y;
        if (float.IsInfinity(lineLimit))
        {
            IncrementAggregate(ref _diagMeasureInfiniteLineLimitCount);
            lineLimit = float.PositiveInfinity;
        }
        else if (float.IsNaN(lineLimit))
        {
            IncrementAggregate(ref _diagMeasureNaNLineLimitCount);
            lineLimit = float.PositiveInfinity;
        }
        else if (lineLimit <= 0f)
        {
            IncrementAggregate(ref _diagMeasureNonPositiveLineLimitCount);
            lineLimit = float.PositiveInfinity;
        }

        var lineMain = 0f;
        var lineCross = 0f;
        var desiredMain = 0f;
        var desiredCross = 0f;
        var measuredChildren = 0;
        var skippedChildren = 0;
        var wrapCount = 0;
        var committedLineCount = 0;

        try
        {
            foreach (var child in Children)
            {
                if (child is not FrameworkElement frameworkChild)
                {
                    skippedChildren++;
                    continue;
                }

                measuredChildren++;

                if (float.IsNaN(itemWidth))
                {
                    IncrementAggregate(ref _diagMeasureAvailableWidthCount);
                }
                else
                {
                    IncrementAggregate(ref _diagMeasureExplicitItemWidthCount);
                }

                if (float.IsNaN(itemHeight))
                {
                    IncrementAggregate(ref _diagMeasureAvailableHeightCount);
                }
                else
                {
                    IncrementAggregate(ref _diagMeasureExplicitItemHeightCount);
                }

                var itemAvailable = new Vector2(
                    float.IsNaN(itemWidth) ? availableSize.X : itemWidth,
                    float.IsNaN(itemHeight) ? availableSize.Y : itemHeight);
                frameworkChild.Measure(itemAvailable);

                var childSize = GetChildSize(frameworkChild, itemWidth, itemHeight, fromMeasure: true);
                var childMain = horizontal ? childSize.X : childSize.Y;
                var childCross = horizontal ? childSize.Y : childSize.X;

                if (lineMain > 0f && lineMain + childMain > lineLimit)
                {
                    wrapCount++;
                    committedLineCount++;
                    desiredMain = MathF.Max(desiredMain, lineMain);
                    desiredCross += lineCross;
                    lineMain = 0f;
                    lineCross = 0f;
                }

                lineMain += childMain;
                lineCross = MathF.Max(lineCross, childCross);
            }

            desiredMain = MathF.Max(desiredMain, lineMain);
            desiredCross += lineCross;
            if (measuredChildren == 0)
            {
                IncrementAggregate(ref _diagMeasureEmptyCount);
            }
            else
            {
                committedLineCount++;
            }

            return horizontal
                ? new Vector2(desiredMain, desiredCross)
                : new Vector2(desiredCross, desiredMain);
        }
        finally
        {
            AddAggregate(ref _diagMeasureElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            AddAggregate(ref _diagMeasuredChildCount, measuredChildren);
            AddAggregate(ref _diagMeasureSkippedChildCount, skippedChildren);
            AddAggregate(ref _diagMeasureWrapCount, wrapCount);
            AddAggregate(ref _diagMeasureCommittedLineCount, committedLineCount);
        }
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        IncrementAggregate(ref _diagArrangeCallCount);

        var horizontal = Orientation == Orientation.Horizontal;
        if (horizontal)
        {
            IncrementAggregate(ref _diagArrangeHorizontalCount);
        }
        else
        {
            IncrementAggregate(ref _diagArrangeVerticalCount);
        }

        var itemWidth = ItemWidth;
        var itemHeight = ItemHeight;
        var lineLimit = horizontal ? finalSize.X : finalSize.Y;
        if (float.IsInfinity(lineLimit))
        {
            IncrementAggregate(ref _diagArrangeInfiniteLineLimitCount);
        }
        else if (float.IsNaN(lineLimit))
        {
            IncrementAggregate(ref _diagArrangeNaNLineLimitCount);
        }
        else if (lineLimit <= 0f)
        {
            IncrementAggregate(ref _diagArrangeNonPositiveLineLimitCount);
        }

        var lineMain = 0f;
        var lineCross = 0f;
        var crossOffset = 0f;
        var arrangedChildren = 0;
        var skippedChildren = 0;
        var wrapCount = 0;
        var committedLineCount = 0;

        try
        {
            foreach (var child in Children)
            {
                if (child is not FrameworkElement frameworkChild)
                {
                    skippedChildren++;
                    continue;
                }

                arrangedChildren++;

                var childSize = GetChildSize(frameworkChild, itemWidth, itemHeight, fromMeasure: false);
                var childMain = horizontal ? childSize.X : childSize.Y;
                var childCross = horizontal ? childSize.Y : childSize.X;

                if (lineMain > 0f && lineMain + childMain > lineLimit)
                {
                    wrapCount++;
                    committedLineCount++;
                    lineMain = 0f;
                    crossOffset += lineCross;
                    lineCross = 0f;
                }

                var x = LayoutSlot.X + (horizontal ? lineMain : crossOffset);
                var y = LayoutSlot.Y + (horizontal ? crossOffset : lineMain);
                var width = horizontal ? childMain : childCross;
                var height = horizontal ? childCross : childMain;

                frameworkChild.Arrange(new LayoutRect(x, y, width, height));
                lineMain += childMain;
                lineCross = MathF.Max(lineCross, childCross);
            }

            if (arrangedChildren == 0)
            {
                IncrementAggregate(ref _diagArrangeEmptyCount);
            }
            else
            {
                committedLineCount++;
            }

            return finalSize;
        }
        finally
        {
            AddAggregate(ref _diagArrangeElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            AddAggregate(ref _diagArrangedChildCount, arrangedChildren);
            AddAggregate(ref _diagArrangeSkippedChildCount, skippedChildren);
            AddAggregate(ref _diagArrangeWrapCount, wrapCount);
            AddAggregate(ref _diagArrangeCommittedLineCount, committedLineCount);
        }
    }

    internal static WrapPanelTelemetrySnapshot GetTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot();
    }

    internal new static WrapPanelTelemetrySnapshot GetTelemetryAndReset()
    {
        return new WrapPanelTelemetrySnapshot(
            ResetAggregate(ref _diagMeasureCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagMeasureElapsedTicks)),
            ResetAggregate(ref _diagMeasuredChildCount),
            ResetAggregate(ref _diagMeasureSkippedChildCount),
            ResetAggregate(ref _diagMeasureHorizontalCount),
            ResetAggregate(ref _diagMeasureVerticalCount),
            ResetAggregate(ref _diagMeasureInfiniteLineLimitCount),
            ResetAggregate(ref _diagMeasureNaNLineLimitCount),
            ResetAggregate(ref _diagMeasureNonPositiveLineLimitCount),
            ResetAggregate(ref _diagMeasureWrapCount),
            ResetAggregate(ref _diagMeasureCommittedLineCount),
            ResetAggregate(ref _diagMeasureEmptyCount),
            ResetAggregate(ref _diagMeasureExplicitItemWidthCount),
            ResetAggregate(ref _diagMeasureAvailableWidthCount),
            ResetAggregate(ref _diagMeasureExplicitItemHeightCount),
            ResetAggregate(ref _diagMeasureAvailableHeightCount),
            ResetAggregate(ref _diagArrangeCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagArrangeElapsedTicks)),
            ResetAggregate(ref _diagArrangedChildCount),
            ResetAggregate(ref _diagArrangeSkippedChildCount),
            ResetAggregate(ref _diagArrangeHorizontalCount),
            ResetAggregate(ref _diagArrangeVerticalCount),
            ResetAggregate(ref _diagArrangeInfiniteLineLimitCount),
            ResetAggregate(ref _diagArrangeNaNLineLimitCount),
            ResetAggregate(ref _diagArrangeNonPositiveLineLimitCount),
            ResetAggregate(ref _diagArrangeWrapCount),
            ResetAggregate(ref _diagArrangeCommittedLineCount),
            ResetAggregate(ref _diagArrangeEmptyCount),
            ResetAggregate(ref _diagGetChildSizeCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagGetChildSizeElapsedTicks)),
            ResetAggregate(ref _diagGetChildSizeFromMeasureCount),
            ResetAggregate(ref _diagGetChildSizeFromArrangeCount),
            ResetAggregate(ref _diagGetChildSizeExplicitWidthCount),
            ResetAggregate(ref _diagGetChildSizeDesiredWidthCount),
            ResetAggregate(ref _diagGetChildSizeExplicitHeightCount),
            ResetAggregate(ref _diagGetChildSizeDesiredHeightCount));
    }

    private static WrapPanelTelemetrySnapshot CreateTelemetrySnapshot()
    {
        return new WrapPanelTelemetrySnapshot(
            ReadAggregate(ref _diagMeasureCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagMeasureElapsedTicks)),
            ReadAggregate(ref _diagMeasuredChildCount),
            ReadAggregate(ref _diagMeasureSkippedChildCount),
            ReadAggregate(ref _diagMeasureHorizontalCount),
            ReadAggregate(ref _diagMeasureVerticalCount),
            ReadAggregate(ref _diagMeasureInfiniteLineLimitCount),
            ReadAggregate(ref _diagMeasureNaNLineLimitCount),
            ReadAggregate(ref _diagMeasureNonPositiveLineLimitCount),
            ReadAggregate(ref _diagMeasureWrapCount),
            ReadAggregate(ref _diagMeasureCommittedLineCount),
            ReadAggregate(ref _diagMeasureEmptyCount),
            ReadAggregate(ref _diagMeasureExplicitItemWidthCount),
            ReadAggregate(ref _diagMeasureAvailableWidthCount),
            ReadAggregate(ref _diagMeasureExplicitItemHeightCount),
            ReadAggregate(ref _diagMeasureAvailableHeightCount),
            ReadAggregate(ref _diagArrangeCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagArrangeElapsedTicks)),
            ReadAggregate(ref _diagArrangedChildCount),
            ReadAggregate(ref _diagArrangeSkippedChildCount),
            ReadAggregate(ref _diagArrangeHorizontalCount),
            ReadAggregate(ref _diagArrangeVerticalCount),
            ReadAggregate(ref _diagArrangeInfiniteLineLimitCount),
            ReadAggregate(ref _diagArrangeNaNLineLimitCount),
            ReadAggregate(ref _diagArrangeNonPositiveLineLimitCount),
            ReadAggregate(ref _diagArrangeWrapCount),
            ReadAggregate(ref _diagArrangeCommittedLineCount),
            ReadAggregate(ref _diagArrangeEmptyCount),
            ReadAggregate(ref _diagGetChildSizeCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagGetChildSizeElapsedTicks)),
            ReadAggregate(ref _diagGetChildSizeFromMeasureCount),
            ReadAggregate(ref _diagGetChildSizeFromArrangeCount),
            ReadAggregate(ref _diagGetChildSizeExplicitWidthCount),
            ReadAggregate(ref _diagGetChildSizeDesiredWidthCount),
            ReadAggregate(ref _diagGetChildSizeExplicitHeightCount),
            ReadAggregate(ref _diagGetChildSizeDesiredHeightCount));
    }

    private static void IncrementAggregate(ref long counter)
    {
        counter++;
    }

    private static void AddAggregate(ref long counter, long value)
    {
        counter += value;
    }

    private static long ReadAggregate(ref long counter)
    {
        return counter;
    }

    private static long ResetAggregate(ref long counter)
    {
        var value = counter;
        counter = 0;
        return value;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    private static Vector2 GetChildSize(FrameworkElement element, float itemWidth, float itemHeight, bool fromMeasure)
    {
        var startTicks = Stopwatch.GetTimestamp();
        IncrementAggregate(ref _diagGetChildSizeCallCount);
        if (fromMeasure)
        {
            IncrementAggregate(ref _diagGetChildSizeFromMeasureCount);
        }
        else
        {
            IncrementAggregate(ref _diagGetChildSizeFromArrangeCount);
        }

        try
        {
            float width;
            if (float.IsNaN(itemWidth))
            {
                IncrementAggregate(ref _diagGetChildSizeDesiredWidthCount);
                width = element.DesiredSize.X;
            }
            else
            {
                IncrementAggregate(ref _diagGetChildSizeExplicitWidthCount);
                width = itemWidth;
            }

            float height;
            if (float.IsNaN(itemHeight))
            {
                IncrementAggregate(ref _diagGetChildSizeDesiredHeightCount);
                height = element.DesiredSize.Y;
            }
            else
            {
                IncrementAggregate(ref _diagGetChildSizeExplicitHeightCount);
                height = itemHeight;
            }

            return new Vector2(width, height);
        }
        finally
        {
            AddAggregate(ref _diagGetChildSizeElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
        }
    }
}
