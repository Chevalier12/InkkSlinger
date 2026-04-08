using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace InkkSlinger;

public sealed class InkkOopsArtifacts : IDisposable
{
    private readonly IInkkOopsArtifactNamingPolicy _namingPolicy;
    private readonly List<string> _actionLogLines = new();
    private readonly Dictionary<string, string> _bufferedTextArtifacts = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastActionLogSubject;

    public InkkOopsArtifacts(string rootPath, string scriptName)
        : this(rootPath, scriptName, new DefaultInkkOopsArtifactNamingPolicy())
    {
    }

    public InkkOopsArtifacts(string rootPath, string scriptName, IInkkOopsArtifactNamingPolicy namingPolicy)
    {
        _namingPolicy = namingPolicy ?? throw new ArgumentNullException(nameof(namingPolicy));
        DirectoryPath = Path.GetFullPath(Path.Combine(rootPath, _namingPolicy.CreateRunDirectoryName(scriptName, DateTime.UtcNow)));
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public string GetPath(string fileName)
    {
        return Path.Combine(DirectoryPath, fileName);
    }

    public string GetActionLogPath()
    {
        return GetPath(_namingPolicy.GetActionLogFileName());
    }

    public void LogActionEntry(string subject, string details)
    {
        if (!string.Equals(_lastActionLogSubject, subject, StringComparison.Ordinal))
        {
            _actionLogLines.Add(subject);
            _lastActionLogSubject = subject;
        }

        if (string.IsNullOrWhiteSpace(details))
        {
            return;
        }

        _actionLogLines.Add($"- {details}");
    }

    public void BufferTextArtifact(string fileName, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        _bufferedTextArtifacts[fileName] = content ?? string.Empty;
    }

    public int? GetLastLoggedActionIndex()
    {
        for (var i = _actionLogLines.Count - 1; i >= 0; i--)
        {
            var line = _actionLogLines[i];
            var marker = "action[";
            var start = line.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                continue;
            }

            start += marker.Length;
            var end = line.IndexOf(']', start);
            if (end > start && int.TryParse(line.AsSpan(start, end - start), out var index))
            {
                return index;
            }
        }

        return null;
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
        File.WriteAllLines(GetActionLogPath(), _actionLogLines, Encoding.UTF8);
        foreach (var pair in _bufferedTextArtifacts)
        {
            File.WriteAllText(GetPath(pair.Key), pair.Value, Encoding.UTF8);
        }
    }
}
