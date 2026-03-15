using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class FrameView : UserControl
{
    private Frame? _demoFrame;
    private Label? _actionLabel;
    private Label? _currentContentLabel;
    private Label? _serviceStateLabel;
    private Button? _goBackButton;
    private Button? _goForwardButton;
    private int _dynamicPageCounter = 1;
    private Page? _stickyPage;

    public FrameView()
    {
        InitializeComponent();

        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = BuildDemoSurface();
        }

        NavigateTo(
            BuildPage(
                "Home",
                "Frame keeps object-based journal history. Use Navigate/Back/Forward to inspect live behavior."),
            "Initialized with Home page.");
    }

    private UIElement BuildDemoSurface()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var actions = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(actions, 0);
        root.AddChild(actions);

        var topRow = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        var bottomRow = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        actions.AddChild(topRow);
        actions.AddChild(bottomRow);

        topRow.AddChild(CreateActionButton(
            "Navigate Home",
            () => NavigateTo(
                BuildPage("Home", "This page is the default navigation target."),
                "Navigate(Home)")));
        topRow.AddChild(CreateActionButton(
            "Navigate Details",
            () => NavigateTo(
                BuildPage("Details", "Details page created on demand as a new journal entry."),
                "Navigate(Details)")));
        topRow.AddChild(CreateActionButton(
            "Navigate Dynamic",
            () =>
            {
                _dynamicPageCounter++;
                NavigateTo(
                    BuildPage($"Dynamic {_dynamicPageCounter}", "Runtime-generated page instance."),
                    "Navigate(Dynamic)");
            }));
        topRow.AddChild(CreateActionButton(
            "Navigate Sticky",
            () =>
            {
                _stickyPage ??= BuildPage("Sticky", "Reused page instance to show same-instance behavior.");
                NavigateTo(_stickyPage, "Navigate(Sticky)");
            }));
        bottomRow.AddChild(CreateActionButton(
            "External Content Set",
            () =>
            {
                if (_demoFrame == null)
                {
                    return;
                }

                _demoFrame.Content = new Label
                {
                    Content = new TextBlock
                    {
                        Text = "Frame.Content assigned directly (journal cleared by design).",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new Color(225, 238, 252)
                    }
                };
                RefreshState("External Content assignment.");
            }));

        _goBackButton = CreateActionButton("Go Back", GoBack);
        _goForwardButton = CreateActionButton("Go Forward", GoForward);
        bottomRow.AddChild(_goBackButton);
        bottomRow.AddChild(_goForwardButton);

        var statePanel = new Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new Color(62, 88, 116),
            Background = new Color(20, 30, 43)
        };
        Grid.SetRow(statePanel, 1);
        root.AddChild(statePanel);

        var stateStack = new StackPanel();
        _actionLabel = new Label { Foreground = new Color(244, 249, 255) };
        _currentContentLabel = new Label { Foreground = new Color(215, 231, 247) };
        _serviceStateLabel = new Label { Foreground = new Color(191, 216, 240) };
        stateStack.AddChild(_actionLabel);
        stateStack.AddChild(_currentContentLabel);
        stateStack.AddChild(_serviceStateLabel);
        statePanel.Child = stateStack;

        _demoFrame = new Frame
        {
            Margin = new Thickness(0),
            MinHeight = 250
        };

        var frameHost = new Border
        {
            Padding = new Thickness(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new Color(70, 102, 136),
            Background = new Color(14, 24, 36),
            Child = _demoFrame
        };
        Grid.SetRow(frameHost, 2);
        root.AddChild(frameHost);

        return root;
    }

    private Button CreateActionButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(0, 0, 8, 8),
            MinWidth = 120
        };
        button.Click += (_, _) => action();
        return button;
    }

    private Page BuildPage(string title, string description)
    {
        var page = new Page
        {
            Title = title
        };

        var content = new StackPanel
        {
            Margin = new Thickness(8)
        };
        content.AddChild(new Label
        {
            Content = $"Page: {title}",
            Foreground = new Color(236, 246, 255)
        });
        content.AddChild(new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Color(202, 225, 247),
            Margin = new Thickness(0, 4, 0, 6)
        });

        var inPageNavigate = new Button
        {
            Content = "Navigate Next Via Page.NavigationService",
            Margin = new Thickness(0, 0, 0, 4)
        };
        inPageNavigate.Click += (_, _) =>
        {
            if (page.NavigationService == null)
            {
                RefreshState($"Page '{title}' has no NavigationService (not hosted in Frame).");
                return;
            }

            _dynamicPageCounter++;
            page.NavigationService.Navigate(BuildPage(
                $"From {title} {_dynamicPageCounter}",
                "This navigation originated from inside the currently hosted page."));
            RefreshState($"Page '{title}' invoked NavigationService.Navigate(...).");
        };
        content.AddChild(inPageNavigate);

        content.AddChild(new TextBlock
        {
            Text = "Tip: Navigate Sticky repeatedly to inspect same-instance journal behavior.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Color(172, 199, 226)
        });

        page.Content = content;
        return page;
    }

    private void NavigateTo(object content, string action)
    {
        if (_demoFrame == null)
        {
            return;
        }

        _demoFrame.Navigate(content);
        RefreshState(action);
    }

    private void GoBack()
    {
        if (_demoFrame == null)
        {
            return;
        }

        if (!_demoFrame.CanGoBack)
        {
            RefreshState("GoBack ignored (no back entry).");
            return;
        }

        _demoFrame.GoBack();
        RefreshState("GoBack executed.");
    }

    private void GoForward()
    {
        if (_demoFrame == null)
        {
            return;
        }

        if (!_demoFrame.CanGoForward)
        {
            RefreshState("GoForward ignored (no forward entry).");
            return;
        }

        _demoFrame.GoForward();
        RefreshState("GoForward executed.");
    }

    private void RefreshState(string action)
    {
        if (_demoFrame == null)
        {
            return;
        }

        if (_goBackButton != null)
        {
            _goBackButton.IsEnabled = _demoFrame.CanGoBack;
        }

        if (_goForwardButton != null)
        {
            _goForwardButton.IsEnabled = _demoFrame.CanGoForward;
        }

        if (_actionLabel != null)
        {
            _actionLabel.Content = $"Action: {action}";
        }

        if (_demoFrame.Content is Page page)
        {
            _currentContentLabel!.Content = $"Current content: Page \"{page.Title}\"";
            _serviceStateLabel!.Content = $"Current page service: {(page.NavigationService != null ? "Attached" : "Null")}";
            return;
        }

        var contentName = _demoFrame.Content?.GetType().Name ?? "(null)";
        _currentContentLabel!.Content = $"Current content: {contentName}";
        _serviceStateLabel!.Content = "Current page service: n/a (content is not Page)";
    }
}



