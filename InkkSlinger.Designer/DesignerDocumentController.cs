using System;
using System.IO;

namespace InkkSlinger.Designer;

public interface IDesignerDocumentFileStore
{
    bool Exists(string path);

    string ReadAllText(string path)
    {
        throw new NotSupportedException("This document file store does not support reading text.");
    }

    void WriteAllText(string path, string text);
}

public sealed class PhysicalDesignerDocumentFileStore : IDesignerDocumentFileStore
{
    public bool Exists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return File.Exists(path);
    }

    public string ReadAllText(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return NormalizeLineEndings(File.ReadAllText(path));
    }

    public void WriteAllText(string path, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directoryPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllText(path, ConvertToPlatformLineEndings(text));
    }

    private static string ConvertToPlatformLineEndings(string? text)
    {
        var normalized = NormalizeLineEndings(text);
        return Environment.NewLine == "\n"
            ? normalized
            : normalized.Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    private static string NormalizeLineEndings(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}

public sealed class DesignerDocumentController
{
    private readonly IDesignerDocumentFileStore _fileStore;

    public DesignerDocumentController(string newDocumentText, IDesignerDocumentFileStore? fileStore = null)
    {
        _fileStore = fileStore ?? new PhysicalDesignerDocumentFileStore();
        CurrentText = NormalizeLineEndings(newDocumentText);
    }

    public string CurrentText { get; private set; }

    public string? CurrentPath { get; private set; }

    public bool IsDirty { get; private set; }

    public string DisplayName => string.IsNullOrWhiteSpace(CurrentPath)
        ? "Untitled.xml"
        : Path.GetFileName(CurrentPath);

    public bool UpdateText(string? text)
    {
        var normalized = NormalizeLineEndings(text);
        if (string.Equals(CurrentText, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        CurrentText = normalized;
        IsDirty = true;
        return true;
    }

    public bool PathExists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return _fileStore.Exists(path);
    }

    public bool Save()
    {
        if (string.IsNullOrWhiteSpace(CurrentPath))
        {
            return false;
        }

        _fileStore.WriteAllText(CurrentPath, CurrentText);
        IsDirty = false;
        return true;
    }

    public void SaveToPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _fileStore.WriteAllText(path, CurrentText);
        CurrentPath = path;
        IsDirty = false;
    }

    public void OpenPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        CurrentText = NormalizeLineEndings(_fileStore.ReadAllText(path));
        CurrentPath = path;
        IsDirty = false;
    }

    public void Reset(string text)
    {
        CurrentText = NormalizeLineEndings(text);
        CurrentPath = null;
        IsDirty = false;
    }

    private static string NormalizeLineEndings(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}