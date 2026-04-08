using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace InkkSlinger;

public static partial class XamlLoader
{
    public static UIElement LoadFromFile(string path, object? codeBehind = null)
    {
        var fullPath = Path.GetFullPath(path);
        var document = ParseDocumentFile(fullPath, "Failed to parse XAML document.");
        var baseDirectory = Path.GetDirectoryName(fullPath);
        UIElement root = null!;
        RunWithinXamlBaseDirectory(baseDirectory, () =>
        {
            root = LoadFromDocument(document, codeBehind);
        });
        return root;
    }

    public static UIElement LoadFromString(string xaml, object? codeBehind = null)
    {
        var document = ParseDocument(xaml, "Failed to parse XAML document.");
        return LoadFromDocument(document, codeBehind);
    }

    public static void LoadInto(UserControl target, string path, object? codeBehind = null)
    {
        EnsureDefaultApplicationResourcesInScope(target);

        var fullPath = Path.GetFullPath(path);
        var document = ParseDocumentFile(fullPath, "Failed to parse XAML document.");
        var baseDirectory = Path.GetDirectoryName(fullPath);
        RunWithinXamlBaseDirectory(baseDirectory, () =>
        {
            LoadIntoFromDocument(target, document, codeBehind);
        });
    }

    public static void LoadIntoCompiled(UserControl target, string path, object? codeBehind = null, string? ownerTypeName = null)
    {
        try
        {
            LoadInto(target, path, codeBehind);
        }
        catch (InvalidOperationException ex) when (!string.IsNullOrWhiteSpace(ownerTypeName))
        {
            throw new InvalidOperationException(
                $"Failed to initialize compiled view '{ownerTypeName}' from '{path}'. {ex.Message}",
                ex);
        }
    }

    public static void LoadApplicationResourcesFromFile(string path, bool clearExisting = false)
    {
        var fullPath = Path.GetFullPath(path);
        var document = ParseDocumentFile(fullPath, "Failed to parse application XML document.");
        var baseDirectory = Path.GetDirectoryName(fullPath);
        RunWithinXamlBaseDirectory(baseDirectory, () =>
        {
            LoadApplicationResourcesFromDocument(document, clearExisting);
        });
    }

    public static void LoadApplicationResourcesFromString(string xaml, bool clearExisting = false)
    {
        var document = ParseDocument(xaml, "Failed to parse application XML document.");
        LoadApplicationResourcesFromDocument(document, clearExisting);
    }

    public static void LoadIntoFromString(UserControl target, string xaml, object? codeBehind = null)
    {
        var document = ParseDocument(xaml, "Failed to parse XAML document.");
        LoadIntoFromDocument(target, document, codeBehind);
    }

    private static UIElement LoadFromDocument(XDocument document, object? codeBehind)
    {
        if (document.Root == null)
        {
            throw CreateXamlException("XAML document has no root element.", document);
        }

        var rootElement = document.Root;
        var rootType = ResolveElementType(rootElement.Name.LocalName);
        if (XamlObjectFactory.CreateInstance(rootType) is not UIElement uiRoot)
        {
            throw CreateXamlException($"Type '{rootType.Name}' is not a UIElement.", rootElement);
        }

        var previousRootScope = CurrentLoadRootScope;
        try
        {
            var rootScope = uiRoot as FrameworkElement;
            CurrentLoadRootScope = rootScope;
            RunWithinDeferredFinalizeActions(() =>
            {
                RunWithinConstructionScope(rootScope, () =>
                {
                    ApplyAttributes(uiRoot, rootElement, codeBehind, rootScope);
                    ApplyChildren(uiRoot, rootElement, codeBehind, rootScope);
                });
            });
            return uiRoot;
        }
        finally
        {
            CurrentLoadRootScope = previousRootScope;
        }
    }

    private static void LoadApplicationResourcesFromDocument(XDocument document, bool clearExisting)
    {
        if (document.Root == null)
        {
            throw CreateXamlException("Application XML document has no root element.", document);
        }

        var loadedResources = ParseApplicationResourcesDocument(document.Root);
        var appResources = UiApplication.Current.Resources;
        if (clearExisting)
        {
            appResources.ReplaceContents(loadedResources, loadedResources.MergedDictionaries);
            return;
        }

        MergeResourceDictionaryContents(appResources, loadedResources);
    }

    private static void LoadIntoFromDocument(UserControl target, XDocument document, object? codeBehind)
    {
        if (document.Root == null)
        {
            throw CreateXamlException("XAML document has no root element.", document);
        }

        var root = document.Root;
        var rootType = ResolveElementType(root.Name.LocalName);
        if (!typeof(UserControl).IsAssignableFrom(rootType))
        {
            throw CreateXamlException("LoadInto expects a UserControl root element.", root);
        }

        var previousRootScope = CurrentLoadRootScope;
        CurrentLoadRootScope = target;
        try
        {
            RunWithinDeferredFinalizeActions(() =>
            {
                RunWithinConstructionScope(target, () =>
                {
                    ApplyAttributes(target, root, codeBehind, target);
                    target.Content = null;
                    ApplyChildren(target, root, codeBehind, target);
                });
            });
        }
        finally
        {
            CurrentLoadRootScope = previousRootScope;
        }
    }

    private static XDocument ParseDocumentFile(string path, string failureMessage)
    {
        var fullPath = Path.GetFullPath(path);
        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Exists &&
            FileDocumentCache.TryGetValue(fullPath, out var cachedDocument) &&
            cachedDocument.Matches(fileInfo))
        {
            return cachedDocument.Document;
        }

        try
        {
            using var stream = File.OpenRead(fullPath);
            using var reader = XmlReader.Create(
                stream,
                new XmlReaderSettings
                {
                    IgnoreWhitespace = false
                });
            var document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            if (fileInfo.Exists)
            {
                FileDocumentCache[fullPath] = new CachedFileDocument(
                    fullPath,
                    fileInfo.LastWriteTimeUtc,
                    fileInfo.Length,
                    document);
                Interlocked.Increment(ref FileDocumentCacheMissCount);
            }

            return document;
        }
        catch (Exception ex)
        {
            throw CreateXamlException(failureMessage, null, ex);
        }
    }

    private static XDocument ParseDocument(string xaml, string failureMessage)
    {
        if (StringDocumentCache.TryGetValue(xaml, out var cachedDocument))
        {
            return cachedDocument.Document;
        }

        try
        {
            var document = XDocument.Parse(xaml, LoadOptions.SetLineInfo);
            StringDocumentCache[xaml] = new CachedStringDocument(document);
            Interlocked.Increment(ref StringDocumentCacheMissCount);
            return document;
        }
        catch (Exception ex)
        {
            throw CreateXamlException(failureMessage, null, ex);
        }
    }

    internal static (int StringMisses, int FileMisses) GetDocumentCacheMissCounts()
    {
        return (Volatile.Read(ref StringDocumentCacheMissCount), Volatile.Read(ref FileDocumentCacheMissCount));
    }
}
