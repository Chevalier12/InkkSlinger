using System.Diagnostics;
using System.Buffers.Binary;
using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using InkkSlinger;
using InkkSlinger.Cli;

static int PrintUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  inkkoops list");
    Console.Error.WriteLine("  inkkoops run --script <name> --launch [--project <path>] [--pipe <name>] [--artifacts <path>]");
    Console.Error.WriteLine("  inkkoops run --script <name> --attach [--pipe <name>] [--timeout <ms>] [--artifacts <path>]");
    Console.Error.WriteLine("  inkkoops live --attach --command <ping|get-host-info|get-property|assert-property|assert-exists|assert-not-exists|move-pointer|hover|click|invoke|drag|double-click-target|right-click-target|leave-target|pointer-down|pointer-up|pointer-down-target|pointer-up-target|key-down|key-up|text-input|set-clipboard-text|maximize-window|resize-window|wait-frames|wait-for-element|wait-for-visible|wait-for-enabled|wait-for-in-viewport|wait-for-interactive|wait-for-idle|wheel|scroll-to|scroll-by|scroll-into-view|get-telemetry|get-target-diagnostics|screenshot|take-screenshot|capture-frame|dump-telemetry|move-pointer-path|drag-path-target|assert-automation-event|run-scenario|probe-during-drag|assert-nonblank|diff-telemetry> [--scenario <json-file>] [--scope <name>] [--owner <name>] [--target <name>] [--property <name>] [--expected <value>] [--key-name <name>] [--text <text>] [--event-type <type>] [--button <left|right|middle|xbutton1|xbutton2>] [--waypoints <json>] [--width <px>] [--height <px>] [--x <value>] [--y <value>] [--anchor <center|top-left|top-right|bottom-left|bottom-right|offset>] [--offset-x <value>] [--offset-y <value>] [--frames <count>] [--travel-frames <count>] [--step-distance <value>] [--easing <linear|ease-in-out>] [--dwell-frames <count>] [--delta <value>] [--delta-x <value>] [--delta-y <value>] [--horizontal <percent>] [--vertical <percent>] [--padding <value>] [--min-bright-pixels <count>] [--min-average-luma <value>] [--artifact <name>] [--compact] [--counters <names>] [--pipe <name>] [--timeout <ms>] [--artifacts <path>]");
    Console.Error.WriteLine("  inkkoops record --launch [--project <path>] [--artifacts <path>]");
    Console.Error.WriteLine("  inkkoops <recording-path> [--project <path>] [--artifacts <path>]");
    return 1;
}

static Dictionary<string, string> ParseOptions(string[] args, int startIndex)
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = startIndex; i < args.Length; i++)
    {
        var arg = args[i];
        if (!arg.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = arg[2..];
        var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[++i]
            : "true";
        options[key] = value;
    }

    return options;
}

static string ResolvePipeName(Dictionary<string, string> options, InkkOopsHostConfiguration hostConfiguration)
{
    return options.TryGetValue("pipe", out var pipe) && !string.IsNullOrWhiteSpace(pipe)
        ? pipe
        : hostConfiguration.DefaultNamedPipeName;
}

static async Task<int> RunAttachAsync(Dictionary<string, string> options, InkkOopsHostConfiguration hostConfiguration)
{
    var pipeName = ResolvePipeName(options, hostConfiguration);
    var timeoutMilliseconds = options.TryGetValue("timeout", out var timeoutText) && int.TryParse(timeoutText, out var timeout)
        ? timeout
        : 120000;

    using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    await client.ConnectAsync(timeoutMilliseconds).ConfigureAwait(false);
    var request = BuildAttachRequest(options, timeoutMilliseconds);
    if (request == null)
    {
        return PrintUsage();
    }

    var requestJson = JsonSerializer.Serialize(request);
    await WritePipeMessageAsync(client, requestJson).ConfigureAwait(false);
    var responseJson = await ReadPipeMessageAsync(client).ConfigureAwait(false);
    Console.WriteLine(responseJson);
    if (string.IsNullOrWhiteSpace(responseJson))
    {
        return InkkOopsExitCodes.Failed;
    }

    var response = JsonSerializer.Deserialize<InkkOopsPipeResponse>(responseJson);
    return InkkOopsExitCodes.FromStatus(response?.Status);
}

