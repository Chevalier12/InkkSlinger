using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class StackPanel : Panel, IScrollTransformContent
{
    private static long _diagMeasureCallCount;
    private static long _diagMeasureElapsedTicks;
    private static long _diagMeasureChildCount;
    private static long _diagMeasureSkippedChildCount;
    private static long _diagMeasureVerticalCount;
    private static long _diagMeasureHorizontalCount;
    private static long _diagMeasureEmptyCount;
    private static long _diagMeasureInfiniteCrossAxisCount;
    private static long _diagMeasureNaNCrossAxisCount;
    private static long _diagMeasureNonPositiveCrossAxisCount;
    private static double _diagMeasurePrimaryDesiredTotal;
    private static double _diagMeasureCrossDesiredTotal;
    private static long _diagArrangeCallCount;
    private static long _diagArrangeElapsedTicks;
    private static long _diagArrangeChildCount;
    private static long _diagArrangeSkippedChildCount;
    private static long _diagArrangeVerticalCount;
    private static long _diagArrangeHorizontalCount;
    private static long _diagArrangeEmptyCount;
    private static long _diagArrangeInfinitePrimarySizeCount;
    private static long _diagArrangeNaNPrimarySizeCount;
    private static long _diagArrangeNonPositivePrimarySizeCount;
    private static double _diagArrangePrimarySpanTotal;
    private static double _diagArrangeCrossSpanTotal;
    private static long _diagCanReuseMeasureForAvailableSizeChangeCallCount;
    private static long _diagCanReuseMeasureForAvailableSizeChangeAcceptedCount;
    private static long _diagCanReuseMeasureForAvailableSizeChangeRejectedDerivedTypeCount;
    private static long _diagCanReuseMeasureForAvailableSizeChangeRejectedChildCount;

    private long _runtimeCanReuseMeasureForAvailableSizeChangeCallCount;
    private long _runtimeCanReuseMeasureForAvailableSizeChangeAcceptedCount;
    private long _runtimeCanReuseMeasureForAvailableSizeChangeRejectedDerivedTypeCount;
    private long _runtimeCanReuseMeasureForAvailableSizeChangeRejectedChildCount;
    private string _runtimeLastMeasureReuseDecision = "none";
    private string _runtimeLastMeasureReuseFailure = "none";
    private int _runtimeLastMeasureReuseRejectedChildIndex = -1;
    private string _runtimeLastMeasureReuseRejectedChildType = "none";
    private string _runtimeLastMeasureReuseRejectedChildName = "none";
    private Vector2 _runtimeLastMeasureReusePreviousChildAvailableSize = new(float.NaN, float.NaN);
    private Vector2 _runtimeLastMeasureReuseNextChildAvailableSize = new(float.NaN, float.NaN);

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(StackPanel),
            new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        IncrementAggregate(ref _diagMeasureCallCount);
        var desired = Vector2.Zero;
        var orientation = Orientation;
        var vertical = orientation == Orientation.Vertical;
        if (vertical)
        {
            IncrementAggregate(ref _diagMeasureVerticalCount);
        }
        else
        {
            IncrementAggregate(ref _diagMeasureHorizontalCount);
        }

        var crossAxisAvailable = vertical ? availableSize.X : availableSize.Y;
        if (float.IsInfinity(crossAxisAvailable))
        {
            IncrementAggregate(ref _diagMeasureInfiniteCrossAxisCount);
        }
        else if (float.IsNaN(crossAxisAvailable))
        {
            IncrementAggregate(ref _diagMeasureNaNCrossAxisCount);
        }
        else if (crossAxisAvailable <= 0f)
        {
            IncrementAggregate(ref _diagMeasureNonPositiveCrossAxisCount);
        }

        var childAvailable = orientation == Orientation.Vertical
            ? new Vector2(availableSize.X, float.PositiveInfinity)
            : new Vector2(float.PositiveInfinity, availableSize.Y);
        var measuredChildren = 0;
        var skippedChildren = 0;

        try
        {
            foreach (var child in Children)
            {
                if (child is not FrameworkElement frameworkChild)
                {
                    skippedChildren++;
                    continue;
                }

                frameworkChild.Measure(childAvailable);
                measuredChildren++;
                var childDesired = frameworkChild.DesiredSize;
                if (vertical)
                {
                    desired.X = System.MathF.Max(desired.X, childDesired.X);
                    desired.Y += childDesired.Y;
                    continue;
                }

                desired.X += childDesired.X;
                desired.Y = System.MathF.Max(desired.Y, childDesired.Y);
            }

            if (measuredChildren == 0)
            {
                IncrementAggregate(ref _diagMeasureEmptyCount);
            }

            return desired;
        }
        finally
        {
            AddAggregate(ref _diagMeasureElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            AddAggregate(ref _diagMeasureChildCount, measuredChildren);
            AddAggregate(ref _diagMeasureSkippedChildCount, skippedChildren);
            AddAggregate(ref _diagMeasurePrimaryDesiredTotal, vertical ? desired.Y : desired.X);
            AddAggregate(ref _diagMeasureCrossDesiredTotal, vertical ? desired.X : desired.Y);
        }
    }

    protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        _runtimeCanReuseMeasureForAvailableSizeChangeCallCount++;
        IncrementAggregate(ref _diagCanReuseMeasureForAvailableSizeChangeCallCount);
        _runtimeLastMeasureReuseDecision = "evaluating";
        _runtimeLastMeasureReuseFailure = "none";
        _runtimeLastMeasureReuseRejectedChildIndex = -1;
        _runtimeLastMeasureReuseRejectedChildType = "none";
        _runtimeLastMeasureReuseRejectedChildName = "none";

        if (GetType() != typeof(StackPanel))
        {
            _runtimeCanReuseMeasureForAvailableSizeChangeRejectedDerivedTypeCount++;
            IncrementAggregate(ref _diagCanReuseMeasureForAvailableSizeChangeRejectedDerivedTypeCount);
            _runtimeLastMeasureReuseDecision = "rejected";
            _runtimeLastMeasureReuseFailure = "derived-stackpanel-type";
            return false;
        }

        var previousChildAvailable = ResolveChildAvailableSize(previousAvailableSize);
        var nextChildAvailable = ResolveChildAvailableSize(nextAvailableSize);
        _runtimeLastMeasureReusePreviousChildAvailableSize = previousChildAvailable;
        _runtimeLastMeasureReuseNextChildAvailableSize = nextChildAvailable;
        for (var childIndex = 0; childIndex < Children.Count; childIndex++)
        {
            var child = Children[childIndex];
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            if (!frameworkChild.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousChildAvailable, nextChildAvailable))
            {
                _runtimeCanReuseMeasureForAvailableSizeChangeRejectedChildCount++;
                IncrementAggregate(ref _diagCanReuseMeasureForAvailableSizeChangeRejectedChildCount);
                _runtimeLastMeasureReuseDecision = "rejected";
                _runtimeLastMeasureReuseFailure = "child-reuse-rejected";
                _runtimeLastMeasureReuseRejectedChildIndex = childIndex;
                _runtimeLastMeasureReuseRejectedChildType = frameworkChild.GetType().Name;
                _runtimeLastMeasureReuseRejectedChildName = string.IsNullOrEmpty(frameworkChild.Name) ? "none" : frameworkChild.Name;
                return false;
            }
        }

        _runtimeCanReuseMeasureForAvailableSizeChangeAcceptedCount++;
        IncrementAggregate(ref _diagCanReuseMeasureForAvailableSizeChangeAcceptedCount);
        _runtimeLastMeasureReuseDecision = "accepted";
        _runtimeLastMeasureReuseFailure = "none";
        return true;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        IncrementAggregate(ref _diagArrangeCallCount);
        var currentX = LayoutSlot.X;
        var currentY = LayoutSlot.Y;
        var orientation = Orientation;
        var vertical = orientation == Orientation.Vertical;
        if (vertical)
        {
            IncrementAggregate(ref _diagArrangeVerticalCount);
        }
        else
        {
            IncrementAggregate(ref _diagArrangeHorizontalCount);
        }

        var primarySize = vertical ? finalSize.Y : finalSize.X;
        if (float.IsInfinity(primarySize))
        {
            IncrementAggregate(ref _diagArrangeInfinitePrimarySizeCount);
        }
        else if (float.IsNaN(primarySize))
        {
            IncrementAggregate(ref _diagArrangeNaNPrimarySizeCount);
        }
        else if (primarySize <= 0f)
        {
            IncrementAggregate(ref _diagArrangeNonPositivePrimarySizeCount);
        }

        var arrangedChildren = 0;
        var skippedChildren = 0;
        var arrangedPrimarySpan = 0f;
        var arrangedCrossSpan = 0f;

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

                if (vertical)
                {
                    var height = frameworkChild.DesiredSize.Y;
                    frameworkChild.Arrange(new LayoutRect(LayoutSlot.X, currentY, finalSize.X, height));
                    currentY += height;
                    arrangedPrimarySpan += height;
                    arrangedCrossSpan = System.MathF.Max(arrangedCrossSpan, finalSize.X);
                    continue;
                }

                var width = frameworkChild.DesiredSize.X;
                frameworkChild.Arrange(new LayoutRect(currentX, LayoutSlot.Y, width, finalSize.Y));
                currentX += width;
                arrangedPrimarySpan += width;
                arrangedCrossSpan = System.MathF.Max(arrangedCrossSpan, finalSize.Y);
            }

            if (arrangedChildren == 0)
            {
                IncrementAggregate(ref _diagArrangeEmptyCount);
            }

            return finalSize;
        }
        finally
        {
            AddAggregate(ref _diagArrangeElapsedTicks, Stopwatch.GetTimestamp() - startTicks);
            AddAggregate(ref _diagArrangeChildCount, arrangedChildren);
            AddAggregate(ref _diagArrangeSkippedChildCount, skippedChildren);
            AddAggregate(ref _diagArrangePrimarySpanTotal, arrangedPrimarySpan);
            AddAggregate(ref _diagArrangeCrossSpanTotal, arrangedCrossSpan);
        }
    }

    private Vector2 ResolveChildAvailableSize(Vector2 availableSize)
    {
        return Orientation == Orientation.Vertical
            ? new Vector2(availableSize.X, float.PositiveInfinity)
            : new Vector2(float.PositiveInfinity, availableSize.Y);
    }

    internal StackPanelRuntimeDiagnosticsSnapshot GetStackPanelSnapshotForDiagnostics()
    {
        return new StackPanelRuntimeDiagnosticsSnapshot(
            Orientation,
            Children.Count,
            DesiredSize.X,
            DesiredSize.Y,
            RenderSize.X,
            RenderSize.Y,
            ActualWidth,
            ActualHeight,
            PreviousAvailableSizeForTests.X,
            PreviousAvailableSizeForTests.Y,
            MeasureCallCount,
            MeasureWorkCount,
            ArrangeCallCount,
            ArrangeWorkCount,
            TicksToMilliseconds(MeasureElapsedTicksForTests),
            TicksToMilliseconds(MeasureExclusiveElapsedTicksForTests),
            TicksToMilliseconds(ArrangeElapsedTicksForTests),
            IsMeasureValidForTests,
            IsArrangeValidForTests,
            _runtimeCanReuseMeasureForAvailableSizeChangeCallCount,
            _runtimeCanReuseMeasureForAvailableSizeChangeAcceptedCount,
            _runtimeCanReuseMeasureForAvailableSizeChangeRejectedDerivedTypeCount,
            _runtimeCanReuseMeasureForAvailableSizeChangeRejectedChildCount,
            _runtimeLastMeasureReuseDecision,
            _runtimeLastMeasureReuseFailure,
            _runtimeLastMeasureReuseRejectedChildIndex,
            _runtimeLastMeasureReuseRejectedChildType,
            _runtimeLastMeasureReuseRejectedChildName,
            _runtimeLastMeasureReusePreviousChildAvailableSize.X,
            _runtimeLastMeasureReusePreviousChildAvailableSize.Y,
            _runtimeLastMeasureReuseNextChildAvailableSize.X,
            _runtimeLastMeasureReuseNextChildAvailableSize.Y);
    }

    internal new static StackPanelTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot();
    }

    internal static StackPanelTelemetrySnapshot GetTelemetrySnapshotForDiagnostics()
    {
        return GetAggregateTelemetrySnapshotForDiagnostics();
    }

    internal new static StackPanelTelemetrySnapshot GetTelemetryAndReset()
    {
        var snapshot = CreateTelemetrySnapshot();
        ResetAggregate(ref _diagMeasureCallCount);
        ResetAggregate(ref _diagMeasureElapsedTicks);
        ResetAggregate(ref _diagMeasureChildCount);
        ResetAggregate(ref _diagMeasureSkippedChildCount);
        ResetAggregate(ref _diagMeasureVerticalCount);
        ResetAggregate(ref _diagMeasureHorizontalCount);
        ResetAggregate(ref _diagMeasureEmptyCount);
        ResetAggregate(ref _diagMeasureInfiniteCrossAxisCount);
        ResetAggregate(ref _diagMeasureNaNCrossAxisCount);
        ResetAggregate(ref _diagMeasureNonPositiveCrossAxisCount);
        ResetAggregate(ref _diagMeasurePrimaryDesiredTotal);
        ResetAggregate(ref _diagMeasureCrossDesiredTotal);
        ResetAggregate(ref _diagArrangeCallCount);
        ResetAggregate(ref _diagArrangeElapsedTicks);
        ResetAggregate(ref _diagArrangeChildCount);
        ResetAggregate(ref _diagArrangeSkippedChildCount);
        ResetAggregate(ref _diagArrangeVerticalCount);
        ResetAggregate(ref _diagArrangeHorizontalCount);
        ResetAggregate(ref _diagArrangeEmptyCount);
        ResetAggregate(ref _diagArrangeInfinitePrimarySizeCount);
        ResetAggregate(ref _diagArrangeNaNPrimarySizeCount);
        ResetAggregate(ref _diagArrangeNonPositivePrimarySizeCount);
        ResetAggregate(ref _diagArrangePrimarySpanTotal);
        ResetAggregate(ref _diagArrangeCrossSpanTotal);
        ResetAggregate(ref _diagCanReuseMeasureForAvailableSizeChangeCallCount);
        ResetAggregate(ref _diagCanReuseMeasureForAvailableSizeChangeAcceptedCount);
        ResetAggregate(ref _diagCanReuseMeasureForAvailableSizeChangeRejectedDerivedTypeCount);
        ResetAggregate(ref _diagCanReuseMeasureForAvailableSizeChangeRejectedChildCount);
        return snapshot;
    }

    private static StackPanelTelemetrySnapshot CreateTelemetrySnapshot()
    {
        return new StackPanelTelemetrySnapshot(
            ReadAggregate(ref _diagMeasureCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagMeasureElapsedTicks)),
            ReadAggregate(ref _diagMeasureChildCount),
            ReadAggregate(ref _diagMeasureSkippedChildCount),
            ReadAggregate(ref _diagMeasureVerticalCount),
            ReadAggregate(ref _diagMeasureHorizontalCount),
            ReadAggregate(ref _diagMeasureEmptyCount),
            ReadAggregate(ref _diagMeasureInfiniteCrossAxisCount),
            ReadAggregate(ref _diagMeasureNaNCrossAxisCount),
            ReadAggregate(ref _diagMeasureNonPositiveCrossAxisCount),
            ReadAggregate(ref _diagMeasurePrimaryDesiredTotal),
            ReadAggregate(ref _diagMeasureCrossDesiredTotal),
            ReadAggregate(ref _diagArrangeCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagArrangeElapsedTicks)),
            ReadAggregate(ref _diagArrangeChildCount),
            ReadAggregate(ref _diagArrangeSkippedChildCount),
            ReadAggregate(ref _diagArrangeVerticalCount),
            ReadAggregate(ref _diagArrangeHorizontalCount),
            ReadAggregate(ref _diagArrangeEmptyCount),
            ReadAggregate(ref _diagArrangeInfinitePrimarySizeCount),
            ReadAggregate(ref _diagArrangeNaNPrimarySizeCount),
            ReadAggregate(ref _diagArrangeNonPositivePrimarySizeCount),
            ReadAggregate(ref _diagArrangePrimarySpanTotal),
            ReadAggregate(ref _diagArrangeCrossSpanTotal),
            ReadAggregate(ref _diagCanReuseMeasureForAvailableSizeChangeCallCount),
            ReadAggregate(ref _diagCanReuseMeasureForAvailableSizeChangeAcceptedCount),
            ReadAggregate(ref _diagCanReuseMeasureForAvailableSizeChangeRejectedDerivedTypeCount),
            ReadAggregate(ref _diagCanReuseMeasureForAvailableSizeChangeRejectedChildCount));
    }

    private static void IncrementAggregate(ref long counter)
    {
        counter++;
    }

    private static void AddAggregate(ref long counter, long value)
    {
        counter += value;
    }

    private static void AddAggregate(ref double counter, double value)
    {
        counter += value;
    }

    private static long ReadAggregate(ref long counter)
    {
        return counter;
    }

    private static double ReadAggregate(ref double counter)
    {
        return counter;
    }

    private static void ResetAggregate(ref long counter)
    {
        counter = 0;
    }

    private static void ResetAggregate(ref double counter)
    {
        counter = 0d;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }
}


