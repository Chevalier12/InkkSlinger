using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace InkkSlinger;

public sealed class InkkOopsArtifacts : IDisposable
{
    private readonly StreamWriter _commandLogWriter;
    private readonly IInkkOopsArtifactNamingPolicy _namingPolicy;

    public InkkOopsArtifacts(string rootPath, string scriptName)
        : this(rootPath, scriptName, new DefaultInkkOopsArtifactNamingPolicy())
    {
    }

    public InkkOopsArtifacts(string rootPath, string scriptName, IInkkOopsArtifactNamingPolicy namingPolicy)
    {
        _namingPolicy = namingPolicy ?? throw new ArgumentNullException(nameof(namingPolicy));
        DirectoryPath = Path.GetFullPath(Path.Combine(rootPath, _namingPolicy.CreateRunDirectoryName(scriptName, DateTime.UtcNow)));
        Directory.CreateDirectory(DirectoryPath);
        _commandLogWriter = new StreamWriter(Path.Combine(DirectoryPath, _namingPolicy.GetCommandLogFileName()), append: false, Encoding.UTF8)
        {
            AutoFlush = true
        };
    }

    public string DirectoryPath { get; }

    public string GetPath(string fileName)
    {
        return Path.Combine(DirectoryPath, fileName);
    }

    public string GetCommandLogPath()
    {
        return GetPath(_namingPolicy.GetCommandLogFileName());
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
        File.WriteAllText(GetPath(_namingPolicy.GetResultFileName()), json, Encoding.UTF8);
    }

    public void WriteCommandDiagnostics(InkkOopsCommandDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var fileName = _namingPolicy.GetCommandDiagnosticsFileName(diagnostics.CommandIndex);
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
}
