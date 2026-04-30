using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO;

namespace InkkSlinger.Designer;

public interface IDesignerProjectFileStore
{
    bool Exists(string path);

    bool DirectoryExists(string path);

    bool FileExists(string path);

    IReadOnlyList<string> EnumerateDirectories(string path);

    IReadOnlyList<string> EnumerateFiles(string path);

    void CreateDirectory(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string text);

    void Rename(string path, string newPath);

    void Delete(string path);
}

public interface IDesignerProjectRecycleBin
{
    bool TryRecycleFile(string path);

    bool TryRecycleDirectory(string path);
}

public sealed class WindowsDesignerProjectRecycleBin : IDesignerProjectRecycleBin
{
    public bool TryRecycleFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool TryRecycleDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public sealed class PhysicalDesignerProjectFileStore : IDesignerProjectFileStore, IDesignerDocumentFileStore
{
    private readonly IDesignerProjectRecycleBin _recycleBin;

    public PhysicalDesignerProjectFileStore()
        : this(new WindowsDesignerProjectRecycleBin())
    {
    }

    public PhysicalDesignerProjectFileStore(IDesignerProjectRecycleBin recycleBin)
    {
        _recycleBin = recycleBin ?? throw new ArgumentNullException(nameof(recycleBin));
    }

    public bool Exists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Directory.Exists(path) || File.Exists(path);
    }

    public bool DirectoryExists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Directory.Exists(path);
    }

    public bool FileExists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return File.Exists(path);
    }

    public IReadOnlyList<string> EnumerateDirectories(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Directory.Exists(path))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateDirectories(path)
            .OrderBy(directory => Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> EnumerateFiles(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Directory.Exists(path))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(path)
            .OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void CreateDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(path);
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

    public void Rename(string path, string newPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);
        if (File.Exists(path))
        {
            var directoryPath = Path.GetDirectoryName(newPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.Move(path, newPath, overwrite: false);
            return;
        }

        Directory.Move(path, newPath);
    }

    public void Delete(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (File.Exists(path))
        {
            if (_recycleBin.TryRecycleFile(path))
            {
                return;
            }

            File.Delete(path);
            return;
        }

        if (Directory.Exists(path))
        {
            if (_recycleBin.TryRecycleDirectory(path))
            {
                return;
            }

            Directory.Delete(path, recursive: true);
        }
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