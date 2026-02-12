using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class ContextMenuTests
{
    public ContextMenuTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void OpenAt_AttachesToHost_AndClose_Detaches()
    {
        var host = new Panel
        {
            Width = 500f,
            Height = 300f
        };
        var menu = new ContextMenu();
        menu.Items.Add(new Button { Text = "Inspect" });

        host.Measure(new Vector2(500f, 300f));
        host.Arrange(new LayoutRect(0f, 0f, 500f, 300f));

        menu.OpenAt(host, 120f, 80f);

        Assert.True(menu.IsOpen);
        Assert.Contains(menu, host.Children);

        menu.Close();

        Assert.False(menu.IsOpen);
        Assert.DoesNotContain(menu, host.Children);
    }

    [Fact]
    public void AttachedContextMenu_RightClick_OpensAtPointer()
    {
        var host = new Panel
        {
            Width = 640f,
            Height = 360f
        };
        var target = new TestTarget
        {
            Width = 220f,
            Height = 80f
        };
        host.AddChild(target);

        host.Measure(new Vector2(640f, 360f));
        host.Arrange(new LayoutRect(0f, 0f, 640f, 360f));

        var menu = new ContextMenu();
        menu.Items.Add(new Button { Text = "Pin" });
        ContextMenu.SetContextMenu(target, menu);

        var pointer = new Vector2(target.LayoutSlot.X + 12f, target.LayoutSlot.Y + 18f);
        target.FireRightDown(pointer);

        Assert.True(menu.IsOpen);
        Assert.Contains(menu, host.Children);
        Assert.Same(target, menu.PlacementTarget);
    }

    [Fact]
    public void OutsideClick_ClosesMenu()
    {
        var host = new Panel
        {
            Width = 480f,
            Height = 320f
        };
        var target = new TestTarget
        {
            Width = 220f,
            Height = 80f
        };
        var outside = new TestTarget
        {
            Width = 220f,
            Height = 80f,
            Margin = new Thickness(0f, 120f, 0f, 0f)
        };
        host.AddChild(target);
        host.AddChild(outside);

        host.Measure(new Vector2(480f, 320f));
        host.Arrange(new LayoutRect(0f, 0f, 480f, 320f));

        var menu = new ContextMenu();
        menu.Items.Add(new Button { Text = "Delete" });
        menu.OpenAt(host, 80f, 60f, target);
        Assert.True(menu.IsOpen);

        var outsidePoint = new Vector2(outside.LayoutSlot.X + 8f, outside.LayoutSlot.Y + 8f);
        outside.FireLeftDown(outsidePoint);

        Assert.False(menu.IsOpen);
        Assert.DoesNotContain(menu, host.Children);
    }

    [Fact]
    public void HandledItemMouseUp_StillClosesMenu()
    {
        var host = new Panel
        {
            Width = 520f,
            Height = 340f
        };

        var menu = new ContextMenu
        {
            Width = 220f
        };

        var button = new TestHandledButton
        {
            Text = "Open Settings",
            Height = 32f
        };
        menu.Items.Add(button);

        host.Measure(new Vector2(520f, 340f));
        host.Arrange(new LayoutRect(0f, 0f, 520f, 340f));
        menu.OpenAt(host, 40f, 30f);
        host.UpdateLayout();

        var point = new Vector2(button.LayoutSlot.X + 6f, button.LayoutSlot.Y + 6f);
        button.FireHandledMouseUp(point);

        Assert.False(menu.IsOpen);
        Assert.DoesNotContain(menu, host.Children);
    }

    [Fact]
    public void XamlLoader_Parses_ContextMenu_Properties_And_Items()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <ContextMenu x:Name="Actions"
                                           StaysOpen="true"
                                           PlacementMode="Bottom"
                                           HorizontalOffset="4"
                                           VerticalOffset="8">
                                <Button Text="Copy" />
                                <Button Text="Paste" />
                              </ContextMenu>
                            </UserControl>
                            """;

        var codeBehind = new ContextMenuCodeBehind();
        var root = new UserControl();
        XamlLoader.LoadIntoFromString(root, xaml, codeBehind);

        Assert.NotNull(codeBehind.Actions);
        Assert.True(codeBehind.Actions!.StaysOpen);
        Assert.Equal(PopupPlacementMode.Bottom, codeBehind.Actions.PlacementMode);
        Assert.Equal(4f, codeBehind.Actions.HorizontalOffset, 3);
        Assert.Equal(8f, codeBehind.Actions.VerticalOffset, 3);
        Assert.Equal(2, codeBehind.Actions.Items.Count);
    }

    private sealed class TestTarget : Border
    {
        public void FireRightDown(Vector2 point)
        {
            RaisePreviewMouseDown(point, MouseButton.Right, 1, ModifierKeys.None);
            RaiseMouseDown(point, MouseButton.Right, 1, ModifierKeys.None);
        }

        public void FireLeftDown(Vector2 point)
        {
            RaisePreviewMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
        }
    }

    private sealed class ContextMenuCodeBehind
    {
        public ContextMenu? Actions { get; set; }
    }

    private sealed class TestHandledButton : Button
    {
        public void FireHandledMouseUp(Vector2 point)
        {
            var args = new RoutedMouseButtonEventArgs(MouseUpEvent, point, MouseButton.Left, 1, ModifierKeys.None)
            {
                Handled = true
            };
            RaiseRoutedEvent(MouseUpEvent, args);
        }
    }
}
