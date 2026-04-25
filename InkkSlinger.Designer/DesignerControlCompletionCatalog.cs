using System;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger.Designer;

internal static class DesignerControlCompletionCatalog
{
    private static readonly Lazy<DesignerControlCompletionItem[]> AllItems = new(CreateItems);
    private static readonly Dictionary<string, DesignerControlCompletionItem[]> PropertyItemsByOwnerName = CreatePropertyItemsByOwnerName();

    public static IReadOnlyList<DesignerControlCompletionItem> GetItems(string? prefix)
    {
        var normalizedPrefix = prefix ?? string.Empty;
        if (normalizedPrefix.Length == 0)
        {
            return AllItems.Value;
        }

        var matches = new List<DesignerControlCompletionItem>();
        var allItems = AllItems.Value;
        for (var i = 0; i < allItems.Length; i++)
        {
            if (allItems[i].ElementName.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(allItems[i]);
            }
        }

        return matches.Count == 0 ? Array.Empty<DesignerControlCompletionItem>() : matches.ToArray();
    }

    public static IReadOnlyList<DesignerControlCompletionItem> GetAllItems()
    {
        return AllItems.Value;
    }

    public static IReadOnlyList<DesignerControlCompletionItem> GetPropertyElementItems(string? prefix)
    {
        var normalizedPrefix = prefix ?? string.Empty;
        var separatorIndex = normalizedPrefix.IndexOf('.');
        if (separatorIndex <= 0)
        {
            return Array.Empty<DesignerControlCompletionItem>();
        }

        var ownerName = normalizedPrefix[..separatorIndex];
        if (!TryGetPropertyItemsForOwner(ownerName, out var propertyItems))
        {
            return Array.Empty<DesignerControlCompletionItem>();
        }

        if (normalizedPrefix.Length == ownerName.Length + 1)
        {
            return propertyItems;
        }

        var matches = new List<DesignerControlCompletionItem>();
        for (var i = 0; i < propertyItems.Length; i++)
        {
            if (propertyItems[i].ElementName.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(propertyItems[i]);
            }
        }

        return matches.Count == 0 ? Array.Empty<DesignerControlCompletionItem>() : matches.ToArray();
    }

    private static DesignerControlCompletionItem[] CreateItems()
    {
        var namesByType = new Dictionary<Type, List<string>>();
        foreach (var knownType in XamlLoader.GetKnownTypes())
        {
            var candidateType = knownType.Type;
            if (!typeof(UIElement).IsAssignableFrom(candidateType) ||
                candidateType.IsAbstract ||
                candidateType.ContainsGenericParameters ||
                !CanInstantiate(candidateType))
            {
                continue;
            }

            if (!namesByType.TryGetValue(candidateType, out var names))
            {
                names = new List<string>();
                namesByType[candidateType] = names;
            }

            names.Add(knownType.Name);
        }

        return namesByType
            .Select(static pair =>
            {
                var preferredName = ChoosePreferredName(pair.Key, pair.Value);
                return new DesignerControlCompletionItem(preferredName, preferredName, pair.Key);
            })
            .OrderBy(static item => item.ElementName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.ElementName, StringComparer.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, DesignerControlCompletionItem[]> CreatePropertyItemsByOwnerName()
    {
        var knownNamesByType = new Dictionary<Type, List<string>>();
        foreach (var knownType in XamlLoader.GetKnownTypes())
        {
            if (!knownNamesByType.TryGetValue(knownType.Type, out var names))
            {
                names = new List<string>();
                knownNamesByType[knownType.Type] = names;
            }

            names.Add(knownType.Name);
        }

        var propertyItemsByName = new Dictionary<string, DesignerControlCompletionItem[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in knownNamesByType)
        {
            var preferredName = ChoosePreferredName(pair.Key, pair.Value);
            var propertyItems = CreatePropertyItems(preferredName, pair.Key);
            if (propertyItems.Length == 0)
            {
                continue;
            }

            foreach (var knownName in pair.Value)
            {
                propertyItemsByName[knownName] = propertyItems;
            }
        }

        return propertyItemsByName;
    }

    private static DesignerControlCompletionItem[] CreatePropertyItems(string ownerName, Type ownerType)
    {
        var propertyMap = new Dictionary<string, Type>(StringComparer.Ordinal);
        var currentType = ownerType;
        while (currentType != null)
        {
            var properties = currentType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                if (property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                if (XamlTypeResolver.GetWritableProperty(ownerType, property.Name) == null)
                {
                    continue;
                }

                propertyMap.TryAdd(property.Name, property.PropertyType);
            }

            currentType = currentType.BaseType;
        }

        return propertyMap
            .Select(static pair => pair)
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new DesignerControlCompletionItem(ownerName + "." + pair.Key, pair.Key, pair.Value))
            .ToArray();
    }

    private static bool TryGetPropertyItemsForOwner(string ownerName, out DesignerControlCompletionItem[] propertyItems)
    {
        return PropertyItemsByOwnerName.TryGetValue(ownerName, out propertyItems!);
    }

    private static bool CanInstantiate(Type candidateType)
    {
        return candidateType.IsValueType || candidateType.GetConstructor(Type.EmptyTypes) != null;
    }

    private static string ChoosePreferredName(Type type, IReadOnlyList<string> names)
    {
        if (names.Count == 1)
        {
            return names[0];
        }

        return names
            .OrderBy(name => ScoreName(type, name))
            .ThenBy(static name => name.Length)
            .ThenBy(static name => name, StringComparer.Ordinal)
            .First();
    }

    private static int ScoreName(Type type, string candidateName)
    {
        if (!string.Equals(candidateName, type.Name, StringComparison.Ordinal) && candidateName.Length < type.Name.Length)
        {
            return 0;
        }

        if (string.Equals(candidateName, type.Name, StringComparison.Ordinal))
        {
            return 1;
        }

        return 2;
    }
}

internal readonly record struct DesignerControlCompletionItem(string ElementName, string DisplayName, Type ElementType);
