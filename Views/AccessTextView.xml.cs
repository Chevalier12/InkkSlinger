using System;

namespace InkkSlinger;

public partial class AccessTextView : UserControl
{
    private int _primaryInvokeCount;
    private int _secondaryInvokeCount;
    private int _approveInvokeCount;
    private int _queueInvokeCount;

    public AccessTextView()
    {
        InitializeComponent();

        WireInteractivePlayground();
        WireTargetWorkbench();
        WireLabelDemo();

        UpdateInvokeCounts();
        UpdateCommandCounts();
        UpdateLivePreviewState();
    }

    private void WireInteractivePlayground()
    {
        if (this.FindName("LiveMarkupEditor") is TextBox liveMarkupEditor)
        {
            liveMarkupEditor.TextChanged += OnLiveEditorTextChanged;
        }

        if (this.FindName("TargetNameEditor") is TextBox targetNameEditor)
        {
            targetNameEditor.TextChanged += OnLiveEditorTextChanged;
        }

        if (this.FindName("LoadPrimaryPresetButton") is Button loadPrimaryPresetButton)
        {
            loadPrimaryPresetButton.Click += (_, _) => LoadLivePreset("_Run primary action", "PrimaryWorkbenchButton");
        }

        if (this.FindName("LoadEscapedPresetButton") is Button loadEscapedPresetButton)
        {
            loadEscapedPresetButton.Click += (_, _) => LoadLivePreset("Save __As and _pin it", "SecondaryWorkbenchButton");
        }

        if (this.FindName("LoadFocusPresetButton") is Button loadFocusPresetButton)
        {
            loadFocusPresetButton.Click += (_, _) => LoadLivePreset("_Search owner notes", "SearchTargetTextBox");
        }

        if (this.FindName("SimulateLiveAccessKeyButton") is Button simulateLiveAccessKeyButton)
        {
            simulateLiveAccessKeyButton.Click += OnSimulateLiveAccessKey;
        }

        if (this.FindName("PrimaryWorkbenchButton") is Button primaryWorkbenchButton)
        {
            primaryWorkbenchButton.Click += OnPrimaryWorkbenchButtonClick;
        }

        if (this.FindName("SecondaryWorkbenchButton") is Button secondaryWorkbenchButton)
        {
            secondaryWorkbenchButton.Click += OnSecondaryWorkbenchButtonClick;
        }
    }

    private void WireTargetWorkbench()
    {
        if (this.FindName("ApproveItemButton") is Button approveItemButton)
        {
            approveItemButton.Click += OnApproveItemButtonClick;
        }

        if (this.FindName("QueueItemButton") is Button queueItemButton)
        {
            queueItemButton.Click += OnQueueItemButtonClick;
        }

        if (this.FindName("SimulateApproveButton") is Button simulateApproveButton)
        {
            simulateApproveButton.Click += (_, _) => SimulateCommandBoardAccessKey('A', "ApproveItemButton");
        }

        if (this.FindName("SimulateQueueButton") is Button simulateQueueButton)
        {
            simulateQueueButton.Click += (_, _) => SimulateCommandBoardAccessKey('Q', "QueueItemButton");
        }

        if (this.FindName("SimulateOwnerButton") is Button simulateOwnerButton)
        {
            simulateOwnerButton.Click += (_, _) => SimulateCommandBoardAccessKey('O', "OwnerFilterTextBox");
        }
    }

    private void WireLabelDemo()
    {
        if (this.FindName("ProjectNameLabel") is Label projectNameLabel &&
            this.FindName("ProjectNameTextBox") is TextBox projectNameTextBox)
        {
            projectNameLabel.Target = projectNameTextBox;
        }

        if (this.FindName("SimulateLabelAccessKeyButton") is Button simulateLabelAccessKeyButton)
        {
            simulateLabelAccessKeyButton.Click += OnSimulateLabelAccessKey;
        }

        if (this.FindName("FocusQueueButton") is Button focusQueueButton)
        {
            focusQueueButton.Click += OnFocusQueueButtonClick;
        }
    }

