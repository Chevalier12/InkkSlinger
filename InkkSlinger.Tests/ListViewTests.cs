using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class ListViewTests
{
    public ListViewTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void ListView_SelectsContainer_OnPreviewMouseDown()
    {
        var listView = new ListView
        {
            Width = 240f,
            Height = 140f
        };

        var first = new TestListViewItem { Content = new Label { Text = "First" } };
        var second = new TestListViewItem { Content = new Label { Text = "Second" } };
        listView.Items.Add(first);
        listView.Items.Add(second);

        listView.Measure(new Vector2(240f, 140f));
        listView.Arrange(new LayoutRect(0f, 0f, 240f, 140f));

        var click = new Vector2(second.LayoutSlot.X + 6f, second.LayoutSlot.Y + 6f);
        second.FireLeftDown(click);

        Assert.Equal(1, listView.SelectedIndex);
        Assert.Same(second, listView.SelectedItem);
        Assert.True(second.IsSelected);
        Assert.False(first.IsSelected);
    }

    [Fact]
    public void ListView_CreatesListViewItemContainer_ForDataItems()
    {
        var listView = new TestListView();
        listView.Items.Add("Alpha");
        listView.Items.Add("Beta");

        listView.Measure(new Vector2(240f, 140f));
        listView.Arrange(new LayoutRect(0f, 0f, 240f, 140f));

        Assert.IsType<ListViewItem>(listView.GetContainerAt(0));
        Assert.IsType<ListViewItem>(listView.GetContainerAt(1));
    }

    [Fact]
    public void XamlLoader_Parses_ListView_AndSelectionChangedHandler()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <ListView x:Name="Catalog" SelectionChanged="OnChanged">
                                <ListViewItem>
                                  <Label Text="Raven Ink" />
                                </ListViewItem>
                                <ListViewItem>
                                  <Label Text="Sepia Wash" />
                                </ListViewItem>
                              </ListView>
                            </UserControl>
                            """;

        var codeBehind = new ListViewCodeBehind();
        var root = new UserControl();
        XamlLoader.LoadIntoFromString(root, xaml, codeBehind);

        Assert.NotNull(codeBehind.Catalog);
        codeBehind.Catalog!.SelectedIndex = 1;

        Assert.Equal(1, codeBehind.SelectionChangedCount);
        Assert.Equal(2, codeBehind.Catalog.Items.Count);
        Assert.IsType<ListViewItem>(codeBehind.Catalog.Items[0]);
    }

    private sealed class TestListViewItem : ListViewItem
    {
        public void FireLeftDown(Vector2 point)
        {
            RaisePreviewMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
        }
    }

    private sealed class TestListView : ListView
    {
        public UIElement GetContainerAt(int index)
        {
            return ItemContainers[index];
        }
    }

    private sealed class ListViewCodeBehind
    {
        public ListView? Catalog { get; set; }

        public int SelectionChangedCount { get; private set; }

        public void OnChanged(object? sender, SelectionChangedEventArgs args)
        {
            SelectionChangedCount++;
        }
    }
}
