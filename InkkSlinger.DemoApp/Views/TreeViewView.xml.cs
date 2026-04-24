using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class TreeViewView : UserControl
{
    private int _fileSystemChangeCount;
    private int _styledChangeCount;
    private int _interactiveChangeCount;
    private TreeViewItem? _interactiveRoot;
    private TreeViewItem? _interactiveLeaf;

    public TreeViewView()
    {
        InitializeComponent();

        BuildFileSystemTree();
        BuildStyledTree();
        BuildInteractiveTree();
        WireEvents();
        UpdateAllReadouts();
    }

    private static TreeViewItem BuildFileSystemHierarchy()
    {
        var srcUiControls = new TreeViewItem
        {
            Header = "Controls",
            IsExpanded = true
        };
        srcUiControls.Items.Add(new TreeViewItem { Header = "Button.cs" });
        srcUiControls.Items.Add(new TreeViewItem { Header = "TextBox.cs" });
        srcUiControls.Items.Add(new TreeViewItem { Header = "TreeView.cs" });
        srcUiControls.Items.Add(new TreeViewItem { Header = "Slider.cs" });

        var srcUiCore = new TreeViewItem
        {
            Header = "Core"
        };
        srcUiCore.Items.Add(new TreeViewItem { Header = "Dispatcher.cs" });
        srcUiCore.Items.Add(new TreeViewItem { Header = "FrameworkElement.cs" });

        var srcUiRenderingDirty = new TreeViewItem
        {
            Header = "DirtyRegions",
            IsExpanded = true
        };
        srcUiRenderingDirty.Items.Add(new TreeViewItem { Header = "DirtyRegionTracker.cs" });
        srcUiRenderingDirty.Items.Add(new TreeViewItem { Header = "RegionMerger.cs" });

        var srcUiRendering = new TreeViewItem
        {
            Header = "Rendering",
            IsExpanded = true
        };
        srcUiRendering.Items.Add(srcUiRenderingDirty);
        srcUiRendering.Items.Add(new TreeViewItem { Header = "RenderPipeline.cs" });

        var srcUi = new TreeViewItem
        {
            Header = "UI",
            IsExpanded = true
        };
        srcUi.Items.Add(srcUiControls);
        srcUi.Items.Add(srcUiCore);
        srcUi.Items.Add(srcUiRendering);

        var srcTests = new TreeViewItem
        {
            Header = "Tests"
        };
        srcTests.Items.Add(new TreeViewItem { Header = "TreeViewInputTests.cs" });
        srcTests.Items.Add(new TreeViewItem { Header = "ButtonTests.cs" });

        var srcDemoAppViews = new TreeViewItem
        {
            Header = "Views"
        };
        srcDemoAppViews.Items.Add(new TreeViewItem { Header = "TreeViewView.xml" });
        srcDemoAppViews.Items.Add(new TreeViewItem { Header = "ListBoxView.xml" });

        var srcDemoApp = new TreeViewItem
        {
            Header = "DemoApp"
        };
        srcDemoApp.Items.Add(srcDemoAppViews);

        var src = new TreeViewItem
        {
            Header = "src",
            IsExpanded = true
        };
        src.Items.Add(srcUi);
        src.Items.Add(srcTests);
        src.Items.Add(srcDemoApp);

        var docs = new TreeViewItem
        {
            Header = "docs"
        };
        docs.Items.Add(new TreeViewItem { Header = "architecture.md" });

        var root = new TreeViewItem
        {
            Header = "Project Root",
            IsExpanded = true
        };
        root.Items.Add(src);
        root.Items.Add(docs);
        root.Items.Add(new TreeViewItem { Header = "README.md" });

        return root;
    }

    private void BuildFileSystemTree()
    {
        if (FileSystemTree.Items.Count > 0)
        {
            return;
        }

        FileSystemTree.Items.Add(BuildFileSystemHierarchy());
    }

    private void BuildStyledTree()
    {
        if (StyledTree.Items.Count > 0)
        {
            return;
        }

        StyledTree.Background = new Color(20, 18, 14);
        StyledTree.BorderBrush = new Color(100, 82, 54);
        StyledTree.Foreground = new Color(255, 184, 77);

        var root = BuildFileSystemHierarchy();
        ApplyStyledRecursive(root, 24f, new Color(57, 84, 74));
        StyledTree.Items.Add(root);
    }

    private static void ApplyStyledRecursive(TreeViewItem item, float indent, Color selectedBackground)
    {
        item.Indent = indent;
        item.SelectedBackground = selectedBackground;

        foreach (var child in item.GetChildTreeItems())
        {
            ApplyStyledRecursive(child, indent, selectedBackground);
        }
    }

    private void BuildInteractiveTree()
    {
        if (InteractiveTree.Items.Count > 0)
        {
            return;
        }

        var reportsQuarterly = new TreeViewItem
        {
            Header = "Quarterly"
        };
        reportsQuarterly.Items.Add(new TreeViewItem { Header = "Q1 Report" });
        reportsQuarterly.Items.Add(new TreeViewItem { Header = "Q2 Report" });
        reportsQuarterly.Items.Add(new TreeViewItem { Header = "Q3 Report" });

        var reportsAnnual = new TreeViewItem
        {
            Header = "Annual",
            IsExpanded = true
        };
        reportsAnnual.Items.Add(new TreeViewItem { Header = "Year End Summary" });
        reportsAnnual.Items.Add(new TreeViewItem { Header = "Audit Results" });

        var reports = new TreeViewItem
        {
            Header = "Reports",
            IsExpanded = true
        };
        reports.Items.Add(reportsQuarterly);
        reports.Items.Add(reportsAnnual);

        var inbox = new TreeViewItem
        {
            Header = "Inbox"
        };
        inbox.Items.Add(new TreeViewItem { Header = "Welcome" });
        inbox.Items.Add(new TreeViewItem { Header = "Setup Guide" });

        var root = new TreeViewItem
        {
            Header = "Dashboard",
            IsExpanded = true
        };
        root.Items.Add(reports);
        root.Items.Add(inbox);

        _interactiveRoot = root;
        _interactiveLeaf = reportsAnnual.GetChildTreeItems()[0];
        InteractiveTree.Items.Add(root);
    }

    private void WireEvents()
    {
        FileSystemTree.SelectedItemChanged += OnFileSystemSelectionChanged;
        StyledTree.SelectedItemChanged += OnStyledSelectionChanged;
        InteractiveTree.SelectedItemChanged += OnInteractiveSelectionChanged;

        ExpandAllButton.Click += OnExpandAllClicked;
        CollapseAllButton.Click += OnCollapseAllClicked;
        SelectRootButton.Click += OnSelectRootClicked;
        SelectLeafButton.Click += OnSelectLeafClicked;
        DeselectButton.Click += OnDeselectClicked;
    }

    private void OnFileSystemSelectionChanged(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        _fileSystemChangeCount++;
        UpdateFileSystemReadout();
    }

    private void OnStyledSelectionChanged(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        _styledChangeCount++;
        UpdateStyledReadout();
    }

    private void OnInteractiveSelectionChanged(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        _interactiveChangeCount++;
        UpdateInteractiveReadout();
    }

    private void OnExpandAllClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        SetExpandedRecursive(_interactiveRoot, true);
        UpdateInteractiveReadout();
    }

    private void OnCollapseAllClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        SetExpandedRecursive(_interactiveRoot, false);
        UpdateInteractiveReadout();
    }

    private void OnSelectRootClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_interactiveRoot is { IsEnabled: true })
        {
            InteractiveTree.SelectItem(_interactiveRoot);
        }
    }

    private void OnSelectLeafClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_interactiveLeaf is { IsEnabled: true })
        {
            _interactiveLeaf.IsExpanded = true;
            InteractiveTree.SelectItem(_interactiveLeaf);
        }
    }

    private void OnDeselectClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (InteractiveTree.SelectedItem is not null)
        {
            InteractiveTree.SelectedItem.IsSelected = false;
            InteractiveTree.SelectedItem = null;
        }
    }

    private static void SetExpandedRecursive(TreeViewItem? item, bool expanded)
    {
        if (item == null)
        {
            return;
        }

        if (item.HasChildItems())
        {
            item.IsExpanded = expanded;
        }

        foreach (var child in item.GetChildTreeItems())
        {
            SetExpandedRecursive(child, expanded);
        }
    }

    private void UpdateAllReadouts()
    {
        UpdateFileSystemReadout();
        UpdateStyledReadout();
        UpdateInteractiveReadout();
    }

    private void UpdateFileSystemReadout()
    {
        var selectedItem = FileSystemTree.SelectedItem;
        FileSystemSelectionText.Text = selectedItem == null
            ? "Selected item: none"
            : $"Selected item: {selectedItem.Header} | IsExpanded: {selectedItem.IsExpanded} | HasChildren: {selectedItem.HasChildItems()}";

        FileSystemChangeCountText.Text =
            $"SelectedItemChanged fired: {_fileSystemChangeCount} | Click items to select or expander glyphs to toggle branches.";
    }

    private void UpdateStyledReadout()
    {
        var selectedItem = StyledTree.SelectedItem;
        StyledSelectionText.Text = selectedItem == null
            ? "Selected item: none"
            : $"Selected item: {selectedItem.Header} | Indent: {selectedItem.Indent:F0}px | SelectedBackground: {FormatColor(selectedItem.SelectedBackground)}";
    }

    private void UpdateInteractiveReadout()
    {
        var selectedItem = InteractiveTree.SelectedItem;
        InteractiveSelectionText.Text = selectedItem == null
            ? "Selected item: none"
            : $"Selected item: {selectedItem.Header} | IsExpanded: {selectedItem.IsExpanded}";

        InteractiveChangeCountText.Text =
            $"SelectedItemChanged fired: {_interactiveChangeCount} | Use the buttons above to drive the tree from code.";
    }

    private static string FormatColor(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
