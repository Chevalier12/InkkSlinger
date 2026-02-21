using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public class AdornerClippingTests
{
    [Fact]
    public void Adorner_ClipsToScrollViewerContentViewport_InsteadOfScrollbarBand()
    {
        var root = new Panel();
        var decorator = new AdornerDecorator();
        root.AddChild(decorator);

        var canvas = new Canvas { Width = 800f, Height = 600f };
        var selected = new Border { Width = 220f, Height = 140f };
        Canvas.SetLeft(selected, 140f);
        Canvas.SetTop(selected, 520f);
        canvas.AddChild(selected);

        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = 1f,
            ScrollBarThickness = 12f,
            Content = canvas
        };
        decorator.Child = viewer;

        var adorner = new ProbeAdorner(selected);
        decorator.AdornerLayer.AddAdorner(adorner);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);

        Assert.True(viewer.TryGetContentViewportClipRect(out var expectedViewportClip));
        Assert.True(adorner.TryGetClipForTesting(out var adornerClip));
        Assert.True(AreClose(expectedViewportClip.X, adornerClip.X));
        Assert.True(AreClose(expectedViewportClip.Y, adornerClip.Y));
        Assert.True(AreClose(expectedViewportClip.Width, adornerClip.Width));
        Assert.True(AreClose(expectedViewportClip.Height, adornerClip.Height));
    }

    [Fact]
    public void Adorner_ClipAndBounds_RemainCorrect_AfterScrollOffsetChanges()
    {
        var root = new Panel();
        var decorator = new AdornerDecorator();
        root.AddChild(decorator);

        var canvas = new Canvas { Width = 800f, Height = 600f };
        var selected = new Border { Width = 220f, Height = 140f };
        Canvas.SetLeft(selected, 140f);
        Canvas.SetTop(selected, 520f);
        canvas.AddChild(selected);

        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = 1f,
            ScrollBarThickness = 12f,
            Content = canvas
        };
        decorator.Child = viewer;

        var adorner = new ProbeAdorner(selected);
        decorator.AdornerLayer.AddAdorner(adorner);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 200, 16);
        var initialRect = adorner.LastAdornerRectForTesting;

        viewer.ScrollToVerticalOffset(110f);
        RunLayout(uiRoot, 320, 200, 32);

        Assert.True(viewer.TryGetContentViewportClipRect(out var expectedViewportClip));
        Assert.True(adorner.TryGetClipForTesting(out var adornerClip));
        Assert.True(selected.TryGetRenderBoundsInRootSpace(out var selectedBounds));
        Assert.True(AreClose(expectedViewportClip.X, adornerClip.X));
        Assert.True(AreClose(expectedViewportClip.Y, adornerClip.Y));
        Assert.True(AreClose(expectedViewportClip.Width, adornerClip.Width));
        Assert.True(AreClose(expectedViewportClip.Height, adornerClip.Height));
        Assert.True(AreClose(selectedBounds.Y, adorner.LastAdornerRectForTesting.Y));
        Assert.True(adorner.LastAdornerRectForTesting.Y < initialRect.Y - 0.05f);
    }

    [Fact]
    public void PaintShell_SelectionAdorners_TrackHorizontalScrollOffset()
    {
        var view = new PaintShellView();
        var decorator = Assert.IsType<AdornerDecorator>(view.FindName("CanvasAdornerRoot"));
        var viewer = Assert.IsType<ScrollViewer>(view.FindName("DrawingScrollViewer"));
        var selected = Assert.IsType<Border>(view.FindName("SelectedShape"));

        var uiRoot = new UiRoot(view);
        RunLayout(uiRoot, 1280, 840, 16);

        var adorners = decorator.AdornerLayer.GetAdorners(selected).ToArray();
        Assert.NotEmpty(adorners);
        Assert.True(selected.TryGetRenderBoundsInRootSpace(out var initialSelectedBounds));

        var handleAdorner = adorners
            .OrderBy(adorner => MathF.Abs(adorner.LastAdornerRectForTesting.Width - initialSelectedBounds.Width))
            .ThenBy(adorner => MathF.Abs(adorner.LastAdornerRectForTesting.Height - initialSelectedBounds.Height))
            .First();
        var initialRect = handleAdorner.LastAdornerRectForTesting;

        viewer.ScrollToHorizontalOffset(260f);
        Assert.False(selected.NeedsArrange);

        RunLayout(uiRoot, 1280, 840, 32);

        Assert.True(selected.TryGetRenderBoundsInRootSpace(out var selectedBoundsAfterScroll));
        var updatedRect = handleAdorner.LastAdornerRectForTesting;

        Assert.True(updatedRect.X < initialRect.X - 0.05f);
        Assert.True(AreClose(selectedBoundsAfterScroll.X, updatedRect.X));
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

    private sealed class ProbeAdorner : Adorner
    {
        public ProbeAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
        }

        public bool TryGetClipForTesting(out LayoutRect clipRect)
        {
            return TryGetClipRect(out clipRect);
        }
    }
}
