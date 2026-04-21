using System;

namespace InkkSlinger;

public sealed class SourceEditorGutterObjectObserver : InkkOopsObjectObserver
{
    public SourceEditorGutterObjectObserver(string targetName = "SourceEditor")
        : base(targetName)
    {
    }

    protected override void Observe(InkkOopsObjectObserverContext context, UIElement element, InkkOopsObjectObserverDumpBuilder builder)
    {
        _ = context;
        if (element is not IDE_Editor editor)
        {
            builder.Add("elementType", element.GetType().Name);
            return;
        }

        var gutter = editor.LineNumberPresenter;
        var extentHeightPerLine = editor.LineCount > 0
            ? editor.ExtentHeight / editor.LineCount
            : 0f;

        builder.Add("elementType", nameof(IDE_Editor));
        builder.Add("selectionStart", editor.SelectionStart);
        builder.Add("selectionLength", editor.SelectionLength);
        builder.Add("lineCount", editor.LineCount);
        builder.Add("estimatedLineHeight", editor.EstimatedLineHeight);
        builder.Add("horizontalOffset", editor.HorizontalOffset);
        builder.Add("verticalOffset", editor.VerticalOffset);
        builder.Add("viewportWidth", editor.ViewportWidth);
        builder.Add("viewportHeight", editor.ViewportHeight);
        builder.Add("extentWidth", editor.ExtentWidth);
        builder.Add("extentHeight", editor.ExtentHeight);
        builder.Add("extentHeightPerLine", extentHeightPerLine);
        builder.Add("scrollableHeight", editor.ScrollableHeight);
        builder.Add("gutterLineHeight", gutter.LineHeight);
        builder.Add("gutterVerticalLineOffset", gutter.VerticalLineOffset);
        builder.Add("gutterFirstVisibleLine", gutter.FirstVisibleLine);
        builder.Add("gutterVisibleLineCount", gutter.VisibleLineCount);
        builder.Add("gutterFirstVisibleText", gutter.VisibleLineCount > 0 ? gutter.VisibleLineTexts[0] : string.Empty);
        builder.Add("gutterLastVisibleText", gutter.VisibleLineCount > 0 ? gutter.VisibleLineTexts[^1] : string.Empty);
        builder.Add("gutterVsEstimatedDelta", MathF.Abs(gutter.LineHeight - editor.EstimatedLineHeight));
        builder.Add("gutterVsExtentDelta", MathF.Abs(gutter.LineHeight - extentHeightPerLine));

        if (editor.TryGetCaretBounds(out var caretBounds))
        {
            builder.Add("hasCaretBounds", true);
            builder.Add("caretBoundsX", caretBounds.X);
            builder.Add("caretBoundsY", caretBounds.Y);
            builder.Add("caretBoundsWidth", caretBounds.Width);
            builder.Add("caretBoundsHeight", caretBounds.Height);
            builder.Add("gutterVsCaretDelta", MathF.Abs(gutter.LineHeight - caretBounds.Height));
        }
        else
        {
            builder.Add("hasCaretBounds", false);
            builder.Add("caretBounds", "unavailable");
        }
    }
}