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
    Console.Error.WriteLine("  inkkoops live --attach --command <ping|get-host-info|get-property|assert-property|assert-exists|assert-not-exists|hover|click|invoke|drag|wait-frames|wait-for-element|wait-for-visible|wait-for-enabled|wait-for-in-viewport|wait-for-interactive|wait-for-idle|wheel|scroll-to|scroll-by|scroll-into-view|get-telemetry|get-target-diagnostics|screenshot|take-screenshot> [--scope <name>] [--owner <name>] [--target <name>] [--property <name>] [--expected <value>] [--frames <count>] [--delta <value>] [--delta-x <value>] [--delta-y <value>] [--horizontal <percent>] [--vertical <percent>] [--padding <value>] [--artifact <name>] [--compact] [--counters <names>] [--pipe <name>] [--timeout <ms>] [--artifacts <path>]");
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
            : 0
    };
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
