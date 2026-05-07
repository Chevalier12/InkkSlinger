using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class ScrollViewer
{
    private void ArrangeContentForCurrentOffsets()
    {
        _contentPresenter.ArrangeForCurrentOffsets(_contentViewportRect);
    }

    private void ArrangeContentForCurrentOffsets(LayoutRect previousViewportRect)
    {
        _contentPresenter.ArrangeForCurrentOffsets(previousViewportRect);
    }

    private bool CanReuseExistingContentArrange(FrameworkElement content, LayoutRect nextArrangeRect)
    {
        return _contentPresenter.CanReuseExistingArrange(content, nextArrangeRect);
    }

    private float ResolveContentArrangeWidth(FrameworkElement content, LayoutRect previousViewportRect)
    {
        return _contentPresenter.ResolveArrangeWidth(content, previousViewportRect);
    }

    private float ResolveContentArrangeHeight(FrameworkElement content, LayoutRect previousViewportRect)
    {
        return _contentPresenter.ResolveArrangeHeight(content, previousViewportRect);
    }

    private Vector2 CreateContentMeasureConstraint(float viewportWidth, float viewportHeight)
    {
        return _contentPresenter.CreateMeasureConstraint(viewportWidth, viewportHeight);
    }

    private bool UsesTransformBasedContentScrolling()
    {
        return _contentPresenter.UsesTransformBasedScrolling();
    }

    private IScrollInfo? LogicalScrollInfo => _contentPresenter.GetLogicalScrollInfo();
}
