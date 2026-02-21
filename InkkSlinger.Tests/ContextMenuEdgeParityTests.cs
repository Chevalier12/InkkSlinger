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

    [Fact]
    public void OpenAtPointer_NearViewportEdge_ShouldClampRootMenuIntoWorkArea()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var menu = new ContextMenu();
        for (var i = 0; i < 8; i++)
        {
            menu.Items.Add(new MenuItem { Header = $"Item {i}" });
        }

        var pointer = new Vector2(host.LayoutSlot.X + host.LayoutSlot.Width - 2f, host.LayoutSlot.Y + host.LayoutSlot.Height - 2f);
        menu.OpenAtPointer(host, pointer, placementTarget: null);
        RunLayout(uiRoot);

        Assert.True(menu.Left >= host.LayoutSlot.X - 0.01f);
        Assert.True(menu.Top >= host.LayoutSlot.Y - 0.01f);
        Assert.True(menu.Left + menu.Width <= host.LayoutSlot.X + host.LayoutSlot.Width + 0.01f);
        Assert.True(menu.Top + menu.Height <= host.LayoutSlot.Y + host.LayoutSlot.Height + 0.01f);
    }

    [Fact]
    public void Submenu_NearRightEdge_ShouldFlipLeft()
    {
        var (uiRoot, host) = CreateUiRootWithHost();
        var parent = new MenuItem { Header = "Recent Files" };
        parent.Items.Add(new MenuItem { Header = "ReadMe.txt" });
        parent.Items.Add(new MenuItem { Header = "Schedule.xls" });

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "File" });
        menu.Items.Add(parent);
        menu.OpenAt(host, left: host.LayoutSlot.Width - 90f, top: 80f);
        RunLayout(uiRoot);

        var hoverParent = new Vector2(parent.LayoutSlot.X + 3f, parent.LayoutSlot.Y + 3f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverParent, pointerMoved: true));
        RunLayout(uiRoot);

        Assert.True(parent.IsSubmenuOpen);
        var firstChild = parent.GetFirstChildMenuItem();
        Assert.NotNull(firstChild);
        Assert.True(firstChild.LayoutSlot.X < parent.LayoutSlot.X);
    }

    [Fact]
    public void Submenu_ShouldClampVerticallyIntoViewport()
    {
        var host = new Canvas { Width = 260f, Height = 120f };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 260, 120);

        var parent = new MenuItem { Header = "Recent Files" };
        for (var i = 0; i < 10; i++)
        {
            parent.Items.Add(new MenuItem { Header = $"Child {i}" });
        }

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "File" });
        menu.Items.Add(parent);
        menu.OpenAt(host, left: 140f, top: 95f);
        RunLayout(uiRoot, 260, 120);

        var hoverParent = new Vector2(parent.LayoutSlot.X + 3f, parent.LayoutSlot.Y + 3f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverParent, pointerMoved: true));
        RunLayout(uiRoot, 260, 120);

        Assert.True(parent.IsSubmenuOpen);
        Assert.True(parent.TryGetOpenSubmenuBounds(out var submenuBounds));
        Assert.True(submenuBounds.Y >= host.LayoutSlot.Y - 0.01f);
        Assert.True(submenuBounds.Y + submenuBounds.Height <= host.LayoutSlot.Y + host.LayoutSlot.Height + 0.01f);
    }

    [Fact]
    public void SubmenuOverflow_ShouldScrollOnMouseWheel()
    {
        var host = new Canvas { Width = 260f, Height = 120f };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 260, 120);

        var parent = new MenuItem { Header = "Recent Files" };
        for (var i = 0; i < 14; i++)
        {
            parent.Items.Add(new MenuItem { Header = $"Child {i}" });
        }

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "File" });
        menu.Items.Add(parent);
        menu.OpenAt(host, left: 120f, top: 50f);
        RunLayout(uiRoot, 260, 120);

        var hoverParent = new Vector2(parent.LayoutSlot.X + 3f, parent.LayoutSlot.Y + 3f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(hoverParent, pointerMoved: true));
        RunLayout(uiRoot, 260, 120);

        Assert.True(parent.IsSubmenuOpen);
        var firstChildBeforeScroll = parent.GetFirstChildMenuItem();
        Assert.NotNull(firstChildBeforeScroll);
        var beforeY = firstChildBeforeScroll.LayoutSlot.Y;

        var wheelPointer = new Vector2(firstChildBeforeScroll.LayoutSlot.X + 4f, firstChildBeforeScroll.LayoutSlot.Y + 4f);
        var wheelDelta = new InputDelta
        {
            Previous = new InputSnapshot(default, default, wheelPointer),
            Current = new InputSnapshot(default, default, wheelPointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = false,
            WheelDelta = -120,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };

        uiRoot.RunInputDeltaForTests(wheelDelta);
        RunLayout(uiRoot, 260, 120);

        var firstChildAfterScroll = parent.GetFirstChildMenuItem();
        Assert.NotNull(firstChildAfterScroll);
        Assert.True(firstChildAfterScroll.LayoutSlot.Y < beforeY);
    }

    [Fact]
    public void DeepClampedSubmenus_ShouldKeepHierarchyReadable_ByAvoidingFullColumnOverlap()
    {
        var host = new Canvas { Width = 220f, Height = 140f };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 220, 140);

        var leaf = new MenuItem { Header = "Leaf" };
        var level3 = new MenuItem { Header = "Level3" };
        level3.Items.Add(leaf);

        var level2 = new MenuItem { Header = "Level2" };
        level2.Items.Add(level3);

        var level1 = new MenuItem { Header = "Level1" };
        level1.Items.Add(level2);

        var menu = new ContextMenu();
        menu.Items.Add(level1);
        menu.OpenAt(host, left: 180f, top: 40f);
        RunLayout(uiRoot, 220, 140);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(level1.LayoutSlot.X + 2f, level1.LayoutSlot.Y + 2f), pointerMoved: true));
        RunLayout(uiRoot, 220, 140);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(level2.LayoutSlot.X + 2f, level2.LayoutSlot.Y + 2f), pointerMoved: true));
        RunLayout(uiRoot, 220, 140);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(level3.LayoutSlot.X + 2f, level3.LayoutSlot.Y + 2f), pointerMoved: true));
        RunLayout(uiRoot, 220, 140);

        Assert.True(level1.TryGetOpenSubmenuBounds(out var b1));
        Assert.True(level2.TryGetOpenSubmenuBounds(out var b2));
        Assert.True(level3.TryGetOpenSubmenuBounds(out var b3));

        Assert.False(MathF.Abs(b1.X - b2.X) < 0.5f && MathF.Abs(b1.Y - b2.Y) < 0.5f);
        Assert.False(MathF.Abs(b2.X - b3.X) < 0.5f && MathF.Abs(b2.Y - b3.Y) < 0.5f);
    }

    [Fact]
    public void DeepSubmenus_WithNoOverflow_ShouldNotStaircaseVertically()
    {
        var host = new Canvas { Width = 1200f, Height = 600f };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 1200, 600);

        var level4 = new MenuItem { Header = "Level4" };
        level4.Items.Add(new MenuItem { Header = "Leaf" });

        var level3 = new MenuItem { Header = "Level3" };
        level3.Items.Add(level4);
        level3.Items.Add(new MenuItem { Header = "Sibling3" });

        var level2 = new MenuItem { Header = "Level2" };
        level2.Items.Add(level3);
        level2.Items.Add(new MenuItem { Header = "Sibling2" });

        var level1 = new MenuItem { Header = "Level1" };
        level1.Items.Add(level2);
        level1.Items.Add(new MenuItem { Header = "Sibling1" });

        var menu = new ContextMenu();
        menu.Items.Add(level1);
        menu.OpenAt(host, left: 80f, top: 80f);
        RunLayout(uiRoot, 1200, 600);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(level1.LayoutSlot.X + 2f, level1.LayoutSlot.Y + 2f), pointerMoved: true));
        RunLayout(uiRoot, 1200, 600);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(level2.LayoutSlot.X + 2f, level2.LayoutSlot.Y + 2f), pointerMoved: true));
        RunLayout(uiRoot, 1200, 600);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(level3.LayoutSlot.X + 2f, level3.LayoutSlot.Y + 2f), pointerMoved: true));
        RunLayout(uiRoot, 1200, 600);

        Assert.True(level1.TryGetOpenSubmenuBounds(out var b1));
        Assert.True(level2.TryGetOpenSubmenuBounds(out var b2));
        Assert.True(level3.TryGetOpenSubmenuBounds(out var b3));

        Assert.Equal(b1.Y, b2.Y, 0.5f);
        Assert.Equal(b2.Y, b3.Y, 0.5f);
    }

    [Fact]
    public void DeepSubmenus_WithOverflow_ShouldOpenUnderRow_AndAlternateHorizontalDirection()
    {
        var host = new Canvas { Width = 220f, Height = 140f };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 220, 140);
        const string wide = "ExtremelyWideMenuHeaderToForceClamp";

        var level4 = new MenuItem { Header = wide + "4" };
        level4.Items.Add(new MenuItem { Header = wide + "Leaf" });

        var level3 = new MenuItem { Header = wide + "3" };
        level3.Items.Add(level4);

        var level2 = new MenuItem { Header = wide + "2" };
        level2.Items.Add(level3);

        var level1 = new MenuItem { Header = wide + "1" };
        level1.Items.Add(level2);

        var menu = new ContextMenu();
        menu.Items.Add(level1);
        menu.OpenAt(host, left: 180f, top: 32f);
        RunLayout(uiRoot, 220, 140);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
        RunLayout(uiRoot, 220, 140);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
        RunLayout(uiRoot, 220, 140);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
        RunLayout(uiRoot, 220, 140);

        Assert.True(level1.TryGetOpenSubmenuBounds(out var b1));
        Assert.True(level2.TryGetOpenSubmenuBounds(out var b2));
        Assert.True(level3.TryGetOpenSubmenuBounds(out var b3));

        // Overflow collision path should place deeper submenus under the triggering row.
        Assert.True(
            b2.Y >= level2.LayoutSlot.Y + level2.LayoutSlot.Height - 0.5f ||
            b3.Y >= level3.LayoutSlot.Y + level3.LayoutSlot.Height - 0.5f,
            $"Expected under-row placement. b2.Y={b2.Y:0.##}, l2Bottom={(level2.LayoutSlot.Y + level2.LayoutSlot.Height):0.##}, b3.Y={b3.Y:0.##}, l3Bottom={(level3.LayoutSlot.Y + level3.LayoutSlot.Height):0.##}");

        var overlap12X = MathF.Max(0f, MathF.Min(b1.X + b1.Width, b2.X + b2.Width) - MathF.Max(b1.X, b2.X));
        var overlap12Y = MathF.Max(0f, MathF.Min(b1.Y + b1.Height, b2.Y + b2.Height) - MathF.Max(b1.Y, b2.Y));
        var overlap23X = MathF.Max(0f, MathF.Min(b2.X + b2.Width, b3.X + b3.Width) - MathF.Max(b2.X, b3.X));
        var overlap23Y = MathF.Max(0f, MathF.Min(b2.Y + b2.Height, b3.Y + b3.Height) - MathF.Max(b2.Y, b3.Y));
        Assert.True(overlap12X * overlap12Y < b2.Width * b2.Height - 0.5f);
        Assert.True(overlap23X * overlap23Y < b3.Width * b3.Height - 0.5f);
    }

    [Fact]
    public void DeepOverflowChain_ShouldAvoidFullOverlapAcrossVisibleOpenPanels()
    {
        var host = new Canvas { Width = 560f, Height = 240f };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 560, 240);

        MenuItem BuildSingleChildChain(string prefix, int depth)
        {
            var root = new MenuItem { Header = $"{prefix}-0" };
            var current = root;
            for (var i = 1; i < depth; i++)
            {
                var next = new MenuItem { Header = $"{prefix}-{i}" };
                current.Items.Add(next);
                current = next;
            }

            current.Items.Add(new MenuItem { Header = $"{prefix}-leaf" });
            return root;
        }

        var chain = BuildSingleChildChain("VeryWideHeaderThatForcesReposition", 7);
        var menu = new ContextMenu();
        menu.Items.Add(chain);
        menu.OpenAt(host, left: 470f, top: 58f);
        RunLayout(uiRoot, 560, 240);

        // Open the deep chain deterministically by keyboard.
        for (var i = 0; i < 6; i++)
        {
            uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
            RunLayout(uiRoot, 560, 240);
        }

        var openPanels = new List<LayoutRect>();
        for (var current = chain; current != null; current = current.GetFirstChildMenuItem())
        {
            if (current.TryGetOpenSubmenuBounds(out var bounds))
            {
                openPanels.Add(bounds);
            }
            else
            {
                break;
            }
        }

        Assert.True(openPanels.Count >= 3);
        for (var i = 1; i < openPanels.Count; i++)
        {
            var previous = openPanels[i - 1];
            var current = openPanels[i];
            var overlapX = MathF.Max(0f, MathF.Min(previous.X + previous.Width, current.X + current.Width) - MathF.Max(previous.X, current.X));
            var overlapY = MathF.Max(0f, MathF.Min(previous.Y + previous.Height, current.Y + current.Height) - MathF.Max(previous.Y, current.Y));
            var overlapArea = overlapX * overlapY;
            Assert.True(overlapArea < current.Width * current.Height - 0.5f);
        }
    }

    [Fact]
    public void HoverOpenedOverflowCollision_ShouldFlipDirectionAndStepDown()
    {
        var host = new Canvas { Width = 560f, Height = 240f };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 560, 240);

        MenuItem BuildChain(string prefix, int depth)
        {
            var root = new MenuItem { Header = $"{prefix}-0-ExtremelyWideLabelToForceEdgeHandling" };
            var current = root;
            for (var i = 1; i < depth; i++)
            {
                var next = new MenuItem { Header = $"{prefix}-{i}-ExtremelyWideLabelToForceEdgeHandling" };
                current.Items.Add(next);
                current = next;
            }

            return root;
        }

        var chain = BuildChain("Chain", 6);
        var menu = new ContextMenu();
        menu.Items.Add(chain);
        menu.OpenAt(host, left: 470f, top: 58f);
        RunLayout(uiRoot, 560, 240);

        var opened = new List<MenuItem>();
        var currentParent = chain;
        for (var i = 0; i < 5; i++)
        {
            var hover = new Vector2(currentParent.LayoutSlot.X + 3f, currentParent.LayoutSlot.Y + 3f);
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(hover, pointerMoved: true));
            RunLayout(uiRoot, 560, 240);
            opened.Add(currentParent);
            currentParent = currentParent.GetFirstChildMenuItem()!;
        }

        Assert.True(opened.Count >= 3);
        Assert.True(opened[0].TryGetOpenSubmenuBounds(out var p0));
        Assert.True(opened[1].TryGetOpenSubmenuBounds(out var p1));
        Assert.True(opened[2].TryGetOpenSubmenuBounds(out var p2));

        var movedDown = p1.Y > p0.Y + 0.5f || p2.Y > p1.Y + 0.5f;
        Assert.True(movedDown);

        var d01 = p1.X - p0.X;
        var d12 = p2.X - p1.X;
        if (MathF.Abs(d01) > 0.5f && MathF.Abs(d12) > 0.5f)
        {
            Assert.True(MathF.Sign(d01) != MathF.Sign(d12));
        }
    }

    [Fact]
    public void HoveringSprintOverflowLeaf_ShouldNotFullyOverlapParentPanel()
    {
        var host = new Canvas { Width = 620f, Height = 260f };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 620, 260);

        var day01 = new MenuItem { Header = "Day 01" };
        day01.Items.Add(new MenuItem { Header = "Focus" });

        var sprint = new MenuItem { Header = "Sprint 01" };
        sprint.Items.Add(day01);

        var notes = new MenuItem { Header = "Notes" };
        notes.Items.Add(sprint);

        var year = new MenuItem { Header = "2026" };
        year.Items.Add(notes);

        var archive = new MenuItem { Header = "Archive" };
        archive.Items.Add(year);

        var menu = new ContextMenu();
        menu.Items.Add(archive);
        menu.OpenAt(host, left: 510f, top: 70f);
        RunLayout(uiRoot, 620, 260);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(archive.LayoutSlot.X + 3f, archive.LayoutSlot.Y + 3f), pointerMoved: true));
        RunLayout(uiRoot, 620, 260);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(year.LayoutSlot.X + 3f, year.LayoutSlot.Y + 3f), pointerMoved: true));
        RunLayout(uiRoot, 620, 260);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(notes.LayoutSlot.X + 3f, notes.LayoutSlot.Y + 3f), pointerMoved: true));
        RunLayout(uiRoot, 620, 260);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(sprint.LayoutSlot.X + 3f, sprint.LayoutSlot.Y + 3f), pointerMoved: true));
        RunLayout(uiRoot, 620, 260);

        Assert.True(notes.TryGetOpenSubmenuBounds(out var notesBounds));
        Assert.True(sprint.TryGetOpenSubmenuBounds(out var sprintBounds));

        var overlapX = MathF.Max(0f, MathF.Min(notesBounds.X + notesBounds.Width, sprintBounds.X + sprintBounds.Width) - MathF.Max(notesBounds.X, sprintBounds.X));
        var overlapY = MathF.Max(0f, MathF.Min(notesBounds.Y + notesBounds.Height, sprintBounds.Y + sprintBounds.Height) - MathF.Max(notesBounds.Y, sprintBounds.Y));
        var overlapArea = overlapX * overlapY;
        Assert.True(overlapArea < sprintBounds.Width * sprintBounds.Height - 0.5f);

        // Ensure it still stays near the chain (not teleported away).
        Assert.True(MathF.Abs(sprintBounds.Y - notesBounds.Y) < 120f);
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
