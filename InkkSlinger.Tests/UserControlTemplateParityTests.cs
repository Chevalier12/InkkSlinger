using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class UserControlTemplateParityTests
{
    [Fact]
    public void UserControl_Template_DirectSet_DoesNotThrow_AndBuildsTemplateVisualTree()
    {
        var userControl = new UserControl
        {
            Template = new ControlTemplate(_ => new Border())
            {
                TargetType = typeof(UserControl)
            }
        };

        Assert.True(userControl.ApplyTemplate());
        var templateRoot = Assert.IsType<Border>(Assert.Single(userControl.GetVisualChildren()));
        Assert.Same(userControl, templateRoot.VisualParent);
    }

    [Fact]
    public void UserControl_Template_FromStyle_Setter_Applies_And_ContentPresenter_ShowsContent()
    {
        var style = new Style(typeof(UserControl));
        style.Setters.Add(
            new Setter(
                Control.TemplateProperty,
                new ControlTemplate(_ =>
                {
                    var root = new Border();
                    root.Child = new ContentPresenter();
                    return root;
                })
                {
                    TargetType = typeof(UserControl)
                }));

        var payload = new Border
        {
            Width = 40f,
            Height = 20f
        };

        var userControl = new UserControl
        {
            Width = 180f,
            Height = 90f,
            Content = payload
        };

        style.Apply(userControl);
        RunLayout(userControl, 320, 200);

        var presenter = FindDescendant<ContentPresenter>(userControl);
        Assert.Same(payload, Assert.Single(presenter.GetVisualChildren()));
    }

    [Fact]
    public void UserControl_Template_WithoutContentPresenter_HidesContent_WpfStrict()
    {
        var payload = new Border
        {
            Width = 70f,
            Height = 30f
        };

        var userControl = new UserControl
        {
            Width = 180f,
            Height = 90f,
            Content = payload,
            Template = new ControlTemplate(_ => new Border())
            {
                TargetType = typeof(UserControl)
            }
        };

        RunLayout(userControl, 320, 200);

        Assert.Null(FindDescendantOrDefault<Border>(userControl, b => ReferenceEquals(b, payload)));
        Assert.Equal(Vector2.Zero, payload.DesiredSize);
    }

    [Fact]
    public void UserControl_NotTemplated_PreservesLegacyChromeAndContentLayoutBehavior()
    {
        var payload = new Border();
        var userControl = new UserControl
        {
            Width = 120f,
            Height = 80f,
            BorderThickness = new Thickness(2f, 3f, 4f, 5f),
            Padding = new Thickness(6f, 7f, 8f, 9f),
            Content = payload
        };

        var uiRoot = BuildUiRootWithSingleChild(userControl, 320, 200, x: 10f, y: 15f);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 320, 200));

        var chrome = new Thickness(8f, 10f, 12f, 14f);
        Assert.Equal(userControl.LayoutSlot.X + chrome.Left, payload.LayoutSlot.X);
        Assert.Equal(userControl.LayoutSlot.Y + chrome.Top, payload.LayoutSlot.Y);
        Assert.Equal(userControl.LayoutSlot.Width - chrome.Horizontal, payload.LayoutSlot.Width);
        Assert.Equal(userControl.LayoutSlot.Height - chrome.Vertical, payload.LayoutSlot.Height);
    }

    [Fact]
    public void XamlLoader_UserControlTemplate_PropertyElement_ParsesAndApplies()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                         Width="160"
                                         Height="90">
                              <UserControl.Template>
                                <ControlTemplate TargetType="{x:Type UserControl}">
                                  <Border x:Name="TemplateRoot">
                                    <ContentPresenter x:Name="TemplatePresenter" />
                                  </Border>
                                </ControlTemplate>
                              </UserControl.Template>
                              <Border x:Name="Payload" Width="36" Height="24" />
                            </UserControl>
                            """;

        var root = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
        Assert.True(root.ApplyTemplate());
        RunLayout(root, 320, 200);

        var templateRoot = FindDescendant<Border>(root, b => string.Equals(b.Name, "TemplateRoot", StringComparison.Ordinal));
        Assert.Same(templateRoot, Assert.Single(root.GetVisualChildren()));
        Assert.NotNull(root.Content);
    }

    [Fact]
    public void UserControl_Content_MustStillBeUIElement_WhenTemplateEnabled()
    {
        var userControl = new UserControl
        {
            Template = new ControlTemplate(_ => new Border())
            {
                TargetType = typeof(UserControl)
            }
        };

        Assert.Throws<InvalidOperationException>(() => ((ContentControl)userControl).Content = "plain text");
    }

    [Fact]
    public void UserControl_Template_TargetTypeMismatch_StillThrows()
    {
        var userControl = new UserControl();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            userControl.Template = new ControlTemplate(_ => new Border())
            {
                TargetType = typeof(Button)
            });
        Assert.Contains("not compatible", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserControl_Template_SetThenClear_RevertsToNonTemplatedPath_AndRemovesTemplateVisuals()
    {
        var payload = new Border
        {
            Width = 42f,
            Height = 28f
        };

        var userControl = new UserControl
        {
            Width = 150f,
            Height = 100f,
            BorderThickness = new Thickness(2f, 3f, 4f, 5f),
            Padding = new Thickness(6f, 7f, 8f, 9f),
            Content = payload,
            Template = new ControlTemplate(_ =>
            {
                var root = new Border { Name = "TemplateRootA" };
                root.Child = new ContentPresenter();
                return root;
            })
            {
                TargetType = typeof(UserControl)
            }
        };

        RunLayout(userControl, 320, 200);
        var templateRoot = FindDescendant<Border>(userControl, b => string.Equals(b.Name, "TemplateRootA", StringComparison.Ordinal));
        Assert.Same(templateRoot, Assert.Single(userControl.GetVisualChildren()));

        userControl.Template = null;
        RunLayout(userControl, 320, 200);

        Assert.False(IsDescendant(userControl, templateRoot));
        Assert.Same(payload, Assert.Single(userControl.GetVisualChildren()));
        Assert.Same(userControl, payload.VisualParent);
        Assert.Same(userControl, payload.LogicalParent);
    }

    [Fact]
    public void UserControl_Template_SetTwice_ReplacesVisualTree_InsteadOfAppending()
    {
        var userControl = new UserControl
        {
            Width = 150f,
            Height = 100f,
            Content = new Border { Width = 30f, Height = 20f },
            Template = new ControlTemplate(_ => new Border { Name = "TemplateRootA" })
            {
                TargetType = typeof(UserControl)
            }
        };

        RunLayout(userControl, 320, 200);
        var firstRoot = FindDescendant<Border>(userControl, b => string.Equals(b.Name, "TemplateRootA", StringComparison.Ordinal));

        userControl.Template = new ControlTemplate(_ => new Border { Name = "TemplateRootB" })
        {
            TargetType = typeof(UserControl)
        };
        RunLayout(userControl, 320, 200);

        Assert.False(IsDescendant(userControl, firstRoot));
        var secondRoot = FindDescendant<Border>(userControl, b => string.Equals(b.Name, "TemplateRootB", StringComparison.Ordinal));
        Assert.Same(secondRoot, Assert.Single(userControl.GetVisualChildren()));
    }

    private static void RunLayout(FrameworkElement element, int width, int height)
    {
        var uiRoot = BuildUiRootWithSingleChild(element, width, height, x: 10f, y: 15f);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }

    private static UiRoot BuildUiRootWithSingleChild(FrameworkElement element, int width, int height, float x, float y)
    {
        if (element.VisualParent is Panel existingPanel)
        {
            _ = existingPanel.RemoveChild(element);
        }
        else
        {
            element.SetVisualParent(null);
            element.SetLogicalParent(null);
        }

        var host = new Canvas
        {
            Width = width,
            Height = height
        };
        host.AddChild(element);
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        return new UiRoot(host);
    }

    private static T FindDescendant<T>(UIElement root, Func<T, bool>? predicate = null)
        where T : UIElement
    {
        return FindDescendantOrDefault(root, predicate)
               ?? throw new InvalidOperationException($"Could not find descendant of type '{typeof(T).Name}'.");
    }

    private static T? FindDescendantOrDefault<T>(UIElement root, Func<T, bool>? predicate = null)
        where T : UIElement
    {
        var pending = new Stack<UIElement>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is T typed && (predicate == null || predicate(typed)))
            {
                return typed;
            }

            foreach (var child in current.GetVisualChildren())
            {
                pending.Push(child);
            }
        }

        return null;
    }

    private static bool IsDescendant(UIElement root, UIElement target)
    {
        var pending = new Stack<UIElement>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (ReferenceEquals(current, target))
            {
                return true;
            }

            foreach (var child in current.GetVisualChildren())
            {
                pending.Push(child);
            }
        }

        return false;
    }
}
