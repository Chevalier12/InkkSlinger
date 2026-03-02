using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public static class ControlViews
{
    public static readonly string[] All =
    {
        "AccessText",
        "Border",
        "Button",
        "Calendar",
        "Canvas",
        "CatchMe",
        "CheckBox",
        "ComboBox",
        "ComboBoxItem",
        "ContentControl",
        "ContentPresenter",
        "ContextMenu",
        "Control",
        "DataGrid",
        "DataGridCell",
        "DataGridColumnHeader",
        "DataGridDetailsPresenter",
        "DataGridRow",
        "DataGridRowHeader",
        "DatePicker",
        "Decorator",
        "DockPanel",
        "DocumentViewer",
        "Expander",
        "Frame",
        "Grid",
        "GridSplitter",
        "GroupBox",
        "GroupItem",
        "HeaderedContentControl",
        "HeaderedItemsControl",
        "Image",
        "InkCanvas",
        "InkPresenter",
        "ItemsControl",
        "Label",
        "ListBox",
        "ListBoxItem",
        "ListView",
        "ListViewItem",
        "MediaElement",
        "Menu",
        "MenuItem",
        "Page",
        "Panel",
        "PasswordBox",
        "Popup",
        "ProgressBar",
        "RadioButton",
        "RepeatButton",
        "ResizeGrip",
        "RichTextBox",
        "ScrollBar",
        "ScrollViewer",
        "Separator",
        "Slider",
        "StackPanel",
        "StatusBar",
        "StatusBarItem",
        "TabControl",
        "TabItem",
        "TextBlock",
        "TextBox",
        "Thumb",
        "ToggleButton",
        "ToolBar",
        "ToolBarOverflowPanel",
        "ToolBarPanel",
        "ToolBarTray",
        "ToolTip",
        "TreeView",
        "TreeViewItem",
        "UniformGrid",
        "UserControl",
        "Viewbox",
        "VirtualizingStackPanel",
        "WrapPanel",
        "Window",
    };
}

internal static class ControlDemoSupport
{
    private sealed class DemoRow
    {
        public required int Id { get; init; }

        public required string Name { get; init; }
    }

    internal static UIElement BuildSampleElement(string controlName)
    {
        var sample = TryBuildKnownSample(controlName);
        if (sample != null)
        {
            return EnsureSampleFilled(controlName, sample);
        }

        var controlType = ResolveType(controlName);
        if (controlType == null || !typeof(UIElement).IsAssignableFrom(controlType))
        {
            return BuildInfoLabel($"{controlName} is not implemented as a UIElement in this framework.");
        }

        var ctor = controlType.GetConstructor(Type.EmptyTypes);
        if (ctor == null)
        {
            return BuildInfoLabel($"{controlName} exists but has no parameterless constructor.");
        }

        try
        {
            var element = ctor.Invoke(null) as UIElement;
            if (element == null)
            {
                return BuildInfoLabel($"Could not create {controlName}.");
            }

            return EnsureSampleFilled(controlName, element);
        }
        catch (Exception ex)
        {
            return BuildInfoLabel($"Failed to create {controlName}: {ex.GetType().Name}");
        }
    }

