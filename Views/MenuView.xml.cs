using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class MenuView : UserControl
{
    private readonly ContentControl? _workspaceMenuHost;
    private readonly ContentControl? _inspectorMenuHost;
    private readonly TextBlock? _workspaceTitleLabel;
    private readonly TextBlock? _workspaceSummaryLabel;
    private readonly TextBlock? _workspaceHintLabel;
    private readonly TextBlock? _workspaceMenuStateLabel;
    private readonly TextBlock? _inspectorMenuStateLabel;
    private readonly TextBlock? _inspectorActionLabel;
    private readonly TextBlock? _openTransitionsLabel;
    private readonly TextBlock? _closeTransitionsLabel;
    private readonly TextBlock? _invocationCountLabel;
    private readonly TextBlock? _lastOpenPathLabel;
    private readonly TextBlock? _lastActionLabel;
    private readonly TextBlock? _lastTargetLabel;
    private readonly TextBlock? _recentActivityLabel;
    private readonly Menu _workspaceMenu;
    private readonly Menu _inspectorMenu;
    private readonly MenuItem _workspaceFileMenu;
    private readonly MenuItem _workspaceEditMenu;
    private readonly MenuItem _workspaceViewMenu;
    private readonly MenuItem _workspaceExportMenu;
    private readonly MenuItem _workspacePanelsMenu;
    private readonly MenuItem _workspaceTelemetryBundleItem;
    private readonly MenuItem _inspectorDiagnosticsMenu;
    private readonly MenuItem _inspectorAutomationMenu;
    private readonly MenuItem _inspectorRecordScriptItem;
    private readonly Dictionary<MenuItem, string> _menuItemPaths = new();
    private readonly Dictionary<MenuItem, string> _menuItemTargets = new();
    private readonly Queue<string> _recentActivity = new();
    private int _openTransitions;
    private int _closeTransitions;
    private int _invocationCount;
    private string _lastOpenPath = "none";
    private string _lastAction = "none";
    private string _lastTarget = "none";

    public MenuView()
    {
        InitializeComponent();

        _workspaceMenuHost = this.FindName("WorkspaceMenuHost") as ContentControl;
        _inspectorMenuHost = this.FindName("InspectorMenuHost") as ContentControl;
        _workspaceTitleLabel = this.FindName("WorkspaceTitleLabel") as TextBlock;
        _workspaceSummaryLabel = this.FindName("WorkspaceSummaryLabel") as TextBlock;
        _workspaceHintLabel = this.FindName("WorkspaceHintLabel") as TextBlock;
        _workspaceMenuStateLabel = this.FindName("WorkspaceMenuStateLabel") as TextBlock;
        _inspectorMenuStateLabel = this.FindName("InspectorMenuStateLabel") as TextBlock;
        _inspectorActionLabel = this.FindName("InspectorActionLabel") as TextBlock;
        _openTransitionsLabel = this.FindName("OpenTransitionsLabel") as TextBlock;
        _closeTransitionsLabel = this.FindName("CloseTransitionsLabel") as TextBlock;
        _invocationCountLabel = this.FindName("InvocationCountLabel") as TextBlock;
        _lastOpenPathLabel = this.FindName("LastOpenPathLabel") as TextBlock;
        _lastActionLabel = this.FindName("LastActionLabel") as TextBlock;
        _lastTargetLabel = this.FindName("LastTargetLabel") as TextBlock;
        _recentActivityLabel = this.FindName("RecentActivityLabel") as TextBlock;

        (_workspaceMenu, _workspaceFileMenu, _workspaceEditMenu, _workspaceViewMenu, _workspaceExportMenu, _workspacePanelsMenu, _workspaceTelemetryBundleItem) = BuildWorkspaceMenu();
        (_inspectorMenu, _inspectorDiagnosticsMenu, _inspectorAutomationMenu, _inspectorRecordScriptItem) = BuildInspectorMenu();

        if (_workspaceMenuHost != null)
        {
            _workspaceMenuHost.Content = _workspaceMenu;
        }

        if (_inspectorMenuHost != null)
        {
            _inspectorMenuHost.Content = _inspectorMenu;
        }

        AttachMenuTelemetry(_workspaceMenu);
        AttachMenuTelemetry(_inspectorMenu);
        WireButtons();
        AppendActivity("Menu demo ready");
        UpdateWorkspaceSurface(
            "Workspace ready",
            "Use the menu strip to open routes like File > Export or View > Panels. The content area updates after leaf commands so the demo doubles as a quick behavior probe.",
            "Suggested path: open File, then expand Recent Sessions or Export to inspect nested submenu rendering.");
        UpdateTelemetry();
    }

    private (Menu Menu, MenuItem File, MenuItem Edit, MenuItem View, MenuItem Export, MenuItem Panels, MenuItem TelemetryBundle) BuildWorkspaceMenu()
    {
        var palette = new MenuPalette(
            new Color(18, 24, 33),
            new Color(48, 72, 99),
            Color.White,
            new Color(46, 83, 122),
            new Color(35, 63, 91),
            new Color(19, 27, 39),
            new Color(70, 100, 134));

        var menu = CreateMenu(palette);

        var fileMenu = CreateBranchItem("_File", palette, "Workspace shell", "File");
        fileMenu.Items.Add(CreateLeafItem(
            "New storyboard",
            "Ctrl+N",
            palette,
            "Workspace shell",
            "File > New storyboard",
            "Started a new storyboard draft.",
            () => UpdateWorkspaceSurface(
                "Draft storyboard created",
                "A new motion storyboard shell was staged from the File menu.",
                "Next step: open File > Recent Sessions to compare nested submenu depth and route ordering.")));
        fileMenu.Items.Add(CreateLeafItem(
            "Open workspace",
            "Ctrl+O",
            palette,
            "Workspace shell",
            "File > Open workspace",
            "Opened the parity-workbench workspace.",
            () => UpdateWorkspaceSurface(
                "Workspace loaded",
                "parity-workbench.inkk is active with automation traces and control repro notes attached.",
                "Try File > Export > Telemetry bundle to see a deeper branch stay open while leaf items remain invokable.")));

        var recentSessions = CreateBranchItem("Recent Sessions", palette, "Workspace shell", "File > Recent Sessions");
        recentSessions.Items.Add(CreateLeafItem(
            "DockPanel parity pass",
            string.Empty,
            palette,
            "Workspace shell",
            "File > Recent Sessions > DockPanel parity pass",
            "Loaded the DockPanel parity session.",
            () => UpdateWorkspaceSurface(
                "Session loaded: DockPanel parity pass",
                "The document area switched to the saved DockPanel regression snapshot.",
                "Nested menu leaf invocation should collapse open menu routes after execution.")));
        recentSessions.Items.Add(CreateLeafItem(
            "Menu regression sweep",
            string.Empty,
            palette,
            "Workspace shell",
            "File > Recent Sessions > Menu regression sweep",
            "Loaded the Menu regression sweep session.",
            () => UpdateWorkspaceSurface(
                "Session loaded: Menu regression sweep",
                "The catalog returned to a saved validation surface focused on submenu layout and access keys.",
                "Use Edit or View next to watch top-level routing move across the same menu bar.")));
        fileMenu.Items.Add(recentSessions);

        var exportMenu = CreateBranchItem("Export", palette, "Workspace shell", "File > Export");
        exportMenu.Items.Add(CreateLeafItem(
            "Frame capture",
            "Ctrl+Shift+F",
            palette,
            "Workspace shell",
            "File > Export > Frame capture",
            "Captured the current frame as a PNG artifact.",
            () => UpdateWorkspaceSurface(
                "Frame capture exported",
                "A current-frame preview was written for visual inspection.",
                "The export path uses explicit InputGestureText and a second-level submenu.")));
        exportMenu.Items.Add(CreateLeafItem(
            "Design notes",
            "Ctrl+Shift+M",
            palette,
            "Workspace shell",
            "File > Export > Design notes",
            "Exported design notes as markdown.",
            () => UpdateWorkspaceSurface(
                "Design notes exported",
                "A markdown handoff summary was generated from the current workspace selection.",
                "Open View > Panels next to compare a sibling top-level route and its submenu branch.")));
        var telemetryBundle = CreateLeafItem(
            "Telemetry bundle",
            "Ctrl+Shift+T",
            palette,
            "Workspace shell",
            "File > Export > Telemetry bundle",
            "Bundled UI telemetry, automation logs, and captures.",
            () => UpdateWorkspaceSurface(
                "Telemetry bundle ready",
                "The export includes command logs, runtime telemetry, and the latest captures from the workspace shell.",
                "Leaf actions update the faux document surface so the demo remains useful even outside automated tests."));
        exportMenu.Items.Add(telemetryBundle);
        fileMenu.Items.Add(exportMenu);

        var editMenu = CreateBranchItem("_Edit", palette, "Workspace shell", "Edit");
        editMenu.Items.Add(CreateLeafItem(
            "Undo",
            "Ctrl+Z",
            palette,
            "Workspace shell",
            "Edit > Undo",
            "Reverted the latest surface adjustment.",
            () => UpdateWorkspaceSurface(
                "Last change undone",
                "The workspace rolled back the previous layout adjustment.",
                "Edit demonstrates a sibling top-level route with command-style leaf items.")));
        editMenu.Items.Add(CreateLeafItem(
            "Redo",
            "Ctrl+Y",
            palette,
            "Workspace shell",
            "Edit > Redo",
            "Reapplied the latest surface adjustment.",
            () => UpdateWorkspaceSurface(
                "Last change restored",
                "The workspace reapplied the previously undone operation.",
                "Try moving between File and Edit using the buttons or direct clicks on the strip.")));

        var viewMenu = CreateBranchItem("_View", palette, "Workspace shell", "View");
        var zoomMenu = CreateBranchItem("Zoom", palette, "Workspace shell", "View > Zoom");
        zoomMenu.Items.Add(CreateLeafItem(
            "100%",
            "Ctrl+1",
            palette,
            "Workspace shell",
            "View > Zoom > 100%",
            "Reset the canvas zoom to 100%.",
            () => UpdateWorkspaceSurface(
                "Zoom reset to 100%",
                "The canvas preview returned to its neutral zoom level.",
                "Deep nested routes like View > Zoom mirror the classic application menu pattern.")));
        zoomMenu.Items.Add(CreateLeafItem(
            "200%",
            "Ctrl+2",
            palette,
            "Workspace shell",
            "View > Zoom > 200%",
            "Zoomed the canvas preview to 200%.",
            () => UpdateWorkspaceSurface(
                "Zoom increased to 200%",
                "The preview surface now emphasizes hit targets and layout slots.",
                "Open View > Panels after this to inspect another nested branch under the same top-level item.")));
        viewMenu.Items.Add(zoomMenu);

        var panelsMenu = CreateBranchItem("Panels", palette, "Workspace shell", "View > Panels");
        panelsMenu.Items.Add(CreateLeafItem(
            "Inspector",
            string.Empty,
            palette,
            "Workspace shell",
            "View > Panels > Inspector",
            "Focused the Inspector panel.",
            () => UpdateWorkspaceSurface(
                "Inspector panel focused",
                "The right dock now emphasizes property editing and selection state.",
                "The menu demo keeps each invoked leaf visible in telemetry so route coverage is easy to confirm.")));
        panelsMenu.Items.Add(CreateLeafItem(
            "Automation Console",
            string.Empty,
            palette,
            "Workspace shell",
            "View > Panels > Automation Console",
            "Opened the automation console panel.",
            () => UpdateWorkspaceSurface(
                "Automation console visible",
                "Automation commands and script output are docked beside the current workspace surface.",
                "This leaf is useful for verifying that sibling submenu items invoke correctly and close the menu tree.")));
        viewMenu.Items.Add(panelsMenu);

        var helpMenu = CreateBranchItem("_Help", palette, "Workspace shell", "Help");
        helpMenu.Items.Add(CreateLeafItem(
            "Controls Catalog",
            "F1",
            palette,
            "Workspace shell",
            "Help > Controls Catalog",
            "Opened the Controls Catalog reference.",
            () => UpdateWorkspaceSurface(
                "Controls Catalog reference opened",
                "Reference material for the current control family is now front and center.",
                "Help completes the classic top-level menu arrangement while still participating in telemetry.")));

        menu.Items.Add(fileMenu);
        menu.Items.Add(editMenu);
        menu.Items.Add(viewMenu);
        menu.Items.Add(helpMenu);

        return (menu, fileMenu, editMenu, viewMenu, exportMenu, panelsMenu, telemetryBundle);
    }

    private (Menu Menu, MenuItem Diagnostics, MenuItem Automation, MenuItem RecordScript) BuildInspectorMenu()
    {
        var palette = new MenuPalette(
            new Color(29, 22, 39),
            new Color(92, 74, 135),
            Color.White,
            new Color(103, 76, 150),
            new Color(76, 54, 120),
            new Color(35, 24, 49),
            new Color(114, 93, 166));

        var menu = CreateMenu(palette);
        menu.Padding = new Thickness(5f, 3f, 5f, 3f);
        menu.ItemSpacing = 4f;

        var sessionMenu = CreateBranchItem("Session", palette, "Inspector strip", "Session");
        sessionMenu.Items.Add(CreateLeafItem(
            "Save checkpoint",
            "Ctrl+S",
            palette,
            "Inspector strip",
            "Session > Save checkpoint",
            "Saved an inspector checkpoint.",
            () => UpdateInspectorStatus("Inspector status: session checkpoint saved for the current validation surface.")));

        var diagnosticsMenu = CreateBranchItem("Diagnostics", palette, "Inspector strip", "Diagnostics");
        diagnosticsMenu.Items.Add(CreateLeafItem(
            "Dump visual tree",
            string.Empty,
            palette,
            "Inspector strip",
            "Diagnostics > Dump visual tree",
            "Dumped the current visual tree.",
            () => UpdateInspectorStatus("Inspector status: visual tree dump queued for the focused surface.")));
        var automationMenu = CreateBranchItem("Automation", palette, "Inspector strip", "Diagnostics > Automation");
        var recordScriptItem = CreateLeafItem(
            "Record script",
            string.Empty,
            palette,
            "Inspector strip",
            "Diagnostics > Automation > Record script",
            "Started recording an automation script.",
            () => UpdateInspectorStatus("Inspector status: automation recording started for the current workflow."));
        automationMenu.Items.Add(recordScriptItem);
        automationMenu.Items.Add(CreateLeafItem(
            "Capture frame",
            string.Empty,
            palette,
            "Inspector strip",
            "Diagnostics > Automation > Capture frame",
            "Captured a diagnostic frame.",
            () => UpdateInspectorStatus("Inspector status: a fresh diagnostic frame capture was queued.")));
        diagnosticsMenu.Items.Add(automationMenu);

        var shareMenu = CreateBranchItem("Share", palette, "Inspector strip", "Share");
        shareMenu.Items.Add(CreateLeafItem(
            "Copy issue summary",
            string.Empty,
            palette,
            "Inspector strip",
            "Share > Copy issue summary",
            "Copied the issue summary.",
            () => UpdateInspectorStatus("Inspector status: issue summary copied for triage handoff.")));

        menu.Items.Add(sessionMenu);
        menu.Items.Add(diagnosticsMenu);
        menu.Items.Add(shareMenu);

        return (menu, diagnosticsMenu, automationMenu, recordScriptItem);
    }

    private void WireButtons()
    {
        WireButton("OpenFileMenuButton", OnOpenFileMenuClick);
        WireButton("OpenEditMenuButton", OnOpenEditMenuClick);
        WireButton("OpenViewMenuButton", OnOpenViewMenuClick);
        WireButton("OpenExportTrailButton", OnOpenExportTrailClick);
        WireButton("CollapseMenusButton", OnCollapseMenusClick);
        WireButton("OpenDiagnosticsMenuButton", OnOpenDiagnosticsMenuClick);
        WireButton("OpenAutomationTrailButton", OnOpenAutomationTrailClick);
    }

    private void WireButton(string name, EventHandler<RoutedSimpleEventArgs> handler)
    {
        if (this.FindName(name) is Button button)
        {
            button.Click += handler;
        }
    }

    private void AttachMenuTelemetry(Menu menu)
    {
        foreach (var item in menu.GetTopLevelItems())
        {
            AttachMenuItemTelemetry(item);
        }
    }

    private void AttachMenuItemTelemetry(MenuItem item)
    {
        item.DependencyPropertyChanged += OnMenuItemDependencyPropertyChanged;

        foreach (var child in item.Items.OfType<MenuItem>())
        {
            AttachMenuItemTelemetry(child);
        }
    }

    private void OnMenuItemDependencyPropertyChanged(object? sender, DependencyPropertyChangedEventArgs args)
    {
        if (args.Property != MenuItem.IsSubmenuOpenProperty || sender is not MenuItem item)
        {
            return;
        }

        var path = _menuItemPaths.GetValueOrDefault(item, GetDisplayHeader(item.Header));
        var target = _menuItemTargets.GetValueOrDefault(item, "Menu demo");
        var isOpen = args.NewValue is bool value && value;

        if (isOpen)
        {
            _openTransitions++;
            _lastOpenPath = path;
            _lastTarget = target;
            AppendActivity($"{target}: opened {path}");
        }
        else
        {
            _closeTransitions++;
            _lastTarget = target;
            _lastOpenPath = TryGetAnyOpenPath(out var activePath) ? activePath : "none";
            AppendActivity($"{target}: closed {path}");
        }

        UpdateTelemetry();
    }

    private static Menu CreateMenu(MenuPalette palette)
    {
        return new Menu
        {
            Background = palette.MenuBackground,
            BorderBrush = palette.MenuBorderBrush,
            BorderThickness = 1f,
            Padding = new Thickness(6f, 3f, 6f, 3f),
            ItemSpacing = 3f
        };
    }

    private MenuItem CreateBranchItem(string header, MenuPalette palette, string targetSurface, string path)
    {
        var item = new MenuItem
        {
            Header = header
        };

        ApplyPalette(item, palette);
        RegisterMenuItem(item, targetSurface, path);
        return item;
    }

    private MenuItem CreateLeafItem(
        string header,
        string inputGestureText,
        MenuPalette palette,
        string targetSurface,
        string path,
        string activityLabel,
        Action onInvoke)
    {
        var item = new MenuItem
        {
            Header = header,
            InputGestureText = inputGestureText
        };

        ApplyPalette(item, palette);
        RegisterMenuItem(item, targetSurface, path);
        item.Click += (_, _) =>
        {
            _invocationCount++;
            _lastAction = path;
            _lastTarget = targetSurface;
            _lastOpenPath = path;
            onInvoke();
            AppendActivity($"{targetSurface}: {activityLabel}");
            UpdateTelemetry();
        };
        return item;
    }

    private void RegisterMenuItem(MenuItem item, string targetSurface, string path)
    {
        _menuItemTargets[item] = targetSurface;
        _menuItemPaths[item] = path;
    }

    private static void ApplyPalette(MenuItem item, MenuPalette palette)
    {
        item.Foreground = palette.ItemForeground;
        item.HighlightBackground = palette.HighlightBackground;
        item.OpenBackground = palette.OpenBackground;
        item.SubmenuBackground = palette.SubmenuBackground;
        item.SubmenuBorderBrush = palette.SubmenuBorderBrush;
        item.SubmenuBorderThickness = 1f;
        item.Padding = new Thickness(11f, 7f, 11f, 7f);
    }

    private void OnOpenFileMenuClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        OpenPath(_workspaceFileMenu);
    }

    private void OnOpenEditMenuClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        OpenPath(_workspaceEditMenu);
    }

    private void OnOpenViewMenuClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        OpenPath(_workspaceViewMenu);
    }

    private void OnOpenExportTrailClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        OpenPath(_workspaceFileMenu, _workspaceExportMenu);
    }

    private void OnOpenDiagnosticsMenuClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        OpenPath(_inspectorDiagnosticsMenu);
    }

    private void OnOpenAutomationTrailClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        OpenPath(_inspectorDiagnosticsMenu, _inspectorAutomationMenu);
    }

    private void OnCollapseMenusClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        CloseAllMenus();
        AppendActivity("All menus collapsed");
        UpdateTelemetry();
    }

    private void OpenPath(params MenuItem[] path)
    {
        CloseAllMenus();

        if (path.Length == 0)
        {
            UpdateTelemetry();
            return;
        }

        var topLevel = path[0].GetTopLevelAncestor();
        topLevel.OwnerMenu?.EnterMenuMode(topLevel);

        foreach (var item in path)
        {
            if (item.Items.Count > 0)
            {
                item.IsSubmenuOpen = true;
            }
        }

        _lastTarget = _menuItemTargets.GetValueOrDefault(path[0], "Menu demo");
        _lastOpenPath = _menuItemPaths.GetValueOrDefault(path[^1], "none");

        UpdateTelemetry();
    }

    private void CloseAllMenus()
    {
        _workspaceMenu.CloseAllSubmenus();
        _inspectorMenu.CloseAllSubmenus();
    }

    private bool TryGetAnyOpenPath(out string path)
    {
        if (TryGetOpenPath(_workspaceMenu, out path))
        {
            return true;
        }

        return TryGetOpenPath(_inspectorMenu, out path);
    }

    private bool TryGetOpenPath(Menu menu, out string path)
    {
        foreach (var item in menu.GetTopLevelItems())
        {
            if (TryGetOpenPath(item, out path))
            {
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    private bool TryGetOpenPath(MenuItem item, out string path)
    {
        if (!item.IsSubmenuOpen)
        {
            path = string.Empty;
            return false;
        }

        path = _menuItemPaths.GetValueOrDefault(item, GetDisplayHeader(item.Header));
        foreach (var child in item.Items.OfType<MenuItem>())
        {
            if (TryGetOpenPath(child, out var nestedPath))
            {
                path = nestedPath;
                return true;
            }
        }

        return true;
    }

    private void UpdateWorkspaceSurface(string title, string summary, string hint)
    {
        if (_workspaceTitleLabel != null)
        {
            _workspaceTitleLabel.Text = title;
        }

        if (_workspaceSummaryLabel != null)
        {
            _workspaceSummaryLabel.Text = summary;
        }

        if (_workspaceHintLabel != null)
        {
            _workspaceHintLabel.Text = hint;
        }
    }

    private void UpdateInspectorStatus(string status)
    {
        if (_inspectorActionLabel != null)
        {
            _inspectorActionLabel.Text = status;
        }
    }

    private void AppendActivity(string activity)
    {
        if (_recentActivity.Count == 4)
        {
            _recentActivity.Dequeue();
        }

        _recentActivity.Enqueue(activity);
    }

    private void UpdateTelemetry()
    {
        if (_workspaceMenuStateLabel != null)
        {
            _workspaceMenuStateLabel.Text = $"Workspace menu path: {(TryGetOpenPath(_workspaceMenu, out var workspacePath) ? workspacePath : "closed")}";
        }

        if (_inspectorMenuStateLabel != null)
        {
            _inspectorMenuStateLabel.Text = $"Inspector menu path: {(TryGetOpenPath(_inspectorMenu, out var inspectorPath) ? inspectorPath : "closed")}";
        }

        if (_openTransitionsLabel != null)
        {
            _openTransitionsLabel.Text = $"Opens: {_openTransitions}";
        }

        if (_closeTransitionsLabel != null)
        {
            _closeTransitionsLabel.Text = $"Closes: {_closeTransitions}";
        }

        if (_invocationCountLabel != null)
        {
            _invocationCountLabel.Text = $"Invocations: {_invocationCount}";
        }

        if (_lastOpenPathLabel != null)
        {
            _lastOpenPathLabel.Text = $"Last open path: {_lastOpenPath}";
        }

        if (_lastActionLabel != null)
        {
            _lastActionLabel.Text = $"Last action: {_lastAction}";
        }

        if (_lastTargetLabel != null)
        {
            _lastTargetLabel.Text = $"Last target: {_lastTarget}";
        }

        if (_recentActivityLabel != null)
        {
            _recentActivityLabel.Text = _recentActivity.Count == 0
                ? "Recent activity: idle"
                : $"Recent activity: {string.Join(" | ", _recentActivity)}";
        }
    }

    private static string GetDisplayHeader(string header)
    {
        return string.IsNullOrEmpty(header)
            ? string.Empty
            : header.Replace("_", string.Empty, StringComparison.Ordinal);
    }

    private readonly record struct MenuPalette(
        Color MenuBackground,
        Color MenuBorderBrush,
        Color ItemForeground,
        Color HighlightBackground,
        Color OpenBackground,
        Color SubmenuBackground,
        Color SubmenuBorderBrush);
}




