using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class PageView : UserControl
{
    private ContentControl? _standaloneHost;
    private Frame? _hostFrame;
    private Label? _standaloneStatusLabel;
    private Label? _hostedStatusLabel;
    private Label? _journalStatusLabel;
    private Button? _backButton;
    private int _workflowStep = 1;

    public PageView()
    {
        InitializeComponent();

        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = BuildDemoSurface();
        }

        InitializePages();
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        ControlDemoSupport.ApplyFontRecursive(this, font);
    }

    private UIElement BuildDemoSurface()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var actions = new WrapPanel
        {
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(actions, 0);
        root.AddChild(actions);

        actions.AddChild(CreateActionButton(
            "Load Intro Page",
            () => NavigateHosted(CreateWorkflowPage("Intro", "Hosted page with NavigationService available."), "Navigate Intro")));
        actions.AddChild(CreateActionButton(
            "Load Checklist Page",
            () => NavigateHosted(CreateWorkflowPage("Checklist", "A second hosted page to create history."), "Navigate Checklist")));
        actions.AddChild(CreateActionButton(
            "Navigate Via Current Page",
            NavigateViaCurrentPage));

        _backButton = CreateActionButton("Back", GoBack);
        actions.AddChild(_backButton);

        var statusPanel = new Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new Color(65, 93, 121),
            Background = new Color(19, 30, 42)
        };
        Grid.SetRow(statusPanel, 1);
        root.AddChild(statusPanel);

        var statusStack = new StackPanel();
        _standaloneStatusLabel = new Label { Foreground = new Color(229, 241, 255) };
        _hostedStatusLabel = new Label { Foreground = new Color(208, 227, 245) };
        _journalStatusLabel = new Label { Foreground = new Color(185, 210, 235) };
        statusStack.AddChild(_standaloneStatusLabel);
        statusStack.AddChild(_hostedStatusLabel);
        statusStack.AddChild(_journalStatusLabel);
        statusPanel.Child = statusStack;

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        Grid.SetRow(layout, 2);
        root.AddChild(layout);

        _standaloneHost = new ContentControl();
        var standalonePanel = BuildPanel("Standalone Page (not in Frame)", _standaloneHost);
        Grid.SetColumn(standalonePanel, 0);
        layout.AddChild(standalonePanel);

        _hostFrame = new Frame
        {
            MinHeight = 240
        };
        var hostedPanel = BuildPanel("Hosted Page (inside Frame)", _hostFrame);
        Grid.SetColumn(hostedPanel, 1);
        layout.AddChild(hostedPanel);

        return root;
    }

    private Border BuildPanel(string title, UIElement content)
    {
        var panel = new Border
        {
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new Color(73, 105, 137),
            Background = new Color(14, 23, 35)
        };

        var stack = new StackPanel();
        stack.AddChild(new Label
        {
            Text = title,
            Foreground = new Color(237, 246, 255),
            Margin = new Thickness(0, 0, 0, 6)
        });
        stack.AddChild(content);
        panel.Child = stack;
        return panel;
    }

    private Button CreateActionButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            Margin = new Thickness(0, 0, 8, 8),
            MinWidth = 140
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void InitializePages()
    {
        if (_standaloneHost != null)
        {
            _standaloneHost.Content = CreateStandalonePage();
        }

        NavigateHosted(
            CreateWorkflowPage("Welcome", "The hosted page receives NavigationService from Frame."),
            "Initialized hosted page.");
    }

    private Page CreateStandalonePage()
    {
        var page = new Page
        {
            Title = "Standalone"
        };

        var stack = new StackPanel
        {
            Margin = new Thickness(8)
        };
        stack.AddChild(new Label
        {
            Text = "This Page is rendered through ContentControl, not Frame.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Color(208, 226, 243)
        });
        stack.AddChild(new Label
        {
            Text = "Expected: Page.NavigationService remains null.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Color(173, 198, 223),
            Margin = new Thickness(0, 6, 0, 0)
        });

        page.Content = stack;
        return page;
    }

    private Page CreateWorkflowPage(string title, string description)
    {
        var page = new Page
        {
            Title = title
        };

        var stack = new StackPanel
        {
            Margin = new Thickness(8)
        };
        stack.AddChild(new Label
        {
            Text = $"Hosted Page: {title}",
            Foreground = new Color(236, 246, 255)
        });
        stack.AddChild(new Label
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Color(203, 224, 246),
            Margin = new Thickness(0, 4, 0, 8)
        });

        var nextButton = new Button
        {
            Text = "Next Via This Page.NavigationService"
        };
        nextButton.Click += (_, _) =>
        {
            if (page.NavigationService == null)
            {
                RefreshState($"Page '{title}' has no NavigationService.");
                return;
            }

            _workflowStep++;
            page.NavigationService.Navigate(CreateWorkflowPage(
                $"Step {_workflowStep}",
                "This page was pushed by the current page itself via NavigationService."));
            RefreshState($"Page '{title}' invoked NavigationService.Navigate(...).");
        };
        stack.AddChild(nextButton);

        page.Content = stack;
        return page;
    }

    private void NavigateHosted(Page page, string action)
    {
        if (_hostFrame == null)
        {
            return;
        }

        _hostFrame.Navigate(page);
        RefreshState(action);
    }

    private void NavigateViaCurrentPage()
    {
        if (_hostFrame?.Content is not Page currentPage)
        {
            RefreshState("No current hosted Page.");
            return;
        }

        if (currentPage.NavigationService == null)
        {
            RefreshState("Current hosted Page has null NavigationService.");
            return;
        }

        _workflowStep++;
        currentPage.NavigationService.Navigate(CreateWorkflowPage(
            $"From Toolbar {_workflowStep}",
            "Toolbar action called NavigationService on the current hosted page."));
        RefreshState("Navigate via current page service.");
    }

    private void GoBack()
    {
        if (_hostFrame == null)
        {
            return;
        }

        if (!_hostFrame.CanGoBack)
        {
            RefreshState("GoBack ignored (no back entry).");
            return;
        }

        _hostFrame.GoBack();
        RefreshState("GoBack executed.");
    }

    private void RefreshState(string action)
    {
        if (_standaloneHost?.Content is Page standalonePage)
        {
            _standaloneStatusLabel!.Text =
                $"Standalone '{standalonePage.Title}' NavigationService: {(standalonePage.NavigationService == null ? "Null" : "Attached")}";
        }

        if (_hostFrame?.Content is Page hostedPage)
        {
            _hostedStatusLabel!.Text =
                $"Hosted '{hostedPage.Title}' NavigationService: {(hostedPage.NavigationService == null ? "Null" : "Attached")}";
        }
        else
        {
            _hostedStatusLabel!.Text = "Hosted content is not a Page.";
        }

        if (_hostFrame != null)
        {
            _journalStatusLabel!.Text =
                $"Action: {action} | CanGoBack={_hostFrame.CanGoBack} | CanGoForward={_hostFrame.CanGoForward}";
        }

        if (_backButton != null)
        {
            _backButton.IsEnabled = _hostFrame?.CanGoBack == true;
        }
    }
}