    private void OnLiveEditorTextChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateLivePreviewState();
    }

    private void LoadLivePreset(string markup, string targetName)
    {
        if (this.FindName("LiveMarkupEditor") is TextBox liveMarkupEditor)
        {
            liveMarkupEditor.Text = markup;
        }

        if (this.FindName("TargetNameEditor") is TextBox targetNameEditor)
        {
            targetNameEditor.Text = targetName;
        }

        UpdateLivePreviewState();
    }

    private void UpdateLivePreviewState()
    {
        var markup = (this.FindName("LiveMarkupEditor") as TextBox)?.Text ?? string.Empty;
        var targetName = (this.FindName("TargetNameEditor") as TextBox)?.Text ?? string.Empty;
        var parsed = AccessTextParser.Parse(markup);

        if (this.FindName("LivePreviewAccessText") is AccessText livePreviewAccessText)
        {
            livePreviewAccessText.Text = markup;
            livePreviewAccessText.TargetName = targetName;
        }

        if (this.FindName("ParsedDisplayText") is TextBlock parsedDisplayText)
        {
            parsedDisplayText.Text = $"Display text: {FormatValue(parsed.DisplayText)}";
        }

        if (this.FindName("ParsedAccessKeyText") is TextBlock parsedAccessKeyText)
        {
            parsedAccessKeyText.Text = parsed.AccessKey is char accessKey
                ? $"Access key: {accessKey}"
                : "Access key: none";
        }

        if (this.FindName("ParsedIndexText") is TextBlock parsedIndexText)
        {
            parsedIndexText.Text = $"Access key display index: {parsed.AccessKeyDisplayIndex}";
        }

        if (this.FindName("ResolvedTargetText") is TextBlock resolvedTargetText)
        {
            resolvedTargetText.Text = $"Resolved target: {DescribeTargetName(targetName)}";
        }

        UpdateFocusSnapshot();
    }

    private void OnSimulateLiveAccessKey(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        var markup = (this.FindName("LiveMarkupEditor") as TextBox)?.Text ?? string.Empty;
        var parsed = AccessTextParser.Parse(markup);
        if (this.FindName("LiveSimulationStatusText") is not TextBlock simulationStatusText)
        {
            return;
        }

        if (parsed.AccessKey is not char accessKey)
        {
            simulationStatusText.Text = "Simulation status: current markup does not declare an access key.";
            UpdateFocusSnapshot();
            return;
        }

        var executed = AccessKeyService.TryExecute(accessKey, this, FocusManager.GetFocusedElement());
        var targetName = (this.FindName("TargetNameEditor") as TextBox)?.Text ?? string.Empty;
        simulationStatusText.Text = executed
            ? $"Simulation status: Alt+{accessKey} executed and targeted {DescribeTargetName(targetName)}."
            : $"Simulation status: Alt+{accessKey} did not execute. Target lookup for '{FormatValue(targetName)}' failed or the target was not interactive.";
        UpdateFocusSnapshot();
    }

    private void OnPrimaryWorkbenchButtonClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _primaryInvokeCount++;
        UpdateInvokeCounts();
        UpdateFocusSnapshot();
    }

    private void OnSecondaryWorkbenchButtonClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _secondaryInvokeCount++;
        UpdateInvokeCounts();
        UpdateFocusSnapshot();
    }

    private void UpdateInvokeCounts()
    {
        if (this.FindName("PrimaryInvokeCountText") is TextBlock primaryInvokeCountText)
        {
            primaryInvokeCountText.Text = $"Primary target clicks: {_primaryInvokeCount}";
        }

        if (this.FindName("SecondaryInvokeCountText") is TextBlock secondaryInvokeCountText)
        {
            secondaryInvokeCountText.Text = $"Secondary target clicks: {_secondaryInvokeCount}";
        }
    }

    private void OnApproveItemButtonClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _approveInvokeCount++;
        UpdateCommandCounts();
        UpdateFocusSnapshot();
    }

    private void OnQueueItemButtonClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _queueInvokeCount++;
        UpdateCommandCounts();
        UpdateFocusSnapshot();
    }

    private void UpdateCommandCounts()
    {
        if (this.FindName("ApproveCountText") is TextBlock approveCountText)
        {
            approveCountText.Text = $"Approve clicks: {_approveInvokeCount}";
        }

        if (this.FindName("QueueCountText") is TextBlock queueCountText)
        {
            queueCountText.Text = $"Queue clicks: {_queueInvokeCount}";
        }
    }

    private void SimulateCommandBoardAccessKey(char accessKey, string expectedTargetName)
    {
        var executed = AccessKeyService.TryExecute(accessKey, this, FocusManager.GetFocusedElement());
        if (this.FindName("CommandBoardStatusText") is TextBlock commandBoardStatusText)
        {
            commandBoardStatusText.Text = executed
                ? $"Command board status: Alt+{accessKey} executed and targeted {DescribeTargetName(expectedTargetName)}."
                : $"Command board status: Alt+{accessKey} did not execute. Expected target: {DescribeTargetName(expectedTargetName)}.";
        }

        UpdateFocusSnapshot();
    }

    private void OnSimulateLabelAccessKey(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        var focusedBefore = DescribeElement(FocusManager.GetFocusedElement());
        var executed = AccessKeyService.TryExecute('P', this, FocusManager.GetFocusedElement());
        var focusedAfter = DescribeElement(FocusManager.GetFocusedElement());

        if (this.FindName("LabelDemoStatusText") is TextBlock labelDemoStatusText)
        {
            labelDemoStatusText.Text = executed
                ? $"Label demo status: Alt+P focused {focusedAfter}. Previous focus: {focusedBefore}."
                : "Label demo status: Alt+P did not execute.";
        }

        UpdateFocusSnapshot();
    }

    private void OnFocusQueueButtonClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        if (this.FindName("QueueItemButton") is Button queueItemButton)
        {
            FocusManager.SetFocus(queueItemButton);
        }

        if (this.FindName("LabelDemoStatusText") is TextBlock labelDemoStatusText)
        {
            labelDemoStatusText.Text = $"Label demo status: focus moved to {DescribeElement(FocusManager.GetFocusedElement())}.";
        }

        UpdateFocusSnapshot();
    }

    private void UpdateFocusSnapshot()
    {
        if (this.FindName("FocusedElementText") is TextBlock focusedElementText)
        {
            focusedElementText.Text = $"Focused element: {DescribeElement(FocusManager.GetFocusedElement())}";
        }
    }

    private UIElement? ResolveTargetByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        if (NameScopeService.FindName(this, targetName) is UIElement namedElement)
        {
            return namedElement;
        }

        return this.FindName(targetName) as UIElement;
    }

    private string DescribeTargetName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return "none (TargetName is empty)";
        }

        var target = ResolveTargetByName(targetName);
        return target == null
            ? $"{targetName} (not found)"
            : DescribeElement(target);
    }

    private static string DescribeElement(UIElement? element)
    {
        if (element == null)
        {
            return "none";
        }

        if (element is FrameworkElement frameworkElement && !string.IsNullOrWhiteSpace(frameworkElement.Name))
        {
            return $"{frameworkElement.Name} ({element.GetType().Name})";
        }

        return element.GetType().Name;
    }

    private static string FormatValue(string? value)
    {
        return string.IsNullOrEmpty(value) ? "(empty)" : value;
    }
}




