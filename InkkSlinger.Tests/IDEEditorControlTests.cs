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

    [Theory]
    [InlineData(26f, 0.05f)]
    [InlineData(32f, 0.49f)]
    [InlineData(48f, 0.99f)]
    [InlineData(64f, 1.01f)]
    [InlineData(92f, 1.49f)]
    [InlineData(128f, 2.25f)]
    public void ScrollToVerticalOffset_FractionalOffsetsAcrossViewportSizes_PreserveGutterIntegrity(float height, float offsetMultiplier)
    {
        var (uiRoot, editor) = CreateLaidOutEditor(220f, height, CreateNumberedText(120));

        editor.ScrollToVerticalOffset(editor.EstimatedLineHeight * offsetMultiplier);
        RunLayout(uiRoot, 260, (int)height + 40, 16);

        AssertGutterIntegrity(editor);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.15f)]
    [InlineData(0.5f)]
    [InlineData(0.95f)]
    [InlineData(1.5f)]
    [InlineData(3f)]
    public void ScrollToVerticalOffset_NearDocumentEnd_PreservesBoundedTailRange(float tailBackoffLineCount)
    {
        var (uiRoot, editor) = CreateLaidOutEditor(220f, 72f, CreateNumberedText(160));
        var targetOffset = Math.Max(0f, editor.ScrollableHeight - (editor.EstimatedLineHeight * tailBackoffLineCount));

        editor.ScrollToVerticalOffset(targetOffset);
        RunLayout(uiRoot, 260, 112, 16);

        AssertGutterIntegrity(editor);
        var lastVisibleLine = int.Parse(editor.LineNumberPresenter.VisibleLineTexts[^1]);
        var boundedTailFloor = Math.Max(1, editor.LineCount - (int)Math.Ceiling(tailBackoffLineCount));
        Assert.InRange(lastVisibleLine, boundedTailFloor, editor.LineCount);
    }

    [Fact]
    public void ScrollToVerticalOffset_RoundTripAcrossDocument_DoesNotCollapseGutterState()
    {
        var (uiRoot, editor) = CreateLaidOutEditor(220f, 80f, CreateNumberedText(180));
        var offsets = new[]
        {
            0f,
            editor.EstimatedLineHeight * 0.35f,
            editor.EstimatedLineHeight * 1.1f,
            editor.EstimatedLineHeight * 2.75f,
            editor.ScrollableHeight * 0.5f,
            Math.Max(0f, editor.ScrollableHeight - (editor.EstimatedLineHeight * 0.4f)),
            editor.ScrollableHeight,
            editor.ScrollableHeight * 0.67f,
            editor.EstimatedLineHeight * 1.6f,
            0f
        };

        foreach (var offset in offsets)
        {
            editor.ScrollToVerticalOffset(offset);
            RunLayout(uiRoot, 260, 120, 16);
            AssertGutterIntegrity(editor);
        }

        Assert.Equal(0, editor.LineNumberPresenter.FirstVisibleLine);
        Assert.Equal("1", editor.LineNumberPresenter.VisibleLineTexts[0]);
    }

    [Fact]
    public void ScrollToVerticalOffset_IncreasingOffsetsAcrossLineBoundaries_KeepVisibleRangeMonotonic()
    {
        var (uiRoot, editor) = CreateLaidOutEditor(220f, 84f, CreateNumberedText(140));
        var lineHeight = editor.EstimatedLineHeight;
        var offsets = new[]
        {
            lineHeight * 0.99f,
            lineHeight * 1.01f,
            lineHeight * 1.99f,
            lineHeight * 2.01f,
            lineHeight * 2.99f,
            lineHeight * 3.01f,
            lineHeight * 4.5f
        };

        var previousFirstVisibleLine = -1;
        foreach (var offset in offsets)
        {
            editor.ScrollToVerticalOffset(offset);
            RunLayout(uiRoot, 260, 124, 16);

            AssertGutterIntegrity(editor);
            Assert.True(
                editor.LineNumberPresenter.FirstVisibleLine >= previousFirstVisibleLine,
                $"Expected first visible gutter line to stay monotonic as scroll offsets increased, but previous={previousFirstVisibleLine}, current={editor.LineNumberPresenter.FirstVisibleLine}, offset={offset:0.###}, verticalOffset={editor.VerticalOffset:0.###}.");

            previousFirstVisibleLine = editor.LineNumberPresenter.FirstVisibleLine;
        }
    }

    [Fact]
    public void HandleMouseWheelFromInput_RepeatedVerticalScroll_PreservesGutterIntegrity()
    {
        var (uiRoot, editor) = CreateLaidOutEditor(220f, 88f, CreateNumberedText(120));

        editor.SetFocusedFromInput(true);
        editor.SetMouseOverFromInput(true);

        var handledWheelDeltas = 0;
        for (var iteration = 0; iteration < 24; iteration++)
        {
            if (editor.HandleMouseWheelFromInput(-120))
            {
                handledWheelDeltas++;
            }

            RunLayout(uiRoot, 260, 128, 16);
            AssertGutterIntegrity(editor);
        }

        Assert.True(handledWheelDeltas > 0, "Expected mouse-wheel input to move the IDE editor viewport at least once.");
        Assert.True(editor.VerticalOffset > 0f, $"Expected repeated wheel input to produce vertical movement, but offset={editor.VerticalOffset:0.###}.");
    }

    [Fact]
    public void RefreshDocumentMetrics_WhenDocumentShrinksWhileScrolledToEnd_ClampsGutterRange()
    {
        var (uiRoot, editor) = CreateLaidOutEditor(220f, 76f, CreateNumberedText(120));

        editor.ScrollToVerticalOffset(editor.ScrollableHeight);
        RunLayout(uiRoot, 260, 116, 16);
        AssertGutterIntegrity(editor);

        DocumentEditing.ReplaceAllText(editor.Document, CreateNumberedText(6));
        editor.RefreshDocumentMetrics();
        RunLayout(uiRoot, 260, 116, 16);

        Assert.Equal(6, editor.LineCount);
        AssertGutterIntegrity(editor);
        var lastVisibleLine = int.Parse(editor.LineNumberPresenter.VisibleLineTexts[^1]);
        Assert.InRange(lastVisibleLine, Math.Max(1, editor.LineCount - 1), editor.LineCount);
        Assert.InRange(editor.VerticalOffset, 0f, editor.ScrollableHeight + 0.01f);
    }

    [Fact]
    public void RefreshDocumentMetrics_WhenDocumentGrowsWhilePartiallyScrolled_PreservesSequentialLabels()
    {
        var (uiRoot, editor) = CreateLaidOutEditor(220f, 76f, CreateNumberedText(8));

        editor.ScrollToVerticalOffset(editor.EstimatedLineHeight * 1.4f);
        RunLayout(uiRoot, 260, 116, 16);
        AssertGutterIntegrity(editor);

        DocumentEditing.ReplaceAllText(editor.Document, CreateNumberedText(80));
        editor.RefreshDocumentMetrics();
        RunLayout(uiRoot, 260, 116, 16);

        Assert.Equal(80, editor.LineCount);
        AssertGutterIntegrity(editor);
        Assert.True(
            editor.ScrollableHeight > 0f,
            $"Expected document growth to produce a scrollable editor with valid sequential gutter labels, but scrollableHeight={editor.ScrollableHeight:0.###}, verticalOffset={editor.VerticalOffset:0.###}, firstVisible={editor.LineNumberPresenter.FirstVisibleLine}.");
    }

    [Fact]
    public void ScrollToVerticalOffset_SingleLineDocument_DoesNotDuplicateOrOffsetGutterRows()
    {
        var (uiRoot, editor) = CreateLaidOutEditor(220f, 72f, "only line");

        editor.ScrollToVerticalOffset(999f);
        RunLayout(uiRoot, 260, 112, 16);

        Assert.Equal(1, editor.LineCount);
        AssertGutterIntegrity(editor);
        Assert.Equal(0, editor.LineNumberPresenter.FirstVisibleLine);
        Assert.Equal(1, editor.LineNumberPresenter.VisibleLineCount);
        Assert.Equal("1", editor.LineNumberPresenter.VisibleLineTexts[0]);
        Assert.Equal(0f, editor.LineNumberPresenter.VerticalLineOffset);
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
    public void HandleKeyDownFromInput_Tab_InsertsTwoSpacesInsteadOfTabCharacter()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "alpha");

        editor.SetFocusedFromInput(true);
        editor.Select(editor.DocumentText.Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Tab, ModifierKeys.None));

        Assert.Equal("alpha  ", editor.DocumentText);
        Assert.DoesNotContain('\t', editor.DocumentText);
        Assert.Equal(editor.DocumentText.Length, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void HandleKeyDownFromInput_Tab_ReplacesSelectionWithTwoSpaces()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "alpha");

        editor.SetFocusedFromInput(true);
        editor.Select(1, 3);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Tab, ModifierKeys.None));

        Assert.Equal("a  a", editor.DocumentText);
        Assert.DoesNotContain('\t', editor.DocumentText);
        Assert.Equal(3, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void HandleKeyDownFromInput_Backspace_AdjacentSpaces_DeleteBothSpaces()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "alpha  ");

        editor.SetFocusedFromInput(true);
        editor.Select(editor.DocumentText.Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        Assert.Equal("alpha", editor.DocumentText);
        Assert.Equal(editor.DocumentText.Length, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void HandleKeyDownFromInput_Backspace_SingleSpace_DeletesOnlyOneSpace()
    {
        var (_, editor) = CreateLaidOutEditor(180f, 96f, "alpha ");

        editor.SetFocusedFromInput(true);
        editor.Select(editor.DocumentText.Length, 0);

        Assert.True(editor.HandleKeyDownFromInput(Keys.Back, ModifierKeys.None));

        Assert.Equal("alpha", editor.DocumentText);
        Assert.Equal(editor.DocumentText.Length, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
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

    private static string CreateNumberedText(int lineCount)
    {
        return string.Join("\n", Enumerable.Range(1, lineCount).Select(static index => $"Line {index:000}"));
    }

    private static void AssertGutterIntegrity(IDE_Editor editor)
    {
        var presenter = editor.LineNumberPresenter;

        Assert.InRange(presenter.FirstVisibleLine, 0, Math.Max(0, editor.LineCount - 1));
        Assert.InRange(presenter.VisibleLineCount, 1, editor.LineCount);
        Assert.True(
            presenter.FirstVisibleLine + presenter.VisibleLineCount <= editor.LineCount,
            $"Expected gutter visible range to stay within the document, but firstVisible={presenter.FirstVisibleLine}, visibleCount={presenter.VisibleLineCount}, lineCount={editor.LineCount}.");
        Assert.True(presenter.LineHeight >= 1f, $"Expected the gutter to use a positive line height, but LineHeight={presenter.LineHeight:0.###}.");
        Assert.InRange(presenter.VerticalLineOffset, 0f, presenter.LineHeight + 0.01f);

        for (var index = 0; index < presenter.VisibleLineTexts.Count; index++)
        {
            Assert.True(
                int.TryParse(presenter.VisibleLineTexts[index], out var parsedLineNumber),
                $"Expected gutter text at index {index} to be numeric, but value was '{presenter.VisibleLineTexts[index]}'.");
            Assert.Equal(presenter.FirstVisibleLine + index + 1, parsedLineNumber);
        }
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        _ = elapsedMs;
        uiRoot.RunLayoutForTests(new Viewport(0, 0, width, height));
    }
}