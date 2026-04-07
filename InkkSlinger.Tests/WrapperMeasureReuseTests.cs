using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class WrapperMeasureReuseTests
{
    [Fact]
    public void Measure_PanelWithReusableChild_ReusesAcrossAvailableSizeChanges()
    {
        var child = new Button
        {
            Content = "31",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };
        var panel = new Panel();
        panel.AddChild(child);

        panel.Measure(new Vector2(80f, 40f));
        panel.Measure(new Vector2(220f, 120f));

        Assert.Equal(2, panel.MeasureCallCount);
        Assert.Equal(1, panel.MeasureWorkCount);
        Assert.Equal(1, child.MeasureCallCount);
        Assert.Equal(1, child.MeasureWorkCount);
    }

    [Fact]
    public void Measure_BorderWithReusableChild_ReusesAcrossAvailableSizeChanges()
    {
        var child = new Button
        {
            Content = "31",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };
        var border = new Border
        {
            Padding = new Thickness(8f),
            BorderThickness = new Thickness(2f),
            Child = child
        };

        border.Measure(new Vector2(120f, 60f));
        border.Measure(new Vector2(280f, 160f));

        Assert.Equal(2, border.MeasureCallCount);
        Assert.Equal(1, border.MeasureWorkCount);
        Assert.Equal(1, child.MeasureCallCount);
        Assert.Equal(1, child.MeasureWorkCount);
    }

    [Fact]
    public void Measure_BorderWithWrappedTextChild_DoesNotReuseWhenWidthNarrows()
    {
        var child = new TextBlock
        {
            Text = "Wednesday Thursday Friday Saturday",
            TextWrapping = TextWrapping.Wrap
        };
        var border = new Border
        {
            Padding = new Thickness(8f),
            BorderThickness = new Thickness(2f),
            Child = child
        };

        border.Measure(new Vector2(320f, 200f));
        var wideDesired = border.DesiredSize;

        border.Measure(new Vector2(100f, 200f));
        var narrowDesired = border.DesiredSize;

        Assert.Equal(2, border.MeasureWorkCount);
        Assert.Equal(2, child.MeasureWorkCount);
        Assert.True(narrowDesired.Y > wideDesired.Y, $"Expected height to increase when width narrows (wide={wideDesired.Y}, narrow={narrowDesired.Y})");
    }

    [Fact]
    public void Measure_StackPanelWithReusableChildren_ReusesAcrossAvailableSizeChanges()
    {
        var stack = new StackPanel();
        stack.AddChild(new Button
        {
            Content = "31",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        });
        stack.AddChild(new Button
        {
            Content = "42",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        });

        stack.Measure(new Vector2(120f, 60f));
        stack.Measure(new Vector2(280f, 160f));

        Assert.Equal(2, stack.MeasureCallCount);
        Assert.Equal(1, stack.MeasureWorkCount);
    }

    [Fact]
    public void Measure_WrapPanelSingleLineChildren_ReusesAcrossAvailableSizeChanges()
    {
        var wrap = new WrapPanel();
        wrap.AddChild(new Button
        {
            Content = "One",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        });
        wrap.AddChild(new Button
        {
            Content = "Two",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        });

        wrap.Measure(new Vector2(300f, 80f));
        wrap.Measure(new Vector2(340f, 80f));

        Assert.Equal(2, wrap.MeasureCallCount);
        Assert.Equal(1, wrap.MeasureWorkCount);
    }

    [Fact]
    public void Measure_WrapPanelThatStartsWrapping_DoesNotReuseWhenWidthNarrows()
    {
        var wrap = new WrapPanel();
        wrap.AddChild(new Button
        {
            Content = "Long Button A",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        });
        wrap.AddChild(new Button
        {
            Content = "Long Button B",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        });

        wrap.Measure(new Vector2(280f, 80f));
        var wideDesired = wrap.DesiredSize;

        wrap.Measure(new Vector2(100f, 80f));

        Assert.Equal(2, wrap.MeasureWorkCount);
        Assert.True(wrap.DesiredSize.Y > wideDesired.Y, $"Expected wrap height to increase when width narrows (wide={wideDesired.Y}, narrow={wrap.DesiredSize.Y})");
    }

    [Fact]
    public void Measure_TemplatedControlWithReusableTemplateRoot_ReusesAcrossAvailableSizeChanges()
    {
        var child = new Button
        {
            Content = "31",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };
        var control = new ProbeControl
        {
            Template = new ControlTemplate(_ => child)
        };

        control.Measure(new Vector2(120f, 60f));
        control.Measure(new Vector2(280f, 160f));

        Assert.Equal(2, control.MeasureCallCount);
        Assert.Equal(1, control.MeasureWorkCount);
        Assert.Equal(1, child.MeasureCallCount);
        Assert.Equal(1, child.MeasureWorkCount);
    }

    [Fact]
    public void Measure_ContentPresenterWithReusableContent_ReusesAcrossAvailableSizeChanges()
    {
        var child = new Button
        {
            Content = "31",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };
        var presenter = new ContentPresenter
        {
            Content = child
        };

        presenter.Measure(new Vector2(120f, 60f));
        presenter.Measure(new Vector2(280f, 160f));

        Assert.Equal(2, presenter.MeasureCallCount);
        Assert.Equal(1, presenter.MeasureWorkCount);
        Assert.Equal(1, child.MeasureCallCount);
        Assert.Equal(1, child.MeasureWorkCount);
    }

    [Fact]
    public void Measure_GridWithStableDesiredSize_ReusesAcrossAvailableSizeChanges()
    {
        var child = new Button
        {
            Content = "31",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.AddChild(child);

        grid.Measure(new Vector2(120f, 60f));
        var firstDesired = grid.DesiredSize;

        grid.Measure(new Vector2(280f, 160f));

        Assert.Equal(2, grid.MeasureCallCount);
        Assert.Equal(1, grid.MeasureWorkCount);
        Assert.Equal(firstDesired, grid.DesiredSize);
        Assert.Equal(1, child.MeasureCallCount);
        Assert.Equal(1, child.MeasureWorkCount);
    }

    [Fact]
    public void Measure_GridWithStarColumn_DoesNotReuseAcrossAvailableSizeChanges()
    {
        var child = new Button
        {
            Content = "31",
            Padding = new Thickness(10f, 6f, 10f, 6f),
            BorderThickness = 1f,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.AddChild(child);

        grid.Measure(new Vector2(120f, 60f));
        var firstDesired = grid.DesiredSize;

        grid.Measure(new Vector2(280f, 60f));

        Assert.Equal(2, grid.MeasureCallCount);
        Assert.Equal(2, grid.MeasureWorkCount);
        Assert.True(grid.DesiredSize.X > firstDesired.X, $"Expected grid desired width to grow with finite star space (first={firstDesired.X}, next={grid.DesiredSize.X})");
        Assert.Equal(1, child.MeasureWorkCount);
    }

    private sealed class ProbeControl : Control
    {
    }
}