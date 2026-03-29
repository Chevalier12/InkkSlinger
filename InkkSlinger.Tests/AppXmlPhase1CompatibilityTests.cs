using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AppXmlPhase1CompatibilityTests
{
    [Fact]
    public void ColorResourceObjectElement_ParsesAndStoresColorValue()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Color x:Key="AccentColor">#FF336699</Color>
  </UserControl.Resources>
</UserControl>
""");

        Assert.True(root.Resources.TryGetValue("AccentColor", out var resource));
        var color = Assert.IsType<Color>(resource);
        Assert.Equal(new Color(0x33, 0x66, 0x99, 0xFF), color);
    }

    [Fact]
    public void SolidColorBrushStaticResource_CoercesToColorTypedDependencyProperty()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Color x:Key="AccentColor">#1255AA</Color>
    <SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}" />
  </UserControl.Resources>
  <Button x:Name="Probe" Background="{StaticResource AccentBrush}" />
</UserControl>
""");

        var button = Assert.IsType<Button>(root.FindName("Probe"));
        Assert.Equal(new Color(0x12, 0x55, 0xAA), button.Background);
    }

    [Fact]
    public void LinearGradientBrushStaticResource_CoercesToColorTypedDependencyProperty()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <LinearGradientBrush x:Key="AccentBrush" StartPoint="0,0" EndPoint="1,0">
      <GradientStop Color="#FFFF0000" Offset="0" />
      <GradientStop Color="#FF0000FF" Offset="1" />
    </LinearGradientBrush>
  </UserControl.Resources>
  <Button x:Name="Probe" Background="{StaticResource AccentBrush}" />
</UserControl>
""");

        var button = Assert.IsType<Button>(root.FindName("Probe"));
        Assert.Equal(new Color(127, 0, 127, 255), button.Background);
    }

    [Fact]
    public void LinearGradientBrushObjectElement_ParsesDirectGradientStopChildren()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Border x:Name="Probe">
    <Border.Background>
      <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
        <GradientStop Color="#FFFF0000" Offset="0" />
        <GradientStop Color="#FF0000FF" Offset="1" />
      </LinearGradientBrush>
    </Border.Background>
  </Border>
</UserControl>
""");

        var border = Assert.IsType<Border>(root.FindName("Probe"));
        var brush = Assert.IsType<LinearGradientBrush>(border.Background);
        Assert.Equal(new Vector2(0f, 0f), brush.StartPoint);
        Assert.Equal(new Vector2(1f, 0f), brush.EndPoint);
        Assert.Equal(2, brush.GradientStops.Count);
        Assert.Equal(new Color(255, 0, 0, 255), brush.GradientStops[0].Color);
        Assert.Equal(1f, brush.GradientStops[1].Offset);
    }

    [Fact]
    public void FontFamilyObjectElement_ParsesAndAppliesToFontFamilyProperty()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <FontFamily x:Key="BodyFont">Segoe UI Variable, Segoe UI, Calibri</FontFamily>
  </UserControl.Resources>
  <TextBlock x:Name="Probe" FontFamily="{StaticResource BodyFont}" />
</UserControl>
""");

        var textBlock = Assert.IsType<TextBlock>(root.FindName("Probe"));
        Assert.Equal(new FontFamily("Segoe UI Variable, Segoe UI, Calibri"), textBlock.FontFamily);
        Assert.Equal("Segoe UI Variable", textBlock.FontFamily.PrimaryFamilyName);
        Assert.Equal(3, textBlock.FontFamily.FamilyNames.Count);
    }

    [Fact]
    public void TextBlockStyleSetter_LineHeight_ParsesAndAppliesToTextBlock()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="BodyText" TargetType="TextBlock">
      <Setter Property="LineHeight" Value="22" />
    </Style>
  </UserControl.Resources>
  <TextBlock x:Name="Probe" Style="{StaticResource BodyText}" Text="Hello" />
</UserControl>
""");

        var textBlock = Assert.IsType<TextBlock>(root.FindName("Probe"));
        Assert.Equal(22f, textBlock.LineHeight);
    }

    [Fact]
    public void TextBlockStyleSetter_CharacterSpacing_ParsesAndAppliesToTextBlock()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="CaptionText" TargetType="TextBlock">
      <Setter Property="CharacterSpacing" Value="60" />
    </Style>
  </UserControl.Resources>
  <TextBlock x:Name="Probe" Style="{StaticResource CaptionText}" Text="Hello" />
