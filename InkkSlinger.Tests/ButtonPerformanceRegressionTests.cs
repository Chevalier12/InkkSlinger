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
            Text = "Calendar"
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
            Text = "Open Calendar",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };

        button.Measure(new Vector2(300f, 120f));

        var layout = TextLayout.Layout(button.Text, button.Font, button.FontSize, float.PositiveInfinity, TextWrapping.NoWrap);
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
            Text = string.Empty,
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
            Text = "Wednesday Thursday Friday Saturday",
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };
        var availableSize = new Vector2(90f, 300f);

        button.Measure(availableSize);

        var textWidth = availableSize.X - button.Padding.Horizontal - (button.BorderThickness * 2f);
        var layout = TextLayout.Layout(button.Text, button.Font, button.FontSize, textWidth, TextWrapping.Wrap);
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
            Text = "31",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };

        button.Measure(new Vector2(300f, 120f));

        var expected = new Vector2(
            FontStashTextRenderer.MeasureWidth(button.Font, button.Text, button.FontSize) + button.Padding.Horizontal + (button.BorderThickness * 2f),
            FontStashTextRenderer.GetLineHeight(button.Font, button.FontSize) + button.Padding.Vertical + (button.BorderThickness * 2f));

        AssertClose(expected, button.DesiredSize);
        Assert.Equal(0, TextLayout.GetMetricsSnapshot().BuildCount);
    }

    [Fact]
    public void Measure_PlainTextSingleLineNoWrapButton_ReusesMeasureAcrossAvailableSizeChanges()
    {
        var button = new Button
        {
            Text = "31",
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

        var layout = TextLayout.Layout("Alpha\nBeta", null, 14f, float.PositiveInfinity, TextWrapping.NoWrap);

        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var width = Button.ResolveRenderedLineWidth(layout, i, layout.Lines[i], null, 14f);
            Assert.Equal(layout.LineWidths[i], width);
        }

        Assert.Equal(0, Button.GetRenderLineWidthFallbackCountForTests());
    }

    [Fact]
    public void ResolveRenderedLineWidth_FallsBack_WhenCachedWidthIsMissing()
    {
        Button.ResetRenderLineWidthFallbackCountForTests();

        var layout = new TextLayout.TextLayoutResult(new[] { "Fallback" }, System.Array.Empty<float>(), new Vector2(0f, 14f));

        var width = Button.ResolveRenderedLineWidth(layout, 0, "Fallback", null, 14f);

        Assert.True(width > 0f);
        Assert.Equal(1, Button.GetRenderLineWidthFallbackCountForTests());
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
}
