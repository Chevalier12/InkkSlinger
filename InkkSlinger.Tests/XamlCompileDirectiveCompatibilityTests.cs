using Xunit;

namespace InkkSlinger.Tests;

public sealed class XamlCompileDirectiveCompatibilityTests
{
    [Fact]
    public void LoadFromString_AllowsCompileTimeXDirectives()
    {
        const string xaml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="InkkSlinger.SampleView"
             x:ClassModifier="public"
             x:Subclass="InkkSlinger.SampleViewSubclass">
  <Grid>
    <Button x:Name="MyButton" x:FieldModifier="private" Content="OK" />
  </Grid>
</UserControl>
""";

        var root = XamlLoader.LoadFromString(xaml);
        Assert.IsType<UserControl>(root);
    }
}
