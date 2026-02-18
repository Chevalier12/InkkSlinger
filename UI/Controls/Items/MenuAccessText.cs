namespace InkkSlinger;

internal static class MenuAccessText
{
    internal static bool TryExtractAccessKey(string text, out char accessKey)
    {
        accessKey = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] != '_')
            {
                continue;
            }

            var c = text[i + 1];
            if (c == '_')
            {
                i++;
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                accessKey = char.ToUpperInvariant(c);
                return true;
            }
        }

        return false;
    }

    internal static string StripAccessMarkers(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var chars = new char[text.Length];
        var index = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '_' && i + 1 < text.Length)
            {
                if (text[i + 1] == '_')
                {
                    chars[index++] = '_';
                    i++;
                    continue;
                }

                continue;
            }

            chars[index++] = c;
        }

        return new string(chars, 0, index);
    }
}
