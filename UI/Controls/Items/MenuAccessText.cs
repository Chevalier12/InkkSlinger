namespace InkkSlinger;

internal static class MenuAccessText
{
    internal static bool TryExtractAccessKey(string text, out char accessKey)
    {
        var parsed = AccessTextParser.Parse(text);
        accessKey = parsed.AccessKey ?? default;
        return parsed.AccessKey.HasValue;
    }

    internal static string StripAccessMarkers(string text)
    {
        return AccessTextParser.Parse(text).DisplayText;
    }
}
