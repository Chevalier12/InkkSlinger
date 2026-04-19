using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using InkkSlinger.XamlNameGenerator;
using Microsoft.CodeAnalysis;
using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class XNameSourceGeneratorTests
{
    [Fact]
    public void GeneratesNamedMembers_ForValidXNameElements()
    {
        const string source = """
namespace InkkSlinger;

public partial class SampleView
{
}

public class UIElement
{
}

public class Grid : UIElement
{
}
""";

        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="InkkSlinger.SampleView">
  <Grid x:Name="RootGrid" />
</UserControl>
""";

        var result = RunGenerator(source, new TestAdditionalText("Views/SampleView.xml", xml));
        var generatedSource = result.Results.Single().GeneratedSources
            .Single(static s => s.HintName.EndsWith("Names.g.cs", StringComparison.Ordinal))
            .SourceText.ToString();

        Assert.Contains("private global::InkkSlinger.Grid RootGrid { get; set; } = default!;", generatedSource);
    }

    [Fact]
    public void GeneratesInitializeComponent_ForValidXClassView()
    {
        const string source = """
namespace InkkSlinger;

public partial class SampleView : UserControl
{
}

public class UserControl
{
}
""";

        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="InkkSlinger.SampleView">
</UserControl>
""";

        var result = RunGenerator(source, new TestAdditionalText("Views/SampleView.xml", xml));
        var generated = result.Results.Single().GeneratedSources.Select(static s => s.SourceText.ToString()).ToArray();
        var initSource = generated.Single(static text => text.Contains("private void InitializeComponent()", StringComparison.Ordinal));
        Assert.Contains("global::InkkSlinger.XamlLoader.LoadIntoCompiledFromString", initSource);
        Assert.Contains("x:Class=\"\"InkkSlinger.SampleView\"\"", initSource);
    }

    [Fact]
    public void ReportsDiagnostic_WhenXClassIsMissing()
    {
        const string source = """
namespace InkkSlinger;

public partial class SampleView : UserControl
{
}

public class UserControl
{
}
""";

        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl x:Name="Root" />
</UserControl>
""";

        var result = RunGenerator(source, new TestAdditionalText("Views/SampleView.xml", xml));
        var ids = result.Results.Single().Diagnostics.Select(static d => d.Id).ToImmutableHashSet(StringComparer.Ordinal);
        Assert.Contains("XNAME010", ids);
    }

    [Fact]
    public void GeneratesNamedField_WithFieldModifier()
    {
        const string source = """
namespace InkkSlinger;

public partial class SampleView : UserControl
{
}

public class UserControl
{
}

public class Grid : UserControl
{
}
""";

        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="InkkSlinger.SampleView">
  <Grid x:Name="RootGrid" x:FieldModifier="internal" />
</UserControl>
""";

        var result = RunGenerator(source, new TestAdditionalText("Views/SampleView.xml", xml));
        var generatedSource = result.Results.Single().GeneratedSources.Single(static s => s.HintName.EndsWith("Names.g.cs", StringComparison.Ordinal)).SourceText.ToString();
        Assert.Contains("internal global::InkkSlinger.Grid RootGrid { get; set; } = default!;", generatedSource);
    }

    [Fact]
    public void ReportsDiagnostic_WhenEventHandlerSignatureDoesNotMatchDelegate()
    {
        const string source = """
namespace InkkSlinger;

using System;

public partial class SampleView : UserControl
{
    private void HandleClick(object sender)
    {
    }
}

public class UserControl
{
}

public class Button : UserControl
{
    public event EventHandler? Click;
}
""";

        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="InkkSlinger.SampleView">
  <Button Click="HandleClick" />
</UserControl>
""";

        var result = RunGenerator(source, new TestAdditionalText("Views/SampleView.xml", xml));
        var ids = result.Results.Single().Diagnostics.Select(static d => d.Id).ToImmutableHashSet(StringComparer.Ordinal);
        Assert.Contains("XNAME041", ids);
    }

    [Fact]
    public void ReportsDiagnostic_ForUnknownEventLikeAttribute()
    {
        const string source = """
namespace InkkSlinger;

public partial class SampleView : UserControl
{
}

public class UserControl
{
}
""";

        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="InkkSlinger.SampleView"
             NotAPropertyOrEvent="Handler" />
""";

        var result = RunGenerator(source, new TestAdditionalText("Views/SampleView.xml", xml));
        var ids = result.Results.Single().Diagnostics.Select(static d => d.Id).ToImmutableHashSet(StringComparer.Ordinal);
        Assert.Contains("XNAME042", ids);
    }

    [Fact]
    public void CustomControlAttributes_InCurrentAssembly_DoNotReportXName042()
    {
        const string source = """
namespace InkkSlinger;

public class UserControl
{
}

public class Panel : UserControl
{
    public object? Margin { get; set; }
    public object? FontFamily { get; set; }
}

namespace InkkSlinger.Designer;

public partial class SampleView : global::InkkSlinger.UserControl
{
}

public sealed class DesignerSourceLineNumberPresenter : global::InkkSlinger.Panel
{
    public object? LineForeground { get; set; }
}
""";

        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="InkkSlinger.Designer.SampleView">
  <DesignerSourceLineNumberPresenter Margin="0,10,6,0"
                                     FontFamily="Consolas"
                                     LineForeground="#4A6880" />
</UserControl>
""";

        var result = RunGenerator(source, new TestAdditionalText("Views/SampleView.xml", xml));
        var ids = result.Results.Single().Diagnostics.Select(static d => d.Id).ToImmutableHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("XNAME042", ids);
    }

    [Fact]
    public void UnknownEventLikeAttribute_OnReadOnlyProperty_StillReportsDiagnostic()
    {
        const string source = """
namespace InkkSlinger;

public partial class SampleView : UserControl
{
}

public class UserControl
{
    public string ReadOnlyProp => "value";
}
""";

        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="InkkSlinger.SampleView"
             ReadOnlyProp="Handler" />
""";

        var result = RunGenerator(source, new TestAdditionalText("Views/SampleView.xml", xml));
        var ids = result.Results.Single().Diagnostics.Select(static d => d.Id).ToImmutableHashSet(StringComparer.Ordinal);
        Assert.Contains("XNAME042", ids);
    }

        [Fact]
        public void UserControlResources_WithStyleSettersAndTriggers_DoesNotReportXName042()
        {
                const string source = """
namespace InkkSlinger;

public partial class SampleView : UserControl
{
}

public class UserControl
{
}

public class Button : UserControl
{
        public bool IsMouseOver { get; set; }
        public bool IsPressed { get; set; }
        public bool IsFocused { get; set; }
        public bool IsEnabled { get; set; }
        public object? Background { get; set; }
        public object? BorderBrush { get; set; }
        public object? BorderThickness { get; set; }
        public object? Padding { get; set; }
        public object? HorizontalContentAlignment { get; set; }
        public object? VerticalContentAlignment { get; set; }
        public object? Cursor { get; set; }
        public object? Template { get; set; }
}

public class Border : UserControl
{
        public object? Background { get; set; }
        public object? BorderBrush { get; set; }
        public object? BorderThickness { get; set; }
        public object? CornerRadius { get; set; }
        public object? Padding { get; set; }
}

public class ContentPresenter : UserControl
{
        public object? HorizontalAlignment { get; set; }
        public object? VerticalAlignment { get; set; }
}
""";

                const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="InkkSlinger.SampleView">
    <UserControl.Resources>
        <Style x:Key="DiagnosticCardButtonStyle" TargetType="{x:Type Button}">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="BorderBrush" Value="Transparent" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="Padding" Value="0" />
            <Setter Property="HorizontalContentAlignment" Value="Stretch" />
            <Setter Property="VerticalContentAlignment" Value="Stretch" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="{x:Type Button}">
                        <Border x:Name="HoverChrome"
                                        Background="Transparent"
                                        BorderBrush="Transparent"
                                        BorderThickness="1"
                                        CornerRadius="7"
                                        Padding="1">
                            <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                                                VerticalAlignment="{TemplateBinding VerticalContentAlignment}" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="HoverChrome" Property="Background" Value="#132131" />
                                <Setter TargetName="HoverChrome" Property="BorderBrush" Value="#294866" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </UserControl.Resources>
</UserControl>
""";

                var result = RunGenerator(source, new TestAdditionalText("Views/SampleView.xml", xml));
                var ids = result.Results.Single().Diagnostics.Select(static d => d.Id).ToImmutableHashSet(StringComparer.Ordinal);
                Assert.DoesNotContain("XNAME042", ids);
        }

    [Fact]
    public void ReportsDiagnostic_WhenClassModifierDoesNotMatchOwnerAccessibility()
    {
        const string source = """
namespace InkkSlinger;

public partial class SampleView : UserControl
{
}

public class UserControl
{
}
""";

        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="InkkSlinger.SampleView"
             x:ClassModifier="internal" />
""";

        var result = RunGenerator(source, new TestAdditionalText("Views/SampleView.xml", xml));
        var ids = result.Results.Single().Diagnostics.Select(static d => d.Id).ToImmutableHashSet(StringComparer.Ordinal);
        Assert.Contains("XNAME033", ids);
    }

    [Fact]
    public void ReportsDiagnostic_WhenOwnerTypeIsNotUserControl()
    {
        const string source = """
namespace InkkSlinger;

public partial class SampleView
{
}

public class UserControl
{
}
""";

        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="InkkSlinger.SampleView" />
""";

        var result = RunGenerator(source, new TestAdditionalText("Views/SampleView.xml", xml));
        var ids = result.Results.Single().Diagnostics.Select(static d => d.Id).ToImmutableHashSet(StringComparer.Ordinal);
        Assert.Contains("XNAME036", ids);
    }

    [Fact]
    public void ReportsDiagnostics_ForInvalidAndDuplicateXName()
    {
        const string source = """
namespace InkkSlinger;

public partial class DiagnosticView
{
}

public class UIElement
{
}
""";

        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="InkkSlinger.DiagnosticView">
  <Grid x:Name="1BadName" />
  <Grid x:Name="Dup" />
  <Grid x:Name="Dup" />
</UserControl>
""";

        var result = RunGenerator(source, new TestAdditionalText("Views/DiagnosticView.xml", xml));
        var ids = result.Results.Single().Diagnostics.Select(static d => d.Id).ToImmutableHashSet(StringComparer.Ordinal);

        Assert.Contains("XNAME021", ids);
        Assert.Contains("XNAME022", ids);
    }

    [Fact]
    public void ReportsDiagnostic_WhenMemberAlreadyExists()
    {
        const string source = """
namespace InkkSlinger;

public partial class ConflictView
{
    private UIElement? Existing { get; set; }
}

public class UIElement
{
}

public class Grid : UIElement
{
}
""";

        const string xml = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="InkkSlinger.ConflictView">
  <Grid x:Name="Existing" />
</UserControl>
""";

        var result = RunGenerator(source, new TestAdditionalText("Views/ConflictView.xml", xml));
        var ids = result.Results.Single().Diagnostics.Select(static d => d.Id).ToImmutableHashSet(StringComparer.Ordinal);

        Assert.Contains("XNAME023", ids);
    }

    [Fact]
    public void MainMenuView_GeneratedMember_IsAssignedByLoader()
    {
        var snapshot = CaptureApplicationResources();
        try
        {
            LoadAppResources();

            var view = new MainMenuView();
            var property = typeof(MainMenuView).GetProperty("DemoTextBox", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(property);
            var value = property!.GetValue(view);
            Assert.NotNull(value);
            Assert.IsType<TextBox>(value);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    private static GeneratorDriverRunResult RunGenerator(string source, params AdditionalText[] additionalTexts)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorUnitTests",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new XNameGenerator().AsSourceGenerator() },
            additionalTexts: additionalTexts,
            parseOptions: (CSharpParseOptions)syntaxTree.Options);

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private sealed class TestAdditionalText : AdditionalText
    {
        private readonly SourceText _sourceText;

        public TestAdditionalText(string path, string text)
        {
            Path = path;
            _sourceText = SourceText.From(text);
        }

        public override string Path { get; }

        public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _sourceText;
        }
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

    private static void LoadAppResources()
    {
        var appPath = TestApplicationResources.GetDemoAppAppXmlPath();
        XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);
    }

    private sealed record ResourceSnapshot(
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<object, object>> Entries,
        System.Collections.Generic.List<ResourceDictionary> MergedDictionaries);
}
