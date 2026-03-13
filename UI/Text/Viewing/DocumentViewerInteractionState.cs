using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

internal sealed class DocumentViewerInteractionState
{
    private const double MultiClickWindowMs = 450d;
    private DateTime _lastPointerDownUtc;
    private int _lastPointerDownIndex = -1;
    private int _pointerClickCount;

    public int RegisterPointerDown(int offset)
    {
        var now = DateTime.UtcNow;
        var sameOffset = _lastPointerDownIndex == offset;
        var withinWindow = (now - _lastPointerDownUtc).TotalMilliseconds <= MultiClickWindowMs;

        if (sameOffset && withinWindow)
        {
            _pointerClickCount = Math.Clamp(_pointerClickCount + 1, 1, 3);
        }
        else
        {
            _pointerClickCount = 1;
        }

        _lastPointerDownUtc = now;
        _lastPointerDownIndex = offset;
        return _pointerClickCount;
    }

    public static (int Start, int Length) SelectWord(string text, int offset)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (0, 0);
        }

        var clamped = Math.Clamp(offset, 0, text.Length);
        if (clamped == text.Length)
        {
            clamped = Math.Max(0, clamped - 1);
        }

        var start = clamped;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        var end = clamped;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        return (start, Math.Max(0, end - start));
    }

    public static (int Start, int Length) SelectParagraph(string text, int offset)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (0, 0);
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var clamped = Math.Clamp(offset, 0, normalized.Length);
        if (clamped == normalized.Length)
        {
            clamped = Math.Max(0, clamped - 1);
        }

        var start = clamped;
        while (start > 0 && normalized[start - 1] != '\n')
        {
            start--;
        }

        var end = clamped;
        while (end < normalized.Length && normalized[end] != '\n')
        {
            end++;
        }

        return (start, Math.Max(0, end - start));
    }

    public static float ResolveLineScrollAmount(FrameworkElement element)
    {
        return MathF.Max(16f, UiTextRenderer.GetLineHeight(element) * 3f);
    }
}
