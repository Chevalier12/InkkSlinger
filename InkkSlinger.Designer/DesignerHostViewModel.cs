using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using InkkSlinger;
using Microsoft.Xna.Framework;

namespace InkkSlinger.Designer;

public enum DesignerHostViewState
{
    StartPage,
    Workspace
}

public sealed record DesignerFolderBrowserItem(string Name, string Path);

public sealed class DesignerHostViewModel : INotifyPropertyChanged
{
    private const string DefaultWorkspaceDocumentText = "<UserControl />";

    private readonly IDesignerProjectFileStore _projectFileStore;
    private readonly DesignerRecentProjectStore _recentProjectStore;
    private readonly DesignerDocumentController _documentController;
    private DesignerHostViewState _currentViewState = DesignerHostViewState.StartPage;
    private DesignerProjectSession? _currentProjectSession;
    private IReadOnlyList<DesignerRecentProject> _recentProjects;
    private IReadOnlyList<DesignerFolderBrowserItem> _folderBrowserFolders = Array.Empty<DesignerFolderBrowserItem>();
    private string _openProjectPathText = string.Empty;
    private string _folderBrowserPathText = string.Empty;
    private string _statusText = "Open or create a project folder to begin.";
    private string _folderBrowserStatusText = "Choose a folder to browse.";
    private Color _statusForeground = new(141, 161, 181);
    private Color _folderBrowserStatusForeground = new(141, 161, 181);

