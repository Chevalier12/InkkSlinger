using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace InkkSlinger.Designer;

public interface IDesignerRecentProjectPersistenceStore
{
    string? ReadAllText();

    void WriteAllText(string text);
}

public sealed class PhysicalDesignerRecentProjectPersistenceStore : IDesignerRecentProjectPersistenceStore
{
    private readonly string _path;

    public PhysicalDesignerRecentProjectPersistenceStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public string? ReadAllText()
    {
        return File.Exists(_path) ? File.ReadAllText(_path) : null;
    }

    public void WriteAllText(string text)
    {
        var directoryPath = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllText(_path, text);
    }
}

public sealed record DesignerRecentProject(string Path, string DisplayName, DateTimeOffset LastOpenedAt)
{
    public string DisplayInitial => string.IsNullOrWhiteSpace(DisplayName)
        ? "?"
        : DisplayName.Trim()[0].ToString().ToUpperInvariant();
}

public sealed class DesignerRecentProjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IDesignerRecentProjectPersistenceStore _persistenceStore;

    public DesignerRecentProjectStore(IDesignerRecentProjectPersistenceStore persistenceStore)
    {
        _persistenceStore = persistenceStore ?? throw new ArgumentNullException(nameof(persistenceStore));
    }

    public IReadOnlyList<DesignerRecentProject> Load()
    {
        var text = _persistenceStore.ReadAllText();
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<DesignerRecentProject>();
        }

        try
        {
            var projects = JsonSerializer.Deserialize<List<DesignerRecentProject>>(text, JsonOptions);
            if (projects == null)
            {
                return Array.Empty<DesignerRecentProject>();
            }

            return projects
                .Where(project => !string.IsNullOrWhiteSpace(project.Path))
                .Select(project => CreateRecentProject(project.Path, project.LastOpenedAt))
                .OrderByDescending(project => project.LastOpenedAt)
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<DesignerRecentProject>();
        }
        catch (NotSupportedException)
        {
            return Array.Empty<DesignerRecentProject>();
        }
    }

    public void AddOrUpdate(string path, DateTimeOffset lastOpenedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalizedPath = NormalizePath(path);
        var projects = Load()
            .Where(project => !string.Equals(NormalizePath(project.Path), normalizedPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        projects.Add(CreateRecentProject(normalizedPath, lastOpenedAt));
        var ordered = projects
            .OrderByDescending(project => project.LastOpenedAt)
            .ToArray();
        _persistenceStore.WriteAllText(JsonSerializer.Serialize(ordered, JsonOptions));
    }

    public void Remove(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalizedPath = NormalizePath(path);
        var remaining = Load()
            .Where(project => !string.Equals(NormalizePath(project.Path), normalizedPath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(project => project.LastOpenedAt)
            .ToArray();
        _persistenceStore.WriteAllText(JsonSerializer.Serialize(remaining, JsonOptions));
    }

    private static DesignerRecentProject CreateRecentProject(string path, DateTimeOffset lastOpenedAt)
    {
        var normalizedPath = NormalizePath(path);
        return new DesignerRecentProject(normalizedPath, GetName(normalizedPath), lastOpenedAt);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }

    private static string GetName(string path)
    {
        var normalized = NormalizePath(path);
        var index = normalized.LastIndexOf('/');
        var name = index < 0 ? normalized : normalized[(index + 1)..];
        return string.Equals(Path.GetExtension(name), ".sln", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(name)
            : name;
    }
}