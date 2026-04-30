using Xunit;

namespace InkkSlinger.Tests;

public class DesignerHostViewModelTests
{
    [Fact]
    public void HostStartsOnStartPageWithoutAutoOpeningRecentProject()
    {
        var harness = CreateHarness();
        harness.RecentProjects.AddOrUpdate("C:/projects/Previous", new DateTimeOffset(2026, 4, 27, 9, 0, 0, TimeSpan.Zero));

        var host = harness.CreateHost();

        Assert.Equal(InkkSlinger.Designer.DesignerHostViewState.StartPage, host.CurrentViewState);
        Assert.Null(host.CurrentProjectSession);
        Assert.Single(host.RecentProjects);
    }

    [Fact]
    public void OpenProjectCommand_TransitionsToWorkspaceAndUpdatesRecents()
    {
        var harness = CreateHarness();
        harness.ProjectFiles.CreateDirectory("C:/projects/Sample");
        harness.ProjectFiles.WriteAllText("C:/projects/Sample/App.xml", "<Application />");
        harness.ProjectFiles.WriteAllText("C:/projects/Old/Old.xml", "<UserControl />");
        harness.DocumentController.OpenPath("C:/projects/Old/Old.xml");
        var host = harness.CreateHost();

        host.OpenProjectCommand.Execute("C:/projects/Sample");

        Assert.Equal(InkkSlinger.Designer.DesignerHostViewState.Workspace, host.CurrentViewState);
        Assert.NotNull(host.CurrentProjectSession);
        Assert.Equal("C:/projects/Sample", host.CurrentProjectSession.RootPath);
        var recent = Assert.Single(host.RecentProjects);
        Assert.Equal("C:/projects/Sample", recent.Path);
        Assert.Equal("Sample", recent.DisplayName);
        Assert.Null(harness.DocumentController.CurrentPath);
        Assert.False(harness.DocumentController.IsDirty);
    }

    [Fact]
    public void OpenProjectCommand_UsesBoundPathTextWhenCommandParameterIsEmpty()
    {
        var harness = CreateHarness();
        harness.ProjectFiles.CreateDirectory("C:/projects/Sample");
        var host = harness.CreateHost();
        host.OpenProjectPathText = "C:/projects/Sample";

        host.OpenProjectCommand.Execute(null);

        Assert.Equal(InkkSlinger.Designer.DesignerHostViewState.Workspace, host.CurrentViewState);
        Assert.Equal("C:/projects/Sample", host.CurrentProjectSession?.RootPath);
    }

    [Fact]
    public void BackToStartCommand_ReturnsToStartPageWithoutAutoOpeningProject()
    {
        var harness = CreateHarness();
        harness.ProjectFiles.CreateDirectory("C:/projects/Sample");
        harness.ProjectFiles.WriteAllText("C:/projects/Sample/App.xml", "<Application />");
        var host = harness.CreateHost();
        host.OpenProjectCommand.Execute("C:/projects/Sample");

        host.BackToStartCommand.Execute(null);

        Assert.Equal(InkkSlinger.Designer.DesignerHostViewState.StartPage, host.CurrentViewState);
        Assert.Null(host.CurrentProjectSession);
        Assert.Equal("C:/projects/Sample", Assert.Single(host.RecentProjects).Path);
        Assert.Null(harness.DocumentController.CurrentPath);
    }

    [Fact]
    public void RemoveRecentProjectCommand_RemovesProjectFromRecentsWithoutLeavingStartPage()
    {
        var harness = CreateHarness();
        harness.RecentProjects.AddOrUpdate("C:/projects/Alpha", new DateTimeOffset(2026, 4, 27, 9, 0, 0, TimeSpan.Zero));
        harness.RecentProjects.AddOrUpdate("C:/projects/Beta", new DateTimeOffset(2026, 4, 27, 10, 0, 0, TimeSpan.Zero));
        var host = harness.CreateHost();

        host.RemoveRecentProjectCommand.Execute("C:/projects/Alpha");

        Assert.Equal(InkkSlinger.Designer.DesignerHostViewState.StartPage, host.CurrentViewState);
        var recent = Assert.Single(host.RecentProjects);
        Assert.Equal("C:/projects/Beta", recent.Path);
    }

    [Fact]
    public void OpenProjectCommand_WhenNoPathAndNoParameter_ShouldBeEnabledForFolderDialog()
    {
        var harness = CreateHarness();
        var host = harness.CreateHost();

        var canExecute = host.OpenProjectCommand.CanExecute(null);

        Assert.True(canExecute,
            "The Open button should be enabled (CanExecute(null) == true) even when " +
            "OpenProjectPathText is empty, so the native folder dialog can be reached.");
    }

    [Fact]
    public void RecentProjectsSearch_WhenFilterTextDoesNotMatchAnyDisplayName_ShouldFilterToEmpty()
    {
        var harness = CreateHarness();
        harness.RecentProjects.AddOrUpdate("C:/projects/InkkSlinger", new DateTimeOffset(2026, 4, 28, 9, 0, 0, TimeSpan.Zero));
        harness.RecentProjects.AddOrUpdate("C:/projects/OtherProject", new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero));
        var host = harness.CreateHost();

        host.OpenProjectPathText = "raf";

