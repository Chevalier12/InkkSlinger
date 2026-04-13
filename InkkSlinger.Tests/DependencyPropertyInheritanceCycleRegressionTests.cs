using System.Reflection;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DependencyPropertyInheritanceCycleRegressionTests
{
    [Fact]
    public void VisualParentCycle_IsRejectedBeforeInheritedFontLookupCanRecurse()
    {
        var viewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel()
        };

        var panel = Assert.IsType<StackPanel>(viewer.Content);
        var text = new TextBlock
        {
            Text = "Known regression font-size recursion repro",
            TextWrapping = TextWrapping.Wrap
        };

        panel.AddChild(text);
        viewer.Measure(new Vector2(180f, 120f));

        var exception = Assert.Throws<TargetInvocationException>(() => AttachVisualParent(viewer, panel));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("UIElement parent assignment would create a cycle.", exception.InnerException!.Message);
        Assert.Equal(12f, text.FontSize);
    }

    [Fact]
    public void LogicalParentCycle_IsRejected()
    {
        var parent = new StackPanel();
        var child = new Border();

        parent.AddChild(child);

        var exception = Assert.Throws<TargetInvocationException>(() => AttachLogicalParent(parent, child));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("UIElement parent assignment would create a cycle.", exception.InnerException!.Message);
    }

    [Fact]
    public void InheritedFontLookup_WithCorruptedVisualCycle_DoesNotRecurseForever()
    {
        var viewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel()
        };

        var panel = Assert.IsType<StackPanel>(viewer.Content);
        var text = new TextBlock
        {
            Text = "Known regression font-size recursion repro",
            TextWrapping = TextWrapping.Wrap
        };

        panel.AddChild(text);
        viewer.Measure(new Vector2(180f, 120f));

        ForceVisualParent(viewer, panel);
        try
        {
            var fontSize = text.FontSize;

            Assert.Equal(12f, fontSize);
        }
        finally
        {
            ForceVisualParent(viewer, null);
        }
    }

    [Fact]
    public void TryFindResource_WithCorruptedVisualCycle_DoesNotLoopForever()
    {
        var viewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel()
        };

        var panel = Assert.IsType<StackPanel>(viewer.Content);
        var text = new TextBlock
        {
            Text = "Known regression resource-lookup cycle repro",
            TextWrapping = TextWrapping.Wrap
        };

        panel.AddChild(text);
        viewer.Measure(new Vector2(180f, 120f));

        ForceVisualParent(viewer, panel);
        try
        {
            var found = text.TryFindResource("MissingResourceKey", out var resource);

            Assert.False(found);
            Assert.Null(resource);
        }
        finally
        {
            ForceVisualParent(viewer, null);
        }
    }

    [Fact]
    public void ResolveTypography_WithCorruptedLongVisualCycle_DoesNotRecurseForever()
    {
        var (border, text) = CreateTypographyCycleFixture();

        ForceVisualParent(border, text);
        try
        {
            var typography = UiTextRenderer.ResolveTypography(text, text.FontSize);

            Assert.False(string.IsNullOrWhiteSpace(typography.Family));
        }
        finally
        {
            ForceVisualParent(border, null);
        }
    }

    [Fact]
    public void InheritedTypographyLookup_UsesCachedEffectiveValue_OnRepeatedRead()
    {
        var (border, text) = CreateTypographyCycleFixture();

        var firstTypography = UiTextRenderer.ResolveTypography(text, text.FontSize);

        ForceVisualParent(border, text);
        try
        {
            var secondTypography = UiTextRenderer.ResolveTypography(text, text.FontSize);

            Assert.Equal(firstTypography, secondTypography);
        }
        finally
        {
            ForceVisualParent(border, null);
        }
    }

    [Fact]
    public void WrappedLayoutReuse_WithCorruptedLongVisualCycle_DoesNotRecurseForever()
    {
        var (border, text) = CreateTypographyCycleFixture();

        ForceVisualParent(border, text);
        try
        {
            var canReuseMethod = typeof(TextBlock).GetMethod(
                "CanReuseWrappedLayoutForWidthRange",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(canReuseMethod);

            var result = canReuseMethod!.Invoke(text, new object[] { 180f, 220f });

            Assert.IsType<bool>(result);
        }
        finally
        {
            ForceVisualParent(border, null);
        }
    }

    private static void AttachVisualParent(UIElement child, UIElement parent)
    {
        var visualParentMethod = typeof(UIElement).GetMethod("SetVisualParent", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(visualParentMethod);

        _ = visualParentMethod!.Invoke(child, new object?[] { parent });
    }


    private static void AttachLogicalParent(UIElement child, UIElement parent)
    {
        var logicalParentMethod = typeof(UIElement).GetMethod("SetLogicalParent", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(logicalParentMethod);

        _ = logicalParentMethod!.Invoke(child, new object?[] { parent });
    }

    private static void ForceVisualParent(UIElement child, UIElement? parent)
    {
        var backingField = typeof(UIElement).GetField("<VisualParent>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(backingField);

        backingField!.SetValue(child, parent);
    }

    private static (Border Border, TextBlock Text) CreateTypographyCycleFixture()
    {
        var border = new Border();
        var grid = new Grid();
        var viewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel()
        };

        var panel = Assert.IsType<StackPanel>(viewer.Content);
        var text = new TextBlock
        {
            Text = "Known regression typography cycle repro",
            TextWrapping = TextWrapping.Wrap
        };

        panel.AddChild(text);
        grid.AddChild(viewer);
        border.Child = grid;

        border.Measure(new Vector2(320f, 240f));
        text.Measure(new Vector2(180f, 120f));

        return (border, text);
    }
}