using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class MenuTests
{
    public MenuTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void XamlLoader_Parses_Menu_And_MenuItems()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Menu x:Name="MainMenu">
                                <MenuItem Header="File">
                                  <MenuItem Header="New" />
                                  <MenuItem Header="Open" />
                                </MenuItem>
                                <MenuItem Header="Edit" />
                              </Menu>
                            </UserControl>
                            """;

        var codeBehind = new MenuCodeBehind();
        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, codeBehind);

        Assert.NotNull(codeBehind.MainMenu);
        Assert.Equal(2, codeBehind.MainMenu!.Items.Count);

        var file = Assert.IsType<MenuItem>(codeBehind.MainMenu.Items[0]);
        Assert.Equal("File", file.Header);
        Assert.Equal(2, file.Items.Count);
    }

    [Fact]
    public void MenuItem_EnterOnLeaf_RaisesClick_AndClosesMenuMode()
    {
        var root = new Panel { Width = 800f, Height = 600f };
        var menu = new Menu();
        var file = new TestMenuItem { Header = "File" };
        var open = new TestMenuItem { Header = "Open" };
        file.Items.Add(open);
        menu.Items.Add(file);
        root.AddChild(menu);

        root.Measure(new Vector2(800f, 600f));
        root.Arrange(new LayoutRect(0f, 0f, 800f, 600f));

        var clickRaised = false;
        open.Click += (_, _) => clickRaised = true;

        file.FireLeftClick();
        Assert.True(file.IsSubmenuOpen);
        Assert.True(menu.IsMenuMode);

        file.FireKeyDown(Keys.Down);
        Assert.Same(open, FocusManager.FocusedElement);

        open.FireKeyDown(Keys.Enter);

        Assert.True(clickRaised);
        Assert.False(file.IsSubmenuOpen);
        Assert.False(menu.IsMenuMode);
    }

    [Fact]
    public void MenuMode_ClickOutside_ClosesOpenSubmenu()
    {
        var root = new Panel { Width = 640f, Height = 420f };
        var menu = new Menu();
        var file = new TestMenuItem { Header = "File" };
        file.Items.Add(new TestMenuItem { Header = "Save" });
        menu.Items.Add(file);

        var outside = new TestOutsideTarget
        {
            Width = 100f,
            Height = 60f,
            Margin = new Thickness(0f, 120f, 0f, 0f)
        };

        root.AddChild(menu);
        root.AddChild(outside);

        root.Measure(new Vector2(640f, 420f));
        root.Arrange(new LayoutRect(0f, 0f, 640f, 420f));

        file.FireLeftClick();
        Assert.True(file.IsSubmenuOpen);

        outside.FireLeftDown(new Vector2(outside.LayoutSlot.X + 4f, outside.LayoutSlot.Y + 4f));

        Assert.False(file.IsSubmenuOpen);
        Assert.False(menu.IsMenuMode);
    }

    [Fact]
    public void Menu_EntersMenuMode_RaisesZIndex_AndRestoresOnClose()
    {
        var root = new Panel { Width = 640f, Height = 420f };
        var menu = new Menu();
        var file = new TestMenuItem { Header = "File" };
        file.Items.Add(new TestMenuItem { Header = "Open" });

        var toolbar = new Border { Width = 640f, Height = 30f, Margin = new Thickness(0f, 40f, 0f, 0f) };

        menu.Items.Add(file);
        root.AddChild(menu);
        root.AddChild(toolbar);

        root.Measure(new Vector2(640f, 420f));
        root.Arrange(new LayoutRect(0f, 0f, 640f, 420f));

        Assert.Equal(0, Panel.GetZIndex(menu));

        file.FireLeftClick();
        Assert.True(menu.IsMenuMode);
        Assert.True(Panel.GetZIndex(menu) > Panel.GetZIndex(toolbar));

        menu.CloseAllSubmenus();
        Assert.Equal(0, Panel.GetZIndex(menu));
    }

    private sealed class MenuCodeBehind
    {
        public Menu? MainMenu { get; set; }
    }

    private sealed class TestMenuItem : MenuItem
    {
        public void FireLeftClick()
        {
            var point = new Vector2(LayoutSlot.X + 4f, LayoutSlot.Y + 4f);
            RaisePreviewMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
            RaisePreviewMouseUp(point, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseUp(point, MouseButton.Left, 1, ModifierKeys.None);
        }

        public void FireKeyDown(Keys key)
        {
            RaisePreviewKeyDown(key, false, ModifierKeys.None);
            RaiseKeyDown(key, false, ModifierKeys.None);
        }
    }

    private sealed class TestOutsideTarget : Border
    {
        public void FireLeftDown(Vector2 point)
        {
            RaisePreviewMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
        }
    }
}
