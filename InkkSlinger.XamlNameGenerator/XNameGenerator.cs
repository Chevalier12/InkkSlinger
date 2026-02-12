using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace InkkSlinger.XamlNameGenerator;

[Generator]
public sealed class XNameGenerator : IIncrementalGenerator
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var viewModels = context.AdditionalTextsProvider
            .Where(static file => IsXmlViewFile(file.Path))
            .Select(static (file, cancellationToken) => ParseView(file, cancellationToken));

        var generationInputs = viewModels.Combine(context.CompilationProvider);
        context.RegisterSourceOutput(generationInputs, static (productionContext, input) =>
        {
            var parseResult = input.Left;
            var compilation = input.Right;

            foreach (var diagnostic in parseResult.Diagnostics)
            {
                productionContext.ReportDiagnostic(diagnostic);
            }

            if (parseResult.Model is not ViewModel model)
            {
                return;
            }

            EmitNamedMembers(productionContext, compilation, model);
        });
    }

    private static void EmitNamedMembers(
        SourceProductionContext productionContext,
        Compilation compilation,
        ViewModel model)
    {
        if (model.NamedElements.IsDefaultOrEmpty)
        {
            return;
        }

        if (!TryResolveOwnerType(compilation, model.OwnerClass, out var ownerType))
        {
            var location = CreateLocation(model.FilePath, model.SourceText, model.OwnerLineNumber, model.OwnerLinePosition);
            productionContext.ReportDiagnostic(
                CreateDiagnostic(
                    "XNAME020",
                    "Could not resolve owner class for generated names.",
                    $"View '{model.FilePath}' targets '{model.OwnerClass}', but that type was not found in compilation.",
                    DiagnosticSeverity.Warning,
                    location));
            return;
        }

        var generatedMembers = new List<GeneratedMember>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in model.NamedElements)
        {
            var location = CreateLocation(model.FilePath, model.SourceText, element.LineNumber, element.LinePosition);

            if (!SyntaxFacts.IsValidIdentifier(element.Name))
            {
                productionContext.ReportDiagnostic(
                    CreateDiagnostic(
                        "XNAME021",
                        "Invalid x:Name identifier.",
                        $"'{element.Name}' is not a valid C# identifier and cannot be generated.",
                        DiagnosticSeverity.Warning,
                        location));
                continue;
            }

            if (!seenNames.Add(element.Name))
            {
                productionContext.ReportDiagnostic(
                    CreateDiagnostic(
                        "XNAME022",
                        "Duplicate x:Name in view.",
                        $"The x:Name '{element.Name}' appears multiple times in '{model.FilePath}'.",
                        DiagnosticSeverity.Warning,
                        location));
                continue;
            }

            if (ownerType!.GetMembers(element.Name).Length > 0)
            {
                productionContext.ReportDiagnostic(
                    CreateDiagnostic(
                        "XNAME023",
                        "Generated member conflicts with existing member.",
                        $"Type '{ownerType.ToDisplayString()}' already declares '{element.Name}'. Generation for this name was skipped.",
                        DiagnosticSeverity.Info,
                        location));
                continue;
            }

            var resolvedType = ResolveControlType(compilation, element.TagName);
            if (resolvedType.UsedFallback)
            {
                productionContext.ReportDiagnostic(
                    CreateDiagnostic(
                        "XNAME024",
                        "Unknown XML tag mapped to fallback type.",
                        $"Tag '{element.TagName}' could not be mapped to a known control type. Falling back to '{resolvedType.TypeDisplayName}'.",
                        DiagnosticSeverity.Info,
                        location));
            }

            generatedMembers.Add(new GeneratedMember(element.Name, resolvedType.TypeDisplayName));
        }

        if (generatedMembers.Count == 0)
        {
            return;
        }

        var source = BuildSource(ownerType!, generatedMembers);
        var hintName = BuildHintName(model.FilePath, ownerType!);
        productionContext.AddSource(hintName, source);
    }

    private static string BuildSource(INamedTypeSymbol ownerType, IReadOnlyList<GeneratedMember> members)
    {
        var builder = new StringBuilder(256);
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        var namespaceName = ownerType.ContainingNamespace?.ToDisplayString();
        if (!string.IsNullOrWhiteSpace(namespaceName))
        {
            builder.Append("namespace ");
            builder.Append(namespaceName);
            builder.AppendLine(";");
            builder.AppendLine();
        }

        builder.Append("public partial class ");
        builder.Append(ownerType.Name);
        builder.AppendLine();
        builder.AppendLine("{");

        foreach (var member in members)
        {
            builder.Append("    private ");
            builder.Append(member.TypeDisplayName);
            builder.Append("? ");
            builder.Append(member.Name);
            builder.AppendLine(" { get; set; }");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildHintName(string filePath, INamedTypeSymbol ownerType)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var safeFileName = SanitizeHintSegment(fileName);
        var safeOwner = SanitizeHintSegment(ownerType.ToDisplayString());
        var pathHash = ComputeDeterministicHash(filePath);
        return $"{safeFileName}.{safeOwner}.{pathHash}.Names.g.cs";
    }

    private static string SanitizeHintSegment(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString();
    }

    private static string ComputeDeterministicHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            for (var i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return hash.ToString("x8");
        }
    }

    private static ResolvedType ResolveControlType(Compilation compilation, string tagName)
    {
        var inkkSlingerTypeName = $"InkkSlinger.{tagName}";
        if (compilation.GetTypeByMetadataName(inkkSlingerTypeName) is not null)
        {
            return new ResolvedType($"global::{inkkSlingerTypeName}", usedFallback: false);
        }

        if (compilation.GetTypeByMetadataName("InkkSlinger.UIElement") is not null)
        {
            return new ResolvedType("global::InkkSlinger.UIElement", usedFallback: true);
        }

        return new ResolvedType("global::object", usedFallback: true);
    }

    private static bool TryResolveOwnerType(Compilation compilation, string ownerClass, out INamedTypeSymbol? ownerType)
    {
        if (ownerClass.IndexOf('.') >= 0)
        {
            ownerType = compilation.GetTypeByMetadataName(ownerClass);
            return ownerType is not null;
        }

        var matches = new List<INamedTypeSymbol>();
        CollectMatchingTypes(compilation.Assembly.GlobalNamespace, ownerClass, matches);
        if (matches.Count == 1)
        {
            ownerType = matches[0];
            return true;
        }

        foreach (var match in matches)
        {
            if (string.Equals(match.ContainingNamespace?.ToDisplayString(), "InkkSlinger", StringComparison.Ordinal))
            {
                ownerType = match;
                return true;
            }
        }

        ownerType = null;
        return false;
    }

    private static void CollectMatchingTypes(INamespaceSymbol namespaceSymbol, string typeName, List<INamedTypeSymbol> matches)
    {
        foreach (var member in namespaceSymbol.GetTypeMembers())
        {
            if (string.Equals(member.Name, typeName, StringComparison.Ordinal))
            {
                matches.Add(member);
            }
        }

        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            CollectMatchingTypes(nestedNamespace, typeName, matches);
        }
    }

    private static bool IsXmlViewFile(string path)
    {
        if (!path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedPath = path.Replace('\\', '/');
        return normalizedPath.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) < 0
               && normalizedPath.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static ViewParseResult ParseView(AdditionalText file, System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var text = file.GetText(cancellationToken);
        if (text is null)
        {
            return ViewParseResult.WithError(
                CreateDiagnostic(
                    "XNAME001",
                    "Unable to read XML view.",
                    $"Could not read '{file.Path}'.",
                    DiagnosticSeverity.Warning,
                    Location.None));
        }

        try
        {
            var document = XDocument.Parse(text.ToString(), LoadOptions.SetLineInfo);
            var root = document.Root;
            if (root is null)
            {
                return ViewParseResult.WithError(
                    CreateDiagnostic(
                        "XNAME002",
                        "Invalid XML view.",
                        $"View '{file.Path}' has no root element.",
                        DiagnosticSeverity.Warning,
                        CreateLocation(file.Path, text, 1, 1)));
            }

            if (!IsUiMarkupDocument(root))
            {
                return ViewParseResult.None;
            }

            var ownerClassAttribute = root.Attribute(XamlNamespace + "Class");
            var ownerClass = ownerClassAttribute?.Value?.Trim();
            var usedFallbackOwnerClass = string.IsNullOrWhiteSpace(ownerClass);
            ownerClass ??= Path.GetFileNameWithoutExtension(file.Path);

            var ownerLineInfo = ownerClassAttribute as IXmlLineInfo;
            var ownerLine = ownerLineInfo?.HasLineInfo() == true ? ownerLineInfo.LineNumber : 1;
            var ownerPosition = ownerLineInfo?.HasLineInfo() == true ? ownerLineInfo.LinePosition : 1;

            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
            if (usedFallbackOwnerClass)
            {
                diagnostics.Add(
                    CreateDiagnostic(
                        "XNAME010",
                        "x:Class missing; using filename fallback.",
                        $"View '{file.Path}' has no x:Class. Falling back to '{ownerClass}'.",
                        DiagnosticSeverity.Info,
                        CreateLocation(file.Path, text, ownerLine, ownerPosition)));
            }

            var namedElements = new List<NamedElementModel>();
            foreach (var element in root.DescendantsAndSelf())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var nameAttribute = element.Attribute(XamlNamespace + "Name");
                if (nameAttribute is null)
                {
                    continue;
                }

                var elementName = nameAttribute.Value.Trim();
                if (string.IsNullOrWhiteSpace(elementName))
                {
                    continue;
                }

                var lineInfo = (IXmlLineInfo)nameAttribute;
                namedElements.Add(
                    new NamedElementModel(
                        elementName,
                        element.Name.LocalName,
                        lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1,
                        lineInfo.HasLineInfo() ? lineInfo.LinePosition : 1));
            }

            return ViewParseResult.WithModel(
                new ViewModel(
                    file.Path,
                    text,
                    ownerClass,
                    ownerLine,
                    ownerPosition,
                    namedElements.ToImmutableArray()),
                diagnostics.ToImmutable());
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            return ViewParseResult.WithError(
                CreateDiagnostic(
                    "XNAME003",
                    "Failed to parse XML view.",
                    $"View '{file.Path}' could not be parsed: {ex.Message}",
                    DiagnosticSeverity.Warning,
                    CreateLocation(file.Path, text, 1, 1)));
        }
    }

    private static Diagnostic CreateDiagnostic(
        string id,
        string title,
        string message,
        DiagnosticSeverity severity,
        Location location)
    {
        var descriptor = new DiagnosticDescriptor(
            id,
            title,
            message,
            "XamlNameGenerator",
            severity,
            isEnabledByDefault: true);

        return Diagnostic.Create(descriptor, location);
    }

    private static bool IsUiMarkupDocument(XElement root)
    {
        if (root.Name.NamespaceName == "urn:inkkslinger-ui")
        {
            return true;
        }

        if (root.Attribute(XamlNamespace + "Class") is not null)
        {
            return true;
        }

        foreach (var element in root.DescendantsAndSelf())
        {
            if (element.Attribute(XamlNamespace + "Name") is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static Location CreateLocation(string path, SourceText sourceText, int lineNumber, int linePosition)
    {
        if (sourceText.Lines.Count == 0)
        {
            return Location.Create(path, new TextSpan(0, 0), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 0)));
        }

        var lineIndex = Math.Max(0, Math.Min(lineNumber - 1, sourceText.Lines.Count - 1));
        var textLine = sourceText.Lines[lineIndex];
        var characterIndex = Math.Max(0, Math.Min(linePosition - 1, textLine.Span.Length));
        var absolutePosition = textLine.Start + characterIndex;
        var linePositionValue = new LinePosition(lineIndex, characterIndex);
        return Location.Create(path, new TextSpan(absolutePosition, 0), new LinePositionSpan(linePositionValue, linePositionValue));
    }

    private readonly struct ViewParseResult
    {
        public ViewParseResult(ViewModel? model, ImmutableArray<Diagnostic> diagnostics)
        {
            Model = model;
            Diagnostics = diagnostics;
        }

        public ViewModel? Model { get; }

        public ImmutableArray<Diagnostic> Diagnostics { get; }

        public static ViewParseResult WithModel(ViewModel model, ImmutableArray<Diagnostic> diagnostics) => new(model, diagnostics);

        public static ViewParseResult WithError(Diagnostic diagnostic) => new(null, ImmutableArray.Create(diagnostic));

        public static ViewParseResult None => new(null, ImmutableArray<Diagnostic>.Empty);
    }

    private readonly struct ViewModel
    {
        public ViewModel(
            string filePath,
            SourceText sourceText,
            string ownerClass,
            int ownerLineNumber,
            int ownerLinePosition,
            ImmutableArray<NamedElementModel> namedElements)
        {
            FilePath = filePath;
            SourceText = sourceText;
            OwnerClass = ownerClass;
            OwnerLineNumber = ownerLineNumber;
            OwnerLinePosition = ownerLinePosition;
            NamedElements = namedElements;
        }

        public string FilePath { get; }

        public SourceText SourceText { get; }

        public string OwnerClass { get; }

        public int OwnerLineNumber { get; }

        public int OwnerLinePosition { get; }

        public ImmutableArray<NamedElementModel> NamedElements { get; }
    }

    private readonly struct NamedElementModel
    {
        public NamedElementModel(string name, string tagName, int lineNumber, int linePosition)
        {
            Name = name;
            TagName = tagName;
            LineNumber = lineNumber;
            LinePosition = linePosition;
        }

        public string Name { get; }

        public string TagName { get; }

        public int LineNumber { get; }

        public int LinePosition { get; }
    }

    private readonly struct GeneratedMember
    {
        public GeneratedMember(string name, string typeDisplayName)
        {
            Name = name;
            TypeDisplayName = typeDisplayName;
        }

        public string Name { get; }

        public string TypeDisplayName { get; }
    }

    private readonly struct ResolvedType
    {
        public ResolvedType(string typeDisplayName, bool usedFallback)
        {
            TypeDisplayName = typeDisplayName;
            UsedFallback = usedFallback;
        }

        public string TypeDisplayName { get; }

        public bool UsedFallback { get; }
    }
}
