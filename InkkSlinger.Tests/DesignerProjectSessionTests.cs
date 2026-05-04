using Xunit;

namespace InkkSlinger.Tests;

public class DesignerProjectSessionTests
{
    [Fact]
    public void Open_MaterializesFolderBackedProjectTreeWithSortedChildNodes()
    {
        var store = new FakeProjectFileStore();
        store.CreateDirectory("C:/projects/Sample");
        store.CreateDirectory("C:/projects/Sample/Views");
        store.CreateDirectory("C:/projects/Sample/Assets");
        store.WriteAllText("C:/projects/Sample/Readme.md", "# Sample");
        store.WriteAllText("C:/projects/Sample/App.xml", "<Application />");
        store.WriteAllText("C:/projects/Sample/Views/Main.xml", "<UserControl />");

        var session = InkkSlinger.Designer.DesignerProjectSession.Open("C:/projects/Sample", store);

        Assert.Equal("C:/projects/Sample", session.RootPath);
        Assert.Equal("Sample", session.DisplayName);
        Assert.Equal("Sample", session.RootNode.Name);
        Assert.True(session.RootNode.IsFolder);
        Assert.Equal(
            new[] { "Assets", "Views", "App.xml", "Readme.md" },
            session.RootNode.Children.Select(child => child.Name).ToArray());
        Assert.Equal(
            new[] { true, true, false, false },
            session.RootNode.Children.Select(child => child.IsFolder).ToArray());
    }

    [Fact]
    public void Open_DoesNotEnumerateNestedProjectFoldersUntilChildrenAreRequested()
    {
        var store = new FakeProjectFileStore();
        store.CreateDirectory("C:/projects/Sample");
        for (var folderIndex = 0; folderIndex < 100; folderIndex++)
        {
            store.CreateDirectory($"C:/projects/Sample/Folder{folderIndex:000}");
            for (var fileIndex = 0; fileIndex < 20; fileIndex++)
            {
                store.WriteAllText($"C:/projects/Sample/Folder{folderIndex:000}/File{fileIndex:000}.xml", "<UserControl />");
            }
        }

        var session = InkkSlinger.Designer.DesignerProjectSession.Open("C:/projects/Sample", store);

        Assert.Equal(0, store.EnumerateDirectoriesCallCount);
        Assert.Equal(0, store.EnumerateFilesCallCount);

        var rootChildren = session.RootNode.Children;

        Assert.Equal(100, rootChildren.Count);
        Assert.Equal(1, store.EnumerateDirectoriesCallCount);
        Assert.Equal(1, store.EnumerateFilesCallCount);

        _ = rootChildren[0].Children;

        Assert.Equal(2, store.EnumerateDirectoriesCallCount);
        Assert.Equal(2, store.EnumerateFilesCallCount);
    }

    [Fact]
    public void CreateFileAndCreateFolder_AddNodesAndPersistThroughStore()
    {
        var store = new FakeProjectFileStore();
        store.CreateDirectory("C:/projects/Sample");
        var session = InkkSlinger.Designer.DesignerProjectSession.Open("C:/projects/Sample", store);

        var folder = session.CreateFolder("C:/projects/Sample", "Views");
        var file = session.CreateFile("C:/projects/Sample/Views", "Main.xml", "<UserControl />");

        Assert.Equal("C:/projects/Sample/Views", folder.FullPath);
        Assert.Equal("C:/projects/Sample/Views/Main.xml", file.FullPath);
        Assert.Equal("<UserControl />", store.ReadAllText("C:/projects/Sample/Views/Main.xml"));
        Assert.Equal("Views", session.RootNode.Children.Single().Name);
        Assert.Equal("Main.xml", session.RootNode.Children.Single().Children.Single().Name);
    }

