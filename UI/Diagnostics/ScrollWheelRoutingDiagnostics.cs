using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal static class ScrollWheelRoutingDiagnostics
{
    private static readonly bool IsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_WHEEL_ROUTE_LOGS"), "1", StringComparison.Ordinal);

    internal static void Trace(ScrollViewer viewer, string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        var line = $"[WheelRoute][ScrollViewer#{RuntimeHelpers.GetHashCode(viewer):X8}] {message}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    internal static string FormatPointer(Vector2 pointerPosition)
    {
        return $"({pointerPosition.X:0.###},{pointerPosition.Y:0.###})";
    }
}
