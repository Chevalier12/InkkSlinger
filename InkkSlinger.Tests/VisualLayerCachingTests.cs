using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class VisualLayerCachingTests
{
    [Fact]
    public void HitTest_UsesAncestorTransformForChildGeometry()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 300f, 300f));

        var transformedParent = new TestTransformPanel();
        transformedParent.SetLayoutSlot(new LayoutRect(0f, 0f, 120f, 120f));
        transformedParent.ConfigureTransform(new Vector2(-40f, 0f));

        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(80f, 10f, 20f, 20f));
        transformedParent.AddChild(child);
        root.AddChild(transformedParent);

        var hit = VisualTreeHelper.HitTest(root, new Vector2(50f, 20f));
        Assert.Same(child, hit);
    }

    [Fact]
    public void HitTest_RejectsPointOutsideAncestorClip_WhenTransformed()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 300f, 300f));

        var transformedParent = new TestTransformPanel();
        transformedParent.SetLayoutSlot(new LayoutRect(0f, 0f, 120f, 120f));
        transformedParent.ConfigureTransform(new Vector2(-40f, 0f));
        transformedParent.ConfigureClip(new LayoutRect(0f, 0f, 30f, 120f), isEnabled: true);

        var child = new Border();
        child.SetLayoutSlot(new LayoutRect(80f, 10f, 20f, 20f));
        transformedParent.AddChild(child);
        root.AddChild(transformedParent);

        var hit = VisualTreeHelper.HitTest(root, new Vector2(50f, 20f));
        Assert.NotSame(child, hit);
    }

    private sealed class TestTransformPanel : Panel
    {
        private bool _hasClip;
        private LayoutRect _clipRect;
        private bool _hasTransform;
        private Matrix _transform = Matrix.Identity;
        private Matrix _inverseTransform = Matrix.Identity;

        public void ConfigureClip(LayoutRect clipRect, bool isEnabled)
        {
            _clipRect = clipRect;
            _hasClip = isEnabled;
            InvalidateVisual();
        }

        public void ConfigureTransform(Vector2 translation)
        {
            _hasTransform = translation != Vector2.Zero;
            _transform = _hasTransform
                ? Matrix.CreateTranslation(translation.X, translation.Y, 0f)
                : Matrix.Identity;
            _inverseTransform = _hasTransform
                ? Matrix.CreateTranslation(-translation.X, -translation.Y, 0f)
                : Matrix.Identity;
            InvalidateVisual();
        }

        protected override bool TryGetClipRect(out LayoutRect clipRect)
        {
            clipRect = _clipRect;
            return _hasClip;
        }

        protected override bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)
        {
            transform = _transform;
            inverseTransform = _inverseTransform;
            return _hasTransform;
        }
    }
}
