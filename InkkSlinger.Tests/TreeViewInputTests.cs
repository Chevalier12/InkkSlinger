using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TreeViewInputTests
{
    [Fact]
    public void ClickingItemRow_ShouldSelectTreeViewItem()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 280f
        };

        var treeView = new TreeView
        {
            Width = 320f,
            Height = 220f
        };
        var root = new TreeViewItem
        {
            Header = "Root",
            IsExpanded = true
        };
        var child = new TreeViewItem
        {
            Header = "Child"
        };
        root.Items.Add(child);
        treeView.Items.Add(root);

        host.AddChild(treeView);
        Canvas.SetLeft(treeView, 20f);
        Canvas.SetTop(treeView, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        var childRowPoint = new Vector2(child.LayoutSlot.X + 24f, child.LayoutSlot.Y + 8f);
        Click(uiRoot, childRowPoint);

        Assert.Same(child, treeView.SelectedItem);
        Assert.True(child.IsSelected);
        Assert.False(root.IsSelected);
    }

    [Fact]
    public void ClickingExpanderGlyph_ShouldToggleExpandedState()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 280f
        };

        var treeView = new TreeView
        {
            Width = 320f,
            Height = 220f
        };
        var root = new TreeViewItem
        {
            Header = "Root",
            IsExpanded = true
        };
        root.Items.Add(new TreeViewItem { Header = "Child" });
        treeView.Items.Add(root);

        host.AddChild(treeView);
        Canvas.SetLeft(treeView, 20f);
        Canvas.SetTop(treeView, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        Assert.True(root.IsExpanded);

        var expanderPoint = new Vector2(root.LayoutSlot.X + 8f, root.LayoutSlot.Y + 9f);
        Click(uiRoot, expanderPoint);
        Assert.False(root.IsExpanded);

        RunLayout(uiRoot);
        Click(uiRoot, expanderPoint);
        Assert.True(root.IsExpanded);
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 460, 280));
    }
}
