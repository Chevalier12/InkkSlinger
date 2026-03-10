using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class XamlLoaderOptimizationTests
{
    [Fact]
    public void LoadFromString_Repeatedly_WithAttachedProperties_PreservesAttachedValues()
    {
        const string xaml = """
<Canvas xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Button x:Name="Probe" Text="Run" Canvas.Left="20" Canvas.Top="30" />
</Canvas>
""";

        for (var i = 0; i < 20; i++)
        {
            var root = Assert.IsType<Canvas>(XamlLoader.LoadFromString(xaml));
            var probe = Assert.IsType<Button>(root.FindName("Probe"));

            Assert.Equal(20f, Canvas.GetLeft(probe));
            Assert.Equal(30f, Canvas.GetTop(probe));
        }
    }

    [Fact]
    public void LoadFromString_Repeatedly_WithForwardXReferenceBinding_ResolvesDeferredReference()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid>
    <ContentControl x:Name="Probe">
      <ContentControl.Content>
        <PriorityBinding>
          <Binding Path="Text" Source="{x:Reference Preferred}" />
          <Binding Path="Text" ElementName="Fallback" />
        </PriorityBinding>
      </ContentControl.Content>
    </ContentControl>
    <TextBlock x:Name="Fallback" Text="fallback" />
    <TextBlock x:Name="Preferred" Text="preferred" />
  </Grid>
</UserControl>
""";

        for (var i = 0; i < 20; i++)
        {
            var root = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
            var probe = Assert.IsType<ContentControl>(root.FindName("Probe"));
            Assert.Equal("preferred", probe.Content);
        }
    }

    [Fact]
    public void LoadIntoFromString_Repeatedly_AssignsNamedMembers_AndHooksEvents()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Button x:Name="Probe" Text="Run" Click="OnProbeClick" />
</UserControl>
""";

        for (var i = 0; i < 10; i++)
        {
            var host = new LoaderCodeBehindHost();
            XamlLoader.LoadIntoFromString(host, xaml, host);

            Assert.NotNull(host.Probe);
            InvokeButtonClick(host.Probe!);

            Assert.Equal(1, host.ClickCount);
        }
    }

    [Fact]
    public void PushDiagnosticSink_RepeatedFailures_EmitStableDiagnosticMetadata()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Button UnknownOption="nope" />
</UserControl>
""";

        for (var i = 0; i < 5; i++)
        {
            var diagnostics = new List<XamlDiagnostic>();
            using var sink = XamlLoader.PushDiagnosticSink(diagnostics.Add);

            _ = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xaml));

            var diagnostic = Assert.Single(
                diagnostics,
                entry => entry.Line.HasValue && entry.Position.HasValue);
            Assert.Equal(XamlDiagnosticCode.UnknownProperty, diagnostic.Code);
            Assert.Equal(nameof(Button), diagnostic.ElementName);
            Assert.Equal("UnknownOption", diagnostic.PropertyName);
            Assert.True(diagnostic.Line.HasValue);
            Assert.True(diagnostic.Position.HasValue);
        }
    }

    private static void InvokeButtonClick(Button button)
    {
        var onClick = typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(onClick);

        try
        {
            onClick.Invoke(button, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private sealed class LoaderCodeBehindHost : UserControl
    {
        public Button? Probe { get; private set; }

        public int ClickCount { get; private set; }

        private void OnProbeClick(object? sender, RoutedSimpleEventArgs args)
        {
            ClickCount++;
        }
    }
}
