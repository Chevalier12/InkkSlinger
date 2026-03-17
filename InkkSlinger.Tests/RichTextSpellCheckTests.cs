using Xunit;

namespace InkkSlinger.Tests;

public sealed class RichTextSpellCheckTests
{
    [Fact]
    public void IsSpellCheckEnabled_AndCustomDictionaries_RoundTripThroughSpellCheckService()
    {
        var editor = CreateEditor("alpha beta");

        Assert.False(editor.IsSpellCheckEnabled);
        Assert.False(SpellCheck.GetIsEnabled(editor));

        editor.IsSpellCheckEnabled = true;
        var customDictionaries = SpellCheck.GetCustomDictionaries(editor);
        customDictionaries.Add(new Uri("https://example.com/Lexicons/custom.lex", UriKind.Absolute));

        Assert.True(editor.IsSpellCheckEnabled);
        Assert.True(SpellCheck.GetIsEnabled(editor));
        Assert.Single(SpellCheck.GetCustomDictionaries(editor));
    }

    [Fact]
    public void SpellCheckQueries_WithNoEngine_ReportNoErrors()
    {
        var editor = CreateEditor("mispelled text");
        editor.IsSpellCheckEnabled = true;

        Assert.Equal(-1, editor.GetNextSpellingErrorCharacterIndex(0, LogicalDirection.Forward));
        Assert.Null(editor.GetNextSpellingErrorPosition(editor.CaretPosition, LogicalDirection.Forward));
        Assert.Null(editor.GetSpellingError(editor.CaretPosition));
        Assert.Null(editor.GetSpellingErrorRange(editor.CaretPosition));
    }

    [Fact]
    public void SpellCheckQueries_ValidateArguments()
    {
        var editor = CreateEditor("alpha");
        var foreignEditor = CreateEditor("beta");

        Assert.Throws<ArgumentOutOfRangeException>(() => editor.GetNextSpellingErrorCharacterIndex(-1, LogicalDirection.Forward));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.GetNextSpellingErrorCharacterIndex(6, LogicalDirection.Forward));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.GetNextSpellingErrorCharacterIndex(0, (LogicalDirection)42));
        Assert.Throws<ArgumentException>(() => editor.GetNextSpellingErrorPosition(foreignEditor.CaretPosition, LogicalDirection.Forward));
        Assert.Throws<ArgumentException>(() => editor.GetSpellingError(foreignEditor.CaretPosition));
        Assert.Throws<ArgumentException>(() => editor.GetSpellingErrorRange(foreignEditor.CaretPosition));
    }

    private static RichTextBox CreateEditor(string text)
    {
        var editor = new RichTextBox();
        editor.SetLayoutSlot(new LayoutRect(0f, 0f, 480f, 240f));
        DocumentEditing.ReplaceAllText(editor.Document, text);
        editor.SetFocusedFromInput(true);
        return editor;
    }
}