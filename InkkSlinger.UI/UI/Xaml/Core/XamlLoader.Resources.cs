using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace InkkSlinger;

public static partial class XamlLoader
{
    private static ResourceDictionary ParseApplicationResourcesDocument(XElement rootElement)
    {
        if (string.Equals(rootElement.Name.LocalName, nameof(ResourceDictionary), StringComparison.Ordinal))
        {
            return BuildObject(rootElement, codeBehind: null, resourceScope: null) as ResourceDictionary
                ?? throw CreateXamlException("Failed to parse ResourceDictionary root.", rootElement);
        }

        if (!string.Equals(rootElement.Name.LocalName, "Application", StringComparison.Ordinal))
        {
            throw CreateXamlException(
                $"Application XML root must be 'Application' or '{nameof(ResourceDictionary)}'.",
                rootElement);
        }

        XElement? resourcesProperty = null;
        foreach (var child in rootElement.Elements())
        {
            if (!string.Equals(child.Name.LocalName, "Application.Resources", StringComparison.Ordinal))
            {
                continue;
            }

            if (resourcesProperty != null)
            {
                throw CreateXamlException("Application XML can contain only one Application.Resources element.", rootElement);
            }

            resourcesProperty = child;
        }

        if (resourcesProperty == null)
        {
            return new ResourceDictionary();
        }

        var resources = new ResourceDictionary();
        foreach (var resourceElement in resourcesProperty.Elements())
        {
            if (string.Equals(resourceElement.Name.LocalName, nameof(ResourceDictionary), StringComparison.Ordinal) &&
                !HasExplicitXamlKey(resourceElement))
            {
                var nestedDictionary = BuildObject(resourceElement, codeBehind: null, resourceScope: null) as ResourceDictionary;
                if (nestedDictionary == null)
                {
                    throw CreateXamlException(
                        "Application.Resources dictionary element must resolve to ResourceDictionary.",
                        resourceElement);
                }

                MergeResourceDictionaryContents(resources, nestedDictionary);
                continue;
            }

            AddResourceEntry(resources, resourceElement, codeBehind: null, resourceScope: null);
        }

        return resources;
    }

