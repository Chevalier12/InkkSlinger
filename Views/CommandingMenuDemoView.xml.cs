using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class CommandingMenuDemoView : UserControl
{
    private RoutedCommand _newCommand = new("New", typeof(CommandingMenuDemoView));
    private RoutedCommand _openCommand = new("Open", typeof(CommandingMenuDemoView));
    private RoutedCommand _exitCommand = new("Exit", typeof(CommandingMenuDemoView));
    private RelayCommand? _directCommand;
    private SpriteFont? _currentFont;

    public CommandingMenuDemoView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "CommandingMenuDemoView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        ResolveCommandsFromResources();

        BuildMenu();
        WireCommanding();

        AppendLog("Ready. Try Alt+F, Ctrl+N, and switching focus between Editor/Side.");
        UpdateStatus("Demo initialized.");
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

    private void BuildMenu()
    {
        if (MainMenu == null)
        {
            return;
        }

        var file = new MenuItem { Header = "_File" };
        var itemNew = new MenuItem { Header = "_New", InputGestureText = "Ctrl+N" };
        var itemOpen = new MenuItem { Header = "_Open", InputGestureText = "Ctrl+O" };
        var itemExit = new MenuItem { Header = "E_xit", InputGestureText = "Alt+F4" };

        file.Items.Add(itemNew);
        file.Items.Add(itemOpen);
        file.Items.Add(new Separator());
        file.Items.Add(itemExit);

        var help = new MenuItem { Header = "_Help" };
        var about = new MenuItem { Header = "_About" };
        about.Click += (_, _) =>
        {
            AppendLog("About: RoutedCommand + CommandBinding + InputBinding + access keys demo.");
            UpdateStatus("About clicked.");
        };
        help.Items.Add(about);

        MainMenu.Items.Add(file);
        MainMenu.Items.Add(help);

        // Keep references via logical tree lookup.
        NewMenuItem = itemNew;
        OpenMenuItem = itemOpen;
        ExitMenuItem = itemExit;
    }

    // These are assigned in BuildMenu() so we don't need XAML collection syntax support.
    private MenuItem? NewMenuItem { get; set; }
    private MenuItem? OpenMenuItem { get; set; }
    private MenuItem? ExitMenuItem { get; set; }

    private void WireCommanding()
    {
        var target = (UIElement?)EditorTextBox ?? this;

        // Command bindings live on the view so routed commands can target Editor/Side and walk up to here.
        CommandBindings.Add(new CommandBinding(_newCommand, OnNewExecuted, OnAlwaysCanExecute));
        CommandBindings.Add(new CommandBinding(_openCommand, OnOpenExecuted, OnOpenCanExecute));
        CommandBindings.Add(new CommandBinding(_exitCommand, OnExitExecuted, OnAlwaysCanExecute));

        if (NewMenuItem != null)
        {
            NewMenuItem.Command = _newCommand;
            NewMenuItem.CommandTarget = target;
        }

        if (OpenMenuItem != null)
        {
            OpenMenuItem.Command = _openCommand;
            OpenMenuItem.CommandTarget = target;
        }

        if (ExitMenuItem != null)
        {
            ExitMenuItem.Command = _exitCommand;
            ExitMenuItem.CommandTarget = target;
        }

        if (NewButton != null)
        {
            NewButton.Command = _newCommand;
            NewButton.CommandTarget = target;
        }

        if (OpenButton != null)
        {
            OpenButton.Command = _openCommand;
            OpenButton.CommandTarget = target;
        }

        _directCommand = new RelayCommand(_ =>
        {
            AppendLog("Direct ICommand executed (RelayCommand).");
            UpdateStatus("Direct command executed.");
        });

        if (DirectButton != null)
        {
            DirectButton.Command = _directCommand;
        }
    }

    private void ResolveCommandsFromResources()
    {
        if (TryFindResource("NewCommand", out var newCommandResource) && newCommandResource is RoutedCommand newCommand)
        {
            _newCommand = newCommand;
        }

        if (TryFindResource("OpenCommand", out var openCommandResource) && openCommandResource is RoutedCommand openCommand)
        {
            _openCommand = openCommand;
        }

        if (TryFindResource("ExitCommand", out var exitCommandResource) && exitCommandResource is RoutedCommand exitCommand)
        {
            _exitCommand = exitCommand;
        }
    }

    private void OnAlwaysCanExecute(object? sender, CanExecuteRoutedEventArgs args)
    {
        args.CanExecute = true;
        args.Handled = true;
    }

    private void OnOpenCanExecute(object? sender, CanExecuteRoutedEventArgs args)
    {
        var allowToggle = AllowOpenCheckBox?.IsChecked == true;
        args.CanExecute = allowToggle;
        args.Handled = true;
    }

    private void OnNewExecuted(object? sender, ExecutedRoutedEventArgs args)
    {
        AppendLog("New executed (RoutedCommand).");
        UpdateStatus("New executed.");
    }

    private void OnOpenExecuted(object? sender, ExecutedRoutedEventArgs args)
    {
        AppendLog("Open executed (RoutedCommand).");
        UpdateStatus("Open executed.");
    }

    private void OnExitExecuted(object? sender, ExecutedRoutedEventArgs args)
    {
        AppendLog("Exit executed (RoutedCommand).");
        UpdateStatus("Exit executed (demo).");
    }

    private void OnAllowOpenChanged(object? sender, RoutedSimpleEventArgs args)
    {
        AppendLog($"AllowOpen toggled: {AllowOpenCheckBox?.IsChecked}");
        CommandManager.InvalidateRequerySuggested();
    }

    private void OnInvalidateRequeryClick(object? sender, RoutedSimpleEventArgs args)
    {
        AppendLog("Manual requery invalidation requested.");
        CommandManager.InvalidateRequerySuggested();
    }

    private void AppendLog(string message)
    {
        if (LogList == null)
        {
            return;
        }

        var line = $"{DateTime.Now:HH:mm:ss} {message}";
        var label = new Label
        {
            Text = line
        };

        if (_currentFont != null)
        {
            label.Font = _currentFont;
        }

        // Add UIElements so the label carries a font even for items appended after SetFont() ran.
        LogList.Items.Add(label);

        // Avoid unbounded growth during long runs.
        while (LogList.Items.Count > 200)
        {
            LogList.Items.RemoveAt(0);
        }
    }

    private void UpdateStatus(string message)
    {
        if (StatusLabel == null)
        {
            return;
        }

        StatusLabel.Text = message;
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

        if (element is Button button)
        {
            button.Font = font;
        }

        if (element is TextBox textBox)
        {
            textBox.Font = font;
        }

        if (element is CheckBox checkBox)
        {
            checkBox.Font = font;
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