static async Task WritePipeMessageAsync(Stream stream, string payload)
{
    var bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
    var lengthPrefix = new byte[sizeof(int)];
    BinaryPrimitives.WriteInt32LittleEndian(lengthPrefix, bytes.Length);
    await stream.WriteAsync(lengthPrefix).ConfigureAwait(false);
    await stream.WriteAsync(bytes).ConfigureAwait(false);
    await stream.FlushAsync().ConfigureAwait(false);
}

static async Task<string> ReadPipeMessageAsync(Stream stream)
{
    var lengthPrefix = new byte[sizeof(int)];
    await ReadExactlyAsync(stream, lengthPrefix).ConfigureAwait(false);
    var length = BinaryPrimitives.ReadInt32LittleEndian(lengthPrefix);
    if (length <= 0)
    {
        return string.Empty;
    }

    var payload = new byte[length];
    await ReadExactlyAsync(stream, payload).ConfigureAwait(false);
    return Encoding.UTF8.GetString(payload);
}

static async Task ReadExactlyAsync(Stream stream, byte[] buffer)
{
    var offset = 0;
    while (offset < buffer.Length)
    {
        var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset)).ConfigureAwait(false);
        if (read <= 0)
        {
            throw new EndOfStreamException("The pipe closed before the full message was received.");
        }

        offset += read;
    }
}

