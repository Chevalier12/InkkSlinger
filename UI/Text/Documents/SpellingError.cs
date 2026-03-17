using System;
using System.Collections.Generic;

namespace InkkSlinger;

public sealed class SpellingError
{
    private readonly Action<string>? _correctAction;
    private readonly Action? _ignoreAllAction;

    internal SpellingError(
        TextRange range,
        IReadOnlyList<string>? suggestions = null,
        Action<string>? correctAction = null,
        Action? ignoreAllAction = null)
    {
        Range = range.Normalize();
        Suggestions = suggestions ?? Array.Empty<string>();
        _correctAction = correctAction;
        _ignoreAllAction = ignoreAllAction;
    }

    public TextRange Range { get; }

    public IReadOnlyList<string> Suggestions { get; }

    public void Correct(string correction)
    {
        ArgumentNullException.ThrowIfNull(correction);

        if (_correctAction is null)
        {
            throw new NotSupportedException("This spelling error cannot be corrected because no spell-check engine is active.");
        }

        _correctAction(correction);
    }

    public void IgnoreAll()
    {
        _ignoreAllAction?.Invoke();
    }
}