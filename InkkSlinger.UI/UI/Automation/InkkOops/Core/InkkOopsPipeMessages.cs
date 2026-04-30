namespace InkkSlinger;

public static class InkkOopsPipeRequestKinds
{
    public const string RunScript = "run-script";
    public const string Ping = "ping";
    public const string GetProperty = "get-property";
    public const string AssertProperty = "assert-property";
    public const string AssertExists = "assert-exists";
    public const string AssertNotExists = "assert-not-exists";
    public const string HoverTarget = "hover-target";
    public const string ClickTarget = "click-target";
    public const string InvokeTarget = "invoke-target";
    public const string WaitFrames = "wait-frames";
    public const string WaitForElement = "wait-for-element";
    public const string WaitForVisible = "wait-for-visible";
    public const string WaitForEnabled = "wait-for-enabled";
    public const string WaitForInViewport = "wait-for-in-viewport";
    public const string WaitForInteractive = "wait-for-interactive";
    public const string WaitForIdle = "wait-for-idle";
    public const string Wheel = "wheel";
    public const string ScrollTo = "scroll-to";
    public const string ScrollBy = "scroll-by";
    public const string ScrollIntoView = "scroll-into-view";
    public const string GetTelemetry = "get-telemetry";
    public const string GetTargetDiagnostics = "get-target-diagnostics";
    public const string GetHostInfo = "get-host-info";
    public const string DragTarget = "drag-target";
    public const string TakeScreenshot = "take-screenshot";
}

public sealed class InkkOopsPipeRequest
{
    public string RequestKind { get; set; } = InkkOopsPipeRequestKinds.RunScript;

    public string ScriptName { get; set; } = string.Empty;

    public string TargetName { get; set; } = string.Empty;

    public string ScopeTargetName { get; set; } = string.Empty;

    public string OwnerTargetName { get; set; } = string.Empty;

    public string PropertyName { get; set; } = string.Empty;

    public string ExpectedValue { get; set; } = string.Empty;

    public string ArtifactName { get; set; } = string.Empty;

    public int FrameCount { get; set; }

    public int WheelDelta { get; set; }

    public float HorizontalPercent { get; set; }

    public float VerticalPercent { get; set; }

    public float Padding { get; set; }

    public bool Compact { get; set; }

    public string CounterNames { get; set; } = string.Empty;

    public float DeltaX { get; set; }

    public float DeltaY { get; set; }

    public int TimeoutMilliseconds { get; set; }

    public string ArtifactRootOverride { get; set; } = string.Empty;
}

public sealed class InkkOopsPipeResponse
{
    public string Status { get; set; } = string.Empty;

    public string RequestKind { get; set; } = string.Empty;

    public string ScriptName { get; set; } = string.Empty;

    public string ArtifactDirectory { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
