using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogRichTextBoxViewTests
{
    [Fact]
    public void RichTextBoxView_ShouldBuildInteractiveEditorWorkspace()
    {
        var view = CreateView();
        var uiRoot = CreateUiRoot(view, 1360, 860);

        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);

        var editor = Assert.IsType<RichTextBox>(view.FindName("Editor"));
        var payloadPreview = Assert.IsType<TextBox>(view.FindName("PayloadPreviewTextBox"));
        var formatComboBox = Assert.IsType<ComboBox>(view.FindName("FormatComboBox"));
        Assert.True(formatComboBox.Items.Count >= 5);
        Assert.NotNull(FindFirstVisualChild<Button>(view, static button => button.GetContentText() == "Bold"));
        Assert.NotNull(FindFirstVisualChild<Button>(view, static button => button.GetContentText() == "Insert Table"));
        Assert.NotNull(FindFirstVisualChild<Button>(view, static button => button.GetContentText() == "Embedded UI"));
        Assert.NotNull(FindFirstVisualChild<Button>(view, static button => button.GetContentText() == "Export Doc"));
        Assert.True(string.IsNullOrEmpty(payloadPreview.Text));
        Assert.True(editor.AcceptsReturn);
        Assert.True(editor.AcceptsTab);

        var heroSummaryLabel = Assert.IsType<TextBlock>(view.FindName("HeroSummaryLabel"));
        Assert.Contains("Preset: Welcome", heroSummaryLabel.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RichTextBoxView_StructurePresetAndExport_ShouldExposeListAndTablePayload()
    {
        var view = CreateView();
        var uiRoot = CreateUiRoot(view, 1360, 860);

        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);

        var structureButton = FindFirstVisualChild<Button>(view, static button => button.GetContentText() == "Structure");
        Assert.NotNull(structureButton);
        InvokeButtonClick(structureButton!);
        RunLayout(uiRoot, 1360, 860);

        var editor = Assert.IsType<RichTextBox>(view.FindName("Editor"));
        Assert.Contains(editor.Document.Blocks, static block => block is InkkSlinger.List);
        Assert.Contains(editor.Document.Blocks, static block => block is Table);

        var heroSummaryLabel = Assert.IsType<TextBlock>(view.FindName("HeroSummaryLabel"));
        Assert.Contains("Preset: Structure", heroSummaryLabel.Text, StringComparison.Ordinal);

        var exportButton = FindFirstVisualChild<Button>(view, static button => button.GetContentText() == "Export Doc");
        Assert.NotNull(exportButton);
        InvokeButtonClick(exportButton!);
        RunLayout(uiRoot, 1360, 860);

        var payloadPreview = Assert.IsType<TextBox>(view.FindName("PayloadPreviewTextBox"));
        Assert.Contains("<Table>", payloadPreview.Text ?? string.Empty, StringComparison.Ordinal);

        var activityLabel = Assert.IsType<TextBox>(view.FindName("ActivityStatusLabel"));
        Assert.Contains("Exported document", activityLabel.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RichTextBoxView_PresetButtons_ShouldNotAutoExportPayloadPreview()
    {
        var view = CreateView();
        var uiRoot = CreateUiRoot(view, 1360, 860);

        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);

        var longformButton = FindFirstVisualChild<Button>(view, static button => button.GetContentText() == "Longform");
        Assert.NotNull(longformButton);

        InvokeButtonClick(longformButton!);
        RunLayout(uiRoot, 1360, 860);

        var payloadPreview = Assert.IsType<TextBox>(view.FindName("PayloadPreviewTextBox"));
        Assert.True(string.IsNullOrEmpty(payloadPreview.Text));

        var heroSummaryLabel = Assert.IsType<TextBlock>(view.FindName("HeroSummaryLabel"));
        Assert.Contains("Preset: Longform", heroSummaryLabel.Text, StringComparison.Ordinal);

        var activityLabel = Assert.IsType<TextBox>(view.FindName("ActivityStatusLabel"));
        Assert.Contains("Loaded Longform preset", activityLabel.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RichTextBoxView_RepeatedTypingAcrossFrames_ShouldPreserveOrderAndCaret()
    {
        var view = CreateView();
        var uiRoot = CreateUiRoot(view, 1360, 860);

        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);

        var editor = Assert.IsType<RichTextBox>(view.FindName("Editor"));
        editor.SetFocusedFromInput(true);
        editor.Select(0, 0);

        Assert.True(editor.HandleTextInputFromInput('a'));
        Assert.StartsWith("a", DocumentEditing.GetText(editor.Document), StringComparison.Ordinal);
        Assert.Equal(1, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionLength);
        RunLayout(uiRoot, 1360, 860);
        Assert.StartsWith("a", DocumentEditing.GetText(editor.Document), StringComparison.Ordinal);
        Assert.Equal(1, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionLength);
        Assert.True(editor.HandleTextInputFromInput('b'));
        var afterSecondInput = DocumentEditing.GetText(editor.Document);
        Assert.True(
            afterSecondInput.StartsWith("ab", StringComparison.Ordinal),
            string.Join(Environment.NewLine, GetRecentOperations(editor)) + Environment.NewLine + afterSecondInput);
        RunLayout(uiRoot, 1360, 860);
        Assert.StartsWith("ab", DocumentEditing.GetText(editor.Document), StringComparison.Ordinal);
        Assert.True(editor.HandleTextInputFromInput('c'));
        Assert.StartsWith("abc", DocumentEditing.GetText(editor.Document), StringComparison.Ordinal);
        RunLayout(uiRoot, 1360, 860);

        var text = DocumentEditing.GetText(editor.Document);
        Assert.StartsWith("abc", text, StringComparison.Ordinal);
        Assert.Equal(3, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void RichTextBoxView_RefreshUiState_AfterTyping_ShouldNotMutateDocument()
    {
        var view = CreateView();
        var uiRoot = CreateUiRoot(view, 1360, 860);

        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);

        var editor = Assert.IsType<RichTextBox>(view.FindName("Editor"));
        editor.SetFocusedFromInput(true);
        editor.Select(0, 0);
        Assert.True(editor.HandleTextInputFromInput('a'));

        var refresh = typeof(RichTextBoxView).GetMethod("RefreshUiState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(refresh);
        refresh!.Invoke(view, null);

        Assert.StartsWith("a", DocumentEditing.GetText(editor.Document), StringComparison.Ordinal);
    }

    [Fact]
    public void RichTextBoxView_BlankPreset_Typing_ShouldUpdateStatusAfterDeferredRefresh()
    {
        var view = CreateView();
        var uiRoot = CreateUiRoot(view, 1360, 860);

        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);

        var blankButton = FindFirstVisualChild<Button>(view, static button => button.GetContentText() == "Blank");
        Assert.NotNull(blankButton);
        InvokeButtonClick(blankButton!);
        RunLayout(uiRoot, 1360, 860);

        var editor = Assert.IsType<RichTextBox>(view.FindName("Editor"));
        editor.SetFocusedFromInput(true);
        editor.Select(0, 0);

        Assert.True(editor.HandleTextInputFromInput('a'));
        WaitForDeferredEditorStatusRefresh(
            uiRoot,
            () =>
            {
                var documentStatus = Assert.IsType<TextBox>(view.FindName("DocumentStatusLabel"));
                return documentStatus.Text?.Contains("1 chars", StringComparison.Ordinal) == true;
            });

        Assert.Equal("a", DocumentEditing.GetText(editor.Document));

        var documentStatusLabel = Assert.IsType<TextBox>(view.FindName("DocumentStatusLabel"));
        Assert.Contains("1 chars", documentStatusLabel.Text, StringComparison.Ordinal);

        var selectionStatusLabel = Assert.IsType<TextBox>(view.FindName("SelectionStatusLabel"));
        Assert.Contains("caret 1", selectionStatusLabel.Text, StringComparison.Ordinal);
        Assert.Contains("Selection: start 1 | length 0", selectionStatusLabel.Text, StringComparison.Ordinal);
    }

    private static void WaitForDeferredEditorStatusRefresh(UiRoot uiRoot, Func<bool> condition)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * 1.5d);
        while (Stopwatch.GetTimestamp() < deadline)
        {
            RunLayout(uiRoot, 1360, 860);
            if (condition())
            {
                return;
            }

            Thread.Sleep(25);
        }

        Assert.True(condition(), "Timed out waiting for the deferred editor status refresh.");
    }

    [Fact]
    public void RichTextBoxView_LiveStatusText_ShouldWrapWithinSidebar()
    {
        var view = CreateView();
        var uiRoot = CreateUiRoot(view, 1360, 860);

        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);

        var documentStatusLabel = Assert.IsType<TextBox>(view.FindName("DocumentStatusLabel"));
        var selectionStatusLabel = Assert.IsType<TextBox>(view.FindName("SelectionStatusLabel"));
        var viewportStatusLabel = Assert.IsType<TextBox>(view.FindName("ViewportStatusLabel"));
        var activityStatusLabel = Assert.IsType<TextBox>(view.FindName("ActivityStatusLabel"));
        var spellCheckStatusLabel = Assert.IsType<TextBox>(view.FindName("SpellCheckStatusLabel"));

        Assert.Equal(TextWrapping.Wrap, documentStatusLabel.TextWrapping);
        Assert.Equal(TextWrapping.Wrap, selectionStatusLabel.TextWrapping);
        Assert.Equal(TextWrapping.Wrap, viewportStatusLabel.TextWrapping);
        Assert.Equal(TextWrapping.Wrap, activityStatusLabel.TextWrapping);
        Assert.Equal(TextWrapping.Wrap, spellCheckStatusLabel.TextWrapping);

        Assert.Equal(300f, documentStatusLabel.MaxWidth);
        Assert.Equal(300f, selectionStatusLabel.MaxWidth);
        Assert.Equal(300f, viewportStatusLabel.MaxWidth);
        Assert.Equal(300f, activityStatusLabel.MaxWidth);
        Assert.Equal(300f, spellCheckStatusLabel.MaxWidth);
    }

    [Fact]
    public void RichTextBoxView_EmbeddedUiPreset_EnterAfterInlineHostedButton_ShouldPreserveHostedChild()
    {
        var view = CreateView();
        var uiRoot = CreateUiRoot(view, 1360, 860);

        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);

        var embeddedUiButton = FindFirstVisualChild<Button>(view, static button => button.GetContentText() == "Embedded UI");
        Assert.NotNull(embeddedUiButton);
        InvokeButtonClick(embeddedUiButton!);
        RunLayout(uiRoot, 1360, 860);

        var editor = Assert.IsType<RichTextBox>(view.FindName("Editor"));
        editor.SetFocusedFromInput(true);

        var documentText = DocumentEditing.GetText(editor.Document);
        var inlineObjectIndex = documentText.IndexOf('\uFFFC');
        Assert.True(inlineObjectIndex >= 0, $"Expected embedded UI preset to contain an inline object. text={documentText}");

        editor.Select(inlineObjectIndex + 1, 0);
        Assert.True(editor.HandleKeyDownFromInput(Keys.Enter, ModifierKeys.None));
        RunLayout(uiRoot, 1360, 860);

        var inlineContainer = FindFirstInlineUiContainer(editor.Document);
        Assert.NotNull(inlineContainer);
        var hostedButton = Assert.IsType<Button>(inlineContainer!.Child);
        Assert.Equal("Inline", hostedButton.GetContentText());
    }

    [Fact]
    public void RichTextBoxView_EmbeddedUiPreset_ShouldExposeInlineHostedButtonInVisualTraversal()
    {
        var host = new Canvas
        {
            Width = 1360f,
            Height = 860f
        };
        var catalog = CreateCatalog();
        host.AddChild(catalog);
        var uiRoot = new UiRoot(host);

        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);

        var view = GetSelectedRichTextBoxView(catalog);

        var embeddedUiButton = FindFirstVisualChild<Button>(view, static button => button.GetContentText() == "Embedded UI");
        Assert.NotNull(embeddedUiButton);
        InvokeButtonClick(embeddedUiButton!);
        RunLayout(uiRoot, 1360, 860);

        var editor = Assert.IsType<RichTextBox>(view.FindName("Editor"));
        var hostedButton = FindFirstVisualChild<Button>(view, static button => button.GetContentText() == "Inline");
        Assert.NotNull(hostedButton);
        editor.UpdateLayout();

        var getVisualChildCountForTraversal = typeof(RichTextBox).GetMethod("GetVisualChildCountForTraversal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(getVisualChildCountForTraversal);
        var getVisualChildAtForTraversal = typeof(RichTextBox).GetMethod("GetVisualChildAtForTraversal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(getVisualChildAtForTraversal);

        var traversalCount = Assert.IsType<int>(getVisualChildCountForTraversal!.Invoke(editor, null));
        Assert.True(traversalCount > 1, $"Expected RichTextBox traversal to include hosted children, but count was {traversalCount}.");

        Button? inlineTraversalButton = null;
        Button? blockTraversalButton = null;
        for (var index = 0; index < traversalCount; index++)
        {
            if (getVisualChildAtForTraversal!.Invoke(editor, [index]) is not Button traversalChild)
            {
                continue;
            }

            if (inlineTraversalButton == null && string.Equals(traversalChild.GetContentText(), "Inline", StringComparison.Ordinal))
            {
                inlineTraversalButton = traversalChild;
            }

            if (blockTraversalButton == null && string.Equals(traversalChild.GetContentText(), "Block", StringComparison.Ordinal))
            {
                blockTraversalButton = traversalChild;
            }
        }

        Assert.NotNull(inlineTraversalButton);
        Assert.NotNull(blockTraversalButton);
    }

    [Fact]
    public void UiRootInputPipeline_HoveringAppStyledHostedInlineButton_UpdatesButtonHoverStateWithoutClick()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();

            var hostedButton = new Button
            {
                Content = "Inline",
                Width = 72f,
                Height = 18f,
                FontSize = 11f,
                Padding = new Thickness(8f, 2f, 8f, 2f)
            };

            var document = new FlowDocument();
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run("Prefix "));
            paragraph.Inlines.Add(new InlineUIContainer { Child = hostedButton });
            paragraph.Inlines.Add(new Run(" suffix"));
            document.Blocks.Add(paragraph);

            var editor = new RichTextBox
            {
                Width = 420f,
                Height = 180f,
                Padding = new Thickness(8f),
                BorderThickness = 1f
            };
            editor.Document = document;

            var host = new Canvas
            {
                Width = 640f,
                Height = 320f
            };
            host.AddChild(editor);
            var uiRoot = new UiRoot(host);

            RunLayout(uiRoot, 640, 320);
            RunLayout(uiRoot, 640, 320);
            RunLayout(uiRoot, 640, 320);

            var ensureHostedDocumentChildLayout = typeof(RichTextBox).GetMethod("EnsureHostedDocumentChildLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            Assert.NotNull(ensureHostedDocumentChildLayout);
            ensureHostedDocumentChildLayout!.Invoke(editor, null);

            Assert.True(hostedButton.LayoutSlot.Width > 0f && hostedButton.LayoutSlot.Height > 0f);
            Assert.NotNull(hostedButton.Template);

            var editorPoint = new Vector2(editor.LayoutSlot.X + 24f, editor.LayoutSlot.Y + 24f);
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(editorPoint));
            Assert.False(hostedButton.IsMouseOver);

            var buttonPoint = GetCenter(hostedButton);
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(buttonPoint));
            Assert.True(hostedButton.IsMouseOver, $"Expected hosted button hover after move. path={uiRoot.LastPointerResolvePathForDiagnostics}");

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(editorPoint));
            Assert.False(hostedButton.IsMouseOver);
        }
        finally
        {
            RestoreApplicationResources(backup);
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();
        }
    }

    [Fact]
    public void UiRootInputPipeline_CachedRichTextBoxClickTarget_ShouldRetargetToHostedInlineButton()
    {
        var clickCount = 0;
        var hostedButton = new Button
        {
            Content = "Inline",
            Width = 72f,
            Height = 18f,
            FontSize = 11f,
            Padding = new Thickness(8f, 2f, 8f, 2f)
        };
        hostedButton.Click += (_, _) => clickCount++;

        var document = new FlowDocument();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run("Prefix "));
        paragraph.Inlines.Add(new InlineUIContainer { Child = hostedButton });
        paragraph.Inlines.Add(new Run(" suffix"));
        document.Blocks.Add(paragraph);

        var editor = new RichTextBox
        {
            Width = 420f,
            Height = 180f,
            Padding = new Thickness(8f),
            BorderThickness = 1f
        };
        editor.Document = document;

        var host = new Canvas
        {
            Width = 640f,
            Height = 320f
        };
        host.AddChild(editor);
        var uiRoot = new UiRoot(host);

        RunLayout(uiRoot, 640, 320);
        RunLayout(uiRoot, 640, 320);
        RunLayout(uiRoot, 640, 320);

        var ensureHostedDocumentChildLayout = typeof(RichTextBox).GetMethod("EnsureHostedDocumentChildLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        Assert.NotNull(ensureHostedDocumentChildLayout);
        ensureHostedDocumentChildLayout!.Invoke(editor, null);

        Assert.True(
            hostedButton.LayoutSlot.Width > 0f && hostedButton.LayoutSlot.Height > 0f,
            $"Expected hosted button layout to be positive. editor=({editor.LayoutSlot.X:0.###},{editor.LayoutSlot.Y:0.###},{editor.LayoutSlot.Width:0.###},{editor.LayoutSlot.Height:0.###}) button=({hostedButton.LayoutSlot.X:0.###},{hostedButton.LayoutSlot.Y:0.###},{hostedButton.LayoutSlot.Width:0.###},{hostedButton.LayoutSlot.Height:0.###})");

        var warmupPoint = new Vector2(editor.LayoutSlot.X + 24f, editor.LayoutSlot.Y + 24f);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(warmupPoint, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(warmupPoint, leftReleased: true));

        var tryResolveClickTargetFromCandidate = typeof(UiRoot).GetMethod("TryResolveClickTargetFromCandidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(tryResolveClickTargetFromCandidate);

        var buttonPoint = GetCenter(hostedButton);
        object?[] resolveArgs = [editor, buttonPoint, null];
        var resolved = Assert.IsType<bool>(tryResolveClickTargetFromCandidate!.Invoke(uiRoot, resolveArgs));
        Assert.True(resolved);
        Assert.Same(hostedButton, resolveArgs[2]);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(buttonPoint, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(buttonPoint, leftReleased: true));

        Assert.Equal(1, clickCount);
    }

    [Fact]
    public void RichTextBox_WithTemplateRoot_ShouldExcludeHostedInlineButtonFromRetainedRenderList()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var hostedButton = new Button
            {
                Content = "Inline",
                Width = 96f,
                Height = 28f
            };

            var document = new FlowDocument();
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run("Prefix "));
            paragraph.Inlines.Add(new InlineUIContainer { Child = hostedButton });
            paragraph.Inlines.Add(new Run(" suffix"));
            document.Blocks.Add(paragraph);

            var editor = new RichTextBox
            {
                Width = 420f,
                Height = 180f,
                Padding = new Thickness(8f),
                BorderThickness = 1f,
                Document = document
            };

            var host = new Canvas
            {
                Width = 640f,
                Height = 320f
            };
            host.AddChild(editor);

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot, 640, 320);
            RunLayout(uiRoot, 640, 320);
            RunLayout(uiRoot, 640, 320);
            uiRoot.SynchronizeRetainedRenderListForTests();

            Assert.True(editor.ApplyTemplate());
            Assert.DoesNotContain(hostedButton, editor.GetRetainedRenderChildren());
            Assert.DoesNotContain(hostedButton, uiRoot.GetRetainedVisualOrderForTests());
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void RichTextBoxView_PayloadFormatComboBox_WhenSidebarIsScrolled_ShouldCloseOnScroll()
    {
        var view = CreateView();
        var uiRoot = CreateUiRoot(view, 1360, 860);

        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);
        RunLayout(uiRoot, 1360, 860);

        var formatComboBox = Assert.IsType<ComboBox>(view.FindName("FormatComboBox"));
        var sidebarScrollViewer = FindAncestor<ScrollViewer>(formatComboBox);
        Assert.NotNull(sidebarScrollViewer);

        sidebarScrollViewer!.ScrollToVerticalOffset(120f);
        RunLayout(uiRoot, 1360, 860);
        Assert.True(sidebarScrollViewer.VerticalOffset > 0f);

        formatComboBox.IsDropDownOpen = true;
        RunLayout(uiRoot, 1360, 860);
        Assert.True(formatComboBox.IsDropDownOpen);
        Assert.True(formatComboBox.IsDropDownPopupOpenForTesting);

        sidebarScrollViewer.ScrollToVerticalOffset(0f);
        RunLayout(uiRoot, 1360, 860);

        Assert.False(formatComboBox.IsDropDownOpen);
        Assert.False(formatComboBox.IsDropDownPopupOpenForTesting);
    }

    [Fact]
    public void RichTextBoxView_InControlsCatalog_ShrinkThenGrow_ShouldMatchFreshLargeLayout()
    {
        const int largeWidth = 1360;
        const int largeHeight = 860;
        var smallViewports = new (int Width, int Height)[]
        {
            (540, 860),
            (620, 860),
            (760, 860),
            (760, 640)
        };

        var freshCatalog = CreateCatalog();
        var freshUiRoot = CreateUiRoot(freshCatalog, largeWidth, largeHeight);
        RunLayout(freshUiRoot, largeWidth, largeHeight);
        RunLayout(freshUiRoot, largeWidth, largeHeight);
        RunLayout(freshUiRoot, largeWidth, largeHeight);

        var freshView = GetSelectedRichTextBoxView(freshCatalog);
        var freshSnapshot = CaptureLayoutSnapshot(freshCatalog, freshView);

        var resizedCatalog = CreateCatalog();
        var resizedUiRoot = CreateUiRoot(resizedCatalog, largeWidth, largeHeight);
        RunLayout(resizedUiRoot, largeWidth, largeHeight);
        RunLayout(resizedUiRoot, largeWidth, largeHeight);
        RunLayout(resizedUiRoot, largeWidth, largeHeight);

        foreach (var (width, height) in smallViewports)
        {
            RunLayout(resizedUiRoot, width, height);
            RunLayout(resizedUiRoot, width, height);
            RunLayout(resizedUiRoot, width, height);
            RunLayout(resizedUiRoot, largeWidth, largeHeight);
            RunLayout(resizedUiRoot, largeWidth, largeHeight);
            RunLayout(resizedUiRoot, largeWidth, largeHeight);

            var resizedView = GetSelectedRichTextBoxView(resizedCatalog);
            var resizedSnapshot = CaptureLayoutSnapshot(resizedCatalog, resizedView);

            AssertLayoutSnapshotsClose(
                freshSnapshot,
                resizedSnapshot,
                $"after shrink viewport {width}x{height}");
        }
    }

    [Fact]
    public void RichTextBoxView_CreateEmbeddedButton_ShouldNotApplyLocalStyleOverrides()
    {
        var view = CreateView();

        AssertNoLocalEmbeddedButtonStyleOverrides(CreateEmbeddedButton(view, "Inline"));
        AssertNoLocalEmbeddedButtonStyleOverrides(CreateEmbeddedButton(view, "Block"));
    }

    private static RichTextBoxView CreateView()
    {
        return new RichTextBoxView
        {
            Width = 1360f,
            Height = 860f
        };
    }

    private static ControlsCatalogView CreateCatalog()
    {
        var catalog = new ControlsCatalogView();
        catalog.ShowControl("RichTextBox");
        return catalog;
    }

    private static UiRoot CreateUiRoot(UIElement content, int width, int height)
    {
        var host = new Canvas
        {
            Width = width,
            Height = height
        };
        host.AddChild(content);
        return new UiRoot(host);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }

    private static void LoadRootAppResources()
    {
        var appPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "App.xml"));
        Assert.True(File.Exists(appPath), $"Expected App.xml to exist at '{appPath}'.");
        XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);
    }

    private static ResourceSnapshot CaptureApplicationResources()
    {
        var resources = UiApplication.Current.Resources;
        return new ResourceSnapshot(resources.ToList(), resources.MergedDictionaries.ToList());
    }

    private static void RestoreApplicationResources(ResourceSnapshot snapshot)
    {
        TestApplicationResources.Restore(snapshot.Entries, snapshot.MergedDictionaries);
    }

    private static void InvokeButtonClick(Button button)
    {
        var onClick = typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(onClick);
        onClick!.Invoke(button, null);
    }

    private static T? FindFirstVisualChild<T>(UIElement root, Predicate<T>? predicate = null)
        where T : UIElement
    {
        if (root is T typed && (predicate == null || predicate(typed)))
        {
            return typed;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var match = FindFirstVisualChild(child, predicate);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static T? FindAncestor<T>(UIElement? element)
        where T : UIElement
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is T typed)
            {
                return typed;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetRecentOperations(RichTextBox editor)
    {
        var build = typeof(RichTextBox).GetMethod("BuildRecentOperationLines", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(build);
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(build!.Invoke(editor, null));
    }

    private static InlineUIContainer? FindFirstInlineUiContainer(FlowDocument document)
    {
        foreach (var paragraph in FlowDocumentPlainText.EnumerateParagraphs(document))
        {
            var match = FindFirstInlineUiContainer(paragraph.Inlines);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static InlineUIContainer? FindFirstInlineUiContainer(IEnumerable<Inline> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case InlineUIContainer inlineUiContainer:
                    return inlineUiContainer;
                case Span span:
                {
                    var nested = FindFirstInlineUiContainer(span.Inlines);
                    if (nested != null)
                    {
                        return nested;
                    }

                    break;
                }
            }
        }

        return null;
    }

    private static RichTextBoxView GetSelectedRichTextBoxView(ControlsCatalogView catalog)
    {
        var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
        return Assert.IsType<RichTextBoxView>(previewHost.Content);
    }

    private static RichTextBoxLayoutSnapshot CaptureLayoutSnapshot(ControlsCatalogView catalog, RichTextBoxView view)
    {
        var selectedControlLabel = Assert.IsType<Label>(catalog.FindName("SelectedControlLabel"));
        var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
        var historyToolbarPanel = Assert.IsType<WrapPanel>(view.FindName("HistoryToolbarPanel"));
        var formatToolbarPanel = Assert.IsType<WrapPanel>(view.FindName("FormatToolbarPanel"));
        var presetPanel = Assert.IsType<WrapPanel>(view.FindName("PresetPanel"));
        var payloadActionPanel = Assert.IsType<WrapPanel>(view.FindName("PayloadActionPanel"));
        var editor = Assert.IsType<RichTextBox>(view.FindName("Editor"));
        var payloadPreview = Assert.IsType<TextBox>(view.FindName("PayloadPreviewTextBox"));
        var formatComboBox = Assert.IsType<ComboBox>(view.FindName("FormatComboBox"));
        var selectionOpacitySlider = Assert.IsType<Slider>(view.FindName("SelectionOpacitySlider"));
        var heroSummaryLabel = Assert.IsType<TextBlock>(view.FindName("HeroSummaryLabel"));
        var canvasFooterLabel = Assert.IsType<TextBlock>(view.FindName("CanvasFooterLabel"));

        return new RichTextBoxLayoutSnapshot(
            selectedControlLabel.LayoutSlot,
            previewHost.LayoutSlot,
            historyToolbarPanel.LayoutSlot,
            formatToolbarPanel.LayoutSlot,
            presetPanel.LayoutSlot,
            payloadActionPanel.LayoutSlot,
            editor.LayoutSlot,
            payloadPreview.LayoutSlot,
            formatComboBox.LayoutSlot,
            selectionOpacitySlider.LayoutSlot,
            heroSummaryLabel.LayoutSlot,
            canvasFooterLabel.LayoutSlot,
            CountWrapPanelRows(historyToolbarPanel),
            CountWrapPanelRows(formatToolbarPanel),
            CountWrapPanelRows(presetPanel),
            CountWrapPanelRows(payloadActionPanel));
    }

    private static void AssertLayoutSnapshotsClose(RichTextBoxLayoutSnapshot expected, RichTextBoxLayoutSnapshot actual, string scenario)
    {
        if (AreClose(expected.SelectedControlLabel, actual.SelectedControlLabel) &&
            AreClose(expected.PreviewHost, actual.PreviewHost) &&
            AreClose(expected.HistoryToolbarPanel, actual.HistoryToolbarPanel) &&
            AreClose(expected.FormatToolbarPanel, actual.FormatToolbarPanel) &&
            AreClose(expected.PresetPanel, actual.PresetPanel) &&
            AreClose(expected.PayloadActionPanel, actual.PayloadActionPanel) &&
            AreClose(expected.Editor, actual.Editor) &&
            AreClose(expected.PayloadPreview, actual.PayloadPreview) &&
            AreClose(expected.FormatComboBox, actual.FormatComboBox) &&
            AreClose(expected.SelectionOpacitySlider, actual.SelectionOpacitySlider) &&
            AreClose(expected.HeroSummaryLabel, actual.HeroSummaryLabel) &&
            AreClose(expected.CanvasFooterLabel, actual.CanvasFooterLabel) &&
            expected.HistoryToolbarRows == actual.HistoryToolbarRows &&
            expected.FormatToolbarRows == actual.FormatToolbarRows &&
            expected.PresetRows == actual.PresetRows &&
            expected.PayloadActionRows == actual.PayloadActionRows)
        {
            return;
        }

        Assert.Fail(
            $"Shrink-then-grow layout diverged from fresh large layout {scenario}.{Environment.NewLine}Expected: {expected}{Environment.NewLine}Actual: {actual}");
    }

    private static int CountWrapPanelRows(WrapPanel panel)
    {
        var rowOrigins = new List<float>();

        foreach (var child in panel.Children)
        {
            if (child is not FrameworkElement element || element.LayoutSlot.Height <= 0f)
            {
                continue;
            }

            var y = element.LayoutSlot.Y;
            var matched = false;
            for (var i = 0; i < rowOrigins.Count; i++)
            {
                if (MathF.Abs(rowOrigins[i] - y) <= 0.5f)
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                rowOrigins.Add(y);
            }
        }

        return rowOrigins.Count;
    }

    private static bool AreClose(LayoutRect expected, LayoutRect actual)
    {
        return MathF.Abs(expected.X - actual.X) <= 0.5f &&
               MathF.Abs(expected.Y - actual.Y) <= 0.5f &&
               MathF.Abs(expected.Width - actual.Width) <= 0.5f &&
               MathF.Abs(expected.Height - actual.Height) <= 0.5f;
    }

    private static Vector2 GetCenter(UIElement element)
    {
        return new Vector2(
            element.LayoutSlot.X + (element.LayoutSlot.Width * 0.5f),
            element.LayoutSlot.Y + (element.LayoutSlot.Height * 0.5f));
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool leftPressed = false, bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = true,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private readonly record struct RichTextBoxLayoutSnapshot(
        LayoutRect SelectedControlLabel,
        LayoutRect PreviewHost,
        LayoutRect HistoryToolbarPanel,
        LayoutRect FormatToolbarPanel,
        LayoutRect PresetPanel,
        LayoutRect PayloadActionPanel,
        LayoutRect Editor,
        LayoutRect PayloadPreview,
        LayoutRect FormatComboBox,
        LayoutRect SelectionOpacitySlider,
        LayoutRect HeroSummaryLabel,
        LayoutRect CanvasFooterLabel,
        int HistoryToolbarRows,
        int FormatToolbarRows,
        int PresetRows,
        int PayloadActionRows)
    {
        public override string ToString()
        {
            return $"selectedLabel={FormatRect(SelectedControlLabel)}; previewHost={FormatRect(PreviewHost)}; history={FormatRect(HistoryToolbarPanel)} rows={HistoryToolbarRows}; format={FormatRect(FormatToolbarPanel)} rows={FormatToolbarRows}; preset={FormatRect(PresetPanel)} rows={PresetRows}; payloadActions={FormatRect(PayloadActionPanel)} rows={PayloadActionRows}; editor={FormatRect(Editor)}; payloadPreview={FormatRect(PayloadPreview)}; formatCombo={FormatRect(FormatComboBox)}; selectionOpacity={FormatRect(SelectionOpacitySlider)}; heroSummary={FormatRect(HeroSummaryLabel)}; canvasFooter={FormatRect(CanvasFooterLabel)}";
        }

        private static string FormatRect(LayoutRect rect)
        {
            return $"({rect.X:0.###},{rect.Y:0.###},{rect.Width:0.###},{rect.Height:0.###})";
        }
    }

    private sealed record ResourceSnapshot(
        List<KeyValuePair<object, object>> Entries,
        List<ResourceDictionary> MergedDictionaries);

    private static void AssertNoLocalEmbeddedButtonStyleOverrides(Button? button)
    {
        Assert.NotNull(button);
        var subject = button!;

        Assert.False(subject.HasLocalValue(FrameworkElement.WidthProperty));
        Assert.False(subject.HasLocalValue(FrameworkElement.HeightProperty));
        Assert.False(subject.HasLocalValue(FrameworkElement.FontSizeProperty));
        Assert.False(subject.HasLocalValue(Button.PaddingProperty));
        Assert.False(subject.HasLocalValue(Button.ForegroundProperty));
        Assert.False(subject.HasLocalValue(Button.BorderBrushProperty));
        Assert.False(subject.HasLocalValue(FrameworkElement.StyleProperty));
        Assert.False(subject.HasLocalValue(Control.TemplateProperty));
    }

    private static Button CreateEmbeddedButton(
        RichTextBoxView view,
        string content)
    {
        var method = typeof(RichTextBoxView).GetMethod("CreateEmbeddedButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<Button>(method!.Invoke(view, [content, string.Empty]));
    }
}
