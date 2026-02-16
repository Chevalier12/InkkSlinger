using System;
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
