using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Xna.Framework;

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
        "RenderSurface",
        "RenderSurface [GPU]",
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

    private static readonly IReadOnlyDictionary<string, Func<UserControl>> CatalogViewFactories =
        new Dictionary<string, Func<UserControl>>(StringComparer.Ordinal)
        {
            ["AccessText"] = static () => new AccessTextView(),
            ["Border"] = static () => new BorderView(),
            ["Button"] = static () => new ButtonView(),
            ["Calendar"] = static () => new CalendarView(),
            ["Canvas"] = static () => new CanvasView(),
            ["CatchMe"] = static () => new CatchMeView(),
            ["CheckBox"] = static () => new CheckBoxView(),
            ["ComboBox"] = static () => new ComboBoxView(),
            ["ComboBoxItem"] = static () => new ComboBoxItemView(),
            ["ContentControl"] = static () => new ContentControlView(),
            ["ContentPresenter"] = static () => new ContentPresenterView(),
            ["ContextMenu"] = static () => new ContextMenuView(),
            ["Control"] = static () => new ControlView(),
            ["DataGrid"] = static () => new DataGridView(),
            ["DataGridCell"] = static () => new DataGridCellView(),
            ["DataGridColumnHeader"] = static () => new DataGridColumnHeaderView(),
            ["DataGridDetailsPresenter"] = static () => new DataGridDetailsPresenterView(),
            ["DataGridRow"] = static () => new DataGridRowView(),
            ["DataGridRowHeader"] = static () => new DataGridRowHeaderView(),
            ["DatePicker"] = static () => new DatePickerView(),
            ["Decorator"] = static () => new DecoratorView(),
            ["DockPanel"] = static () => new DockPanelView(),
            ["DocumentViewer"] = static () => new DocumentViewerView(),
            ["Expander"] = static () => new ExpanderView(),
            ["Frame"] = static () => new FrameView(),
            ["Grid"] = static () => new GridView(),
            ["GridSplitter"] = static () => new GridSplitterView(),
            ["GroupBox"] = static () => new GroupBoxView(),
            ["GroupItem"] = static () => new GroupItemView(),
            ["HeaderedContentControl"] = static () => new HeaderedContentControlView(),
            ["HeaderedItemsControl"] = static () => new HeaderedItemsControlView(),
            ["Image"] = static () => new ImageView(),
            ["InkCanvas"] = static () => new InkCanvasView(),
            ["InkPresenter"] = static () => new InkPresenterView(),
            ["ItemsControl"] = static () => new ItemsControlView(),
            ["Label"] = static () => new LabelView(),
            ["ListBox"] = static () => new ListBoxView(),
            ["ListBoxItem"] = static () => new ListBoxItemView(),
            ["ListView"] = static () => new ListViewView(),
            ["ListViewItem"] = static () => new ListViewItemView(),
            ["MediaElement"] = static () => new MediaElementView(),
            ["Menu"] = static () => new MenuView(),
            ["MenuItem"] = static () => new MenuItemView(),
            ["Page"] = static () => new PageView(),
            ["Panel"] = static () => new PanelView(),
            ["PasswordBox"] = static () => new PasswordBoxView(),
            ["Popup"] = static () => new PopupView(),
            ["ProgressBar"] = static () => new ProgressBarView(),
            ["RadioButton"] = static () => new RadioButtonView(),
            ["RepeatButton"] = static () => new RepeatButtonView(),
            ["RenderSurface"] = static () => new RenderSurfaceView(),
            ["RenderSurface [GPU]"] = static () => new RenderSurfaceGpuView(),
            ["ResizeGrip"] = static () => new ResizeGripView(),
            ["RichTextBox"] = static () => new RichTextBoxView(),
            ["ScrollBar"] = static () => new ScrollBarView(),
            ["ScrollViewer"] = static () => new ScrollViewerView(),
            ["Separator"] = static () => new SeparatorView(),
            ["Slider"] = static () => new SliderView(),
            ["StackPanel"] = static () => new StackPanelView(),
            ["StatusBar"] = static () => new StatusBarView(),
            ["StatusBarItem"] = static () => new StatusBarItemView(),
            ["TabControl"] = static () => new TabControlView(),
            ["TabItem"] = static () => new TabItemView(),
            ["TextBlock"] = static () => new TextBlockView(),
            ["TextBox"] = static () => new TextBoxView(),
            ["Thumb"] = static () => new ThumbView(),
            ["ToggleButton"] = static () => new ToggleButtonView(),
            ["ToolBar"] = static () => new ToolBarView(),
            ["ToolBarOverflowPanel"] = static () => new ToolBarOverflowPanelView(),
            ["ToolBarPanel"] = static () => new ToolBarPanelView(),
            ["ToolBarTray"] = static () => new ToolBarTrayView(),
            ["ToolTip"] = static () => new ToolTipView(),
            ["TreeView"] = static () => new TreeViewView(),
            ["TreeViewItem"] = static () => new TreeViewItemView(),
            ["UniformGrid"] = static () => new UniformGridView(),
            ["UserControl"] = static () => new UserControlView(),
            ["Viewbox"] = static () => new ViewboxView(),
            ["VirtualizingStackPanel"] = static () => new VirtualizingStackPanelView(),
            ["WrapPanel"] = static () => new WrapPanelView(),
            ["Window"] = static () => new WindowView(),
        };

    internal static UserControl CreateCatalogView(string controlName)
    {
        if (CatalogViewFactories.TryGetValue(controlName, out var factory))
        {
            return factory();
        }

        return new MissingControlView(controlName);
    }

    internal static bool HasCatalogView(string controlName)
    {
        return CatalogViewFactories.ContainsKey(controlName);
    }
}