    [Fact]
    public void Rename_UpdatesStoreAndTreeNodePath()
    {
        var store = new FakeProjectFileStore();
        store.CreateDirectory("C:/projects/Sample");
        store.WriteAllText("C:/projects/Sample/Old.xml", "<UserControl />");
        var session = InkkSlinger.Designer.DesignerProjectSession.Open("C:/projects/Sample", store);

        var renamed = session.Rename("C:/projects/Sample/Old.xml", "New.xml");

        Assert.Equal("New.xml", renamed.Name);
        Assert.Equal("C:/projects/Sample/New.xml", renamed.FullPath);
        Assert.False(store.Exists("C:/projects/Sample/Old.xml"));
        Assert.Equal("<UserControl />", store.ReadAllText("C:/projects/Sample/New.xml"));
        Assert.Equal("New.xml", session.RootNode.Children.Single().Name);
    }

    [Fact]
    public void Delete_RemovesFileOrFolderFromStoreAndTree()
    {
        var store = new FakeProjectFileStore();
        store.CreateDirectory("C:/projects/Sample");
        store.CreateDirectory("C:/projects/Sample/Views");
        store.WriteAllText("C:/projects/Sample/Views/Main.xml", "<UserControl />");
        var session = InkkSlinger.Designer.DesignerProjectSession.Open("C:/projects/Sample", store);

        session.Delete("C:/projects/Sample/Views");

        Assert.False(store.Exists("C:/projects/Sample/Views"));
        Assert.False(store.Exists("C:/projects/Sample/Views/Main.xml"));
        Assert.Empty(session.RootNode.Children);
    }

    [Fact]
    public void Refresh_RebuildsTreeAfterExternalStoreChanges()
    {
        var store = new FakeProjectFileStore();
        store.CreateDirectory("C:/projects/Sample");
        store.WriteAllText("C:/projects/Sample/App.xml", "<Application />");
        var session = InkkSlinger.Designer.DesignerProjectSession.Open("C:/projects/Sample", store);

        store.WriteAllText("C:/projects/Sample/Zebra.xml", "<UserControl />");
        store.Delete("C:/projects/Sample/App.xml");

        session.Refresh();

        Assert.Equal(new[] { "Zebra.xml" }, session.RootNode.Children.Select(child => child.Name).ToArray());
    }

    [Fact]
    public void OpenDocument_LoadsProjectFileIntoProvidedDocumentController()
    {
        var store = new FakeProjectFileStore();
        store.CreateDirectory("C:/projects/Sample");
        store.WriteAllText("C:/projects/Sample/Views/Main.xml", "<UserControl>\r\n    <Grid />\r\n</UserControl>");
        var session = InkkSlinger.Designer.DesignerProjectSession.Open("C:/projects/Sample", store);
        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl />", store);

        var opened = session.OpenDocument("C:/projects/Sample/Views/Main.xml", documentController);

        Assert.Equal("Main.xml", opened.Name);
        Assert.Equal("<UserControl>\n    <Grid />\n</UserControl>", documentController.CurrentText);
        Assert.Equal("C:/projects/Sample/Views/Main.xml", documentController.CurrentPath);
        Assert.False(documentController.IsDirty);
    }

    [Theory]
    [InlineData("C:/projects/Sample/App.xml")]
    [InlineData("C:/projects/Sample/App.XML")]
    [InlineData("C:/projects/Sample/View.cs")]
    [InlineData("C:/projects/Sample/Notes.txt")]
    public void IsSupportedDocumentPath_AllowsDesignerTextFileExtensions(string path)
    {
        Assert.True(InkkSlinger.Designer.DesignerProjectSession.IsSupportedDocumentPath(path));
    }

    [Theory]
    [InlineData("C:/projects/Sample/bin/Debug/net9.0/InkkSlinger.DemoApp.dll")]
    [InlineData("C:/projects/Sample/README.md")]
    [InlineData("C:/projects/Sample/image.png")]
    [InlineData("C:/projects/Sample/file")]
    public void IsSupportedDocumentPath_BlocksUnsupportedProjectFileExtensions(string path)
    {
        Assert.False(InkkSlinger.Designer.DesignerProjectSession.IsSupportedDocumentPath(path));
    }