        Assert.Empty(host.RecentProjects);
    }

    [Fact]
    public void RecentProjectsSearch_WhenFilterTextMatchesPartialDisplayName_ShouldShowOnlyMatchingProjects()
    {
        var harness = CreateHarness();
        harness.RecentProjects.AddOrUpdate("C:/projects/InkkSlinger", new DateTimeOffset(2026, 4, 28, 9, 0, 0, TimeSpan.Zero));
        harness.RecentProjects.AddOrUpdate("C:/projects/OtherProject", new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero));
        var host = harness.CreateHost();

        host.OpenProjectPathText = "Inkk";

        var recent = Assert.Single(host.RecentProjects);
        Assert.Equal("C:/projects/InkkSlinger", recent.Path);
    }

    [Fact]
    public void RecentProjectsSearch_WhenFilterTextIsCleared_ShouldRestoreAllProjects()
    {
        var harness = CreateHarness();
        harness.RecentProjects.AddOrUpdate("C:/projects/InkkSlinger", new DateTimeOffset(2026, 4, 28, 9, 0, 0, TimeSpan.Zero));
        harness.RecentProjects.AddOrUpdate("C:/projects/OtherProject", new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero));
        var host = harness.CreateHost();

        host.OpenProjectPathText = "Inkk";
        Assert.Single(host.RecentProjects);

        host.OpenProjectPathText = "";

        Assert.Equal(2, host.RecentProjects.Count);
    }

    [Fact]
    public void FolderBrowserRefresh_ListsChildFoldersForCurrentPath()
    {
        var harness = CreateHarness();
        harness.ProjectFiles.CreateDirectory("C:/projects");
        harness.ProjectFiles.CreateDirectory("C:/projects/Zebra");
        harness.ProjectFiles.CreateDirectory("C:/projects/Alpha");
        harness.ProjectFiles.CreateDirectory("C:/projects/Alpha/Nested");
        var host = harness.CreateHost();

        host.FolderBrowserPathText = "C:/projects";
        host.RefreshFolderBrowserCommand.Execute(null);

        Assert.Equal("C:/projects", host.FolderBrowserPathText);
        Assert.Equal(new[] { "Alpha", "Zebra" }, host.FolderBrowserFolders.Select(folder => folder.Name).ToArray());
        Assert.Equal(new[] { "C:/projects/Alpha", "C:/projects/Zebra" }, host.FolderBrowserFolders.Select(folder => folder.Path).ToArray());
    }

    [Fact]
    public void FolderBrowserNavigateAndUp_UpdateCurrentPathAndFolders()
    {
        var harness = CreateHarness();
        harness.ProjectFiles.CreateDirectory("C:/projects");
        harness.ProjectFiles.CreateDirectory("C:/projects/Alpha");
        harness.ProjectFiles.CreateDirectory("C:/projects/Alpha/Nested");
        var host = harness.CreateHost();
        host.FolderBrowserPathText = "C:/projects";
        host.RefreshFolderBrowserCommand.Execute(null);
        var alpha = Assert.Single(host.FolderBrowserFolders);

        host.NavigateFolderBrowserCommand.Execute(alpha);

        Assert.Equal("C:/projects/Alpha", host.FolderBrowserPathText);
        Assert.Equal("Nested", Assert.Single(host.FolderBrowserFolders).Name);

        host.NavigateFolderBrowserUpCommand.Execute(null);

        Assert.Equal("C:/projects", host.FolderBrowserPathText);
        Assert.Equal("Alpha", Assert.Single(host.FolderBrowserFolders).Name);
    }

    [Fact]
    public void FolderBrowserUsePathCommands_CopyCurrentPathToOpenText()
    {
        var harness = CreateHarness();
        harness.ProjectFiles.CreateDirectory("C:/projects");
        var host = harness.CreateHost();
        host.FolderBrowserPathText = "C:/projects";

        host.UseFolderBrowserPathForOpenCommand.Execute(null);

        Assert.Equal("C:/projects", host.OpenProjectPathText);
    }

    private static HostHarness CreateHarness()
    {
        var projectFiles = new FakeProjectFileStore();
        var recentPersistence = new FakeRecentProjectPersistenceStore();
        var recentProjects = new InkkSlinger.Designer.DesignerRecentProjectStore(recentPersistence);
        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl />", projectFiles);
        return new HostHarness(projectFiles, recentProjects, documentController);
    }

    private sealed record HostHarness(
        FakeProjectFileStore ProjectFiles,
        InkkSlinger.Designer.DesignerRecentProjectStore RecentProjects,
        InkkSlinger.Designer.DesignerDocumentController DocumentController)
    {
        public InkkSlinger.Designer.DesignerHostViewModel CreateHost()
        {
            return new InkkSlinger.Designer.DesignerHostViewModel(ProjectFiles, RecentProjects, DocumentController);
        }
    }

    private sealed class FakeRecentProjectPersistenceStore : InkkSlinger.Designer.IDesignerRecentProjectPersistenceStore
    {
        public string? PersistedText { get; set; }

        public string? ReadAllText()
        {
            return PersistedText;
        }

        public void WriteAllText(string text)
        {
            PersistedText = text;
        }
    }

    private sealed class FakeProjectFileStore : InkkSlinger.Designer.IDesignerProjectFileStore, InkkSlinger.Designer.IDesignerDocumentFileStore
    {
        private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

        public bool Exists(string path)
        {
            return DirectoryExists(path) || FileExists(path);
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
            var prefix = NormalizePath(path) + "/";
            return _directories
                .Where(directory => directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(directory => !directory[prefix.Length..].Contains('/', StringComparison.Ordinal))
                .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<string> EnumerateFiles(string path)
        {
            var prefix = NormalizePath(path) + "/";
            return _files.Keys
                .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(file => !file[prefix.Length..].Contains('/', StringComparison.Ordinal))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
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
            }
        }

        public void Delete(string path)
        {
            var normalized = NormalizePath(path);
            _files.Remove(normalized);
            _directories.Remove(normalized);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }

        private static string? GetParent(string path)
        {
            var index = NormalizePath(path).LastIndexOf('/');
            return index < 0 ? null : path[..index];
        }
    }
}