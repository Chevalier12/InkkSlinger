using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AccessTextXamlTests
{
    [Fact]
    public void LoadFromXaml_CreatesAccessText()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <AccessText x:Name="ShortcutText" Text="_Save" />
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var accessText = Assert.IsType<AccessText>(root.FindName("ShortcutText"));
        Assert.Equal("Save", accessText.DisplayText);
        Assert.Equal('S', accessText.AccessKey);
    }

    [Fact]
    public void LoadFromXaml_TargetName_ActivatesNamedButtonViaAltKey()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <StackPanel>
    <AccessText Text="_Save" TargetName="SaveButton" />
    <Button x:Name="SaveButton" Text="Save" />
  </StackPanel>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var button = Assert.IsType<Button>(root.FindName("SaveButton"));
        var executions = 0;
        button.Click += (_, _) => executions++;

        var uiRoot = new UiRoot(root);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 800, 400));

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.S, new KeyboardState(Keys.LeftAlt)));

        Assert.Equal(1, executions);
    }

    [Fact]
    public void LoadFromXaml_AccessTextWithUnknownAttribute_Throws()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <AccessText Text="_Save" UnknownOption="x" />
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("UnknownOption", ex.Message, StringComparison.OrdinalIgnoreCase);
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
