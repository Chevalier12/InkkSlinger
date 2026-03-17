using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    [Fact]
    public void ControlsCatalog_Labels_BuildPresentedTextChildren()
    {
        var catalog = new ControlsCatalogView();

        var uiRoot = RunLayout(catalog);
        uiRoot.RebuildRenderListForTests();

        var header = Assert.IsType<Label>(catalog.FindName("SelectedControlLabel"));
        var presenter = Assert.IsType<ContentPresenter>(FindDescendant<ContentPresenter>(header));
        var presented = Assert.Single(presenter.GetVisualChildren());
        var headerAccessText = Assert.IsType<AccessText>(presented);
        Assert.True(headerAccessText.DesiredSize.X > 0f);
        Assert.Contains(headerAccessText, uiRoot.GetRetainedVisualOrderForTests());

        var sidebarHeader = FindLabelByContent(catalog, "Control Views");
        presenter = Assert.IsType<ContentPresenter>(FindDescendant<ContentPresenter>(sidebarHeader));
        presented = Assert.Single(presenter.GetVisualChildren());
        var sidebarAccessText = Assert.IsType<AccessText>(presented);
        Assert.True(sidebarAccessText.DesiredSize.X > 0f);
        Assert.Contains(sidebarAccessText, uiRoot.GetRetainedVisualOrderForTests());

        var host = Assert.IsType<StackPanel>(catalog.FindName("ControlButtonsHost"));
        var button = Assert.IsType<Button>(host.Children[0]);
        presenter = Assert.IsType<ContentPresenter>(FindDescendant<ContentPresenter>(button));
        presented = Assert.Single(presenter.GetVisualChildren());
        var fallbackLabel = Assert.IsType<Label>(presented);
        var nestedPresenter = Assert.IsType<ContentPresenter>(FindDescendant<ContentPresenter>(fallbackLabel));
        var nestedText = Assert.IsType<AccessText>(Assert.Single(nestedPresenter.GetVisualChildren()));
        Assert.True(nestedText.DesiredSize.X > 0f);
        Assert.Contains(nestedText, uiRoot.GetRetainedVisualOrderForTests());
    }

    [Fact]
    public void ControlsCatalog_WithAppResources_BuildsButtonAndLabelPresentedTextChildren()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            var appPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "App.xml"));
            XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);

            var catalog = new ControlsCatalogView();
            var uiRoot = RunLayout(catalog);
            uiRoot.RebuildRenderListForTests();

            var header = Assert.IsType<Label>(catalog.FindName("SelectedControlLabel"));
            var headerPresenter = Assert.IsType<ContentPresenter>(FindDescendant<ContentPresenter>(header));
            var headerText = Assert.IsType<AccessText>(Assert.Single(headerPresenter.GetVisualChildren()));
            Assert.True(headerText.DesiredSize.X > 0f);

            var host = Assert.IsType<StackPanel>(catalog.FindName("ControlButtonsHost"));
            var button = Assert.IsType<Button>(host.Children[0]);
            var border = Assert.IsType<Border>(Assert.Single(button.GetVisualChildren()));
            Assert.NotNull(border.Child);
            var presenter = Assert.IsType<ContentPresenter>(FindDescendant<ContentPresenter>(button));
            var fallbackLabel = Assert.IsType<Label>(Assert.Single(presenter.GetVisualChildren()));
            var nestedPresenter = Assert.IsType<ContentPresenter>(FindDescendant<ContentPresenter>(fallbackLabel));
            var nestedText = Assert.IsType<AccessText>(Assert.Single(nestedPresenter.GetVisualChildren()));
            Assert.True(nestedText.DesiredSize.X > 0f);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
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

    private static Label FindLabelByContent(UIElement root, string text)
    {
        if (root is Label label && string.Equals(label.GetContentText(), text, StringComparison.Ordinal))
        {
            return label;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindLabelByContent(child, text);
            if (found != null)
            {
                return found;
            }
        }

        throw new InvalidOperationException($"Could not find label with content '{text}'.");
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

    private static Dictionary<object, object> SnapshotApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
    }

    private static void RestoreApplicationResources(Dictionary<object, object> snapshot)
    {
        TestApplicationResources.Restore(snapshot);
    }
}
