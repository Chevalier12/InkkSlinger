namespace InkkSlinger;

public static class TextClipboard
{
    private static string _text = string.Empty;

    public static System.Func<string?>? GetTextOverride { get; set; }

    public static System.Action<string>? SetTextOverride { get; set; }

    public static bool TryGetText(out string text)
    {
        if (GetTextOverride != null)
        {
            text = GetTextOverride() ?? string.Empty;
            return text.Length > 0;
        }

        text = _text;
        return text.Length > 0;
    }

    public static void SetText(string text)
    {
        text ??= string.Empty;

        if (SetTextOverride != null)
        {
            SetTextOverride(text);
            return;
        }

        _text = text;
    }

    public static void ResetForTests()
    {
        _text = string.Empty;
        GetTextOverride = null;
        SetTextOverride = null;
    }
}
