using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using InkkSlinger.XamlNameGenerator;
using Microsoft.CodeAnalysis;
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
        var generatedSource = result.Results.Single().GeneratedSources.Single().SourceText.ToString();

        Assert.Contains("private global::InkkSlinger.Grid? RootGrid { get; set; }", generatedSource);
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
        var view = new MainMenuView();
        var property = typeof(MainMenuView).GetProperty("DemoTextBox", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(property);
        var value = property!.GetValue(view);
        Assert.NotNull(value);
        Assert.IsType<TextBox>(value);
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
}
