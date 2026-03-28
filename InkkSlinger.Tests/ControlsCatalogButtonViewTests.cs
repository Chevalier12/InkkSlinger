using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogButtonViewTests
{
    private const string WrappingButtonText =
        "This label is intentionally long and wraps when the button is constrained to the preview pane";

    [Fact]
    public void ButtonCatalog_WrappingSample_ShouldWrapAtDefaultCatalogViewport()
    {
        var catalog = new ControlsCatalogView();
        catalog.ShowControl("Button");

        var uiRoot = new UiRoot(catalog);
        RunLayout(uiRoot, 1280, 820, 16);

        var button = FindButtonByContent(catalog, WrappingButtonText);
        Assert.NotNull(button);

        var textBlock = FindDescendantTextBlock(button!, WrappingButtonText);
        Assert.NotNull(textBlock);

        var renderPlan = TextLayout.LayoutForElement(
            textBlock!.Text,
            textBlock,
            textBlock.FontSize,
            textBlock.ActualWidth,
            textBlock.TextWrapping);
        Assert.True(
            renderPlan.Lines.Count > 1,
            $"Expected the catalog wrapping sample to render multiple lines, but it rendered {renderPlan.Lines.Count} line(s).");
        Assert.True(
            button.ActualWidth <= 320.01f,
            $"Expected the catalog wrapping sample to stay constrained for the wrapping demo, got width {button.ActualWidth:0.##}.");
        Assert.True(
            button.ActualHeight > 30f,
            $"Expected the catalog wrapping sample to be taller than a single-line button, got {button.ActualWidth:0.##}x{button.ActualHeight:0.##}.");
    }

    private static Button? FindButtonByContent(UIElement root, string text)
    {
        if (root is Button button)
        {
            if (string.Equals(button.Content?.ToString(), text, StringComparison.Ordinal))
            {
                return button;
            }

            if (FindDescendantTextBlock(button, text) != null)
            {
                return button;
            }
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindButtonByContent(child, text);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TextBlock? FindDescendantTextBlock(UIElement root, string text)
    {
        if (root is TextBlock textBlock && string.Equals(textBlock.Text, text, StringComparison.Ordinal))
        {
            return textBlock;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindDescendantTextBlock(child, text);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}
