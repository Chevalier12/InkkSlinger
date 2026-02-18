using System.Reflection;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class PasswordBoxTests
{
    [Fact]
    public void PasswordBox_PropertyDefaults_AreExpected()
    {
        var passwordBox = new PasswordBox();

        Assert.Equal(string.Empty, passwordBox.Password);
        Assert.Equal("•", passwordBox.PasswordChar);
        Assert.False(passwordBox.RevealPassword);
        Assert.False(passwordBox.AllowClipboardCopy);
        Assert.False(passwordBox.IsReadOnly);
        Assert.Equal(0, passwordBox.MaxLength);
        Assert.Equal(TextWrapping.NoWrap, passwordBox.TextWrapping);
        Assert.Equal(ScrollBarVisibility.Disabled, passwordBox.VerticalScrollBarVisibility);
    }

    [Fact]
    public void PasswordBox_SetPassword_RaisesPasswordChanged_AndRespectsMaxLength()
    {
        var passwordBox = new PasswordBox { MaxLength = 4 };
        var raised = 0;
        passwordBox.PasswordChanged += (_, _) => raised++;

        passwordBox.Password = "abcdefgh";

        Assert.Equal("abcd", passwordBox.Password);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void PasswordBox_TextInput_AppendsPassword_WhenEditable()
    {
        var passwordBox = new PasswordBox();
        passwordBox.SetFocusedFromInput(true);

        Assert.True(passwordBox.HandleTextInputFromInput('a'));
        Assert.True(passwordBox.HandleTextInputFromInput('b'));
        Assert.True(passwordBox.HandleTextInputFromInput('c'));

        Assert.Equal("abc", passwordBox.Password);
    }

    [Fact]
    public void PasswordBox_BackspaceDelete_AndSelectionBehaveLikeTextEditingBuffer()
    {
        var passwordBox = new PasswordBox { Password = "abcd" };
        passwordBox.SetFocusedFromInput(true);

        _ = passwordBox.HandleKeyDownFromInput(Keys.End, ModifierKeys.None);
        Assert.True(passwordBox.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));
        Assert.Equal("abc", passwordBox.Password);

        Assert.True(passwordBox.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control));
        _ = passwordBox.HandleKeyDownFromInput(Keys.Delete, ModifierKeys.None);
        Assert.Equal(string.Empty, passwordBox.Password);
    }

    [Fact]
    public void PasswordBox_RevealPassword_TogglesDisplayMode_WithoutChangingStoredPassword()
    {
        var passwordBox = new PasswordBox
        {
            Password = "S3cr3t!"
        };

        passwordBox.RevealPassword = true;
        passwordBox.RevealPassword = false;

        Assert.Equal("S3cr3t!", passwordBox.Password);
    }

    [Fact]
    public void PasswordBox_CopyCut_BlockedByDefault_AllowedWhenEnabled()
    {
        TextClipboard.ResetForTests();
        var passwordBox = new PasswordBox { Password = "secret" };
        passwordBox.SetFocusedFromInput(true);

        Assert.True(passwordBox.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control));
        Assert.True(passwordBox.HandleKeyDownFromInput(Keys.C, ModifierKeys.Control));
        Assert.False(TextClipboard.TryGetText(out _));

        passwordBox.AllowClipboardCopy = true;
        Assert.True(passwordBox.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control));
        Assert.True(passwordBox.HandleKeyDownFromInput(Keys.C, ModifierKeys.Control));
        Assert.True(TextClipboard.TryGetText(out var copied));
        Assert.Equal("secret", copied);

        Assert.True(passwordBox.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control));
        Assert.True(passwordBox.HandleKeyDownFromInput(Keys.X, ModifierKeys.Control));
        Assert.Equal(string.Empty, passwordBox.Password);
        Assert.True(TextClipboard.TryGetText(out var cut));
        Assert.Equal("secret", cut);
    }

    [Fact]
    public void RenderCachePolicy_PasswordBox_IsCacheableOnlyWhenStable()
    {
        var policy = new DefaultRenderCachePolicy();
        var passwordBox = new PasswordBox();
        var context = new RenderCachePolicyContext(
            IsEffectivelyVisible: true,
            HasBoundsSnapshot: true,
            BoundsSnapshot: new LayoutRect(0f, 0f, 300f, 80f),
            HasTransformState: false,
            HasClipState: true,
            RenderStateStepCount: 1,
            RenderStateSignature: 101,
            SubtreeVisualCount: 1,
            SubtreeHighCostVisualCount: 1,
            SubtreeRenderVersionStamp: passwordBox.RenderCacheRenderVersion,
            SubtreeLayoutVersionStamp: passwordBox.RenderCacheLayoutVersion);

        Assert.True(policy.CanCache(passwordBox, context));

        passwordBox.SetValue(PasswordBox.IsFocusedProperty, true);
        Assert.False(policy.CanCache(passwordBox, context));
    }

    [Fact]
    public void XamlLoader_CanInstantiatePasswordBox_AndApplyAttributes()
    {
        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <PasswordBox x:Name="Pwd"
               Password="abc"
               MaxLength="2"
               RevealPassword="True"
               AllowClipboardCopy="True"
               PasswordChar="*" />
</UserControl>
""";

        var root = XamlLoader.LoadFromString(xml);
        var passwordBox = Assert.IsType<PasswordBox>(Assert.Single(root.GetVisualChildren()));

        Assert.Equal("ab", passwordBox.Password);
        Assert.True(passwordBox.RevealPassword);
        Assert.True(passwordBox.AllowClipboardCopy);
        Assert.Equal("*", passwordBox.PasswordChar);
    }

    [Fact]
    public void XNameSourceGenerator_AssignsPasswordBoxNamedFields_InDemoView()
    {
        var view = new PasswordBoxDemoView();
        var property = typeof(PasswordBoxDemoView).GetProperty("PasswordInput", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(property);
        var value = property!.GetValue(view);
        Assert.NotNull(value);
        Assert.IsType<PasswordBox>(value);
    }
}



