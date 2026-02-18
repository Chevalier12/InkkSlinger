using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class BindingParityGap5DemoView : UserControl
{
    private readonly BindingParityGap5ViewModel _viewModel = new();
    private SpriteFont? _currentFont;

    public BindingParityGap5DemoView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "BindingParityGap5DemoView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        DataContext = _viewModel;
        WireExceptionFilterBinding();
        UpdateGateFromCheckbox();
        AppendLog("Ready.");
        UpdateStatus("Type values, then commit groups.");
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

    private void OnUsePrimaryChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateGateFromCheckbox();
    }

    private void OnCommitRootClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        var committed = RootGroupHost?.BindingGroup?.CommitEdit() == true;
        AppendLog($"Commit RootGroup => {committed}");
        UpdateStatus(committed ? "RootGroup committed." : "RootGroup commit failed.");
    }

    private void OnCommitInnerClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        var committed = InnerGroupHost?.BindingGroup?.CommitEdit() == true;
        AppendLog($"Commit InnerGroup => {committed}");
        UpdateStatus(committed ? "InnerGroup committed." : "InnerGroup commit failed.");
    }

    private void OnValidateRootClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        var valid = RootGroupHost?.BindingGroup?.ValidateWithoutUpdate() == true;
        AppendLog($"Validate RootGroup => {valid}");
        UpdateStatus(valid ? "RootGroup valid." : "RootGroup has validation errors.");
    }

    private void OnCancelRootClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        RootGroupHost?.BindingGroup?.CancelEdit();
        AppendLog("Cancel RootGroup.");
        UpdateStatus("RootGroup canceled.");
    }

    private void WireExceptionFilterBinding()
    {
        if (FaultyInput == null)
        {
            return;
        }

        BindingOperations.SetBinding(
            FaultyInput,
            TextBox.TextProperty,
            new Binding
            {
                Source = _viewModel,
                Path = nameof(BindingParityGap5ViewModel.FaultyValue),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnExceptions = true,
                UpdateSourceExceptionFilter = OnUpdateSourceException
            });
    }

    private object? OnUpdateSourceException(object bindingExpression, Exception exception)
    {
        var message = $"Filtered source exception: {exception.Message}";
        AppendLog(message);
        UpdateStatus(message);
        return message;
    }

    private void UpdateGateFromCheckbox()
    {
        var enabled = UsePrimaryCheck?.IsChecked == true;
        if (TryFindResource("PrimaryGate", out var gateResource) && gateResource is PriorityGateConverter gate)
        {
            gate.Enabled = enabled;
            _viewModel.RefreshPriorityBinding();
            if (PriorityOutput != null)
            {
                BindingOperations.UpdateTarget(PriorityOutput, TextBlock.TextProperty);
            }
        }

        AppendLog($"Primary gate => {enabled}");
    }

    private void UpdateStatus(string message)
    {
        if (StatusLabel != null)
        {
            StatusLabel.Text = message;
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

        LogList.Items.Insert(0, label);
        while (LogList.Items.Count > 120)
        {
            LogList.Items.RemoveAt(LogList.Items.Count - 1);
        }
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

        if (element is Button button)
        {
            button.Font = font;
        }

        if (element is CheckBox checkBox)
        {
            checkBox.Font = font;
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }
}

public sealed class PriorityGateConverter : IValueConverter
{
    public bool Enabled { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!Enabled)
        {
            throw new InvalidOperationException("primary source is gated off");
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}

public sealed class BindingParityGap5ViewModel : INotifyPropertyChanged
{
    private string _primaryText = "Primary text";
    private string _secondaryText = "Secondary text";
    private string _rootValue = "root-initial";
    private string _innerValue = "inner-initial";
    private string _faultyValue = "faulty";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PrimaryText
    {
        get => _primaryText;
        set
        {
            if (string.Equals(_primaryText, value, StringComparison.Ordinal))
            {
                return;
            }

            _primaryText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PrimaryText)));
        }
    }

    public string SecondaryText
    {
        get => _secondaryText;
        set
        {
            if (string.Equals(_secondaryText, value, StringComparison.Ordinal))
            {
                return;
            }

            _secondaryText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SecondaryText)));
        }
    }

    public string RootValue
    {
        get => _rootValue;
        set
        {
            if (string.Equals(_rootValue, value, StringComparison.Ordinal))
            {
                return;
            }

            _rootValue = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RootValue)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CommittedSummary)));
        }
    }

    public string InnerValue
    {
        get => _innerValue;
        set
        {
            if (string.Equals(_innerValue, value, StringComparison.Ordinal))
            {
                return;
            }

            _innerValue = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InnerValue)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CommittedSummary)));
        }
    }

    public string FaultyValue
    {
        get => _faultyValue;
        set => throw new InvalidOperationException($"FaultyValue rejected '{value}'.");
    }

    public string CommittedSummary => $"RootValue='{RootValue}', InnerValue='{InnerValue}'";

    public void RefreshPriorityBinding()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PrimaryText)));
    }
}
