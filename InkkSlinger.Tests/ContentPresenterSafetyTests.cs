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
}
