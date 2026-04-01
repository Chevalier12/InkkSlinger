using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace InkkSlinger;

public sealed class InkkOopsArtifacts : IDisposable
{
    private readonly StreamWriter _commandLogWriter;
    private readonly StreamWriter _semanticLogWriter;
    private readonly IInkkOopsArtifactNamingPolicy _namingPolicy;
    private string? _lastSemanticLogSubject;

    public InkkOopsArtifacts(string rootPath, string scriptName)
        : this(rootPath, scriptName, new DefaultInkkOopsArtifactNamingPolicy())
    {
    }

    public InkkOopsArtifacts(string rootPath, string scriptName, IInkkOopsArtifactNamingPolicy namingPolicy)
    {
        _namingPolicy = namingPolicy ?? throw new ArgumentNullException(nameof(namingPolicy));
        DirectoryPath = Path.GetFullPath(Path.Combine(rootPath, _namingPolicy.CreateRunDirectoryName(scriptName, DateTime.UtcNow)));
        Directory.CreateDirectory(DirectoryPath);
        _commandLogWriter = CreateWriter(_namingPolicy.GetCommandLogFileName());
        _semanticLogWriter = CreateWriter(_namingPolicy.GetSemanticLogFileName());
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

    public string GetSemanticLogPath()
    {
        return GetPath(_namingPolicy.GetSemanticLogFileName());
    }

    public void LogCommand(int index, string description)
    {
        _commandLogWriter.WriteLine($"command[{index}]={description}");
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
                failureCategory = result.FailureCategory.ToString(),
                failureMessage = result.FailureMessage,
                durationMilliseconds = result.Duration.TotalMilliseconds
            },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetPath(_namingPolicy.GetResultFileName()), json, Encoding.UTF8);
    }

    public void Dispose()
    {
        _semanticLogWriter.Dispose();
        _commandLogWriter.Dispose();
    }

    public void LogSemanticEntry(string subject, string details)
    {
        if (!string.Equals(_lastSemanticLogSubject, subject, StringComparison.Ordinal))
        {
            _semanticLogWriter.WriteLine(subject);
            _lastSemanticLogSubject = subject;
        }

        if (string.IsNullOrWhiteSpace(details))
        {
            return;
        }

        _semanticLogWriter.WriteLine($"- {details}");
    }

    private StreamWriter CreateWriter(string fileName)
    {
        return new StreamWriter(
            new FileStream(GetPath(fileName), FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
            Encoding.UTF8)
        {
            AutoFlush = true
        };
    }
}
