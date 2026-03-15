using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ApplicationButtonThemeTests
{
    [Fact]
    public void AppResourceStyle_KeyedByButtonType_ShouldApplyImplicitlyToButtons()
    {
        var resources = UiApplication.Current.Resources;
        var key = (object)typeof(Button);
        var hadExisting = resources.TryGetValue(key, out var existingValue);

        try
        {
            var themedStyle = new Style(typeof(Button));
            themedStyle.Setters.Add(new Setter(Button.BackgroundProperty, new Color(201, 33, 141)));
            themedStyle.Setters.Add(new Setter(Button.BorderBrushProperty, new Color(5, 240, 255)));
            themedStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, 4f));
            resources[key] = themedStyle;

            var host = new Panel
            {
                Width = 320f,
                Height = 220f
            };
            var button = new Button
            {
                Content = "Theme Probe",
                Width = 180f,
                Height = 36f
            };
            host.AddChild(button);

            var uiRoot = new UiRoot(host);
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 320, 220));

            Assert.Equal(new Color(201, 33, 141), button.Background);
            Assert.Equal(new Color(5, 240, 255), button.BorderBrush);
            Assert.Equal(4f, button.BorderThickness);
        }
        finally
        {
            if (hadExisting)
            {
                resources[key] = existingValue!;
            }
            else
            {
                resources.Remove(key);
            }
        }
    }

    [Fact]
    public void ExistingButton_ShouldReactWhenAppButtonStyleResourceChanges()
    {
        var resources = UiApplication.Current.Resources;
        var key = (object)typeof(Button);
        var hadExisting = resources.TryGetValue(key, out var existingValue);

        try
        {
            resources.Remove(key);

            var host = new Panel
            {
                Width = 320f,
                Height = 220f
            };
            var button = new Button
            {
                Content = "Live Theme Probe",
                Width = 180f,
                Height = 36f
            };
            host.AddChild(button);

            var uiRoot = new UiRoot(host);
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, 320, 220));

            var liveStyle = new Style(typeof(Button));
            liveStyle.Setters.Add(new Setter(Button.BackgroundProperty, new Color(34, 188, 51)));
            liveStyle.Setters.Add(new Setter(Button.BorderBrushProperty, new Color(255, 90, 12)));
            resources[key] = liveStyle;

            Assert.Equal(new Color(34, 188, 51), button.Background);
            Assert.Equal(new Color(255, 90, 12), button.BorderBrush);
        }
        finally
        {
            if (hadExisting)
            {
                resources[key] = existingValue!;
            }
            else
            {
                resources.Remove(key);
            }
        }
    }
}
