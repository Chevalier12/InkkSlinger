using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public class DesignerShellProjectExplorerTests
{
    [Fact]
    public void Constructor_WithProjectSession_ExposesRootSelection()
    {
        var harness = CreateHarness();
        var viewModel = harness.CreateShellViewModel();

        Assert.Equal("Sample", viewModel.ProjectDisplayName);
        Assert.Equal("C:/projects/Sample", viewModel.ProjectRootNode?.FullPath);
        Assert.Equal("C:/projects/Sample", viewModel.SelectedProjectNode?.FullPath);
    }

    [Fact]
    public void SelectProjectNode_File_OpensDocumentIntoExistingController()
    {
        var harness = CreateHarness();
        harness.ProjectFiles.WriteAllText("C:/projects/Sample/Views/Main.xml", "<UserControl>\r\n    <Grid />\r\n</UserControl>");
        harness.ProjectSession.Refresh();
        var viewModel = harness.CreateShellViewModel();
        var fileNode = viewModel.ProjectRootNode!.Children.Single(child => child.Name == "Views").Children.Single();

        viewModel.SelectProjectNode(fileNode);

        Assert.Equal("C:/projects/Sample/Views/Main.xml", harness.DocumentController.CurrentPath);
        Assert.Equal("<UserControl>\n    <Grid />\n</UserControl>", viewModel.SourceText);
        Assert.False(harness.DocumentController.IsDirty);
    }

    [Fact]
    public void SelectProjectNode_UnsupportedFileExtension_DoesNotOpenDocument()
    {
        var harness = CreateHarness();
        harness.ProjectFiles.CreateDirectory("C:/projects/Sample/bin");
        harness.ProjectFiles.CreateDirectory("C:/projects/Sample/bin/Debug");
        harness.ProjectFiles.CreateDirectory("C:/projects/Sample/bin/Debug/net9.0");
        harness.ProjectFiles.WriteAllText("C:/projects/Sample/bin/Debug/net9.0/InkkSlinger.DemoApp.dll", "binary-looking-text");
        harness.ProjectSession.Refresh();
        var viewModel = harness.CreateShellViewModel();
        var fileNode = viewModel.ProjectRootNode!
            .Children.Single(child => child.Name == "bin")
            .Children.Single(child => child.Name == "Debug")
            .Children.Single(child => child.Name == "net9.0")
            .Children.Single(child => child.Name == "InkkSlinger.DemoApp.dll");

        viewModel.SelectProjectNode(fileNode);

        Assert.Equal("C:/projects/Sample/bin/Debug/net9.0/InkkSlinger.DemoApp.dll", viewModel.SelectedProjectNode?.FullPath);
        Assert.Null(harness.DocumentController.CurrentPath);
        Assert.Equal("<UserControl />", viewModel.SourceText);
        Assert.False(harness.DocumentController.IsDirty);
        Assert.Contains(".xml, .cs, and .txt", viewModel.DocumentStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectedEditorTabIndex_AppResourcesTab_LoadsProjectAppXmlWhenItExists()
    {
        var harness = CreateHarness();
        harness.ProjectFiles.WriteAllText(
            "C:/projects/Sample/App.xml",
            "<Application>\r\n    <Application.Resources />\r\n</Application>");
        harness.ProjectSession.Refresh();
        var viewModel = harness.CreateShellViewModel();

        viewModel.SelectedEditorTabIndex = 1;

        Assert.Equal(
            "<Application>\n    <Application.Resources />\n</Application>",
            viewModel.AppResourcesText);
    }

    [Fact]
    public void SelectedEditorTabIndex_AppResourcesTab_KeepsCurrentResourcesTextWhenProjectAppXmlIsMissing()
    {
        var harness = CreateHarness();
        var viewModel = harness.CreateShellViewModel();
        viewModel.AppResourcesText = "<Application>\n    <Application.Resources />\n</Application>";

        viewModel.SelectedEditorTabIndex = 1;

        Assert.Equal(
            "<Application>\n    <Application.Resources />\n</Application>",
            viewModel.AppResourcesText);
    }

    [Fact]
    public void ProjectExplorer_UsesPlainHeaders_AndOnlyShowsParentAffordancesForActualChildren()
    {
        var harness = CreateHarness();
        harness.ProjectFiles.CreateDirectory("C:/projects/Sample/Views");
        harness.ProjectFiles.CreateDirectory("C:/projects/Sample/EmptyFolder");
        harness.ProjectFiles.WriteAllText("C:/projects/Sample/Views/Main.xml", "<UserControl />");
        harness.ProjectSession.Refresh();

        var shell = new InkkSlinger.Designer.DesignerShellView(
            documentController: harness.DocumentController,
            projectSession: harness.ProjectSession);
        var uiRoot = new UiRoot(shell);
        RunLayout(uiRoot);
        RunLayout(uiRoot);

        var treeView = Assert.IsType<TreeView>(shell.FindName("ProjectExplorerTree"));
        var rootNode = shell.ViewModel.ProjectRootNode!;
        var viewsNode = rootNode.Children.Single(child => child.Name == "Views");
        var emptyFolderNode = rootNode.Children.Single(child => child.Name == "EmptyFolder");
        var mainFileNode = viewsNode.Children.Single(child => child.Name == "Main.xml");

        Assert.True(treeView.ScrollHierarchicalItemIntoView(mainFileNode));
        RunLayout(uiRoot);

        var viewsItem = treeView.ContainerFromHierarchicalItem(viewsNode);
        var emptyFolderItem = treeView.ContainerFromHierarchicalItem(emptyFolderNode);
        var mainFileItem = treeView.ContainerFromHierarchicalItem(mainFileNode);

        Assert.NotNull(viewsItem);
        Assert.NotNull(emptyFolderItem);
        Assert.NotNull(mainFileItem);

        Assert.Equal("Views", viewsItem!.Header);
        Assert.True(viewsItem.HasChildItems());
        Assert.Equal("EmptyFolder", emptyFolderItem!.Header);
        Assert.False(emptyFolderItem.HasChildItems());
        Assert.Equal("Main.xml", mainFileItem!.Header);
        Assert.False(mainFileItem.HasChildItems());

        var viewsExpander = Assert.IsType<Grid>(FindNamedVisualChild<Grid>(viewsItem, "PART_Expander"));
        var emptyFolderExpander = Assert.IsType<Grid>(FindNamedVisualChild<Grid>(emptyFolderItem, "PART_Expander"));
        var fileExpander = Assert.IsType<Grid>(FindNamedVisualChild<Grid>(mainFileItem, "PART_Expander"));
        var chevronUp = Assert.IsType<Viewbox>(FindNamedVisualChild<Viewbox>(viewsExpander, "ProjectExplorerChevronUp"));
        var chevronDown = Assert.IsType<Viewbox>(FindNamedVisualChild<Viewbox>(viewsExpander, "ProjectExplorerChevronDown"));
        var chevronUpPath = Assert.IsType<PathShape>(FindNamedVisualChild<PathShape>(chevronUp, "ProjectExplorerChevronUpPath"));
        var chevronDownPath = Assert.IsType<PathShape>(FindNamedVisualChild<PathShape>(chevronDown, "ProjectExplorerChevronDownPath"));

        Assert.Equal(10, chevronUp.Width);
        Assert.Equal(10, chevronUp.Height);
        Assert.Equal(10, chevronDown.Width);
        Assert.Equal(10, chevronDown.Height);
        Assert.IsType<PathGeometry>(chevronUpPath.Data);
        Assert.IsType<PathGeometry>(chevronDownPath.Data);
        AssertMatchingFilledCaretGeometry(chevronUpPath, chevronDownPath);
        Assert.Equal(Visibility.Collapsed, chevronUp.Visibility);
        Assert.Equal(Visibility.Visible, chevronDown.Visibility);
        Assert.Equal(Visibility.Collapsed, emptyFolderExpander.Visibility);
        Assert.Equal(Visibility.Collapsed, fileExpander.Visibility);
    }

    private static void AssertMatchingFilledCaretGeometry(PathShape chevronUp, PathShape chevronDown)
    {
        var upBounds = GetSingleClosedFigureBounds(chevronUp);
        var downBounds = GetSingleClosedFigureBounds(chevronDown);

        Assert.Equal(0f, chevronUp.StrokeThickness, precision: 3);
        Assert.Equal(chevronUp.StrokeThickness, chevronDown.StrokeThickness, precision: 3);
        Assert.Equal(255, chevronUp.Fill.A);
        Assert.Equal(chevronUp.Fill.A, chevronDown.Fill.A);
        Assert.Equal(upBounds.Width, downBounds.Width, precision: 3);
        Assert.Equal(upBounds.Height, downBounds.Height, precision: 3);
        Assert.True(upBounds.Width > upBounds.Height);
        Assert.True(upBounds.Width <= 10f);
        Assert.True(upBounds.Height <= 10f);
    }

    private static LayoutRect GetSingleClosedFigureBounds(PathShape chevron)
    {
        var geometry = Assert.IsType<PathGeometry>(chevron.Data);
        var figures = geometry.GetFlattenedFigures();
        var figure = Assert.Single(figures);
        Assert.True(figure.IsClosed);
        Assert.Equal(4, figure.Points.Count);
        Assert.Equal(figure.Points[0], figure.Points[^1]);

        var minX = figure.Points.Min(point => point.X);
        var minY = figure.Points.Min(point => point.Y);
        var maxX = figure.Points.Max(point => point.X);
        var maxY = figure.Points.Max(point => point.Y);
        return new LayoutRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static ShellHarness CreateHarness()
    {
        var projectFiles = new FakeProjectFileStore();
        projectFiles.CreateDirectory("C:/projects/Sample");
        var documentController = new InkkSlinger.Designer.DesignerDocumentController("<UserControl />", projectFiles);
        var projectSession = InkkSlinger.Designer.DesignerProjectSession.Open("C:/projects/Sample", projectFiles);
        return new ShellHarness(projectFiles, documentController, projectSession);
    }

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1280, 820));
    }

    private static TElement? FindNamedVisualChild<TElement>(UIElement root, string name)
        where TElement : FrameworkElement
    {
        if (root is TElement typed && typed.Name == name)
        {
            return typed;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindNamedVisualChild<TElement>(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private sealed record ShellHarness(
        FakeProjectFileStore ProjectFiles,
        InkkSlinger.Designer.DesignerDocumentController DocumentController,
        InkkSlinger.Designer.DesignerProjectSession ProjectSession)
    {
        public InkkSlinger.Designer.DesignerShellViewModel CreateShellViewModel()
        {
            return new InkkSlinger.Designer.DesignerShellViewModel(
                documentController: DocumentController,
                projectSession: ProjectSession);
        }
    }

    private sealed class FakeProjectFileStore : InkkSlinger.Designer.IDesignerProjectFileStore, InkkSlinger.Designer.IDesignerDocumentFileStore
    {
        private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

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
            var prefix = NormalizePath(path) + "/";
            return _directories
                .Where(directory => directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(directory => !directory[prefix.Length..].Contains('/', StringComparison.Ordinal))
                .OrderBy(GetName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<string> EnumerateFiles(string path)
        {
            var prefix = NormalizePath(path) + "/";
            return _files.Keys
                .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(file => !file[prefix.Length..].Contains('/', StringComparison.Ordinal))
                .OrderBy(GetName, StringComparer.OrdinalIgnoreCase)
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

            if (!_directories.Remove(normalizedPath))
            {
                return;
            }

            _directories.Add(normalizedNewPath);
            var directoryPrefix = normalizedPath + "/";
            foreach (var directory in _directories.Where(directory => directory.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                _directories.Remove(directory);
                _directories.Add(normalizedNewPath + "/" + directory[directoryPrefix.Length..]);
            }

            foreach (var file in _files.Keys.Where(file => file.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                var value = _files[file];
                _files.Remove(file);
                _files[normalizedNewPath + "/" + file[directoryPrefix.Length..]] = value;
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
