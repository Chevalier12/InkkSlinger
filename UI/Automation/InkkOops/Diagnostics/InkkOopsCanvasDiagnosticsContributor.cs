using System;
using System.Collections.Generic;
using System.Text;

namespace InkkSlinger;

public sealed class InkkOopsCanvasDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 40;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not Canvas canvas)
        {
            return;
        }

        builder.Add("type", canvas.GetType().Name);
        builder.Add("slot", FormatRect(canvas.LayoutSlot));
        builder.Add("desired", $"{canvas.DesiredSize.X:0.##},{canvas.DesiredSize.Y:0.##}");
        builder.Add("renderSize", $"{canvas.RenderSize.X:0.##},{canvas.RenderSize.Y:0.##}");
        builder.Add("actual", $"{canvas.ActualWidth:0.##},{canvas.ActualHeight:0.##}");
        builder.Add("previousAvailable", $"{canvas.PreviousAvailableSizeForTests.X:0.##},{canvas.PreviousAvailableSizeForTests.Y:0.##}");
        builder.Add("measureCalls", canvas.MeasureCallCount);
        builder.Add("measureWork", canvas.MeasureWorkCount);
        builder.Add("arrangeCalls", canvas.ArrangeCallCount);
        builder.Add("arrangeWork", canvas.ArrangeWorkCount);
        builder.Add("measureMs", FormatMilliseconds(canvas.MeasureElapsedTicksForTests));
        builder.Add("measureExclusiveMs", FormatMilliseconds(canvas.MeasureExclusiveElapsedTicksForTests));
        builder.Add("arrangeMs", FormatMilliseconds(canvas.ArrangeElapsedTicksForTests));
        builder.Add("measureValid", canvas.IsMeasureValidForTests);
        builder.Add("arrangeValid", canvas.IsArrangeValidForTests);
        builder.Add("visible", canvas.Visibility == Visibility.Visible);
        builder.Add("enabled", canvas.IsEnabled);
        builder.Add("focusable", canvas.Focusable);
        builder.Add("loaded", canvas.IsLoaded);
        builder.Add("opacity", $"{canvas.Opacity:0.###}");
        builder.Add("snapsToDevicePixels", canvas.SnapsToDevicePixels);
        builder.Add("useLayoutRounding", canvas.UseLayoutRounding);
        builder.Add("clipToBounds", canvas.ClipToBounds);
        builder.Add("margin", FormatThickness(canvas.Margin));
        builder.Add("width", FormatFloat(canvas.Width));
        builder.Add("height", FormatFloat(canvas.Height));
        builder.Add("min", $"{canvas.MinWidth:0.##},{canvas.MinHeight:0.##}");
        builder.Add("max", $"{canvas.MaxWidth:0.##},{canvas.MaxHeight:0.##}");
        builder.Add("children", canvas.Children.Count);
        builder.Add("childSummary", BuildChildSummary(canvas));
    }

    private static string BuildChildSummary(Canvas canvas)
    {
        if (canvas.Children.Count == 0)
        {
            return "none";
        }

        var parts = new List<string>(canvas.Children.Count);
        for (var i = 0; i < canvas.Children.Count; i++)
        {
            var child = canvas.Children[i];
            var childName = DescribeElement(child);
            var zIndex = Panel.GetZIndex(child);
            var left = FormatFloat(Canvas.GetLeft(child));
            var top = FormatFloat(Canvas.GetTop(child));
            var right = FormatFloat(Canvas.GetRight(child));
            var bottom = FormatFloat(Canvas.GetBottom(child));

            if (child is FrameworkElement frameworkChild)
            {
                parts.Add($"{i}:{childName}@slot={FormatRect(frameworkChild.LayoutSlot)} desired={frameworkChild.DesiredSize.X:0.##},{frameworkChild.DesiredSize.Y:0.##} actual={frameworkChild.ActualWidth:0.##},{frameworkChild.ActualHeight:0.##} z={zIndex} anchors={left},{top},{right},{bottom} mc={frameworkChild.MeasureCallCount}/{frameworkChild.MeasureWorkCount} ac={frameworkChild.ArrangeCallCount}/{frameworkChild.ArrangeWorkCount}");
            }
            else
            {
                parts.Add($"{i}:{childName} z={zIndex} anchors={left},{top},{right},{bottom}");
            }
        }

        return string.Join(" | ", parts);
    }

    private static string DescribeElement(UIElement element)
    {
        return element is FrameworkElement frameworkElement && !string.IsNullOrWhiteSpace(frameworkElement.Name)
            ? $"{element.GetType().Name}#{frameworkElement.Name}"
            : element.GetType().Name;
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}";
    }

    private static string FormatThickness(Thickness thickness)
    {
        return $"{thickness.Left:0.##},{thickness.Top:0.##},{thickness.Right:0.##},{thickness.Bottom:0.##}";
    }

    private static string FormatMilliseconds(long ticks)
    {
        return ((double)ticks * 1000d / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond / System.Diagnostics.Stopwatch.Frequency).ToString("0.###");
    }

    private static string FormatFloat(float value)
    {
        if (float.IsNaN(value))
        {
            return "NaN";
        }

        if (float.IsPositiveInfinity(value))
        {
            return "+Infinity";
        }

        if (float.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        return value.ToString("0.##");
    }
}