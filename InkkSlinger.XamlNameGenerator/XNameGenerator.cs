using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace InkkSlinger.XamlNameGenerator;

[Generator]
public sealed class XNameGenerator : IIncrementalGenerator
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly Dictionary<string, string> ElementAliases = new(StringComparer.Ordinal)
    {
        ["Rectangle"] = "RectangleShape",
        ["Ellipse"] = "EllipseShape",
        ["Line"] = "LineShape",
        ["Polygon"] = "PolygonShape",
        ["Polyline"] = "PolylineShape",
        ["Path"] = "PathShape"
    };

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
        if (string.IsNullOrWhiteSpace(model.OwnerClass))
        {
            var location = CreateLocation(model.FilePath, model.SourceText, model.RootLineNumber, model.RootLinePosition);
            productionContext.ReportDiagnostic(
                CreateDiagnostic(
                    "XNAME010",
                    "x:Class is required.",
                    $"View '{model.FilePath}' must define x:Class for compile-time generation.",
                    DiagnosticSeverity.Error,
                    location));
            return;
        }

        if (!TryResolveOwnerType(compilation, model.OwnerClass!, out var ownerType))
        {
            var location = CreateLocation(model.FilePath, model.SourceText, model.OwnerLineNumber, model.OwnerLinePosition);
            productionContext.ReportDiagnostic(
                CreateDiagnostic(
                    "XNAME020",
                    "Could not resolve owner class for generated names.",
                    $"View '{model.FilePath}' targets '{model.OwnerClass}', but that type was not found in compilation.",
                    DiagnosticSeverity.Error,
                    location));
            return;
        }

        if (ownerType is null)
        {
            return;
        }

        ValidateOwnerPartial(productionContext, ownerType, model);
        ValidateRootTypeCompatibility(productionContext, compilation, ownerType, model);
        ValidateClassModifier(productionContext, model, ownerType);
        ValidateCompiledRootCompatibility(productionContext, compilation, ownerType, model);
        ValidateSubclass(productionContext, model);
        ValidateEventHandlers(productionContext, compilation, ownerType, model);
        EmitLegacyLoadWarning(productionContext, ownerType);

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
                        DiagnosticSeverity.Error,
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
                        DiagnosticSeverity.Error,
                        location));
                continue;
            }

            if (ownerType.GetMembers(element.Name).Length > 0)
            {
                productionContext.ReportDiagnostic(
                    CreateDiagnostic(
                        "XNAME023",
                        "Generated member conflicts with existing member.",
                        $"Type '{ownerType.ToDisplayString()}' already declares '{element.Name}'. Generation for this name was skipped.",
                        DiagnosticSeverity.Warning,
                        location));
                continue;
            }

            var resolvedType = ResolveControlType(compilation, element.TagName);
            if (resolvedType.Symbol is null)
            {
                productionContext.ReportDiagnostic(
                    CreateDiagnostic(
                        "XNAME024",
                        "Unknown XML tag mapped to fallback type.",
                        $"Tag '{element.TagName}' could not be mapped to a known control type. Falling back to 'object'.",
                        DiagnosticSeverity.Warning,
                        location));
            }

            var modifier = ResolveFieldModifier(productionContext, model, element);
            if (modifier is null)
            {
                continue;
            }

            generatedMembers.Add(new GeneratedMember(element.Name, resolvedType.TypeDisplayName, modifier));
        }

        if (generatedMembers.Count > 0)
        {
            var source = BuildSource(ownerType, generatedMembers);
            var hintName = BuildHintName(model.FilePath, ownerType, "Names.g.cs");
            productionContext.AddSource(hintName, source);
        }

        var initializeSource = BuildInitializeComponentSource(ownerType, model);
        var initializeHintName = BuildHintName(model.FilePath, ownerType, "InitializeComponent.g.cs");
        productionContext.AddSource(initializeHintName, initializeSource);
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
            builder.Append("    ");
            builder.Append(member.Modifier);
            builder.Append(' ');
            builder.Append(member.TypeDisplayName);
            builder.Append("? ");
            builder.Append(member.Name);
            builder.AppendLine(" { get; set; }");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildInitializeComponentSource(INamedTypeSymbol ownerType, ViewModel model)
    {
        var relativePath = BuildRuntimeRelativePath(model.FilePath);
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
        builder.AppendLine("    private bool _isInitialized;");
        builder.AppendLine();
        builder.AppendLine("    private void InitializeComponent()");
        builder.AppendLine("    {");
        builder.AppendLine("        if (_isInitialized)");
        builder.AppendLine("        {");
        builder.AppendLine("            return;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        _isInitialized = true;");
        builder.Append("        var markupPath = global::System.IO.Path.Combine(global::System.AppContext.BaseDirectory");
        foreach (var segment in relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries))
        {
            builder.Append(", \"");
            builder.Append(EscapeStringLiteral(segment));
            builder.Append('\"');
        }

        builder.AppendLine(");");
        builder.Append("        global::InkkSlinger.XamlLoader.LoadIntoCompiled(this, markupPath, this, \"");
        builder.Append(EscapeStringLiteral(ownerType.ToDisplayString()));
        builder.AppendLine("\");");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildRuntimeRelativePath(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        var viewIndex = normalized.IndexOf("/Views/", StringComparison.OrdinalIgnoreCase);
        if (viewIndex >= 0)
        {
            return normalized.Substring(viewIndex + 1);
        }

        if (normalized.StartsWith("Views/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        return Path.GetFileName(filePath);
    }

    private static string EscapeStringLiteral(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string BuildHintName(string filePath, INamedTypeSymbol ownerType, string suffix)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var safeFileName = SanitizeHintSegment(fileName);
        var safeOwner = SanitizeHintSegment(ownerType.ToDisplayString());
        var pathHash = ComputeDeterministicHash(filePath);
        return $"{safeFileName}.{safeOwner}.{pathHash}.{suffix}";
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
        var mappedName = ElementAliases.TryGetValue(tagName, out var alias) ? alias : tagName;
        var inkkSlingerTypeName = $"InkkSlinger.{mappedName}";
        var symbol = compilation.GetTypeByMetadataName(inkkSlingerTypeName);
        if (symbol is not null)
        {
            return new ResolvedType(symbol, $"global::{inkkSlingerTypeName}");
        }

        var fallback = compilation.GetTypeByMetadataName("InkkSlinger.UIElement");
        if (fallback is not null)
        {
            return new ResolvedType(fallback, "global::InkkSlinger.UIElement");
        }

        return new ResolvedType(null, "global::object");
    }

    private static void ValidateOwnerPartial(SourceProductionContext context, INamedTypeSymbol ownerType, ViewModel model)
    {
        foreach (var syntaxReference in ownerType.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is TypeDeclarationSyntax declaration &&
                declaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)))
            {
                return;
            }
        }

        context.ReportDiagnostic(
            CreateDiagnostic(
                "XNAME031",
                "x:Class target type must be partial.",
                $"Type '{ownerType.ToDisplayString()}' must be declared partial for generated XAML members.",
                DiagnosticSeverity.Error,
                CreateLocation(model.FilePath, model.SourceText, model.OwnerLineNumber, model.OwnerLinePosition)));
    }

    private static void ValidateRootTypeCompatibility(
        SourceProductionContext context,
        Compilation compilation,
        INamedTypeSymbol ownerType,
        ViewModel model)
    {
        var rootType = ResolveControlType(compilation, model.RootTagName).Symbol;
        if (rootType is null)
        {
            return;
        }

        if (IsAssignableTo(ownerType, rootType))
        {
            return;
        }

        context.ReportDiagnostic(
            CreateDiagnostic(
                "XNAME030",
                "Root element and x:Class type are incompatible.",
                $"Type '{ownerType.ToDisplayString()}' must derive from '{rootType.ToDisplayString()}' to back root element '{model.RootTagName}'.",
                DiagnosticSeverity.Error,
                CreateLocation(model.FilePath, model.SourceText, model.RootLineNumber, model.RootLinePosition)));
    }

    private static bool IsAssignableTo(ITypeSymbol candidate, ITypeSymbol target)
    {
        var current = candidate;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target))
            {
                return true;
            }

            current = (current as INamedTypeSymbol)?.BaseType;
        }

        return false;
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

    private static void ValidateClassModifier(
        SourceProductionContext context,
        ViewModel model,
        INamedTypeSymbol ownerType)
    {
        if (string.IsNullOrWhiteSpace(model.ClassModifier))
        {
            return;
        }

        var normalized = NormalizeModifier(model.ClassModifier!);
        if (normalized is null)
        {
            context.ReportDiagnostic(
                CreateDiagnostic(
                    "XNAME032",
                    "Invalid x:ClassModifier value.",
                    $"x:ClassModifier '{model.ClassModifier}' is not supported.",
                    DiagnosticSeverity.Error,
                    CreateLocation(model.FilePath, model.SourceText, model.ClassModifierLineNumber, model.ClassModifierLinePosition)));
            return;
        }

        var ownerAccessibility = ownerType.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => null
        };

        if (ownerAccessibility is null ||
            !string.Equals(ownerAccessibility, normalized, StringComparison.Ordinal))
        {
            context.ReportDiagnostic(
                CreateDiagnostic(
                    "XNAME033",
                    "x:ClassModifier does not match owner type accessibility.",
                    $"x:ClassModifier '{normalized}' does not match '{ownerType.ToDisplayString()}' accessibility '{ownerAccessibility ?? "unknown"}'.",
                    DiagnosticSeverity.Warning,
                    CreateLocation(model.FilePath, model.SourceText, model.ClassModifierLineNumber, model.ClassModifierLinePosition)));
        }
    }

    private static void ValidateCompiledRootCompatibility(
        SourceProductionContext context,
        Compilation compilation,
        INamedTypeSymbol ownerType,
        ViewModel model)
    {
        var userControlType = compilation.GetTypeByMetadataName("InkkSlinger.UserControl");
        if (userControlType is null)
        {
            return;
        }

        var ownerIsUserControl = IsAssignableTo(ownerType, userControlType);
        if (ownerIsUserControl)
        {
            return;
        }

        context.ReportDiagnostic(
            CreateDiagnostic(
                "XNAME036",
                "Compiled views require UserControl-backed owner types.",
                $"Type '{ownerType.ToDisplayString()}' is not assignable to '{userControlType.ToDisplayString()}'. Generated InitializeComponent requires a UserControl-backed view.",
                DiagnosticSeverity.Error,
                CreateLocation(model.FilePath, model.SourceText, model.RootLineNumber, model.RootLinePosition)));
    }

    private static void ValidateSubclass(SourceProductionContext context, ViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Subclass))
        {
            return;
        }

        context.ReportDiagnostic(
            CreateDiagnostic(
                "XNAME034",
                "x:Subclass is not supported.",
                $"x:Subclass '{model.Subclass}' is currently not supported by the compile-time XAML pipeline.",
                DiagnosticSeverity.Warning,
                CreateLocation(model.FilePath, model.SourceText, model.SubclassLineNumber, model.SubclassLinePosition)));
    }

    private static string? ResolveFieldModifier(
        SourceProductionContext context,
        ViewModel model,
        NamedElementModel element)
    {
        if (string.IsNullOrWhiteSpace(element.FieldModifier))
        {
            return "private";
        }

        var normalized = NormalizeModifier(element.FieldModifier!);
        if (normalized is not null)
        {
            return normalized;
        }

        context.ReportDiagnostic(
            CreateDiagnostic(
                "XNAME035",
                "Invalid x:FieldModifier value.",
                $"x:FieldModifier '{element.FieldModifier}' is not supported for x:Name '{element.Name}'.",
                DiagnosticSeverity.Error,
                CreateLocation(model.FilePath, model.SourceText, element.FieldModifierLineNumber, element.FieldModifierLinePosition)));
        return null;
    }

    private static string? NormalizeModifier(string rawModifier)
    {
        var normalized = string.Join(" ", rawModifier.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        return normalized switch
        {
            "public" => "public",
            "internal" => "internal",
            "private" => "private",
            "protected" => "protected",
            "protected internal" => "protected internal",
            "private protected" => "private protected",
            _ => null
        };
    }

    private static void ValidateEventHandlers(
        SourceProductionContext context,
        Compilation compilation,
        INamedTypeSymbol ownerType,
        ViewModel model)
    {
        foreach (var handler in model.EventHandlers)
        {
            var elementType = ResolveControlType(compilation, handler.ElementTag).Symbol;
            if (elementType is null)
            {
                continue;
            }

            var eventSymbol = FindEventSymbol(elementType, handler.EventName);
            if (eventSymbol is null)
            {
                if (HasSettableProperty(elementType, handler.EventName))
                {
                    continue;
                }

                context.ReportDiagnostic(
                    CreateDiagnostic(
                        "XNAME042",
                        "Unknown event-like attribute.",
                        $"Attribute '{handler.EventName}' on '{handler.ElementTag}' does not resolve to an event or settable property.",
                        DiagnosticSeverity.Error,
                        CreateLocation(model.FilePath, model.SourceText, handler.LineNumber, handler.LinePosition)));
                continue;
            }

            var method = FindHandlerMethod(ownerType, handler.HandlerName);
            if (method is null)
            {
                context.ReportDiagnostic(
                    CreateDiagnostic(
                        "XNAME040",
                        "Event handler method was not found.",
                        $"Handler '{handler.HandlerName}' for event '{handler.EventName}' was not found on '{ownerType.ToDisplayString()}'.",
                        DiagnosticSeverity.Error,
                        CreateLocation(model.FilePath, model.SourceText, handler.LineNumber, handler.LinePosition)));
                continue;
            }

            if (!IsCompatibleHandlerSignature(compilation, method, eventSymbol))
            {
                context.ReportDiagnostic(
                    CreateDiagnostic(
                        "XNAME041",
                        "Event handler signature is not compatible.",
                        $"Handler '{method.Name}' is not compatible with event '{eventSymbol.Name}'.",
                        DiagnosticSeverity.Error,
                        CreateLocation(model.FilePath, model.SourceText, handler.LineNumber, handler.LinePosition)));
            }
        }
    }

    private static IMethodSymbol? FindHandlerMethod(INamedTypeSymbol ownerType, string handlerName)
    {
        foreach (var method in ownerType.GetMembers(handlerName).OfType<IMethodSymbol>())
        {
            if (method.MethodKind == MethodKind.Ordinary && !method.IsStatic)
            {
                return method;
            }
        }

        var baseType = ownerType.BaseType;
        while (baseType is not null)
        {
            foreach (var method in baseType.GetMembers(handlerName).OfType<IMethodSymbol>())
            {
                if (method.MethodKind == MethodKind.Ordinary && !method.IsStatic)
                {
                    return method;
                }
            }

            baseType = baseType.BaseType;
        }

        return null;
    }

    private static IEventSymbol? FindEventSymbol(INamedTypeSymbol typeSymbol, string eventName)
    {
        var current = typeSymbol;
        while (current is not null)
        {
            foreach (var member in current.GetMembers(eventName))
            {
                if (member is IEventSymbol eventSymbol)
                {
                    return eventSymbol;
                }
            }

            current = current.BaseType;
        }

        return null;
    }

    private static bool HasSettableProperty(INamedTypeSymbol typeSymbol, string propertyName)
    {
        var current = typeSymbol;
        while (current is not null)
        {
            foreach (var member in current.GetMembers(propertyName))
            {
                if (member is IPropertySymbol property &&
                    property.SetMethod is not null)
                {
                    return true;
                }
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool IsCompatibleHandlerSignature(
        Compilation compilation,
        IMethodSymbol method,
        IEventSymbol eventSymbol)
    {
        if (eventSymbol.Type is not INamedTypeSymbol delegateType)
        {
            return true;
        }

        var invoke = delegateType.DelegateInvokeMethod;
        if (invoke is null)
        {
            return true;
        }

        if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, invoke.ReturnType))
        {
            return false;
        }

        if (method.Parameters.Length != invoke.Parameters.Length)
        {
            return false;
        }

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var delegateParameterType = invoke.Parameters[i].Type;
            var methodParameterType = method.Parameters[i].Type;
            var conversion = compilation.ClassifyConversion(delegateParameterType, methodParameterType);
            if (!conversion.Exists || !conversion.IsImplicit)
            {
                return false;
            }
        }

        return true;
    }

    private static void EmitLegacyLoadWarning(SourceProductionContext context, INamedTypeSymbol ownerType)
    {
        foreach (var syntaxReference in ownerType.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax declaration)
            {
                continue;
            }

            var sourceText = declaration.SyntaxTree.GetText().ToString();
            if (sourceText.Contains("XamlLoader.LoadInto(", StringComparison.Ordinal) &&
                !sourceText.Contains("InitializeComponent(", StringComparison.Ordinal))
            {
                context.ReportDiagnostic(
                    CreateDiagnostic(
                        "XNAME060",
                        "Legacy XAML load pattern detected.",
                        $"Type '{ownerType.ToDisplayString()}' still calls XamlLoader.LoadInto(...). Prefer generated InitializeComponent().",
                        DiagnosticSeverity.Warning,
                        declaration.Identifier.GetLocation()));
                break;
            }
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
            var classModifierAttribute = root.Attribute(XamlNamespace + "ClassModifier");
            var subclassAttribute = root.Attribute(XamlNamespace + "Subclass");

            var ownerLineInfo = ownerClassAttribute as IXmlLineInfo;
            var ownerLine = ownerLineInfo?.HasLineInfo() == true ? ownerLineInfo.LineNumber : 1;
            var ownerPosition = ownerLineInfo?.HasLineInfo() == true ? ownerLineInfo.LinePosition : 1;

            var namedElements = new List<NamedElementModel>();
            var eventHandlers = new List<EventHandlerBinding>();
            foreach (var element in root.DescendantsAndSelf())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var nameAttribute = element.Attribute(XamlNamespace + "Name");
                if (nameAttribute is not null)
                {
                    var elementName = nameAttribute.Value.Trim();
                    if (!string.IsNullOrWhiteSpace(elementName))
                    {
                        var lineInfo = (IXmlLineInfo)nameAttribute;
                        var fieldModifierAttribute = element.Attribute(XamlNamespace + "FieldModifier");
                        var fieldModifierLineInfo = fieldModifierAttribute as IXmlLineInfo;
                        namedElements.Add(
                            new NamedElementModel(
                                elementName,
                                element.Name.LocalName,
                                fieldModifierAttribute?.Value?.Trim(),
                                lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1,
                                lineInfo.HasLineInfo() ? lineInfo.LinePosition : 1,
                                fieldModifierLineInfo?.HasLineInfo() == true ? fieldModifierLineInfo.LineNumber : 1,
                                fieldModifierLineInfo?.HasLineInfo() == true ? fieldModifierLineInfo.LinePosition : 1));
                    }
                }

                foreach (var attribute in element.Attributes())
                {
                    if (attribute.IsNamespaceDeclaration || !string.IsNullOrEmpty(attribute.Name.NamespaceName))
                    {
                        continue;
                    }

                    var localName = attribute.Name.LocalName;
                    if (localName.IndexOf('.') >= 0 ||
                        string.Equals(localName, "Name", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var value = attribute.Value.Trim();
                    if (string.IsNullOrWhiteSpace(value) || value.StartsWith("{", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var attributeLineInfo = attribute as IXmlLineInfo;
                    eventHandlers.Add(
                        new EventHandlerBinding(
                            element.Name.LocalName,
                            localName,
                            value,
                            attributeLineInfo?.HasLineInfo() == true ? attributeLineInfo.LineNumber : 1,
                            attributeLineInfo?.HasLineInfo() == true ? attributeLineInfo.LinePosition : 1));
                }
            }

            var classModifierLineInfo = classModifierAttribute as IXmlLineInfo;
            var subclassLineInfo = subclassAttribute as IXmlLineInfo;
            var rootLineInfo = (IXmlLineInfo)root;

            return ViewParseResult.WithModel(
                new ViewModel(
                    file.Path,
                    text,
                    ownerClass,
                    ownerLine,
                    ownerPosition,
                    root.Name.LocalName,
                    rootLineInfo.HasLineInfo() ? rootLineInfo.LineNumber : 1,
                    rootLineInfo.HasLineInfo() ? rootLineInfo.LinePosition : 1,
                    classModifierAttribute?.Value?.Trim(),
                    classModifierLineInfo?.HasLineInfo() == true ? classModifierLineInfo.LineNumber : 1,
                    classModifierLineInfo?.HasLineInfo() == true ? classModifierLineInfo.LinePosition : 1,
                    subclassAttribute?.Value?.Trim(),
                    subclassLineInfo?.HasLineInfo() == true ? subclassLineInfo.LineNumber : 1,
                    subclassLineInfo?.HasLineInfo() == true ? subclassLineInfo.LinePosition : 1,
                    namedElements.ToImmutableArray(),
                    eventHandlers.ToImmutableArray()),
                ImmutableArray<Diagnostic>.Empty);
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
            string? ownerClass,
            int ownerLineNumber,
            int ownerLinePosition,
            string rootTagName,
            int rootLineNumber,
            int rootLinePosition,
            string? classModifier,
            int classModifierLineNumber,
            int classModifierLinePosition,
            string? subclass,
            int subclassLineNumber,
            int subclassLinePosition,
            ImmutableArray<NamedElementModel> namedElements,
            ImmutableArray<EventHandlerBinding> eventHandlers)
        {
            FilePath = filePath;
            SourceText = sourceText;
            OwnerClass = ownerClass;
            OwnerLineNumber = ownerLineNumber;
            OwnerLinePosition = ownerLinePosition;
            RootTagName = rootTagName;
            RootLineNumber = rootLineNumber;
            RootLinePosition = rootLinePosition;
            ClassModifier = classModifier;
            ClassModifierLineNumber = classModifierLineNumber;
            ClassModifierLinePosition = classModifierLinePosition;
            Subclass = subclass;
            SubclassLineNumber = subclassLineNumber;
            SubclassLinePosition = subclassLinePosition;
            NamedElements = namedElements;
            EventHandlers = eventHandlers;
        }

        public string FilePath { get; }

        public SourceText SourceText { get; }

        public string? OwnerClass { get; }

        public int OwnerLineNumber { get; }

        public int OwnerLinePosition { get; }

        public string RootTagName { get; }

        public int RootLineNumber { get; }

        public int RootLinePosition { get; }

        public string? ClassModifier { get; }

        public int ClassModifierLineNumber { get; }

        public int ClassModifierLinePosition { get; }

        public string? Subclass { get; }

        public int SubclassLineNumber { get; }

        public int SubclassLinePosition { get; }

        public ImmutableArray<NamedElementModel> NamedElements { get; }

        public ImmutableArray<EventHandlerBinding> EventHandlers { get; }
    }

    private readonly struct NamedElementModel
    {
        public NamedElementModel(
            string name,
            string tagName,
            string? fieldModifier,
            int lineNumber,
            int linePosition,
            int fieldModifierLineNumber,
            int fieldModifierLinePosition)
        {
            Name = name;
            TagName = tagName;
            FieldModifier = fieldModifier;
            LineNumber = lineNumber;
            LinePosition = linePosition;
            FieldModifierLineNumber = fieldModifierLineNumber;
            FieldModifierLinePosition = fieldModifierLinePosition;
        }

        public string Name { get; }

        public string TagName { get; }

        public string? FieldModifier { get; }

        public int LineNumber { get; }

        public int LinePosition { get; }

        public int FieldModifierLineNumber { get; }

        public int FieldModifierLinePosition { get; }
    }

    private readonly struct EventHandlerBinding
    {
        public EventHandlerBinding(string elementTag, string eventName, string handlerName, int lineNumber, int linePosition)
        {
            ElementTag = elementTag;
            EventName = eventName;
            HandlerName = handlerName;
            LineNumber = lineNumber;
            LinePosition = linePosition;
        }

        public string ElementTag { get; }

        public string EventName { get; }

        public string HandlerName { get; }

        public int LineNumber { get; }

        public int LinePosition { get; }
    }

    private readonly struct GeneratedMember
    {
        public GeneratedMember(string name, string typeDisplayName, string modifier)
        {
            Name = name;
            TypeDisplayName = typeDisplayName;
            Modifier = modifier;
        }

        public string Name { get; }

        public string TypeDisplayName { get; }

        public string Modifier { get; }
    }

    private readonly struct ResolvedType
    {
        public ResolvedType(INamedTypeSymbol? symbol, string typeDisplayName)
        {
            Symbol = symbol;
            TypeDisplayName = typeDisplayName;
        }

        public INamedTypeSymbol? Symbol { get; }

        public string TypeDisplayName { get; }
    }
}
