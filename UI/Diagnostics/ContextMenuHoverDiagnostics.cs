using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal static class ContextMenuHoverDiagnostics
{
    private static readonly bool IsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_CONTEXTMENU_HOVER_LOGS"), "1", StringComparison.Ordinal);
    private static readonly Dictionary<ContextMenu, MenuItem> LastItemByMenu = new();
    private static long _sequence;

    internal static void ObservePointerHover(MenuItem hoveredItem, Vector2 pointerPosition, UIElement? inputTarget, string resolvePath, string stage)
    {
        if (!IsEnabled)
        {
            return;
        }

        var ownerMenu = hoveredItem.OwnerContextMenu;
        if (ownerMenu == null)
        {
            return;
        }

        var previous = LastItemByMenu.TryGetValue(ownerMenu, out var lastItem) ? lastItem : null;
        LastItemByMenu[ownerMenu] = hoveredItem;
        _sequence++;

        var line =
            $"[ContextMenuHover] seq={_sequence} stage={stage} changed={!ReferenceEquals(previous, hoveredItem)} " +
            $"resolvePath={resolvePath} pointer=({pointerPosition.X:0.##},{pointerPosition.Y:0.##}) " +
            $"target={DescribeElement(inputTarget)} item=\"{hoveredItem.Header}\" " +
            $"itemPath={BuildItemPath(hoveredItem)} hasChildren={hoveredItem.HasChildItems} " +
            $"highlighted={hoveredItem.IsHighlighted} submenuOpen={hoveredItem.IsSubmenuOpen} " +
            $"menuOpen={ownerMenu.IsOpen} itemSlot={FormatRect(hoveredItem.LayoutSlot)}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    private static string BuildItemPath(MenuItem item)
    {
        var parts = new List<string>(8);
        UIElement? current = item;
        while (current != null && parts.Count < 8)
        {
            if (current is MenuItem menuItem)
            {
                parts.Add(menuItem.Header);
            }
            else if (current is ContextMenu)
            {
                parts.Add("ContextMenu");
                break;
            }

            current = current.VisualParent ?? current.LogicalParent;
        }

        parts.Reverse();
        return string.Join(">", parts);
    }

    private static string DescribeElement(UIElement? element)
    {
        if (element == null)
        {
            return "<null>";
        }

        return element.GetType().Name;
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"({rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##})";
    }
}
