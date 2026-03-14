using System;
using Xunit;

namespace InkkSlinger.Tests;

public class FrameNavigationTests
{
    [Fact]
    public void Navigate_FirstContent_SetsContent_AndNoHistory()
    {
        var frame = new Frame();
        var page = new Page { Title = "First" };

        var result = frame.Navigate(page);

        Assert.True(result);
        Assert.Same(page, frame.Content);
        Assert.False(frame.CanGoBack);
        Assert.False(frame.CanGoForward);
    }

    [Fact]
    public void Navigate_SecondContent_EnablesBack_AndClearsForward()
    {
        var frame = new Frame();
        frame.Navigate(new Page { Title = "First" });

        frame.Navigate(new Page { Title = "Second" });

        Assert.True(frame.CanGoBack);
        Assert.False(frame.CanGoForward);
    }

    [Fact]
    public void GoBack_RestoresPreviousContent_AndEnablesForward()
    {
        var frame = new Frame();
        var first = new Page { Title = "First" };
        var second = new Page { Title = "Second" };
        frame.Navigate(first);
        frame.Navigate(second);

        frame.GoBack();

        Assert.Same(first, frame.Content);
        Assert.False(frame.CanGoBack);
        Assert.True(frame.CanGoForward);
    }

    [Fact]
    public void GoForward_RestoresNextContent_AndUpdatesFlags()
    {
        var frame = new Frame();
        var first = new Page { Title = "First" };
        var second = new Page { Title = "Second" };
        frame.Navigate(first);
        frame.Navigate(second);
        frame.GoBack();

        frame.GoForward();

        Assert.Same(second, frame.Content);
        Assert.True(frame.CanGoBack);
        Assert.False(frame.CanGoForward);
    }

    [Fact]
    public void Navigate_AfterGoBack_ClearsForwardJournal()
    {
        var frame = new Frame();
        var first = new Page { Title = "First" };
        var second = new Page { Title = "Second" };
        var branch = new Page { Title = "Branch" };
        frame.Navigate(first);
        frame.Navigate(second);
        frame.GoBack();

        frame.Navigate(branch);

        Assert.Same(branch, frame.Content);
        Assert.True(frame.CanGoBack);
        Assert.False(frame.CanGoForward);
    }

    [Fact]
    public void GoBack_WhenUnavailable_ThrowsInvalidOperationException()
    {
        var frame = new Frame();

        Assert.Throws<InvalidOperationException>(() => frame.GoBack());
    }

    [Fact]
    public void GoForward_WhenUnavailable_ThrowsInvalidOperationException()
    {
        var frame = new Frame();

        Assert.Throws<InvalidOperationException>(() => frame.GoForward());
    }

    [Fact]
    public void Page_WhenHostedByFrame_ReceivesNavigationService_AndLosesItWhenDetached()
    {
        var frame = new Frame();
        var first = new Page { Title = "First" };
        var second = new Page { Title = "Second" };

        frame.Navigate(first);
        Assert.NotNull(first.NavigationService);
        Assert.Same(frame.NavigationService, first.NavigationService);

        frame.Navigate(second);

        Assert.Null(first.NavigationService);
        Assert.NotNull(second.NavigationService);
        Assert.Same(frame.NavigationService, second.NavigationService);
    }

    [Fact]
    public void ExternalContentSet_ClearsJournal()
    {
        var frame = new Frame();
        var first = new Page { Title = "First" };
        var second = new Page { Title = "Second" };
        frame.Navigate(first);
        frame.Navigate(second);
        Assert.Same(frame.NavigationService, second.NavigationService);
        Assert.True(frame.CanGoBack);

        frame.Content = new Label { Content = "Externally set content" };

        Assert.False(frame.CanGoBack);
        Assert.False(frame.CanGoForward);
        Assert.Null(second.NavigationService);
    }