internal static class ControlDemoSupport
{
    internal sealed class DemoRow
    {
        public required int Id { get; init; }

        public required string Name { get; init; }
    }

    private static readonly DemoRow[] SampleDataGridRows =
    {
        new() { Id = 1, Name = "Alpha" },
        new() { Id = 2, Name = "Bravo" },
        new() { Id = 3, Name = "Charlie" },
        new() { Id = 4, Name = "Delta" },
        new() { Id = 5, Name = "Echo" },
        new() { Id = 6, Name = "Foxtrot" },
        new() { Id = 7, Name = "Golf" },
        new() { Id = 8, Name = "Hotel" },
        new() { Id = 9, Name = "India" },
        new() { Id = 10, Name = "Juliet" },
        new() { Id = 11, Name = "Kilo" },
        new() { Id = 12, Name = "Lima" },
    };

    private static readonly IReadOnlyDictionary<string, Func<UIElement>> DefaultSampleFactories =
        new Dictionary<string, Func<UIElement>>(StringComparer.Ordinal)
        {
            ["AccessText"] = static () => new AccessText(),
            ["Border"] = static () => new Border(),
            ["Button"] = static () => new Button(),
            ["Calendar"] = static () => new Calendar(),
            ["Canvas"] = static () => new Canvas(),
            ["CheckBox"] = static () => new CheckBox(),
            ["ComboBox"] = static () => new ComboBox(),
            ["ComboBoxItem"] = static () => new ComboBoxItem(),
            ["ContentControl"] = static () => new ContentControl(),
            ["ContentPresenter"] = static () => new ContentPresenter(),
            ["ContextMenu"] = static () => new ContextMenu(),
            ["Control"] = static () => new Control(),
            ["DataGrid"] = static () => new DataGrid(),
            ["DataGridCell"] = static () => new DataGridCell(),
            ["DataGridColumnHeader"] = static () => new DataGridColumnHeader(),
            ["DataGridDetailsPresenter"] = static () => new DataGridDetailsPresenter(),
            ["DataGridRow"] = static () => new DataGridRow(),
            ["DataGridRowHeader"] = static () => new DataGridRowHeader(),
            ["DatePicker"] = static () => new DatePicker(),
            ["Decorator"] = static () => new Decorator(),
            ["DockPanel"] = static () => new DockPanel(),
            ["DocumentViewer"] = static () => new DocumentViewer(),
            ["Expander"] = static () => new Expander(),
            ["Frame"] = static () => new Frame(),
            ["Grid"] = static () => new Grid(),
            ["GridSplitter"] = static () => new GridSplitter(),
            ["GroupBox"] = static () => new GroupBox(),
            ["GroupItem"] = static () => new GroupItem(),
            ["HeaderedContentControl"] = static () => new HeaderedContentControl(),
            ["HeaderedItemsControl"] = static () => new HeaderedItemsControl(),
            ["Image"] = static () => new Image(),
            ["ItemsControl"] = static () => new ItemsControl(),
            ["Label"] = static () => new Label(),
            ["ListBox"] = static () => new ListBox(),
            ["ListBoxItem"] = static () => new ListBoxItem(),
            ["ListView"] = static () => new ListView(),
            ["ListViewItem"] = static () => new ListViewItem(),
            ["Menu"] = static () => new Menu(),
            ["MenuItem"] = static () => new MenuItem(),
            ["Page"] = static () => new Page(),
            ["Panel"] = static () => new Panel(),
            ["PasswordBox"] = static () => new PasswordBox(),
            ["Popup"] = static () => new Popup(),
            ["ProgressBar"] = static () => new ProgressBar(),
            ["RadioButton"] = static () => new RadioButton(),
            ["RepeatButton"] = static () => new RepeatButton(),
            ["RenderSurface"] = static () => new RenderSurface(),
            ["RenderSurface [GPU]"] = static () => new RenderSurface(),
            ["ResizeGrip"] = static () => new ResizeGrip(),
            ["RichTextBox"] = static () => new RichTextBox(),
            ["ScrollBar"] = static () => new ScrollBar(),
            ["ScrollViewer"] = static () => new ScrollViewer(),
            ["Separator"] = static () => new Separator(),
            ["Slider"] = static () => new Slider(),
            ["StackPanel"] = static () => new StackPanel(),
            ["StatusBar"] = static () => new StatusBar(),
            ["StatusBarItem"] = static () => new StatusBarItem(),
            ["TabControl"] = static () => new TabControl(),
            ["TabItem"] = static () => new TabItem(),
            ["TextBlock"] = static () => new TextBlock(),
            ["TextBox"] = static () => new TextBox(),
            ["Thumb"] = static () => new Thumb(),
            ["ToggleButton"] = static () => new ToggleButton(),
            ["ToolBar"] = static () => new ToolBar(),
            ["ToolBarOverflowPanel"] = static () => new ToolBarOverflowPanel(),
            ["ToolBarPanel"] = static () => new ToolBarPanel(),
            ["ToolBarTray"] = static () => new ToolBarTray(),
            ["ToolTip"] = static () => new ToolTip(),
            ["TreeView"] = static () => new TreeView(),
            ["TreeViewItem"] = static () => new TreeViewItem(),
            ["UniformGrid"] = static () => new UniformGrid(),
            ["UserControl"] = static () => new UserControl(),
            ["Viewbox"] = static () => new Viewbox(),
            ["VirtualizingStackPanel"] = static () => new VirtualizingStackPanel(),
            ["WrapPanel"] = static () => new WrapPanel(),
        };

