using System;

namespace InkkSlinger;

public partial class ButtonView : UserControl
{
    private int _clickCount;

    public ButtonView()
    {
        InitializeComponent();

        if (this.FindName("CountedButton") is Button countedButton)
            countedButton.Click += OnCountedButtonClick;
    }

    private void OnCountedButtonClick(object? sender, RoutedSimpleEventArgs e)
    {
        _clickCount++;
        if (this.FindName("ClickCountLabel") is TextBlock label)
            label.Text = $"Clicks: {_clickCount}";
    }
}
