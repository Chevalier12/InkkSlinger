using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class StatusAndGroupingControlsTests
{
    public StatusAndGroupingControlsTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void StatusBar_ArrangesRightAlignedItems_ToRightEdge()
    {
        var statusBar = new StatusBar
        {
            Width = 400f,
            Height = 28f,
            Padding = new Thickness(4f, 2f, 4f, 2f),
            ItemSpacing = 4f
        };

        var left = new StatusBarItem { Content = "Ready" };
        var middle = new StatusBarItem { Content = "Ln 10" };
        var right = new StatusBarItem
        {
            Content = "UTF-8",
            HorizontalContentAlignment = HorizontalAlignment.Right
        };

        statusBar.Items.Add(left);
        statusBar.Items.Add(middle);
        statusBar.Items.Add(right);

        statusBar.Measure(new Vector2(400f, 30f));
        statusBar.Arrange(new LayoutRect(0f, 0f, 400f, 28f));

        Assert.True(right.LayoutSlot.X > middle.LayoutSlot.X);
        Assert.True(right.LayoutSlot.X + right.LayoutSlot.Width <= statusBar.LayoutSlot.X + statusBar.LayoutSlot.Width);
    }

    [Fact]
    public void GroupBox_ArrangesHeaderAndContent_WithContentBelowHeader()
    {
        var groupBox = new GroupBox
        {
            Width = 300f,
            Height = 180f,
            Header = "Layers",
            Content = new Border { Height = 80f, Width = 100f }
        };

        groupBox.Measure(new Vector2(300f, 180f));
        groupBox.Arrange(new LayoutRect(0f, 0f, 300f, 180f));

        var content = Assert.IsType<Border>(groupBox.Content);
        Assert.True(content.LayoutSlot.Y > groupBox.LayoutSlot.Y);
        Assert.True(content.LayoutSlot.Width > 0f);
        Assert.True(content.LayoutSlot.Height > 0f);
    }

    [Fact]
    public void GroupBox_AcceptsHeaderElement_AndKeepsItInLogicalTree()
    {
        var header = new Label { Text = "Properties" };
        var groupBox = new GroupBox
        {
            Header = header,
            Content = new Label { Text = "Body" }
        };

        groupBox.Measure(new Vector2(220f, 120f));
        groupBox.Arrange(new LayoutRect(0f, 0f, 220f, 120f));

        var foundHeader = false;
        foreach (var child in groupBox.GetLogicalChildren())
        {
            if (ReferenceEquals(child, header))
            {
                foundHeader = true;
                break;
            }
        }

        Assert.True(foundHeader);
    }

    [Fact]
    public void XamlLoader_Parses_StatusBar_And_GroupBox()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <StackPanel>
                                <StatusBar x:Name="MainStatus">
                                  <StatusBarItem Content="Ready" />
                                  <StatusBarItem Content="UTF-8" HorizontalContentAlignment="Right" />
                                </StatusBar>
                                <GroupBox x:Name="MainGroup" Header="Tool Settings">
                                  <Label Text="Fill: Solid" />
                                </GroupBox>
                              </StackPanel>
                            </UserControl>
                            """;

        var codeBehind = new StatusGroupingCodeBehind();
        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, codeBehind);

        Assert.NotNull(codeBehind.MainStatus);
        Assert.NotNull(codeBehind.MainGroup);
        Assert.Equal(2, codeBehind.MainStatus!.Items.Count);
        Assert.Equal("Tool Settings", codeBehind.MainGroup!.Header as string);
    }

    private sealed class StatusGroupingCodeBehind
    {
        public StatusBar? MainStatus { get; set; }

        public GroupBox? MainGroup { get; set; }
    }
}
