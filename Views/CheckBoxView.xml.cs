using System;

namespace InkkSlinger;

public partial class CheckBoxView : UserControl
{
    private int _checkedCount;
    private int _uncheckedCount;
    private int _indeterminateCount;

    public CheckBoxView()
    {
        InitializeComponent();

        if (this.FindName("CheckedBox") is CheckBox checkedBox)
            checkedBox.IsChecked = true;

        if (this.FindName("IndeterminateBox") is CheckBox indeterminateBox)
        {
            indeterminateBox.IsThreeState = true;
            indeterminateBox.IsChecked = null;
        }

        if (this.FindName("ThreeStateBox") is CheckBox threeStateBox)
        {
            threeStateBox.IsThreeState = true;
            threeStateBox.Checked += OnThreeStateChanged;
            threeStateBox.Unchecked += OnThreeStateChanged;
            threeStateBox.Indeterminate += OnThreeStateChanged;
        }

        if (this.FindName("EventBox") is CheckBox eventBox)
        {
            eventBox.IsThreeState = true;
            eventBox.Checked += OnEventBoxChecked;
            eventBox.Unchecked += OnEventBoxUnchecked;
            eventBox.Indeterminate += OnEventBoxIndeterminate;
        }
    }

    private void OnThreeStateChanged(object? sender, RoutedSimpleEventArgs e)
    {
        if (sender is CheckBox box && this.FindName("ThreeStateLabel") is TextBlock label)
            label.Text = box.IsChecked.HasValue ? $"IsChecked: {box.IsChecked}" : "IsChecked: null (indeterminate)";
    }

    private void OnEventBoxChecked(object? sender, RoutedSimpleEventArgs e)
    {
        _checkedCount++;
        UpdateEventCounts();
    }

    private void OnEventBoxUnchecked(object? sender, RoutedSimpleEventArgs e)
    {
        _uncheckedCount++;
        UpdateEventCounts();
    }

    private void OnEventBoxIndeterminate(object? sender, RoutedSimpleEventArgs e)
    {
        _indeterminateCount++;
        UpdateEventCounts();
    }

    private void UpdateEventCounts()
    {
        if (this.FindName("CheckedCountLabel") is TextBlock checkedLabel)
            checkedLabel.Text = $"Checked fired: {_checkedCount}";
        if (this.FindName("UncheckedCountLabel") is TextBlock uncheckedLabel)
            uncheckedLabel.Text = $"Unchecked fired: {_uncheckedCount}";
        if (this.FindName("IndeterminateCountLabel") is TextBlock indeterminateLabel)
            indeterminateLabel.Text = $"Indeterminate fired: {_indeterminateCount}";
    }
}
