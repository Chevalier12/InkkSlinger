namespace InkkSlinger;

public sealed class InkkOopsTextBlockDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 30;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        if (element is not TextBlock textBlock)
        {
            return;
        }

        builder.Add("text", Escape(textBlock.Text));
        var typography = UiTextRenderer.ResolveTypography(textBlock, textBlock.FontSize);
        var lineHeight = UiTextRenderer.GetLineHeight(typography);
        var inkBounds = string.IsNullOrWhiteSpace(textBlock.LastRenderedLayoutTextForTests)
            ? new LayoutRect(0f, 0f, 0f, 0f)
            : UiTextRenderer.GetInkBoundsForTests(typography, textBlock.LastRenderedLayoutTextForTests);
        builder.Add("renderLines", textBlock.LastRenderedLineCountForTests);
        builder.Add("renderWidth", $"{textBlock.LastRenderedLayoutWidthForTests:0.##}");
        builder.Add("desired", $"{textBlock.DesiredSize.X:0.##},{textBlock.DesiredSize.Y:0.##}");
        builder.Add("previousAvailable", $"{textBlock.PreviousAvailableSizeForTests.X:0.##},{textBlock.PreviousAvailableSizeForTests.Y:0.##}");
        builder.Add("measureCalls", textBlock.MeasureCallCount);
        builder.Add("measureWork", textBlock.MeasureWorkCount);
        builder.Add("arrangeCalls", textBlock.ArrangeCallCount);
        builder.Add("arrangeWork", textBlock.ArrangeWorkCount);
        builder.Add("measureValid", textBlock.IsMeasureValidForTests);
        builder.Add("arrangeValid", textBlock.IsArrangeValidForTests);
        builder.Add("lineHeight", $"{lineHeight:0.###}");
        builder.Add("inkBounds", $"{inkBounds.X:0.##},{inkBounds.Y:0.##},{inkBounds.Width:0.##},{inkBounds.Height:0.##}");
        if (!string.IsNullOrWhiteSpace(textBlock.LastRenderedLayoutTextForTests))
        {
            builder.Add("renderText", Escape(textBlock.LastRenderedLayoutTextForTests));
        }
    }

    private static string Escape(string text)
    {
        return text.Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
