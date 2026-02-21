using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ContextMenuEdgeParityTests
{
    [Fact]
    public void RightClick_OnElementWithAttachedContextMenu_ShouldOpenAtPointer()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var button = new Button { Width = 160f, Height = 30f, Text = "Open" };
        host.AddChild(button);
        Canvas.SetLeft(button, 40f);
        Canvas.SetTop(button, 50f);

        var menu = CreateSimpleContextMenu();
        ContextMenu.SetContextMenu(button, menu);
        RunLayout(uiRoot);

        var pointer = new Vector2(60f, 60f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, rightPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, rightReleased: true));

        Assert.True(menu.IsOpen);
        Assert.Equal(pointer.X, menu.Left, 0.5f);
        Assert.Equal(pointer.Y, menu.Top, 0.5f);
    }

    [Fact]
    public void RightClick_WithoutPointerMove_ShouldStillOpenAttachedContextMenu()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var button = new Button { Width = 160f, Height = 30f, Text = "Open" };
        host.AddChild(button);
        Canvas.SetLeft(button, 40f);
        Canvas.SetTop(button, 50f);

        var menu = CreateSimpleContextMenu();
        ContextMenu.SetContextMenu(button, menu);
        RunLayout(uiRoot);

        var pointer = new Vector2(60f, 60f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, rightPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, rightReleased: true, pointerMoved: false));

        Assert.True(menu.IsOpen);
    }

    [Fact]
    public void ShiftF10_ShouldOpenAttachedContextMenuForFocusedElement()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var button = new Button { Width = 160f, Height = 30f, Text = "Open" };
        host.AddChild(button);
        Canvas.SetLeft(button, 40f);
        Canvas.SetTop(button, 50f);

        var menu = CreateSimpleContextMenu();
        ContextMenu.SetContextMenu(button, menu);
        RunLayout(uiRoot);

        var pointer = new Vector2(60f, 60f);
        Click(uiRoot, pointer);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F10, new KeyboardState(Keys.LeftShift)));

        Assert.True(menu.IsOpen);
    }

    [Fact]
    public void AppsKey_ShouldOpenAttachedContextMenuForFocusedElement()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var button = new Button { Width = 160f, Height = 30f, Text = "Open" };
        host.AddChild(button);
        Canvas.SetLeft(button, 40f);
        Canvas.SetTop(button, 50f);

        var menu = CreateSimpleContextMenu();
        ContextMenu.SetContextMenu(button, menu);
        RunLayout(uiRoot);

        var pointer = new Vector2(60f, 60f);
        Click(uiRoot, pointer);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Apps));

        Assert.True(menu.IsOpen);
    }

    [Fact]
    public void OpenedAndClosedEvents_ShouldFireOncePerTransition()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var menu = CreateSimpleContextMenu();
        var opened = 0;
        var closed = 0;
        menu.Opened += (_, _) => opened++;
        menu.Closed += (_, _) => closed++;

        menu.OpenAt(host, 100f, 80f);
        RunLayout(uiRoot);
        menu.Close();

        Assert.Equal(1, opened);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Esc_WhenOpen_ShouldClose()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var menu = CreateSimpleContextMenu();
        menu.OpenAt(host, left: 160f, top: 110f);
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape));

        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void OutsideClick_WhenStaysOpenFalse_ShouldClose()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var menu = CreateSimpleContextMenu();
        menu.StaysOpen = false;
        menu.OpenAt(host, left: 220f, top: 120f);
        RunLayout(uiRoot);

        Click(uiRoot, new Vector2(20f, 20f));

        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void OutsideClick_WhenStaysOpenTrue_ShouldRemainOpen()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var menu = CreateSimpleContextMenu();
        menu.StaysOpen = true;
        menu.OpenAt(host, left: 220f, top: 120f);
        RunLayout(uiRoot);

        Click(uiRoot, new Vector2(20f, 20f));

        Assert.True(menu.IsOpen);
    }

    [Fact]
    public void Keyboard_RightAndLeft_ShouldOpenAndCloseSubmenu()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var recent = new MenuItem { Header = "Recent Files" };
        recent.Items.Add(new MenuItem { Header = "ReadMe.txt" });
        recent.Items.Add(new MenuItem { Header = "Schedule.xls" });

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "File" });
        menu.Items.Add(new MenuItem { Header = "Save" });
        menu.Items.Add(new MenuItem { Header = "SaveAs" });
        menu.Items.Add(recent);

        menu.OpenAt(host, left: 100f, top: 80f);
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));

        Assert.True(recent.IsSubmenuOpen);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Left));

        Assert.False(recent.IsSubmenuOpen);
    }

    [Fact]
    public void PlacementBottom_WithOffsets_ShouldPositionFromTarget()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var target = new Button { Width = 120f, Height = 40f, Text = "anchor" };
        host.AddChild(target);
        Canvas.SetLeft(target, 30f);
        Canvas.SetTop(target, 20f);

        var menu = CreateSimpleContextMenu();
        menu.Placement = PopupPlacementMode.Bottom;
        menu.PlacementTarget = target;
        menu.HorizontalOffset = 5f;
        menu.VerticalOffset = 7f;

        menu.Open(host);
        RunLayout(uiRoot);

        Assert.Equal(35f, menu.Left, 0.5f);
        Assert.Equal(67f, menu.Top, 0.5f);
    }

    [Fact]
    public void LeftClick_SubmenuLeaf_ShouldInvokeLeafClick()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var invoked = false;
        var readme = new MenuItem { Header = "ReadMe.txt" };
        readme.Click += (_, _) => invoked = true;

        var recent = new MenuItem { Header = "Recent Files" };
        recent.Items.Add(readme);
        recent.Items.Add(new MenuItem { Header = "Schedule.xls" });

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "File" });
        menu.Items.Add(new MenuItem { Header = "Save" });
        menu.Items.Add(new MenuItem { Header = "SaveAs" });
        menu.Items.Add(recent);
        menu.StaysOpen = true;
        menu.OpenAt(host, left: 100f, top: 80f);
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
        RunLayout(uiRoot);

        var clickPoint = new Vector2(readme.LayoutSlot.X + 4f, readme.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(clickPoint, leftPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(clickPoint, leftReleased: true, pointerMoved: false));

        Assert.True(invoked);
    }

    [Fact]
    public void HoveringContextMenuItem_ShouldHighlightAndExpandSubmenu()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var recent = new MenuItem { Header = "Recent Files" };
        recent.Items.Add(new MenuItem { Header = "ReadMe.txt" });
        recent.Items.Add(new MenuItem { Header = "Schedule.xls" });

        var file = new MenuItem { Header = "File" };
        var menu = new ContextMenu();
        menu.Items.Add(file);
        menu.Items.Add(recent);
        menu.OpenAt(host, left: 100f, top: 80f);
        RunLayout(uiRoot);

        var hoverPoint = new Vector2(recent.LayoutSlot.X + 4f, recent.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverPoint, pointerMoved: true));
        RunLayout(uiRoot);

        Assert.True(recent.IsHighlighted);
        Assert.True(recent.IsSubmenuOpen);
        Assert.False(file.IsSubmenuOpen);
    }

    [Fact]
    public void HoverAfterRightClickOpen_ShouldHighlightAndExpandContextMenuItem()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var recent = new MenuItem { Header = "Recent Files" };
        recent.Items.Add(new MenuItem { Header = "ReadMe.txt" });
        recent.Items.Add(new MenuItem { Header = "Schedule.xls" });

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "File" });
        menu.Items.Add(recent);

        var button = new Button { Width = 160f, Height = 30f, Text = "Open" };
        host.AddChild(button);
        Canvas.SetLeft(button, 40f);
        Canvas.SetTop(button, 50f);
        ContextMenu.SetContextMenu(button, menu);
        RunLayout(uiRoot);

        var openPoint = new Vector2(60f, 60f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(openPoint, rightPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(openPoint, rightReleased: true, pointerMoved: false));
        RunLayout(uiRoot);

        var hoverPoint = new Vector2(recent.LayoutSlot.X + 4f, recent.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverPoint, pointerMoved: true));
        RunLayout(uiRoot);

        Assert.True(recent.IsHighlighted);
        Assert.True(recent.IsSubmenuOpen);
    }

    [Fact]
    public void HoveringSubmenuLeaf_ShouldHighlightLeafItem()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var readme = new MenuItem { Header = "ReadMe.txt" };
        var schedule = new MenuItem { Header = "Schedule.xls" };
        var recent = new MenuItem { Header = "Recent Files" };
        recent.Items.Add(readme);
        recent.Items.Add(schedule);

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "File" });
        menu.Items.Add(recent);

        var button = new Button { Width = 160f, Height = 30f, Text = "Open" };
        host.AddChild(button);
        Canvas.SetLeft(button, 40f);
        Canvas.SetTop(button, 50f);
        ContextMenu.SetContextMenu(button, menu);
        RunLayout(uiRoot);

        var openPoint = new Vector2(60f, 60f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(openPoint, rightPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(openPoint, rightReleased: true, pointerMoved: false));
        RunLayout(uiRoot);

        var hoverParent = new Vector2(recent.LayoutSlot.X + 4f, recent.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverParent, pointerMoved: true));
        RunLayout(uiRoot);
        Assert.True(recent.IsSubmenuOpen);

        var hoverLeaf = new Vector2(readme.LayoutSlot.X + 4f, readme.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverLeaf, pointerMoved: true));
        RunLayout(uiRoot);

        Assert.True(readme.IsHighlighted);
        Assert.False(schedule.IsHighlighted);
    }

    [Fact]
    public void HoveringSecondSubmenuLeaf_ShouldHighlightSecondLeafItem()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var readme = new MenuItem { Header = "ReadMe.txt" };
        var schedule = new MenuItem { Header = "Schedule.xls" };
        var recent = new MenuItem { Header = "Recent Files" };
        recent.Items.Add(readme);
        recent.Items.Add(schedule);

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "File" });
        menu.Items.Add(recent);

        var button = new Button { Width = 160f, Height = 30f, Text = "Open" };
        host.AddChild(button);
        Canvas.SetLeft(button, 40f);
        Canvas.SetTop(button, 50f);
        ContextMenu.SetContextMenu(button, menu);
        RunLayout(uiRoot);

        var openPoint = new Vector2(60f, 60f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(openPoint, rightPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(openPoint, rightReleased: true, pointerMoved: false));
        RunLayout(uiRoot);

        var hoverParent = new Vector2(recent.LayoutSlot.X + 4f, recent.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverParent, pointerMoved: true));
        RunLayout(uiRoot);
        Assert.True(recent.IsSubmenuOpen);

        var hoverSecondLeaf = new Vector2(schedule.LayoutSlot.X + 4f, schedule.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverSecondLeaf, pointerMoved: true));
        RunLayout(uiRoot);

        Assert.True(schedule.IsHighlighted);
        Assert.False(readme.IsHighlighted);
    }

    [Fact]
    public void HoveringVisibleDeepBranchItems_ShouldHighlightInkkSlingerAndDesign()
    {
        var (uiRoot, host) = CreateUiRootWithHost();

        var inkkSlinger = new MenuItem { Header = "InkkSlinger" };
        inkkSlinger.Items.Add(new MenuItem { Header = "Roadmap.md" });
        inkkSlinger.Items.Add(new MenuItem { Header = "Bugs.csv" });

        var design = new MenuItem { Header = "Design" };
        design.Items.Add(new MenuItem { Header = "Palette.json" });
        design.Items.Add(new MenuItem { Header = "Typography.txt" });

        var projects = new MenuItem { Header = "Projects" };
        projects.Items.Add(inkkSlinger);
        projects.Items.Add(design);

        var recent = new MenuItem { Header = "Recent Files" };
        recent.Items.Add(new MenuItem { Header = "ReadMe.txt" });
        recent.Items.Add(new MenuItem { Header = "Schedule.xls" });
        recent.Items.Add(projects);

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "File" });
        menu.Items.Add(new MenuItem { Header = "Save" });
        menu.Items.Add(new MenuItem { Header = "SaveAs" });
        menu.Items.Add(recent);

        var button = new Button { Width = 220f, Height = 30f, Text = "Button with Context Menu" };
        host.AddChild(button);
        Canvas.SetLeft(button, 40f);
        Canvas.SetTop(button, 50f);
        ContextMenu.SetContextMenu(button, menu);
        RunLayout(uiRoot);

        var openPoint = new Vector2(60f, 60f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(openPoint, rightPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(openPoint, rightReleased: true, pointerMoved: false));
        RunLayout(uiRoot);

        var hoverRecent = new Vector2(recent.LayoutSlot.X + 4f, recent.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverRecent, pointerMoved: true));
        RunLayout(uiRoot);
        Assert.True(recent.IsSubmenuOpen);

        var hoverProjects = new Vector2(projects.LayoutSlot.X + 4f, projects.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverProjects, pointerMoved: true));
        RunLayout(uiRoot);
        Assert.True(projects.IsSubmenuOpen);

        var hoverInkkSlinger = new Vector2(inkkSlinger.LayoutSlot.X + 4f, inkkSlinger.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverInkkSlinger, pointerMoved: true));
        RunLayout(uiRoot);
        Assert.True(inkkSlinger.IsHighlighted);
        Assert.False(design.IsHighlighted);

        var hoverDesign = new Vector2(design.LayoutSlot.X + 4f, design.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverDesign, pointerMoved: true));
        RunLayout(uiRoot);
        Assert.True(design.IsHighlighted);
        Assert.False(inkkSlinger.IsHighlighted);
    }

    [Fact]
    public void HoveringDeepLeafParent_Archive_ShouldHighlightAndOpenSubmenu()
    {
        var (uiRoot, host) = CreateUiRootWithHost();

        var archive = new MenuItem { Header = "Archive" };
        archive.Items.Add(new MenuItem { Header = "2026" });

        var inkkSlinger = new MenuItem { Header = "InkkSlinger" };
        inkkSlinger.Items.Add(new MenuItem { Header = "Roadmap.md" });
        inkkSlinger.Items.Add(new MenuItem { Header = "Bugs.csv" });
        inkkSlinger.Items.Add(archive);

        var design = new MenuItem { Header = "Design" };
        design.Items.Add(new MenuItem { Header = "Palette.json" });

        var projects = new MenuItem { Header = "Projects" };
        projects.Items.Add(inkkSlinger);
        projects.Items.Add(design);

        var recent = new MenuItem { Header = "Recent Files" };
        recent.Items.Add(new MenuItem { Header = "ReadMe.txt" });
        recent.Items.Add(new MenuItem { Header = "Schedule.xls" });
        recent.Items.Add(projects);

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "File" });
        menu.Items.Add(new MenuItem { Header = "Save" });
        menu.Items.Add(new MenuItem { Header = "SaveAs" });
        menu.Items.Add(recent);

        var button = new Button { Width = 220f, Height = 30f, Text = "Button with Context Menu" };
        host.AddChild(button);
        Canvas.SetLeft(button, 40f);
        Canvas.SetTop(button, 50f);
        ContextMenu.SetContextMenu(button, menu);
        RunLayout(uiRoot);

        var openPoint = new Vector2(60f, 60f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(openPoint, rightPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(openPoint, rightReleased: true, pointerMoved: false));
        RunLayout(uiRoot);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(recent.LayoutSlot.X + 4f, recent.LayoutSlot.Y + 4f), pointerMoved: true));
        RunLayout(uiRoot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(projects.LayoutSlot.X + 4f, projects.LayoutSlot.Y + 4f), pointerMoved: true));
        RunLayout(uiRoot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(inkkSlinger.LayoutSlot.X + 4f, inkkSlinger.LayoutSlot.Y + 4f), pointerMoved: true));
        RunLayout(uiRoot);
        Assert.True(inkkSlinger.IsHighlighted);
        Assert.True(inkkSlinger.IsSubmenuOpen);
        Assert.Same(inkkSlinger, archive.GetParentMenuItem());

        var hoverArchive = new Vector2(archive.LayoutSlot.X + 4f, archive.LayoutSlot.Y + 4f);
        Assert.True(menu.TryHitTestMenuItem(hoverArchive, out var resolvedArchive));
        Assert.Same(archive, resolvedArchive);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverArchive, pointerMoved: true));
        RunLayout(uiRoot);

        Assert.True(
            archive.IsHighlighted,
            $"Archive not highlighted. IsSubmenuOpen={archive.IsSubmenuOpen}, " +
            $"InkkSlingerHighlightedChild={inkkSlinger.GetHighlightedChild()?.Header ?? "<null>"}, " +
            $"ProjectsHighlightedChild={projects.GetHighlightedChild()?.Header ?? "<null>"}");
        Assert.True(archive.IsSubmenuOpen);
    }

    [Fact]
    public void HoverFirstParentImmediatelyAfterOpen_ShouldExpandWithoutNeedingExtraHoverPasses()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var firstParent = new MenuItem { Header = "Recent Files" };
        firstParent.Items.Add(new MenuItem { Header = "ReadMe.txt" });
        firstParent.Items.Add(new MenuItem { Header = "Schedule.xls" });

        var menu = new ContextMenu();
        menu.Items.Add(firstParent);
        menu.Items.Add(new MenuItem { Header = "Save" });

        var button = new Button { Width = 160f, Height = 30f, Text = "Open" };
        host.AddChild(button);
        Canvas.SetLeft(button, 40f);
        Canvas.SetTop(button, 50f);
        ContextMenu.SetContextMenu(button, menu);
        RunLayout(uiRoot);

        var openPoint = new Vector2(64f, 66f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(openPoint, rightPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(openPoint, rightReleased: true, pointerMoved: false));
        // Immediately hover first row area without waiting for another layout/update tick.
        var hoverFirstParent = new Vector2(openPoint.X + 10f, openPoint.Y + 12f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverFirstParent, pointerMoved: true));
        RunLayout(uiRoot);

        Assert.True(menu.IsOpen);
        Assert.True(firstParent.IsSubmenuOpen);
    }

    [Fact]
    public void ClickingContextMenuParentItem_ShouldNotChangeVisualOpenState()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var recent = new MenuItem { Header = "Recent Files" };
        recent.Items.Add(new MenuItem { Header = "ReadMe.txt" });
        recent.Items.Add(new MenuItem { Header = "Schedule.xls" });

        var file = new MenuItem { Header = "File" };
        var menu = new ContextMenu();
        menu.Items.Add(file);
        menu.Items.Add(recent);
        menu.OpenAt(host, left: 100f, top: 80f);
        RunLayout(uiRoot);

        var beforeFileHighlighted = file.IsHighlighted;
        var beforeRecentHighlighted = recent.IsHighlighted;
        var beforeRecentOpen = recent.IsSubmenuOpen;

        var clickPoint = new Vector2(recent.LayoutSlot.X + 4f, recent.LayoutSlot.Y + 4f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(clickPoint, leftPressed: true, pointerMoved: false));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(clickPoint, leftReleased: true, pointerMoved: false));
        RunLayout(uiRoot);

        Assert.Equal(beforeFileHighlighted, file.IsHighlighted);
        Assert.Equal(beforeRecentHighlighted, recent.IsHighlighted);
        Assert.Equal(beforeRecentOpen, recent.IsSubmenuOpen);
    }

    [Fact]
    public void RightClick_OpenContextMenu_ShouldNotAffectGridLayout()
    {
        var rootHost = new Panel { Width = 640f, Height = 360f };
        var grid = new Grid { Width = 640f, Height = 360f };
        var rowAuto = new RowDefinition { Height = GridLength.Auto };
        var rowStar = new RowDefinition { Height = GridLength.Star };
        grid.RowDefinitions.Add(rowAuto);
        grid.RowDefinitions.Add(rowStar);

        var button = new Button
        {
            Width = 200f,
            Height = 30f,
            Text = "Button with Context Menu"
        };
        Grid.SetRow(button, 0);
        grid.AddChild(button);
        grid.AddChild(new Border { Background = new Color(10, 20, 30), Height = 200f });
        Grid.SetRow(grid.Children[1], 1);
        rootHost.AddChild(grid);

        var uiRoot = new UiRoot(rootHost);
        RunLayout(uiRoot, 640, 360);
        var beforeAuto = rowAuto.ActualHeight;
        var beforeStar = rowStar.ActualHeight;

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "File" });
        menu.Items.Add(new MenuItem { Header = "Save" });
        menu.Items.Add(new MenuItem { Header = "SaveAs" });
        ContextMenu.SetContextMenu(button, menu);

        var pointer = new Vector2(20f, 15f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, rightPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, rightReleased: true));
        RunLayout(uiRoot, 640, 360);

        Assert.True(menu.IsOpen);
        Assert.Equal(beforeAuto, rowAuto.ActualHeight, 0.01f);
        Assert.Equal(beforeStar, rowStar.ActualHeight, 0.01f);
    }

    private static ContextMenu CreateSimpleContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "item" });
        return menu;
    }

    private static (UiRoot UiRoot, Canvas Host) CreateUiRootWithHost()
    {
        var host = new Canvas
        {
            Width = 400f,
            Height = 240f
        };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        return (uiRoot, host);
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
    }

    private static InputDelta CreateKeyDownDelta(Keys key, KeyboardState? keyboard = null)
    {
        var state = keyboard ?? default;
        var pointer = new Vector2(12f, 12f);
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(state, default, pointer),
            PressedKeys = new List<Keys> { key },
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool leftPressed = false, bool leftReleased = false, bool rightPressed = false, bool rightReleased = false, bool pointerMoved = true)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = rightPressed,
            RightReleased = rightReleased,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width = 400, int height = 240)
    {
        const string inputPipelineVariable = "INKKSLINGER_ENABLE_INPUT_PIPELINE";
        var previous = Environment.GetEnvironmentVariable(inputPipelineVariable);
        Environment.SetEnvironmentVariable(inputPipelineVariable, "0");
        try
        {
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, width, height));
        }
        finally
        {
            Environment.SetEnvironmentVariable(inputPipelineVariable, previous);
        }
    }
}
