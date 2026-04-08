using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public static partial class XamlLoader
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly Assembly UiAssembly = typeof(UIElement).Assembly;
    internal static readonly IReadOnlyDictionary<string, Type> TypeByName = BuildTypeMap();
    private static readonly object DefaultApplicationResourceCacheLock = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, object>> EnumValueCache = new();
    private static readonly ConcurrentDictionary<string, CachedStringDocument> StringDocumentCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, CachedFileDocument> FileDocumentCache = new(StringComparer.OrdinalIgnoreCase);
    private static int StringDocumentCacheMissCount;
    private static int FileDocumentCacheMissCount;
    private static ResourceDictionary? DefaultApplicationResourceCache;
    private static bool HasAttemptedDefaultApplicationResourceLoad;
    private static readonly IReadOnlyDictionary<string, Color> NamedColors = BuildNamedColorMap();

    private sealed class CachedStringDocument
    {
        public CachedStringDocument(XDocument document)
        {
            Document = document;
        }

        public XDocument Document { get; }
    }

    private sealed class CachedFileDocument
    {
        public CachedFileDocument(string path, DateTime lastWriteUtc, long length, XDocument document)
        {
            Path = path;
            LastWriteUtc = lastWriteUtc;
            Length = length;
            Document = document;
        }

        public string Path { get; }

        public DateTime LastWriteUtc { get; }

        public long Length { get; }

        public XDocument Document { get; }

        public bool Matches(FileInfo fileInfo)
        {
            return string.Equals(Path, fileInfo.FullName, StringComparison.OrdinalIgnoreCase) &&
                   LastWriteUtc == fileInfo.LastWriteTimeUtc &&
                   Length == fileInfo.Length;
        }
    }

}

internal sealed class XamlResourceBuildContext
{
    private readonly ResourceDictionary _dictionary;
    private readonly FrameworkElement? _parentScope;

    public XamlResourceBuildContext(ResourceDictionary dictionary, FrameworkElement? parentScope)
    {
        _dictionary = dictionary;
        _parentScope = parentScope;
    }

    public bool TryResolve(object key, out object? value)
    {
        if (_dictionary.TryGetValue(key, out value))
        {
            return true;
        }

        foreach (var mergedDictionary in _dictionary.MergedDictionaries)
        {
            if (mergedDictionary.TryGetValue(key, out value))
            {
                return true;
            }
        }

        if (_parentScope != null &&
            ResourceResolver.TryFindResource(_parentScope, key, out value, includeApplicationResources: false))
        {
            return true;
        }

        value = null;
        return false;
    }
}