</UserControl>
""");

        var textBlock = Assert.IsType<TextBlock>(root.FindName("Probe"));
        Assert.Equal(60, textBlock.CharacterSpacing);
    }

    [Fact]
    public void SetterValueObjectElement_ControlTemplate_ParsesAndBuilds()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="TemplateStyle" TargetType="{x:Type Button}">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="{x:Type Button}">
            <Border x:Name="RootBorder" Background="#223344" Padding="8">
              <ContentPresenter />
            </Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </UserControl.Resources>
  <Button x:Name="Probe" Style="{StaticResource TemplateStyle}" />
</UserControl>
""");

        var button = Assert.IsType<Button>(root.FindName("Probe"));
        Assert.True(button.ApplyTemplate());

        var templateRoot = Assert.Single(button.GetVisualChildren());
        var border = Assert.IsType<Border>(templateRoot);
        AssertBrushColor(new Color(0x22, 0x33, 0x44), border.Background);
        Assert.Equal(new Thickness(8f), border.Padding);
    }

    [Fact]
    public void TemplateBinding_ShorthandAndPropertyForm_ApplyAndTrackOwnerPropertyChanges()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="TemplateStyle" TargetType="{x:Type Button}">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="{x:Type Button}">
            <Border x:Name="RootBorder"
                    Background="{TemplateBinding Background}"
                    Padding="{TemplateBinding Property=Padding}">
              <ContentPresenter />
            </Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </UserControl.Resources>
  <Button x:Name="Probe"
          Style="{StaticResource TemplateStyle}"
          Background="#AA5500"
          Padding="10,4" />
</UserControl>
""");

        var button = Assert.IsType<Button>(root.FindName("Probe"));
        Assert.True(button.ApplyTemplate());

        var templateRoot = Assert.Single(button.GetVisualChildren());
        var border = Assert.IsType<Border>(templateRoot);

        AssertBrushColor(new Color(0xAA, 0x55, 0x00), border.Background);
        Assert.Equal(new Thickness(10f, 4f, 10f, 4f), border.Padding);

        button.Background = new Color(0x0F, 0x70, 0xCC);
        button.Padding = new Thickness(3f, 2f, 3f, 2f);

        AssertBrushColor(new Color(0x0F, 0x70, 0xCC), border.Background);
        Assert.Equal(new Thickness(3f, 2f, 3f, 2f), border.Padding);
    }

    [Fact]
    public void TemplateBinding_OnUnnamedNonRootElement_IsWiredViaGeneratedTargetName()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="TemplateStyle" TargetType="{x:Type Button}">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="{x:Type Button}">
            <Grid>
              <Border Background="{TemplateBinding Background}" />
            </Grid>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </UserControl.Resources>
  <Button x:Name="Probe" Style="{StaticResource TemplateStyle}" Background="#119944" />
</UserControl>
""");

        var button = Assert.IsType<Button>(root.FindName("Probe"));
        Assert.True(button.ApplyTemplate());

        var templateRoot = Assert.IsType<Grid>(Assert.Single(button.GetVisualChildren()));
        var nestedBorder = Assert.IsType<Border>(Assert.Single(templateRoot.GetVisualChildren()));
        AssertBrushColor(new Color(0x11, 0x99, 0x44), nestedBorder.Background);

        button.Background = new Color(0x77, 0x33, 0xBB);
        AssertBrushColor(new Color(0x77, 0x33, 0xBB), nestedBorder.Background);
    }

    [Fact]
    public void ButtonContent_WithControlTemplateContentPresenter_PresentsAndUpdatesContent()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="TemplateStyle" TargetType="{x:Type Button}">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="{x:Type Button}">
            <Border>
              <ContentPresenter />
            </Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </UserControl.Resources>
  <Button x:Name="Probe" Style="{StaticResource TemplateStyle}" Content="FromContent" />
