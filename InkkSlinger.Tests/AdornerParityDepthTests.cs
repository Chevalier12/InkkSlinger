using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AdornerParityDepthTests
{
    [Fact]
    public void AdornerLayer_FollowsScrollTransformOffsets_WithoutLayoutPass()
    {
        var fixture = CreateScrollFixture(useTransformContentScrolling: true);
        var adorner = new ProbeAdorner(fixture.Selected);
        fixture.Decorator.AdornerLayer.AddAdorner(adorner);

        var uiRoot = new UiRoot(fixture.Root);
        RunLayout(uiRoot, 320, 220, 16);

        var initialRect = adorner.LastAdornerRectForTesting;
        fixture.Viewer.ScrollToVerticalOffset(80f);
        Assert.False(fixture.Selected.NeedsArrange);

        RunLayout(uiRoot, 320, 220, 32);

        Assert.True(fixture.Selected.TryGetRenderBoundsInRootSpace(out var targetBoundsAfterScroll));
        var updatedRect = adorner.LastAdornerRectForTesting;
        Assert.True(updatedRect.Y < initialRect.Y - 0.05f);
        Assert.True(AreClose(updatedRect.Y, targetBoundsAfterScroll.Y));
        Assert.False(fixture.Selected.NeedsArrange);
    }

    [Fact]
    public void AdornerLayer_FollowsNonTransformScrolling_WhenUseTransformContentScrollingFalse()
    {
        var fixture = CreateScrollFixture(useTransformContentScrolling: false);
        var adorner = new ProbeAdorner(fixture.Selected);
        fixture.Decorator.AdornerLayer.AddAdorner(adorner);

        var uiRoot = new UiRoot(fixture.Root);
        RunLayout(uiRoot, 320, 220, 16);

        var initialRect = adorner.LastAdornerRectForTesting;
        fixture.Viewer.ScrollToVerticalOffset(65f);
        RunLayout(uiRoot, 320, 220, 32);

        var updatedRect = adorner.LastAdornerRectForTesting;
        Assert.True(updatedRect.Y < initialRect.Y - 0.05f);
        Assert.True(AreClose(updatedRect.Y, fixture.Selected.LayoutSlot.Y));
    }

    [Fact]
    public void AdornerLayer_PrunesAdorner_WhenAdornedElementLeavesVisualTree()
    {
        var fixture = CreateScrollFixture(useTransformContentScrolling: true);
        var adorner = new ProbeAdorner(fixture.Selected);
        fixture.Decorator.AdornerLayer.AddAdorner(adorner);

        var uiRoot = new UiRoot(fixture.Root);
        RunLayout(uiRoot, 320, 220, 16);
        Assert.Single(fixture.Decorator.AdornerLayer.GetAdorners(fixture.Selected));

        fixture.Canvas.RemoveChild(fixture.Selected);
        RunLayout(uiRoot, 320, 220, 32);

        Assert.Empty(fixture.Decorator.AdornerLayer.GetAdorners(fixture.Selected));
        Assert.Null(adorner.Layer);
        Assert.DoesNotContain(
            fixture.Decorator.AdornerLayer.GetVisualChildren(),
            child => ReferenceEquals(child, adorner));
    }

    [Fact]
    public void AdornerLayer_ZOrder_RespectsExplicitZIndex_ThenInsertionOrder()
    {
        var fixture = CreateScrollFixture(useTransformContentScrolling: true);
        var first = new ProbeAdorner(fixture.Selected);
        var second = new ProbeAdorner(fixture.Selected);
        var top = new ProbeAdorner(fixture.Selected);
        Panel.SetZIndex(top, 10);

        fixture.Decorator.AdornerLayer.AddAdorner(first);
        fixture.Decorator.AdornerLayer.AddAdorner(second);
        fixture.Decorator.AdornerLayer.AddAdorner(top);

        var ordered = fixture.Decorator.AdornerLayer.GetVisualChildren().OfType<Adorner>().ToArray();
        Assert.Equal(3, ordered.Length);
        Assert.Same(first, ordered[0]);
        Assert.Same(second, ordered[1]);
        Assert.Same(top, ordered[2]);
    }

    [Fact]
    public void AdornerLayer_PassThroughHitTest_WhenNonInteractiveAdornerMisses()
    {
        var fixture = CreateScrollFixture(useTransformContentScrolling: true);
        var adorner = new ProbeAdorner(fixture.Selected);
        fixture.Decorator.AdornerLayer.AddAdorner(adorner);

        var uiRoot = new UiRoot(fixture.Root);
        RunLayout(uiRoot, 320, 220, 16);

        var bounds = adorner.LastAdornerRectForTesting;
        var probe = new Vector2(bounds.X + (bounds.Width * 0.5f), bounds.Y + (bounds.Height * 0.5f));
        Assert.False(fixture.Decorator.AdornerLayer.HitTest(probe));
    }

    [Fact]
    public void AdornerLayer_InteractiveAdorner_HitTestsWithinUpdatedBounds()
    {
        var fixture = CreateScrollFixture(useTransformContentScrolling: true);
        var adorner = new InteractiveCornerAdorner(fixture.Selected)
        {
            HandleSize = 12f
        };
        fixture.Decorator.AdornerLayer.AddAdorner(adorner);

        var uiRoot = new UiRoot(fixture.Root);
        RunLayout(uiRoot, 320, 220, 16);

        var initialCenter = adorner.GetHandleCenter();
        Assert.True(fixture.Decorator.AdornerLayer.HitTest(initialCenter));

        fixture.Viewer.ScrollToVerticalOffset(90f);
        RunLayout(uiRoot, 320, 220, 32);

        var updatedCenter = adorner.GetHandleCenter();
        Assert.True(fixture.Decorator.AdornerLayer.HitTest(updatedCenter));
        Assert.False(AreClose(initialCenter.Y, updatedCenter.Y));
    }

    private static ScrollFixture CreateScrollFixture(bool useTransformContentScrolling)
    {
        var root = new Panel();
        var decorator = new AdornerDecorator();
        root.AddChild(decorator);

        var canvas = new Canvas { Width = 900f, Height = 800f };
        var selected = new Border { Width = 220f, Height = 140f };
        Canvas.SetLeft(selected, 140f);
        Canvas.SetTop(selected, 180f);
        canvas.AddChild(selected);

        if (!useTransformContentScrolling)
        {
            ScrollViewer.SetUseTransformContentScrolling(canvas, false);
        }

        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = 1f,
            ScrollBarThickness = 12f,
            Content = canvas
        };
        decorator.Child = viewer;
        return new ScrollFixture(root, decorator, viewer, canvas, selected);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        const string inputPipelineVariable = "INKKSLINGER_ENABLE_INPUT_PIPELINE";
        var previous = Environment.GetEnvironmentVariable(inputPipelineVariable);
        Environment.SetEnvironmentVariable(inputPipelineVariable, "0");
        try
        {
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
                new Viewport(0, 0, width, height));
        }
        finally
        {
            Environment.SetEnvironmentVariable(inputPipelineVariable, previous);
        }
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.05f;
    }

    private sealed class ProbeAdorner : Adorner
    {
        public ProbeAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
        }
    }

    private sealed class InteractiveCornerAdorner : Adorner
    {
        public InteractiveCornerAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
            IsHitTestVisible = true;
        }

        public float HandleSize { get; set; } = 8f;

        public Vector2 GetHandleCenter()
        {
            var rect = LastAdornerRectForTesting;
            return new Vector2(rect.X, rect.Y);
        }

        public override bool HitTest(Vector2 point)
        {
            if (!IsVisible || !IsEnabled || !IsHitTestVisible)
            {
                return false;
            }

            if (!IsPointVisibleThroughClipChain(point))
            {
                return false;
            }

            var half = MathF.Max(2f, HandleSize * 0.5f);
            var rect = LayoutSlot;
            return point.X >= rect.X - half &&
                   point.X <= rect.X + half &&
                   point.Y >= rect.Y - half &&
                   point.Y <= rect.Y + half;
        }
    }

    private sealed record ScrollFixture(
        Panel Root,
        AdornerDecorator Decorator,
        ScrollViewer Viewer,
        Canvas Canvas,
        Border Selected);
}