static InkkOopsPipeRequest? BuildAttachRequest(Dictionary<string, string> options, int timeoutMilliseconds)
{
    if (options.TryGetValue("script", out var scriptName) && !string.IsNullOrWhiteSpace(scriptName))
    {
        return new InkkOopsPipeRequest
        {
            RequestKind = InkkOopsPipeRequestKinds.RunScript,
            ScriptName = scriptName,
            TimeoutMilliseconds = timeoutMilliseconds,
            ArtifactRootOverride = options.TryGetValue("artifacts", out var scriptArtifacts) ? scriptArtifacts : string.Empty
        };
    }

    if (!options.TryGetValue("command", out var commandText) || string.IsNullOrWhiteSpace(commandText))
    {
        return null;
    }

    var requestKind = commandText.Trim().ToLowerInvariant() switch
    {
        "ping" => InkkOopsPipeRequestKinds.Ping,
        "get-host-info" => InkkOopsPipeRequestKinds.GetHostInfo,
        "host-info" => InkkOopsPipeRequestKinds.GetHostInfo,
        "get-property" => InkkOopsPipeRequestKinds.GetProperty,
        "assert-property" => InkkOopsPipeRequestKinds.AssertProperty,
        "assert-exists" => InkkOopsPipeRequestKinds.AssertExists,
        "assert-not-exists" => InkkOopsPipeRequestKinds.AssertNotExists,
        "move-pointer" => InkkOopsPipeRequestKinds.MovePointer,
        "move" => InkkOopsPipeRequestKinds.MovePointer,
        "hover" => InkkOopsPipeRequestKinds.HoverTarget,
        "click" => InkkOopsPipeRequestKinds.ClickTarget,
        "invoke" => InkkOopsPipeRequestKinds.InvokeTarget,
        "activate" => InkkOopsPipeRequestKinds.InvokeTarget,
        "drag" => InkkOopsPipeRequestKinds.DragTarget,
        "wait-frames" => InkkOopsPipeRequestKinds.WaitFrames,
        "wait-for-element" => InkkOopsPipeRequestKinds.WaitForElement,
        "wait-for-visible" => InkkOopsPipeRequestKinds.WaitForVisible,
        "wait-for-enabled" => InkkOopsPipeRequestKinds.WaitForEnabled,
        "wait-for-in-viewport" => InkkOopsPipeRequestKinds.WaitForInViewport,
        "wait-for-interactive" => InkkOopsPipeRequestKinds.WaitForInteractive,
        "wait-for-idle" => InkkOopsPipeRequestKinds.WaitForIdle,
        "wheel" => InkkOopsPipeRequestKinds.Wheel,
        "scroll-to" => InkkOopsPipeRequestKinds.ScrollTo,
        "scroll-by" => InkkOopsPipeRequestKinds.ScrollBy,
        "scroll-into-view" => InkkOopsPipeRequestKinds.ScrollIntoView,
        "get-telemetry" => InkkOopsPipeRequestKinds.GetTelemetry,
        "get-target-diagnostics" => InkkOopsPipeRequestKinds.GetTargetDiagnostics,
        "screenshot" => InkkOopsPipeRequestKinds.TakeScreenshot,
        "take-screenshot" => InkkOopsPipeRequestKinds.TakeScreenshot,
        "double-click-target" => InkkOopsPipeRequestKinds.DoubleClickTarget,
        "double-click" => InkkOopsPipeRequestKinds.DoubleClickTarget,
        "right-click-target" => InkkOopsPipeRequestKinds.RightClickTarget,
        "right-click" => InkkOopsPipeRequestKinds.RightClickTarget,
        "key-down" => InkkOopsPipeRequestKinds.KeyDown,
        "key-up" => InkkOopsPipeRequestKinds.KeyUp,
        "text-input" => InkkOopsPipeRequestKinds.TextInput,
        "type" => InkkOopsPipeRequestKinds.TextInput,
        "set-clipboard-text" => InkkOopsPipeRequestKinds.SetClipboardText,
        "set-clipboard" => InkkOopsPipeRequestKinds.SetClipboardText,
        "maximize-window" => InkkOopsPipeRequestKinds.MaximizeWindow,
        "maximize" => InkkOopsPipeRequestKinds.MaximizeWindow,
        "resize-window" => InkkOopsPipeRequestKinds.ResizeWindow,
        "resize" => InkkOopsPipeRequestKinds.ResizeWindow,
        "leave-target" => InkkOopsPipeRequestKinds.LeaveTarget,
        "leave" => InkkOopsPipeRequestKinds.LeaveTarget,
        "capture-frame" => InkkOopsPipeRequestKinds.CaptureFrame,
        "dump-telemetry" => InkkOopsPipeRequestKinds.DumpTelemetry,
        "assert-automation-event" => InkkOopsPipeRequestKinds.AssertAutomationEvent,
        "assert-event" => InkkOopsPipeRequestKinds.AssertAutomationEvent,
        "pointer-down" => InkkOopsPipeRequestKinds.PointerDown,
        "pointer-up" => InkkOopsPipeRequestKinds.PointerUp,
        "pointer-down-target" => InkkOopsPipeRequestKinds.PointerDownTarget,
        "pointer-up-target" => InkkOopsPipeRequestKinds.PointerUpTarget,
        "move-pointer-path" => InkkOopsPipeRequestKinds.MovePointerPath,
        "drag-path-target" => InkkOopsPipeRequestKinds.DragPathTarget,
        "drag-path" => InkkOopsPipeRequestKinds.DragPathTarget,
        "run-scenario" => InkkOopsPipeRequestKinds.RunScenario,
        "scenario" => InkkOopsPipeRequestKinds.RunScenario,
        "probe-during-drag" => InkkOopsPipeRequestKinds.ProbeDuringDrag,
        "probe-drag" => InkkOopsPipeRequestKinds.ProbeDuringDrag,
        "assert-nonblank" => InkkOopsPipeRequestKinds.AssertNonBlank,
        "assert-frame-nonblank" => InkkOopsPipeRequestKinds.AssertNonBlank,
        "diff-telemetry" => InkkOopsPipeRequestKinds.DiffTelemetry,
        _ => string.Empty
    };

    if (string.IsNullOrWhiteSpace(requestKind))
    {
        return null;
    }

    return new InkkOopsPipeRequest
    {
        RequestKind = requestKind,
        TimeoutMilliseconds = timeoutMilliseconds,
        ArtifactRootOverride = options.TryGetValue("artifacts", out var liveArtifacts) ? liveArtifacts : string.Empty,
        ScopeTargetName = options.TryGetValue("scope", out var scopeTargetName) ? scopeTargetName : string.Empty,
        OwnerTargetName = options.TryGetValue("owner", out var ownerTargetName) ? ownerTargetName : string.Empty,
        TargetName = options.TryGetValue("target", out var targetName) ? targetName : string.Empty,
        X = options.TryGetValue("x", out var xText) && float.TryParse(xText, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            ? x
            : null,
        Y = options.TryGetValue("y", out var yText) && float.TryParse(yText, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            ? y
            : null,
        Anchor = options.TryGetValue("anchor", out var anchor) ? anchor : string.Empty,
        OffsetX = options.TryGetValue("offset-x", out var offsetXText) && float.TryParse(offsetXText, NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetX)
            ? offsetX
            : 0f,
        OffsetY = options.TryGetValue("offset-y", out var offsetYText) && float.TryParse(offsetYText, NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetY)
            ? offsetY
            : 0f,
        PropertyName = options.TryGetValue("property", out var propertyName) ? propertyName : string.Empty,
        ExpectedValue = options.TryGetValue("expected", out var expectedValue) ? expectedValue : string.Empty,
        ArtifactName = options.TryGetValue("artifact", out var artifactName) ? artifactName : string.Empty,
        Compact = options.ContainsKey("compact"),
        CounterNames = options.TryGetValue("counters", out var counterNames) ? counterNames : string.Empty,
        DeltaX = options.TryGetValue("delta-x", out var deltaXText) && float.TryParse(deltaXText, NumberStyles.Float, CultureInfo.InvariantCulture, out var deltaX)
            ? deltaX
            : 0f,
        DeltaY = options.TryGetValue("delta-y", out var deltaYText) && float.TryParse(deltaYText, NumberStyles.Float, CultureInfo.InvariantCulture, out var deltaY)
            ? deltaY
            : 0f,
        TravelFrames = options.TryGetValue("travel-frames", out var travelFramesText) && int.TryParse(travelFramesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var travelFrames)
            ? travelFrames
            : 0,
        StepDistance = options.TryGetValue("step-distance", out var stepDistanceText) && float.TryParse(stepDistanceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var stepDistance)
            ? stepDistance
            : 0f,
        Easing = options.TryGetValue("easing", out var easing) ? easing : string.Empty,
        DwellFrames = options.TryGetValue("dwell-frames", out var dwellFramesText) && int.TryParse(dwellFramesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dwellFrames)
            ? dwellFrames
            : options.TryGetValue("frames", out var dwellFallbackText) && int.TryParse(dwellFallbackText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dwellFallback)
                ? dwellFallback
            : 0,
        WheelDelta = options.TryGetValue("delta", out var deltaText) && int.TryParse(deltaText, out var wheelDelta)
            ? wheelDelta
            : 0,
        HorizontalPercent = options.TryGetValue("horizontal", out var horizontalText) && float.TryParse(horizontalText, NumberStyles.Float, CultureInfo.InvariantCulture, out var horizontalPercent)
            ? horizontalPercent
            : 0f,
        VerticalPercent = options.TryGetValue("vertical", out var verticalText) && float.TryParse(verticalText, NumberStyles.Float, CultureInfo.InvariantCulture, out var verticalPercent)
            ? verticalPercent
            : 0f,
        Padding = options.TryGetValue("padding", out var paddingText) && float.TryParse(paddingText, NumberStyles.Float, CultureInfo.InvariantCulture, out var padding)
            ? padding
            : 0f,
        FrameCount = options.TryGetValue("frames", out var framesText) && int.TryParse(framesText, out var frameCount)
            ? frameCount
            : 0,
        KeyName = options.TryGetValue("key-name", out var keyName) ? keyName : string.Empty,
        Text = ResolveLiveText(options),
        Width = options.TryGetValue("width", out var widthText) && int.TryParse(widthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
            ? width
            : 0,
        Height = options.TryGetValue("height", out var heightText) && int.TryParse(heightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
            ? height
            : 0,
        EventType = options.TryGetValue("event-type", out var eventType) ? eventType : string.Empty,
        ButtonName = options.TryGetValue("button", out var buttonName) ? buttonName : string.Empty,
        Waypoints = options.TryGetValue("waypoints", out var waypoints) ? waypoints : string.Empty,
        ScenarioName = options.TryGetValue("scenario-name", out var scenarioName) ? scenarioName : string.Empty,
        MinBrightPixels = options.TryGetValue("min-bright-pixels", out var minBrightText) && int.TryParse(minBrightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minBright)
            ? minBright
            : 1,
        MinAverageLuma = options.TryGetValue("min-average-luma", out var minAverageLumaText) && float.TryParse(minAverageLumaText, NumberStyles.Float, CultureInfo.InvariantCulture, out var minAverageLuma)
            ? minAverageLuma
            : 0f
    };
}

static string ResolveLiveText(Dictionary<string, string> options)
{
    if (options.TryGetValue("scenario", out var scenarioPath) && !string.IsNullOrWhiteSpace(scenarioPath))
    {
        return File.ReadAllText(scenarioPath, Encoding.UTF8);
    }

    return options.TryGetValue("text", out var textValue) ? textValue : string.Empty;
}

static int RunLaunch(Dictionary<string, string> options, InkkOopsHostConfiguration hostConfiguration, IInkkOopsLaunchTargetResolver launchTargetResolver)
{
    if (!options.TryGetValue("script", out var scriptName) || string.IsNullOrWhiteSpace(scriptName))
    {
        return PrintUsage();
    }

    var launchTarget = launchTargetResolver.Resolve(options);
    var arguments = new StringBuilder();
    arguments.Append("run --project \"").Append(launchTarget.ProjectPath).Append("\" -- ");
    arguments.Append("--inkkoops-script \"").Append(scriptName).Append("\" ");
    if (options.TryGetValue("pipe", out var pipeName) || !string.IsNullOrWhiteSpace(hostConfiguration.DefaultNamedPipeName))
    {
        arguments.Append("--inkkoops-pipe \"").Append(ResolvePipeName(options, hostConfiguration)).Append("\" ");
    }

    if (options.TryGetValue("artifacts", out var artifacts))
    {
        arguments.Append("--inkkoops-artifacts \"").Append(artifacts).Append("\" ");
    }

    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = arguments.ToString(),
        WorkingDirectory = launchTarget.WorkingDirectory,
        UseShellExecute = false
    });
    process?.WaitForExit();
    return process?.ExitCode ?? 1;
}

static int RunRecordLaunch(Dictionary<string, string> options, IInkkOopsLaunchTargetResolver launchTargetResolver)
{
    var launchTarget = launchTargetResolver.Resolve(options);
    var arguments = new StringBuilder();
    arguments.Append("run --project \"").Append(launchTarget.ProjectPath).Append("\" -- --inkkoops-record ");
    arguments.Append("--inkkoops-project \"").Append(launchTarget.ProjectPath).Append("\" ");
    if (options.TryGetValue("artifacts", out var artifacts))
    {
        arguments.Append("--inkkoops-record-root \"").Append(artifacts).Append("\" ");
    }

    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = arguments.ToString(),
        WorkingDirectory = launchTarget.WorkingDirectory,
        UseShellExecute = false
    });
    process?.WaitForExit();
    return process?.ExitCode ?? 1;
}

