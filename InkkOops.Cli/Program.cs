using System.Diagnostics;
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
    Console.Error.WriteLine("  inkkoops run --script <name> --launch [--project <path>] [--pipe <name>] [--artifacts <path>] [--object-observer <name[,name]>]");
    Console.Error.WriteLine("  inkkoops run --script <name> --attach [--pipe <name>] [--timeout <ms>] [--artifacts <path>]");
    Console.Error.WriteLine("  inkkoops record --launch [--project <path>] [--artifacts <path>]");
    Console.Error.WriteLine("  inkkoops <recording-path> [--project <path>] [--artifacts <path>] [--object-observer <name[,name]>]");
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

static int[] ParseActionDiagnosticsIndexes(Dictionary<string, string> options)
{
    if (!options.TryGetValue("action-diagnostics", out var text) || string.IsNullOrWhiteSpace(text))
    {
        return [];
    }

    var parts = text.Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var values = new List<int>(parts.Length);
    for (var i = 0; i < parts.Length; i++)
    {
        if (int.TryParse(parts[i], out var value) && value >= 0)
        {
            values.Add(value);
        }
    }

    return [.. values];
}

static string ResolvePipeName(Dictionary<string, string> options, InkkOopsHostConfiguration hostConfiguration)
{
    return options.TryGetValue("pipe", out var pipe) && !string.IsNullOrWhiteSpace(pipe)
        ? pipe
        : hostConfiguration.DefaultNamedPipeName;
}

static async Task<int> RunAttachAsync(Dictionary<string, string> options, InkkOopsHostConfiguration hostConfiguration)
{
    if (!options.TryGetValue("script", out var scriptName) || string.IsNullOrWhiteSpace(scriptName))
    {
        return PrintUsage();
    }

    var pipeName = ResolvePipeName(options, hostConfiguration);
    var timeoutMilliseconds = options.TryGetValue("timeout", out var timeoutText) && int.TryParse(timeoutText, out var timeout)
        ? timeout
        : 120000;

    using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    await client.ConnectAsync(timeoutMilliseconds).ConfigureAwait(false);
    using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
    using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
    var request = new InkkOopsPipeRequest
    {
        ScriptName = scriptName,
        ActionDiagnosticsIndexes = ParseActionDiagnosticsIndexes(options),
        TimeoutMilliseconds = timeoutMilliseconds,
        ArtifactRootOverride = options.TryGetValue("artifacts", out var artifacts) ? artifacts : string.Empty
    };
    var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
    await writer.WriteLineAsync(requestJson).ConfigureAwait(false);
    var responseJson = await reader.ReadLineAsync().ConfigureAwait(false);
    Console.WriteLine(responseJson);
    if (string.IsNullOrWhiteSpace(responseJson))
    {
        return InkkOopsExitCodes.Failed;
    }

    var response = JsonSerializer.Deserialize<InkkOopsPipeResponse>(responseJson);
    return InkkOopsExitCodes.FromStatus(response?.Status);
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

    if (options.TryGetValue("action-diagnostics", out var actionDiagnostics))
    {
        arguments.Append("--inkkoops-action-diagnostics \"").Append(actionDiagnostics).Append("\" ");
    }

    if (options.TryGetValue("object-observer", out var objectObserver))
    {
        arguments.Append("--inkkoops-object-observer \"").Append(objectObserver).Append("\" ");
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

    if (options.TryGetValue("object-observer", out var objectObserver))
    {
        arguments.Append("--inkkoops-object-observer \"").Append(objectObserver).Append("\" ");
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
    !string.Equals(args[0], "record", StringComparison.Ordinal))
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

if (args.Length >= 2 && string.Equals(args[0], "record", StringComparison.Ordinal))
{
    var options = ParseOptions(args, 1);
    if (options.ContainsKey("launch"))
    {
        return RunRecordLaunch(options, launchTargetResolver);
    }
}

return PrintUsage();
