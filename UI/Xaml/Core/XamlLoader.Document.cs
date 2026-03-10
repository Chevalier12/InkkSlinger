using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace InkkSlinger;

public static partial class XamlLoader
{
    public static UIElement LoadFromFile(string path, object? codeBehind = null)
    {
        var xaml = File.ReadAllText(path);
        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
        UIElement root = null!;
        RunWithinXamlBaseDirectory(baseDirectory, () =>
        {
            root = LoadFromString(xaml, codeBehind);
        });
        return root;
    }

    public static UIElement LoadFromString(string xaml, object? codeBehind = null)
    {
        var document = ParseDocument(xaml, "Failed to parse XAML document.");
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

    public static void LoadInto(UserControl target, string path, object? codeBehind = null)
    {
        EnsureDefaultApplicationResourcesInScope(target);

        var xaml = File.ReadAllText(path);
        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
        RunWithinXamlBaseDirectory(baseDirectory, () =>
        {
            LoadIntoFromString(target, xaml, codeBehind);
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
        var xaml = File.ReadAllText(path);
        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
        RunWithinXamlBaseDirectory(baseDirectory, () =>
        {
            LoadApplicationResourcesFromString(xaml, clearExisting);
        });
    }

    public static void LoadApplicationResourcesFromString(string xaml, bool clearExisting = false)
    {
        var document = ParseDocument(xaml, "Failed to parse application XML document.");
        if (document.Root == null)
        {
            throw CreateXamlException("Application XML document has no root element.", document);
        }

        var loadedResources = ParseApplicationResourcesDocument(document.Root);
        var appResources = UiApplication.Current.Resources;
        if (clearExisting)
        {
            appResources.Clear();
            foreach (var merged in appResources.MergedDictionaries.ToList())
            {
                appResources.RemoveMergedDictionary(merged);
            }
        }

        MergeResourceDictionaryContents(appResources, loadedResources);
    }

    public static void LoadIntoFromString(UserControl target, string xaml, object? codeBehind = null)
    {
        var document = ParseDocument(xaml, "Failed to parse XAML document.");
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

    private static XDocument ParseDocument(string xaml, string failureMessage)
    {
        try
        {
            return XDocument.Parse(xaml, LoadOptions.SetLineInfo);
        }
        catch (Exception ex)
        {
            throw CreateXamlException(failureMessage, null, ex);
        }
    }
}
