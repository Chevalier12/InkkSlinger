using System;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class XamlMarkupExtensionTests
{
    [Fact]
    public void XStatic_OnCommandProperty_ResolvesStaticField()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <Button x:Name="Probe" Command="{x:Static EditingCommands.Copy}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = Assert.IsType<Button>(root.FindName("Probe"));

        Assert.Same(EditingCommands.Copy, probe.Command);
    }

    [Fact]
    public void XStatic_OnEnumTypedProperty_ResolvesEnumMemberAndAssigns()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <StackPanel x:Name="Probe" Orientation="{x:Static Orientation.Horizontal}" />
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = Assert.IsType<StackPanel>(root.FindName("Probe"));

        Assert.Equal(Orientation.Horizontal, probe.Orientation);
    }

    [Fact]
    public void XStatic_InBindingSource_ResolvesAndBinds()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl x:Name="Probe" Content="{Binding Path=Length, Source={x:Static x:String.Empty}}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = Assert.IsType<ContentControl>(root.FindName("Probe"));

        Assert.Equal(0, Assert.IsType<int>(probe.Content));
    }

    [Fact]
    public void XStatic_WithMemberNamedSyntax_Resolves()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <Button x:Name="Probe" Command="{x:Static Member=EditingCommands.Paste}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = Assert.IsType<Button>(root.FindName("Probe"));

        Assert.Same(EditingCommands.Paste, probe.Command);
    }

    [Fact]
    public void XStatic_UnknownTypeOrMember_ThrowsHelpfulException()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <Button Command="{x:Static EditingCommands.NotARealCommand}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));

        Assert.Contains("x:Static member", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NotARealCommand", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Line", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsupportedMarkupExtension_FailsFastWithDiagnostic()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <TextBlock Text="{x:NoSuchExtension}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));

        Assert.Contains("Markup extension 'x:NoSuchExtension' is not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void XNull_OnReferenceTypeProperty_AssignsNull()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl x:Name="Probe" Content="{x:Null}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var probe = Assert.IsType<ContentControl>(root.FindName("Probe"));
        Assert.Null(probe.Content);
    }

    [Fact]
    public void XNull_OnNonNullableValueType_ThrowsHelpfulException()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <Button Background="{x:Null}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("x:Null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void XType_WithNamedTypeNameSyntax_Resolves()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Name="ProbeStyle" x:Key="ProbeStyle" TargetType="{x:Type TypeName=Button}" />
  </UserControl.Resources>
  <Grid />
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var style = Assert.IsType<Style>(root.Resources["ProbeStyle"]);
        Assert.Equal(typeof(Button), style.TargetType);
    }

    [Fact]
    public void XReference_BackwardReference_Resolves()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <TextBlock x:Name="Source" Text="from-source" />
    <ContentControl x:Name="Target" Content="{x:Reference Source}" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var source = Assert.IsType<TextBlock>(root.FindName("Source"));
        var target = Assert.IsType<ContentControl>(root.FindName("Target"));
        Assert.Same(source, target.Content);
    }

    [Fact]
    public void XReference_ForwardReference_ResolvesAfterFinalize()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl x:Name="Target" Content="{x:Reference Source}" />
    <TextBlock x:Name="Source" Text="forward-source" />
  </Grid>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var source = Assert.IsType<TextBlock>(root.FindName("Source"));
        var target = Assert.IsType<ContentControl>(root.FindName("Target"));
        Assert.Same(source, target.Content);
    }

    [Fact]
    public void XReference_UnresolvedAfterFinalize_ThrowsHelpfulException()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl Content="{x:Reference Missing}" />
  </Grid>
</UserControl>
""";

        var ex = Assert.ThrowsAny<Exception>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("x:Reference target 'Missing' could not be resolved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
