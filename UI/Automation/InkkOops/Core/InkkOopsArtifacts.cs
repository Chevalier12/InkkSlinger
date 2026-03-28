using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace InkkSlinger;

public sealed class InkkOopsArtifacts : IDisposable
{
    private readonly StreamWriter _commandLogWriter;

    public InkkOopsArtifacts(string rootPath, string scriptName)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
        DirectoryPath = Path.GetFullPath(Path.Combine(rootPath, $"{timestamp}-{Sanitize(scriptName)}"));
        Directory.CreateDirectory(DirectoryPath);
        _commandLogWriter = new StreamWriter(Path.Combine(DirectoryPath, "commands.log"), append: false, Encoding.UTF8)
        {
            AutoFlush = true
        };
    }

    public string DirectoryPath { get; }

    public string GetPath(string fileName)
    {
        return Path.Combine(DirectoryPath, fileName);
    }

    public void LogCommand(int index, string description)
    {
        _commandLogWriter.WriteLine($"{DateTime.UtcNow:O} command[{index}]={description}");
    }

    public void WriteResult(InkkOopsRunResult result)
    {
        var json = JsonSerializer.Serialize(
            new
            {
                status = result.Status.ToString(),
                scriptName = result.ScriptName,
                artifactDirectory = result.ArtifactDirectory,
                commandCount = result.CommandCount,
                failedCommandIndex = result.FailedCommandIndex,
                failedCommandDescription = result.FailedCommandDescription,
                failureMessage = result.FailureMessage,
                durationMilliseconds = result.Duration.TotalMilliseconds
            },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetPath("result.json"), json, Encoding.UTF8);
    }

    public void WriteCommandDiagnostics(InkkOopsCommandDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var fileName = $"command-{diagnostics.CommandIndex:000}.json";
        var json = JsonSerializer.Serialize(
            diagnostics,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
        File.WriteAllText(GetPath(fileName), json, Encoding.UTF8);
    }

    public void Dispose()
    {
        _commandLogWriter.Dispose();
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "script";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return value;
    }
}
