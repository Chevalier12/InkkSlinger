using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace InkkSlinger;

public enum IDEEditorXmlSyntaxTokenKind
{
    Text,
    Delimiter,
    Equals,
    String,
    Comment,
    CData,
    ProcessingInstruction,
    Declaration,
    NamespaceDeclaration,
    ElementName,
    ControlTypeName,
    PropertyName
}

public readonly record struct IDEEditorXmlSyntaxToken(int Start, int Length, IDEEditorXmlSyntaxTokenKind Kind)
{
    public int End => Start + Length;
}

public static class IDEEditorXmlSyntaxClassifier
{
    private static readonly IReadOnlyDictionary<string, Type> KnownControlTypesByName = XamlLoader.GetKnownTypes()
        .GroupBy(static knownType => knownType.Name, StringComparer.Ordinal)
        .ToDictionary(static group => group.Key, static group => group.First().Type, StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, IDEEditorXmlSyntaxTokenKind> TagNameTokenKinds = new(StringComparer.Ordinal);

    public static IReadOnlyList<IDEEditorXmlSyntaxToken> Classify(string? text)
    {
        var source = text ?? string.Empty;
        var tokens = new List<IDEEditorXmlSyntaxToken>();
        var index = 0;

        while (index < source.Length)
        {
            if (source[index] != '<')
            {
                var textStart = index;
                while (index < source.Length && source[index] != '<')
                {
                    index++;
                }

                AddToken(tokens, textStart, index - textStart, IDEEditorXmlSyntaxTokenKind.Text);
                continue;
            }

            if (TryReadDelimitedMarkup(tokens, source, ref index, "<!--", "-->", IDEEditorXmlSyntaxTokenKind.Comment) ||
                TryReadDelimitedMarkup(tokens, source, ref index, "<![CDATA[", "]]>", IDEEditorXmlSyntaxTokenKind.CData) ||
                TryReadDelimitedMarkup(tokens, source, ref index, "<?", "?>", IDEEditorXmlSyntaxTokenKind.ProcessingInstruction))
            {
                continue;
            }

            if (Matches(source, index, "<!"))
            {
                var declarationStart = index;
                SkipUntil(source, ref index, '>');
                AddToken(tokens, declarationStart, index - declarationStart, IDEEditorXmlSyntaxTokenKind.Declaration);
                continue;
            }

            ReadElement(tokens, source, ref index);
        }

        return tokens;
    }

    public static bool TryClassifyTagName(string? tagName, out IDEEditorXmlSyntaxTokenKind kind)
    {
        var resolvedKind = ClassifyTagName(tagName ?? string.Empty);
        if (resolvedKind is IDEEditorXmlSyntaxTokenKind.ControlTypeName or IDEEditorXmlSyntaxTokenKind.PropertyName)
        {
            kind = resolvedKind;
            return true;
        }

        kind = default;
        return false;
    }

    private static void ReadElement(List<IDEEditorXmlSyntaxToken> tokens, string source, ref int index)
    {
        var delimiterStart = index;
        index++;
        if (index < source.Length && source[index] == '/')
        {
            index++;
        }

        AddToken(tokens, delimiterStart, index - delimiterStart, IDEEditorXmlSyntaxTokenKind.Delimiter);
        SkipWhitespace(source, ref index);

        var tagNameStart = index;
        ReadXmlName(source, ref index);
        if (index > tagNameStart)
        {
            var tagName = source.Substring(tagNameStart, index - tagNameStart);
            AddToken(tokens, tagNameStart, index - tagNameStart, TagNameTokenKinds.GetOrAdd(tagName, static candidate => ClassifyTagName(candidate)));
        }

        while (index < source.Length)
        {
            SkipWhitespace(source, ref index);
            if (index >= source.Length)
            {
                return;
            }

            if (source[index] == '>')
            {
                AddToken(tokens, index, 1, IDEEditorXmlSyntaxTokenKind.Delimiter);
                index++;
                return;
            }

            if (source[index] == '/' && index + 1 < source.Length && source[index + 1] == '>')
            {
                AddToken(tokens, index, 2, IDEEditorXmlSyntaxTokenKind.Delimiter);
                index += 2;
                return;
            }

            if (source[index] == '=')
            {
                AddToken(tokens, index, 1, IDEEditorXmlSyntaxTokenKind.Equals);
                index++;
                continue;
            }

            if (source[index] is '"' or '\'')
            {
                ReadQuotedString(tokens, source, ref index);
                continue;
            }

            var attributeNameStart = index;
            ReadXmlName(source, ref index);
            if (index == attributeNameStart)
            {
                AddToken(tokens, index, 1, IDEEditorXmlSyntaxTokenKind.Delimiter);
                index++;
                continue;
            }

            var attributeKind = IsNamespaceDeclaration(source.AsSpan(attributeNameStart, index - attributeNameStart))
                ? IDEEditorXmlSyntaxTokenKind.NamespaceDeclaration
                : IDEEditorXmlSyntaxTokenKind.PropertyName;
            AddToken(tokens, attributeNameStart, index - attributeNameStart, attributeKind);
        }
    }

    private static bool TryReadDelimitedMarkup(
        List<IDEEditorXmlSyntaxToken> tokens,
        string source,
        ref int index,
        string prefix,
        string suffix,
        IDEEditorXmlSyntaxTokenKind kind)
    {
        if (!Matches(source, index, prefix))
        {
            return false;
        }

        var start = index;
        index += prefix.Length;
        var suffixIndex = source.IndexOf(suffix, index, StringComparison.Ordinal);
        index = suffixIndex >= 0 ? suffixIndex + suffix.Length : source.Length;
        AddToken(tokens, start, index - start, kind);
        return true;
    }

    private static void ReadQuotedString(List<IDEEditorXmlSyntaxToken> tokens, string source, ref int index)
    {
        var start = index;
        var quote = source[index];
        index++;
        while (index < source.Length && source[index] != quote)
        {
            index++;
        }

        if (index < source.Length)
        {
            index++;
        }

        AddToken(tokens, start, index - start, IDEEditorXmlSyntaxTokenKind.String);
    }

    private static IDEEditorXmlSyntaxTokenKind ClassifyTagName(string tagName)
    {
        var candidate = tagName.AsSpan();
        if (TryResolveKnownType(candidate, out _))
        {
            return IDEEditorXmlSyntaxTokenKind.ControlTypeName;
        }

        return IsKnownPropertyElement(candidate)
            ? IDEEditorXmlSyntaxTokenKind.PropertyName
            : IDEEditorXmlSyntaxTokenKind.ElementName;
    }

    private static bool IsKnownPropertyElement(ReadOnlySpan<char> tagName)
    {
        var separatorIndex = tagName.IndexOf('.');
        if (separatorIndex <= 0 || separatorIndex >= tagName.Length - 1)
        {
            return false;
        }

        if (!TryResolveKnownType(tagName[..separatorIndex], out var ownerType))
        {
            return false;
        }

        var propertyName = tagName[(separatorIndex + 1)..].ToString();
        return HasInstanceProperty(ownerType, propertyName) ||
               HasDependencyPropertyField(ownerType, propertyName) ||
               HasAttachedSetter(ownerType, propertyName);
    }

    private static bool TryResolveKnownType(ReadOnlySpan<char> typeName, out Type type)
    {
        var unqualifiedName = UnqualifyXmlName(typeName).ToString();
        return KnownControlTypesByName.TryGetValue(unqualifiedName, out type!);
    }

    private static ReadOnlySpan<char> UnqualifyXmlName(ReadOnlySpan<char> value)
    {
        var separatorIndex = value.IndexOf(':');
        return separatorIndex >= 0 ? value[(separatorIndex + 1)..] : value;
    }

    private static bool HasInstanceProperty(Type ownerType, string propertyName)
    {
        var current = ownerType;
        while (current != null)
        {
            if (current.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly) != null)
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool HasDependencyPropertyField(Type ownerType, string propertyName)
    {
        var fieldName = propertyName + "Property";
        var current = ownerType;
        while (current != null)
        {
            var field = current.GetField(fieldName, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (field?.FieldType == typeof(DependencyProperty))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool HasAttachedSetter(Type ownerType, string propertyName)
    {
        var setterName = "Set" + propertyName;
        var current = ownerType;
        while (current != null)
        {
            foreach (var method in current.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!string.Equals(method.Name, setterName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (method.GetParameters().Length == 2)
                {
                    return true;
                }
            }

            current = current.BaseType;
        }

        return false;
    }

    private static void SkipUntil(string source, ref int index, char terminator)
    {
        while (index < source.Length && source[index] != terminator)
        {
            index++;
        }

        if (index < source.Length)
        {
            index++;
        }
    }

    private static void SkipWhitespace(string source, ref int index)
    {
        while (index < source.Length && char.IsWhiteSpace(source[index]))
        {
            index++;
        }
    }

    private static void ReadXmlName(string source, ref int index)
    {
        while (index < source.Length && IsXmlNameCharacter(source[index]))
        {
            index++;
        }
    }

    private static bool IsXmlNameCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is ':' or '_' or '-' or '.';
    }

    private static bool IsNamespaceDeclaration(ReadOnlySpan<char> value)
    {
        return value.SequenceEqual("xmlns".AsSpan()) || value.StartsWith("xmlns:".AsSpan(), StringComparison.Ordinal);
    }

    private static bool Matches(string source, int index, string value)
    {
        return index >= 0 &&
               index + value.Length <= source.Length &&
               string.Compare(source, index, value, 0, value.Length, StringComparison.Ordinal) == 0;
    }

    private static void AddToken(List<IDEEditorXmlSyntaxToken> tokens, int start, int length, IDEEditorXmlSyntaxTokenKind kind)
    {
        if (length <= 0)
        {
            return;
        }

        tokens.Add(new IDEEditorXmlSyntaxToken(start, length, kind));
    }
}
