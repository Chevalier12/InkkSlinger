using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class StackPanel : Panel
{
    private static int _diagMeasureCallCount;
    private static long _diagMeasureElapsedTicks;
    private static int _diagMeasureChildCount;
    private static int _diagArrangeCallCount;
    private static long _diagArrangeElapsedTicks;
    private static int _diagArrangeChildCount;

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
        var desired = Vector2.Zero;
        var orientation = Orientation;
        var childAvailable = orientation == Orientation.Vertical
            ? new Vector2(availableSize.X, float.PositiveInfinity)
            : new Vector2(float.PositiveInfinity, availableSize.Y);
        var measuredChildren = 0;

        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            frameworkChild.Measure(childAvailable);
            measuredChildren++;
            var childDesired = frameworkChild.DesiredSize;
            if (orientation == Orientation.Vertical)
            {
                desired.X = System.MathF.Max(desired.X, childDesired.X);
                desired.Y += childDesired.Y;
                continue;
            }

            desired.X += childDesired.X;
            desired.Y = System.MathF.Max(desired.Y, childDesired.Y);
        }

        _diagMeasureCallCount++;
        _diagMeasureElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _diagMeasureChildCount += measuredChildren;

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var currentX = LayoutSlot.X;
        var currentY = LayoutSlot.Y;
        var orientation = Orientation;
        var arrangedChildren = 0;

        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            arrangedChildren++;

            if (orientation == Orientation.Vertical)
            {
                var height = frameworkChild.DesiredSize.Y;
                frameworkChild.Arrange(new LayoutRect(LayoutSlot.X, currentY, finalSize.X, height));
                currentY += height;
                continue;
            }

            var width = frameworkChild.DesiredSize.X;
            frameworkChild.Arrange(new LayoutRect(currentX, LayoutSlot.Y, width, finalSize.Y));
            currentX += width;
        }

        _diagArrangeCallCount++;
        _diagArrangeElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _diagArrangeChildCount += arrangedChildren;

        return finalSize;
    }

    internal static StackPanelTelemetrySnapshot GetTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot();
    }

    internal new static StackPanelTelemetrySnapshot GetTelemetryAndReset()
    {
        var snapshot = CreateTelemetrySnapshot();
        _diagMeasureCallCount = 0;
        _diagMeasureElapsedTicks = 0L;
        _diagMeasureChildCount = 0;
        _diagArrangeCallCount = 0;
        _diagArrangeElapsedTicks = 0L;
        _diagArrangeChildCount = 0;
        return snapshot;
    }

    private static StackPanelTelemetrySnapshot CreateTelemetrySnapshot()
    {
        return new StackPanelTelemetrySnapshot(
            _diagMeasureCallCount,
            (double)_diagMeasureElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagMeasureChildCount,
            _diagArrangeCallCount,
            (double)_diagArrangeElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagArrangeChildCount);
    }
}