    public DesignerHostViewModel(
        IDesignerProjectFileStore projectFileStore,
        DesignerRecentProjectStore recentProjectStore,
        DesignerDocumentController documentController)
    {
        _projectFileStore = projectFileStore ?? throw new ArgumentNullException(nameof(projectFileStore));
        _recentProjectStore = recentProjectStore ?? throw new ArgumentNullException(nameof(recentProjectStore));
        _documentController = documentController ?? throw new ArgumentNullException(nameof(documentController));
        OpenProjectCommand = new RelayCommand(ExecuteOpenProject, CanOpenProject);
        BackToStartCommand = new RelayCommand(_ => ExecuteBackToStart());
        RemoveRecentProjectCommand = new RelayCommand(ExecuteRemoveRecentProject, CanRemoveRecentProject);
        RefreshFolderBrowserCommand = new RelayCommand(_ => RefreshFolderBrowser(), _ => !string.IsNullOrWhiteSpace(FolderBrowserPathText));
        NavigateFolderBrowserCommand = new RelayCommand(ExecuteNavigateFolderBrowser, CanNavigateFolderBrowser);
        NavigateFolderBrowserUpCommand = new RelayCommand(_ => ExecuteNavigateFolderBrowserUp(), _ => !string.IsNullOrWhiteSpace(GetFolderBrowserParentPath()));
        UseFolderBrowserPathForOpenCommand = new RelayCommand(_ => OpenProjectPathText = FolderBrowserPathText, _ => !string.IsNullOrWhiteSpace(FolderBrowserPathText));
        MinimizeWindowCommand = new RelayCommand(_ => ExecuteMinimizeWindow());
        MaximizeWindowCommand = new RelayCommand(_ => ExecuteMaximizeWindow());
        CloseWindowCommand = new RelayCommand(_ => ExecuteCloseWindow());

        FolderBrowserPathText = GetDefaultFolderBrowserPath();
        RefreshFolderBrowser();
        ApplyRecentProjectsFilter();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RelayCommand OpenProjectCommand { get; }

    public RelayCommand BackToStartCommand { get; }

    public RelayCommand RemoveRecentProjectCommand { get; }

    public RelayCommand RefreshFolderBrowserCommand { get; }

    public RelayCommand NavigateFolderBrowserCommand { get; }

    public RelayCommand NavigateFolderBrowserUpCommand { get; }

    public RelayCommand UseFolderBrowserPathForOpenCommand { get; }

    public RelayCommand MinimizeWindowCommand { get; }

    public RelayCommand MaximizeWindowCommand { get; }

    public RelayCommand CloseWindowCommand { get; }

    public DesignerDocumentController DocumentController => _documentController;

    public DesignerHostViewState CurrentViewState
    {
        get => _currentViewState;
        private set
        {
            if (SetField(ref _currentViewState, value))
            {
                OnPropertyChanged(nameof(StartPageVisibility));
                OnPropertyChanged(nameof(WorkspaceVisibility));
            }
        }
    }

    public Visibility StartPageVisibility => CurrentViewState == DesignerHostViewState.StartPage
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility WorkspaceVisibility => CurrentViewState == DesignerHostViewState.Workspace
        ? Visibility.Visible
        : Visibility.Collapsed;

    public DesignerProjectSession? CurrentProjectSession
    {
        get => _currentProjectSession;
        private set => SetField(ref _currentProjectSession, value);
    }

    public IReadOnlyList<DesignerRecentProject> RecentProjects
    {
        get => _recentProjects;
        private set => SetField(ref _recentProjects, value);
    }

    public IReadOnlyList<DesignerFolderBrowserItem> FolderBrowserFolders
    {
        get => _folderBrowserFolders;
        private set => SetField(ref _folderBrowserFolders, value);
    }

    public string FolderBrowserPathText
    {
        get => _folderBrowserPathText;
        set
        {
            if (SetField(ref _folderBrowserPathText, value ?? string.Empty))
            {
                RefreshFolderBrowserCommand.RaiseCanExecuteChanged();
                NavigateFolderBrowserUpCommand.RaiseCanExecuteChanged();
                UseFolderBrowserPathForOpenCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string OpenProjectPathText
    {
        get => _openProjectPathText;
        set
        {
            if (SetField(ref _openProjectPathText, value ?? string.Empty))
            {
                OpenProjectCommand.RaiseCanExecuteChanged();
                ApplyRecentProjectsFilter();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public Color StatusForeground
    {
        get => _statusForeground;
        private set => SetField(ref _statusForeground, value);
    }

    public string FolderBrowserStatusText
    {
        get => _folderBrowserStatusText;
        private set => SetField(ref _folderBrowserStatusText, value);
    }

    public Color FolderBrowserStatusForeground
    {
        get => _folderBrowserStatusForeground;
        private set => SetField(ref _folderBrowserStatusForeground, value);
    }

    private void ExecuteOpenProject(object? parameter)
    {
        var path = GetPathParameter(parameter) ?? OpenProjectPathText;

        // If no path was provided through the parameter (recent project click) or
        // the search textbox, show a native folder picker dialog on Windows.
        if (string.IsNullOrWhiteSpace(path) && OperatingSystem.IsWindows())
        {
            path = NativeFolderBrowserHelper.BrowseForFolder("Select a project folder to open.");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (!_projectFileStore.DirectoryExists(path))
            {
                throw new InvalidOperationException("Project folder does not exist.");
            }

            CurrentProjectSession = DesignerProjectSession.Open(path, _projectFileStore);
            _documentController.Reset(DefaultWorkspaceDocumentText);
            CurrentViewState = DesignerHostViewState.Workspace;
            AddRecent(CurrentProjectSession.RootPath);
            SetStatus($"Opened {CurrentProjectSession.DisplayName}.", success: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.IO.IOException or UnauthorizedAccessException)
        {
            SetStatus(ex.Message, success: false);
        }
    }

    private void ExecuteBackToStart()
    {
        CurrentProjectSession = null;
        CurrentViewState = DesignerHostViewState.StartPage;
        ApplyRecentProjectsFilter();
        SetStatus("Open or create a project folder to begin.", success: null);
    }

    private void ExecuteRemoveRecentProject(object? parameter)
    {
        var path = GetPathParameter(parameter);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _recentProjectStore.Remove(path);
        ApplyRecentProjectsFilter();
        SetStatus($"Removed {GetFolderDisplayName(path)} from recent projects.", success: null);
    }

    private static void ExecuteMinimizeWindow()
    {
        if (UiApplication.Current.HasMainWindow && OperatingSystem.IsWindows())
        {
            Application.Current.MainWindow.Minimize();
        }
    }

    private static void ExecuteMaximizeWindow()
    {
        if (UiApplication.Current.HasMainWindow && OperatingSystem.IsWindows())
        {
            Application.Current.MainWindow.Maximize();
        }
    }

    private static void ExecuteCloseWindow()
    {
        if (UiApplication.Current.HasMainWindow)
        {
            Application.Current.Shutdown();
        }
    }

    private void RefreshFolderBrowser()
    {
        var path = FolderBrowserPathText;
        if (string.IsNullOrWhiteSpace(path))
        {
            FolderBrowserFolders = Array.Empty<DesignerFolderBrowserItem>();
            SetFolderBrowserStatus("Choose a folder to browse.", success: null);
            return;
        }

        try
        {
            if (!_projectFileStore.DirectoryExists(path))
            {
                FolderBrowserFolders = Array.Empty<DesignerFolderBrowserItem>();
                SetFolderBrowserStatus("Folder does not exist.", success: false);
                return;
            }

            FolderBrowserFolders = _projectFileStore.EnumerateDirectories(path)
                .Select(directory => new DesignerFolderBrowserItem(GetFolderDisplayName(directory), directory))
                .ToArray();
            SetFolderBrowserStatus(FolderBrowserFolders.Count == 0 ? "No child folders." : $"{FolderBrowserFolders.Count} folder(s).", success: null);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            FolderBrowserFolders = Array.Empty<DesignerFolderBrowserItem>();
            SetFolderBrowserStatus(ex.Message, success: false);
        }
    }

    private void ExecuteNavigateFolderBrowser(object? parameter)
    {
        var path = parameter switch
        {
            DesignerFolderBrowserItem item => item.Path,
            string text => text,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        FolderBrowserPathText = path;
        RefreshFolderBrowser();
    }

    private void ExecuteNavigateFolderBrowserUp()
    {
        var parent = GetFolderBrowserParentPath();
        if (string.IsNullOrWhiteSpace(parent))
        {
            return;
        }

        FolderBrowserPathText = parent;
        RefreshFolderBrowser();
    }

    private bool CanNavigateFolderBrowser(object? parameter)
    {
        return parameter is DesignerFolderBrowserItem item && !string.IsNullOrWhiteSpace(item.Path)
            || parameter is string text && !string.IsNullOrWhiteSpace(text);
    }

    private void AddRecent(string path)
    {
        _recentProjectStore.AddOrUpdate(path, DateTimeOffset.UtcNow);
        ApplyRecentProjectsFilter();
    }

    private void ApplyRecentProjectsFilter()
    {
        var all = _recentProjectStore.Load();
        var filter = _openProjectPathText?.Trim() ?? string.Empty;
        RecentProjects = string.IsNullOrWhiteSpace(filter)
            ? all
            : all.Where(p => p.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private static bool CanRemoveRecentProject(object? parameter)
    {
        return !string.IsNullOrWhiteSpace(GetPathParameter(parameter));
    }

    private bool CanOpenProject(object? parameter)
    {
        // parameter is non-null when invoked from a recent-project button (path as string).
        // When null, the "Open" button itself was clicked — always enabled so it can
        // fall through to the native folder dialog when no path is typed or selected.
        return parameter != null
            ? !string.IsNullOrWhiteSpace(GetPathParameter(parameter))
            : true;
    }

    private static string? GetPathParameter(object? parameter)
    {
        return parameter as string;
    }

    private string? GetFolderBrowserParentPath()
    {
        var originalPath = FolderBrowserPathText;
        var path = originalPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var parent = Path.GetDirectoryName(path);
        return originalPath.Contains('/', StringComparison.Ordinal)
            ? parent?.Replace('\\', '/')
            : parent;
    }

    private static string GetDefaultFolderBrowserPath()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documents))
        {
            return documents;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile) ? Environment.CurrentDirectory : userProfile;
    }

    private static string GetFolderDisplayName(string path)
    {
        var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(normalized);
        return string.IsNullOrWhiteSpace(name) ? normalized : name;
    }

    private void SetStatus(string message, bool? success)
    {
        StatusText = message;
        StatusForeground = success switch
        {
            true => new Color(143, 210, 179),
            false => new Color(255, 164, 128),
            _ => new Color(141, 161, 181)
        };
    }

    private void SetFolderBrowserStatus(string message, bool? success)
    {
        FolderBrowserStatusText = message;
        FolderBrowserStatusForeground = success switch
        {
            true => new Color(143, 210, 179),
            false => new Color(255, 164, 128),
            _ => new Color(141, 161, 181)
        };
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}