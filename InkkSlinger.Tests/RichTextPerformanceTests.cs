using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RichTextPerformanceTests
{
    [Fact]
    public void RepeatedLocalEdits_DoNotShowPathologicalAllocationSpikes()
    {
        var editor = CreateEditor("seed");
        editor.ResetPerformanceSnapshot();

        var startBytes = GC.GetAllocatedBytesForCurrentThread();
        const int editCount = 200;
        for (var i = 0; i < editCount; i++)
        {
            Assert.True(editor.HandleTextInputFromInput('a'));
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - startBytes;
        var snapshot = editor.GetPerformanceSnapshot();

        Assert.True(allocatedBytes < 256L * 1024L * 1024L);
        Assert.True(snapshot.EditSampleCount >= editCount);
        Assert.True(snapshot.MaxEditMilliseconds >= 0d);
    }

    [Fact]
    public void LocalEditLayoutInvalidation_RemainsBoundedInCommonFlow()
    {
        var editor = CreateEditor("hello world");
        editor.ResetPerformanceSnapshot();

        const int edits = 16;
        for (var i = 0; i < edits; i++)
        {
            Assert.True(editor.HandleTextInputFromInput('x'));
        }

        var snapshot = editor.GetPerformanceSnapshot();
        Assert.True(snapshot.LayoutCacheMissCount >= edits);
        Assert.True(snapshot.LayoutCacheMissCount <= edits * 3);
        Assert.True(snapshot.P95LayoutBuildMilliseconds >= 0d);
        Assert.True(snapshot.P99LayoutBuildMilliseconds >= snapshot.P95LayoutBuildMilliseconds);
    }

    [Fact]
    public void PerformanceSnapshot_TracksClipboardAndUndoMetrics()
    {
        TextClipboard.ResetForTests();
        var source = CreateEditor("copy me");
        source.HandleKeyDownFromInput(Keys.A, ModifierKeys.Control);
        CommandManager.Execute(EditingCommands.Copy, null, source);

        var target = CreateEditor(string.Empty);
        target.ResetPerformanceSnapshot();
        CommandManager.Execute(EditingCommands.Paste, null, target);

        var snapshot = target.GetPerformanceSnapshot();
        Assert.True(snapshot.ClipboardDeserializeSampleCount >= 1);
        Assert.True(snapshot.UndoDepth >= 1);
        Assert.True(snapshot.UndoOperationCount >= 1);
    }

    [Fact]
    public void EmbeddedUiTyping_UsesParagraphLocalUndoOperation()
    {
        var editor = CreateEditorWithEmbeddedUi();
        editor.Select(0, 0);
        editor.ResetPerformanceSnapshot();

        Assert.True(editor.HandleTextInputFromInput('a'));
        Assert.True(editor.HandleTextInputFromInput('b'));
        Assert.True(editor.HandleTextInputFromInput('c'));

        var snapshot = editor.GetPerformanceSnapshot();
        Assert.Equal(1, snapshot.UndoDepth);
        Assert.Equal(1, snapshot.UndoOperationCount);
        Assert.StartsWith("abc", DocumentEditing.GetText(editor.Document));
        Assert.True(snapshot.EditSampleCount >= 3);
    }

    private static RichTextBox CreateEditor(string text)
    {
        var editor = new RichTextBox();
        editor.SetLayoutSlot(new LayoutRect(0f, 0f, 520f, 280f));
        DocumentEditing.ReplaceAllText(editor.Document, text);
        editor.SetFocusedFromInput(true);
        return editor;
    }

    private static RichTextBox CreateEditorWithEmbeddedUi()
    {
        var editor = new RichTextBox();
        editor.SetLayoutSlot(new LayoutRect(0f, 0f, 520f, 280f));
        editor.SetFocusedFromInput(true);

        var document = new FlowDocument();
        var intro = new Paragraph();
        intro.Inlines.Add(new Run("Hosted UI proves that inline and block containers participate in layout and input."));
        document.Blocks.Add(intro);

        var inlineParagraph = new Paragraph();
        inlineParagraph.Inlines.Add(new Run("Inline tool: hosted button "));
        inlineParagraph.Inlines.Add(new InlineUIContainer
        {
            Child = new Button { Content = "Inline" }
        });
        document.Blocks.Add(inlineParagraph);

        var blockParagraph = new Paragraph();
        blockParagraph.Inlines.Add(new Run("Block host below."));
        document.Blocks.Add(blockParagraph);
        document.Blocks.Add(new BlockUIContainer
        {
            Child = new Button { Content = "Block" }
        });

        editor.Document = document;
        return editor;
    }
}
