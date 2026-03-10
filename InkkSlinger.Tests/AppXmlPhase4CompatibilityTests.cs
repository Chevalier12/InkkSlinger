using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AppXmlPhase4CompatibilityTests
{
    [Fact]
    public void FrameworkElementImplicitStyle_DoesNotOverrideExplicitLocalStyle()
    {
        var backup = CaptureApplicationResources();
        try
        {
            UiApplication.Current.Resources[typeof(Panel)] = BuildPanelStyle(new Color(0x12, 0x34, 0x56));
            var panel = new Panel
            {
                Width = 220f,
                Height = 130f
            };
            var uiRoot = BuildUiRootWithSingleChild(panel, 460, 280);
            RunLayout(uiRoot, 460, 280);

            var explicitStyle = BuildPanelStyle(new Color(0xC8, 0x20, 0x40));
            panel.Style = explicitStyle;

            UiApplication.Current.Resources[typeof(Panel)] = BuildPanelStyle(new Color(0x20, 0x60, 0x90));

            Assert.Same(explicitStyle, panel.Style);
            Assert.Equal(new Color(0xC8, 0x20, 0x40), panel.Background);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void FrameworkElementImplicitStyle_ReappliesOnMergedDictionaryChanges()
    {
        var backup = CaptureApplicationResources();
        try
        {
            var first = new ResourceDictionary();
            first[typeof(Panel)] = BuildPanelStyle(new Color(0x1A, 0x2B, 0x3C));
            UiApplication.Current.Resources.AddMergedDictionary(first);

            var panel = new Panel
            {
                Width = 180f,
                Height = 90f
            };
            var uiRoot = BuildUiRootWithSingleChild(panel, 400, 240);
            RunLayout(uiRoot, 400, 240);
            panel.RaiseLoaded();
            Assert.Equal(new Color(0x1A, 0x2B, 0x3C), panel.Background);

            UiApplication.Current.Resources.RemoveMergedDictionary(first);
            var second = new ResourceDictionary();
            second[typeof(Panel)] = BuildPanelStyle(new Color(0x7B, 0x6A, 0x59));
            UiApplication.Current.Resources.AddMergedDictionary(second);

            Assert.Equal(new Color(0x7B, 0x6A, 0x59), panel.Background);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void Window_ExplicitStyle_IsNotOverriddenByImplicitUpdates()
    {
        var backup = CaptureApplicationResources();
        try
        {
            UiApplication.Current.Resources[typeof(Window)] = BuildWindowStyle(new Color(0x22, 0x44, 0x66));
            using var window = new Window(new FakeWindowNativeAdapter(), new FakeWindowGraphicsAdapter());

            var explicitStyle = BuildWindowStyle(new Color(0x90, 0x40, 0x10));
            window.Style = explicitStyle;
            UiApplication.Current.Resources[typeof(Window)] = BuildWindowStyle(new Color(0x10, 0x70, 0xE0));

            Assert.Same(explicitStyle, window.Style);
            Assert.Equal(new Color(0x90, 0x40, 0x10), window.Background);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void RichTextBox_HyperlinkImplicitStyle_ReappliesOnResourceUpdates()
    {
        var backup = CaptureApplicationResources();
        try
        {
            UiApplication.Current.Resources[typeof(Hyperlink)] = BuildHyperlinkStyle(new Color(0x29, 0x4A, 0x7C));
            var (box, hyperlink) = CreateRichTextBoxWithSingleHyperlink();
            _ = box;

            Assert.Equal(new Color(0x29, 0x4A, 0x7C), hyperlink.Foreground);

            UiApplication.Current.Resources[typeof(Hyperlink)] = BuildHyperlinkStyle(new Color(0xF0, 0xA0, 0x30));

            Assert.Equal(new Color(0xF0, 0xA0, 0x30), hyperlink.Foreground);
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void StyleSetter_SolidColorBrushToColor_CoercesForColorDp()
    {
        var hyperlink = new Hyperlink();
        var style = new Style(typeof(Hyperlink));
        style.Setters.Add(new Setter(Hyperlink.ForegroundProperty, new SolidColorBrush(new Color(0x14, 0x28, 0x3C))));
        hyperlink.Style = style;

        Assert.Equal(new Color(0x14, 0x28, 0x3C), hyperlink.Foreground);
        Assert.Equal(DependencyPropertyValueSource.Style, hyperlink.GetValueSource(Hyperlink.ForegroundProperty));
    }

    [Fact]
    public void TriggerSetter_SolidColorBrushToColor_CoercesForColorDp()
    {
        var hyperlink = new Hyperlink();
        var style = new Style(typeof(Hyperlink));
        var trigger = new Trigger(Hyperlink.IsMouseOverProperty, true);
        trigger.Setters.Add(new Setter(Hyperlink.ForegroundProperty, new SolidColorBrush(new Color(0x66, 0x77, 0x88))));
        style.Triggers.Add(trigger);
        hyperlink.Style = style;

        hyperlink.SetValue(Hyperlink.IsMouseOverProperty, true);

        Assert.Equal(new Color(0x66, 0x77, 0x88), hyperlink.Foreground);
        Assert.Equal(DependencyPropertyValueSource.StyleTrigger, hyperlink.GetValueSource(Hyperlink.ForegroundProperty));
    }

    [Fact]
    public void DiagnosticSink_CapturesUnknownPropertyAndUnsupportedConstruct()
    {
        var diagnostics = new List<XamlDiagnostic>();
        using var sink = XamlLoader.PushDiagnosticSink(diagnostics.Add);

        var unknownPropertyXaml =
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Button UnknownPhase4Property="True" />
</UserControl>
""";
        _ = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(unknownPropertyXaml));

        var unsupportedActionXaml =
            """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="BadActionStyle" TargetType="{x:Type Button}">
      <Style.Triggers>
        <EventTrigger RoutedEvent="Loaded">
          <EventTrigger.Actions>
            <UnsupportedAction />
          </EventTrigger.Actions>
        </EventTrigger>
      </Style.Triggers>
    </Style>
  </UserControl.Resources>
</UserControl>
""";
        _ = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(unsupportedActionXaml));

        var unknownProperty = diagnostics.Last(
            d => d.Code == XamlDiagnosticCode.UnknownProperty &&
                 string.Equals(d.PropertyName, "UnknownPhase4Property", StringComparison.Ordinal) &&
                 d.Line.HasValue &&
                 d.Position.HasValue);
        Assert.Equal("Button", unknownProperty.ElementName);
        Assert.True(unknownProperty.Line.HasValue);
        Assert.True(unknownProperty.Position.HasValue);

        var unsupportedConstruct = diagnostics.Last(
            d => d.Code == XamlDiagnosticCode.UnsupportedConstruct &&
                 string.Equals(d.ElementName, "UnsupportedAction", StringComparison.Ordinal));
        Assert.True(unsupportedConstruct.Line.HasValue);
        Assert.True(unsupportedConstruct.Position.HasValue);
    }

    private static Style BuildPanelStyle(Color background)
    {
        var style = new Style(typeof(Panel));
        style.Setters.Add(new Setter(Panel.BackgroundProperty, background));
        return style;
    }

    private static Style BuildWindowStyle(Color background)
    {
        var style = new Style(typeof(Window));
        style.Setters.Add(new Setter(Window.BackgroundProperty, background));
        return style;
    }

    private static Style BuildHyperlinkStyle(Color foreground)
    {
        var style = new Style(typeof(Hyperlink));
        style.Setters.Add(new Setter(Hyperlink.ForegroundProperty, foreground));
        return style;
    }

    private static (RichTextBox Box, Hyperlink Hyperlink) CreateRichTextBoxWithSingleHyperlink()
    {
        var hyperlink = new Hyperlink
        {
            NavigateUri = "https://example.com/inkkslinger"
        };
        hyperlink.Inlines.Add(new Run("Open link"));
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(hyperlink);
        var document = new FlowDocument();
        document.Blocks.Add(paragraph);

        var box = new RichTextBox
        {
            Width = 280f,
            Height = 120f,
            Document = document
        };

        return (box, hyperlink);
    }

    private static UiRoot BuildUiRootWithSingleChild(FrameworkElement element, int width, int height)
    {
        var host = new Canvas
        {
            Width = width,
            Height = height
        };
        host.AddChild(element);
        Canvas.SetLeft(element, 28f);
        Canvas.SetTop(element, 18f);
        return new UiRoot(host);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
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
        var resources = UiApplication.Current.Resources;
        resources.Clear();
        foreach (var merged in resources.MergedDictionaries.ToList())
        {
            resources.RemoveMergedDictionary(merged);
        }

        foreach (var pair in snapshot.Entries)
        {
            resources[pair.Key] = pair.Value;
        }

        foreach (var merged in snapshot.MergedDictionaries)
        {
            resources.AddMergedDictionary(merged);
        }
    }

    private sealed record ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        List<ResourceDictionary> MergedDictionaries);

    private sealed class FakeWindowNativeAdapter : IWindowNativeAdapter
    {
        public event EventHandler<EventArgs>? ClientSizeChanged;

        public string Title { get; set; } = string.Empty;

        public bool AllowUserResizing { get; set; }

        public bool IsBorderless { get; set; }

        public Point Position { get; set; }

        public Rectangle ClientBounds { get; set; } = new(0, 0, 1200, 800);

        public IntPtr Handle { get; } = new(101);

        public void RaiseClientSizeChanged()
        {
            ClientSizeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeWindowGraphicsAdapter : IWindowGraphicsAdapter
    {
        public int PreferredBackBufferWidth { get; set; } = 1200;

        public int PreferredBackBufferHeight { get; set; } = 800;

        public bool IsFullScreen { get; set; }

        public void ApplyChanges()
        {
        }
    }
}
