using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class UseLayoutRoundingTests
{
    [Fact]
    public void UseLayoutRounding_DefaultFalse_PreservesFractionalLayout()
    {
        var probe = new FractionalProbeElement
        {
            MeasureResult = new Vector2(10.4f, 6.6f),
            ArrangeResult = new Vector2(7.7f, 5.5f)
        };

        probe.Measure(new Vector2(50.5f, 40.5f));
        probe.Arrange(new LayoutRect(1.2f, 2.3f, 20.6f, 10.4f));

        Assert.Equal(10.4f, probe.DesiredSize.X, 3);
        Assert.Equal(6.6f, probe.DesiredSize.Y, 3);
        Assert.Equal(1.2f, probe.LayoutSlot.X, 3);
        Assert.Equal(2.3f, probe.LayoutSlot.Y, 3);
        Assert.Equal(7.7f, probe.LayoutSlot.Width, 3);
        Assert.Equal(5.5f, probe.LayoutSlot.Height, 3);
    }

    [Fact]
    public void UseLayoutRounding_True_RoundsArrangedSlotAndRenderSize()
    {
        var probe = new FractionalProbeElement
        {
            UseLayoutRounding = true,
            MeasureResult = new Vector2(10.4f, 6.6f),
            ArrangeResult = new Vector2(7.7f, 5.5f)
        };

        probe.Measure(new Vector2(50.5f, 40.5f));
        probe.Arrange(new LayoutRect(1.2f, 2.3f, 20.6f, 10.4f));

        Assert.Equal(10f, probe.DesiredSize.X, 3);
        Assert.Equal(7f, probe.DesiredSize.Y, 3);
        Assert.Equal(1f, probe.LayoutSlot.X, 3);
        Assert.Equal(2f, probe.LayoutSlot.Y, 3);
        Assert.Equal(8f, probe.LayoutSlot.Width, 3);
        Assert.Equal(6f, probe.LayoutSlot.Height, 3);
        Assert.Equal(8f, probe.RenderSize.X, 3);
        Assert.Equal(6f, probe.RenderSize.Y, 3);
    }

    [Fact]
    public void UseLayoutRounding_InheritsFromAncestor_WhenChildUnset()
    {
        var host = new Canvas
        {
            UseLayoutRounding = true
        };

        var child = new FractionalProbeElement
        {
            MeasureResult = new Vector2(10.4f, 6.6f),
            ArrangeResult = new Vector2(float.NaN, float.NaN)
        };

        Canvas.SetLeft(child, 1.2f);
        Canvas.SetTop(child, 2.2f);
        host.AddChild(child);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 160, 120, 16);

        Assert.True(child.UseLayoutRounding);
        Assert.Equal(DependencyPropertyValueSource.Inherited, child.GetValueSource(FrameworkElement.UseLayoutRoundingProperty));
        Assert.Equal(1f, child.LayoutSlot.X, 3);
        Assert.Equal(2f, child.LayoutSlot.Y, 3);
        Assert.Equal(10f, child.LayoutSlot.Width, 3);
        Assert.Equal(7f, child.LayoutSlot.Height, 3);
    }

    [Fact]
    public void UseLayoutRounding_LocalFalse_OverridesInheritedTrue()
    {
        var host = new Canvas
        {
            UseLayoutRounding = true
        };

        var child = new FractionalProbeElement
        {
            UseLayoutRounding = false,
            MeasureResult = new Vector2(10.4f, 6.6f),
            ArrangeResult = new Vector2(float.NaN, float.NaN)
        };

        Canvas.SetLeft(child, 1.2f);
        Canvas.SetTop(child, 2.2f);
        host.AddChild(child);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 160, 120, 16);

        Assert.False(child.UseLayoutRounding);
        Assert.Equal(DependencyPropertyValueSource.Local, child.GetValueSource(FrameworkElement.UseLayoutRoundingProperty));
        Assert.Equal(1.2f, child.LayoutSlot.X, 3);
        Assert.Equal(2.2f, child.LayoutSlot.Y, 3);
        Assert.Equal(10.4f, child.LayoutSlot.Width, 3);
        Assert.Equal(6.6f, child.LayoutSlot.Height, 3);
    }

    [Fact]
    public void UseLayoutRounding_ReparentBetweenHosts_UpdatesEffectiveValueAndLayout()
    {
        var root = new Panel();
        var hostA = new Canvas
        {
            UseLayoutRounding = true
        };
        var hostB = new Canvas
        {
            UseLayoutRounding = false
        };
        root.AddChild(hostA);
        root.AddChild(hostB);

        var child = new FractionalProbeElement
        {
            MeasureResult = new Vector2(10.4f, 6.6f),
            ArrangeResult = new Vector2(float.NaN, float.NaN)
        };
        Canvas.SetLeft(child, 1.2f);
        Canvas.SetTop(child, 2.2f);
        hostA.AddChild(child);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 220, 140, 16);

        Assert.True(child.UseLayoutRounding);
        Assert.Equal(1f, child.LayoutSlot.X, 3);

        Assert.True(hostA.RemoveChild(child));
        hostB.AddChild(child);
        RunLayout(uiRoot, 220, 140, 32);

        Assert.False(child.UseLayoutRounding);
        Assert.Equal(DependencyPropertyValueSource.Inherited, child.GetValueSource(FrameworkElement.UseLayoutRoundingProperty));
        Assert.Equal(1.2f, child.LayoutSlot.X, 3);
        Assert.Equal(2.2f, child.LayoutSlot.Y, 3);
    }

    [Fact]
    public void UseLayoutRounding_XamlAttribute_ParsesAndApplies()
    {
        const string xaml = """
                            <Border xmlns="urn:inkkslinger-ui"
                                    UseLayoutRounding="True" />
                            """;

        var border = Assert.IsType<Border>(XamlLoader.LoadFromString(xaml));
        Assert.True(border.UseLayoutRounding);
        Assert.Equal(DependencyPropertyValueSource.Local, border.GetValueSource(FrameworkElement.UseLayoutRoundingProperty));
    }

    [Fact]
    public void UseLayoutRounding_StyleSetter_AppliesAndRounds()
    {
        var style = new Style(typeof(FractionalProbeElement));
        style.Setters.Add(new Setter(FrameworkElement.UseLayoutRoundingProperty, true));

        var probe = new FractionalProbeElement
        {
            MeasureResult = new Vector2(10.4f, 6.6f),
            ArrangeResult = new Vector2(7.7f, 5.5f)
        };
        style.Apply(probe);

        probe.Measure(new Vector2(50.5f, 40.5f));
        probe.Arrange(new LayoutRect(1.2f, 2.3f, 20.6f, 10.4f));

        Assert.True(probe.UseLayoutRounding);
        Assert.Equal(DependencyPropertyValueSource.Style, probe.GetValueSource(FrameworkElement.UseLayoutRoundingProperty));
        Assert.Equal(1f, probe.LayoutSlot.X, 3);
        Assert.Equal(8f, probe.LayoutSlot.Width, 3);
    }

    [Fact]
    public void UseLayoutRounding_WithCenterAlignmentAndMargins_UsesEdgeConsistentRounding()
    {
        var probe = new FractionalProbeElement
        {
            UseLayoutRounding = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(1.3f, 2.2f, 2.7f, 3.1f),
            MeasureResult = new Vector2(31.4f, 13.6f),
            ArrangeResult = new Vector2(float.NaN, float.NaN)
        };

        probe.Measure(new Vector2(120.2f, 80.2f));
        probe.Arrange(new LayoutRect(0.2f, 0.4f, 101.2f, 41.2f));

        Assert.True(IsRounded(probe.LayoutSlot.X));
        Assert.True(IsRounded(probe.LayoutSlot.Y));
        Assert.True(IsRounded(probe.LayoutSlot.Width));
        Assert.True(IsRounded(probe.LayoutSlot.Height));

        var right = probe.LayoutSlot.X + probe.LayoutSlot.Width;
        var bottom = probe.LayoutSlot.Y + probe.LayoutSlot.Height;
        Assert.True(IsRounded(right));
        Assert.True(IsRounded(bottom));
        Assert.True(probe.LayoutSlot.Width >= 0f);
        Assert.True(probe.LayoutSlot.Height >= 0f);
    }

    [Fact]
    public void UseLayoutRounding_WithScrollViewerContentHost_DoesNotRegressViewportOrHitTest()
    {
        var host = new StackPanel
        {
            UseLayoutRounding = true
        };

        for (var i = 0; i < 16; i++)
        {
            host.AddChild(new Button
            {
                Content = $"Button {i}",
                Height = 40f,
                Margin = new Thickness(0f, 0f, 0f, 4f)
            });
        }

        var viewer = new ScrollViewer
        {
            Width = 220f,
            Height = 120f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            LineScrollAmount = 64f,
            Content = host
        };

        var root = new Panel
        {
            UseLayoutRounding = true
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 280, 220, 16);

        var pointer = new Vector2(viewer.LayoutSlot.X + 28f, viewer.LayoutSlot.Y + 26f);
        uiRoot.RunInputDeltaForTests(CreateInputDelta(pointer, pointerMoved: true));
        var beforeButton = FindAncestor<Button>(VisualTreeHelper.HitTest(root, pointer));
        Assert.NotNull(beforeButton);
        Assert.True(beforeButton!.IsMouseOver);

        uiRoot.RunInputDeltaForTests(CreateInputDelta(pointer, wheelDelta: -120));
        var afterButton = FindAncestor<Button>(VisualTreeHelper.HitTest(root, pointer));
        Assert.NotNull(afterButton);
        Assert.NotSame(beforeButton, afterButton);
        Assert.True(viewer.VerticalOffset > 0.01f);
        Assert.True(afterButton!.IsMouseOver);
    }

    private static InputDelta CreateInputDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        int wheelDelta = 0)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved,
            WheelDelta = wheelDelta,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static TElement? FindAncestor<TElement>(UIElement? start)
        where TElement : UIElement
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement typed)
            {
                return typed;
            }
        }

        return null;
    }

    private static bool IsRounded(float value)
    {
        return MathF.Abs(value - MathF.Round(value)) <= 0.0001f;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private sealed class FractionalProbeElement : FrameworkElement
    {
        public Vector2 MeasureResult { get; set; } = new(10.4f, 6.6f);

        public Vector2 ArrangeResult { get; set; } = new(7.7f, 5.5f);

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return MeasureResult;
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            if (float.IsNaN(ArrangeResult.X) || float.IsNaN(ArrangeResult.Y))
            {
                return finalSize;
            }

            return ArrangeResult;
        }
    }
}