static int RunRecordingAutoLaunch(string recordingPath, Dictionary<string, string> options, IInkkOopsLaunchTargetResolver launchTargetResolver)
{
    InkkOopsLaunchTarget launchTarget;
    if (options.TryGetValue("project", out var projectPath) && !string.IsNullOrWhiteSpace(projectPath))
    {
        launchTarget = launchTargetResolver.Resolve(options);
    }
    else
    {
        var metadata = InkkOopsRecordedSessionLoader.ReadMetadata(recordingPath);
        if (string.IsNullOrWhiteSpace(metadata.RecordedProjectPath))
        {
            Console.Error.WriteLine(
                $"Recording '{metadata.RecordingPath}' does not specify a recorded project. Provide --project <path> to replay it.");
            return 1;
        }

        var recordedProjectOptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["project"] = metadata.RecordedProjectPath
        };
        launchTarget = launchTargetResolver.Resolve(recordedProjectOptions);
    }

    if (!File.Exists(launchTarget.ProjectPath))
    {
        Console.Error.WriteLine($"The provided file path does not exist: {launchTarget.ProjectPath}.");
        return 1;
    }

    var arguments = new StringBuilder();
    arguments.Append("run --project \"").Append(launchTarget.ProjectPath).Append("\" -- ");
    arguments.Append("--inkkoops-recording \"").Append(recordingPath).Append("\" ");
    if (options.TryGetValue("artifacts", out var artifacts))
    {
        arguments.Append("--inkkoops-artifacts \"").Append(artifacts).Append("\" ");
    }

    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = arguments.ToString(),
        WorkingDirectory = launchTarget.WorkingDirectory,
        UseShellExecute = false
    });
    process?.WaitForExit();
    return process?.ExitCode ?? 1;
}

