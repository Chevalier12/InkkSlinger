using Xunit;

namespace InkkSlinger.Tests;

public sealed class DocumentUndoTransactionTests
{
    [Fact]
    public void GroupedTyping_FormsSingleUndoUnit()
    {
        var document = new FlowDocument();
        FlowDocumentPlainText.SetText(document, string.Empty);
        var undo = new DocumentUndoManager();

        ApplyReplace(document, undo, 0, 0, "a", GroupingPolicy.TypingBurst, "InsertText", 0);
        ApplyReplace(document, undo, 1, 0, "b", GroupingPolicy.TypingBurst, "InsertText", 1);
        ApplyReplace(document, undo, 2, 0, "c", GroupingPolicy.TypingBurst, "InsertText", 2);

        Assert.Equal("abc", FlowDocumentPlainText.GetText(document));
        Assert.True(undo.Undo());
        Assert.Equal(string.Empty, FlowDocumentPlainText.GetText(document));
        Assert.False(undo.Undo());
    }

    [Fact]
    public void CaretMove_BreaksTypingGroup()
    {
        var document = new FlowDocument();
        FlowDocumentPlainText.SetText(document, string.Empty);
        var undo = new DocumentUndoManager();

        ApplyReplace(document, undo, 0, 0, "a", GroupingPolicy.TypingBurst, "InsertText", 0);
        ApplyReplace(document, undo, 0, 0, "b", GroupingPolicy.TypingBurst, "InsertText", 0);

        Assert.Equal("ba", FlowDocumentPlainText.GetText(document));

        Assert.True(undo.Undo());
        Assert.Equal("a", FlowDocumentPlainText.GetText(document));

        Assert.True(undo.Undo());
        Assert.Equal(string.Empty, FlowDocumentPlainText.GetText(document));
    }

    [Fact]
    public void UndoThenNewEdit_ClearsRedoStack()
    {
        var document = new FlowDocument();
        FlowDocumentPlainText.SetText(document, "hello");
        var undo = new DocumentUndoManager();

        ApplyReplace(document, undo, 5, 0, "!", GroupingPolicy.StructuralAtomic, "Append", 5);
        Assert.Equal("hello!", FlowDocumentPlainText.GetText(document));

        Assert.True(undo.Undo());
        Assert.Equal("hello", FlowDocumentPlainText.GetText(document));

        ApplyReplace(document, undo, 0, 0, "X", GroupingPolicy.StructuralAtomic, "Prefix", 0);
        Assert.Equal("Xhello", FlowDocumentPlainText.GetText(document));
        Assert.False(undo.Redo());
    }

    [Fact]
    public void MixedTransaction_UndoesAtomically()
    {
        var document = new FlowDocument();
        FlowDocumentPlainText.SetText(document, "abc");
        var undo = new DocumentUndoManager();
        var session = new DocumentEditSession(document, undo);

        session.BeginTransaction(
            "MixedEdit",
            GroupingPolicy.StructuralAtomic,
            new DocumentEditContext(3, 5, 3, 0, 5, 0, "MixedEdit"));
        session.ApplyOperation(new ReplaceTextOperation(3, string.Empty, "d"));
        session.ApplyOperation(new ReplaceTextOperation(4, string.Empty, "e"));
        session.CommitTransaction();

        Assert.Equal("abcde", FlowDocumentPlainText.GetText(document));
        Assert.True(undo.Undo());
        Assert.Equal("abc", FlowDocumentPlainText.GetText(document));
        Assert.True(undo.Redo());
        Assert.Equal("abcde", FlowDocumentPlainText.GetText(document));
    }

    [Fact]
    public void Rollback_LeavesDocumentUnchanged()
    {
        var document = new FlowDocument();
        FlowDocumentPlainText.SetText(document, "seed");
        var undo = new DocumentUndoManager();
        var session = new DocumentEditSession(document, undo);

        session.BeginTransaction(
            "Rollback",
            GroupingPolicy.StructuralAtomic,
            new DocumentEditContext(4, 5, 4, 0, 5, 0, "Rollback"));
        session.ApplyOperation(new ReplaceTextOperation(4, string.Empty, "!"));
        session.RollbackTransaction();

        Assert.Equal("seed", FlowDocumentPlainText.GetText(document));
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void UndoAcrossParagraphSplitThenTyping_IsDeterministic()
    {
        var document = new FlowDocument();
        FlowDocumentPlainText.SetText(document, "abcd");
        var undo = new DocumentUndoManager();

        var split = new DocumentEditSession(document, undo);
        split.BeginTransaction(
            "SplitParagraph",
            GroupingPolicy.StructuralAtomic,
            new DocumentEditContext(2, 3, 2, 0, 3, 0, "SplitParagraph"));
        split.ApplyOperation(new SplitParagraphOperation(2));
        split.CommitTransaction();

        ApplyReplace(document, undo, 3, 0, "X", GroupingPolicy.TypingBurst, "InsertText", 3);
        Assert.Equal($"ab{Environment.NewLine}Xcd", FlowDocumentPlainText.GetText(document));

        Assert.True(undo.Undo());
        Assert.Equal($"ab{Environment.NewLine}cd", FlowDocumentPlainText.GetText(document));
        Assert.True(undo.Undo());
        Assert.Equal("abcd", FlowDocumentPlainText.GetText(document));
    }

    [Fact]
    public void UndoRedo_MultilinePaste_IsDeterministic()
    {
        var document = new FlowDocument();
        FlowDocumentPlainText.SetText(document, "seed");
        var undo = new DocumentUndoManager();

        ApplyReplace(document, undo, 4, 0, "\nline2\nline3", GroupingPolicy.StructuralAtomic, "Paste", 4);
        Assert.Equal($"seed{Environment.NewLine}line2{Environment.NewLine}line3", FlowDocumentPlainText.GetText(document));

        Assert.True(undo.Undo());
        Assert.Equal("seed", FlowDocumentPlainText.GetText(document));
        Assert.True(undo.Redo());
        Assert.Equal($"seed{Environment.NewLine}line2{Environment.NewLine}line3", FlowDocumentPlainText.GetText(document));
    }

    private static void ApplyReplace(
        FlowDocument document,
        DocumentUndoManager manager,
        int start,
        int length,
        string replacement,
        GroupingPolicy policy,
        string commandType,
        int caretBefore)
    {
        var caretAfter = start + replacement.Length;
        var session = new DocumentEditSession(document, manager);
        session.BeginTransaction(
            commandType,
            policy,
            new DocumentEditContext(caretBefore, caretAfter, start, length, caretAfter, 0, commandType));
        DocumentEditing.ReplaceTextRange(document, start, length, replacement, session);
        session.CommitTransaction();
    }
}
