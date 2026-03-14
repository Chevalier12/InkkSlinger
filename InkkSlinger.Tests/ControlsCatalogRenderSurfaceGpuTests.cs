using System;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogRenderSurfaceGpuTests
{
    [Fact]
    public void Catalog_GpuRenderSurfaceButton_LoadsGpuPreviewView()
    {
        var view = new ControlsCatalogView();

        var button = FindCatalogButton(view, "RenderSurface [GPU]");
        Assert.NotNull(button);

        view.ShowControl("RenderSurface [GPU]");

        var previewHost = Assert.IsType<ContentControl>(view.FindName("PreviewHost"));
        Assert.IsType<RenderSurfaceGpuView>(previewHost.Content);
    }

    private static Button? FindCatalogButton(UIElement root, string text)
    {
        if (root is Button button && string.Equals(button.Text, text, StringComparison.Ordinal))
        {
            return button;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindCatalogButton(child, text);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
