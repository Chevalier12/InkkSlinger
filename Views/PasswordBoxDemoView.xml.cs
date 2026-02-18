using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class PasswordBoxDemoView : UserControl
{
    private SpriteFont? _currentFont;

    public PasswordBoxDemoView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "PasswordBoxDemoView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        UpdateStatus("Ready.");
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

    private void OnPasswordChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        var length = PasswordInput?.Password.Length ?? 0;
        AppendLog($"{DateTime.Now:HH:mm:ss} PasswordChanged length={length}");
        UpdateStatus($"Length: {length}");
    }

    private void OnRevealClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (PasswordInput == null || RevealCheck == null)
        {
            return;
        }

        PasswordInput.RevealPassword = RevealCheck.IsChecked == true;
        UpdateStatus($"Reveal: {PasswordInput.RevealPassword}");
    }

    private void OnReadOnlyClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (PasswordInput == null || ReadOnlyCheck == null)
        {
            return;
        }

        PasswordInput.IsReadOnly = ReadOnlyCheck.IsChecked == true;
        UpdateStatus($"ReadOnly: {PasswordInput.IsReadOnly}");
    }

    private void OnAllowCopyClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (PasswordInput == null || AllowCopyCheck == null)
        {
            return;
        }

        PasswordInput.AllowClipboardCopy = AllowCopyCheck.IsChecked == true;
        UpdateStatus($"AllowClipboardCopy: {PasswordInput.AllowClipboardCopy}");
    }

    private void OnMaskDotClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (PasswordInput == null)
        {
            return;
        }

        PasswordInput.PasswordChar = "•";
        UpdateStatus("Mask set to bullet.");
    }

    private void OnMaskStarClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (PasswordInput == null)
        {
            return;
        }

        PasswordInput.PasswordChar = "*";
        UpdateStatus("Mask set to star.");
    }

    private void OnClearClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (PasswordInput == null)
        {
            return;
        }

        PasswordInput.Password = string.Empty;
        UpdateStatus("Password cleared.");
    }

    private void OnSetSampleClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (PasswordInput == null)
        {
            return;
        }

        PasswordInput.Password = "S3cur3-P@ssw0rd";
        UpdateStatus("Sample password assigned.");
    }

    private void UpdateStatus(string message)
    {
        if (StatusLabel != null)
        {
            StatusLabel.Text = message;
        }
    }

    private void AppendLog(string line)
    {
        if (EventLogBox == null)
        {
            return;
        }

        var current = EventLogBox.Text;
        if (string.IsNullOrEmpty(current))
        {
            EventLogBox.Text = line;
            return;
        }

        EventLogBox.Text = $"{line}\n{current}";
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

        if (element is PasswordBox passwordBox)
        {
            passwordBox.Font = font;
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

