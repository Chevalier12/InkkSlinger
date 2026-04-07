using System.Reflection;
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
    public void Measure_ButtonWithWrappedTextBlockContent_IncreasesDesiredHeight()
    {
        var textBlock = new TextBlock
        {
            Text = "Wednesday Thursday Friday Saturday",
            TextWrapping = TextWrapping.Wrap
        };
        var button = new Button
        {
            Content = textBlock,
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };
        var availableSize = new Vector2(90f, 300f);

        button.Measure(availableSize);
        var singleLineHeight = UiTextRenderer.GetLineHeight(button, button.FontSize) + button.Padding.Vertical + (button.BorderThickness * 2f);

        Assert.True(
            button.DesiredSize.Y > singleLineHeight,
            $"Expected wrapped TextBlock content to increase button height beyond a single line. desired={button.DesiredSize}, singleLineHeight={singleLineHeight:0.##}");
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
    public void Measure_ButtonWithWrappedTextBlockContent_DoesNotReuseMeasureAcrossAvailableSizeChanges()
    {
        var textBlock = new TextBlock
        {
            Text = "Wednesday Thursday Friday Saturday",
            TextWrapping = TextWrapping.Wrap
        };
        var button = new Button
        {
            Content = textBlock,
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };

        button.Measure(new Vector2(300f, 300f));
        var wideDesired = button.DesiredSize;

        button.Measure(new Vector2(90f, 300f));
        var narrowDesired = button.DesiredSize;

        // Measure work must run again for the narrower pass.
        Assert.Equal(2, button.MeasureWorkCount);
        // A narrower constraint must produce a taller desired size.
        Assert.True(narrowDesired.Y > wideDesired.Y, $"Expected height to increase when width narrows (wide={wideDesired.Y}, narrow={narrowDesired.Y})");
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

        var layout = new TextLayout.TextLayoutResult(
            new[] { "Fallback" },
            System.Array.Empty<float>(),
            new Vector2(0f, 14f),
            0f,
            float.PositiveInfinity);

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

    [Fact]
    public void TelemetrySnapshots_ReportCacheUsage_StateTransitions_AndReset()
    {
        _ = Button.GetTelemetryAndReset();

        var button = new Button
        {
            Content = "Telemetry",
            Padding = new Thickness(2f),
            BorderThickness = 1f,
            FontSize = 14f
        };

        button.Measure(new Vector2(120f, 40f));

        var slot = new LayoutRect(0f, 0f, 120f, 40f);
        var firstPlan = button.PrepareTextRenderPlanForTests(slot);
        var cachedPlan = button.PrepareTextRenderPlanForTests(slot);
        var shiftedPlan = button.PrepareTextRenderPlanForTests(new LayoutRect(4f, 0f, 120f, 40f));

        button.Content = "Telemetry Updated";
        var updatedPlan = button.PrepareTextRenderPlanForTests(slot);

        var getFallbackStyle = typeof(Button).GetMethod("GetFallbackStyle", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(getFallbackStyle);
        Assert.NotNull((Style?)getFallbackStyle!.Invoke(button, null));
        Assert.NotNull((Style?)getFallbackStyle.Invoke(button, null));

        button.SetMouseOverFromInput(true);
        button.SetMouseOverFromInput(true);
        button.SetPressedFromInput(true);
        button.SetPressedFromInput(true);
        button.InvokeFromInput();
        _ = button.HasAvailableIndependentDesiredSizeForUniformGrid();

        var runtime = button.GetButtonSnapshotForDiagnostics();

        Assert.True(firstPlan.HasValue);
        Assert.True(cachedPlan.HasValue);
        Assert.True(shiftedPlan.HasValue);
        Assert.True(updatedPlan.HasValue);
        Assert.Equal(nameof(String), runtime.ContentType);
        Assert.Equal("Telemetry Updated", runtime.DisplayText);
        Assert.True(runtime.MeasureOverrideCallCount > 0);
        Assert.True(runtime.ResolveTextLayoutCallCount > 0);
        Assert.True(runtime.TextLayoutCacheHitCount > 0);
        Assert.True(runtime.TextLayoutCacheMissCount > 0);
        Assert.True(runtime.TextLayoutInvalidationCount > 0);
        Assert.True(runtime.TextRenderPlanCacheHitCount > 0);
        Assert.True(runtime.TextRenderPlanCacheMissCount > 0);
        Assert.True(runtime.TextRenderPlanInvalidationCount > 0);
        Assert.True(runtime.RenderTextPreparationCallCount > 0);
        Assert.True(runtime.SetMouseOverFromInputChangedCount > 0);
        Assert.True(runtime.SetMouseOverFromInputNoOpCount > 0);
        Assert.True(runtime.SetPressedFromInputChangedCount > 0);
        Assert.True(runtime.SetPressedFromInputNoOpCount > 0);
        Assert.Equal(1, runtime.OnClickCallCount);
        Assert.Equal(1, runtime.RaiseClickEventCallCount);
        Assert.Equal(1, runtime.InvokeFromInputCallCount);
        Assert.Equal(1, runtime.OnClickAutomationNotifyCount + runtime.OnClickAutomationSkipCount);

        var aggregate = Button.GetTelemetryAndReset();

        Assert.True(aggregate.MeasureOverrideCallCount > 0);
        Assert.True(aggregate.ResolveTextLayoutCallCount > 0);
        Assert.True(aggregate.TextLayoutCacheHitCount > 0);
        Assert.True(aggregate.TextLayoutCacheMissCount > 0);
        Assert.True(aggregate.TextLayoutInvalidationCount > 0);
        Assert.True(aggregate.TextRenderPlanCacheHitCount > 0);
        Assert.True(aggregate.TextRenderPlanCacheMissCount > 0);
        Assert.True(aggregate.TextRenderPlanInvalidationCount > 0);
        Assert.True(aggregate.GetFallbackStyleCallCount >= 2);
        Assert.Equal(aggregate.GetFallbackStyleCallCount, aggregate.GetFallbackStyleCacheHitCount + aggregate.GetFallbackStyleCacheMissCount);
        Assert.True(aggregate.SetMouseOverFromInputChangedCount > 0);
        Assert.True(aggregate.SetPressedFromInputChangedCount > 0);
        Assert.Equal(1, aggregate.OnClickCallCount);
        Assert.Equal(1, aggregate.InvokeFromInputCallCount);

        var cleared = Button.GetTelemetryAndReset();

        Assert.Equal(0, cleared.MeasureOverrideCallCount);
        Assert.Equal(0, cleared.ResolveTextLayoutCallCount);
        Assert.Equal(0, cleared.TextRenderPlanCacheMissCount);
        Assert.Equal(0, cleared.OnClickCallCount);
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
