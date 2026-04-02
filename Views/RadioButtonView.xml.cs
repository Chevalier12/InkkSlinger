using System;

namespace InkkSlinger;

public partial class RadioButtonView : UserControl
{
    private int _clickCount;
    private int _checkedCount;
    private int _uncheckedCount;

    public RadioButtonView()
    {
        InitializeComponent();

        InitializeStateSamples();
        InitializeSiblingGroupingDemo();
        InitializeCrossContainerGroupingDemo();
        InitializeSizingDemo();
        InitializeEventDemo();
    }

    private void InitializeStateSamples()
    {
        SetChecked("CheckedStateRadio");
        SetChecked("DisabledCheckedStateRadio");
    }

    private void InitializeSiblingGroupingDemo()
    {
        SetChecked("DraftOptionRadio");
        AttachCheckedHandler(OnSiblingGroupChecked, "DraftOptionRadio", "ReviewOptionRadio", "PublishOptionRadio");
        UpdateSiblingSelection();
    }

    private void InitializeCrossContainerGroupingDemo()
    {
        SetChecked("MediumPriorityRadio");
        AttachCheckedHandler(OnCrossContainerGroupChecked, "LowPriorityRadio", "MediumPriorityRadio", "HighPriorityRadio", "CriticalPriorityRadio");
        UpdateCrossContainerSelection();
    }

    private void InitializeSizingDemo()
    {
        SetChecked("CompactDensityRadio");
    }

    private void InitializeEventDemo()
    {
        SetChecked("EventBrowseRadio");

        AttachEventHandlers("EventBrowseRadio", "EventInspectRadio", "EventCalibrateRadio");
        UpdateEventSelection();
        UpdateEventCounts();
    }

    private void OnSiblingGroupChecked(object? sender, RoutedSimpleEventArgs e)
    {
        UpdateSiblingSelection();
    }

    private void OnCrossContainerGroupChecked(object? sender, RoutedSimpleEventArgs e)
    {
        UpdateCrossContainerSelection();
    }

    private void OnEventRadioClick(object? sender, RoutedSimpleEventArgs e)
    {
        _clickCount++;
        UpdateEventCounts();
    }

    private void OnEventRadioChecked(object? sender, RoutedSimpleEventArgs e)
    {
        _checkedCount++;
        UpdateEventSelection();
        UpdateEventCounts();
    }

    private void OnEventRadioUnchecked(object? sender, RoutedSimpleEventArgs e)
    {
        _uncheckedCount++;
        UpdateEventCounts();
    }

    private void UpdateSiblingSelection()
    {
        if (this.FindName("SiblingSelectionLabel") is TextBlock label)
        {
            label.Text = $"Selected: {GetSelectedLabel(("DraftOptionRadio", "Draft"), ("ReviewOptionRadio", "Review"), ("PublishOptionRadio", "Publish"))}";
        }
    }

    private void UpdateCrossContainerSelection()
    {
        if (this.FindName("CrossContainerSelectionLabel") is TextBlock label)
        {
            label.Text = $"Shared selection: {GetSelectedLabel(("LowPriorityRadio", "Low"), ("MediumPriorityRadio", "Medium"), ("HighPriorityRadio", "High"), ("CriticalPriorityRadio", "Critical"))}";
        }
    }

    private void UpdateEventSelection()
    {
        if (this.FindName("EventSelectionLabel") is TextBlock label)
        {
            label.Text = $"Selected mode: {GetSelectedLabel(("EventBrowseRadio", "Browse"), ("EventInspectRadio", "Inspect"), ("EventCalibrateRadio", "Calibrate"))}";
        }
    }

    private void UpdateEventCounts()
    {
        if (this.FindName("ClickCountLabel") is TextBlock clickLabel)
        {
            clickLabel.Text = $"Click fired: {_clickCount}";
        }

        if (this.FindName("CheckedCountLabel") is TextBlock checkedLabel)
        {
            checkedLabel.Text = $"Checked fired: {_checkedCount}";
        }

        if (this.FindName("UncheckedCountLabel") is TextBlock uncheckedLabel)
        {
            uncheckedLabel.Text = $"Unchecked fired: {_uncheckedCount}";
        }
    }

    private void AttachCheckedHandler(EventHandler<RoutedSimpleEventArgs> handler, params string[] radioNames)
    {
        foreach (var radioName in radioNames)
        {
            if (this.FindName(radioName) is RadioButton radio)
            {
                radio.Checked += handler;
            }
        }
    }

    private void AttachEventHandlers(params string[] radioNames)
    {
        foreach (var radioName in radioNames)
        {
            if (this.FindName(radioName) is not RadioButton radio)
            {
                continue;
            }

            radio.Click += OnEventRadioClick;
            radio.Checked += OnEventRadioChecked;
            radio.Unchecked += OnEventRadioUnchecked;
        }
    }

    private void SetChecked(string radioName)
    {
        if (this.FindName(radioName) is RadioButton radio)
        {
            radio.IsChecked = true;
        }
    }

    private string GetSelectedLabel(params (string Name, string Label)[] options)
    {
        foreach (var option in options)
        {
            if (this.FindName(option.Name) is RadioButton radio && radio.IsChecked == true)
            {
                return option.Label;
            }
        }

        return "None";
    }
}




