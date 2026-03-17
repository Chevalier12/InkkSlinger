using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ProgressBarBehaviorTests
{
    [Fact]
    public void TemplatedIndicator_HorizontalWidth_TracksNormalizedValue()
    {
        var (uiRoot, progressBar) = BuildTemplatedProgressBar();
        RunLayout(uiRoot, 240, 120);

        var indicator = FindNamedVisualChild<FrameworkElement>(progressBar, "PART_Indicator");
        Assert.NotNull(indicator);
        Assert.Equal(50f, indicator!.ActualWidth, 2);
        Assert.Equal(20f, indicator.ActualHeight, 2);
        Assert.Equal(0f, indicator.Margin.Left, 2);
        Assert.Equal(0f, indicator.Margin.Top, 2);
    }

    [Fact]
    public void TemplatedIndicator_VerticalFill_GrowsFromBottom()
    {
        var (uiRoot, progressBar) = BuildTemplatedProgressBar(orientation: Orientation.Vertical, width: 20f, height: 200f, value: 25f);
        RunLayout(uiRoot, 120, 260);

        var indicator = FindNamedVisualChild<FrameworkElement>(progressBar, "PART_Indicator");
        Assert.NotNull(indicator);
        Assert.Equal(20f, indicator!.ActualWidth, 2);
        Assert.Equal(50f, indicator.ActualHeight, 2);
        Assert.Equal(150f, indicator.Margin.Top, 2);
    }

    [Fact]
    public void IndeterminateTemplate_AdvancesIndicatorAndGlowAcrossFrames()
    {
        var (uiRoot, progressBar) = BuildTemplatedProgressBar(isIndeterminate: true, value: 0f);
        RunLayout(uiRoot, 240, 120);

        var indicator = FindNamedVisualChild<FrameworkElement>(progressBar, "PART_Indicator");
        var glow = FindNamedVisualChild<FrameworkElement>(progressBar, "PART_GlowRect");
        Assert.NotNull(indicator);
        Assert.NotNull(glow);

        var firstLeft = indicator!.Margin.Left;
        var firstGlowLeft = glow!.Margin.Left;

        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(416), TimeSpan.FromMilliseconds(400)),
            new Viewport(0, 0, 240, 120));

        Assert.NotEqual(firstLeft, indicator.Margin.Left);
        Assert.NotEqual(firstGlowLeft, glow.Margin.Left);
        Assert.Equal(0.8f, glow.Opacity, 3);

        progressBar.IsIndeterminate = false;
        Assert.Equal(0f, glow.Opacity, 3);
    }

    [Fact]
    public void ValidationErrors_UpdateTemplateValidationState()
    {
        var (uiRoot, progressBar) = BuildTemplatedProgressBar();
        RunLayout(uiRoot, 240, 120);

        var rootChrome = FindNamedVisualChild<Border>(progressBar, "RootChrome");
        Assert.NotNull(rootChrome);
        AssertThickness(rootChrome!, 0f);

        progressBar.IsFocused = true;
        Validation.SetErrors(progressBar, this, [new ValidationError(null, this, "bad")]);

        AssertThickness(rootChrome, 1f);

        Validation.ClearErrors(progressBar, this);
        AssertThickness(rootChrome, 0f);
    }

    [Fact]
    public void IncompleteTemplate_DoesNotThrowAndUsesFallbackRenderingPath()
    {
        var host = new Canvas { Width = 200f, Height = 80f };
        var progressBar = new ProgressBar
        {
            Width = 160f,
            Height = 18f,
            Value = 40f,
            Template = new ControlTemplate(static _ => new Border())
            {
                TargetType = typeof(ProgressBar)
            }
        };
        host.AddChild(progressBar);

        var uiRoot = new UiRoot(host);
        var exception = Record.Exception(() => RunLayout(uiRoot, 200, 80));

        Assert.Null(exception);
        Assert.True(progressBar.ActualWidth > 0f);
        Assert.True(progressBar.ActualHeight > 0f);
    }

    [Fact]
    public void LoadFromXaml_HiddenBorderThicknessProperty_UsesProgressBarProperty()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <ProgressBar x:Name="Probe"
               Minimum="0"
               Maximum="100"
               Value="40"
               BorderThickness="1" />
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var progressBar = Assert.IsType<ProgressBar>(root.FindName("Probe"));

        Assert.Equal(1f, progressBar.BorderThickness);
        Assert.Equal(40f, progressBar.Value);
    }

    private static (UiRoot UiRoot, ProgressBar ProgressBar) BuildTemplatedProgressBar(
        Orientation orientation = Orientation.Horizontal,
        float width = 200f,
        float height = 20f,
        float value = 25f,
        bool isIndeterminate = false)
    {
        var host = new Canvas
        {
            Width = 240f,
            Height = 260f
        };

        var progressBar = new ProgressBar
        {
            Width = width,
            Height = height,
            Minimum = 0f,
            Maximum = 100f,
            Value = value,
            Orientation = orientation,
            IsIndeterminate = isIndeterminate,
            Template = CreateProgressBarTemplate()
        };

        host.AddChild(progressBar);
        Canvas.SetLeft(progressBar, 16f);
        Canvas.SetTop(progressBar, 16f);

        var uiRoot = new UiRoot(host);
        return (uiRoot, progressBar);
    }

    private static ControlTemplate CreateProgressBarTemplate()
    {
        return new ControlTemplate(static _ =>
        {
            var root = new Border
            {
                Name = "RootChrome",
                Background = new Color(0x33, 0x33, 0x33),
                BorderBrush = new Color(0x3F, 0x3F, 0x3F)
            };

            var track = new Grid
            {
                Name = "PART_Track"
            };
            var indicator = new Border
            {
                Name = "PART_Indicator",
                Background = new Color(0xFF, 0x8C, 0x00)
            };
            var glow = new Border
            {
                Name = "PART_GlowRect",
                Background = new Color(0xF0, 0xF0, 0xF0)
            };

            track.AddChild(indicator);
            track.AddChild(glow);
            root.Child = track;

            var commonStates = new VisualStateGroup("CommonStates");
            var determinate = new VisualState("Determinate");
            determinate.Setters.Add(new Setter("PART_GlowRect", UIElement.OpacityProperty, 0f));
            commonStates.States.Add(determinate);
            var indeterminate = new VisualState("Indeterminate");
            indeterminate.Setters.Add(new Setter("PART_GlowRect", UIElement.OpacityProperty, 0.8f));
            commonStates.States.Add(indeterminate);

            var validationStates = new VisualStateGroup("ValidationStates");
            validationStates.States.Add(new VisualState("Valid"));
            var invalidFocused = new VisualState("InvalidFocused");
            invalidFocused.Setters.Add(new Setter("RootChrome", Border.BorderThicknessProperty, new Thickness(1f)));
            validationStates.States.Add(invalidFocused);
            var invalidUnfocused = new VisualState("InvalidUnfocused");
            invalidUnfocused.Setters.Add(new Setter("RootChrome", Border.BorderThicknessProperty, new Thickness(1f)));
            validationStates.States.Add(invalidUnfocused);

            VisualStateManager.SetVisualStateGroups(root, [commonStates, validationStates]);
            return root;
        })
        {
            TargetType = typeof(ProgressBar)
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }

    private static TElement? FindNamedVisualChild<TElement>(UIElement root, string name)
        where TElement : FrameworkElement
    {
        if (root is TElement typed && typed.Name == name)
        {
            return typed;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindNamedVisualChild<TElement>(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void AssertThickness(Border border, float expected)
    {
        Assert.Equal(expected, border.BorderThickness.Left, 3);
        Assert.Equal(expected, border.BorderThickness.Top, 3);
        Assert.Equal(expected, border.BorderThickness.Right, 3);
        Assert.Equal(expected, border.BorderThickness.Bottom, 3);
    }
}