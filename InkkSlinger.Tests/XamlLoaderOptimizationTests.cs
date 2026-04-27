using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class XamlLoaderOptimizationTests
{
    [Fact]
    public void LoadFromString_Repeatedly_WithAttachedProperties_PreservesAttachedValues()
    {
        const string xaml = """
<Canvas xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Button x:Name="Probe" Content="Run" Canvas.Left="20" Canvas.Top="30" />
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
  <Button x:Name="Probe" Content="Run" Click="OnProbeClick" />
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
        public void LoadIntoFromString_DataTemplateGeneratedElements_CanBindEventsToDeclaringCodeBehind()
        {
          var host = new DataTemplateEventHost();
          host.ItemsHost!.ItemsSource = host.Rows;

          host.Rows.Add(new TemplateRow { Label = "Alpha" });

          var generated = Assert.Single(host.ItemsHost!.GetItemContainersForPresenter());
          var button = Assert.IsType<Button>(generated);

          InvokeButtonClick(button);

          Assert.Equal(1, host.RowClickCount);
          Assert.Same(button, host.LastRowClickSender);
        }

        [Fact]
        public void LoadIntoFromString_ControlTemplateGeneratedElements_CanBindEventsToDeclaringCodeBehind()
        {
          var host = new ControlTemplateEventHost();

          Assert.True(host.ApplyTemplate());

          var button = Assert.Single(FindDescendants<Button>(host, static candidate => string.Equals(candidate.Content as string, "TemplateHook", StringComparison.Ordinal)));

          InvokeButtonClick(button);

          Assert.Equal(1, host.TemplateClickCount);
          Assert.Same(button, host.LastTemplateClickSender);
        }

        [Fact]
        public void LoadIntoFromString_ItemsPanelTemplateGeneratedElements_CanBindEventsToDeclaringCodeBehind()
        {
          var host = new ItemsPanelTemplateEventHost();
          var panel = Assert.IsType<StackPanel>(host.ItemsHost!.ItemsPanel!.Build(host.ItemsHost));
          var button = Assert.Single(FindDescendants<Button>(panel, static candidate => string.Equals(candidate.Content as string, "PanelHook", StringComparison.Ordinal)));

          InvokeButtonClick(button);

          Assert.Equal(1, host.ItemsPanelClickCount);
          Assert.Same(button, host.LastItemsPanelClickSender);
        }

        [Fact]
        public void LoadIntoFromString_DelaysEventHandlerAttachment_UntilConstructorCompletes()
        {
          var host = new ConstructorEventOrderHost();

          Assert.NotNull(host.Editor);
          Assert.False(host.EventObservedBeforeConstructorCompleted);
          Assert.Equal(0, host.TextChangedCount);

          host.Editor!.Text = "after";

          Assert.False(host.EventObservedBeforeConstructorCompleted);
          Assert.True(host.TextChangedCount > 0);
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

    [Fact]
    public void LoadFromString_Repeatedly_WithTemplateBinding_PreservesTemplateValues()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="CornflowerBlue"
             Padding="9,4,3,2">
  <UserControl.Template>
    <ControlTemplate TargetType="{x:Type UserControl}">
      <Border x:Name="Chrome"
              Background="{TemplateBinding Background}"
              Padding="{TemplateBinding Padding}">
        <ContentPresenter />
      </Border>
    </ControlTemplate>
  </UserControl.Template>
  <Border x:Name="Payload" Width="20" Height="10" />
</UserControl>
""";

        for (var i = 0; i < 20; i++)
        {
            var root = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
            Assert.True(root.ApplyTemplate());

            var chrome = Assert.IsType<Border>(Assert.Single(root.GetVisualChildren()));
            AssertBrushColor(new Microsoft.Xna.Framework.Color(100, 149, 237), chrome.Background);
            Assert.Equal(new Thickness(9f, 4f, 3f, 2f), chrome.Padding);
        }
    }

    [Fact]
    public void LoadFromString_Repeatedly_WithNamedColorThicknessAndTransform_PreservesConvertedValues()
    {
        const string xaml = """
<Border xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Name="Probe"
        Background="CornflowerBlue"
      Padding="21,3,4,9">
      <Border.RenderTransform>
    <TranslateTransform X="5" Y="6" />
      </Border.RenderTransform>
    </Border>
""";

        for (var i = 0; i < 20; i++)
        {
            var border = Assert.IsType<Border>(XamlLoader.LoadFromString(xaml));
            AssertBrushColor(new Microsoft.Xna.Framework.Color(100, 149, 237), border.Background);
            Assert.Equal(new Thickness(21f, 3f, 4f, 9f), border.Padding);

            var transform = Assert.IsType<TranslateTransform>(border.RenderTransform);
            Assert.Equal(5f, transform.X);
            Assert.Equal(6f, transform.Y);
        }
    }

    [Fact]
    public void LoadFromString_Repeatedly_WithTemplatePropertyElement_WrapperStillBuildsTemplate()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="{x:Type Button}">
            <Border x:Name="Chrome" Background="{TemplateBinding Background}">
              <Border.Effect>
                <DropShadowEffect Color="#FF8C00" ShadowDepth="3" BlurRadius="12" Opacity="0.5" />
              </Border.Effect>
              <ContentPresenter />
            </Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </UserControl.Resources>
  <Button x:Name="Probe" Style="{StaticResource ProbeStyle}" Background="CornflowerBlue" />
</UserControl>
""";

        for (var i = 0; i < 10; i++)
        {
            var root = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
            var probe = Assert.IsType<Button>(root.FindName("Probe"));

            Assert.True(probe.ApplyTemplate());
            var chrome = Assert.IsType<Border>(Assert.Single(probe.GetVisualChildren()));
            Assert.IsType<DropShadowEffect>(chrome.Effect);
        }
    }

    [Fact]
    public void LoadFromFile_Repeatedly_WithMergedResourceDictionary_PreservesRelativeSourceResolution()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var initialMisses = GetDocumentCacheMissCounts();
            var themePath = Path.Combine(tempRoot, "ButtonTheme.xaml");
            File.WriteAllText(
                themePath,
                """
<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style x:Key="ThemeButtonStyle" TargetType="{x:Type Button}">
    <Setter Property="Padding" Value="8,6,4,2" />
  </Style>
</ResourceDictionary>
""");

            var hostPath = Path.Combine(tempRoot, "Host.xaml");
            File.WriteAllText(
                hostPath,
                """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="ButtonTheme.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </UserControl.Resources>
  <Button x:Name="Probe" Style="{StaticResource ThemeButtonStyle}" />
</UserControl>
""");

            for (var i = 0; i < 10; i++)
            {
                var root = Assert.IsType<UserControl>(XamlLoader.LoadFromFile(hostPath));
                var probe = Assert.IsType<Button>(root.FindName("Probe"));
                Assert.Equal(new Thickness(8f, 6f, 4f, 2f), probe.Padding);
            }

            var finalMisses = GetDocumentCacheMissCounts();
            Assert.True(finalMisses.FileMisses - initialMisses.FileMisses <= 2);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void LoadFromString_Repeatedly_ReusesParsedDocumentCache()
    {
        const string xaml = """
<Border xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Background="CornflowerBlue"
        Padding="4" />
""";

        var before = GetDocumentCacheMissCounts();
        for (var i = 0; i < 5; i++)
        {
            _ = Assert.IsType<Border>(XamlLoader.LoadFromString(xaml));
        }

        var after = GetDocumentCacheMissCounts();
        Assert.True(after.StringMisses - before.StringMisses <= 1);
    }

    [Fact]
    public void LoadFromFile_WhenSourceChanges_InvalidatesCachedDocument()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var viewPath = Path.Combine(tempRoot, "View.xaml");
            var initialTimestamp = new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc);
            var updatedTimestamp = initialTimestamp.AddSeconds(2);
            File.WriteAllText(
                viewPath,
                """
<Border xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Padding="4" />
""");
            File.SetLastWriteTimeUtc(viewPath, initialTimestamp);

            var before = GetDocumentCacheMissCounts();
            _ = Assert.IsType<Border>(XamlLoader.LoadFromFile(viewPath));
            var middle = GetDocumentCacheMissCounts();

            File.WriteAllText(
                viewPath,
                """
<Border xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Padding="8" />
""");
            File.SetLastWriteTimeUtc(viewPath, updatedTimestamp);
            Assert.NotEqual(initialTimestamp, File.GetLastWriteTimeUtc(viewPath));

            var reloaded = Assert.IsType<Border>(XamlLoader.LoadFromFile(viewPath));
            var after = GetDocumentCacheMissCounts();

            Assert.Equal(new Thickness(8f), reloaded.Padding);
            Assert.True(middle.FileMisses - before.FileMisses >= 1);
            Assert.True(after.FileMisses - middle.FileMisses >= 1);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
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

    private sealed class DataTemplateEventHost : UserControl
    {
        private const string Xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <ItemsControl x:Name="ItemsHost">
    <ItemsControl.ItemTemplate>
      <DataTemplate>
        <Button Content="{Binding Path=Label}" Click="OnRowClick" />
      </DataTemplate>
    </ItemsControl.ItemTemplate>
  </ItemsControl>
</UserControl>
""";

        public DataTemplateEventHost()
        {
            XamlLoader.LoadIntoFromString(this, Xaml, this);
        }

        public ItemsControl? ItemsHost { get; private set; }

        public ObservableCollection<TemplateRow> Rows { get; } = [];

        public int RowClickCount { get; private set; }

        public object? LastRowClickSender { get; private set; }

        private void OnRowClick(object? sender, RoutedSimpleEventArgs args)
        {
            _ = args;
            RowClickCount++;
            LastRowClickSender = sender;
        }
    }

    private sealed class ControlTemplateEventHost : UserControl
    {
        private const string Xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Template>
    <ControlTemplate TargetType="{x:Type UserControl}">
      <StackPanel>
        <Button Content="TemplateHook" Click="OnTemplateHookClick" />
        <ContentPresenter />
      </StackPanel>
    </ControlTemplate>
  </UserControl.Template>
  <Border Width="8" Height="8" />
</UserControl>
""";

        public ControlTemplateEventHost()
        {
            XamlLoader.LoadIntoFromString(this, Xaml, this);
        }

        public int TemplateClickCount { get; private set; }

        public object? LastTemplateClickSender { get; private set; }

        private void OnTemplateHookClick(object? sender, RoutedSimpleEventArgs args)
        {
            _ = args;
            TemplateClickCount++;
            LastTemplateClickSender = sender;
        }
    }

    private sealed class ItemsPanelTemplateEventHost : UserControl
    {
        private const string Xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <ItemsControl x:Name="ItemsHost">
    <ItemsControl.ItemsPanel>
      <ItemsPanelTemplate>
        <StackPanel>
          <Button Content="PanelHook" Click="OnItemsPanelHookClick" />
        </StackPanel>
      </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
  </ItemsControl>
</UserControl>
""";

        public ItemsPanelTemplateEventHost()
        {
            XamlLoader.LoadIntoFromString(this, Xaml, this);
        }

        public ItemsControl? ItemsHost { get; private set; }

        public ObservableCollection<string> Rows { get; } = [];

        public int ItemsPanelClickCount { get; private set; }

        public object? LastItemsPanelClickSender { get; private set; }

        private void OnItemsPanelHookClick(object? sender, RoutedSimpleEventArgs args)
        {
            _ = args;
          ItemsPanelClickCount++;
          LastItemsPanelClickSender = sender;
        }
    }

    private sealed class TemplateRow
    {
        public string Label { get; init; } = string.Empty;
    }

      private sealed class ConstructorEventOrderHost : UserControl
      {
        private const string Xaml = """
    <UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
      <TextBox x:Name="Editor"
               Text="boot"
               TextChanged="OnEditorTextChanged" />
    </UserControl>
    """;

        private bool _constructorCompleted;

        public ConstructorEventOrderHost()
        {
          XamlLoader.LoadIntoFromString(this, Xaml, this);
          _constructorCompleted = true;
        }

        public TextBox? Editor { get; private set; }

        public bool EventObservedBeforeConstructorCompleted { get; private set; }

        public int TextChangedCount { get; private set; }

        private void OnEditorTextChanged(object? sender, RoutedSimpleEventArgs args)
        {
          _ = sender;
          _ = args;

          if (!_constructorCompleted)
          {
            EventObservedBeforeConstructorCompleted = true;
          }

          TextChangedCount++;
        }
      }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "InkkSlinger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for temp artifacts.
        }
    }

      private static List<TElement> FindDescendants<TElement>(UIElement root, Func<TElement, bool>? predicate = null)
        where TElement : UIElement
      {
        var results = new List<TElement>();
        CollectDescendants(root, results, predicate);
        return results;
      }

      private static void CollectDescendants<TElement>(UIElement root, List<TElement> results, Func<TElement, bool>? predicate)
        where TElement : UIElement
      {
        foreach (var child in root.GetVisualChildren())
        {
          if (child is TElement match && (predicate == null || predicate(match)))
          {
            results.Add(match);
          }

          CollectDescendants(child, results, predicate);
        }
      }

    private static (int StringMisses, int FileMisses) GetDocumentCacheMissCounts()
    {
        var method = typeof(XamlLoader).GetMethod(
            "GetDocumentCacheMissCounts",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method.Invoke(null, null);
        Assert.NotNull(result);

        var stringMissesField = result.GetType().GetField("Item1");
        var fileMissesField = result.GetType().GetField("Item2");
        Assert.NotNull(stringMissesField);
        Assert.NotNull(fileMissesField);

        return (
            (int)stringMissesField.GetValue(result)!,
            (int)fileMissesField.GetValue(result)!);
    }

    private static void AssertBrushColor(Microsoft.Xna.Framework.Color expected, Brush? brush)
    {
        var actualBrush = Assert.IsAssignableFrom<Brush>(brush);
        Assert.Equal(expected, actualBrush.ToColor());
    }

}
