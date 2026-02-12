using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Runtime.CompilerServices;
using Xunit;

namespace InkkSlinger.Tests;

public class NavigationHierarchyTests
{
    public NavigationHierarchyTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void TabControl_KeyboardTraversal_ChangesSelectedTab()
    {
        var tab = new TestTabControl();
        tab.Items.Add(new TabItem { Header = "One", Content = new Label { Text = "A" } });
        tab.Items.Add(new TabItem { Header = "Two", Content = new Label { Text = "B" } });
        tab.Items.Add(new TabItem { Header = "Three", Content = new Label { Text = "C" } });

        tab.Measure(new Vector2(300f, 200f));
        tab.Arrange(new LayoutRect(0f, 0f, 300f, 200f));

        Assert.Equal(0, tab.SelectedIndex);

        tab.FireKeyDown(Keys.Right);
        Assert.Equal(1, tab.SelectedIndex);

        tab.FireKeyDown(Keys.End);
        Assert.Equal(2, tab.SelectedIndex);

        tab.FireKeyDown(Keys.Home);
        Assert.Equal(0, tab.SelectedIndex);
    }

    [Fact]
    public void TreeView_ExpandCollapseAndArrowTraversal_Work()
    {
        var tree = new TestTreeView();
        var root = new TreeViewItem { Header = "Root" };
        var childA = new TreeViewItem { Header = "Child A" };
        var childB = new TreeViewItem { Header = "Child B" };
        root.Items.Add(childA);
        root.Items.Add(childB);
        tree.Items.Add(root);

        tree.Measure(new Vector2(320f, 260f));
        tree.Arrange(new LayoutRect(0f, 0f, 320f, 260f));

        tree.Select(root);
        Assert.Same(root, tree.SelectedItem);

        tree.FireKeyDown(Keys.Right);
        Assert.True(root.IsExpanded);

        tree.FireKeyDown(Keys.Down);
        Assert.Same(childA, tree.SelectedItem);

        tree.FireKeyDown(Keys.Up);
        Assert.Same(root, tree.SelectedItem);

        tree.FireKeyDown(Keys.Left);
        Assert.False(root.IsExpanded);
    }

    [Fact]
    public void TreeView_IgnoresAutoRepeat_ForArrowNavigation()
    {
        var tree = new TestTreeView();
        var root = new TreeViewItem { Header = "Root", IsExpanded = true };
        var childA = new TreeViewItem { Header = "Child A" };
        var childB = new TreeViewItem { Header = "Child B" };
        root.Items.Add(childA);
        root.Items.Add(childB);
        tree.Items.Add(root);

        tree.Measure(new Vector2(320f, 260f));
        tree.Arrange(new LayoutRect(0f, 0f, 320f, 260f));

        tree.Select(root);

        tree.FireKeyDown(Keys.Down, isRepeat: false);
        Assert.Same(childA, tree.SelectedItem);

        tree.FireKeyDown(Keys.Down, isRepeat: true);
        tree.FireKeyDown(Keys.Down, isRepeat: true);
        tree.FireKeyDown(Keys.Down, isRepeat: true);

        Assert.Same(childA, tree.SelectedItem);
    }

    [Fact]
    public void TreeView_MouseClick_SelectsItem_AndGlyphToggleExpands()
    {
        var tree = new TestTreeView();
        var root = new TestTreeViewItem { Header = "Root" };
        root.Items.Add(new TreeViewItem { Header = "Child" });
        tree.Items.Add(root);

        tree.Measure(new Vector2(320f, 260f));
        tree.Arrange(new LayoutRect(0f, 0f, 320f, 260f));

        var headerPoint = new Vector2(root.LayoutSlot.X + 24f, root.LayoutSlot.Y + 8f);
        root.FireLeftDown(headerPoint);
        Assert.Same(root, tree.SelectedItem);

        var glyphPoint = root.GetGlyphCenter();
        root.FireLeftDown(glyphPoint);
        Assert.True(root.IsExpanded);
    }

