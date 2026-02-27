using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RenderTransformPhase1Tests
{
    [Fact]
    public void LoadFromString_ButtonRenderTransformAndOrigin_AppliesFromXaml()
    {
        const string xaml = """
                            <Panel>
                              <Button RenderTransformOrigin="0.5,0.5">
                                <Button.RenderTransform>
                                  <ScaleTransform ScaleX="1.03" ScaleY="1.03" />
                                </Button.RenderTransform>
                              </Button>
                            </Panel>
                            """;

        var root = (Panel)XamlLoader.LoadFromString(xaml);
        var button = Assert.IsType<Button>(Assert.Single(root.Children));

        var transform = Assert.IsType<ScaleTransform>(button.RenderTransform);
        Assert.Equal(1.03f, transform.ScaleX, 3);
        Assert.Equal(1.03f, transform.ScaleY, 3);
        Assert.Equal(new Vector2(0.5f, 0.5f), button.RenderTransformOrigin);
    }

    [Fact]
    public void HitTest_WithTranslateRenderTransform_UsesTranslatedBounds()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 300f));

        var button = new Button
        {
            RenderTransform = new TranslateTransform { X = 50f, Y = 0f }
        };
        button.SetLayoutSlot(new LayoutRect(100f, 100f, 100f, 40f));
        root.AddChild(button);

        Assert.NotSame(button, VisualTreeHelper.HitTest(root, new Vector2(110f, 120f)));
        Assert.Same(button, VisualTreeHelper.HitTest(root, new Vector2(170f, 120f)));
    }

    [Fact]
    public void HitTest_WithScaleRenderTransformOrigin_CentersScaleAroundElementCenter()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 300f));

        var button = new Button
        {
            RenderTransformOrigin = new Vector2(0.5f, 0.5f),
            RenderTransform = new ScaleTransform { ScaleX = 2f, ScaleY = 2f }
        };
        button.SetLayoutSlot(new LayoutRect(100f, 100f, 100f, 40f));
        root.AddChild(button);

        Assert.Same(button, VisualTreeHelper.HitTest(root, new Vector2(60f, 120f)));
        Assert.NotSame(button, VisualTreeHelper.HitTest(root, new Vector2(40f, 120f)));
    }
}
