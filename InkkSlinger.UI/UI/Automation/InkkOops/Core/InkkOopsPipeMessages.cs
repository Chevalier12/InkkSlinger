namespace InkkSlinger;

public static class InkkOopsPipeRequestKinds
{
    public const string RunScript = "run-script";
    public const string Ping = "ping";
    public const string GetProperty = "get-property";
    public const string AssertProperty = "assert-property";
    public const string AssertExists = "assert-exists";
    public const string AssertNotExists = "assert-not-exists";
    public const string MovePointer = "move-pointer";
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

    // ── Newly exposed commands ───────────────────────────────────────────
    public const string DoubleClickTarget = "double-click-target";
    public const string RightClickTarget = "right-click-target";
    public const string KeyDown = "key-down";
    public const string KeyUp = "key-up";
    public const string TextInput = "text-input";
    public const string SetClipboardText = "set-clipboard-text";
    public const string MaximizeWindow = "maximize-window";
    public const string ResizeWindow = "resize-window";
    public const string LeaveTarget = "leave-target";
    public const string CaptureFrame = "capture-frame";
    public const string DumpTelemetry = "dump-telemetry";
    public const string DragPathTarget = "drag-path-target";
    public const string AssertAutomationEvent = "assert-automation-event";

    // ── Pointer state / path commands ────────────────────────────────────
    public const string PointerDown = "pointer-down";
    public const string PointerUp = "pointer-up";
    public const string PointerDownTarget = "pointer-down-target";
    public const string PointerUpTarget = "pointer-up-target";
    public const string MovePointerPath = "move-pointer-path";
    public const string RunScenario = "run-scenario";
    public const string ProbeDuringDrag = "probe-during-drag";
    public const string ProbeScrollbarThumbDrag = "probe-scrollbar-thumb-drag";
    public const string ProbeAction = "probe-action";
    public const string AssertNonBlank = "assert-nonblank";
    public const string DiffTelemetry = "diff-telemetry";
}

public sealed class InkkOopsPipeRequest
{
    public string RequestKind { get; set; } = InkkOopsPipeRequestKinds.RunScript;

    public string ScriptName { get; set; } = string.Empty;

    public string TargetName { get; set; } = string.Empty;

    public float? X { get; set; }

    public float? Y { get; set; }

    public string Anchor { get; set; } = string.Empty;

    public float OffsetX { get; set; }

    public float OffsetY { get; set; }

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

    public int TravelFrames { get; set; }

    public float StepDistance { get; set; }

    public string Easing { get; set; } = string.Empty;

    public int DwellFrames { get; set; }

    // ── Fields for newly exposed commands ────────────────────────────────
    public string KeyName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ButtonName { get; set; } = string.Empty;
    public string Waypoints { get; set; } = string.Empty;

    public string ScenarioName { get; set; } = string.Empty;

    public string Axis { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;

    public int SampleCount { get; set; }

    public int MinBrightPixels { get; set; }

    public float MinAverageLuma { get; set; }

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