var hostConfiguration = InkkOopsHostConfiguration.CreateDefault(typeof(ControlsCatalogView).Assembly);
var launchTargetResolver = new DefaultInkkOopsLaunchTargetResolver();
if (args.Length >= 1 &&
    !string.Equals(args[0], "list", StringComparison.Ordinal) &&
    !string.Equals(args[0], "run", StringComparison.Ordinal) &&
    !string.Equals(args[0], "record", StringComparison.Ordinal) &&
    !string.Equals(args[0], "live", StringComparison.Ordinal))
{
    var options = ParseOptions(args, 1);
    return RunRecordingAutoLaunch(args[0], options, launchTargetResolver);
}

if (args.Length == 0)
{
    return PrintUsage();
}

if (string.Equals(args[0], "list", StringComparison.Ordinal))
{
    foreach (var script in hostConfiguration.ScriptCatalog.ListScripts())
    {
        Console.WriteLine(script);
    }

    return 0;
}

if (args.Length >= 2 && string.Equals(args[0], "run", StringComparison.Ordinal))
{
    var options = ParseOptions(args, 1);
    if (options.ContainsKey("attach"))
    {
        return await RunAttachAsync(options, hostConfiguration).ConfigureAwait(false);
    }

    if (options.ContainsKey("launch"))
    {
        return RunLaunch(options, hostConfiguration, launchTargetResolver);
    }
}

if (args.Length >= 2 && string.Equals(args[0], "live", StringComparison.Ordinal))
{
    var options = ParseOptions(args, 1);
    if (options.ContainsKey("attach"))
    {
        return await RunAttachAsync(options, hostConfiguration).ConfigureAwait(false);
    }
}

if (args.Length >= 2 && string.Equals(args[0], "record", StringComparison.Ordinal))
{
    var options = ParseOptions(args, 1);
    if (options.ContainsKey("launch"))
    {
        return RunRecordLaunch(options, launchTargetResolver);
    }
}

return PrintUsage();