    [Fact]
    public void XamlLoader_Parses_TabControl_AndTreeView()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <StackPanel>
                                <TabControl x:Name="Tabs">
                                  <TabItem Header="A">
                                    <Label Text="Tab A" />
                                  </TabItem>
                                  <TabItem Header="B">
                                    <Label Text="Tab B" />
                                  </TabItem>
                                </TabControl>
                                <TreeView x:Name="Tree" SelectedItemChanged="OnTreeSelectedItemChanged">
                                  <TreeViewItem Header="Node 1" />
                                  <TreeViewItem Header="Node 2" />
                                </TreeView>
                              </StackPanel>
                            </UserControl>
                            """;

        var codeBehind = new NavCodeBehind();
        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, codeBehind);

        Assert.NotNull(codeBehind.Tabs);
        Assert.Equal(2, codeBehind.Tabs!.Items.Count);

        Assert.NotNull(codeBehind.Tree);
        Assert.Equal(2, codeBehind.Tree!.Items.Count);

        var node = Assert.IsType<TreeViewItem>(codeBehind.Tree.Items[1]);
        codeBehind.Tree.SelectItem(node);
        Assert.Equal(1, codeBehind.TreeChangedCount);
    }

    [Fact]
    public void TreeView_Containers_DoNotStoreLocalNullFontValues()
    {
        var tree = new TreeView();
        var root = new TreeViewItem { Header = "Root" };
        var child = new TreeViewItem { Header = "Child" };
        root.Items.Add(child);
        tree.Items.Add(root);

        tree.Measure(new Vector2(320f, 260f));
        tree.Arrange(new LayoutRect(0f, 0f, 320f, 260f));

        Assert.False(root.HasLocalValue(TreeViewItem.FontProperty));
        Assert.False(child.HasLocalValue(TreeViewItem.FontProperty));
    }

    [Fact]
    public void TreeViewItem_FontChange_PropagatesToDescendants()
    {
        var tree = new TreeView();
        var root = new TreeViewItem { Header = "Root" };
        var child = new TreeViewItem { Header = "Child" };
        var grandChild = new TreeViewItem { Header = "Grand Child" };
        child.Items.Add(grandChild);
        root.Items.Add(child);
        tree.Items.Add(root);

        tree.Measure(new Vector2(320f, 260f));
        tree.Arrange(new LayoutRect(0f, 0f, 320f, 260f));

        var font = (Microsoft.Xna.Framework.Graphics.SpriteFont)RuntimeHelpers.GetUninitializedObject(
            typeof(Microsoft.Xna.Framework.Graphics.SpriteFont));

        root.Font = font;

        Assert.Same(font, child.Font);
        Assert.Same(font, grandChild.Font);

        root.Font = null;

        Assert.Null(child.Font);
        Assert.Null(grandChild.Font);
        Assert.False(child.HasLocalValue(TreeViewItem.FontProperty));
        Assert.False(grandChild.HasLocalValue(TreeViewItem.FontProperty));
    }

    private sealed class TestTabControl : TabControl
    {
        public void FireKeyDown(Keys key)
        {
            RaisePreviewKeyDown(key, false, ModifierKeys.None);
            RaiseKeyDown(key, false, ModifierKeys.None);
        }
    }

    private sealed class TestTreeView : TreeView
    {
        public void FireKeyDown(Keys key, bool isRepeat = false)
        {
            RaisePreviewKeyDown(key, isRepeat, ModifierKeys.None);
            RaiseKeyDown(key, isRepeat, ModifierKeys.None);
        }

        public void Select(TreeViewItem item)
        {
            SelectItem(item);
        }
    }

    private sealed class TestTreeViewItem : TreeViewItem
    {
        public void FireLeftDown(Vector2 point)
        {
            RaisePreviewMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseDown(point, MouseButton.Left, 1, ModifierKeys.None);
        }

        public Vector2 GetGlyphCenter()
        {
            var rowHeight = MathF.Max(18f, ((Font?.LineSpacing) ?? 14f) + 4f);
            return new Vector2(LayoutSlot.X + 9f, LayoutSlot.Y + (rowHeight / 2f));
        }
    }

    private sealed class NavCodeBehind
    {
        public TabControl? Tabs { get; set; }

        public TreeView? Tree { get; set; }

        public int TreeChangedCount { get; private set; }

        public void OnTreeSelectedItemChanged(object? sender, RoutedSimpleEventArgs args)
        {
            TreeChangedCount++;
        }
    }
}
