using Xunit;

namespace InkkSlinger.Tests;

public sealed class CustomViewXamlResolutionTests
{
    [Fact]
    public void LoadIntoFromString_NestedCustomUserControlTag_ResolvesFromParentAssembly()
    {
        const string xaml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <NestedCustomView x:Name="Nested" />
  </Grid>
</UserControl>
""";

        var host = new CustomViewHost();

        XamlLoader.LoadIntoFromString(host, xaml, host);

        var nested = Assert.IsType<NestedCustomView>(host.FindName("Nested"));
        var root = Assert.IsType<Grid>(host.Content);

        Assert.Same(nested, root.Children[0]);
        Assert.Equal("Nested child content", Assert.IsType<Label>(nested.Content).Content);
    }
}

public sealed class CustomViewHost : UserControl
{
}

public sealed class NestedCustomView : UserControl
{
    public NestedCustomView()
    {
        Content = new Label
        {
            Content = "Nested child content"
        };
    }
}