    private static ObservableCollection<DemoRow> CreateSampleDataGridItemsSource()
    {
        var items = new ObservableCollection<DemoRow>(
            SampleDataGridRows.Select(static row => new DemoRow
            {
                Id = row.Id,
                Name = row.Name
            }));
        return items;
    }

    internal static UIElement BuildSampleElement(string controlName)
    {
        try
        {
            var sample = TryBuildKnownSample(controlName) ?? TryBuildDefaultSample(controlName);
            if (sample == null)
            {
                return BuildInfoLabel($"{controlName} is not implemented as a UIElement in this framework.");
            }

            return EnsureSampleFilled(controlName, sample);
        }
        catch (Exception ex)
        {
            return BuildInfoLabel($"Failed to create {controlName}: {ex.GetType().Name}");
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
                return new Label { Content = "Label" };
            case "TextBlock":
                return new TextBlock { Text = "TextBlock sample" };
            case "AccessText":
            {
                var root = new StackPanel
                {
                    Margin = new Thickness(4)
                };
                var button = new Button
                {
                    Name = "SaveButton",
                    Text = "Save",
                    Width = 180f
                };
                var accessText = new AccessText
                {
                    Text = "_Save action",
                    TargetName = "SaveButton",
                    Margin = new Thickness(0, 0, 0, 8),
                    Foreground = new Color(232, 245, 255)
                };
                var hint = new Label
                {
                    Content = "Press Alt+S to invoke Save.",
                    Foreground = new Color(180, 210, 235)
                };
                root.AddChild(accessText);
                root.AddChild(button);
                root.AddChild(hint);
                return root;
            }
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
                var dg = new DataGrid
                {
                    BorderThickness = 1f,
                    HeadersVisibility = DataGridHeadersVisibility.Column
                };
                dg.ItemsSource = CreateSampleDataGridItemsSource();
                return dg;
            }
            case "DataGridCell":
            case "DataGridColumnHeader":
            case "DataGridDetailsPresenter":
            case "DataGridRow":
            case "DataGridRowHeader":
                return BuildInfoLabel($"{controlName} is generated by DataGrid; see DataGrid sample with rows/columns.");
            case "ContentControl":
                return new ContentControl { Content = new Label { Content = "Content" } };
            case "Border":
                return new Border
                {
                    Padding = new Thickness(8),
                    BorderBrush = new Color(80, 120, 170),
                    BorderThickness = new Thickness(1),
                    Child = new Label { Content = "Border child" }
                };
            case "StackPanel":
            {
                var sp = new StackPanel();
                sp.AddChild(new Label { Content = "Top" });
                sp.AddChild(new Label { Content = "Bottom" });
                return sp;
            }
            case "VirtualizingStackPanel":
            {
                var vsp = new VirtualizingStackPanel();
                for (var i = 1; i <= 20; i++)
                {
                    vsp.AddChild(new Label { Content = $"Item {i}" });
                }

                return vsp;
            }
            case "Panel":
            {
                var panel = new Panel();
                panel.AddChild(new Label { Content = "Panel child 1" });
                panel.AddChild(new Label { Content = "Panel child 2" });
                return panel;
            }
            case "Decorator":
                return new Decorator { Child = new Label { Content = "Decorator child" } };
            case "Grid":
            {
                var g = new Grid();
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var a = new Label { Content = "Row 0" };
                var b = new Label { Content = "Row 1" };
                Grid.SetRow(a, 0);
                Grid.SetRow(b, 1);
                g.AddChild(a);
                g.AddChild(b);
                return g;
            }
            case "Canvas":
            {
                var c = new Canvas();
                var t = new Label { Content = "Canvas child" };
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
                    sp.AddChild(new Label { Content = $"Line {i}" });
                }

                sv.Content = sp;
                return sv;
            }
            case "ToolTip":
                return new ToolTip { Content = new Label { Content = "Tooltip content" } };
            case "Popup":
            {
                var popup = new Popup
                {
                    Content = new Label { Content = "Popup content (open)" }
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
            case "RenderSurface":
            case "RenderSurface [GPU]":
                return new RenderSurface
                {
                    Width = 320f,
                    Height = 224f,
                    Stretch = Stretch.Uniform
                };
            case "Page":
                return new Page
                {
                    Title = "Sample Page",
                    Content = BuildItemLabel("Page Content")
                };
            case "Frame":
            {
                var frame = new Frame();
                frame.Navigate(new Page
                {
                    Title = "Frame Start",
                    Content = BuildItemLabel("Frame Current Content")
                });
                return frame;
            }
            case "DocumentViewer":
            {
                var viewer = new DocumentViewer
                {
                    Zoom = 100f
                };
                viewer.Document = BuildDocumentViewerSampleDocument();
                return viewer;
            }
            case "UserControl":
                return new UserControl { Content = new Label { Content = "UserControl content" } };
            case "Window":
                return BuildInfoLabel("Window is host-level in this framework and not instanced inside another view.");
            default:
                return null;
        }
    }