    [Fact]
    public void OpenDocument_UnsupportedExtension_DoesNotLoadProjectFile()
    {
        var store = new FakeProjectFileStore();
        store.CreateDirectory("C:/projects/Sample");
        store.WriteAllText("C:/projects/Sample/bin/App.dll", "binary-looking-text");
        var session = InkkSlinger.Designer.DesignerProjectSession.Open("C:/projects/Sample", store);
        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl />", store);

        var exception = Assert.Throws<NotSupportedException>(() =>
            session.OpenDocument("C:/projects/Sample/bin/App.dll", documentController));

        Assert.Contains(".xml, .cs, and .txt", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(documentController.CurrentPath);
        Assert.Equal("<UserControl />", documentController.CurrentText);
        Assert.False(documentController.IsDirty);
    }

    private sealed class FakeProjectFileStore : InkkSlinger.Designer.IDesignerProjectFileStore, InkkSlinger.Designer.IDesignerDocumentFileStore
    {
        private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> Directories => _directories;

        public IReadOnlyDictionary<string, string> Files => _files;

        public int EnumerateDirectoriesCallCount { get; private set; }

        public int EnumerateFilesCallCount { get; private set; }

        public bool Exists(string path)
        {
            var normalized = NormalizePath(path);
            return _directories.Contains(normalized) || _files.ContainsKey(normalized);
        }

        public bool DirectoryExists(string path)
        {
            return _directories.Contains(NormalizePath(path));
        }

        public bool FileExists(string path)
        {
            return _files.ContainsKey(NormalizePath(path));
        }

        public IReadOnlyList<string> EnumerateDirectories(string path)
        {
            EnumerateDirectoriesCallCount++;
            var prefix = NormalizePath(path) + "/";
            return _directories
                .Where(directory => directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(directory => !directory[prefix.Length..].Contains('/', StringComparison.Ordinal))
                .OrderBy(directory => GetName(directory), StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<string> EnumerateFiles(string path)
        {
            EnumerateFilesCallCount++;
            var prefix = NormalizePath(path) + "/";
            return _files.Keys
                .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(file => !file[prefix.Length..].Contains('/', StringComparison.Ordinal))
                .OrderBy(file => GetName(file), StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public void CreateDirectory(string path)
        {
            _directories.Add(NormalizePath(path));
        }

        public string ReadAllText(string path)
        {
            return _files[NormalizePath(path)];
        }

        public void WriteAllText(string path, string text)
        {
            var normalized = NormalizePath(path);
            var parent = GetParent(normalized);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                _directories.Add(parent);
            }

            _files[normalized] = text;
        }

        public void Rename(string path, string newPath)
        {
            var normalizedPath = NormalizePath(path);
            var normalizedNewPath = NormalizePath(newPath);
            if (_files.Remove(normalizedPath, out var text))
            {
                _files[normalizedNewPath] = text;
                return;
            }

            if (_directories.Remove(normalizedPath))
            {
                _directories.Add(normalizedNewPath);
                var directoryPrefix = normalizedPath + "/";
                foreach (var directory in _directories.Where(directory => directory.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
                {
                    _directories.Remove(directory);
                    _directories.Add(normalizedNewPath + directory[directoryPrefix.Length..].Insert(0, "/"));
                }

                foreach (var file in _files.Keys.Where(file => file.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
                {
                    var value = _files[file];
                    _files.Remove(file);
                    _files[normalizedNewPath + file[directoryPrefix.Length..].Insert(0, "/")] = value;
                }
            }
        }

        public void Delete(string path)
        {
            var normalized = NormalizePath(path);
            _files.Remove(normalized);
            var prefix = normalized + "/";
            foreach (var file in _files.Keys.Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                _files.Remove(file);
            }

            foreach (var directory in _directories.Where(directory => string.Equals(directory, normalized, StringComparison.OrdinalIgnoreCase) || directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                _directories.Remove(directory);
            }
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

        private static string? GetParent(string path)
        {
            var index = NormalizePath(path).LastIndexOf('/');
            return index < 0 ? null : path[..index];
        }
    }
}