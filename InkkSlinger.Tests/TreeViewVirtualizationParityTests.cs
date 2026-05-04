using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TreeViewVirtualizationParityTests
{
    [Fact]
    public void HierarchicalDataMode_UsesMeasuredRowHeightsForBringIntoView()
    {
        var root = new Node("Root", true);
        for (var i = 0; i < 80; i++)
        {
            root.Children.Add(new Node($"Item {i:00}", false, fontSize: i == 55 ? 38f : 13f));
        }

        var (uiRoot, treeView) = CreateHierarchicalFixture(root, height: 140f);
        treeView.ItemTemplate = new DataTemplate(item =>
        {
            var node = Assert.IsType<Node>(item);
            return new TextBlock { Text = node.Name, FontSize = node.FontSize };
        });

        RunLayout(uiRoot);
        Assert.True(treeView.ScrollHierarchicalItemIntoView(root.Children[55]));
        RunLayout(uiRoot);

        var selected = treeView.ContainerFromHierarchicalItem(root.Children[55]);
        Assert.NotNull(selected);
        Assert.True(selected.TryGetRenderBoundsInRootSpace(out var bounds));
        var viewer = treeView.AutomationScrollViewer;
        Assert.InRange(bounds.Y, treeView.LayoutSlot.Y - 0.01f, treeView.LayoutSlot.Y + viewer.ViewportHeight - 1f);
        Assert.True(
            bounds.Height > 38f,
            $"Expected template-measured row height, got {bounds.Height:0.###}. " +
            $"desired={selected.DesiredSize}, children={string.Join(",", selected.GetVisualChildren().Select(static child => child.GetType().Name + (child is FrameworkElement fe ? ':' + fe.DesiredSize.ToString() : string.Empty)))}.");
    }

    [Fact]
    public void SelectHierarchicalItem_TracksDataItemAcrossRecycle()
    {
        var root = CreateLargeTree(120);
        var (uiRoot, treeView) = CreateHierarchicalFixture(root, height: 100f);
        var target = root.Children[90];

        RunLayout(uiRoot);
        Assert.True(treeView.SelectHierarchicalItem(target));
        Assert.Same(target, treeView.SelectedDataItem);

        treeView.AutomationScrollViewer.ScrollToVerticalOffset(0f);
        RunLayout(uiRoot);

        Assert.Same(target, treeView.SelectedDataItem);
        Assert.True(treeView.ScrollHierarchicalItemIntoView(target));
        RunLayout(uiRoot);

        var realized = treeView.ContainerFromHierarchicalItem(target);
        Assert.NotNull(realized);
        Assert.Same(realized, treeView.SelectedItem);
        Assert.True(realized.IsSelected);
    }

    [Fact]
    public void HierarchicalItemTemplate_BuildsVisualHeaderWithDataContext()
    {
        var root = CreateLargeTree(4);
        var (uiRoot, treeView) = CreateHierarchicalFixture(root, height: 140f);
        treeView.ItemTemplate = new DataTemplate(item =>
        {
            var node = Assert.IsType<Node>(item);
            return new TextBlock { Text = $"templated:{node.Name}", FontSize = 17f };
        });

        RunLayout(uiRoot);

        var firstChild = treeView.ContainerFromHierarchicalItem(root.Children[0]);
        Assert.NotNull(firstChild);
        var header = Assert.IsType<TextBlock>(Assert.Single(firstChild.GetVisualChildren()));
        Assert.Equal("templated:Item 00", header.Text);
        Assert.Same(root.Children[0], header.DataContext);
    }

    [Fact]
    public void HierarchicalContainer_ExposesBackingItemWithoutUsingTag()
    {
        var root = CreateLargeTree(4);
        var (uiRoot, treeView) = CreateHierarchicalFixture(root, height: 140f);

        RunLayout(uiRoot);

        var firstChild = treeView.ContainerFromHierarchicalItem(root.Children[0]);
        Assert.NotNull(firstChild);
        Assert.Same(root.Children[0], firstChild.HierarchicalDataItem);
        Assert.Null(firstChild.Tag);
    }

    [Fact]
    public void ExpandedObservableChildCollectionChange_RefreshesRowsIncrementallyEnoughToPreserveSelection()
    {
        var root = CreateLargeTree(4);
        var (uiRoot, treeView) = CreateHierarchicalFixture(root, height: 180f);
        var selected = root.Children[2];

        RunLayout(uiRoot);
        Assert.True(treeView.SelectHierarchicalItem(selected));

        var inserted = new Node("Inserted", false);
        root.Children.Insert(1, inserted);
        RunLayout(uiRoot);

        Assert.NotNull(treeView.ContainerFromHierarchicalItem(inserted));
        Assert.Same(selected, treeView.SelectedDataItem);
    }

    [Fact]
    public void ExpandingLazyHierarchicalItem_LoadsChildrenOnce()
    {
        var root = new Node("Root", true);
        var lazy = new Node("Lazy", true);
        root.Children.Add(lazy);
        var loadCount = 0;

        var (uiRoot, treeView) = CreateHierarchicalFixture(root, height: 180f);
        treeView.HierarchicalLazyChildrenLoader = item =>
        {
            if (!ReferenceEquals(item, lazy))
            {
                return null;
            }

            loadCount++;
            return [new Node("Loaded child", false)];
        };

        RunLayout(uiRoot);
        Assert.True(treeView.SetHierarchicalItemExpanded(lazy, true));
        RunLayout(uiRoot);
        Assert.True(treeView.SetHierarchicalItemExpanded(lazy, false));
        Assert.True(treeView.SetHierarchicalItemExpanded(lazy, true));
        RunLayout(uiRoot);

        Assert.Equal(1, loadCount);
        Assert.Single(lazy.Children);
        Assert.NotNull(treeView.ContainerFromHierarchicalItem(lazy.Children[0]));
    }

    [Fact]
    public void KeyboardNavigation_MovesAndExpandsVisibleHierarchicalRows()
    {
        var root = new Node("Root", true);
        var branch = new Node("Branch", true);
        branch.Children.Add(new Node("Child", false));
        root.Children.Add(branch);
        root.Children.Add(new Node("Sibling", false));
        var (uiRoot, treeView) = CreateHierarchicalFixture(root, height: 180f);
        treeView.SetHierarchicalItemExpanded(branch, false);
        RunLayout(uiRoot);
        uiRoot.SetFocusedElementForTests(treeView);

        treeView.SelectHierarchicalItem(root);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        Assert.Same(branch, treeView.SelectedDataItem);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Right));
        Assert.True(treeView.IsHierarchicalItemExpanded(branch));

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Down));
        Assert.Same(branch.Children[0], treeView.SelectedDataItem);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.Left));
        Assert.Same(branch, treeView.SelectedDataItem);
    }

    private static (UiRoot UiRoot, TreeView TreeView) CreateHierarchicalFixture(Node root, float height)
    {
        var host = new Canvas { Width = 480f, Height = 360f };
        var treeView = new TreeView
        {
            Width = 260f,
            Height = height,
            HierarchicalChildrenSelector = item => item is Node node ? node.Children : Array.Empty<Node>(),
            HierarchicalHasChildrenSelector = item => item is Node { IsFolder: true },
            HierarchicalHeaderSelector = item => item is Node node ? node.Name : string.Empty,
            HierarchicalExpandedSelector = item => item is Node node && ReferenceEquals(node, root),
            HierarchicalItemsSource = new[] { root }
        };

        host.AddChild(treeView);
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        return (uiRoot, treeView);
    }

    private static Node CreateLargeTree(int count)
    {
        var root = new Node("Root", true);
        for (var i = 0; i < count; i++)
        {
            root.Children.Add(new Node($"Item {i:00}", false));
        }

        return root;
    }

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 480, 360));
    }

    private static InputDelta CreateKeyDownDelta(Keys key)
    {
        var pointer = new Vector2(16f, 16f);
        return new InputDelta
        {
            Previous = new InputSnapshot(new KeyboardState(), default, pointer),
            Current = new InputSnapshot(new KeyboardState(), default, pointer),
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

    private sealed class Node(string name, bool isFolder, float fontSize = 13f)
    {
        public string Name { get; } = name;

        public bool IsFolder { get; } = isFolder;

        public float FontSize { get; } = fontSize;

        public ObservableCollection<Node> Children { get; } = new();
    }
}
