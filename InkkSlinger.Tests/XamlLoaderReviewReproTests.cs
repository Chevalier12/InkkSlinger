using System;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class XamlLoaderReviewReproTests
{
    [Fact]
    public void LoadIntoFromString_CustomRootTag_ResolvesFromTargetAssembly_WhenCodeBehindIsNull()
    {
        const string xaml = """
<ReviewCustomRootHost xmlns="urn:inkkslinger-ui"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <TextBlock x:Name="Probe" Text="resolved" />
  </Grid>
</ReviewCustomRootHost>
""";

        var host = new ReviewCustomRootHost();

        XamlLoader.LoadIntoFromString(host, xaml);

        var root = Assert.IsType<Grid>(host.Content);
        var probe = Assert.IsType<TextBlock>(host.FindName("Probe"));

        Assert.Same(probe, root.Children[0]);
        Assert.Equal("resolved", probe.Text);
    }

    [Fact]
    public void LoadFromString_InvalidOwnerQualifiedAttachedProperty_Throws()
    {
        const string xaml = """
<Button xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Grid.IsEnabled="False"
        Content="Run" />
""";

        _ = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xaml));
    }
}

public sealed class ReviewCustomRootHost : UserControl
{
}