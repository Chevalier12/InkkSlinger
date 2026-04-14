using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RichTextBoxScrollHostTests
{
    [Fact]
    public void DefaultFallbackTemplate_BuildsScrollViewerContentHost()
    {
        var editor = CreateLaidOutEditor(140f, 80f, "template test");

        var border = Assert.IsType<Border>(Assert.Single(editor.GetVisualChildren()));
        var contentHost = Assert.IsType<ScrollViewer>(border.Child);
        Assert.NotNull(contentHost.Content);
    }

    [Fact]
    public void DefaultFallbackTemplate_PropagatesScrollbarVisibilityToContentHost()
    {
        var editor = new RichTextBox
        {
            Width = 140f,
            Height = 80f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden
        };

        var host = new Canvas
        {
            Width = 180f,
            Height = 120f
        };

        host.AddChild(editor);
        host.Measure(new Vector2(host.Width, host.Height));
        host.Arrange(new LayoutRect(0f, 0f, host.Width, host.Height));

        var border = Assert.IsType<Border>(Assert.Single(editor.GetVisualChildren()));
        var contentHost = Assert.IsType<ScrollViewer>(border.Child);
        Assert.Equal(ScrollBarVisibility.Visible, contentHost.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Hidden, contentHost.VerticalScrollBarVisibility);
    }

    [Fact]
    public void DefaultFallbackTemplate_KeepsHostedHorizontalScrollBarLineButtons()
    {
        var editor = new RichTextBox
        {
            Width = 220f,
            Height = 96f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Document = BuildStructuredHorizontalIndicatorDocument()
        };

        var host = new Canvas
        {
            Width = 260f,
            Height = 136f
        };

        host.AddChild(editor);
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 260, 136, 16);

        var border = Assert.IsType<Border>(Assert.Single(editor.GetVisualChildren()));
        var contentHost = Assert.IsType<ScrollViewer>(border.Child);
        var horizontalBar = GetPrivateScrollBar(contentHost, "_horizontalBar");
        var lineUpButton = FindNamedVisualChild<RepeatButton>(horizontalBar, "PART_LineUpButton");
        var lineDownButton = FindNamedVisualChild<RepeatButton>(horizontalBar, "PART_LineDownButton");

        Assert.NotNull(lineUpButton);
        Assert.NotNull(lineDownButton);
        Assert.True(horizontalBar.ShowLineButtons, "Expected RichTextBox hosted horizontal scrollbar to keep its line buttons enabled.");
    }

    [Fact]
    public void ScrollApi_DelegatesToTemplateScrollViewer()
    {
        var editor = CreateLaidOutEditor(140f, 80f, string.Join("\n", Enumerable.Range(1, 40).Select(static i => $"Line {i}")));

        var border = Assert.IsType<Border>(Assert.Single(editor.GetVisualChildren()));
        var contentHost = Assert.IsType<ScrollViewer>(border.Child);

        editor.ScrollToVerticalOffset(48f);

        Assert.True(
            editor.VerticalOffset > 0f,
            $"editorOffset={editor.VerticalOffset:0.###}, hostOffset={contentHost.VerticalOffset:0.###}, viewport={contentHost.ViewportWidth:0.###}x{contentHost.ViewportHeight:0.###}, extent={contentHost.ExtentWidth:0.###}x{contentHost.ExtentHeight:0.###}, scrollable={editor.ScrollableHeight:0.###}");
        Assert.Equal(editor.VerticalOffset, contentHost.VerticalOffset, 3);
        Assert.Equal(editor.ViewportHeight, contentHost.ViewportHeight, 3);
        Assert.Equal(editor.ExtentHeight, contentHost.ExtentHeight, 3);
    }

    [Fact]
    public void TryGetCaretBounds_HostedScrollViewport_UsesViewportOffsets()
    {
        const string text = "line00\nline01\nline02\nline03\nline04 abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\nline05\nline06";
        var (uiRoot, editor, contentHost) = CreateUiRootEditorFixture(
            180f,
            76f,
            text,
            static editor =>
            {
                editor.TextWrapping = TextWrapping.NoWrap;
                editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            });

        var lineHeight = Math.Max(1f, UiTextRenderer.GetLineHeight(editor, editor.FontSize));
        var caretIndex = text.IndexOf("abcdefghijkl", System.StringComparison.Ordinal) + 12;
        editor.Select(caretIndex, 0);
        editor.ScrollToHorizontalOffset(56f);
        editor.ScrollToVerticalOffset(lineHeight * 3f);
        RunLayout(uiRoot, 220, 116, 32);

        Assert.True(contentHost.TryGetContentViewportClipRect(out var textRect));

        var layout = Layout(editor, textRect.Width);
        Assert.True(layout.TryGetCaretPosition(editor.CaretIndex, out var caretPosition));

        var expected = new LayoutRect(
            textRect.X + caretPosition.X - editor.HorizontalOffset,
            textRect.Y + caretPosition.Y - editor.VerticalOffset,
            1f,
            lineHeight);
        expected = IntersectRect(expected, textRect);

        Assert.True(editor.TryGetCaretBounds(out var actual));
        AssertRectClose(expected, actual);
    }

    [Fact]
    public void HandleMouseWheelFromInput_DelegatesToTemplateScrollHost()
    {
        var editor = CreateLaidOutEditor(140f, 80f, string.Join("\n", Enumerable.Range(1, 40).Select(static i => $"Line {i}")));
        var border = Assert.IsType<Border>(Assert.Single(editor.GetVisualChildren()));
        var contentHost = Assert.IsType<ScrollViewer>(border.Child);
        editor.SetFocusedFromInput(true);

        Assert.True(editor.HandleMouseWheelFromInput(-120));
        Assert.True(contentHost.VerticalOffset > 0f, $"Expected hosted scroll viewer to move, but offset stayed {contentHost.VerticalOffset:0.###}.");
    }

    [Fact]
    public void MouseWheel_OverScrollableRichTextBox_ShouldScrollHostedScrollViewer()
    {
        var (uiRoot, editor, contentHost) = CreateUiRootEditorFixture(140f, 80f, string.Join("\n", Enumerable.Range(1, 40).Select(static i => $"Line {i}")));
        editor.SetFocusedFromInput(true);

        Assert.True(
            contentHost.ExtentHeight > contentHost.ViewportHeight,
            $"Expected a vertically scrollable content host, but extent={contentHost.ExtentHeight:0.###} viewport={contentHost.ViewportHeight:0.###}.");

        var pointer = new Vector2(
            contentHost.LayoutSlot.X + MathF.Min(20f, contentHost.LayoutSlot.Width * 0.5f),
            contentHost.LayoutSlot.Y + MathF.Min(20f, contentHost.LayoutSlot.Height * 0.5f));

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: pointer));
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: pointer, wheelDelta: -120));

        Assert.True(
            contentHost.VerticalOffset > 0f,
            $"Expected wheel scrolling over RichTextBox to move the hosted ScrollViewer, but offset stayed {contentHost.VerticalOffset:0.###}. editorOffset={editor.VerticalOffset:0.###}, extent={contentHost.ExtentHeight:0.###}, viewport={contentHost.ViewportHeight:0.###}.");
    }

    [Fact]
    public void MouseWheel_OverUnfocusedScrollableRichTextBox_ShouldFocusAndScrollHostedScrollViewer()
    {
        var (uiRoot, editor, contentHost) = CreateUiRootEditorFixture(140f, 80f, string.Join("\n", Enumerable.Range(1, 40).Select(static i => $"Line {i}")));
        editor.SetFocusedFromInput(false);

        Assert.False(editor.IsFocused);
        Assert.True(
            contentHost.ExtentHeight > contentHost.ViewportHeight,
            $"Expected a vertically scrollable content host, but extent={contentHost.ExtentHeight:0.###} viewport={contentHost.ViewportHeight:0.###}.");

        var pointer = new Vector2(
            contentHost.LayoutSlot.X + MathF.Min(20f, contentHost.LayoutSlot.Width * 0.5f),
            contentHost.LayoutSlot.Y + MathF.Min(20f, contentHost.LayoutSlot.Height * 0.5f));

        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: pointer));
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: pointer, wheelDelta: -120));

        Assert.True(
            editor.IsFocused,
            $"Expected wheel scrolling over an unfocused RichTextBox to focus it, but IsFocused remained {editor.IsFocused}.");
        Assert.True(
            contentHost.VerticalOffset > 0f,
            $"Expected wheel scrolling over an unfocused RichTextBox to move the hosted ScrollViewer after focusing, but offset stayed {contentHost.VerticalOffset:0.###}. editorFocused={editor.IsFocused}, editorOffset={editor.VerticalOffset:0.###}, extent={contentHost.ExtentHeight:0.###}, viewport={contentHost.ViewportHeight:0.###}.");
    }

    [Fact]
    public void HorizontalScrollBarIndicator_WhenRichTextBoxScrollsOnBothAxes_ShouldMatchHostedScrollMetrics()
    {
        var editor = new RichTextBox
        {
            Width = 220f,
            Height = 96f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Document = BuildStructuredHorizontalIndicatorDocument()
        };

        var host = new Canvas
        {
            Width = 260f,
            Height = 136f
        };

        host.AddChild(editor);
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 260, 136, 16);

        var border = Assert.IsType<Border>(Assert.Single(editor.GetVisualChildren()));
        var contentHost = Assert.IsType<ScrollViewer>(border.Child);

        Assert.True(
            contentHost.ExtentWidth > contentHost.ViewportWidth + 0.01f,
            $"Expected a horizontally scrollable host, but extent={contentHost.ExtentWidth:0.###} viewport={contentHost.ViewportWidth:0.###}.");
        Assert.True(
            contentHost.ExtentHeight > contentHost.ViewportHeight + 0.01f,
            $"Expected a vertically scrollable host, but extent={contentHost.ExtentHeight:0.###} viewport={contentHost.ViewportHeight:0.###}.");

        var horizontalBar = GetPrivateScrollBar(contentHost, "_horizontalBar");
        var track = GetPrivateTrack(horizontalBar);
        var trackRect = track.GetTrackRect();
        var initialThumb = horizontalBar.GetThumbRectForInput();
        var expectedInitialFill = contentHost.ViewportWidth / contentHost.ExtentWidth;
        var actualInitialFill = initialThumb.Width / MathF.Max(0.01f, trackRect.Width);
        var actualInitialTravel = (initialThumb.X - trackRect.X) / MathF.Max(0.01f, trackRect.Width - initialThumb.Width);

        Assert.True(
            MathF.Abs(actualInitialFill - expectedInitialFill) <= 0.04f &&
            MathF.Abs(actualInitialTravel) <= 0.01f,
            $"Expected the horizontal scrollbar indicator to start at the left edge of the hosted track and reflect the hosted RichTextBox viewport fraction, but it did not. extent={contentHost.ExtentWidth:0.###}, viewport={contentHost.ViewportWidth:0.###}, offset={contentHost.HorizontalOffset:0.###}, track={FormatRect(trackRect)}, bar={FormatRect(track.LayoutSlot)}, thumb={FormatRect(initialThumb)}, expectedFill={expectedInitialFill:0.###}, actualFill={actualInitialFill:0.###}, actualTravel={actualInitialTravel:0.###}.");
    }

    [Fact]
    public void HorizontalScrollBarThumb_WhenDraggedToTrackEnd_ShouldReachFarRight()
    {
        var editor = new RichTextBox
        {
            Width = 220f,
            Height = 96f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Document = BuildStructuredHorizontalIndicatorDocument()
        };

        var host = new Canvas
        {
            Width = 260f,
            Height = 136f
        };

        host.AddChild(editor);
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 260, 136, 16);

        var border = Assert.IsType<Border>(Assert.Single(editor.GetVisualChildren()));
        var contentHost = Assert.IsType<ScrollViewer>(border.Child);
        var horizontalBar = GetPrivateScrollBar(contentHost, "_horizontalBar");
        var track = GetPrivateTrack(horizontalBar);
        var thumb = FindNamedVisualChild<Thumb>(horizontalBar, "PART_Thumb");
        Assert.NotNull(thumb);

        var thumbRect = horizontalBar.GetThumbRectForInput();
        var initialTrackRect = track.GetTrackRect();
        var start = GetCenter(thumbRect);
        var end = new Vector2(horizontalBar.LayoutSlot.X + horizontalBar.LayoutSlot.Width - 2f, start.Y);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        Assert.Same(thumb, FocusManager.GetCapturedPointerElement());

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, pointerMoved: true));
        RunLayout(uiRoot, 260, 136, 32);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));
        RunLayout(uiRoot, 260, 136, 48);

        var maxOffset = MathF.Max(0f, contentHost.ExtentWidth - contentHost.ViewportWidth);
        var finalThumb = horizontalBar.GetThumbRectForInput();
        var finalTrackRect = track.GetTrackRect();
        var travelGap = (finalTrackRect.X + finalTrackRect.Width) - (finalThumb.X + finalThumb.Width);
        var barDiagnostics = horizontalBar.GetScrollBarSnapshotForDiagnostics();
        var trackDiagnostics = track.GetTrackSnapshotForDiagnostics();

        Assert.True(
            contentHost.HorizontalOffset >= maxOffset - 1f && travelGap <= 1f,
            $"Expected dragging the hosted RichTextBox horizontal thumb to the track end to also place the thumb at the far right when content reached max scroll, but offset={contentHost.HorizontalOffset:0.###}, maxOffset={maxOffset:0.###}, gap={travelGap:0.###}, initialTrack={FormatRect(initialTrackRect)}, initialThumb={FormatRect(thumbRect)}, track={FormatRect(finalTrackRect)}, thumb={FormatRect(finalThumb)}, barValue={barDiagnostics.Value:0.###}, barMax={barDiagnostics.Maximum:0.###}, barViewport={barDiagnostics.ViewportSize:0.###}, dragOrigin={barDiagnostics.ThumbDragOriginTravel:0.###}, dragAccumulated={barDiagnostics.ThumbDragAccumulatedDelta:0.###}, trackValue={trackDiagnostics.Value:0.###}, trackMax={trackDiagnostics.Maximum:0.###}, trackViewport={trackDiagnostics.ViewportSize:0.###}, trackRectRuntime=({trackDiagnostics.TrackRectX:0.###},{trackDiagnostics.TrackRectY:0.###},{trackDiagnostics.TrackRectWidth:0.###},{trackDiagnostics.TrackRectHeight:0.###}), thumbRectRuntime=({trackDiagnostics.ThumbRectX:0.###},{trackDiagnostics.ThumbRectY:0.###},{trackDiagnostics.ThumbRectWidth:0.###},{trackDiagnostics.ThumbRectHeight:0.###}).");
    }

    [Fact]
    public void RichTextBoxStudio_HorizontalThumb_WhenDraggedPartway_ShouldStayInSyncWithScrollOffset()
    {
        var snapshot = SnapshotApplicationResources();
        try
        {
            TestApplicationResources.LoadDemoAppResources();

            var view = new RichTextBoxView();
            var presetPanel = Assert.IsType<WrapPanel>(view.FindName("PresetPanel"));
            var structureButton = FindButtonByContent(presetPanel, "Structure");
            var editor = Assert.IsType<RichTextBox>(view.FindName("Editor"));
            var uiRoot = new UiRoot(view);

            RunLayout(uiRoot, 760, 900, 16);
            structureButton.InvokeFromInput();
            RunLayout(uiRoot, 760, 900, 32);
            RunLayout(uiRoot, 760, 900, 48);

            var border = Assert.IsType<Border>(Assert.Single(editor.GetVisualChildren()));
            var contentHost = Assert.IsType<ScrollViewer>(border.Child);
            var horizontalBar = GetPrivateScrollBar(contentHost, "_horizontalBar");
            var track = GetPrivateTrack(horizontalBar);
            var thumb = FindNamedVisualChild<Thumb>(horizontalBar, "PART_Thumb");
            Assert.NotNull(thumb);

            Assert.True(
                contentHost.ExtentWidth > contentHost.ViewportWidth + 0.01f,
                $"Expected the RichTextBox Studio editor to overflow horizontally after loading the Structure preset, but extent={contentHost.ExtentWidth:0.###} viewport={contentHost.ViewportWidth:0.###}.");

            var trackRect = track.GetTrackRect();
            var thumbRect = horizontalBar.GetThumbRectForInput();
            var travelLength = MathF.Max(0f, trackRect.Width - thumbRect.Width);
            var start = GetCenter(thumbRect);
            var partialTravel = travelLength * 0.35f;
            var partial = new Vector2(start.X + partialTravel, start.Y);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, pointerMoved: true));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
            Assert.Same(thumb, FocusManager.GetCapturedPointerElement());

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(partial, pointerMoved: true));
            RunLayout(uiRoot, 760, 900, 64);

            var partialThumb = horizontalBar.GetThumbRectForInput();
            var partialTrackRect = track.GetTrackRect();
            var maxOffset = MathF.Max(0f, contentHost.ExtentWidth - contentHost.ViewportWidth);
            var thumbTravel = partialThumb.X - partialTrackRect.X;
            var thumbTravelRatio = thumbTravel / MathF.Max(0.01f, partialTrackRect.Width - partialThumb.Width);
            var offsetRatio = contentHost.HorizontalOffset / MathF.Max(0.01f, maxOffset);
            var barDiagnostics = horizontalBar.GetScrollBarSnapshotForDiagnostics();
            var trackDiagnostics = track.GetTrackSnapshotForDiagnostics();

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(partial, leftReleased: true));
            RunLayout(uiRoot, 760, 900, 80);

            Assert.True(
                MathF.Abs(thumbTravelRatio - offsetRatio) <= 0.12f,
                $"Expected the RichTextBox Studio horizontal thumb position to stay proportional to the hosted scroll offset during a partial drag, but thumbRatio={thumbTravelRatio:0.###}, offsetRatio={offsetRatio:0.###}, offset={contentHost.HorizontalOffset:0.###}, maxOffset={maxOffset:0.###}, track={FormatRect(partialTrackRect)}, thumb={FormatRect(partialThumb)}, barValue={barDiagnostics.Value:0.###}, barMax={barDiagnostics.Maximum:0.###}, barViewport={barDiagnostics.ViewportSize:0.###}, dragOrigin={barDiagnostics.ThumbDragOriginTravel:0.###}, dragAccumulated={barDiagnostics.ThumbDragAccumulatedDelta:0.###}, trackValue={trackDiagnostics.Value:0.###}, trackMax={trackDiagnostics.Maximum:0.###}, trackViewport={trackDiagnostics.ViewportSize:0.###}, trackRectRuntime=({trackDiagnostics.TrackRectX:0.###},{trackDiagnostics.TrackRectY:0.###},{trackDiagnostics.TrackRectWidth:0.###},{trackDiagnostics.TrackRectHeight:0.###}), thumbRectRuntime=({trackDiagnostics.ThumbRectX:0.###},{trackDiagnostics.ThumbRectY:0.###},{trackDiagnostics.ThumbRectWidth:0.###},{trackDiagnostics.ThumbRectHeight:0.###}).");
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    private static FlowDocument BuildStructuredHorizontalIndicatorDocument()
    {
        var document = new FlowDocument();

        var intro = new Paragraph();
        intro.Inlines.Add(new Run("The welcome document mixes inline formatting, hyperlink content, bullets, and a status table."));
        document.Blocks.Add(intro);

        var bullets = new InkkSlinger.List();
        bullets.Items.Add(CreateListItem("Round-trip the full document through Flow XML, XAML, XamlPackage, RTF, or plain text."));
        bullets.Items.Add(CreateListItem("Swap into embedded UI mode to click hosted buttons inside the document."));
        bullets.Items.Add(CreateListItem("Keep the canvas narrow enough that the status grid still needs horizontal scrolling."));
        document.Blocks.Add(bullets);

        var table = new Table();
        var rowGroup = new TableRowGroup();
        rowGroup.Rows.Add(CreateStatusRow("Mode", "Interactive"));
        rowGroup.Rows.Add(CreateStatusRow("Selection", "Live metrics"));
        rowGroup.Rows.Add(CreateStatusRow("Clipboard", "Flow XML / XAML / XamlPackage / RTF / plain text"));
        rowGroup.Rows.Add(CreateStatusRow("Commands", "Ctrl+B / Ctrl+I / Ctrl+U / PageUp / Ctrl+Backspace"));
        table.RowGroups.Add(rowGroup);
        document.Blocks.Add(table);

        for (var i = 0; i < 8; i++)
        {
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run($"Detail row {i + 1}: the horizontal bar should describe the actual width of the table content rather than a stale or wrapped width estimate."));
            document.Blocks.Add(paragraph);
        }

        return document;
    }

    private static ListItem CreateListItem(string text)
    {
        var item = new ListItem();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(text));
        item.Blocks.Add(paragraph);
        return item;
    }

    private static TableRow CreateStatusRow(string label, string value)
    {
        var row = new TableRow();
        row.Cells.Add(CreateCell(label));
        row.Cells.Add(CreateCell(value));
        return row;
    }

    private static TableCell CreateCell(string text)
    {
        var cell = new TableCell();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(text));
        cell.Blocks.Add(paragraph);
        return cell;
    }

    private static RichTextBox CreateLaidOutEditor(float width, float height, string text)
    {
        var editor = new RichTextBox
        {
            Width = width,
            Height = height
        };

        DocumentEditing.ReplaceAllText(editor.Document, text);

        var host = new Canvas
        {
            Width = width + 40f,
            Height = height + 40f
        };

        host.AddChild(editor);
        host.Measure(new Vector2(host.Width, host.Height));
        host.Arrange(new LayoutRect(0f, 0f, host.Width, host.Height));
        return editor;
    }

    private static (UiRoot UiRoot, RichTextBox Editor, ScrollViewer ContentHost) CreateUiRootEditorFixture(
        float width,
        float height,
        string text,
        Action<RichTextBox>? configure)
    {
        var editor = new RichTextBox
        {
            Width = width,
            Height = height
        };

        configure?.Invoke(editor);
        DocumentEditing.ReplaceAllText(editor.Document, text);

        var host = new Canvas
        {
            Width = width + 40f,
            Height = height + 40f
        };

        host.AddChild(editor);
        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, (int)host.Width, (int)host.Height, 16);

        var border = Assert.IsType<Border>(Assert.Single(editor.GetVisualChildren()));
        var contentHost = Assert.IsType<ScrollViewer>(border.Child);
        return (uiRoot, editor, contentHost);
    }

    private static (UiRoot UiRoot, RichTextBox Editor, ScrollViewer ContentHost) CreateUiRootEditorFixture(float width, float height, string text)
    {
        return CreateUiRootEditorFixture(width, height, text, configure: null);
    }

    private static ScrollBar GetPrivateScrollBar(ScrollViewer viewer, string fieldName)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ScrollBar>(field!.GetValue(viewer));
    }

    private static Track GetPrivateTrack(ScrollBar scrollBar)
    {
        var field = typeof(ScrollBar).GetField("_track", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<Track>(field!.GetValue(scrollBar));
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"({rect.X:0.###},{rect.Y:0.###},{rect.Width:0.###},{rect.Height:0.###})";
    }

    private static DocumentLayoutResult Layout(RichTextBox editor, float availableWidth)
    {
        var typography = UiTextRenderer.ResolveTypography(editor, editor.FontSize);
        var settings = new DocumentLayoutSettings(
            AvailableWidth: availableWidth,
            Typography: typography,
            Wrapping: editor.TextWrapping,
            Foreground: Color.White,
            LineHeight: Math.Max(1f, UiTextRenderer.GetLineHeight(typography)),
            ListIndent: 16f,
            ListMarkerGap: 4f,
            TableCellPadding: 4f,
            TableBorderThickness: 1f);
        return new DocumentLayoutEngine().Layout(editor.Document, settings);
    }

    private static LayoutRect IntersectRect(LayoutRect left, LayoutRect right)
    {
        var x = MathF.Max(left.X, right.X);
        var y = MathF.Max(left.Y, right.Y);
        var rightEdge = MathF.Min(left.X + left.Width, right.X + right.Width);
        var bottomEdge = MathF.Min(left.Y + left.Height, right.Y + right.Height);
        return new LayoutRect(x, y, MathF.Max(0f, rightEdge - x), MathF.Max(0f, bottomEdge - y));
    }

    private static void AssertRectClose(LayoutRect expected, LayoutRect actual, int precision = 3)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Width, actual.Width, precision);
        Assert.Equal(expected.Height, actual.Height, precision);
    }

    private static TElement? FindNamedVisualChild<TElement>(UIElement root, string name)
        where TElement : FrameworkElement
    {
        if (root is TElement typed && typed.Name == name)
        {
            return typed;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindNamedVisualChild<TElement>(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Button FindButtonByContent(WrapPanel host, string content)
    {
        return Assert.IsType<Button>(host.Children.Single(child => child is Button button && string.Equals(button.Content as string, content, StringComparison.Ordinal)));
    }

    private static Dictionary<object, object> SnapshotApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
    }

    private static void RestoreApplicationResources(Dictionary<object, object> snapshot)
    {
        TestApplicationResources.Restore(snapshot);
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
    }

    private static InputDelta CreateDelta(
        bool pointerMoved,
        Vector2 position,
        int wheelDelta = 0)
    {
        var previous = new InputSnapshot(default, default, position);
        var current = new InputSnapshot(default, default, position);
        return new InputDelta
        {
            Previous = previous,
            Current = current,
            PressedKeys = [],
            ReleasedKeys = [],
            TextInput = [],
            PointerMoved = pointerMoved,
            WheelDelta = wheelDelta,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = [],
            ReleasedKeys = [],
            TextInput = [],
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}