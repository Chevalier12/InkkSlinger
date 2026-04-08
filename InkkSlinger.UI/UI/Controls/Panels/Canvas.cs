using System;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class Canvas : Panel
{
    private static int _diagMeasureCallCount;
    private static long _diagMeasureElapsedTicks;
    private static int _diagMeasureChildCount;
    private static int _diagArrangeCallCount;
    private static long _diagArrangeElapsedTicks;
    private static int _diagArrangeChildCount;

    public static readonly DependencyProperty LeftProperty =
        DependencyProperty.RegisterAttached(
            "Left",
            typeof(float),
            typeof(Canvas),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty TopProperty =
        DependencyProperty.RegisterAttached(
            "Top",
            typeof(float),
            typeof(Canvas),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty RightProperty =
        DependencyProperty.RegisterAttached(
            "Right",
            typeof(float),
            typeof(Canvas),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty BottomProperty =
        DependencyProperty.RegisterAttached(
            "Bottom",
            typeof(float),
            typeof(Canvas),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static float GetLeft(UIElement element)
    {
        return element.GetValue<float>(LeftProperty);
    }

    public static void SetLeft(UIElement element, float value)
    {
        element.SetValue(LeftProperty, value);
    }

    public static float GetTop(UIElement element)
    {
        return element.GetValue<float>(TopProperty);
    }

    public static void SetTop(UIElement element, float value)
    {
        element.SetValue(TopProperty, value);
    }

    public static float GetRight(UIElement element)
    {
        return element.GetValue<float>(RightProperty);
    }

    public static void SetRight(UIElement element, float value)
    {
        element.SetValue(RightProperty, value);
    }

    public static float GetBottom(UIElement element)
    {
        return element.GetValue<float>(BottomProperty);
    }

    public static void SetBottom(UIElement element, float value)
    {
        element.SetValue(BottomProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var desired = Vector2.Zero;
        var measuredChildren = 0;

        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            frameworkChild.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
            measuredChildren++;

            var left = GetLeft(frameworkChild);
            var top = GetTop(frameworkChild);
            var right = GetRight(frameworkChild);
            var bottom = GetBottom(frameworkChild);

            left = ResolveMeasureOffset(left, right);
            top = ResolveMeasureOffset(top, bottom);

            desired.X = MathF.Max(desired.X, left + frameworkChild.DesiredSize.X);
            desired.Y = MathF.Max(desired.Y, top + frameworkChild.DesiredSize.Y);
        }

        _diagMeasureCallCount++;
        _diagMeasureElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _diagMeasureChildCount += measuredChildren;

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var arrangedChildren = 0;
        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            arrangedChildren++;

            var left = GetLeft(frameworkChild);
            var top = GetTop(frameworkChild);
            var right = GetRight(frameworkChild);
            var bottom = GetBottom(frameworkChild);

            left = ResolveArrangeOffset(left, right, finalSize.X, frameworkChild.DesiredSize.X);
            top = ResolveArrangeOffset(top, bottom, finalSize.Y, frameworkChild.DesiredSize.Y);

            frameworkChild.Arrange(new LayoutRect(
                LayoutSlot.X + left,
                LayoutSlot.Y + top,
                frameworkChild.DesiredSize.X,
                frameworkChild.DesiredSize.Y));
        }

        _diagArrangeCallCount++;
        _diagArrangeElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _diagArrangeChildCount += arrangedChildren;

        return finalSize;
    }

    internal new static CanvasTelemetrySnapshot GetTelemetryAndReset()
    {
        var snapshot = new CanvasTelemetrySnapshot(
            _diagMeasureCallCount,
            (double)_diagMeasureElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagMeasureChildCount,
            _diagArrangeCallCount,
            (double)_diagArrangeElapsedTicks * 1000d / Stopwatch.Frequency,
            _diagArrangeChildCount);
        _diagMeasureCallCount = 0;
        _diagMeasureElapsedTicks = 0L;
        _diagMeasureChildCount = 0;
        _diagArrangeCallCount = 0;
        _diagArrangeElapsedTicks = 0L;
        _diagArrangeChildCount = 0;
        return snapshot;
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }

    private static float ResolveMeasureOffset(float primary, float alternate)
    {
        if (!float.IsNaN(primary))
        {
            return primary;
        }

        if (!float.IsNaN(alternate))
        {
            return alternate;
        }

        return 0f;
    }

    private static float ResolveArrangeOffset(float primary, float alternate, float finalSize, float desiredSize)
    {
        if (!float.IsNaN(primary))
        {
            return primary;
        }

        if (!float.IsNaN(alternate))
        {
            return MathF.Max(0f, finalSize - desiredSize - alternate);
        }

        return 0f;
    }
}


