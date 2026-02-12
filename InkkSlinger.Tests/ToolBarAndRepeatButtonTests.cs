using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class ToolBarAndRepeatButtonTests
{
    public ToolBarAndRepeatButtonTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void ToolBar_ConstrainedWidth_ActivatesOverflow()
    {
        var root = new Canvas { Width = 180f, Height = 120f };
        var toolbar = new TestToolBar { Width = 120f, Height = 30f };
        toolbar.Items.Add(new Button { Text = "A", Width = 50f });
        toolbar.Items.Add(new Button { Text = "B", Width = 50f });
        toolbar.Items.Add(new Button { Text = "C", Width = 50f });
        root.AddChild(toolbar);

        root.Measure(new Vector2(180f, 120f));
        root.Arrange(new LayoutRect(0f, 0f, 180f, 120f));

        Assert.True(toolbar.VisibleItemCount < 3);
        Assert.True(toolbar.OverflowItemCount > 0);
        Assert.True(toolbar.OverflowButtonForTesting.LayoutSlot.Width > 0f);
    }

    [Fact]
    public void ToolBar_OverflowOpen_ArrangesItemsInOverflowRegion()
    {
        var root = new Canvas { Width = 220f, Height = 160f };
        var toolbar = new TestToolBar { Width = 120f, Height = 32f };
        var first = new Button { Text = "A", Width = 50f };
        var second = new Button { Text = "B", Width = 50f };
        var third = new Button { Text = "C", Width = 50f };

        toolbar.Items.Add(first);
        toolbar.Items.Add(second);
        toolbar.Items.Add(third);
        root.AddChild(toolbar);

        root.Measure(new Vector2(220f, 160f));
        root.Arrange(new LayoutRect(0f, 0f, 220f, 160f));

        toolbar.IsOverflowOpen = true;
        root.Measure(new Vector2(220f, 160f));
        root.Arrange(new LayoutRect(0f, 0f, 220f, 160f));

        Assert.True(toolbar.OverflowItemCount > 0);
        Assert.True(toolbar.IsOverflowOpen);
        Assert.True(toolbar.OverflowButtonForTesting.LayoutSlot.Width > 0f);
    }

    [Fact]
    public void ToolBar_OutsideClick_ClosesOverflow()
    {
        var root = new Canvas { Width = 260f, Height = 180f };
        var toolbar = new TestToolBar { Width = 120f, Height = 32f };
        toolbar.Items.Add(new Button { Text = "A", Width = 50f });
        toolbar.Items.Add(new Button { Text = "B", Width = 50f });
        toolbar.Items.Add(new Button { Text = "C", Width = 50f });

        var outside = new ClickTarget
        {
            Width = 100f,
            Height = 40f
        };

        root.AddChild(toolbar);
        root.AddChild(outside);
        Canvas.SetTop(outside, 100f);

        root.Measure(new Vector2(260f, 180f));
        root.Arrange(new LayoutRect(0f, 0f, 260f, 180f));

        toolbar.IsOverflowOpen = true;
        root.Measure(new Vector2(260f, 180f));
        root.Arrange(new LayoutRect(0f, 0f, 260f, 180f));

        outside.FireLeftDown(new Vector2(outside.LayoutSlot.X + 3f, outside.LayoutSlot.Y + 3f));

        Assert.False(toolbar.IsOverflowOpen);
    }

    [Fact]
    public void RepeatButton_PressAndHold_FiresAtDelayAndInterval()
    {
        var button = new TestRepeatButton
        {
            Width = 80f,
            Height = 24f,
            RepeatDelay = 0.3f,
            RepeatInterval = 0.1f
        };

        button.Measure(new Vector2(200f, 50f));
        button.Arrange(new LayoutRect(0f, 0f, 80f, 24f));

        var clickCount = 0;
        button.Click += (_, _) => clickCount++;

        button.FireLeftDown(new Vector2(4f, 4f));

        button.Update(new GameTime(TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1)));
        Assert.Equal(0, clickCount);

        button.Update(new GameTime(TimeSpan.FromSeconds(0.3), TimeSpan.FromSeconds(0.2)));
        Assert.Equal(1, clickCount);

        button.Update(new GameTime(TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(0.2)));
        Assert.Equal(3, clickCount);

        button.FireLeftUp(new Vector2(-20f, -20f));
    }

    [Fact]
    public void XamlLoader_Parses_ToolBar_RepeatButton_And_Tray()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <ToolBarTray x:Name="Tray" Orientation="Vertical">
                                <ToolBar x:Name="Bar">
                                  <Button Text="Select" />
                                  <RepeatButton Text="Zoom" RepeatDelay="0.2" RepeatInterval="0.05" />
                                </ToolBar>
                              </ToolBarTray>
                            </UserControl>
                            """;

        var codeBehind = new TrayCodeBehind();
        var root = new UserControl();
        XamlLoader.LoadIntoFromString(root, xaml, codeBehind);

        Assert.NotNull(codeBehind.Tray);
        Assert.NotNull(codeBehind.Bar);
        Assert.Equal(2, codeBehind.Bar!.Items.Count);

        var repeat = Assert.IsType<RepeatButton>(codeBehind.Bar.Items[1]);
        Assert.Equal(0.2f, repeat.RepeatDelay, 3);
        Assert.Equal(0.05f, repeat.RepeatInterval, 3);
    }

    private sealed class TestToolBar : ToolBar
    {
        public int VisibleItemCount => VisibleItemCountForTesting;

        public int OverflowItemCount => OverflowItemCountForTesting;
    }

    private sealed class ClickTarget : Border
    {
        public void FireLeftDown(Vector2 point)
        {
            RaisePreviewMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
        }
    }

    private sealed class TestRepeatButton : RepeatButton
    {
        public void FireLeftDown(Vector2 point)
        {
            RaisePreviewMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
        }

        public void FireLeftUp(Vector2 point)
        {
            RaisePreviewMouseUp(point, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseUp(point, MouseButton.Left, 1, ModifierKeys.None);
        }
    }

    private sealed class TrayCodeBehind
    {
        public ToolBarTray? Tray { get; set; }

        public ToolBar? Bar { get; set; }
    }
}
