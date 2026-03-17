using Xunit;

namespace InkkSlinger.Tests;

public sealed class AutomationPatternValueTests
{
    [Fact]
    public void TextBoxPeer_ExposesValuePattern_AndCanSetValue()
    {
        var host = new Canvas();
        var textBox = new TextBox { Text = "seed" };
        host.AddChild(textBox);
        var uiRoot = new UiRoot(host);
        var peer = uiRoot.Automation.GetPeer(textBox);
        Assert.NotNull(peer);

        Assert.True(peer.TryGetPattern(AutomationPatternType.Value, out var provider));
        var valueProvider = Assert.IsAssignableFrom<IValueProvider>(provider);

        valueProvider.SetValue("updated");
        Assert.Equal("updated", textBox.Text);

        uiRoot.Shutdown();
    }

    [Fact]
    public void PasswordBoxPeer_ExposesReadOnlyValuePattern()
    {
        var host = new Canvas();
        var passwordBox = new PasswordBox { Password = "secret" };
        host.AddChild(passwordBox);
        var uiRoot = new UiRoot(host);
        var peer = uiRoot.Automation.GetPeer(passwordBox);
        Assert.NotNull(peer);

        Assert.True(peer.TryGetPattern(AutomationPatternType.Value, out var provider));
        var valueProvider = Assert.IsAssignableFrom<IValueProvider>(provider);

        Assert.True(valueProvider.IsReadOnly);
        Assert.Equal(string.Empty, valueProvider.Value);

        uiRoot.Shutdown();
    }

    [Fact]
    public void RichTextBoxPeer_ExposesValuePattern_AndCanSetValue()
    {
        var host = new Canvas();
        var richTextBox = new RichTextBox();
        DocumentEditing.ReplaceAllText(richTextBox.Document, "seed");
        host.AddChild(richTextBox);
        var uiRoot = new UiRoot(host);
        var peer = uiRoot.Automation.GetPeer(richTextBox);
        Assert.NotNull(peer);

        Assert.True(peer.TryGetPattern(AutomationPatternType.Value, out var provider));
        var valueProvider = Assert.IsAssignableFrom<IValueProvider>(provider);

        valueProvider.SetValue("updated");
        Assert.Equal("updated", DocumentEditing.GetText(richTextBox.Document));

        uiRoot.Shutdown();
    }
}
