using System.Reflection;
using Xunit;

namespace InkkSlinger.Tests;

public class InvalidationFlagsTests
{
    [Fact]
    public void MetadataFlags_MapToInvalidationCategories()
    {
        var element = new Border();
        var uiRoot = new UiRoot(element);

        element.BorderThickness = new Thickness(2f);

        Assert.True(element.NeedsMeasure);
        Assert.True(element.NeedsArrange);
        Assert.True(element.NeedsRender);
        Assert.True(element.SubtreeDirty);
        Assert.Equal(1, element.MeasureInvalidationCount);
        Assert.Equal(1, element.ArrangeInvalidationCount);
        Assert.Equal(1, element.RenderInvalidationCount);
        Assert.Equal(1, uiRoot.MeasureInvalidationCount);
        Assert.Equal(1, uiRoot.ArrangeInvalidationCount);
        Assert.Equal(1, uiRoot.RenderInvalidationCount);
    }

    [Fact]
    public void MeasureInvalidation_BubblesToParent()
    {
        var parent = new Border();
        var child = new Border();
        _ = new UiRoot(parent);
        AttachParent(child, parent);

        child.InvalidateMeasure();

        Assert.True(child.NeedsMeasure);
        Assert.True(parent.NeedsMeasure);
        Assert.True(child.SubtreeDirty);
        Assert.True(parent.SubtreeDirty);
    }

    [Fact]
    public void DuplicateInvalidation_IsCoalesced()
    {
        var element = new Border();
        var uiRoot = new UiRoot(element);

        element.InvalidateVisual();
        element.InvalidateVisual();

        Assert.Equal(1, element.RenderInvalidationCount);
        Assert.Equal(1, uiRoot.RenderInvalidationCount);
    }

    [Fact]
    public void AffectsRenderProperty_InvalidatesRenderOnly()
    {
        var element = new Border();
        var uiRoot = new UiRoot(element);

        element.Opacity = 0.5f;

        Assert.False(element.NeedsMeasure);
        Assert.False(element.NeedsArrange);
        Assert.True(element.NeedsRender);
        Assert.Equal(0, uiRoot.MeasureInvalidationCount);
        Assert.Equal(0, uiRoot.ArrangeInvalidationCount);
        Assert.Equal(1, uiRoot.RenderInvalidationCount);
    }

    private static void AttachParent(UIElement child, UIElement parent)
    {
        var visualParentMethod = typeof(UIElement).GetMethod("SetVisualParent", BindingFlags.Instance | BindingFlags.NonPublic);
        var logicalParentMethod = typeof(UIElement).GetMethod("SetLogicalParent", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(visualParentMethod);
        Assert.NotNull(logicalParentMethod);

        _ = visualParentMethod!.Invoke(child, new object?[] { parent });
        _ = logicalParentMethod!.Invoke(child, new object?[] { parent });
    }
}
