namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Value changed routed event telemetry snapshot.
/// </summary>
internal readonly record struct ValueChangedRoutedEventTelemetrySnapshot(
    int RaiseCount,
    double RaiseMilliseconds,
    double RouteBuildMilliseconds,
    double RouteTraverseMilliseconds,
    double ClassHandlerMilliseconds,
    double InstanceDispatchMilliseconds,
    double InstancePrepareMilliseconds,
    double InstanceInvokeMilliseconds,
    int MaxRouteLength);