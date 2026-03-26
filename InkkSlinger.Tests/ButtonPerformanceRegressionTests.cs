using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ButtonPerformanceRegressionTests
{
    [Fact]
    public void Measure_NonTemplatedButton_DoesNotAttemptTemplateApplication()
    {
        Control.ResetMeasureTemplateApplyAttemptCountForTests();

        var button = new ProbeButton
        {
            Content = "Calendar"
        };

        button.Measure(new Vector2(240f, 80f));

        Assert.Equal(0, Control.GetMeasureTemplateApplyAttemptCountForTests());
        Assert.False(button.HasTemplateRootForTesting);
    }

    [Fact]
    public void Measure_PlainTextButtonWithText_MatchesExpectedDesiredSize()
    {
        var button = new Button
        {
            Content = "Open Calendar",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };

        button.Measure(new Vector2(300f, 120f));

        var layout = TextLayout.Layout(button.GetContentText(), UiTextRenderer.ResolveTypography(button), button.FontSize, float.PositiveInfinity, TextWrapping.NoWrap);
        var expected = new Vector2(
            layout.Size.X + button.Padding.Horizontal + (button.BorderThickness * 2f),
            layout.Size.Y + button.Padding.Vertical + (button.BorderThickness * 2f));

        AssertClose(expected, button.DesiredSize);
    }

    [Fact]
    public void Measure_PlainTextButtonWithoutText_PreservesChromeOnlyMinimum()
    {
        var button = new Button
        {
            Content = string.Empty,
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };

        button.Measure(new Vector2(300f, 120f));

        var expected = new Vector2(
            button.Padding.Horizontal + (button.BorderThickness * 2f),
            button.Padding.Vertical + (button.BorderThickness * 2f));

        AssertClose(expected, button.DesiredSize);
    }

    [Fact]
    public void Measure_PlainTextWrappedButton_MatchesExpectedDesiredSize()
    {
        var button = new Button
        {
            Content = "Wednesday Thursday Friday Saturday",
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };
        var availableSize = new Vector2(90f, 300f);

        button.Measure(availableSize);

        var textWidth = availableSize.X - button.Padding.Horizontal - (button.BorderThickness * 2f);
        var layout = TextLayout.Layout(button.GetContentText(), UiTextRenderer.ResolveTypography(button), button.FontSize, textWidth, TextWrapping.Wrap);
        var expected = new Vector2(
            layout.Size.X + button.Padding.Horizontal + (button.BorderThickness * 2f),
            layout.Size.Y + button.Padding.Vertical + (button.BorderThickness * 2f));

        AssertClose(expected, button.DesiredSize);
    }

    [Fact]
    public void Measure_PlainTextSingleLineNoWrapButton_DoesNotInvokeTextLayout()
    {
        TextLayout.ResetMetricsForTests();

        var button = new Button
        {
            Content = "31",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };

        button.Measure(new Vector2(300f, 120f));

        var expected = new Vector2(
            UiTextRenderer.MeasureWidth(button, button.GetContentText(), button.FontSize) + button.Padding.Horizontal + (button.BorderThickness * 2f),
            UiTextRenderer.GetLineHeight(button, button.FontSize) + button.Padding.Vertical + (button.BorderThickness * 2f));

        AssertClose(expected, button.DesiredSize);
        Assert.Equal(0, TextLayout.GetMetricsSnapshot().BuildCount);
    }

    [Fact]
    public void Measure_PlainTextSingleLineNoWrapButton_ReusesMeasureAcrossAvailableSizeChanges()
    {
        var button = new Button
        {
            Content = "31",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };

        button.Measure(new Vector2(32f, 24f));
        button.Measure(new Vector2(300f, 120f));

        Assert.Equal(2, button.MeasureCallCount);
        Assert.Equal(1, button.MeasureWorkCount);
    }

    [Fact]
    public void Measure_TemplatedButton_StillUsesTemplatePath()
    {
        var button = new ProbeButton
        {
            Template = new ControlTemplate(_ => new FixedMeasureElement(new Vector2(72f, 24f)))
        };

        button.Measure(new Vector2(300f, 120f));

        Assert.True(button.HasTemplateRootForTesting);
        AssertClose(new Vector2(72f, 24f), button.DesiredSize);
    }

    [Fact]
    public void ResolveRenderedLineWidth_UsesCachedLayoutWidths_WhenAvailable()
    {
        Button.ResetRenderLineWidthFallbackCountForTests();

        var layout = TextLayout.Layout("Alpha\nBeta", new UiTypography("Segoe UI", 14f, "Normal", "Normal"), 14f, float.PositiveInfinity, TextWrapping.NoWrap);

        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var width = Button.ResolveRenderedLineWidth(layout, i, layout.Lines[i], 14f);
            Assert.Equal(layout.LineWidths[i], width);
        }

        Assert.Equal(0, Button.GetRenderLineWidthFallbackCountForTests());
    }

    [Fact]
    public void ResolveRenderedLineWidth_FallsBack_WhenCachedWidthIsMissing()
    {
        Button.ResetRenderLineWidthFallbackCountForTests();

        var layout = new TextLayout.TextLayoutResult(new[] { "Fallback" }, System.Array.Empty<float>(), new Vector2(0f, 14f));

        var width = Button.ResolveRenderedLineWidth(layout, 0, "Fallback", 14f);

        Assert.True(width > 0f);
        Assert.Equal(1, Button.GetRenderLineWidthFallbackCountForTests());
    }

    [Fact]
    public void PrepareTextRenderPlan_WithText_UsesTextLayoutAndFontMetrics()
    {
        TextLayout.ResetMetricsForTests();
        UiTextRenderer.ResetTimingForTests();
        Button.ResetTimingForTests();

        var button = new Button
        {
            Content = "31",
            Width = 32f,
            Height = 24f,
            Padding = new Thickness(0f),
            BorderThickness = 0f
        };

        var plan = button.PrepareTextRenderPlanForTests(new LayoutRect(0f, 0f, 32f, 24f));
        var buttonTiming = Button.GetTimingSnapshotForTests();
        var textMetrics = TextLayout.GetMetricsSnapshot();
        var fontTiming = UiTextRenderer.GetTimingSnapshotForTests();

        Assert.True(plan.HasValue);
        Assert.Single(plan.Value.LineDraws);
        Assert.True(buttonTiming.RenderTextPreparationElapsedTicks > 0);
        Assert.Equal(1, buttonTiming.RenderTextPreparationCallCount);
        Assert.True(textMetrics.BuildCount > 0);
        Assert.True(fontTiming.MeasureWidthCallCount > 0);
        Assert.True(fontTiming.GetLineHeightCallCount > 0);
    }

    [Fact]
    public void PrepareTextRenderPlan_WithoutText_SkipsTextLayoutAndFontMetrics()
    {
        TextLayout.ResetMetricsForTests();
        UiTextRenderer.ResetTimingForTests();
        Button.ResetTimingForTests();

        var button = new Button
        {
            Content = string.Empty,
            Width = 32f,
            Height = 24f,
            Padding = new Thickness(0f),
            BorderThickness = 0f
        };

        var plan = button.PrepareTextRenderPlanForTests(new LayoutRect(0f, 0f, 32f, 24f));
        var buttonTiming = Button.GetTimingSnapshotForTests();
        var textMetrics = TextLayout.GetMetricsSnapshot();
        var fontTiming = UiTextRenderer.GetTimingSnapshotForTests();

        Assert.False(plan.HasValue);
        Assert.Equal(0, buttonTiming.RenderTextPreparationCallCount);
        Assert.Equal(0, textMetrics.BuildCount);
        Assert.Equal(0, fontTiming.MeasureWidthCallCount);
        Assert.Equal(0, fontTiming.GetLineHeightCallCount);
    }

    [Fact]
    public void PrepareTextRenderPlan_ReusesCachedPlan_WhenLayoutAndTextAreStable()
    {
        TextLayout.ResetMetricsForTests();
        UiTextRenderer.ResetTimingForTests();
        Button.ResetTimingForTests();

        var button = new Button
        {
            Content = "^",
            Width = 32f,
            Height = 24f,
            Padding = new Thickness(0f),
            BorderThickness = 0f,
            FontSize = 8f
        };

        var firstPlan = button.PrepareTextRenderPlanForTests(new LayoutRect(0f, 0f, 32f, 24f));
        var secondPlan = button.PrepareTextRenderPlanForTests(new LayoutRect(0f, 0f, 32f, 24f));
        var buttonTiming = Button.GetTimingSnapshotForTests();
        var textMetrics = TextLayout.GetMetricsSnapshot();

        Assert.True(firstPlan.HasValue);
        Assert.True(secondPlan.HasValue);
        Assert.Equal(firstPlan.Value.LineDraws[0].Text, secondPlan.Value.LineDraws[0].Text);
        Assert.Equal(1, buttonTiming.RenderTextPreparationCallCount);
        Assert.Equal(1, textMetrics.BuildCount);
    }

    [Fact]
    public void PrepareTextRenderPlans_ForFortyTwoButtons_TextVsNoText_ShowsPreparationCostGap()
    {
        var withText = MeasureRenderPreparation(buttonCount: 42, includeText: true);
        var withoutText = MeasureRenderPreparation(buttonCount: 42, includeText: false);

        Assert.Equal(42, withText.RenderTextPreparationCallCount);
        Assert.Equal(0, withoutText.RenderTextPreparationCallCount);
        Assert.True(withText.TextLayoutBuildCount > withoutText.TextLayoutBuildCount);
        Assert.True(withText.FontMeasureWidthCallCount > withoutText.FontMeasureWidthCallCount);
        Assert.True(withText.RenderTextPreparationElapsedTicks > withoutText.RenderTextPreparationElapsedTicks);
    }

    private static RenderPreparationMetrics MeasureRenderPreparation(int buttonCount, bool includeText)
    {
        TextLayout.ResetMetricsForTests();
        UiTextRenderer.ResetTimingForTests();
        Button.ResetTimingForTests();

        for (var i = 0; i < buttonCount; i++)
        {
            var button = new Button
            {
                Content = includeText ? (i + 1).ToString() : string.Empty,
                Width = 32f,
                Height = 24f,
                Padding = new Thickness(0f),
                BorderThickness = 0f
            };

            _ = button.PrepareTextRenderPlanForTests(new LayoutRect(0f, 0f, 32f, 24f));
        }

        var buttonTiming = Button.GetTimingSnapshotForTests();
        var textMetrics = TextLayout.GetMetricsSnapshot();
        var fontTiming = UiTextRenderer.GetTimingSnapshotForTests();
        return new RenderPreparationMetrics(
            buttonTiming.RenderTextPreparationElapsedTicks,
            buttonTiming.RenderTextPreparationCallCount,
            textMetrics.BuildCount,
            fontTiming.MeasureWidthCallCount,
            fontTiming.GetLineHeightCallCount);
    }

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.01f, expected.X + 0.01f);
        Assert.InRange(actual.Y, expected.Y - 0.01f, expected.Y + 0.01f);
    }

    private sealed class ProbeButton : Button
    {
        public bool HasTemplateRootForTesting => HasTemplateRoot;
    }

    private sealed class FixedMeasureElement(Vector2 desiredSize) : FrameworkElement
    {
        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            _ = availableSize;
            return desiredSize;
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            return finalSize;
        }
    }

    private readonly record struct RenderPreparationMetrics(
        long RenderTextPreparationElapsedTicks,
        int RenderTextPreparationCallCount,
        int TextLayoutBuildCount,
        int FontMeasureWidthCallCount,
        int FontGetLineHeightCallCount);
}
