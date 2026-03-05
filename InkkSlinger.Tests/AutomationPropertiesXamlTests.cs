using Xunit;

namespace InkkSlinger.Tests;

public sealed class AutomationPropertiesXamlTests
{
    [Fact]
    public void AttachedAutomationProperties_ParseFromXamlAttributes()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <TextBlock x:Name="Label" Text="User" />
    <TextBox x:Name="Input"
             AutomationProperties.Name="Username"
             AutomationProperties.AutomationId="UserNameBox"
             AutomationProperties.HelpText="Enter your username"
             AutomationProperties.ItemType="Credential"
             AutomationProperties.ItemStatus="Required"
             AutomationProperties.IsRequiredForForm="True"
             AutomationProperties.LabeledBy="{x:Reference Label}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var label = Assert.IsType<TextBlock>(root.FindName("Label"));
        var input = Assert.IsType<TextBox>(root.FindName("Input"));

        Assert.Equal("Username", AutomationProperties.GetName(input));
        Assert.Equal("UserNameBox", AutomationProperties.GetAutomationId(input));
        Assert.Equal("Enter your username", AutomationProperties.GetHelpText(input));
        Assert.Equal("Credential", AutomationProperties.GetItemType(input));
        Assert.Equal("Required", AutomationProperties.GetItemStatus(input));
        Assert.True(AutomationProperties.GetIsRequiredForForm(input));
        Assert.Same(label, AutomationProperties.GetLabeledBy(input));
    }
}