    [Fact]
    public void Xaml_FrameWithPageChild_LoadsSuccessfully()
    {
        const string xaml = """
<Frame xmlns="urn:inkkslinger-ui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Page Title="Dashboard">
    <Label Content="Hello" />
  </Page>
</Frame>
""";

        var root = Assert.IsType<Frame>(XamlLoader.LoadFromString(xaml));
        var page = Assert.IsType<Page>(root.Content);

        Assert.Equal("Dashboard", page.Title);
        var label = Assert.IsType<Label>(page.Content);
        Assert.Equal("Hello", label.GetContentText());
        Assert.Same(root.NavigationService, page.NavigationService);
        Assert.False(root.CanGoBack);
        Assert.False(root.CanGoForward);
    }

    [Fact]
    public void Navigate_SamePageTwice_PushesBackEntry_AndKeepsNavigationServiceAttached()
    {
        var frame = new Frame();
        var page = new Page { Title = "Repeat" };
        frame.Navigate(page);

        frame.Navigate(page);

        Assert.Same(page, frame.Content);
        Assert.True(frame.CanGoBack);
        Assert.False(frame.CanGoForward);
        Assert.Same(frame.NavigationService, page.NavigationService);
    }

    [Fact]
    public void Navigate_SamePageTwice_GoBackThenGoForward_RemainsConsistent()
    {
        // When Navigate is called with the already-current instance, the DP equality guard
        // suppresses OnDependencyPropertyChanged, so UpdateAttachedPage is never called.
        // NavigationService stays attached because nothing cleared it, not due to a re-attach.
        var frame = new Frame();
        var page = new Page { Title = "Repeat" };
        frame.Navigate(page);
        frame.Navigate(page);

        frame.GoBack();
        Assert.Same(page, frame.Content);
        Assert.False(frame.CanGoBack);
        Assert.True(frame.CanGoForward);
        Assert.Same(frame.NavigationService, page.NavigationService);

        frame.GoForward();
        Assert.Same(page, frame.Content);
        Assert.True(frame.CanGoBack);
        Assert.False(frame.CanGoForward);
        Assert.Same(frame.NavigationService, page.NavigationService);
    }

    [Fact]
    public void GoBack_ToNonPageContent_DetachesPageNavigationService()
    {
        var frame = new Frame();
        var label = new Label { Content = "Non-page" };
        var page = new Page { Title = "Page" };
        frame.Navigate(label);
        frame.Navigate(page);
        Assert.Same(frame.NavigationService, page.NavigationService);

        frame.GoBack();

        Assert.Same(label, frame.Content);
        Assert.Null(page.NavigationService);
    }

    [Fact]
    public void DepthThreeJournal_GoBackThenGoForward_PreservesExpectedCounts()
    {
        var frame = new Frame();
        var a = new Page { Title = "A" };
        var b = new Page { Title = "B" };
        var c = new Page { Title = "C" };
        frame.Navigate(a);
        frame.Navigate(b);
        frame.Navigate(c);

        frame.GoBack();
        Assert.Same(b, frame.Content);
        Assert.True(frame.CanGoBack);
        Assert.True(frame.CanGoForward);

        frame.GoForward();
        Assert.Same(c, frame.Content);
        Assert.True(frame.CanGoBack);
        Assert.False(frame.CanGoForward);
    }

    [Fact]
    public void ExternalContentSet_WhenBackAndForwardExist_ClearsHistoryAndDetachesPage()
    {
        var frame = new Frame();
        var a = new Page { Title = "A" };
        var b = new Page { Title = "B" };
        var c = new Page { Title = "C" };
        frame.Navigate(a);
        frame.Navigate(b);
        frame.GoBack();
        Assert.Same(a, frame.Content);
        Assert.True(frame.CanGoForward);
        Assert.Same(frame.NavigationService, a.NavigationService);

        frame.Content = c;

        Assert.Same(c, frame.Content);
        Assert.False(frame.CanGoBack);
        Assert.False(frame.CanGoForward);
        Assert.Null(a.NavigationService);
        Assert.Null(b.NavigationService); // journaled pages must not hold a live NavigationService
        Assert.Same(frame.NavigationService, c.NavigationService);
    }
}