</UserControl>
""");

        var button = Assert.IsType<Button>(root.FindName("Probe"));
        Assert.True(button.ApplyTemplate());
        button.Measure(new Vector2(320f, 120f));
        button.Arrange(new LayoutRect(0f, 0f, 320f, 120f));

        var border = Assert.IsType<Border>(Assert.Single(button.GetVisualChildren()));
        var presenter = Assert.IsType<ContentPresenter>(Assert.Single(border.GetVisualChildren()));
        var label = Assert.IsType<Label>(Assert.Single(presenter.GetVisualChildren()));
        Assert.Equal("FromContent", label.GetContentText());

        button.Content = "UpdatedContent";
        label = Assert.IsType<Label>(Assert.Single(presenter.GetVisualChildren()));
        Assert.Equal("UpdatedContent", label.GetContentText());
    }

    [Fact]
      public void ButtonUiElementContent_WithControlTemplateContentPresenter_PresentsProvidedElement()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="TemplateStyle" TargetType="{x:Type Button}">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="{x:Type Button}">
            <Border>
              <ContentPresenter />
            </Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </UserControl.Resources>
  <Button x:Name="Probe" Style="{StaticResource TemplateStyle}">
    <Label x:Name="InnerLabel" Content="ExplicitContent" />
  </Button>
</UserControl>
""");

        var button = Assert.IsType<Button>(root.FindName("Probe"));
        var innerLabel = Assert.IsType<Label>(root.FindName("InnerLabel"));
        Assert.True(button.ApplyTemplate());
        button.Measure(new Vector2(320f, 120f));
        button.Arrange(new LayoutRect(0f, 0f, 320f, 120f));

        var border = Assert.IsType<Border>(Assert.Single(button.GetVisualChildren()));
        var presenter = Assert.IsType<ContentPresenter>(Assert.Single(border.GetVisualChildren()));
        var label = Assert.IsType<Label>(Assert.Single(presenter.GetVisualChildren()));
        Assert.Same(innerLabel, label);
        Assert.Equal("ExplicitContent", label.GetContentText());
    }

    [Fact]
    public void ThicknessTwoValueForm_MapsToHorizontalVerticalSemantics()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Border x:Name="Probe" Padding="12,5" />
</UserControl>
""");

        var border = Assert.IsType<Border>(root.FindName("Probe"));
        Assert.Equal(new Thickness(12f, 5f, 12f, 5f), border.Padding);
    }

    [Fact]
    public void ThicknessResourceObjectElement_ParsesAndStoresThicknessValue()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Thickness x:Key="PanelPadding">12,5</Thickness>
  </UserControl.Resources>
  <Border x:Name="Probe" Padding="{StaticResource PanelPadding}" />
</UserControl>
""");

        Assert.True(root.Resources.TryGetValue("PanelPadding", out var resource));
        var thickness = Assert.IsType<Thickness>(resource);
        Assert.Equal(new Thickness(12f, 5f, 12f, 5f), thickness);

        var border = Assert.IsType<Border>(root.FindName("Probe"));
        Assert.Equal(thickness, border.Padding);
    }

    [Fact]
    public void PrimitiveResourceObjectElements_ParseAndStoreTypedValues()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:sys="clr-namespace:System;assembly=mscorlib">
  <UserControl.Resources>
    <sys:String x:Key="Caption">Status</sys:String>
    <sys:Boolean x:Key="EnabledFlag">true</sys:Boolean>
    <sys:Int32 x:Key="TrackAmount">60</sys:Int32>
    <sys:Single x:Key="ScaleValue">1.5</sys:Single>
    <sys:Double x:Key="DimOpacity">0.38</sys:Double>
  </UserControl.Resources>
