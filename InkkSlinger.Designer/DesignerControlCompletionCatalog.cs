using System;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger.Designer;

internal static class DesignerControlCompletionCatalog
{
    private static readonly Lazy<DesignerControlCompletionItem[]> AllItems = new(CreateItems);

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
            .Select(static pair => new DesignerControlCompletionItem(ChoosePreferredName(pair.Key, pair.Value), pair.Key))
            .OrderBy(static item => item.ElementName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.ElementName, StringComparer.Ordinal)
            .ToArray();
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

internal readonly record struct DesignerControlCompletionItem(string ElementName, Type ElementType);