    private static void ApplyAttributes(object target, XElement element, object? codeBehind, FrameworkElement? resourceScope = null)
    {
        ValidateStrictRichTextAttributes(target, element);

        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            var namespaceName = attribute.Name.NamespaceName;
            var localName = attribute.Name.LocalName;
            var value = attribute.Value;

            if (namespaceName == XamlNamespace.NamespaceName)
            {
                if (localName == "Name")
                {
                    AssignName(target, value, codeBehind);
                    continue;
                }

                if (localName == "Key")
                {
                    continue;
                }

                if (localName == "Class" ||
                    localName == "ClassModifier" ||
                    localName == "FieldModifier" ||
                    localName == "Subclass")
                {
                    continue;
                }

                throw CreateXamlException(
                    $"x:{localName} metadata is not supported by this runtime loader.",
                    attribute,
                    code: XamlDiagnosticCode.UnsupportedConstruct);
            }

            // Ignore foreign-namespace metadata attributes like xsi:schemaLocation.
            if (!string.IsNullOrEmpty(namespaceName))
            {
                continue;
            }

            try
            {
                if (localName.Contains('.', StringComparison.Ordinal))
                {
                    ApplyAttachedProperty(target, localName, value, resourceScope, attribute);
                    continue;
                }

                if (string.Equals(localName, nameof(FrameworkElement.Name), StringComparison.Ordinal) &&
                    target is FrameworkElement)
                {
                    AssignName(target, value, codeBehind);
                    continue;
                }

                if (TryApplyBindingExpression(target, localName, value, resourceScope))
                {
                    continue;
                }

                if (target is ResourceDictionary dictionary &&
                    string.Equals(localName, "Source", StringComparison.Ordinal))
                {
                    var loadedDictionary = LoadResourceDictionaryFromSource(value, codeBehind, resourceScope, attribute);
                    dictionary.AddMergedDictionary(loadedDictionary);
                    continue;
                }

                if (TryApplyDynamicResourceExpression(target, localName, value))
                {
                    continue;
                }

                if (TryQueueEventHandlerAttachment(target, localName, value, codeBehind, element, attribute))
                {
                    continue;
                }

                ApplyProperty(target, localName, value, resourceScope, attribute);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
            {
                var diagnosticCode = ex switch
                {
                    FormatException => XamlDiagnosticCode.InvalidValue,
                    ArgumentException => XamlDiagnosticCode.InvalidValue,
                    _ when ex.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase) =>
                        XamlDiagnosticCode.UnsupportedConstruct,
                    _ when ex.Message.Contains("was not found on type", StringComparison.OrdinalIgnoreCase) =>
                        XamlDiagnosticCode.UnknownProperty,
                    _ => XamlDiagnosticCode.GeneralFailure
                };

                var hint = diagnosticCode switch
                {
                    XamlDiagnosticCode.UnknownProperty =>
                        $"Verify that '{attribute.Name.LocalName}' is a valid property on '{element.Name.LocalName}'.",
                    XamlDiagnosticCode.InvalidValue =>
                        $"Check the value assigned to '{attribute.Name.LocalName}' and ensure it can be converted.",
                    XamlDiagnosticCode.UnsupportedConstruct =>
                        $"'{attribute.Name.LocalName}' uses a construct that is not supported by this loader.",
                    _ => null
                };

                throw CreateXamlException(
                    $"Failed to apply attribute '{attribute.Name.LocalName}' on '{element.Name.LocalName}': {ex.Message}",
                    attribute,
                    ex,
                    diagnosticCode,
                    propertyName: attribute.Name.LocalName,
                    hint: hint,
                    elementName: element.Name.LocalName);
            }
        }
    }


    private static void AddResourceEntry(
        ResourceDictionary dictionary,
        XElement resourceElement,
        object? codeBehind,
        FrameworkElement? resourceScope)
    {
        object resourceValue = null!;
        RunWithinResourceBuildContext(dictionary, resourceScope, () =>
        {
            resourceValue = BuildObject(resourceElement, codeBehind, resourceScope);
        });
        var resourceKey = GetResourceKey(resourceElement, resourceValue);
        dictionary[resourceKey] = resourceValue;
    }

    private static bool HasExplicitXamlKey(XElement element)
    {
        return element.Attributes().Any(attribute =>
            !attribute.IsNamespaceDeclaration &&
            attribute.Name.Namespace == XamlNamespace &&
            string.Equals(attribute.Name.LocalName, "Key", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(attribute.Value));
    }


    private static void MergeResourceDictionaryContents(ResourceDictionary target, ResourceDictionary source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        var mergedEntries = new Dictionary<object, object>();
        foreach (var pair in target)
        {
            mergedEntries[pair.Key] = pair.Value;
        }

        foreach (var pair in source)
        {
            mergedEntries[pair.Key] = pair.Value;
        }

        var mergedDictionaries = new List<ResourceDictionary>(target.MergedDictionaries.Count + source.MergedDictionaries.Count);
        foreach (var mergedDictionary in target.MergedDictionaries)
        {
            mergedDictionaries.Add(mergedDictionary);
        }

        foreach (var mergedDictionary in source.MergedDictionaries)
        {
            mergedDictionaries.Add(mergedDictionary);
        }

        target.ReplaceContents(mergedEntries, mergedDictionaries);
    }


    private static object GetResourceKey(XElement resourceElement, object resourceValue)
    {
        var keyAttribute = resourceElement.Attributes()
            .FirstOrDefault(attribute =>
                !attribute.IsNamespaceDeclaration &&
                attribute.Name.Namespace == XamlNamespace &&
                string.Equals(attribute.Name.LocalName, "Key", StringComparison.Ordinal));

        if (keyAttribute != null && !string.IsNullOrWhiteSpace(keyAttribute.Value))
        {
            return keyAttribute.Value;
        }

        if (resourceValue is Style style)
        {
            return style.TargetType;
        }

        if (resourceValue is DataTemplate dataTemplate && dataTemplate.DataType != null)
        {
            return dataTemplate.DataType;
        }

        throw new InvalidOperationException(
            $"Resource element '{resourceElement.Name.LocalName}' requires x:Key.");
    }


    private static bool TryParseStaticResourceKey(string valueText, out string key)
    {
        key = string.Empty;
        if (!TryParseMarkupExtensionExpression(valueText, out var expression) ||
            !string.Equals(expression.Name, "StaticResource", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var keyText = expression.Body;
        if (keyText.StartsWith("Key=", StringComparison.OrdinalIgnoreCase))
        {
            keyText = keyText["Key=".Length..];
        }

        keyText = keyText.Trim();
        if (string.IsNullOrWhiteSpace(keyText))
        {
            throw CreateXamlException("StaticResource markup requires a resource key.");
        }

        key = keyText;
        return true;
    }


    private static bool TryParseDynamicResourceKey(string valueText, out string key)
    {
        key = string.Empty;
        if (!TryParseMarkupExtensionExpression(valueText, out var expression) ||
            !string.Equals(expression.Name, "DynamicResource", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var keyText = expression.Body;
        if (keyText.StartsWith("ResourceKey=", StringComparison.OrdinalIgnoreCase))
        {
            keyText = keyText["ResourceKey=".Length..];
        }

        keyText = keyText.Trim();
        if (string.IsNullOrWhiteSpace(keyText))
        {
            throw CreateXamlException("DynamicResource markup requires a resource key.");
        }

        key = keyText;
        return true;
    }

    private static object ResolveStaticResourceValue(string key, FrameworkElement? primaryScope, FrameworkElement? fallbackScope = null)
    {
        if (primaryScope != null &&
            ResourceResolver.TryFindResource(primaryScope, key, out var primaryResource, includeApplicationResources: false))
        {
            return primaryResource!;
        }

        if (fallbackScope != null &&
            !ReferenceEquals(fallbackScope, primaryScope) &&
            ResourceResolver.TryFindResource(fallbackScope, key, out var fallbackResource, includeApplicationResources: false))
        {
            return fallbackResource!;
        }

        if (TryFindResourceInBuildContexts(key, out var resourceBuildValue))
        {
            return resourceBuildValue!;
        }

        if (TryFindResourceInConstructionScopes(key, primaryScope, fallbackScope, out var scopedResource))
        {
            return scopedResource!;
        }

        if (CurrentLoadRootScope != null &&
            !ReferenceEquals(CurrentLoadRootScope, primaryScope) &&
            !ReferenceEquals(CurrentLoadRootScope, fallbackScope) &&
            ResourceResolver.TryFindResource(CurrentLoadRootScope, key, out var rootResource, includeApplicationResources: false))
        {
            return rootResource!;
        }

        if (UiApplication.Current.Resources.TryGetValue(key, out var applicationResource))
        {
            return applicationResource;
        }

        throw CreateXamlException($"StaticResource key '{key}' was not found.");
    }


    private static void EnsureDefaultApplicationResourcesInScope(FrameworkElement target)
    {
        var defaults = GetDefaultApplicationResources();
        if (defaults == null)
        {
            return;
        }

        MergeMissingResourceEntries(target.Resources, defaults);
    }


    private static ResourceDictionary? GetDefaultApplicationResources()
    {
        lock (DefaultApplicationResourceCacheLock)
        {
            if (HasAttemptedDefaultApplicationResourceLoad)
            {
                return DefaultApplicationResourceCache;
            }

            HasAttemptedDefaultApplicationResourceLoad = true;

            var appMarkupPath = Path.Combine(AppContext.BaseDirectory, "App.xml");
            if (!File.Exists(appMarkupPath))
            {
                return null;
            }

            var document = ParseDocumentFile(appMarkupPath, "Failed to parse default App.xml document.");
            if (document.Root == null)
            {
                throw CreateXamlException("Default App.xml document has no root element.", document);
            }

            var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(appMarkupPath));
            RunWithinXamlBaseDirectory(baseDirectory, () =>
            {
                DefaultApplicationResourceCache = ParseApplicationResourcesDocument(document.Root);
            });

            return DefaultApplicationResourceCache;
        }
    }


    private static void MergeMissingResourceEntries(ResourceDictionary target, ResourceDictionary source)
    {
        foreach (var pair in source)
        {
            if (!target.ContainsKey(pair.Key))
            {
                target[pair.Key] = pair.Value;
            }
        }

        foreach (var merged in source.MergedDictionaries)
        {
            if (!ContainsMergedDictionaryReference(target, merged))
            {
                target.AddMergedDictionary(merged);
            }
        }
    }


    private static bool ContainsMergedDictionaryReference(ResourceDictionary dictionary, ResourceDictionary target)
    {
        var visited = new HashSet<ResourceDictionary>();
        return ContainsMergedDictionaryReferenceCore(dictionary, target, visited);
    }


    private static bool ContainsMergedDictionaryReferenceCore(
        ResourceDictionary dictionary,
        ResourceDictionary target,
        HashSet<ResourceDictionary> visited)
    {
        if (!visited.Add(dictionary))
        {
            return false;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (ReferenceEquals(merged, target))
            {
                return true;
            }

            if (ContainsMergedDictionaryReferenceCore(merged, target, visited))
            {
                return true;
            }
        }

        return false;
    }


    private static ResourceDictionary LoadResourceDictionaryFromSource(
        string source,
        object? codeBehind,
        FrameworkElement? resourceScope,
        XObject? location)
    {
        var resolvedPath = ResolveXamlSourcePath(source, location);
        if (HasActiveResourceDictionarySourcePath(resolvedPath))
        {
            throw CreateXamlException(
                $"ResourceDictionary Source cycle detected for '{resolvedPath}'.",
                location);
        }

        if (!File.Exists(resolvedPath))
        {
            throw CreateXamlException($"ResourceDictionary source '{source}' was not found at '{resolvedPath}'.", location);
        }

        return RunWithinResourceDictionarySourcePath(resolvedPath, () =>
        {
            var document = ParseDocumentFile(resolvedPath, $"Failed to parse ResourceDictionary source '{resolvedPath}'.");
            var root = document.Root;
            if (root == null ||
                !string.Equals(root.Name.LocalName, nameof(ResourceDictionary), StringComparison.Ordinal))
            {
                throw CreateXamlException(
                    $"ResourceDictionary source '{resolvedPath}' must have a ResourceDictionary root element.",
                    location);
            }

            ResourceDictionary dictionary = null!;
            var baseDirectory = Path.GetDirectoryName(resolvedPath);
            RunWithinXamlBaseDirectory(baseDirectory, () =>
            {
                RunWithinResourceBuildContext(new ResourceDictionary(), resourceScope, () =>
                {
                    dictionary = (ResourceDictionary)BuildObject(root, codeBehind, resourceScope);
                });
            });
            return dictionary;
        });
    }


    private static string ResolveXamlSourcePath(string source, XObject? location)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw CreateXamlException("ResourceDictionary Source cannot be empty.", location);
        }

        var trimmed = source.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri) && absoluteUri.IsFile)
        {
            return Path.GetFullPath(absoluteUri.LocalPath);
        }

        if (Path.IsPathRooted(trimmed))
        {
            return Path.GetFullPath(trimmed);
        }

        if (CurrentXamlBaseDirectories == null || CurrentXamlBaseDirectories.Count == 0)
        {
            throw CreateXamlException(
                $"Relative ResourceDictionary Source '{trimmed}' requires loading from a file-based XAML context.",
                location);
        }

        return Path.GetFullPath(Path.Combine(CurrentXamlBaseDirectories.Peek(), trimmed));
    }


    private static void RunWithinXamlBaseDirectory(string? directory, Action action)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            action();
            return;
        }

        CurrentXamlBaseDirectories ??= new Stack<string>();
        var normalized = Path.GetFullPath(directory);
        CurrentXamlBaseDirectories.Push(normalized);
        try
        {
            action();
        }
        finally
        {
            _ = CurrentXamlBaseDirectories.Pop();
        }
    }


    private static void RunWithinConstructionScope(FrameworkElement? scope, Action action)
    {
        if (scope == null)
        {
            action();
            return;
        }

        CurrentConstructionScopes ??= new Stack<FrameworkElement>();
        if (CurrentConstructionScopes.Count == 0)
        {
            CurrentConstructionRootScope = scope;
        }

        CurrentConstructionScopes.Push(scope);
        try
        {
            action();
        }
        finally
        {
            CurrentConstructionScopes.Pop();
            if (CurrentConstructionScopes.Count == 0)
            {
                CurrentConstructionRootScope = null;
            }
        }
    }


    private static bool TryFindResourceInConstructionScopes(
        object key,
        FrameworkElement? primaryScope,
        FrameworkElement? fallbackScope,
        out object? resource)
    {
        if (CurrentConstructionScopes == null || CurrentConstructionScopes.Count == 0)
        {
            resource = null;
            return false;
        }

        foreach (var scope in CurrentConstructionScopes)
        {
            if (ReferenceEquals(scope, primaryScope) || ReferenceEquals(scope, fallbackScope))
            {
                continue;
            }

            if (scope.Resources.TryGetValue(key, out var value))
            {
                resource = value;
                return true;
            }
        }

        resource = null;
        return false;
    }


    private static bool TryFindResourceInBuildContexts(object key, out object? resource)
    {
        if (CurrentResourceBuildContexts == null || CurrentResourceBuildContexts.Count == 0)
        {
            resource = null;
            return false;
        }

        foreach (var context in CurrentResourceBuildContexts)
        {
            if (context.TryResolve(key, out resource))
            {
                return true;
            }
        }

        resource = null;
        return false;
    }


    private static void RunWithinResourceBuildContext(
        ResourceDictionary dictionary,
        FrameworkElement? parentScope,
        Action action)
    {
        CurrentResourceBuildContexts ??= new Stack<XamlResourceBuildContext>();
        CurrentResourceBuildContexts.Push(new XamlResourceBuildContext(dictionary, parentScope));
        try
        {
            action();
        }
        finally
        {
            CurrentResourceBuildContexts.Pop();
            if (CurrentResourceBuildContexts.Count == 0)
            {
                CurrentResourceBuildContexts = null;
            }
        }
    }


    private static object CoerceResolvedResourceValue(object resolved, Type targetType, string location)
    {
        if (targetType == typeof(object))
        {
            return resolved;
        }

        if (resolved != null && targetType.IsInstanceOfType(resolved))
        {
            return resolved;
        }

        if (resolved is string text)
        {
            return ConvertValue(text, targetType);
        }

        if (DependencyValueCoercion.TryCoerce(resolved, targetType, out var coerced))
        {
            return coerced!;
        }

        throw new InvalidOperationException(
            $"Resource value for '{location}' is of type '{resolved?.GetType().Name ?? "null"}' and cannot be assigned to '{targetType.Name}'.");
    }


}