    internal static void ApplyFontRecursive(UIElement? element, SpriteFont font)
    {
        if (element == null)
        {
            return;
        }

        switch (element)
        {
            case Label label:
                label.Font = font;
                break;
            case TextBlock textBlock:
                textBlock.Font = font;
                break;
            case Button button:
                button.Font = font;
                break;
            case TextBox textBox:
                textBox.Font = font;
                break;
            case ComboBox comboBox:
                comboBox.Font = font;
                break;
            case ListView listView:
                listView.Font = font;
                break;
            case ListBox listBox:
                listBox.Font = font;
                break;
            case DataGrid dataGrid:
                dataGrid.Font = font;
                break;
            case PasswordBox passwordBox:
                passwordBox.Font = font;
                break;
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }

    private static UIElement? TryBuildKnownSample(string controlName)
    {
        switch (controlName)
        {
            case "Button":
                return new Button { Text = "Button" };
            case "CheckBox":
                return new CheckBox { Text = "Check me" };
            case "RadioButton":
                return new RadioButton { Text = "Option A" };
            case "ToggleButton":
                return new ToggleButton { Text = "Toggle" };
            case "RepeatButton":
                return new RepeatButton { Text = "Repeat" };
            case "Label":
                return new Label { Text = "Label" };
            case "TextBlock":
                return new TextBlock { Text = "TextBlock sample" };
            case "TextBox":
                return new TextBox { Text = "TextBox sample" };
            case "PasswordBox":
                return new PasswordBox();
            case "Slider":
                return new Slider { Minimum = 0, Maximum = 100, Value = 35 };
            case "ProgressBar":
                return new ProgressBar { Minimum = 0, Maximum = 100, Value = 70 };
            case "ListBox":
            {
                var lb = new ListBox();
                lb.Items.Add("Alpha");
                lb.Items.Add("Beta");
                lb.Items.Add("Gamma");
                return lb;
            }
            case "ListView":
            {
                var lv = new ListView();
                lv.Items.Add("Item 1");
                lv.Items.Add("Item 2");
                lv.Items.Add("Item 3");
                return lv;
            }
            case "ComboBox":
            {
                var cb = new ComboBox();
                cb.Items.Add("One");
                cb.Items.Add("Two");
                cb.Items.Add("Three");
                return cb;
            }
            case "Calendar":
                return new Calendar
                {
                    DisplayDate = new DateTime(2026, 3, 1),
                    SelectedDate = new DateTime(2026, 3, 18)
                };
            case "DatePicker":
                return new DatePicker
                {
                    SelectedDate = new DateTime(2026, 3, 18)
                };
            case "Menu":
            {
                var menu = new Menu();
                menu.Items.Add(new MenuItem { Header = "File" });
                menu.Items.Add(new MenuItem { Header = "Edit" });
                return menu;
            }
            case "ContextMenu":
            {
                var contextMenu = new ContextMenu();
                contextMenu.Items.Add(new MenuItem { Header = "Copy" });
                contextMenu.Items.Add(new MenuItem { Header = "Paste" });
                contextMenu.Items.Add(new Separator());
                contextMenu.Items.Add(new MenuItem { Header = "Delete" });
                contextMenu.IsOpen = true;
                return contextMenu;
            }
            case "TreeView":
            {
                var tv = new TreeView();
                foreach (var root in BuildSampleTreeViewRoots())
                {
                    tv.Items.Add(root);
                }

                return tv;
            }
            case "ItemsControl":
            {
                var ic = new ItemsControl();
                ic.Items.Add("One");
                ic.Items.Add("Two");
                return ic;
            }
            case "DataGrid":
            {
                var dg = new DataGrid();
                dg.Items.Add(new DemoRow { Id = 1, Name = "Alpha" });
                dg.Items.Add(new DemoRow { Id = 2, Name = "Bravo" });
                dg.Items.Add(new DemoRow { Id = 3, Name = "Charlie" });
                dg.Items.Add(new DemoRow { Id = 4, Name = "Delta" });
                dg.Items.Add(new DemoRow { Id = 5, Name = "Echo" });
                dg.Items.Add(new DemoRow { Id = 6, Name = "Foxtrot" });
                return dg;
            }
            case "DataGridCell":
            case "DataGridColumnHeader":
            case "DataGridDetailsPresenter":
            case "DataGridRow":
            case "DataGridRowHeader":
                return BuildInfoLabel($"{controlName} is generated by DataGrid; see DataGrid sample with rows/columns.");
            case "ContentControl":
                return new ContentControl { Content = new Label { Text = "Content" } };
            case "Border":
                return new Border
                {
                    Padding = new Thickness(8),
                    BorderBrush = new Color(80, 120, 170),
                    BorderThickness = new Thickness(1),
                    Child = new Label { Text = "Border child" }
                };
            case "StackPanel":
            {
                var sp = new StackPanel();
                sp.AddChild(new Label { Text = "Top" });
                sp.AddChild(new Label { Text = "Bottom" });
                return sp;
            }
            case "VirtualizingStackPanel":
            {
                var vsp = new VirtualizingStackPanel();
                for (var i = 1; i <= 20; i++)
                {
                    vsp.AddChild(new Label { Text = $"Item {i}" });
                }

                return vsp;
            }
            case "Panel":
            {
                var panel = new Panel();
                panel.AddChild(new Label { Text = "Panel child 1" });
                panel.AddChild(new Label { Text = "Panel child 2" });
                return panel;
            }
            case "Decorator":
                return new Decorator { Child = new Label { Text = "Decorator child" } };
            case "Grid":
            {
                var g = new Grid();
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var a = new Label { Text = "Row 0" };
                var b = new Label { Text = "Row 1" };
                Grid.SetRow(a, 0);
                Grid.SetRow(b, 1);
                g.AddChild(a);
                g.AddChild(b);
                return g;
            }
            case "Canvas":
            {
                var c = new Canvas();
                var t = new Label { Text = "Canvas child" };
                Canvas.SetLeft(t, 10);
                Canvas.SetTop(t, 10);
                c.AddChild(t);
                return c;
            }
            case "ScrollViewer":
            {
                var sv = new ScrollViewer();
                var sp = new StackPanel();
                for (var i = 1; i <= 20; i++)
                {
                    sp.AddChild(new Label { Text = $"Line {i}" });
                }

                sv.Content = sp;
                return sv;
            }
            case "ToolTip":
                return new ToolTip { Content = new Label { Text = "Tooltip content" } };
            case "Popup":
            {
                var popup = new Popup
                {
                    Content = new Label { Text = "Popup content (open)" }
                };
                return popup;
            }
            case "ScrollBar":
                return new ScrollBar
                {
                    Orientation = Orientation.Horizontal,
                    Minimum = 0f,
                    Maximum = 100f,
                    ViewportSize = 20f,
                    Value = 35f
                };
            case "UserControl":
                return new UserControl { Content = new Label { Text = "UserControl content" } };
            case "Window":
                return BuildInfoLabel("Window is host-level in this framework and not instanced inside another view.");
            default:
                return null;
        }
    }

    private static UIElement EnsureSampleFilled(string controlName, UIElement element)
    {
        if (element is FrameworkElement frameworkElement)
        {
            ApplySizingHints(controlName, frameworkElement);
        }

        switch (element)
        {
            case HeaderedContentControl headeredContentControl:
                if (headeredContentControl.Header == null)
                {
                    headeredContentControl.Header = $"{controlName} Header";
                }

                if (headeredContentControl.Content == null)
                {
                    headeredContentControl.Content = BuildItemLabel($"{controlName} Content");
                }
                break;
            case GroupBox groupBox:
                if (groupBox.Header == null)
                {
                    groupBox.Header = $"{controlName} Header";
                }

                if (groupBox.Content == null)
                {
                    groupBox.Content = BuildItemLabel($"{controlName} Content");
                }
                break;
            case Expander expander:
                if (expander.Header == null)
                {
                    expander.Header = $"{controlName} Header";
                }

                if (expander.Content == null)
                {
                    expander.Content = BuildItemLabel($"{controlName} Content");
                }

                expander.IsExpanded = true;
                break;
            case HeaderedItemsControl headeredItemsControl:
                if (headeredItemsControl.Header == null)
                {
                    headeredItemsControl.Header = $"{controlName} Header";
                }

                EnsureItemsControlHasItems(headeredItemsControl, controlName);
                break;
            case ContentControl contentControl when contentControl.Content == null:
                contentControl.Content = BuildItemLabel($"{controlName} Content");
                break;
            case ItemsControl itemsControl:
                EnsureItemsControlHasItems(itemsControl, controlName);
                break;
            case Decorator decorator when decorator.Child == null:
                decorator.Child = BuildItemLabel($"{controlName} Child");
                break;
            case Panel panel:
                EnsurePanelHasChildren(panel, controlName);
                break;
        }

        if (element is ScrollViewer scrollViewer && scrollViewer.Content == null)
        {
            var stack = new StackPanel();
            stack.AddChild(BuildItemLabel("Scroll line 1"));
            stack.AddChild(BuildItemLabel("Scroll line 2"));
            stack.AddChild(BuildItemLabel("Scroll line 3"));
            scrollViewer.Content = stack;
        }

        if (element is ScrollBar scrollBar)
        {
            scrollBar.Minimum = 0f;
            scrollBar.Maximum = 100f;
            scrollBar.ViewportSize = 20f;
            scrollBar.Value = 35f;
        }

        if (element is Popup popup)
        {
            popup.Content ??= BuildItemLabel("Popup content");
        }

        if (element is ToolTip toolTip)
        {
            toolTip.Content ??= BuildItemLabel("Tooltip content");
        }

        if (element is PasswordBox passwordBox && string.IsNullOrEmpty(passwordBox.Password))
        {
            passwordBox.Password = "demo";
        }

        return element;
    }

    private static void EnsureItemsControlHasItems(ItemsControl itemsControl, string controlName)
    {
        if (itemsControl.Items.Count > 0)
        {
            return;
        }

        switch (itemsControl)
        {
            case Menu menu:
                menu.Items.Add(new MenuItem { Header = "File" });
                menu.Items.Add(new MenuItem { Header = "Edit" });
                break;
            case ContextMenu contextMenu:
                contextMenu.Items.Add(new MenuItem { Header = "Copy" });
                contextMenu.Items.Add(new MenuItem { Header = "Paste" });
                contextMenu.Items.Add(new MenuItem { Header = "Delete" });
                break;
            case TabControl tabControl:
                tabControl.Items.Add(new TabItem { Header = "Tab 1", Content = BuildItemLabel("Tab 1 Content") });
                tabControl.Items.Add(new TabItem { Header = "Tab 2", Content = BuildItemLabel("Tab 2 Content") });
                break;
            case ToolBar toolBar:
                toolBar.Items.Add(new Button { Text = "New" });
                toolBar.Items.Add(new Button { Text = "Open" });
                toolBar.Items.Add(new Button { Text = "Save" });
                break;
            case StatusBar statusBar:
                statusBar.Items.Add(new StatusBarItem { Content = BuildItemLabel("Ready") });
                statusBar.Items.Add(new StatusBarItem { Content = BuildItemLabel("Line 1, Col 1") });
                break;
            case TreeView treeView:
                foreach (var root in BuildSampleTreeViewRoots())
                {
                    treeView.Items.Add(root);
                }

                break;
            case DataGrid dataGrid:
                dataGrid.Items.Add(new DemoRow { Id = 1, Name = "Row 1" });
                dataGrid.Items.Add(new DemoRow { Id = 2, Name = "Row 2" });
                break;
            default:
                itemsControl.Items.Add($"{controlName} Item 1");
                itemsControl.Items.Add($"{controlName} Item 2");
                itemsControl.Items.Add($"{controlName} Item 3");
                break;
        }
    }

    private static void EnsurePanelHasChildren(Panel panel, string controlName)
    {
        if (panel.GetVisualChildren().Any())
        {
            return;
        }

        switch (panel)
        {
            case Grid grid:
            {
                if (grid.RowDefinitions.Count == 0)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                var row0 = BuildItemLabel($"{controlName} Row 0");
                var row1 = BuildItemLabel($"{controlName} Row 1");
                Grid.SetRow(row0, 0);
                Grid.SetRow(row1, 1);
                grid.AddChild(row0);
                grid.AddChild(row1);
                break;
            }
            case Canvas canvas:
            {
                var child = BuildItemLabel($"{controlName} Child");
                Canvas.SetLeft(child, 12f);
                Canvas.SetTop(child, 12f);
                canvas.AddChild(child);
                break;
            }
            case DockPanel dockPanel:
            {
                var left = BuildItemLabel("Left");
                var top = BuildItemLabel("Top");
                var fill = BuildItemLabel("Fill");
                DockPanel.SetDock(left, Dock.Left);
                DockPanel.SetDock(top, Dock.Top);
                dockPanel.AddChild(left);
                dockPanel.AddChild(top);
                dockPanel.AddChild(fill);
                break;
            }
            case ToolBarTray toolBarTray:
            {
                var toolBar = new ToolBar();
                toolBar.Items.Add(new Button { Text = "Cut" });
                toolBar.Items.Add(new Button { Text = "Copy" });
                toolBar.Items.Add(new Button { Text = "Paste" });
                toolBarTray.AddChild(toolBar);
                break;
            }
            default:
                panel.AddChild(BuildItemLabel($"{controlName} Child 1"));
                panel.AddChild(BuildItemLabel($"{controlName} Child 2"));
                break;
        }
    }

    private static void ApplySizingHints(string controlName, FrameworkElement element)
    {
        if (IsThicknessZero(element.Margin))
        {
            element.Margin = new Thickness(4);
        }

        if (float.IsNaN(element.Width))
        {
            element.Width = controlName switch
            {
                "ScrollBar" => 260f,
                "TextBox" => 260f,
                "PasswordBox" => 260f,
                "ComboBox" => 260f,
                "DatePicker" => 260f,
                _ => element.Width
            };
        }

        if (float.IsNaN(element.Height))
        {
            element.Height = controlName switch
            {
                "DataGrid" => 260f,
                "ListBox" => 260f,
                "ListView" => 260f,
                "TreeView" => 260f,
                "Calendar" => 260f,
                "ItemsControl" => 220f,
                "ScrollViewer" => 260f,
                "VirtualizingStackPanel" => 260f,
                "WrapPanel" => 220f,
                "UniformGrid" => 220f,
                _ => element.Height
            };
        }

        element.MinWidth = MathF.Max(120f, element.MinWidth);
        element.MinHeight = MathF.Max(32f, element.MinHeight);
    }

    private static Label BuildItemLabel(string text)
    {
        return new Label
        {
            Text = text,
            Margin = new Thickness(2)
        };
    }

    private static TreeViewItem[] BuildSampleTreeViewRoots()
    {
        var root = new TreeViewItem
        {
            Header = "Root",
            IsExpanded = true
        };

        var childDocuments = new TreeViewItem
        {
            Header = "Documents",
            IsExpanded = true
        };
        childDocuments.Items.Add(new TreeViewItem { Header = "Invoices" });
        childDocuments.Items.Add(new TreeViewItem { Header = "Reports" });

        var childMedia = new TreeViewItem
        {
            Header = "Media"
        };
        childMedia.Items.Add(new TreeViewItem { Header = "Images" });
        childMedia.Items.Add(new TreeViewItem { Header = "Videos" });

        root.Items.Add(childDocuments);
        root.Items.Add(childMedia);

        return [root];
    }

    private static bool IsThicknessZero(Thickness thickness)
    {
        return thickness.Left == 0f &&
               thickness.Top == 0f &&
               thickness.Right == 0f &&
               thickness.Bottom == 0f;
    }

    private static Type? ResolveType(string controlName)
    {
        var asm = typeof(UIElement).Assembly;
        return asm.GetTypes().FirstOrDefault(t => t.Name == controlName && t.Namespace == "InkkSlinger");
    }

    private static Label BuildInfoLabel(string text)
    {
        return new Label
        {
            Text = text,
            Foreground = new Color(215, 235, 252)
        };
    }
}

