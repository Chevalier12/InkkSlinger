using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using InkkSlinger;

static int PrintUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  inkkoops list");
    Console.Error.WriteLine("  inkkoops run --script <name> --launch [--pipe <name>] [--artifacts <path>]");
    Console.Error.WriteLine("  inkkoops run --script <name> --attach [--pipe <name>] [--timeout <ms>] [--artifacts <path>]");
    Console.Error.WriteLine("  inkkoops record --launch [--artifacts <path>]");
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

static async Task<int> RunAttachAsync(Dictionary<string, string> options)
{
    if (!options.TryGetValue("script", out var scriptName) || string.IsNullOrWhiteSpace(scriptName))
    {
        return PrintUsage();
    }

    var pipeName = options.TryGetValue("pipe", out var pipe) ? pipe : "InkkOops";
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
        TimeoutMilliseconds = timeoutMilliseconds,
        ArtifactRootOverride = options.TryGetValue("artifacts", out var artifacts) ? artifacts : string.Empty
    };
    var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
    await writer.WriteLineAsync(requestJson).ConfigureAwait(false);
    var responseJson = await reader.ReadLineAsync().ConfigureAwait(false);
    Console.WriteLine(responseJson);
    return 0;
}

static int RunLaunch(Dictionary<string, string> options)
{
    if (!options.TryGetValue("script", out var scriptName) || string.IsNullOrWhiteSpace(scriptName))
    {
        return PrintUsage();
    }

    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    var projectPath = Path.Combine(repoRoot, "InkkSlinger.csproj");
    var arguments = new StringBuilder();
    arguments.Append("run --project \"").Append(projectPath).Append("\" -- ");
    arguments.Append("--inkkoops-script \"").Append(scriptName).Append("\" ");
    if (options.TryGetValue("pipe", out var pipeName))
    {
        arguments.Append("--inkkoops-pipe \"").Append(pipeName).Append("\" ");
    }

    if (options.TryGetValue("artifacts", out var artifacts))
    {
        arguments.Append("--inkkoops-artifacts \"").Append(artifacts).Append("\" ");
    }

    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = arguments.ToString(),
        WorkingDirectory = repoRoot,
        UseShellExecute = false
    });
    process?.WaitForExit();
    return process?.ExitCode ?? 1;
}

static int RunRecordLaunch(Dictionary<string, string> options)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    var projectPath = Path.Combine(repoRoot, "InkkSlinger.csproj");
    var arguments = new StringBuilder();
    arguments.Append("run --project \"").Append(projectPath).Append("\" -- --inkkoops-record ");
    if (options.TryGetValue("artifacts", out var artifacts))
    {
        arguments.Append("--inkkoops-record-root \"").Append(artifacts).Append("\" ");
    }

    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = arguments.ToString(),
        WorkingDirectory = repoRoot,
        UseShellExecute = false
    });
    process?.WaitForExit();
    return process?.ExitCode ?? 1;
}

var registry = new InkkOopsScriptRegistry(typeof(Game1).Assembly);
if (args.Length == 0)
{
    return PrintUsage();
}

if (string.Equals(args[0], "list", StringComparison.Ordinal))
{
    foreach (var script in registry.ListScripts())
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
        return await RunAttachAsync(options).ConfigureAwait(false);
    }

    if (options.ContainsKey("launch"))
    {
        return RunLaunch(options);
    }
}

if (args.Length >= 2 && string.Equals(args[0], "record", StringComparison.Ordinal))
{
    var options = ParseOptions(args, 1);
    if (options.ContainsKey("launch"))
    {
        return RunRecordLaunch(options);
    }
}

return PrintUsage();