    private static UIElement? TryBuildDefaultSample(string controlName)
    {
        return DefaultSampleFactories.TryGetValue(controlName, out var factory)
            ? factory()
            : null;
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
                dataGrid.ItemsSource = CreateSampleDataGridItemsSource();
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
                "DocumentViewer" => 520f,
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
                "DocumentViewer" => 320f,
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
            Content = text,
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

    private static Label BuildInfoLabel(string text)
    {
        return new Label
        {
            Content = text,
            Foreground = new Color(215, 235, 252)
        };
    }

    private static FlowDocument BuildDocumentViewerSampleDocument()
    {
        var document = new FlowDocument();

        var intro = new Paragraph();
        intro.Inlines.Add(new Run("DocumentViewer sample: "));
        var bold = new Bold();
        bold.Inlines.Add(new Run("read-only paging"));
        intro.Inlines.Add(bold);
        intro.Inlines.Add(new Run(", selection, and hyperlinks."));
        document.Blocks.Add(intro);

        var linkParagraph = new Paragraph();
        var hyperlink = new Hyperlink { NavigateUri = "https://example.com/docs" };
        hyperlink.Inlines.Add(new Run("Open documentation"));
        linkParagraph.Inlines.Add(hyperlink);
        document.Blocks.Add(linkParagraph);

        var list = new InkkSlinger.List { IsOrdered = true };
        list.Items.Add(BuildListItem("Zoom in/out and fit width."));
        list.Items.Add(BuildListItem("Navigate pages with PageUp/PageDown."));
        list.Items.Add(BuildListItem("Copy selected rich text to clipboard."));
        document.Blocks.Add(list);

        var table = new Table();
        var group = new TableRowGroup();
        var header = new TableRow();
        header.Cells.Add(BuildCell("Feature"));
        header.Cells.Add(BuildCell("Status"));
        group.Rows.Add(header);
        var row = new TableRow();
        row.Cells.Add(BuildCell("Flow paging"));
        row.Cells.Add(BuildCell("Implemented"));
        group.Rows.Add(row);
        table.RowGroups.Add(group);
        document.Blocks.Add(table);

        for (var i = 1; i <= 18; i++)
        {
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run($"Line {i:D2}: scrolling content to validate page boundaries and wheel behavior."));
            document.Blocks.Add(paragraph);
        }

        return document;
    }

    private static ListItem BuildListItem(string text)
    {
        var item = new ListItem();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(text));
        item.Blocks.Add(paragraph);
        return item;
    }

    private static TableCell BuildCell(string text)
    {
        var cell = new TableCell();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(text));
        cell.Blocks.Add(paragraph);
        return cell;
    }
}

