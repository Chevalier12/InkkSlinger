using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class MenuParityInputTests
{
    [Fact]
    public void AltF_OpensFileMenu_WhenFocusIsTextBox()
    {
        var fixture = CreateFixture();
        FocusByClick(fixture.UiRoot, fixture.EditorTextBox);

        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F, new KeyboardState(Keys.LeftAlt)));

        Assert.True(fixture.Menu.IsMenuMode);
        Assert.True(fixture.FileMenuItem.IsSubmenuOpen);
    }

    [Fact]
    public void F10_ActivatesMenuBar_AndHighlightsFirstTopLevel()
    {
        var fixture = CreateFixture();
        FocusByClick(fixture.UiRoot, fixture.EditorTextBox);

        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F10));

        Assert.True(fixture.Menu.IsMenuMode);
        Assert.True(fixture.FileMenuItem.IsHighlighted);
        Assert.False(fixture.FileMenuItem.IsSubmenuOpen);
    }

    [Fact]
    public void Right_FromTopLevel_MovesToNextTopLevel_AndOpensSubmenu()
    {
        var fixture = CreateFixture();
        FocusByClick(fixture.UiRoot, fixture.EditorTextBox);

        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F10));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));

        Assert.False(fixture.FileMenuItem.IsHighlighted);
        Assert.True(fixture.EditMenuItem.IsHighlighted);
        Assert.True(fixture.EditMenuItem.IsSubmenuOpen);
    }

    [Fact]
    public void NestedRight_OpensChildSubmenu_AndHighlightsFirstChild()
    {
        var fixture = CreateFixture();
        FocusByClick(fixture.UiRoot, fixture.EditorTextBox);

        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F, new KeyboardState(Keys.LeftAlt)));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));

        Assert.True(fixture.NewMenuItem.IsSubmenuOpen);
        Assert.True(fixture.ProjectMenuItem.IsHighlighted);
    }

    [Fact]
    public void NestedLeft_ClosesCurrentLevel_AndReturnsHighlightToParent()
    {
        var fixture = CreateFixture();
        FocusByClick(fixture.UiRoot, fixture.EditorTextBox);

        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F, new KeyboardState(Keys.LeftAlt)));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Left));

        Assert.False(fixture.ConsoleMenuItem.IsHighlighted);
        Assert.True(fixture.ProjectMenuItem.IsHighlighted);
    }

    [Fact]
    public void Right_FromFirstLevelLeaf_MovesAcrossTopLevel_AndOpensTargetSubmenu()
    {
        var fixture = CreateFixture();
        FocusByClick(fixture.UiRoot, fixture.EditorTextBox);

        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.E, new KeyboardState(Keys.LeftAlt)));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));

        Assert.True(fixture.Menu.IsMenuMode);
        Assert.False(fixture.EditMenuItem.IsHighlighted);
        Assert.True(fixture.FileMenuItem.IsHighlighted);
        Assert.True(fixture.FileMenuItem.IsSubmenuOpen);
    }

    [Fact]
    public void Left_FromFirstLevelLeaf_MovesAcrossTopLevel_AndOpensTargetSubmenu()
    {
        var fixture = CreateFixture();
        FocusByClick(fixture.UiRoot, fixture.EditorTextBox);

        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.E, new KeyboardState(Keys.LeftAlt)));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Left));

        Assert.True(fixture.Menu.IsMenuMode);
        Assert.False(fixture.EditMenuItem.IsHighlighted);
        Assert.True(fixture.FileMenuItem.IsHighlighted);
        Assert.True(fixture.FileMenuItem.IsSubmenuOpen);
    }

    [Fact]
    public void Enter_OnLeaf_ExecutesOnce_AndClosesMenus()
    {
        var fixture = CreateFixture();
        FocusByClick(fixture.UiRoot, fixture.EditorTextBox);

        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F, new KeyboardState(Keys.LeftAlt)));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Enter));

        Assert.Equal(1, fixture.ConsoleCommandExecutions);
        Assert.False(fixture.Menu.IsMenuMode);
        Assert.False(fixture.FileMenuItem.IsSubmenuOpen);
    }

    [Fact]
    public void Escape_ClosesMenu_AndRestoresPreviousFocus()
    {
        var fixture = CreateFixture();
        FocusByClick(fixture.UiRoot, fixture.EditorTextBox);
        FocusByClick(fixture.UiRoot, fixture.SideTextBox);

        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F, new KeyboardState(Keys.LeftAlt)));
        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Escape));

        Assert.False(fixture.Menu.IsMenuMode);
        Assert.Same(fixture.SideTextBox, FocusManager.GetFocusedElement());
    }

    [Fact]
    public void OutsideClick_ClosesActiveMenuMode()
    {
        var fixture = CreateFixture();
        FocusByClick(fixture.UiRoot, fixture.EditorTextBox);

        fixture.UiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.F, new KeyboardState(Keys.LeftAlt)));
        Click(fixture.UiRoot, fixture.SideTextBox);

        Assert.False(fixture.Menu.IsMenuMode);
    }

    [Theory]
    [InlineData("_File", "File")]
    [InlineData("E_xit", "Exit")]
    [InlineData("__Literal", "_Literal")]
    [InlineData("NoMarkers", "NoMarkers")]
    public void AccessTextParser_StripsMarkers(string input, string expected)
    {
        Assert.Equal(expected, MenuAccessText.StripAccessMarkers(input));
    }

    private static void FocusByClick(UiRoot uiRoot, UIElement target)
    {
        Click(uiRoot, target);
        Assert.Same(target, FocusManager.GetFocusedElement());
    }

    private static void Click(UiRoot uiRoot, UIElement target)
    {
        var point = GetCenter(target);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, leftReleased: true));
    }

    private static Vector2 GetCenter(UIElement element)
    {
        return new Vector2(
            element.LayoutSlot.X + (element.LayoutSlot.Width * 0.5f),
            element.LayoutSlot.Y + (element.LayoutSlot.Height * 0.5f));
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

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool leftPressed = false, bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = true,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static Fixture CreateFixture()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30f) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var editorTextBox = new TextBox();
        Grid.SetRow(editorTextBox, 1);
        Grid.SetColumn(editorTextBox, 0);
        root.AddChild(editorTextBox);

        var sideTextBox = new TextBox();
        Grid.SetRow(sideTextBox, 1);
        Grid.SetColumn(sideTextBox, 1);
        root.AddChild(sideTextBox);

        var menu = new Menu();
        Grid.SetRow(menu, 0);
        Grid.SetColumn(menu, 0);
        Grid.SetColumnSpan(menu, 2);
        root.AddChild(menu);

        var file = new MenuItem { Header = "_File" };
        var @new = new MenuItem { Header = "_New" };
        var project = new MenuItem { Header = "_Project" };
        var console = new MenuItem { Header = "_Console App" };
        var classLibrary = new MenuItem { Header = "_Class Library" };
        project.Items.Add(console);
        project.Items.Add(classLibrary);
        @new.Items.Add(project);
        file.Items.Add(@new);

        var edit = new MenuItem { Header = "_Edit" };
        edit.Items.Add(new MenuItem { Header = "_Undo" });

        menu.Items.Add(file);
        menu.Items.Add(edit);

        var executions = 0;
        var openConsoleCommand = new RoutedCommand("OpenConsole", typeof(MenuParityInputTests));
        root.CommandBindings.Add(new CommandBinding(openConsoleCommand, (_, _) => executions++));
        console.Command = openConsoleCommand;
        console.CommandTarget = root;

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 960, 640, 16);

        return new Fixture(uiRoot, menu, file, edit, @new, project, console, editorTextBox, sideTextBox, () => executions);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        const string inputPipelineVariable = "INKKSLINGER_ENABLE_INPUT_PIPELINE";
        var previous = Environment.GetEnvironmentVariable(inputPipelineVariable);
        Environment.SetEnvironmentVariable(inputPipelineVariable, "0");
        try
        {
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
                new Viewport(0, 0, width, height));
        }
        finally
        {
            Environment.SetEnvironmentVariable(inputPipelineVariable, previous);
        }
    }

    private sealed class Fixture
    {
        private readonly Func<int> _executionCountProvider;

        public Fixture(
            UiRoot uiRoot,
            Menu menu,
            MenuItem fileMenuItem,
            MenuItem editMenuItem,
            MenuItem newMenuItem,
            MenuItem projectMenuItem,
            MenuItem consoleMenuItem,
            TextBox editorTextBox,
            TextBox sideTextBox,
            Func<int> executionCountProvider)
        {
            UiRoot = uiRoot;
            Menu = menu;
            FileMenuItem = fileMenuItem;
            EditMenuItem = editMenuItem;
            NewMenuItem = newMenuItem;
            ProjectMenuItem = projectMenuItem;
            ConsoleMenuItem = consoleMenuItem;
            EditorTextBox = editorTextBox;
            SideTextBox = sideTextBox;
            _executionCountProvider = executionCountProvider;
        }

        public UiRoot UiRoot { get; }
        public Menu Menu { get; }
        public MenuItem FileMenuItem { get; }
        public MenuItem EditMenuItem { get; }
        public MenuItem NewMenuItem { get; }
        public MenuItem ProjectMenuItem { get; }
        public MenuItem ConsoleMenuItem { get; }
        public TextBox EditorTextBox { get; }
        public TextBox SideTextBox { get; }

        public int ConsoleCommandExecutions => _executionCountProvider();
    }
}
