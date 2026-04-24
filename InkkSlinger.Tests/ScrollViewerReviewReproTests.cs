using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ScrollViewerReviewReproTests
{
    [Fact]
    public void Arrange_WhenViewportGrows_ViewportChangedShouldNotExposeOutOfRangeOffset()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(80);
        var viewer = new ScrollViewer
        {
            Width = 220f,
            Height = 120f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 260, 160, 16);

        viewer.ScrollToVerticalOffset(100_000f);

        var observedStates = new List<(float Offset, float MaxOffset)>();
        viewer.ViewportChanged += (_, _) =>
            observedStates.Add((viewer.VerticalOffset, MathF.Max(0f, viewer.ExtentHeight - viewer.ViewportHeight)));

        viewer.Height = 300f;
        RunLayout(uiRoot, 260, 360, 32);

        Assert.NotEmpty(observedStates);
        Assert.All(
            observedStates,
            state => Assert.True(
                state.Offset <= state.MaxOffset + 0.01f,
                $"ViewportChanged observed an out-of-range offset. offset={state.Offset:0.###} max={state.MaxOffset:0.###}."));

        uiRoot.Shutdown();
    }

    [Fact]
    public void ScrollToVerticalOffset_WhenGivenNaN_ShouldRejectNonFiniteOffset()
    {
        var root = new Panel();
        var viewer = new ScrollViewer
        {
            Width = 220f,
            Height = 120f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = CreateTallStackPanel(60)
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 260, 160, 16);

        viewer.ScrollToVerticalOffset(float.NaN);

        Assert.False(float.IsNaN(viewer.VerticalOffset), "Expected ScrollViewer to reject NaN vertical offsets.");
        uiRoot.Shutdown();
    }

    [Fact]
    public void SetValue_OnVerticalOffsetProperty_ShouldMoveContentLikeRealScrolling()
    {
        var root = new Panel();
        var content = CreateTallStackPanel(60);
        ScrollViewer.SetUseTransformContentScrolling(content, false);
        var viewer = new ScrollViewer
        {
            Width = 220f,
            Height = 120f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 260, 160, 16);

        viewer.SetValue(ScrollViewer.VerticalOffsetProperty, 50f);
        RunLayout(uiRoot, 260, 160, 32);

        var expectedY = viewer.LayoutSlot.Y + MathF.Max(0f, viewer.BorderThickness) - viewer.VerticalOffset;
        Assert.True(
            AreClose(expectedY, content.LayoutSlot.Y),
            $"Expected direct VerticalOffsetProperty writes to scroll content. expectedY={expectedY:0.###} actualY={content.LayoutSlot.Y:0.###} offset={viewer.VerticalOffset:0.###}.");

        uiRoot.Shutdown();
    }

    [Fact]
    public void TransformCapableContent_WhenOptedOut_ShouldUseArrangeOffsetScrolling()
    {
        var root = new Panel();
        var content = CreateTransformCapableTallStackPanel(60);
        ScrollViewer.SetUseTransformContentScrolling(content, false);
        var viewer = new ScrollViewer
        {
            Width = 220f,
            Height = 120f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 260, 160, 16);

        viewer.ScrollToVerticalOffset(48f);
        RunLayout(uiRoot, 260, 160, 32);

        var expectedY = viewer.LayoutSlot.Y + MathF.Max(0f, viewer.BorderThickness) - viewer.VerticalOffset;
        Assert.True(
            AreClose(expectedY, content.LayoutSlot.Y),
            $"Expected transform-capable content with UseTransformContentScrolling=false to use arrange-offset scrolling. expectedY={expectedY:0.###} actualY={content.LayoutSlot.Y:0.###}.");

        uiRoot.Shutdown();
    }

    [Fact]
    public void TransformCapableContent_SingleScrollMutation_ShouldOnlyInvalidateContentAndScrollbarThumb()
    {
        var root = new Panel();
        var content = CreateTransformCapableTallStackPanel(60);
        var viewer = new ScrollViewer
        {
            Width = 220f,
            Height = 120f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 260, 160, 16);
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();
        var renderInvalidationsBefore = uiRoot.RenderInvalidationCount;

        viewer.ScrollToVerticalOffset(24f);

        var renderInvalidationDelta = uiRoot.RenderInvalidationCount - renderInvalidationsBefore;
        Assert.True(
            renderInvalidationDelta <= 2,
            $"Expected at most two render invalidation bookkeeping events for a single transform scroll mutation (content plus scrollbar thumb), but saw {renderInvalidationDelta}.");
        uiRoot.Shutdown();
    }

    [Fact]
    public void ScrollViewerClip_ShouldIncludeViewerChrome()
    {
        var root = new Panel();
        var viewer = new ProbeScrollViewer
        {
            Width = 220f,
            Height = 120f,
            BorderThickness = 5f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = CreateTallStackPanel(40)
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 260, 160, 16);

        Assert.True(viewer.TryGetClipRectForTesting(out var clipRect));
        Assert.True(
            clipRect.X <= viewer.LayoutSlot.X + 0.01f &&
            clipRect.Y <= viewer.LayoutSlot.Y + 0.01f &&
            clipRect.X + clipRect.Width >= viewer.LayoutSlot.X + viewer.LayoutSlot.Width - 0.01f &&
            clipRect.Y + clipRect.Height >= viewer.LayoutSlot.Y + viewer.LayoutSlot.Height - 0.01f,
            $"Expected ScrollViewer clip to include the full chrome. clip={clipRect} layout={viewer.LayoutSlot}.");

        uiRoot.Shutdown();
    }

    [Fact]
    public void SettingBaseControlChromeProperties_ShouldFlowIntoScrollViewerChrome()
    {
        var viewer = new ScrollViewer();

        viewer.SetValue(Control.BackgroundProperty, Color.OrangeRed);
        viewer.SetValue(Control.BorderBrushProperty, Color.LimeGreen);
        viewer.SetValue(Control.BorderThicknessProperty, new Thickness(7f));

        Assert.Equal(Color.OrangeRed, viewer.Background);
        Assert.Equal(Color.LimeGreen, viewer.BorderBrush);
        Assert.Equal(7f, viewer.BorderThickness);
    }

    [Fact]
    public void ScrollBarThatAppearsAfterLoad_ShouldBeLoadedWhenItEntersVisualTree()
    {
        var root = new Panel();
        var content = new StackPanel();
        content.AddChild(new Border { Height = 20f });

        var viewer = new ScrollViewer
        {
            Width = 220f,
            Height = 120f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        root.AddChild(viewer);

        var verticalBar = GetPrivateScrollBar(viewer, "_verticalBar");
        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 260, 160, 16);

        Assert.DoesNotContain(viewer.GetVisualChildren(), child => ReferenceEquals(child, verticalBar));

        for (var i = 0; i < 50; i++)
        {
            content.AddChild(new Border { Height = 20f, Margin = new Thickness(0f, 0f, 0f, 2f) });
        }

        RunLayout(uiRoot, 260, 160, 32);

        Assert.Contains(viewer.GetVisualChildren(), child => ReferenceEquals(child, verticalBar));
        Assert.True(verticalBar.IsLoaded, "Expected the internal scrollbar to be loaded when it becomes visible.");

        uiRoot.Shutdown();
    }

    private static StackPanel CreateTallStackPanel(int itemCount)
    {
        var panel = new StackPanel();
        for (var i = 0; i < itemCount; i++)
        {
            panel.AddChild(new Border { Height = 20f, Margin = new Thickness(0f, 0f, 0f, 2f) });
        }

        return panel;
    }

    private static TransformCapableStackPanel CreateTransformCapableTallStackPanel(int itemCount)
    {
        var panel = new TransformCapableStackPanel();
        for (var i = 0; i < itemCount; i++)
        {
            panel.AddChild(new Border { Height = 20f, Margin = new Thickness(0f, 0f, 0f, 2f) });
        }

        return panel;
    }

    private static ScrollBar GetPrivateScrollBar(ScrollViewer viewer, string fieldName)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ScrollBar>(field!.GetValue(viewer));
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.05f;
    }

    private sealed class ProbeScrollViewer : ScrollViewer
    {
        public bool TryGetClipRectForTesting(out LayoutRect clipRect)
        {
            return TryGetClipRect(out clipRect);
        }
    }

    private sealed class TransformCapableStackPanel : StackPanel, IScrollTransformContent
    {
    }
}