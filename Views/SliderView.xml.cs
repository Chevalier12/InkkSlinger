using System;

namespace InkkSlinger;

public partial class SliderView : UserControl
{
    public SliderView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = BuildDemoContent();
        }
    }

    private static UIElement BuildDemoContent()
    {
        var root = new StackPanel();

        root.AddChild(BuildSection(
            "Snapped selection range",
            new Slider
            {
                Width = 280f,
                Minimum = 0f,
                Maximum = 100f,
                Value = 35f,
                TickPlacement = TickPlacement.Both,
                IsSnapToTickEnabled = true,
                IsSelectionRangeEnabled = true,
                SelectionStart = 20f,
                SelectionEnd = 80f,
                AutoToolTipPlacement = AutoToolTipPlacement.TopLeft,
                Ticks = DoubleCollection.Parse("0,10,20,40,60,80,100")
            }));

        root.AddChild(BuildSection(
            "Move-to-point scrubber",
            new Slider
            {
                Width = 320f,
                Minimum = 0f,
                Maximum = 300f,
                Value = 90f,
                LargeChange = 15f,
                IsMoveToPointEnabled = true,
                TickFrequency = 15f,
                IsSnapToTickEnabled = true,
                AutoToolTipPlacement = AutoToolTipPlacement.BottomRight,
                Margin = new Thickness(0f, 10f, 0f, 0f)
            }));

        var verticalHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0f, 10f, 0f, 0f)
        };

        verticalHost.AddChild(new Slider
        {
            Width = 36f,
            Height = 220f,
            Orientation = Orientation.Vertical,
            Minimum = 0f,
            Maximum = 100f,
            Value = 55f,
            TickPlacement = TickPlacement.Both,
            IsSelectionRangeEnabled = true,
            SelectionStart = 15f,
            SelectionEnd = 70f,
            AutoToolTipPlacement = AutoToolTipPlacement.TopLeft,
            Margin = new Thickness(0f, 0f, 18f, 0f)
        });

        verticalHost.AddChild(new Slider
        {
            Width = 36f,
            Height = 220f,
            Orientation = Orientation.Vertical,
            IsDirectionReversed = true,
            Minimum = 0f,
            Maximum = 1f,
            Value = 0.25f,
            TickFrequency = 0.1f,
            IsSnapToTickEnabled = true,
            TickPlacement = TickPlacement.TopLeft,
            AutoToolTipPlacement = AutoToolTipPlacement.BottomRight
        });

        root.AddChild(BuildSection("Vertical and reversed", verticalHost));

        return root;
    }

    private static UIElement BuildSection(string title, UIElement content)
    {
        var stack = new StackPanel();
        stack.AddChild(new Label
        {
            Content = title,
            Margin = new Thickness(0f, 0f, 0f, 6f)
        });
        stack.AddChild(content);

        return new Border
        {
            Margin = new Thickness(0f, 0f, 0f, 12f),
            Padding = new Thickness(10f),
            BorderThickness = new Thickness(1f),
            Child = stack
        };
    }
}




