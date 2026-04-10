using Xunit;

namespace InkkSlinger.Tests;

public sealed class FrameworkElementTagTests
{
    [Fact]
    public void Tag_CanBeAssigned_InCode()
    {
        var button = new Button();
        var value = new object();

        button.Tag = value;

        Assert.Same(value, button.Tag);
        Assert.Same(value, button.GetValue(FrameworkElement.TagProperty));
    }

    [Fact]
    public void LoadFromXaml_AssignsTagAttribute_OnDerivedControl()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Button x:Name="Probe" Tag="PrimaryAction" Content="Run" />
</UserControl>
""";

        var root = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
        var button = Assert.IsType<Button>(root.FindName("Probe"));

        Assert.Equal("PrimaryAction", Assert.IsType<string>(button.Tag));
    }
}