using System.Reflection;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class ContentPresenterSafetyTests
{
    [Fact]
    public void ContentPresenter_WhenOwnerContentIsPresenter_DoesNotCreateSelfCycle()
    {
        var host = new ContentControl();
        var presenter = new ContentPresenter();

        host.Content = presenter;

        Assert.Empty(presenter.GetVisualChildren());
    }

    [Fact]
    public void ContentPresenter_WhenOwnerIsUserControl_DoesNotThrowAmbiguousPropertyLookup()
    {
        var owner = new UserControl();
        var presenter = new ContentPresenter();

        owner.Content = presenter;

        Assert.Empty(presenter.GetVisualChildren());
    }

    [Fact]
    public void ContentPresenter_FallbackLabelForegroundRefresh_InvalidatesRenderWithoutMeasure()
    {
        var owner = new Button
        {
            Content = "Alpha"
        };
        var presenter = new ContentPresenter();

        AttachParent(presenter, owner);
        presenter.Measure(new Vector2(240f, 40f));
        presenter.Arrange(new LayoutRect(0f, 0f, 240f, 40f));

        Assert.False(presenter.NeedsMeasure);
        Assert.False(presenter.NeedsArrange);

        owner.Foreground = new Color(255, 140, 0);

        Assert.False(presenter.NeedsMeasure);
        Assert.False(presenter.NeedsArrange);
        Assert.True(presenter.NeedsRender);
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