</UserControl>
""");

        Assert.Equal("Status", Assert.IsType<string>(root.Resources["Caption"]));
        Assert.True(Assert.IsType<bool>(root.Resources["EnabledFlag"]));
        Assert.Equal(60, Assert.IsType<int>(root.Resources["TrackAmount"]));
        Assert.Equal(1.5f, Assert.IsType<float>(root.Resources["ScaleValue"]));
        Assert.Equal(0.38d, Assert.IsType<double>(root.Resources["DimOpacity"]));
    }

    [Fact]
    public void PrimitiveStaticResourceValues_CoerceToFrameworkPropertyTypes()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:sys="clr-namespace:System;assembly=mscorlib">
  <UserControl.Resources>
    <sys:Int32 x:Key="TrackAmount">60</sys:Int32>
    <sys:Double x:Key="DimOpacity">0.38</sys:Double>
  </UserControl.Resources>
  <Grid>
    <TextBlock x:Name="TextProbe" CharacterSpacing="{StaticResource TrackAmount}" Text="Hello" />
    <Border x:Name="OpacityProbe" Opacity="{StaticResource DimOpacity}" />
  </Grid>
</UserControl>
""");

        var textBlock = Assert.IsType<TextBlock>(root.FindName("TextProbe"));
        Assert.Equal(60, textBlock.CharacterSpacing);

        var border = Assert.IsType<Border>(root.FindName("OpacityProbe"));
        Assert.Equal(0.38f, border.Opacity, 3);
    }

    [Fact]
    public void LoadApplicationResourcesFromFile_RootAppXml_CompletesWithoutException()
    {
        var backup = CaptureApplicationResources();
        try
        {
            var appPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "App.xml"));
            Assert.True(File.Exists(appPath), $"Expected App.xml to exist at '{appPath}'.");

            XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);

            Assert.True(UiApplication.Current.Resources.TryGetValue(typeof(Button), out var buttonStyle));
            Assert.IsType<Style>(buttonStyle);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void UnknownProperty_StillThrowsToPreserveStrictParserBehavior()
    {
        var xaml =
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Button UnknownPhase1Property="True" />
</UserControl>
""";

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("UnknownPhase1Property", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownProperty_EmitsStructuredDiagnostic()
    {
        var xaml =
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Button UnknownPhase4Property="True" />
</UserControl>
""";

        var diagnostics = new List<XamlDiagnostic>();
        using var sink = XamlLoader.PushDiagnosticSink(diagnostics.Add);

        _ = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xaml));

        var unknownPropertyDiagnostic = diagnostics.Last(
            d => d.Code == XamlDiagnosticCode.UnknownProperty &&
                 string.Equals(d.PropertyName, "UnknownPhase4Property", StringComparison.Ordinal) &&
                 d.Line.HasValue &&
                 d.Position.HasValue);

        Assert.Equal("Button", unknownPropertyDiagnostic.ElementName);
        Assert.True(unknownPropertyDiagnostic.Line.HasValue);
        Assert.True(unknownPropertyDiagnostic.Position.HasValue);
    }

    [Fact]
    public void GridViewRowPresenter_InControlTemplate_NoLongerFailsTypeResolution()
    {
        var root = (UserControl)XamlLoader.LoadFromString(
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="GridViewRowTemplateStyle" TargetType="{x:Type Button}">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="{x:Type Button}">
            <Grid>
              <GridViewRowPresenter />
            </Grid>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </UserControl.Resources>
  <Button x:Name="Probe" Style="{StaticResource GridViewRowTemplateStyle}" />
</UserControl>
""");

        var button = Assert.IsType<Button>(root.FindName("Probe"));
        Assert.True(button.ApplyTemplate());

        var templateRoot = Assert.IsType<Grid>(Assert.Single(button.GetVisualChildren()));
        Assert.Contains(templateRoot.GetVisualChildren(), child => child is GridViewRowPresenter);
    }

    private static void AssertBrushColor(Color expected, Brush? brush)
    {
        var actualBrush = Assert.IsAssignableFrom<Brush>(brush);
        Assert.Equal(expected, actualBrush.ToColor());
    }

    private static ResourceSnapshot CaptureApplicationResources()
    {
        var resources = UiApplication.Current.Resources;
        return new ResourceSnapshot(
            resources.ToList(),
            resources.MergedDictionaries.ToList());
    }

    private static void RestoreApplicationResources(ResourceSnapshot snapshot)
    {
      TestApplicationResources.Restore(snapshot.Entries, snapshot.MergedDictionaries);
    }

    private sealed record ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        List<ResourceDictionary> MergedDictionaries);
}
