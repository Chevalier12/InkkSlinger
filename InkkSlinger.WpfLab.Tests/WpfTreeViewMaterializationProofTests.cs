using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;
using Xunit.Abstractions;

namespace InkkSlinger.WpfLab.Tests;

public sealed class WpfTreeViewMaterializationProofTests
{
    private const int FolderCount = 2078;
    private const int FileCount = 15329;

    private readonly ITestOutputHelper _output;

    public WpfTreeViewMaterializationProofTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Wpf_EagerTreeViewItems_MaterializesEveryProjectNode()
    {
        EnsureApplication();
        var project = CreateProjectModel(FolderCount, FileCount);
        var stopwatch = Stopwatch.StartNew();
        var treeView = new TreeView
        {
            Width = 258d,
            Height = 458d
        };
        treeView.Items.Add(CreateExpandedTreeViewItem(project));
        stopwatch.Stop();
        var buildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

        var window = CreateWindow(treeView, width: 320d, height: 560d);
        try
        {
            stopwatch.Restart();
            window.UpdateLayout();
            PumpDispatcher();
            stopwatch.Stop();
            var layoutMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            var visualTreeItems = CountVisualDescendants<TreeViewItem>(treeView);

            _output.WriteLine($"eager_build_ms={buildMilliseconds:0.###}");
            _output.WriteLine($"eager_layout_ms={layoutMilliseconds:0.###}");
            _output.WriteLine($"eager_visual_tree_items={visualTreeItems}");
            _output.WriteLine($"model_nodes={project.TotalNodeCount}");

            Assert.Equal(project.TotalNodeCount, CountLogicalTreeItems(treeView));
            Assert.True(buildMilliseconds > 1d);
        }
        finally
        {
            CloseWindow(window);
        }
    }

    [Fact]
    public void Wpf_DataBoundVirtualizedTreeView_DoesNotBuildTreeViewItemForEveryProjectNodeOnFirstLayout()
    {
        EnsureApplication();
        var project = CreateProjectModel(FolderCount, FileCount);
        var treeView = CreateDataBoundVirtualizedTreeView(project);

        var window = CreateWindow(treeView, width: 320d, height: 560d);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            window.UpdateLayout();
            PumpDispatcher();
            stopwatch.Stop();

            var visualTreeItems = CountVisualDescendants<TreeViewItem>(treeView);
            _output.WriteLine($"databound_layout_ms={stopwatch.Elapsed.TotalMilliseconds:0.###}");
            _output.WriteLine($"databound_visual_tree_items={visualTreeItems}");
            _output.WriteLine($"model_nodes={project.TotalNodeCount}");

            Assert.True(
                visualTreeItems < project.TotalNodeCount / 4,
                $"Expected WPF data-bound virtualized TreeView to keep realized containers far below all project nodes. realized={visualTreeItems}, model={project.TotalNodeCount}.");
        }
        finally
        {
            CloseWindow(window);
        }
    }

    private static TreeView CreateDataBoundVirtualizedTreeView(ProjectNode project)
    {
        var treeView = new TreeView
        {
            Width = 258d,
            Height = 458d,
            ItemsSource = new[] { project }
        };

        VirtualizingStackPanel.SetIsVirtualizing(treeView, true);
        VirtualizingStackPanel.SetVirtualizationMode(treeView, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(treeView, true);

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(ProjectNode.Name)));
        treeView.ItemTemplate = new HierarchicalDataTemplate(typeof(ProjectNode))
        {
            ItemsSource = new Binding(nameof(ProjectNode.Children)),
            VisualTree = textFactory
        };

        var itemContainerStyle = new Style(typeof(TreeViewItem));
        itemContainerStyle.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, true));
        itemContainerStyle.Setters.Add(new Setter(VirtualizingStackPanel.IsVirtualizingProperty, true));
        itemContainerStyle.Setters.Add(new Setter(VirtualizingStackPanel.VirtualizationModeProperty, VirtualizationMode.Recycling));
        treeView.ItemContainerStyle = itemContainerStyle;

        return treeView;
    }

    private static TreeViewItem CreateExpandedTreeViewItem(ProjectNode node)
    {
        var item = new TreeViewItem
        {
            Header = node.Name,
            IsExpanded = true
        };

        foreach (var child in node.Children)
        {
            item.Items.Add(CreateExpandedTreeViewItem(child));
        }

        return item;
    }

    private static ProjectNode CreateProjectModel(int folderCount, int fileCount)
    {
        var root = new ProjectNode("InkkSlinger", isFolder: true);
        var filesPerFolder = fileCount / folderCount;
        var extraFiles = fileCount % folderCount;
        for (var folderIndex = 0; folderIndex < folderCount; folderIndex++)
        {
            var folder = new ProjectNode($"Folder {folderIndex:0000}", isFolder: true);
            var count = filesPerFolder + (folderIndex < extraFiles ? 1 : 0);
            for (var fileIndex = 0; fileIndex < count; fileIndex++)
            {
                folder.Children.Add(new ProjectNode($"File {folderIndex:0000}-{fileIndex:000}.xml", isFolder: false));
            }

            root.Children.Add(folder);
        }

        return root;
    }

    private static Window CreateWindow(UIElement content, double width, double height)
    {
        var window = new Window
        {
            Content = content,
            Height = height,
            Left = -10000d,
            ShowInTaskbar = false,
            Top = -10000d,
            Width = width,
            WindowStyle = WindowStyle.ToolWindow
        };

        window.Show();
        window.Activate();
        PumpDispatcher();
        window.UpdateLayout();
        PumpDispatcher();
        return window;
    }

    private static void CloseWindow(Window window)
    {
        window.Close();
        PumpDispatcher();
    }

    private static int CountLogicalTreeItems(ItemsControl root)
    {
        var count = 0;
        foreach (var item in root.Items)
        {
            if (item is TreeViewItem treeViewItem)
            {
                count += CountLogicalTreeItems(treeViewItem);
            }
        }

        return count + (root is TreeViewItem ? 1 : 0);
    }

    private static int CountVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = root is T ? 1 : 0;
        var children = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < children; i++)
        {
            count += CountVisualDescendants<T>(VisualTreeHelper.GetChild(root, i));
        }

        return count;
    }

    private static void EnsureApplication()
    {
        if (Application.Current is null)
        {
            _ = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
        }
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class ProjectNode
    {
        public ProjectNode(string name, bool isFolder)
        {
            Name = name;
            IsFolder = isFolder;
        }

        public string Name { get; }

        public bool IsFolder { get; }

        public ObservableCollection<ProjectNode> Children { get; } = new();

        public int TotalNodeCount => 1 + Children.Sum(static child => child.TotalNodeCount);
    }
}
