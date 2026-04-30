using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InkkSlinger.Designer;

public sealed class DesignerProjectNode
{
    public DesignerProjectNode(string name, string fullPath, bool isFolder, IReadOnlyList<DesignerProjectNode>? children = null)
    {
        Name = name;
        FullPath = fullPath;
        IsFolder = isFolder;
        Children = children ?? Array.Empty<DesignerProjectNode>();
    }

    public string Name { get; }

    public string FullPath { get; }

    public bool IsFolder { get; }

    public IReadOnlyList<DesignerProjectNode> Children { get; }
}

public sealed class DesignerProjectSession
{
    private readonly IDesignerProjectFileStore _fileStore;

    private DesignerProjectSession(string rootPath, IDesignerProjectFileStore fileStore)
    {
        RootPath = NormalizePath(rootPath);
        _fileStore = fileStore;
        RootNode = BuildNode(RootPath, isFolder: true);
    }

    public string RootPath { get; }

    public string DisplayName => GetName(RootPath);

    public DesignerProjectNode RootNode { get; private set; }

    public static DesignerProjectSession Open(string rootPath, IDesignerProjectFileStore fileStore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(fileStore);
        return new DesignerProjectSession(rootPath, fileStore);
    }

    public void Refresh()
    {
        RootNode = BuildNode(RootPath, isFolder: true);
    }

    public DesignerProjectNode CreateFile(string parentFolderPath, string name, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentFolderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var path = CombinePath(parentFolderPath, name);
        _fileStore.WriteAllText(path, text);
        Refresh();
        return FindNode(path) ?? new DesignerProjectNode(GetName(path), path, isFolder: false);
    }

    public DesignerProjectNode CreateFolder(string parentFolderPath, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentFolderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var path = CombinePath(parentFolderPath, name);
        _fileStore.CreateDirectory(path);
        Refresh();
        return FindNode(path) ?? new DesignerProjectNode(GetName(path), path, isFolder: true);
    }

    public DesignerProjectNode Rename(string path, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        var normalizedPath = NormalizePath(path);
        var parentPath = GetParentPath(normalizedPath)
            ?? throw new InvalidOperationException("Cannot rename a rootless project item.");
        var newPath = CombinePath(parentPath, newName);
        _fileStore.Rename(normalizedPath, newPath);
        Refresh();
        return FindNode(newPath) ?? new DesignerProjectNode(GetName(newPath), newPath, _fileStore.DirectoryExists(newPath));
    }

    public void Delete(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _fileStore.Delete(NormalizePath(path));
        Refresh();
    }

    public DesignerProjectNode OpenDocument(string path, DesignerDocumentController documentController)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(documentController);
        var normalizedPath = NormalizePath(path);
        documentController.OpenPath(normalizedPath);
        return FindNode(normalizedPath) ?? new DesignerProjectNode(GetName(normalizedPath), normalizedPath, isFolder: false);
    }

    private DesignerProjectNode BuildNode(string path, bool isFolder)
    {
        var normalizedPath = NormalizePath(path);
        if (!isFolder)
        {
            return new DesignerProjectNode(GetName(normalizedPath), normalizedPath, isFolder: false);
        }

        var directories = _fileStore.EnumerateDirectories(normalizedPath)
            .Select(directory => BuildNode(directory, isFolder: true));
        var files = _fileStore.EnumerateFiles(normalizedPath)
            .Select(file => BuildNode(file, isFolder: false));
        return new DesignerProjectNode(
            GetName(normalizedPath),
            normalizedPath,
            isFolder: true,
            directories.Concat(files).ToArray());
    }

    private DesignerProjectNode? FindNode(string path)
    {
        var normalizedPath = NormalizePath(path);
        return FindNode(RootNode, normalizedPath);
    }

    private static DesignerProjectNode? FindNode(DesignerProjectNode node, string path)
    {
        if (string.Equals(NormalizePath(node.FullPath), path, StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var match = FindNode(child, path);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static string CombinePath(string parentPath, string name)
    {
        return NormalizePath(parentPath) + "/" + name.Trim().Trim('/', '\\');
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }

    private static string GetName(string path)
    {
        var normalized = NormalizePath(path);
        var index = normalized.LastIndexOf('/');
        return index < 0 ? normalized : normalized[(index + 1)..];
    }

    private static string? GetParentPath(string path)
    {
        var normalized = NormalizePath(path);
        var index = normalized.LastIndexOf('/');
        return index < 0 ? null : normalized[..index];
    }
}