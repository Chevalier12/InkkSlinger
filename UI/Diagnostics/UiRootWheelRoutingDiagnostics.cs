using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private static readonly bool IsWheelRouteDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_WHEEL_ROUTE_LOGS"), "1", StringComparison.Ordinal);

    private void TraceWheelRouting(string message)
    {
        if (!IsWheelRouteDiagnosticsEnabled)
        {
            return;
        }

        var line = $"[WheelRoute] {message}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    private static string DescribeWheelElement(UIElement? element)
    {
        if (element == null)
        {
            return "null";
        }

        var typeName = element.GetType().Name;
        var id = RuntimeHelpers.GetHashCode(element);
        if (element is ScrollViewer viewer)
        {
            return $"{typeName}#{id:X8}(off=({viewer.HorizontalOffset:0.###},{viewer.VerticalOffset:0.###}), vp=({viewer.ViewportWidth:0.###},{viewer.ViewportHeight:0.###}), ext=({viewer.ExtentWidth:0.###},{viewer.ExtentHeight:0.###}))";
        }

        return $"{typeName}#{id:X8}";
    }

    private static string FormatWheelPointer(Vector2 pointerPosition)
    {
        return $"({pointerPosition.X:0.###},{pointerPosition.Y:0.###})";
    }
}
