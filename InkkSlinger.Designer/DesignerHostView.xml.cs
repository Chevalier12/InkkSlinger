using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using InkkSlinger;
using Microsoft.Xna.Framework;

namespace InkkSlinger.Designer;

public partial class DesignerHostView : UserControl
{
    private static readonly Color RecentProjectHoverBackground = new(36, 39, 44);
    private static readonly Color RecentProjectHoverBorderBrush = new(60, 65, 74);
    private readonly DesignerHostViewModel _viewModel;
    private readonly Func<bool>? _beginWindowDragMoveOverride;
    private readonly HashSet<Button> _wiredRecentProjectRemoveButtons = new();
    private DesignerProjectSession? _workspaceSession;

    public DesignerHostView(DesignerHostViewModel? viewModel = null, Func<bool>? beginWindowDragMoveOverride = null)
    {
        InitializeComponent();
        _viewModel = viewModel ?? CreateDefaultViewModel();
        _beginWindowDragMoveOverride = beginWindowDragMoveOverride;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DesignerHostRoot.AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonDownEvent, OnRecentProjectRemoveButtonPreviewMouseLeftButtonDown, handledEventsToo: true);
        DesignerHostRoot.AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonUpEvent, OnRecentProjectRemoveButtonPreviewMouseLeftButtonUp, handledEventsToo: true);
        DesignerHostRoot.AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonDownEvent, OnDesignerTitleBarMouseLeftButtonDown, handledEventsToo: true);
        DesignerHostRoot.AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseMoveEvent, OnDesignerHostRootPreviewMouseMove, handledEventsToo: true);
        DesignerHostRoot.AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeaveEvent, OnDesignerHostRootMouseLeave, handledEventsToo: true);
        DesignerTitleBar.AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeftButtonDownEvent, OnDesignerTitleBarMouseLeftButtonDown, handledEventsToo: true);
        UpdateSearchProjectPlaceholderVisibility();
        UpdateRecentProjectRemoveButtons(null);
        SyncWorkspaceView();
    }

    public DesignerHostViewModel ViewModel => _viewModel;

    private static DesignerHostViewModel CreateDefaultViewModel()
    {
        var fileStore = new PhysicalDesignerProjectFileStore();
        var recentStore = new DesignerRecentProjectStore(
            new PhysicalDesignerRecentProjectPersistenceStore(GetRecentProjectsPath()));
        return new DesignerHostViewModel(
            fileStore,
            recentStore,
            new DesignerDocumentController("<UserControl />", fileStore));
    }

    private static string GetRecentProjectsPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "InkkSlinger", "Designer", "recent-projects.json");
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(DesignerHostViewModel.CurrentProjectSession) or nameof(DesignerHostViewModel.CurrentViewState))
        {
            SyncWorkspaceView();
        }

        if (args.PropertyName == nameof(DesignerHostViewModel.OpenProjectPathText))
        {
            UpdateSearchProjectPlaceholderVisibility();
        }

        if (args.PropertyName == nameof(DesignerHostViewModel.RecentProjects))
        {
            UpdateRecentProjectRemoveButtons(null);
        }
    }

    private void SyncWorkspaceView()
    {
        if (_viewModel.CurrentViewState != DesignerHostViewState.Workspace || _viewModel.CurrentProjectSession == null)
        {
            WorkspaceHost.Content = null;
            _workspaceSession = null;
            return;
        }

        if (ReferenceEquals(_workspaceSession, _viewModel.CurrentProjectSession) && WorkspaceHost.Content != null)
        {
            return;
        }

        _workspaceSession = _viewModel.CurrentProjectSession;
        WorkspaceHost.Content = new DesignerShellView(
            documentController: _viewModel.DocumentController,
            projectSession: _workspaceSession,
            requestStartPage: () => _viewModel.BackToStartCommand.Execute(null));
    }

    private void OnDesignerTitleBarMouseLeftButtonDown(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (args.Handled || args.Button != MouseButton.Left || !IsInTitleBarDragBand(args.Position))
        {
            return;
        }

        if (FindSelfOrAncestor<Button>(args.OriginalSource) != null)
        {
            return;
        }

        if (TryBeginWindowDragMove())
        {
            args.Handled = true;
        }
    }

    private bool TryBeginWindowDragMove()
    {
        if (_beginWindowDragMoveOverride != null)
        {
            return _beginWindowDragMoveOverride();
        }

        if (!UiApplication.Current.HasMainWindow || !OperatingSystem.IsWindows())
        {
            return false;
        }

        Application.Current.MainWindow.BeginDragMove();
        return true;
    }

    private bool IsInTitleBarDragBand(Microsoft.Xna.Framework.Vector2 position)
    {
        var titleBarBounds = DesignerTitleBar.LayoutSlot;
        var top = Math.Max(0f, titleBarBounds.Y - 2f);
        var bottom = titleBarBounds.Y + titleBarBounds.Height;
        return position.Y >= top && position.Y <= bottom;
    }

    private void UpdateSearchProjectPlaceholderVisibility()
    {
        SearchProjectPlaceholder.Visibility = string.IsNullOrWhiteSpace(_viewModel.OpenProjectPathText)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnDesignerHostRootPreviewMouseMove(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        UpdateRecentProjectRemoveButtons(FindNamedAncestor<Grid>(args.OriginalSource, "RecentProjectRowRoot"));
    }

    private void OnDesignerHostRootMouseLeave(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateRecentProjectRemoveButtons(null);
    }

    private void OnRecentProjectRemoveButtonPreviewMouseLeftButtonDown(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        TryHandleRecentProjectRemoveClick(args, executeCommand: true);
    }

    private void OnRecentProjectRemoveButtonPreviewMouseLeftButtonUp(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        TryHandleRecentProjectRemoveClick(args, executeCommand: false);
    }

    private void UpdateRecentProjectRemoveButtons(Grid? visibleRow)
    {
        foreach (var recentRow in FindDescendants<Grid>(this))
        {
            if (!string.Equals(recentRow.Name, "RecentProjectRowRoot", StringComparison.Ordinal))
            {
                continue;
            }

            var openButton = FindRecentProjectOpenButton(recentRow);
            if (openButton == null)
            {
                continue;
            }

            if (ReferenceEquals(recentRow, visibleRow))
            {
                openButton.Background = RecentProjectHoverBackground;
                openButton.BorderBrush = RecentProjectHoverBorderBrush;
            }
            else
            {
                openButton.Background = Color.Transparent;
                openButton.BorderBrush = Color.Transparent;
            }
        }

        foreach (var removeButton in FindDescendants<Button>(this))
        {
            if (!string.Equals(removeButton.Name, "RecentProjectRemoveButton", StringComparison.Ordinal))
            {
                continue;
            }

            if (_wiredRecentProjectRemoveButtons.Add(removeButton))
            {
                removeButton.AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonDownEvent, OnRecentProjectRemoveButtonMouseLeftButtonDown, handledEventsToo: true);
                removeButton.AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonUpEvent, OnRecentProjectRemoveButtonMouseLeftButtonUp, handledEventsToo: true);
            }

            var row = FindNamedAncestor<Grid>(removeButton, "RecentProjectRowRoot");
            removeButton.Visibility = row != null && ReferenceEquals(row, visibleRow)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private static Button? FindRecentProjectOpenButton(Grid recentRow)
    {
        foreach (var button in FindDescendants<Button>(recentRow))
        {
            if (!string.Equals(button.Name, "RecentProjectRemoveButton", StringComparison.Ordinal))
            {
                return button;
            }
        }

        return null;
    }

    private void TryHandleRecentProjectRemoveClick(MouseRoutedEventArgs args, bool executeCommand)
    {
        if (args.Handled || args.Button != MouseButton.Left)
        {
            return;
        }

        foreach (var row in FindDescendants<Grid>(this))
        {
            if (!string.Equals(row.Name, "RecentProjectRowRoot", StringComparison.Ordinal) ||
                !LayoutSlotContainsPoint(row.LayoutSlot, args.Position) ||
                !IsPointWithinRecentProjectRemoveAffordance(row.LayoutSlot, args.Position))
            {
                continue;
            }

            if (executeCommand)
            {
                var path = row.DataContext is DesignerRecentProject recent
                    ? recent.Path
                    : null;
                if (_viewModel.RemoveRecentProjectCommand.CanExecute(path))
                {
                    _viewModel.RemoveRecentProjectCommand.Execute(path);
                }
            }

            args.Handled = true;
            return;
        }
    }

    private void OnRecentProjectRemoveButtonMouseLeftButtonDown(object? sender, MouseRoutedEventArgs args)
    {
        if (args.Handled || sender is not Button removeButton || args.Button != MouseButton.Left)
        {
            return;
        }

        ExecuteRemoveRecentProject(removeButton.DataContext as DesignerRecentProject);
        args.Handled = true;
    }

    private static void OnRecentProjectRemoveButtonMouseLeftButtonUp(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (args.Button == MouseButton.Left)
        {
            args.Handled = true;
        }
    }

    private void ExecuteRemoveRecentProject(DesignerRecentProject? recentProject)
    {
        var path = recentProject?.Path;
        if (_viewModel.RemoveRecentProjectCommand.CanExecute(path))
        {
            _viewModel.RemoveRecentProjectCommand.Execute(path);
        }
    }

    private static bool IsPointWithinRecentProjectRemoveAffordance(LayoutRect rowRect, Microsoft.Xna.Framework.Vector2 point)
    {
        const float removeAffordanceWidth = 52f;
        return point.X >= rowRect.X + rowRect.Width - removeAffordanceWidth;
    }

    private static bool LayoutSlotContainsPoint(LayoutRect rect, Microsoft.Xna.Framework.Vector2 point)
    {
        return point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }

    private static TElement? FindSelfOrAncestor<TElement>(UIElement? element)
        where TElement : UIElement
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement match)
            {
                return match;
            }
        }

        return null;
    }

    private static TElement? FindNamedAncestor<TElement>(UIElement? element, string name)
        where TElement : FrameworkElement
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement match && string.Equals(match.Name, name, StringComparison.Ordinal))
            {
                return match;
            }
        }

        return null;
    }

    private static IEnumerable<TElement> FindDescendants<TElement>(UIElement root)
        where TElement : UIElement
    {
        var stack = new Stack<UIElement>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current is TElement match)
            {
                yield return match;
            }

            foreach (var child in current.GetVisualChildren())
            {
                if (child != null)
                {
                    stack.Push(child);
                }
            }
        }
    }
}