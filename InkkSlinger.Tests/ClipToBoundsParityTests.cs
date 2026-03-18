using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ClipToBoundsParityTests
{
    [Fact]
    public void ClipToBounds_DefaultsToFalse()
    {
        var border = new Border();

        Assert.False(border.ClipToBounds);
        Assert.Equal(DependencyPropertyValueSource.Default, border.GetValueSource(UIElement.ClipToBoundsProperty));
        Assert.False(border.TryGetLocalClipSnapshot(out _));
    }

    [Fact]
    public void ClipToBounds_InheritsThroughFrameworkElementTree()
    {
        var parent = new Border
        {
            ClipToBounds = true,
            Child = new Border()
        };
        var child = Assert.IsType<Border>(parent.Child);

        Assert.True(parent.ClipToBounds);
        Assert.True(child.ClipToBounds);
        Assert.Equal(DependencyPropertyValueSource.Inherited, child.GetValueSource(UIElement.ClipToBoundsProperty));
    }

    [Fact]
    public void XamlLoader_ParsesClipToBounds()
    {
        const string xaml = """
<Border xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        ClipToBounds="True"
        Width="120"
        Height="80" />
""";

        var border = Assert.IsType<Border>(XamlLoader.LoadFromString(xaml));

        Assert.True(border.ClipToBounds);
    }

    [Fact]
    public void ClipToBounds_UsesLayoutSlotAsRectangularClip()
    {
        var border = new Border
        {
            Width = 100f,
            Height = 60f,
            ClipToBounds = true
        };

        var uiRoot = new UiRoot(border);
        border.Measure(new Vector2(100f, 60f));
        border.Arrange(new LayoutRect(10f, 20f, 100f, 60f));

        Assert.True(border.TryGetLocalClipSnapshot(out var clipRect));
        Assert.Equal(new LayoutRect(10f, 20f, 100f, 60f), clipRect);
        Assert.Same(border, uiRoot.VisualRoot);
    }

    [Fact]
    public void ClipToBounds_AncestorPreventsHitTestingOverflowingTransformedChild()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 100f));

        var clipHost = new Border
        {
            ClipToBounds = true
        };
        clipHost.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));

        var child = new Border
        {
            Background = Color.Orange,
            RenderTransform = new TranslateTransform { X = 80f, Y = 0f }
        };
        child.SetLayoutSlot(new LayoutRect(0f, 0f, 40f, 40f));
        clipHost.Child = child;
        root.AddChild(clipHost);

        var overflowPoint = new Vector2(110f, 20f);
        Assert.Same(root, VisualTreeHelper.HitTest(root, overflowPoint));
    }

    [Fact]
    public void ClipToBounds_FalseAllowsHitTestingOverflowingTransformedChild()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 200f, 100f));

        var clipHost = new Border
        {
            ClipToBounds = false
        };
        clipHost.SetLayoutSlot(new LayoutRect(0f, 0f, 100f, 100f));

        var child = new Border
        {
            Background = Color.Orange,
            RenderTransform = new TranslateTransform { X = 80f, Y = 0f }
        };
        child.SetLayoutSlot(new LayoutRect(0f, 0f, 40f, 40f));
        clipHost.Child = child;
        root.AddChild(clipHost);

        var overflowPoint = new Vector2(110f, 20f);
        Assert.Same(child, VisualTreeHelper.HitTest(root, overflowPoint));
    }

    [Fact]
    public void ClipToBounds_LocalRenderState_TransformsClipBeforeScissoring()
    {
        var graphicsDevice = (GraphicsDevice)RuntimeHelpers.GetUninitializedObject(typeof(GraphicsDevice));

        try
        {
            UiDrawing.ConfigureDrawingStateForTests(
                graphicsDevice,
                new[]
                {
                    new Rectangle(0, 0, 500, 500)
                },
                Array.Empty<Matrix>());

            var appliedClip = UiDrawing.PushLocalStateForTests(
                graphicsDevice,
                hasTransform: true,
                Matrix.CreateTranslation(30f, 15f, 0f),
                hasClip: true,
                new LayoutRect(10f, 20f, 100f, 60f));

            Assert.Equal(new Rectangle(40, 35, 100, 60), appliedClip);

            var appliedState = UiDrawing.GetDrawingStateInfoForTests(graphicsDevice);
            Assert.Equal(2, appliedState.ClipCount);
            Assert.Equal(1, appliedState.TransformCount);

            var restoredClip = UiDrawing.PopLocalStateForTests(
                graphicsDevice,
                hasTransform: true,
                hasClip: true);

            Assert.Equal(new Rectangle(0, 0, 500, 500), restoredClip);

            var restoredState = UiDrawing.GetDrawingStateInfoForTests(graphicsDevice);
            Assert.Equal(1, restoredState.ClipCount);
            Assert.Equal(0, restoredState.TransformCount);
        }
        finally
        {
            UiDrawing.ReleaseDeviceResourcesForTests(graphicsDevice);
        }
    }
}