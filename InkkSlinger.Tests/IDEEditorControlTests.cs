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

    private static (UiRoot UiRoot, IDE_Editor Editor) CreateLaidOutEditor(float width, float height, string text)
    {
        var editor = new IDE_Editor
        {
            Width = width,
            Height = height,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping = TextWrapping.NoWrap
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