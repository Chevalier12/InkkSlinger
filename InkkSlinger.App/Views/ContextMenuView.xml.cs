using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class ContextMenuView : UserControl
{
    private readonly Grid? _rootHost;
    private readonly Border? _surfaceTarget;
    private readonly Button? _inspectorTarget;
    private readonly Border? _placementAnchor;
    private readonly Border? _absoluteHotspot;
    private readonly Border? _persistentTarget;
    private readonly TextBlock? _attachedHintLabel;
    private readonly TextBlock? _placementStateLabel;
    private readonly TextBlock? _persistentStatusLabel;
    private readonly TextBlock? _openedCountLabel;
    private readonly TextBlock? _closedCountLabel;
    private readonly TextBlock? _lastMenuLabel;
    private readonly TextBlock? _lastTargetLabel;
    private readonly TextBlock? _lastActionLabel;
    private readonly TextBlock? _lifecycleStatusLabel;
    private readonly ContextMenu _surfaceMenu;
    private readonly ContextMenu _inspectorMenu;
    private readonly ContextMenu _placementMenu;
    private readonly ContextMenu _persistentMenu;
    private int _openedCount;
    private int _closedCount;
    private string _lastMenu = "none";
    private string _lastTarget = "none";
    private string _lastAction = "none";
    private string _lifecycleStatus = "idle";

    public ContextMenuView()
    {
        InitializeComponent();

        _rootHost = this.FindName("RootHost") as Grid;
        _surfaceTarget = this.FindName("SurfaceTarget") as Border;
        _inspectorTarget = this.FindName("InspectorTarget") as Button;
        _placementAnchor = this.FindName("PlacementAnchor") as Border;
        _absoluteHotspot = this.FindName("AbsoluteHotspot") as Border;
        _persistentTarget = this.FindName("PersistentTarget") as Border;
        _attachedHintLabel = this.FindName("AttachedHintLabel") as TextBlock;
        _placementStateLabel = this.FindName("PlacementStateLabel") as TextBlock;
        _persistentStatusLabel = this.FindName("PersistentStatusLabel") as TextBlock;
        _openedCountLabel = this.FindName("OpenedCountLabel") as TextBlock;
        _closedCountLabel = this.FindName("ClosedCountLabel") as TextBlock;
        _lastMenuLabel = this.FindName("LastMenuLabel") as TextBlock;
        _lastTargetLabel = this.FindName("LastTargetLabel") as TextBlock;
        _lastActionLabel = this.FindName("LastActionLabel") as TextBlock;
        _lifecycleStatusLabel = this.FindName("LifecycleStatusLabel") as TextBlock;

        _surfaceMenu = BuildSurfaceMenu();
        _inspectorMenu = BuildInspectorMenu();
        _placementMenu = BuildPlacementMenu();
        _persistentMenu = BuildPersistentMenu();

        WireAttachedTargets();
        WireLaunchButtons();
        UpdateTelemetry();
    }

    private void WireAttachedTargets()
    {
        if (_surfaceTarget != null)
        {
            ContextMenu.SetContextMenu(_surfaceTarget, _surfaceMenu);
        }

        if (_inspectorTarget != null)
        {
            ContextMenu.SetContextMenu(_inspectorTarget, _inspectorMenu);
        }

        if (_persistentTarget != null)
        {
            ContextMenu.SetContextMenu(_persistentTarget, _persistentMenu);
        }

        if (_attachedHintLabel != null)
        {
            _attachedHintLabel.Text = "Right-click targets: blue Design surface card, Focused inspector target button, and purple Review dock card.";
        }
    }

    private void WireLaunchButtons()
    {
        if (this.FindName("OpenAnchoredMenuButton") is Button openAnchoredMenuButton)
        {
            openAnchoredMenuButton.Click += OnOpenAnchoredMenuClick;
        }

        if (this.FindName("OpenAbsoluteMenuButton") is Button openAbsoluteMenuButton)
        {
            openAbsoluteMenuButton.Click += OnOpenAbsoluteMenuClick;
        }

        if (this.FindName("CloseMenusButton") is Button closeMenusButton)
        {
            closeMenusButton.Click += OnCloseMenusClick;
        }

        if (this.FindName("OpenPersistentMenuButton") is Button openPersistentMenuButton)
        {
            openPersistentMenuButton.Click += OnOpenPersistentMenuClick;
        }

        if (this.FindName("ClosePersistentMenuButton") is Button closePersistentMenuButton)
        {
            closePersistentMenuButton.Click += OnCloseMenusClick;
        }
    }

    private ContextMenu BuildSurfaceMenu()
    {
        var menu = CreateMenu(
            "Design surface menu",
            "Design surface",
            new Color(22, 34, 49),
            new Color(84, 126, 176));

        menu.Items.Add(CreateActionItem("Open asset", "Enter", "Design surface menu", "Design surface", "Open asset"));
        menu.Items.Add(CreateActionItem("Duplicate selection", "Ctrl+D", "Design surface menu", "Design surface", "Duplicate selection"));
        menu.Items.Add(CreateNavigatorSubmenu());
        menu.Items.Add(CreateRecentAssetsSubmenu());
        menu.Items.Add(CreateActionItem("Export diagnostics", "Ctrl+Shift+E", "Design surface menu", "Design surface", "Export diagnostics"));
        return menu;
    }

    private ContextMenu BuildInspectorMenu()
    {
        var menu = CreateMenu(
            "Inspector menu",
            "Focused inspector",
            new Color(28, 33, 46),
            new Color(109, 132, 188));

        menu.Items.Add(CreateActionItem("Pin panel", string.Empty, "Inspector menu", "Focused inspector", "Pin panel"));
        menu.Items.Add(CreateActionItem("Copy binding path", "Ctrl+Shift+C", "Inspector menu", "Focused inspector", "Copy binding path"));
        menu.Items.Add(CreateInspectorSnapshotSubmenu());
        return menu;
    }

    private ContextMenu BuildPlacementMenu()
    {
        var menu = CreateMenu(
            "Placement menu",
            "Placement preview",
            new Color(27, 40, 25),
            new Color(113, 163, 93));

        menu.Items.Add(CreateActionItem("Insert row above", string.Empty, "Placement menu", "Placement preview", "Insert row above"));
        menu.Items.Add(CreateActionItem("Insert row below", string.Empty, "Placement menu", "Placement preview", "Insert row below"));

        var columnOptions = new MenuItem { Header = "Column options" };
        columnOptions.Items.Add(CreateActionItem("Auto width", string.Empty, "Placement menu", "Placement preview", "Auto width"));
        columnOptions.Items.Add(CreateActionItem("Fill remaining space", string.Empty, "Placement menu", "Placement preview", "Fill remaining space"));
        columnOptions.Items.Add(CreateActionItem("Freeze column", string.Empty, "Placement menu", "Placement preview", "Freeze column"));
        menu.Items.Add(columnOptions);

        return menu;
    }

    private ContextMenu BuildPersistentMenu()
    {
        var menu = CreateMenu(
            "Review dock menu",
            "Review dock",
            new Color(33, 24, 44),
            new Color(135, 109, 184));
        menu.StaysOpen = true;

        menu.Items.Add(CreatePersistentActionItem("Compact density"));
        menu.Items.Add(CreatePersistentActionItem("Comfortable density"));
        menu.Items.Add(CreatePersistentActionItem("Roomy density"));

        var overlays = new MenuItem { Header = "Surface overlays" };
        overlays.Items.Add(CreatePersistentActionItem("Show rulers"));
        overlays.Items.Add(CreatePersistentActionItem("Show baseline grid"));
        overlays.Items.Add(CreatePersistentActionItem("Show hit test bounds"));
        menu.Items.Add(overlays);

        return menu;
    }

    private ContextMenu CreateMenu(string menuName, string targetName, Color background, Color borderBrush)
    {
        var menu = new ContextMenu
        {
            Background = background,
            BorderBrush = borderBrush,
            BorderThickness = 1f,
            Padding = new Thickness(3f)
        };

        menu.Opened += (_, _) =>
        {
            _openedCount++;
            _lastMenu = menuName;
            _lastTarget = targetName;
            _lifecycleStatus = $"{menuName} opened";
            UpdateTelemetry();
        };

        menu.Closed += (_, _) =>
        {
            _closedCount++;
            _lastMenu = menuName;
            _lastTarget = targetName;
            _lifecycleStatus = $"{menuName} closed";
            if (ReferenceEquals(menu, _persistentMenu) && _persistentStatusLabel != null)
            {
                _persistentStatusLabel.Text = "Persistent menu state: closed";
            }

            UpdateTelemetry();
        };

        return menu;
    }

    private MenuItem CreateNavigatorSubmenu()
    {
        var navigator = new MenuItem { Header = "Reveal in navigator" };
        navigator.Items.Add(CreateActionItem("Open containing folder", "Ctrl+Shift+R", "Design surface menu", "Design surface", "Open containing folder"));
        navigator.Items.Add(CreateActionItem("Copy asset path", string.Empty, "Design surface menu", "Design surface", "Copy asset path"));
        return navigator;
    }

    private MenuItem CreateRecentAssetsSubmenu()
    {
        var recent = new MenuItem { Header = "Recent captures" };

        var today = new MenuItem { Header = "Today" };
        today.Items.Add(CreateActionItem("hero-idle.png", string.Empty, "Design surface menu", "Design surface", "Open hero-idle.png"));
        today.Items.Add(CreateActionItem("sidebar-hover.png", string.Empty, "Design surface menu", "Design surface", "Open sidebar-hover.png"));

        var archive = new MenuItem { Header = "Archive" };
        archive.Items.Add(CreateActionItem("release-v12.json", string.Empty, "Design surface menu", "Design surface", "Open release-v12.json"));
        archive.Items.Add(CreateActionItem("perf-snapshot.txt", string.Empty, "Design surface menu", "Design surface", "Open perf-snapshot.txt"));

        recent.Items.Add(today);
        recent.Items.Add(archive);
        return recent;
    }

    private MenuItem CreateInspectorSnapshotSubmenu()
    {
        var snapshots = new MenuItem { Header = "Export snapshot" };
        snapshots.Items.Add(CreateActionItem("Visual tree", string.Empty, "Inspector menu", "Focused inspector", "Export visual tree"));
        snapshots.Items.Add(CreateActionItem("Layout slot", string.Empty, "Inspector menu", "Focused inspector", "Export layout slot"));
        snapshots.Items.Add(CreateActionItem("Automation state", string.Empty, "Inspector menu", "Focused inspector", "Export automation state"));
        return snapshots;
    }

    private MenuItem CreatePersistentActionItem(string actionName)
    {
        var item = CreateActionItem(actionName, string.Empty, "Review dock menu", "Review dock", actionName);
        item.Click += (_, _) =>
        {
            if (_persistentStatusLabel != null)
            {
                _persistentStatusLabel.Text = $"Persistent menu state: open after '{actionName}'";
            }
        };

        return item;
    }

    private MenuItem CreateActionItem(string header, string inputGestureText, string menuName, string targetName, string actionName)
    {
        var item = new MenuItem
        {
            Header = header,
            InputGestureText = inputGestureText
        };

        item.Click += (_, _) =>
        {
            _lastMenu = menuName;
            _lastTarget = targetName;
            _lastAction = actionName;
            _lifecycleStatus = $"{menuName} invoked '{actionName}'";
            UpdateTelemetry();
        };

        return item;
    }

    private void OnOpenAnchoredMenuClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_rootHost == null || _placementAnchor == null)
        {
            return;
        }

        CloseAllMenus();
        _placementMenu.Placement = PopupPlacementMode.Bottom;
        _placementMenu.PlacementTarget = _placementAnchor;
        _placementMenu.HorizontalOffset = 12f;
        _placementMenu.VerticalOffset = 10f;
        _placementMenu.Open(_rootHost);
        UpdatePlacementState($"Last placement: Bottom on placement anchor at ({_placementMenu.Left:0.#}, {_placementMenu.Top:0.#})");
    }

    private void OnOpenAbsoluteMenuClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_rootHost == null || _absoluteHotspot == null)
        {
            return;
        }

        CloseAllMenus();
        var left = _absoluteHotspot.LayoutSlot.X + 26f;
        var top = _absoluteHotspot.LayoutSlot.Y + 44f;
        _placementMenu.OpenAt(_rootHost, left, top, _absoluteHotspot);
        UpdatePlacementState($"Last placement: Absolute at timeline hotspot ({_placementMenu.Left:0.#}, {_placementMenu.Top:0.#})");
    }

    private void OnOpenPersistentMenuClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_rootHost == null || _persistentTarget == null)
        {
            return;
        }

        CloseAllMenus();
        _persistentMenu.Placement = PopupPlacementMode.Bottom;
        _persistentMenu.PlacementTarget = _persistentTarget;
        _persistentMenu.HorizontalOffset = 0f;
        _persistentMenu.VerticalOffset = 8f;
        _persistentMenu.Open(_rootHost);
        UpdatePlacementState($"Last placement: Bottom on review dock at ({_persistentMenu.Left:0.#}, {_persistentMenu.Top:0.#})");

        if (_persistentStatusLabel != null)
        {
            _persistentStatusLabel.Text = "Persistent menu state: open and waiting for multiple actions";
        }
    }

    private void OnCloseMenusClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        CloseAllMenus();
    }

    private void CloseAllMenus()
    {
        CloseMenu(_surfaceMenu);
        CloseMenu(_inspectorMenu);
        CloseMenu(_placementMenu);
        CloseMenu(_persistentMenu);
    }

    private static void CloseMenu(ContextMenu menu)
    {
        if (menu.IsOpen)
        {
            menu.Close();
        }
    }

    private void UpdatePlacementState(string placementText)
    {
        if (_placementStateLabel != null)
        {
            _placementStateLabel.Text = placementText;
        }
    }

    private void UpdateTelemetry()
    {
        if (_openedCountLabel != null)
        {
            _openedCountLabel.Text = $"Opened: {_openedCount}";
        }

        if (_closedCountLabel != null)
        {
            _closedCountLabel.Text = $"Closed: {_closedCount}";
        }

        if (_lastMenuLabel != null)
        {
            _lastMenuLabel.Text = $"Last menu: {_lastMenu}";
        }

        if (_lastTargetLabel != null)
        {
            _lastTargetLabel.Text = $"Last target: {_lastTarget}";
        }

        if (_lastActionLabel != null)
        {
            _lastActionLabel.Text = $"Last action: {_lastAction}";
        }

        if (_lifecycleStatusLabel != null)
        {
            _lifecycleStatusLabel.Text = $"Lifecycle: {_lifecycleStatus}";
        }
    }
}

