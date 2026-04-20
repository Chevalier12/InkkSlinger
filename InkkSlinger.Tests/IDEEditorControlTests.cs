using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class IDEEditorControlTests
{
    [Fact]
    public void DefaultFallbackTemplate_BuildsFrameworkOwnedGutterAndEditorParts()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "line 1\nline 2\nline 3");

        var templateRoot = Assert.IsType<Border>(Assert.Single(editor.GetVisualChildren()));

        Assert.NotNull(templateRoot.Child);
        Assert.NotNull(editor.LineNumberBorder);
        Assert.NotNull(editor.LineNumberPresenter);
        Assert.NotNull(editor.Editor);
        Assert.True(editor.LineNumberPresenter.VisibleLineCount > 0);
    }

    [Fact]
    public void ScrollToVerticalOffset_UpdatesVisibleLineNumbersWithinFrameworkControl()
    {
        var (uiRoot, editor) = CreateLaidOutEditor(
            220f,
            104f,
            string.Join("\n", Enumerable.Range(1, 60).Select(static index => $"Line {index:00}")));

        Assert.Equal(0, editor.LineNumberPresenter.FirstVisibleLine);
        Assert.Equal("1", editor.LineNumberPresenter.VisibleLineTexts[0]);

        editor.ScrollToVerticalOffset(Math.Max(48f, Math.Max(editor.ScrollableHeight * 0.5f, editor.ViewportHeight * 1.5f)));
        RunLayout(uiRoot, 260, 144, 32);

        Assert.True(
            editor.LineNumberPresenter.FirstVisibleLine > 0,
            $"Expected scrolling to advance the visible line range, but firstVisible={editor.LineNumberPresenter.FirstVisibleLine}, verticalOffset={editor.VerticalOffset:0.###}, scrollable={editor.ScrollableHeight:0.###}, viewport={editor.ViewportHeight:0.###}, extent={editor.ExtentHeight:0.###}, lineCount={editor.LineCount}.");
        Assert.True(editor.LineNumberPresenter.VisibleLineCount > 0);
        Assert.Equal(
            (editor.LineNumberPresenter.FirstVisibleLine + 1).ToString(),
            editor.LineNumberPresenter.VisibleLineTexts[0]);
    }

    [Fact]
    public void TextInput_RefreshesGutterLineCountWithoutSurfaceOwnedSync()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "alpha");

        editor.SetFocusedFromInput(true);
        editor.Select(editor.DocumentText.Length, 0);

        Assert.True(
            editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None),
            $"Expected IDE_Editor to insert a line break, but IsFocused={editor.IsFocused}, innerFocused={editor.Editor.IsFocused}, selectionStart={editor.SelectionStart}, selectionLength={editor.SelectionLength}, textLength={editor.DocumentText.Length}.");
        Assert.True(editor.HandleTextCompositionFromInput("beta"));
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        Assert.True(editor.HandleTextCompositionFromInput("gamma"));

        Assert.True(editor.LineNumberPresenter.VisibleLineCount > 0);
        Assert.Equal("1", editor.LineNumberPresenter.VisibleLineTexts[0]);
        Assert.Equal(3, editor.LineCount);
        Assert.Equal(3, CountLines(editor.DocumentText));
    }

    [Fact]
    public void IndentGuides_MergeAcrossSiblingLinesAtSharedIndentColumns()
    {
        var (_, editor) = CreateLaidOutEditor(
            260f,
            180f,
            "root\n    child\n        grand\n    sibling\nend");

        var snapshot = editor.GetIndentGuideSnapshotForDiagnostics();

        Assert.True(snapshot.HasTextViewport);
        Assert.Equal(2, snapshot.Segments.Count);

        var segments = snapshot.Segments
            .OrderBy(static segment => segment.IndentLevel)
            .ThenBy(static segment => segment.StartVisibleLineIndex)
            .ToArray();

        Assert.Collection(
            segments,
            first =>
            {
                Assert.Equal(1, first.IndentLevel);
                Assert.Equal(1, first.StartVisibleLineIndex);
                Assert.Equal(3, first.EndVisibleLineIndex);
                Assert.True(first.X > snapshot.TextRect.X);
                Assert.True(first.Bottom > first.Top);
            },
            second =>
            {
                Assert.Equal(2, second.IndentLevel);
                Assert.Equal(2, second.StartVisibleLineIndex);
                Assert.Equal(2, second.EndVisibleLineIndex);
                Assert.True(second.X > segments[0].X);
                Assert.True(second.Bottom > second.Top);
            });
    }

    [Fact]
    public void IndentGuides_BridgeBlankLineInsideNestedBlock()
    {
        var (_, editor) = CreateLaidOutEditor(
            260f,
            180f,
            "root\n    child\n        grand\n\n    sibling\nend");

        var snapshot = editor.GetIndentGuideSnapshotForDiagnostics();

        Assert.True(snapshot.HasTextViewport);
        Assert.Equal(2, snapshot.Segments.Count);

        var segments = snapshot.Segments
            .OrderBy(static segment => segment.IndentLevel)
            .ThenBy(static segment => segment.StartVisibleLineIndex)
            .ToArray();

        Assert.Collection(
            segments,
            first =>
            {
                Assert.Equal(1, first.IndentLevel);
                Assert.Equal(1, first.StartVisibleLineIndex);
                Assert.Equal(4, first.EndVisibleLineIndex);
            },
            second =>
            {
                Assert.Equal(2, second.IndentLevel);
                Assert.Equal(2, second.StartVisibleLineIndex);
                Assert.Equal(2, second.EndVisibleLineIndex);
            });
    }

    [Fact]
    public void IndentGuides_ContinueAcrossSoftWrappedIndentedLine()
    {
        var (_, editor) = CreateLaidOutEditor(
            140f,
            220f,
            "root\n    child child child child child\n    sibling\nend",
            TextWrapping.Wrap);

        var snapshot = editor.GetIndentGuideSnapshotForDiagnostics();

        Assert.True(snapshot.HasTextViewport);
        Assert.Single(snapshot.Segments);

        var segment = Assert.Single(snapshot.Segments);
        Assert.Equal(1, segment.IndentLevel);
        Assert.Equal(1, segment.StartVisibleLineIndex);
        Assert.True(segment.EndVisibleLineIndex >= 2, $"Expected wrapped continuation plus sibling line to remain in one guide segment, but endVisibleLineIndex={segment.EndVisibleLineIndex}.");
    }

    [Fact]
    public void IndentGuides_MultilineXmlAttributes_DoNotCreateSeparateHangingIndentGuide()
    {
        var (_, editor) = CreateLaidOutEditor(
            560f,
            260f,
            "<StackPanel>\n  <TextBlock Text=\"Manual refresh is enabled.\"\n             Foreground=\"#E7EDF5\"\n             FontSize=\"18\" />\n  <TextBlock Text=\"Edit the XML below, then press F5 or the toolbar button.\"\n             Foreground=\"#8AA3B8\"\n             Margin=\"0,6,0,12\" />\n\n  <Button x:Name=\"PreviewButton\"\n          Content=\"Preview Action\"\n          Width=\"180\"\n          Height=\"40\" />\n</StackPanel>");

        var snapshot = editor.GetIndentGuideSnapshotForDiagnostics();

        Assert.True(snapshot.HasTextViewport);
        Assert.Single(snapshot.Segments);

        var segment = Assert.Single(snapshot.Segments);
        Assert.Equal(1, segment.IndentLevel);
        Assert.Equal(1, segment.StartVisibleLineIndex);
        Assert.Equal(11, segment.EndVisibleLineIndex);
    }

    [Fact]
    public void IndentGuides_OnlyReportVisibleIndentedBlockAfterScroll()
    {
        var text = string.Join(
            "\n",
            Enumerable.Range(1, 18).Select(static index => $"flat {index:00}")
                .Concat([
                    "root",
                    "    child",
                    "        grand",
                    "    sibling",
                    "tail"
                ]));

        var (uiRoot, editor) = CreateLaidOutEditor(260f, 120f, text);

        var beforeScroll = editor.GetIndentGuideSnapshotForDiagnostics();
        Assert.True(beforeScroll.HasTextViewport);
        Assert.Empty(beforeScroll.Segments);

        editor.ScrollToVerticalOffset(editor.EstimatedLineHeight * 18f);
        RunLayout(uiRoot, 300, 160, 16);

        var afterScroll = editor.GetIndentGuideSnapshotForDiagnostics();

        Assert.NotEmpty(afterScroll.Segments);
        Assert.All(
            afterScroll.Segments,
            segment =>
            {
                Assert.True(segment.Bottom > afterScroll.TextRect.Y);
                Assert.True(segment.Top < afterScroll.TextRect.Y + afterScroll.TextRect.Height);
            });
    }

    [Fact]
    public void Select_InvalidStart_ThrowsArgumentOutOfRangeException()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "abc");

        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Select(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Select(4, 0));
    }

    [Fact]
    public void Select_InvalidLength_ThrowsArgumentOutOfRangeException()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "abc");

        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Select(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Select(0, 4));
    }

    [Fact]
    public void ScrollToVerticalOffset_NonFinite_DoesNotThrow()
    {
        var (uiRoot, editor) = CreateLaidOutEditor(180f, 96f, "line 1\nline 2");

        editor.ScrollToVerticalOffset(float.NaN);
        RunLayout(uiRoot, 220, 136, 16);

        editor.ScrollToVerticalOffset(float.PositiveInfinity);
        RunLayout(uiRoot, 220, 136, 16);
    }

    [Fact]
    public void LineNumberWidth_NonFinite_CoercesToDefault()
    {
        var editor = new IDE_Editor();

        editor.LineNumberWidth = float.PositiveInfinity;
        Assert.Equal(56f, editor.LineNumberWidth);

        editor.LineNumberWidth = float.NaN;
        Assert.Equal(56f, editor.LineNumberWidth);
    }

    [Fact]
    public void IndentGuides_BridgeTrailingBlankLines()
    {
        var (_, editor) = CreateLaidOutEditor(
            260f,
            180f,
            "root\n    child\n        grand\n\n");

        var snapshot = editor.GetIndentGuideSnapshotForDiagnostics();

        Assert.True(snapshot.HasTextViewport);
        Assert.Equal(2, snapshot.Segments.Count);

        var segments = snapshot.Segments
            .OrderBy(static segment => segment.IndentLevel)
            .ThenBy(static segment => segment.StartVisibleLineIndex)
            .ToArray();

        Assert.Collection(
            segments,
            first =>
            {
                Assert.Equal(1, first.IndentLevel);
                Assert.Equal(1, first.StartVisibleLineIndex);
                Assert.Equal(4, first.EndVisibleLineIndex);
            },
            second =>
            {
                Assert.Equal(2, second.IndentLevel);
                Assert.Equal(2, second.StartVisibleLineIndex);
                Assert.Equal(4, second.EndVisibleLineIndex);
            });
    }

    [Fact]
    public void UndoRedo_ExposeInnerEditorState()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "alpha");

        editor.SetFocusedFromInput(true);
        editor.Select(editor.DocumentText.Length, 0);
        Assert.True(editor.HandleTextCompositionFromInput(" beta"));

        Assert.True(editor.CanUndo);
        Assert.False(editor.CanRedo);

        editor.Undo();
        Assert.False(editor.CanUndo);
        Assert.True(editor.CanRedo);
        Assert.Equal("alpha", editor.DocumentText);

        editor.Redo();
        Assert.Equal("alpha beta", editor.DocumentText);
    }

    [Fact]
    public void SelectAll_SelectsEntireDocument()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "abc");

        editor.SelectAll();
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(3, editor.SelectionLength);
    }

    [Fact]
    public void SelectionTextBrush_PropertyDelegatesToInnerEditor()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "abc");

        editor.SelectionTextBrush = Color.Red;
        Assert.Equal(Color.Red, editor.Editor.SelectionTextBrush);
    }

    [Fact]
    public void SelectionOpacity_PropertyDelegatesToInnerEditor()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "abc");

        editor.SelectionOpacity = 0.5f;
        Assert.Equal(0.5f, editor.Editor.SelectionOpacity);
    }

    [Fact]
    public void IsUndoEnabled_PropertyDelegatesToInnerEditor()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "abc");

        editor.IsUndoEnabled = false;
        Assert.False(editor.Editor.IsUndoEnabled);
    }

    [Fact]
    public void UndoLimit_PropertyDelegatesToInnerEditor()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "abc");

        editor.UndoLimit = 10;
        Assert.Equal(10, editor.Editor.UndoLimit);
    }

    [Fact]
    public void CountLines_NormalizesCarriageReturns()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "a\r\nb\rc");

        Assert.Equal(3, editor.LineCount);
    }

    private static (UiRoot UiRoot, IDE_Editor Editor) CreateLaidOutEditor(float width, float height, string text, TextWrapping textWrapping = TextWrapping.NoWrap)
    {
        var editor = new IDE_Editor
        {
            Width = width,
            Height = height,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping = textWrapping
        };

        DocumentEditing.ReplaceAllText(editor.Document, text);

        var host = new Canvas
        {
            Width = width + 40f,
            Height = height + 40f
        };

        host.AddChild(editor);
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, (int)host.Width, (int)host.Height, 16);
        editor.RefreshDocumentMetrics();
        return (uiRoot, editor);
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        var lineCount = 1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                lineCount++;
            }
        }

        return lineCount;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        _ = elapsedMs;
        uiRoot.RunLayoutForTests(new Viewport(0, 0, width, height));
    }
}