using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public partial class MenuParityLabView : UserControl
{
    private RoutedCommand _openConsoleCommand = new("OpenConsole", typeof(MenuParityLabView));
    private RoutedCommand _openClassLibCommand = new("OpenClassLibrary", typeof(MenuParityLabView));
    private RoutedCommand _openSettingsCommand = new("OpenSettings", typeof(MenuParityLabView));
    private RoutedCommand _runCommand = new("Run", typeof(MenuParityLabView));
    private RoutedCommand _aboutCommand = new("About", typeof(MenuParityLabView));
    private SpriteFont? _currentFont;

    public MenuParityLabView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "MenuParityLabView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        ResolveCommandsFromResources();
        BuildMenu();
        WireCommanding();
        WireInteractionLogging();
        AppendLog("Menu parity lab ready.");
        UpdateStatus("Ready. Start with F10 or Alt+F.");
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        _currentFont = font;
        ApplyFontRecursive(this, font);
    }

    private void ResolveCommandsFromResources()
    {
        if (TryFindResource("OpenConsoleCommand", out var openConsole) && openConsole is RoutedCommand openConsoleCommand)
        {
            _openConsoleCommand = openConsoleCommand;
        }

        if (TryFindResource("OpenClassLibCommand", out var openClassLib) && openClassLib is RoutedCommand openClassLibCommand)
        {
            _openClassLibCommand = openClassLibCommand;
        }

        if (TryFindResource("OpenSettingsCommand", out var openSettings) && openSettings is RoutedCommand openSettingsCommand)
        {
            _openSettingsCommand = openSettingsCommand;
        }

        if (TryFindResource("RunCommand", out var run) && run is RoutedCommand runCommand)
        {
            _runCommand = runCommand;
        }

        if (TryFindResource("AboutCommand", out var about) && about is RoutedCommand aboutCommand)
        {
            _aboutCommand = aboutCommand;
        }
    }

    private void BuildMenu()
    {
        if (MainMenu == null)
        {
            return;
        }

        var file = new MenuItem { Header = "_File" };
        var @new = new MenuItem { Header = "_New" };
        var project = new MenuItem { Header = "_Project" };
        var console = new MenuItem { Header = "_Console App", InputGestureText = "Ctrl+N" };
        var classLibrary = new MenuItem { Header = "_Class Library" };
        project.Items.Add(console);
        project.Items.Add(classLibrary);
        @new.Items.Add(project);

        var settings = new MenuItem { Header = "_Settings", InputGestureText = "Ctrl+," };
        var exit = new MenuItem { Header = "E_xit", InputGestureText = "Alt+F4" };
        file.Items.Add(@new);
        file.Items.Add(settings);
        file.Items.Add(new Separator());
        file.Items.Add(exit);

        var edit = new MenuItem { Header = "_Edit" };
        edit.Items.Add(new MenuItem { Header = "_Undo", InputGestureText = "Ctrl+Z" });
        edit.Items.Add(new MenuItem { Header = "_Redo", InputGestureText = "Ctrl+Y" });

        var view = new MenuItem { Header = "_View" };
        var layout = new MenuItem { Header = "_Layout" };
        layout.Items.Add(new MenuItem { Header = "_Dock Left" });
        layout.Items.Add(new MenuItem { Header = "_Dock Right" });
        layout.Items.Add(new MenuItem { Header = "__Literal" });
        view.Items.Add(layout);

        var help = new MenuItem { Header = "_Help" };
        var about = new MenuItem { Header = "_About", InputGestureText = "F1" };
        help.Items.Add(about);

        MainMenu.Items.Add(file);
        MainMenu.Items.Add(edit);
        MainMenu.Items.Add(view);
        MainMenu.Items.Add(help);

        console.Command = _openConsoleCommand;
        classLibrary.Command = _openClassLibCommand;
        settings.Command = _openSettingsCommand;
        about.Command = _aboutCommand;
        exit.Command = _runCommand;
        exit.CommandParameter = "Exit";
    }

    private void WireCommanding()
    {
        var target = (UIElement?)EditorTextBox ?? this;

        CommandBindings.Add(new CommandBinding(_openConsoleCommand, (_, _) => OnMenuCommand("Open Console App"), OnAlwaysCanExecute));
        CommandBindings.Add(new CommandBinding(_openClassLibCommand, (_, _) => OnMenuCommand("Open Class Library"), OnAlwaysCanExecute));
        CommandBindings.Add(new CommandBinding(_openSettingsCommand, (_, _) => OnMenuCommand("Open Settings"), OnAlwaysCanExecute));
        CommandBindings.Add(new CommandBinding(_aboutCommand, (_, _) => OnMenuCommand("About"), OnAlwaysCanExecute));
        CommandBindings.Add(new CommandBinding(_runCommand, OnRunExecuted, OnAlwaysCanExecute));

        if (MainMenu != null)
        {
            foreach (var topLevel in MainMenu.GetTopLevelItems())
            {
                AssignCommandTargetsRecursive(topLevel, target);
            }
        }
    }

    private void WireInteractionLogging()
    {
        AddHandler(UIElement.KeyDownEvent, (object? _, KeyRoutedEventArgs args) =>
        {
            if (args.Key is Keys.F10 or Keys.Escape or Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Enter or Keys.Space)
            {
                AppendLog($"Key: {args.Key} Modifiers={args.Modifiers}");
            }
        }, handledEventsToo: true);

        AddHandler(UIElement.GotFocusEvent, (object? sender, FocusChangedRoutedEventArgs _) =>
        {
            AppendLog($"Focus -> {DescribeElement(sender as UIElement)}");
        }, handledEventsToo: true);
    }

    private void OnAlwaysCanExecute(object? sender, CanExecuteRoutedEventArgs args)
    {
        _ = sender;
        args.CanExecute = true;
        args.Handled = true;
    }

    private void OnRunExecuted(object? sender, ExecutedRoutedEventArgs args)
    {
        _ = sender;
        var action = args.Parameter?.ToString() ?? "Run";
        OnMenuCommand(action);
    }

    private void OnMenuCommand(string action)
    {
        AppendLog($"Command executed: {action}");
        UpdateStatus($"Executed: {action}");
    }

    private void AssignCommandTargetsRecursive(MenuItem item, UIElement target)
    {
        item.CommandTarget = target;
        foreach (var child in item.GetChildMenuItems())
        {
            AssignCommandTargetsRecursive(child, target);
        }
    }

    private void AppendLog(string message)
    {
        if (LogList == null)
        {
            return;
        }

        var line = $"{DateTime.Now:HH:mm:ss} {message}";
        var label = new Label { Text = line };
        if (_currentFont != null)
        {
            label.Font = _currentFont;
        }

        LogList.Items.Add(label);
        while (LogList.Items.Count > 250)
        {
            LogList.Items.RemoveAt(0);
        }
    }

    private void UpdateStatus(string message)
    {
        if (StatusLabel != null)
        {
            StatusLabel.Text = message;
        }
    }

    private static string DescribeElement(UIElement? element)
    {
        if (element == null)
        {
            return "null";
        }

        return element.GetType().Name;
    }

    private static void ApplyFontRecursive(UIElement? element, SpriteFont font)
    {
        if (element == null)
        {
            return;
        }

        if (element is TextBlock textBlock)
        {
            textBlock.Font = font;
        }

        if (element is Label label)
        {
            label.Font = font;
        }

        if (element is TextBox textBox)
        {
            textBox.Font = font;
        }

        if (element is MenuItem menuItem)
        {
            menuItem.Font = font;
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }
}
