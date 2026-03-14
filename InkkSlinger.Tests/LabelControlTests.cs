using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class LabelControlTests
{
    [Fact]
    public void Label_StringContent_UsesAccessTextPresenter()
    {
        var label = new Label
        {
            Content = "_Name"
        };

        RunLayout(label);

        var presenter = Assert.IsType<ContentPresenter>(FindDescendant<ContentPresenter>(label));
        var accessText = Assert.IsType<AccessText>(Assert.Single(presenter.GetVisualChildren()));
        Assert.Equal("Name", accessText.DisplayText);
        Assert.False(label.Focusable);
    }

    [Fact]
    public void Label_UiElementContent_IsPresentedDirectly()
    {
        var content = new TextBlock
        {
            Text = "Value"
        };
        var label = new Label
        {
            Content = content
        };

        RunLayout(label);

        var presenter = Assert.IsType<ContentPresenter>(FindDescendant<ContentPresenter>(label));
        Assert.Same(content, Assert.Single(presenter.GetVisualChildren()));
    }

    [Fact]
    public void Label_ContentTemplate_BuildsTemplatedPresentation()
    {
        var label = new Label
        {
            Content = 42,
            ContentTemplate = new DataTemplate(static item => new TextBlock
            {
                Text = $"templated:{item}"
            })
        };

        RunLayout(label);

        var presenter = Assert.IsType<ContentPresenter>(FindDescendant<ContentPresenter>(label));
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(presenter.GetVisualChildren()));
        Assert.Equal("templated:42", textBlock.Text);
    }

    [Fact]
    public void Label_Target_UpdatesAutomationPropertiesLabeledBy()
    {
        var first = new TextBox();
        var second = new TextBox();
        var label = new Label();

        label.Target = first;
        Assert.Same(label, AutomationProperties.GetLabeledBy(first));

        label.Target = second;
        Assert.Null(AutomationProperties.GetLabeledBy(first));
        Assert.Same(label, AutomationProperties.GetLabeledBy(second));

        label.Target = null;
        Assert.Null(AutomationProperties.GetLabeledBy(second));
    }

    [Fact]
    public void Label_AccessKey_FocusesTarget()
    {
        var root = new StackPanel();
        var input = new TextBox
        {
            Name = "InputBox"
        };
        var label = new Label
        {
            Content = "_Name",
            Target = input
        };

        root.AddChild(label);
        root.AddChild(input);

        var uiRoot = RunLayout(root);
        FocusManager.ClearFocus();
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.N, new KeyboardState(Keys.LeftAlt)));

        Assert.Same(input, FocusManager.GetFocusedElement());
    }

    [Fact]
    public void Label_Xaml_TextNodeContent_AndPlainNameTarget_Parse()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <StackPanel>
    <TextBox x:Name="InputBox" />
    <Label x:Name="NameLabel" Target="InputBox">_Name</Label>
  </StackPanel>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var label = Assert.IsType<Label>(root.FindName("NameLabel"));
        var input = Assert.IsType<TextBox>(root.FindName("InputBox"));

        Assert.Equal("_Name", Assert.IsType<string>(label.Content));
        Assert.Same(input, label.Target);
    }

    [Fact]
    public void Label_Xaml_XReferenceTarget_Resolves()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <StackPanel>
    <TextBox x:Name="InputBox" />
    <Label x:Name="NameLabel" Target="{x:Reference InputBox}">_Name</Label>
  </StackPanel>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var label = Assert.IsType<Label>(root.FindName("NameLabel"));
        var input = Assert.IsType<TextBox>(root.FindName("InputBox"));

        Assert.Same(input, label.Target);
    }

    [Fact]
    public void Label_Xaml_WhitespaceOnlyTextNode_IsIgnored()
    {
        const string xaml = """
<Label xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
  
</Label>
""";

        var label = Assert.IsType<Label>(XamlLoader.LoadFromString(xaml));
        Assert.Null(label.Content);
    }

    [Fact]
    public void Label_Xaml_MixedTextAndVisualChild_Throws()
    {
        const string xaml = """
<Label xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  _Name
  <Border />
</Label>
""";

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("cannot mix implicit text content", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static UiRoot RunLayout(UIElement content)
    {
        var uiRoot = new UiRoot(content);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 800, 400));
        return uiRoot;
    }

    private static T? FindDescendant<T>(UIElement root)
        where T : UIElement
    {
        if (root is T match)
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindDescendant<T>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static InputDelta CreateKeyDownDelta(Keys key, KeyboardState keyboard)
    {
        var pointer = new Vector2(16f, 16f);
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(keyboard, default, pointer),
            PressedKeys = new List<Keys> { key },
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }
}
