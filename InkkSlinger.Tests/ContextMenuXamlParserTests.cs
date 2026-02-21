using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ContextMenuXamlParserTests
{
    [Fact]
    public void ButtonContextMenuPropertyElement_WithNameAliases_ShouldParseAndAttach()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <Button Name="cmButton" Text="Button with Context Menu" Height="30" Width="200">
      <Button.ContextMenu>
        <ContextMenu Name="cm" StaysOpen="true">
          <MenuItem Header="File"/>
          <MenuItem Header="Save"/>
          <MenuItem Header="SaveAs"/>
          <MenuItem Header="Recent Files">
            <MenuItem Header="ReadMe.txt"/>
            <MenuItem Header="Schedule.xls"/>
          </MenuItem>
        </ContextMenu>
      </Button.ContextMenu>
    </Button>
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var button = Assert.IsType<Button>(root.FindName("cmButton"));
        var menu = Assert.IsType<ContextMenu>(ContextMenu.GetContextMenu(button));
        var namedMenu = Assert.IsType<ContextMenu>(root.FindName("cm"));

        Assert.Same(namedMenu, menu);
        Assert.True(menu.StaysOpen);
        Assert.Equal(4, menu.Items.Count);
    }

    [Fact]
    public void FrameworkElementContextMenuAttribute_WithStaticResource_ShouldParseAndAttach()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <ContextMenu x:Key="ParityContextMenu">
      <MenuItem Header="File" />
      <MenuItem Header="Save" />
    </ContextMenu>
  </UserControl.Resources>
  <Grid>
    <Button Name="cmButton"
            Text="Button with Context Menu"
            Height="30"
            Width="200"
            ContextMenu="{StaticResource ParityContextMenu}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var button = Assert.IsType<Button>(root.FindName("cmButton"));
        var menu = Assert.IsType<ContextMenu>(ContextMenu.GetContextMenu(button));

        Assert.Equal(2, menu.Items.Count);
        Assert.Equal("File", Assert.IsType<MenuItem>(menu.Items[0]).Header);
    }

    [Fact]
    public void ContextMenu_OpenedAndClosedHandlers_FromXaml_ShouldWireToCodeBehind()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <Button Name="cmButton" Text="Button with Context Menu" Height="30" Width="200">
      <Button.ContextMenu>
        <ContextMenu Name="cm" Opened="Menu_OnOpened" Closed="Menu_OnClosed" StaysOpen="true">
          <MenuItem Header="File"/>
        </ContextMenu>
      </Button.ContextMenu>
    </Button>
  </Grid>
</UserControl>
""";

        var view = new ContextMenuCodeBehindHost();
        XamlLoader.LoadIntoFromString(view, xaml, view);

        var button = Assert.IsType<Button>(view.FindName("cmButton"));
        var menu = Assert.IsType<ContextMenu>(ContextMenu.GetContextMenu(button));
        var host = new Canvas { Width = 300f, Height = 240f };
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        menu.OpenAt(host, 40f, 50f, button);
        menu.Close();

        Assert.Equal(1, view.OpenedCount);
        Assert.Equal(1, view.ClosedCount);
    }

    private static void RunLayout(UiRoot uiRoot, int width = 300, int height = 240)
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

    private sealed class ContextMenuCodeBehindHost : UserControl
    {
        public int OpenedCount { get; private set; }

        public int ClosedCount { get; private set; }

        private void Menu_OnOpened(object? sender, EventArgs e)
        {
            OpenedCount++;
        }

        private void Menu_OnClosed(object? sender, EventArgs e)
        {
            ClosedCount++;
        }
    }
}

