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
    private readonly Dictionary<Grid, Button> _recentProjectOpenButtonsByRow = new();
    private readonly Dictionary<Button, Grid> _recentProjectRowsByRemoveButton = new();
    private Grid? _visibleRecentProjectRow;
    private bool _recentProjectButtonCacheDirty = true;
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
            _recentProjectButtonCacheDirty = true;
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
        if (_viewModel.CurrentViewState != DesignerHostViewState.StartPage)
        {
            return;
        }

        UpdateRecentProjectRemoveButtons(ResolveRecentProjectRow(args));
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
        if (visibleRow != null && !_recentProjectOpenButtonsByRow.ContainsKey(visibleRow))
        {
            _recentProjectButtonCacheDirty = true;
        }

        if (ReferenceEquals(_visibleRecentProjectRow, visibleRow) &&
            !_recentProjectButtonCacheDirty &&
            (visibleRow == null || IsRecentProjectRowHoverStateApplied(visibleRow)))
        {
            return;
        }

        RefreshRecentProjectButtonCacheIfNeeded();

        if (visibleRow != null && !_recentProjectOpenButtonsByRow.ContainsKey(visibleRow))
        {
            CacheRecentProjectRow(visibleRow);
        }

        if (_visibleRecentProjectRow != null && !ReferenceEquals(_visibleRecentProjectRow, visibleRow))
        {
            ApplyRecentProjectRowHoverState(_visibleRecentProjectRow, isVisible: false);
        }

        if (visibleRow != null)
        {
            ApplyRecentProjectRowHoverState(visibleRow, isVisible: true);
        }

        _visibleRecentProjectRow = visibleRow;
        _recentProjectButtonCacheDirty = false;
    }

    private void RefreshRecentProjectButtonCacheIfNeeded()
    {
        if (!_recentProjectButtonCacheDirty)
        {
            return;
        }

        _recentProjectOpenButtonsByRow.Clear();
        _recentProjectRowsByRemoveButton.Clear();

        foreach (var recentRow in FindDescendants<Grid>(RecentProjectsItemsControl))
        {
            if (!string.Equals(recentRow.Name, "RecentProjectRowRoot", StringComparison.Ordinal))
            {
                continue;
            }

            CacheRecentProjectRow(recentRow);
        }
    }

    private Grid? ResolveRecentProjectRow(MouseRoutedEventArgs args)
    {
        var row = FindNamedAncestor<Grid>(args.OriginalSource, "RecentProjectRowRoot");
        if (row != null)
        {
            return row;
        }

        if (FindSelfOrAncestor<Button>(args.OriginalSource) is Button button &&
            _recentProjectRowsByRemoveButton.TryGetValue(button, out row))
        {
            return row;
        }

        return FindRecentProjectRowAtPoint(args.Position);
    }

    private Grid? FindRecentProjectRowAtPoint(Microsoft.Xna.Framework.Vector2 point)
    {
        RefreshRecentProjectButtonCacheIfNeeded();

        foreach (var row in _recentProjectOpenButtonsByRow.Keys)
        {
            if (LayoutSlotContainsPoint(row.LayoutSlot, point))
            {
                return row;
            }
        }

        foreach (var pair in _recentProjectRowsByRemoveButton)
        {
            if (LayoutSlotContainsPoint(pair.Key.LayoutSlot, point))
            {
                return pair.Value;
            }
        }

        foreach (var recentRow in FindDescendants<Grid>(this))
        {
            if (!string.Equals(recentRow.Name, "RecentProjectRowRoot", StringComparison.Ordinal))
            {
                continue;
            }

            CacheRecentProjectRow(recentRow);
            if (LayoutSlotContainsPoint(recentRow.LayoutSlot, point))
            {
                return recentRow;
            }
        }

        return null;
    }

    private void CacheRecentProjectRow(Grid recentRow)
    {
        var openButton = FindRecentProjectOpenButton(recentRow);
        if (openButton != null)
        {
            _recentProjectOpenButtonsByRow[recentRow] = openButton;
        }

        var removeButton = FindRecentProjectRemoveButton(recentRow);
        if (removeButton == null)
        {
            return;
        }

        _recentProjectRowsByRemoveButton[removeButton] = recentRow;
        if (_wiredRecentProjectRemoveButtons.Add(removeButton))
        {
            removeButton.AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonDownEvent, OnRecentProjectRemoveButtonMouseLeftButtonDown, handledEventsToo: true);
            removeButton.AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonUpEvent, OnRecentProjectRemoveButtonMouseLeftButtonUp, handledEventsToo: true);
        }
    }

    private void ApplyRecentProjectRowHoverState(Grid row, bool isVisible)
    {
        if (_recentProjectOpenButtonsByRow.TryGetValue(row, out var openButton))
        {
            var background = isVisible ? RecentProjectHoverBackground : Color.Transparent;
            var borderBrush = isVisible ? RecentProjectHoverBorderBrush : Color.Transparent;
            if (openButton.Background != background)
            {
                openButton.Background = background;
            }

            if (openButton.BorderBrush != borderBrush)
            {
                openButton.BorderBrush = borderBrush;
            }
        }

        foreach (var pair in _recentProjectRowsByRemoveButton)
        {
            if (!ReferenceEquals(pair.Value, row))
            {
                continue;
            }

            var visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            if (pair.Key.Visibility != visibility)
            {
                pair.Key.Visibility = visibility;
            }
            return;
        }
    }

    private bool IsRecentProjectRowHoverStateApplied(Grid row)
    {
        if (_recentProjectOpenButtonsByRow.TryGetValue(row, out var openButton) &&
            (openButton.Background != RecentProjectHoverBackground || openButton.BorderBrush != RecentProjectHoverBorderBrush))
        {
            return false;
        }

        foreach (var pair in _recentProjectRowsByRemoveButton)
        {
            if (ReferenceEquals(pair.Value, row))
            {
                return pair.Key.Visibility == Visibility.Visible;
            }
        }

        return true;
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

    private static Button? FindRecentProjectRemoveButton(Grid recentRow)
    {
        foreach (var button in FindDescendants<Button>(recentRow))
        {
            if (string.Equals(button.Name, "RecentProjectRemoveButton", StringComparison.Ordinal))
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

        RefreshRecentProjectButtonCacheIfNeeded();

        foreach (var row in _recentProjectOpenButtonsByRow.Keys)
        {
            if (!LayoutSlotContainsPoint(row.LayoutSlot, args.Position) ||
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