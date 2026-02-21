using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ContextMenuParityLabView : UserControl
{
    private ContextMenu? _contextMenu;
    private Button? _contextMenuButton;

    public ContextMenuParityLabView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "ContextMenuParityLabView.xml");
        XamlLoader.LoadInto(this, markupPath, this);

        _contextMenuButton = FindNamedElement<Button>(this, "cmButton");
        _contextMenu = _contextMenuButton == null
            ? null
            : ContextMenu.GetContextMenu(_contextMenuButton) ?? FindNamedElement<ContextMenu>(this, "cm");

        if (_contextMenu != null)
        {
            AttachMenuItemHandlers(_contextMenu);
            _contextMenu.StaysOpen = true;
        }

        RefreshToggleText();
        AppendLog("Ready");
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        ApplyFontRecursive(this, font);
    }

    private static void ApplyFontRecursive(UIElement root, SpriteFont font)
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
            case MenuItem menuItem:
                menuItem.Font = font;
                break;
        }

        foreach (var child in root.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }

    private void Menu_OnOpened(object? sender, EventArgs e)
    {
        AppendLog("ContextMenu Opened");
    }

    private void Menu_OnClosed(object? sender, EventArgs e)
    {
        AppendLog("ContextMenu Closed");
    }

    private void OnToggleStaysOpenClick(object? sender, RoutedSimpleEventArgs e)
    {
        if (_contextMenu == null)
        {
            return;
        }

        _contextMenu.StaysOpen = !_contextMenu.StaysOpen;
        RefreshToggleText();
        AppendLog($"StaysOpen set to {_contextMenu.StaysOpen}");
    }

    private void OnMenuItemClick(object? sender, RoutedSimpleEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        AppendLog($"Invoked: {menuItem.Header}");
    }

    private void AttachMenuItemHandlers(ContextMenu contextMenu)
    {
        foreach (var root in EnumerateRootMenuItems(contextMenu))
        {
            AttachMenuItemHandlersRecursive(root);
        }
    }

    private void AttachMenuItemHandlersRecursive(MenuItem menuItem)
    {
        menuItem.Click += OnMenuItemClick;
        foreach (var child in menuItem.GetChildMenuItems())
        {
            AttachMenuItemHandlersRecursive(child);
        }
    }

    private static IReadOnlyList<MenuItem> EnumerateRootMenuItems(ContextMenu contextMenu)
    {
        var result = new List<MenuItem>();
        foreach (var item in contextMenu.Items)
        {
            if (item is MenuItem menuItem)
            {
                result.Add(menuItem);
            }
        }

        return result;
    }

    private void RefreshToggleText()
    {
        if (ToggleStaysOpenButton == null)
        {
            return;
        }

        var isOn = _contextMenu?.StaysOpen ?? false;
        ToggleStaysOpenButton.Text = $"StaysOpen: {(isOn ? "On" : "Off")}";
    }

    private void AppendLog(string message)
    {
        StatusLabel!.Text = message;
        if (LogList == null)
        {
            return;
        }

        LogList.Items.Insert(0, new Label { Text = message });
        while (LogList.Items.Count > 30)
        {
            LogList.Items.RemoveAt(LogList.Items.Count - 1);
        }
    }

    private static TElement? FindNamedElement<TElement>(UIElement root, string name)
        where TElement : FrameworkElement
    {
        if (root is TElement typed && string.Equals(typed.Name, name, StringComparison.Ordinal))
        {
            return typed;
        }

        foreach (var child in root.GetVisualChildren())
        {
            if (FindNamedElement<TElement>(child, name) is { } match)
            {
                return match;
            }
        }

        return null;
    }
}
