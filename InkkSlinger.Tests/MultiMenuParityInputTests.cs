using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class MultiMenuParityInputTests
{
    [Fact]
    public void F10_UsesFocusedMenuScopeBeforeHigherZSiblingMenu()
    {
        var fixture = CreateFixture();
        fixture.UiRoot.SetFocusedElementForTests(fixture.LeftMenu);

        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F10));

        Assert.True(fixture.LeftMenu.IsMenuMode);
        Assert.True(fixture.LeftFile.IsHighlighted);
        Assert.False(fixture.RightMenu.IsMenuMode);
    }

    [Fact]
    public void AltAccessKey_UsesFocusedMenuScopeWhenTopLevelKeysConflict()
    {
        var fixture = CreateFixture();
        fixture.UiRoot.SetFocusedElementForTests(fixture.LeftMenu);

        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F, new KeyboardState(Keys.LeftAlt)));

        Assert.True(fixture.LeftMenu.IsMenuMode);
        Assert.True(fixture.LeftFile.IsSubmenuOpen);
        Assert.False(fixture.RightFile.IsSubmenuOpen);
    }

    [Fact]
    public void MenuModeKeyRouting_StaysBoundToActiveMenu()
    {
        var fixture = CreateFixture();
        fixture.UiRoot.SetFocusedElementForTests(fixture.LeftMenu);

        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F, new KeyboardState(Keys.LeftAlt)));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));

        Assert.True(fixture.LeftEdit.IsHighlighted);
        Assert.True(fixture.LeftEdit.IsSubmenuOpen);
        Assert.False(fixture.RightEdit.IsHighlighted);
        Assert.False(fixture.RightEdit.IsSubmenuOpen);
    }

    private static Fixture CreateFixture()
    {
        FocusManager.ClearFocus();
        FocusManager.ClearPointerCapture();

        var root = new Canvas
        {
            Width = 960f,
            Height = 320f
        };

        var leftMenu = BuildMenu("_File", "_Edit", out var leftFile, out var leftEdit);
        root.AddChild(leftMenu);
        Canvas.SetLeft(leftMenu, 20f);
        Canvas.SetTop(leftMenu, 20f);
        Panel.SetZIndex(leftMenu, 1);

        var rightMenu = BuildMenu("_File", "_Edit", out var rightFile, out var rightEdit);
        root.AddChild(rightMenu);
        Canvas.SetLeft(rightMenu, 520f);
        Canvas.SetTop(rightMenu, 20f);
        Panel.SetZIndex(rightMenu, 10);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot);
        return new Fixture(uiRoot, leftMenu, rightMenu, leftFile, leftEdit, rightFile, rightEdit);
    }

    private static Menu BuildMenu(
        string fileHeader,
        string editHeader,
        out MenuItem file,
        out MenuItem edit)
    {
        var menu = new Menu
        {
            Width = 300f,
            Height = 30f
        };

        file = new MenuItem { Header = fileHeader };
        file.Items.Add(new MenuItem { Header = "New" });
        file.Items.Add(new MenuItem { Header = "Open" });

        edit = new MenuItem { Header = editHeader };
        edit.Items.Add(new MenuItem { Header = "Undo" });

        menu.Items.Add(file);
        menu.Items.Add(edit);
        return menu;
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

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 960, 320));
    }

    private sealed class Fixture
    {
        public Fixture(
            UiRoot uiRoot,
            Menu leftMenu,
            Menu rightMenu,
            MenuItem leftFile,
            MenuItem leftEdit,
            MenuItem rightFile,
            MenuItem rightEdit)
        {
            UiRoot = uiRoot;
            LeftMenu = leftMenu;
            RightMenu = rightMenu;
            LeftFile = leftFile;
            LeftEdit = leftEdit;
            RightFile = rightFile;
            RightEdit = rightEdit;
        }

        public UiRoot UiRoot { get; }
        public Menu LeftMenu { get; }
        public Menu RightMenu { get; }
        public MenuItem LeftFile { get; }
        public MenuItem LeftEdit { get; }
        public MenuItem RightFile { get; }
        public MenuItem RightEdit { get; }
    }
}
