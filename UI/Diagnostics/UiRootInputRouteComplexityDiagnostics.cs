using System;
using System.Diagnostics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double InputRouteComplexityIdleFlushMs = 700d;
    private const int InputRouteComplexityMinSamples = 10;
    private static readonly bool IsInputRouteComplexityDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_INPUT_ROUTE_COMPLEXITY_LOGS"), "1", StringComparison.Ordinal);

    private int _routeComplexitySampleCount;
    private int _routeComplexityPointerRouteCount;
    private int _routeComplexityWheelRouteCount;
    private int _routeComplexityKeyRouteCount;
    private int _routeComplexityDepthTotal;
    private int _routeComplexityDepthMax;
    private int _routeComplexityHandlerTotal;
    private int _routeComplexityHandlerMax;
    private long _routeComplexityLastActivityTimestamp;

    private void ObserveInputRouteComplexity(string routeKind, UIElement target, RoutedEvent routedEvent)
    {
        if (!IsInputRouteComplexityDiagnosticsEnabled || target == null)
        {
            return;
        }

        var depth = 0;
        var handlerCount = 0;
        if (routedEvent.RoutingStrategy == RoutingStrategy.Direct)
        {
            depth = 1;
            handlerCount = target.GetRoutedHandlerCountForEvent(routedEvent) + EventManager.GetClassHandlerCount(target.GetType(), routedEvent);
        }
        else
        {
            for (var current = target; current != null; current = current.VisualParent)
            {
                depth++;
                handlerCount += current.GetRoutedHandlerCountForEvent(routedEvent);
                handlerCount += EventManager.GetClassHandlerCount(current.GetType(), routedEvent);
            }
        }

        _routeComplexitySampleCount++;
        _routeComplexityDepthTotal += depth;
        _routeComplexityDepthMax = Math.Max(_routeComplexityDepthMax, depth);
        _routeComplexityHandlerTotal += handlerCount;
        _routeComplexityHandlerMax = Math.Max(_routeComplexityHandlerMax, handlerCount);
        _routeComplexityLastActivityTimestamp = Stopwatch.GetTimestamp();

        switch (routeKind)
        {
            case "Pointer":
                _routeComplexityPointerRouteCount++;
                break;
            case "Wheel":
                _routeComplexityWheelRouteCount++;
                break;
            case "Key":
                _routeComplexityKeyRouteCount++;
                break;
        }
    }

    private void ObserveInputRouteComplexityAfterUpdate()
    {
        if (!IsInputRouteComplexityDiagnosticsEnabled)
        {
            return;
        }

        TryFlushInputRouteComplexityDiagnostics();
    }

    private void ObserveInputRouteComplexityAfterDraw()
    {
        if (!IsInputRouteComplexityDiagnosticsEnabled)
        {
            return;
        }

        TryFlushInputRouteComplexityDiagnostics();
    }

    private void TryFlushInputRouteComplexityDiagnostics()
    {
        if (_routeComplexitySampleCount < InputRouteComplexityMinSamples || _routeComplexityLastActivityTimestamp == 0)
        {
            return;
        }

        var idleMs = Stopwatch.GetElapsedTime(_routeComplexityLastActivityTimestamp).TotalMilliseconds;
        if (idleMs < InputRouteComplexityIdleFlushMs)
        {
            return;
        }

        var avgDepth = (double)_routeComplexityDepthTotal / _routeComplexitySampleCount;
        var avgPotentialHandlers = (double)_routeComplexityHandlerTotal / _routeComplexitySampleCount;
        var summary =
            $"[InputRouteComplexity] routes={_routeComplexitySampleCount} pointer={_routeComplexityPointerRouteCount} wheel={_routeComplexityWheelRouteCount} key={_routeComplexityKeyRouteCount} " +
            $"depth(avg={avgDepth:0.00},max={_routeComplexityDepthMax}) handlers(avg={avgPotentialHandlers:0.00},max={_routeComplexityHandlerMax})";

        Debug.WriteLine(summary);
        Console.WriteLine(summary);
        ResetInputRouteComplexityDiagnostics();
    }

    private void ResetInputRouteComplexityDiagnostics()
    {
        _routeComplexitySampleCount = 0;
        _routeComplexityPointerRouteCount = 0;
        _routeComplexityWheelRouteCount = 0;
        _routeComplexityKeyRouteCount = 0;
        _routeComplexityDepthTotal = 0;
        _routeComplexityDepthMax = 0;
        _routeComplexityHandlerTotal = 0;
        _routeComplexityHandlerMax = 0;
        _routeComplexityLastActivityTimestamp = 0L;
    }
}