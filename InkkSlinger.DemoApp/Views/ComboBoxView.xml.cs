using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class ComboBoxView : UserControl
{
    private int _selectionChangedCount;

    public ComboBoxView()
    {
        InitializeComponent();

        SetupBasicCombo();
        SetupDisabledCombo();
        SetupTallCombo();
        SetupFeedbackCombo();
        SetupAccentCombo();
        SetupSubduedCombo();
    }

    private void SetupBasicCombo()
    {
        if (this.FindName("BasicCombo") is not ComboBox combo)
            return;

        combo.Items.Add("Design");
        combo.Items.Add("Engineering");
        combo.Items.Add("Product");
        combo.Items.Add("Research");
        combo.SelectedIndex = 1;
    }

    private void SetupDisabledCombo()
    {
        if (this.FindName("DisabledCombo") is not ComboBox combo)
            return;

        combo.Items.Add("Alpha");
        combo.Items.Add("Beta");
        combo.Items.Add("Gamma");
        combo.SelectedIndex = 0;
    }

    private void SetupTallCombo()
    {
        if (this.FindName("TallCombo") is not ComboBox combo)
            return;

        for (var i = 1; i <= 10; i++)
            combo.Items.Add($"Item {i}");

        combo.MaxDropDownHeight = 110f;
        combo.SelectedIndex = 0;
    }

    private void SetupFeedbackCombo()
    {
        if (this.FindName("FeedbackCombo") is not ComboBox combo)
            return;

        combo.Items.Add("Alpha");
        combo.Items.Add("Beta");
        combo.Items.Add("Gamma");
        combo.Items.Add("Delta");
        combo.Items.Add("Epsilon");
        combo.SelectionChanged += OnFeedbackSelectionChanged;
    }

    private void SetupAccentCombo()
    {
        if (this.FindName("AccentCombo") is not ComboBox combo)
            return;

        combo.Items.Add("Sunrise");
        combo.Items.Add("Noon");
        combo.Items.Add("Dusk");
        combo.Background = new Color(52, 32, 12);
        combo.BorderBrush = new Color(200, 120, 40);
        combo.Foreground = new Color(255, 195, 120);
        combo.SelectedIndex = 0;
    }

    private void SetupSubduedCombo()
    {
        if (this.FindName("SubduedCombo") is not ComboBox combo)
            return;

        combo.Items.Add("Low");
        combo.Items.Add("Medium");
        combo.Items.Add("High");
        combo.Background = new Color(22, 28, 38);
        combo.BorderBrush = new Color(80, 110, 160);
        combo.Foreground = new Color(160, 190, 230);
        combo.SelectedIndex = 1;
    }

    private void OnFeedbackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectionChangedCount++;

        var combo = sender as ComboBox;
        var added = e.AddedItems.Count > 0 ? e.AddedItems[0]?.ToString() : "—";

        if (this.FindName("SelectionIndexLabel") is TextBlock indexLabel)
            indexLabel.Text = $"SelectedIndex: {combo?.SelectedIndex ?? -1}";
        if (this.FindName("SelectionValueLabel") is TextBlock valueLabel)
            valueLabel.Text = $"Selected value: {added}";
        if (this.FindName("SelectionChangedCountLabel") is TextBlock countLabel)
            countLabel.Text = $"SelectionChanged fired: {_selectionChangedCount}";
    }
}
