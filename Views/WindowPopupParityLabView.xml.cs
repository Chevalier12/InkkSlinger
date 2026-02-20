using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class WindowPopupParityLabView : UserControl
{
    private readonly Popup _popup;
    private PopupPlacementMode _placementMode = PopupPlacementMode.Bottom;
    private bool _popupDismissOnOutsideClick = true;
    private bool _popupCanClose = true;
    private bool _contextMenuStaysOpen;

    public WindowPopupParityLabView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "WindowPopupParityLabView.xml");
        XamlLoader.LoadInto(this, markupPath, this);

        _popup = new Popup
        {
            Title = "Parity Popup",
            Width = 320f,
            Height = 170f,
            DismissOnOutsideClick = true,
            CanClose = true,
            CanDragMove = true,
            Content = new Label
            {
                Text = "Use outside click and Esc to validate popup edge behavior."
            }
        };
        _popup.Opened += (_, _) => RefreshStatus("Popup opened");
        _popup.Closed += (_, _) => RefreshStatus("Popup closed");

        if (ParityComboBox != null)
        {
            ParityComboBox.Items.Add("Alpha");
            ParityComboBox.Items.Add("Beta");
            ParityComboBox.Items.Add("Gamma");
            ParityComboBox.SelectedIndex = 0;
        }

        RefreshStatus("Ready");
    }

    public Action? ToggleFullscreenRequested { get; set; }

    public Action? ResizeTo1024Requested { get; set; }

    public Action? ResizeTo1280Requested { get; set; }

    public Func<string>? WindowSnapshotProvider { get; set; }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        _popup.Font = font;
        if (_popup.Content is Label popupLabel)
        {
            popupLabel.Font = font;
        }

        if (ParityComboBox != null)
        {
            ParityComboBox.Font = font;
        }

        var contextMenu = GetParityContextMenu();
        if (contextMenu == null)
        {
            return;
        }

        foreach (var item in contextMenu.Items)
        {
            if (item is Label itemLabel)
            {
                itemLabel.Font = font;
            }
        }

        ApplyFontToControls(this, font);
    }

    private static void ApplyFontToControls(UIElement root, SpriteFont font)
    {
        switch (root)
        {
            case Label label:
                label.Font = font;
                break;
            case Button button:
                button.Font = font;
                break;
            case TextBox textBox:
                textBox.Font = font;
                break;
            case ComboBox comboBox:
                comboBox.Font = font;
                break;
        }

        foreach (var child in root.GetVisualChildren())
        {
            ApplyFontToControls(child, font);
        }
    }

    public void RefreshWindowStatus()
    {
        if (WindowStatusLabel == null)
        {
            return;
        }

        WindowStatusLabel.Text = WindowSnapshotProvider?.Invoke() ?? "Window: n/a";
    }

    private void OnOpenPopupClick(object? sender, RoutedSimpleEventArgs e)
    {
        var host = FindHostPanel();
        if (host == null)
        {
            RefreshStatus("Popup open failed (no host)");
            return;
        }

        ConfigurePopup();
        _popup.Show(host);
        _popup.Activate();
        RefreshStatus("Popup opened via button");
    }

    private void OnClosePopupClick(object? sender, RoutedSimpleEventArgs e)
    {
        _popup.Close();
        RefreshStatus("Popup close requested");
    }

    private void OnTogglePopupDismissClick(object? sender, RoutedSimpleEventArgs e)
    {
        _popupDismissOnOutsideClick = !_popupDismissOnOutsideClick;
        if (PopupDismissToggleButton != null)
        {
            PopupDismissToggleButton.Text = $"DismissOutside: {(_popupDismissOnOutsideClick ? "On" : "Off")}";
        }

        ConfigurePopup();
        RefreshStatus("Popup dismiss behavior toggled");
    }

    private void OnTogglePopupCanCloseClick(object? sender, RoutedSimpleEventArgs e)
    {
        _popupCanClose = !_popupCanClose;
        if (PopupCanCloseToggleButton != null)
        {
            PopupCanCloseToggleButton.Text = $"CanClose: {(_popupCanClose ? "On" : "Off")}";
        }

        ConfigurePopup();
        RefreshStatus("Popup CanClose toggled");
    }

    private void OnOpenContextMenuClick(object? sender, RoutedSimpleEventArgs e)
    {
        var host = FindHostPanel();
        if (host == null)
        {
            RefreshStatus("ContextMenu open failed (no host)");
            return;
        }

        var anchor = sender as UIElement ?? OpenContextMenuButton;
        if (anchor == null)
        {
            RefreshStatus("ContextMenu open failed (no anchor)");
            return;
        }

        var contextMenu = ContextMenu.GetContextMenu(anchor) ?? GetParityContextMenu();
        if (contextMenu == null)
        {
            RefreshStatus("ContextMenu open failed (no menu)");
            return;
        }

        contextMenu.StaysOpen = _contextMenuStaysOpen;
        var left = anchor?.LayoutSlot.X ?? 40f;
        var top = (anchor?.LayoutSlot.Y ?? 40f) + (anchor?.LayoutSlot.Height ?? 0f) + 6f;
        contextMenu.OpenAt(host, left, top, anchor);
        RefreshStatus("ContextMenu opened");
    }

    private void OnToggleContextMenuStaysOpenClick(object? sender, RoutedSimpleEventArgs e)
    {
        _contextMenuStaysOpen = !_contextMenuStaysOpen;
        if (ContextMenuStaysOpenToggleButton != null)
        {
            ContextMenuStaysOpenToggleButton.Text = $"ContextMenu StaysOpen: {(_contextMenuStaysOpen ? "On" : "Off")}";
        }

        var contextMenu = GetParityContextMenu();
        if (contextMenu != null)
        {
            contextMenu.StaysOpen = _contextMenuStaysOpen;
        }

        RefreshStatus("ContextMenu StaysOpen toggled");
    }

    private void OnPlacementAbsoluteClick(object? sender, RoutedSimpleEventArgs e)
    {
        _placementMode = PopupPlacementMode.Absolute;
        ConfigurePopup();
        RefreshStatus("Placement set to Absolute");
    }

    private void OnPlacementBottomClick(object? sender, RoutedSimpleEventArgs e)
    {
        _placementMode = PopupPlacementMode.Bottom;
        ConfigurePopup();
        RefreshStatus("Placement set to Bottom");
    }

    private void OnPlacementRightClick(object? sender, RoutedSimpleEventArgs e)
    {
        _placementMode = PopupPlacementMode.Right;
        ConfigurePopup();
        RefreshStatus("Placement set to Right");
    }

    private void OnPlacementCenterClick(object? sender, RoutedSimpleEventArgs e)
    {
        _placementMode = PopupPlacementMode.Center;
        ConfigurePopup();
        RefreshStatus("Placement set to Center");
    }

    private void OnHorizontalOffsetChanged(object? sender, RoutedSimpleEventArgs e)
    {
        var value = HorizontalOffsetSlider?.Value ?? 0f;
        _popup.HorizontalOffset = value;
        if (HorizontalOffsetValueLabel != null)
        {
            HorizontalOffsetValueLabel.Text = value.ToString("0");
        }

        RefreshStatus("Horizontal offset changed");
    }

    private void OnVerticalOffsetChanged(object? sender, RoutedSimpleEventArgs e)
    {
        var value = VerticalOffsetSlider?.Value ?? 0f;
        _popup.VerticalOffset = value;
        if (VerticalOffsetValueLabel != null)
        {
            VerticalOffsetValueLabel.Text = value.ToString("0");
        }

        RefreshStatus("Vertical offset changed");
    }

    private void OnOpenComboDropDownClick(object? sender, RoutedSimpleEventArgs e)
    {
        if (ParityComboBox == null)
        {
            return;
        }

        ParityComboBox.IsDropDownOpen = true;
        RefreshStatus("ComboBox dropdown opened");
    }

    private void OnToggleFullscreenClick(object? sender, RoutedSimpleEventArgs e)
    {
        ToggleFullscreenRequested?.Invoke();
        RefreshStatus("Toggle fullscreen requested");
        RefreshWindowStatus();
    }

    private void OnResize1024Click(object? sender, RoutedSimpleEventArgs e)
    {
        ResizeTo1024Requested?.Invoke();
        RefreshStatus("Resize 1024x720 requested");
        RefreshWindowStatus();
    }

    private void OnResize1280Click(object? sender, RoutedSimpleEventArgs e)
    {
        ResizeTo1280Requested?.Invoke();
        RefreshStatus("Resize 1280x900 requested");
        RefreshWindowStatus();
    }

    private void ConfigurePopup()
    {
        _popup.DismissOnOutsideClick = _popupDismissOnOutsideClick;
        _popup.CanClose = _popupCanClose;
        _popup.PlacementMode = _placementMode;
        _popup.HorizontalOffset = HorizontalOffsetSlider?.Value ?? _popup.HorizontalOffset;
        _popup.VerticalOffset = VerticalOffsetSlider?.Value ?? _popup.VerticalOffset;
        _popup.PlacementTarget = OpenPopupButton;
        if (_placementMode == PopupPlacementMode.Absolute)
        {
            _popup.Left = 280f;
            _popup.Top = 140f;
        }
    }

    private void RefreshStatus(string pointerStatus)
    {
        if (PopupStatusLabel != null)
        {
            PopupStatusLabel.Text =
                $"Popup: {(_popup.IsOpen ? "Open" : "Closed")} | DismissOutside={_popup.DismissOnOutsideClick} | CanClose={_popup.CanClose} | Placement={_popup.PlacementMode}";
        }

        if (FocusStatusLabel != null)
        {
            var focused = FocusManager.GetFocusedElement();
            FocusStatusLabel.Text = $"Focus: {(focused == null ? "none" : focused.GetType().Name)}";
        }

        if (PointerStatusLabel != null)
        {
            PointerStatusLabel.Text = $"Pointer: {pointerStatus}";
        }
    }

    private Panel? FindHostPanel()
    {
        for (var current = VisualParent; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is Panel panel)
            {
                return panel;
            }
        }

        return null;
    }

    private ContextMenu? GetParityContextMenu()
    {
        if (!Resources.TryGetValue("ParityContextMenu", out var value))
        {
            return null;
        }

        return value as ContextMenu;
    }
}
