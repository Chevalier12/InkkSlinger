namespace InkkSlinger;

internal static class AccessTextParser
{
    internal readonly record struct AccessTextParseResult(
        string DisplayText,
        char? AccessKey,
        int AccessKeyDisplayIndex)
    {
        public static readonly AccessTextParseResult Empty = new(string.Empty, null, -1);
    }

    internal static AccessTextParseResult Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return AccessTextParseResult.Empty;
        }

        var chars = new char[text.Length];
        var outputIndex = 0;
        var accessKey = default(char?);
        var accessKeyDisplayIndex = -1;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '_' && i + 1 < text.Length)
            {
                var next = text[i + 1];
                if (next == '_')
                {
                    chars[outputIndex++] = '_';
                    i++;
                    continue;
                }

                if (accessKey == null && char.IsLetterOrDigit(next))
                {
                    accessKey = char.ToUpperInvariant(next);
                    accessKeyDisplayIndex = outputIndex;
                }

                continue;
            }

            chars[outputIndex++] = c;
        }

        return new AccessTextParseResult(
            new string(chars, 0, outputIndex),
            accessKey,
            accessKeyDisplayIndex);
    }
}
