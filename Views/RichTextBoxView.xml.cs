using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class RichTextBoxView : UserControl
{
    private const string ClipboardRtfFormat = "Rich Text Format";
    private const string ClipboardXamlFormat = "Xaml";
    private const string ClipboardXamlPackageFormat = "XamlPackage";
    private const string ClipboardTextFormat = "Text";

    private static readonly FormatOption[] FormatOptions =
    [
        new("Flow XML", FlowDocumentSerializer.ClipboardFormat),
        new("XAML", ClipboardXamlFormat),
        new("XamlPackage", ClipboardXamlPackageFormat),
        new("Rich Text Format", ClipboardRtfFormat),
        new("Plain Text", ClipboardTextFormat)
    ];

    private static readonly string[] ScrollModeOptions =
    [
        nameof(ScrollBarVisibility.Disabled),
        nameof(ScrollBarVisibility.Auto),
        nameof(ScrollBarVisibility.Hidden),
        nameof(ScrollBarVisibility.Visible)
    ];

    private readonly Dictionary<RoutedCommand, Button> _commandButtons = new();
    private readonly List<Button> _presetButtons = [];

    private readonly RichTextBox _editor;
    private readonly TextBlock _heroSummaryLabel;
    private readonly Label _toolbarHintLabel;
    private readonly TextBlock _canvasFooterLabel;
    private readonly WrapPanel _historyToolbarPanel;
    private readonly WrapPanel _formatToolbarPanel;
    private readonly WrapPanel _presetPanel;
    private readonly ComboBox _formatComboBox;
    private readonly WrapPanel _payloadActionPanel;
    private readonly TextBox _payloadPreviewTextBox;
    private readonly TextBlock _payloadMetaLabel;
    private readonly CheckBox _readOnlyCheckBox;
    private readonly CheckBox _acceptsReturnCheckBox;
    private readonly CheckBox _acceptsTabCheckBox;
    private readonly CheckBox _undoEnabledCheckBox;
    private readonly CheckBox _spellCheckCheckBox;
    private readonly CheckBox _inactiveSelectionCheckBox;
    private readonly CheckBox _readOnlyCaretCheckBox;
    private readonly Label _selectionOpacityLabel;
    private readonly Slider _selectionOpacitySlider;
    private readonly ComboBox _horizontalScrollComboBox;
    private readonly ComboBox _verticalScrollComboBox;
    private readonly TextBlock _documentStatusLabel;
    private readonly TextBlock _selectionStatusLabel;
    private readonly TextBlock _viewportStatusLabel;
    private readonly TextBlock _activityStatusLabel;
    private readonly TextBlock _spellCheckStatusLabel;

    private Button? _undoButton;
    private Button? _redoButton;
    private Button? _exportDocumentButton;
    private Button? _exportSelectionButton;
    private Button? _loadDocumentButton;
    private Button? _loadSelectionButton;

    private string _lastPresetName = "Welcome";
    private string _lastActivity = "Ready.";
    private string _lastNavigateUri = "none";
    private int _textChangedCount;
    private int _selectionChangedCount;
    private bool _hasCompletedInitialPresetLoad;
    private bool _hasInitializedInteractiveSurface;
    private bool _hasQueuedEditorUiRefresh;
    private int _uiRefreshBatchDepth;
    private bool _uiRefreshPending;

    public RichTextBoxView()
    {
        InitializeComponent();

        _editor = RequireElement<RichTextBox>("Editor");
        _heroSummaryLabel = RequireElement<TextBlock>("HeroSummaryLabel");
        _toolbarHintLabel = RequireElement<Label>("ToolbarHintLabel");
        _canvasFooterLabel = RequireElement<TextBlock>("CanvasFooterLabel");
        _historyToolbarPanel = RequireElement<WrapPanel>("HistoryToolbarPanel");
        _formatToolbarPanel = RequireElement<WrapPanel>("FormatToolbarPanel");
        _presetPanel = RequireElement<WrapPanel>("PresetPanel");
        _formatComboBox = RequireElement<ComboBox>("FormatComboBox");
        _payloadActionPanel = RequireElement<WrapPanel>("PayloadActionPanel");
        _payloadPreviewTextBox = RequireElement<TextBox>("PayloadPreviewTextBox");
        _payloadMetaLabel = RequireElement<TextBlock>("PayloadMetaLabel");
        _readOnlyCheckBox = RequireElement<CheckBox>("ReadOnlyCheckBox");
        _acceptsReturnCheckBox = RequireElement<CheckBox>("AcceptsReturnCheckBox");
        _acceptsTabCheckBox = RequireElement<CheckBox>("AcceptsTabCheckBox");
        _undoEnabledCheckBox = RequireElement<CheckBox>("UndoEnabledCheckBox");
        _spellCheckCheckBox = RequireElement<CheckBox>("SpellCheckCheckBox");
        _inactiveSelectionCheckBox = RequireElement<CheckBox>("InactiveSelectionCheckBox");
        _readOnlyCaretCheckBox = RequireElement<CheckBox>("ReadOnlyCaretCheckBox");
        _selectionOpacityLabel = RequireElement<Label>("SelectionOpacityLabel");
        _selectionOpacitySlider = RequireElement<Slider>("SelectionOpacitySlider");
        _horizontalScrollComboBox = RequireElement<ComboBox>("HorizontalScrollComboBox");
        _verticalScrollComboBox = RequireElement<ComboBox>("VerticalScrollComboBox");
        _documentStatusLabel = RequireElement<TextBlock>("DocumentStatusLabel");
        _selectionStatusLabel = RequireElement<TextBlock>("SelectionStatusLabel");
        _viewportStatusLabel = RequireElement<TextBlock>("ViewportStatusLabel");
        _activityStatusLabel = RequireElement<TextBlock>("ActivityStatusLabel");
        _spellCheckStatusLabel = RequireElement<TextBlock>("SpellCheckStatusLabel");

        if (_hasCompletedInitialPresetLoad)
        {
            return;
        }

        InitializeInteractiveSurfaceIfNeeded();
        LoadPreset("Welcome", CreateWelcomeDocument, focusEditor: false);
        _hasCompletedInitialPresetLoad = true;
    }

    private void InitializeInteractiveSurfaceIfNeeded()
    {
        if (_hasInitializedInteractiveSurface)
        {
            return;
        }

        _hasInitializedInteractiveSurface = true;
        BuildToolbars();
        BuildPresetButtons();
        BuildPayloadButtons();
        PopulateComboBoxes();
        InitializeToggles();
        WireEvents();
        RequestUiRefresh();
    }

    private void BuildToolbars()
    {
        _undoButton = CreateActionButton(_historyToolbarPanel, "Undo", HandleUndo);
        _redoButton = CreateActionButton(_historyToolbarPanel, "Redo", HandleRedo);

        CreateCommandButton(_historyToolbarPanel, "Cut", EditingCommands.Cut);
        CreateCommandButton(_historyToolbarPanel, "Copy", EditingCommands.Copy);
        CreateCommandButton(_historyToolbarPanel, "Paste", EditingCommands.Paste);
        CreateCommandButton(_historyToolbarPanel, "Select All", EditingCommands.SelectAll);

        CreateCommandButton(_formatToolbarPanel, "Bold", EditingCommands.ToggleBold);
        CreateCommandButton(_formatToolbarPanel, "Italic", EditingCommands.ToggleItalic);
        CreateCommandButton(_formatToolbarPanel, "Underline", EditingCommands.ToggleUnderline);
        CreateCommandButton(_formatToolbarPanel, "Bullets", EditingCommands.ToggleBullets);
        CreateCommandButton(_formatToolbarPanel, "Numbering", EditingCommands.ToggleNumbering);
        CreateCommandButton(_formatToolbarPanel, "Indent +", EditingCommands.IncreaseListLevel);
        CreateCommandButton(_formatToolbarPanel, "Indent -", EditingCommands.DecreaseListLevel);
        CreateCommandButton(_formatToolbarPanel, "Insert Table", EditingCommands.InsertTable);
        CreateCommandButton(_formatToolbarPanel, "Split Cell", EditingCommands.SplitCell);
        CreateCommandButton(_formatToolbarPanel, "Merge Cells", EditingCommands.MergeCells);
        CreateCommandButton(_formatToolbarPanel, "Page Up", EditingCommands.MoveUpByPage);
        CreateCommandButton(_formatToolbarPanel, "Page Down", EditingCommands.MoveDownByPage);
        CreateCommandButton(_formatToolbarPanel, "Delete Prev Word", EditingCommands.DeletePreviousWord);
        CreateCommandButton(_formatToolbarPanel, "Delete Next Word", EditingCommands.DeleteNextWord);
    }

    private void BuildPresetButtons()
    {
        _presetButtons.Add(CreatePresetButton("Welcome", CreateWelcomeDocument));
        _presetButtons.Add(CreatePresetButton("Structure", CreateStructuredDocument));
        _presetButtons.Add(CreatePresetButton("Embedded UI", CreateEmbeddedUiDocument));
        _presetButtons.Add(CreatePresetButton("Longform", CreateLongformDocument));
        _presetButtons.Add(CreatePresetButton("Blank", CreateBlankDocument));
    }

    private void BuildPayloadButtons()
    {
        _exportDocumentButton = CreateActionButton(_payloadActionPanel, "Export Doc", () => ExportPayload(selectionOnly: false));
        _exportSelectionButton = CreateActionButton(_payloadActionPanel, "Export Selection", () => ExportPayload(selectionOnly: true));
        _loadDocumentButton = CreateActionButton(_payloadActionPanel, "Load Doc", () => LoadPayload(selectionOnly: false));
        _loadSelectionButton = CreateActionButton(_payloadActionPanel, "Load Selection", () => LoadPayload(selectionOnly: true));
    }

    private void PopulateComboBoxes()
    {
        foreach (var option in FormatOptions)
        {
            _formatComboBox.Items.Add(option.Label);
        }

        _formatComboBox.SelectedIndex = 0;

        foreach (var option in ScrollModeOptions)
        {
            _horizontalScrollComboBox.Items.Add(option);
            _verticalScrollComboBox.Items.Add(option);
        }

        _horizontalScrollComboBox.SelectedItem = nameof(ScrollBarVisibility.Auto);
        _verticalScrollComboBox.SelectedItem = nameof(ScrollBarVisibility.Auto);
    }

    private void InitializeToggles()
    {
        _readOnlyCheckBox.IsChecked = false;
        _acceptsReturnCheckBox.IsChecked = true;
        _acceptsTabCheckBox.IsChecked = true;
        _undoEnabledCheckBox.IsChecked = true;
        _spellCheckCheckBox.IsChecked = false;
        _inactiveSelectionCheckBox.IsChecked = _editor.IsInactiveSelectionHighlightEnabled;
        _readOnlyCaretCheckBox.IsChecked = _editor.IsReadOnlyCaretVisible;

        _selectionOpacitySlider.Minimum = 0.15f;
        _selectionOpacitySlider.Maximum = 1f;
        _selectionOpacitySlider.SmallChange = 0.05f;
        _selectionOpacitySlider.LargeChange = 0.1f;
        _selectionOpacitySlider.IsSnapToTickEnabled = true;
        _selectionOpacitySlider.TickFrequency = 0.05f;
        _selectionOpacitySlider.Value = _editor.SelectionOpacity;

        ApplyBehaviorSettings();
        ApplyViewportSettings();
        UpdateSelectionOpacityLabel();
    }

    private void WireEvents()
    {
        _editor.TextChanged += (_, _) =>
        {
            _textChangedCount++;
            QueueEditorUiRefresh();
        };

        _editor.SelectionChanged += (_, _) =>
        {
            _selectionChangedCount++;
            QueueEditorUiRefresh();
        };

        _editor.HyperlinkNavigate += (_, args) =>
        {
            _lastNavigateUri = args.NavigateUri;
            SetActivity($"HyperlinkNavigate -> {args.NavigateUri}");
            RequestUiRefresh();
        };

        _payloadPreviewTextBox.TextChanged += (_, _) => RequestUiRefresh();
        _formatComboBox.SelectionChanged += (_, _) =>
        {
            SetActivity($"Payload format changed to {GetSelectedFormatLabel()}.");
            RequestUiRefresh();
        };

        _selectionOpacitySlider.ValueChanged += (_, _) =>
        {
            _editor.SelectionOpacity = _selectionOpacitySlider.Value;
            UpdateSelectionOpacityLabel();
            SetActivity($"Selection opacity set to {_selectionOpacitySlider.Value:0.00}.");
        };

        _horizontalScrollComboBox.SelectionChanged += (_, _) =>
        {
            ApplyViewportSettings();
            SetActivity($"Horizontal scroll bars -> {_horizontalScrollComboBox.SelectedItem}. ");
            RequestUiRefresh();
        };

        _verticalScrollComboBox.SelectionChanged += (_, _) =>
        {
            ApplyViewportSettings();
            SetActivity($"Vertical scroll bars -> {_verticalScrollComboBox.SelectedItem}. ");
            RequestUiRefresh();
        };

        WireToggle(_readOnlyCheckBox);
        WireToggle(_acceptsReturnCheckBox);
        WireToggle(_acceptsTabCheckBox);
        WireToggle(_undoEnabledCheckBox);
        WireToggle(_spellCheckCheckBox);
        WireToggle(_inactiveSelectionCheckBox);
        WireToggle(_readOnlyCaretCheckBox);
    }

    private void WireToggle(CheckBox checkBox)
    {
        checkBox.Checked += HandleToggleChanged;
        checkBox.Unchecked += HandleToggleChanged;
    }

    private void HandleToggleChanged(object? sender, RoutedSimpleEventArgs args)
    {
        PerformUiRefreshBatch(() =>
        {
            ApplyBehaviorSettings();
            SetActivity($"{(sender as CheckBox)?.Content ?? "Behavior"} updated.");
            RequestUiRefresh();
        });
    }

    private void ApplyBehaviorSettings()
    {
        _editor.IsReadOnly = _readOnlyCheckBox.IsChecked == true;
        _editor.AcceptsReturn = _acceptsReturnCheckBox.IsChecked == true;
        _editor.AcceptsTab = _acceptsTabCheckBox.IsChecked == true;
        _editor.IsUndoEnabled = _undoEnabledCheckBox.IsChecked == true;
        _editor.IsSpellCheckEnabled = _spellCheckCheckBox.IsChecked == true;
        _editor.IsInactiveSelectionHighlightEnabled = _inactiveSelectionCheckBox.IsChecked == true;
        _editor.IsReadOnlyCaretVisible = _readOnlyCaretCheckBox.IsChecked == true;
        CommandManager.InvalidateRequerySuggested();
    }

    private void ApplyViewportSettings()
    {
        if (TryGetSelectedScrollVisibility(_horizontalScrollComboBox, out var horizontalVisibility))
        {
            _editor.HorizontalScrollBarVisibility = horizontalVisibility;
        }

        if (TryGetSelectedScrollVisibility(_verticalScrollComboBox, out var verticalVisibility))
        {
            _editor.VerticalScrollBarVisibility = verticalVisibility;
        }
    }

    private static bool TryGetSelectedScrollVisibility(ComboBox comboBox, out ScrollBarVisibility visibility)
    {
        if (comboBox.SelectedItem is string rawValue &&
            Enum.TryParse(rawValue, ignoreCase: false, out ScrollBarVisibility parsed))
        {
            visibility = parsed;
            return true;
        }

        visibility = ScrollBarVisibility.Auto;
        return false;
    }

    private Button CreateCommandButton(WrapPanel host, string label, RoutedCommand command)
    {
        var button = CreateActionButton(host, label, () => ExecuteCommand(label, command));
        _commandButtons[command] = button;
        return button;
    }

    private Button CreateActionButton(WrapPanel host, string label, Action onClick)
    {
        var button = new Button
        {
            Content = label,
            Margin = new Thickness(0f, 0f, 8f, 8f),
            Padding = new Thickness(10f, 6f, 10f, 6f),
            MinWidth = 92f
        };
        button.Click += (_, _) => onClick();
        host.AddChild(button);
        return button;
    }

    private Button CreatePresetButton(string label, Func<FlowDocument> factory)
    {
        var button = new Button
        {
            Content = label,
            Margin = new Thickness(0f, 0f, 8f, 8f),
            Padding = new Thickness(10f, 6f, 10f, 6f),
            MinWidth = 100f
        };

        button.Click += (_, _) => LoadPreset(label, factory);
        _presetPanel.AddChild(button);
        return button;
    }

    private void HandleUndo()
    {
        if (!_editor.CanUndo || !_editor.IsUndoEnabled)
        {
            SetActivity("Undo is currently unavailable.");
            RequestUiRefresh();
            return;
        }

        PerformUiRefreshBatch(() =>
        {
            _editor.Undo();
            SetActivity("Undo executed.");
            RequestUiRefresh();
        });
    }

    private void HandleRedo()
    {
        if (!_editor.CanRedo || !_editor.IsUndoEnabled)
        {
            SetActivity("Redo is currently unavailable.");
            RequestUiRefresh();
            return;
        }

        PerformUiRefreshBatch(() =>
        {
            _editor.Redo();
            SetActivity("Redo executed.");
            RequestUiRefresh();
        });
    }

    private void ExecuteCommand(string label, RoutedCommand command)
    {
        if (!command.CanExecute(null, _editor))
        {
            SetActivity($"{label} is not available for the current selection.");
            RequestUiRefresh();
            return;
        }

        PerformUiRefreshBatch(() =>
        {
            command.Execute(null, _editor);
            SetActivity($"Executed {label}.");
            RequestUiRefresh();
        });
    }

    private void LoadPreset(string presetName, Func<FlowDocument> factory, bool focusEditor = true)
    {
        PerformUiRefreshBatch(() =>
        {
            var clearedPayloadPreview = !string.IsNullOrEmpty(_payloadPreviewTextBox.Text);
            var document = factory();
            _editor.Document = document;
            _editor.Select(0, 0);
            if (focusEditor)
            {
                _editor.SetFocusedFromInput(true);
            }

            _lastPresetName = presetName;
            SetActivity($"Loaded {presetName} preset.");

            if (clearedPayloadPreview)
            {
                _payloadPreviewTextBox.Text = string.Empty;
            }

            RequestUiRefresh();
        });
    }

    private void ExportPayload(bool selectionOnly)
    {
        PerformUiRefreshBatch(() =>
        {
            var format = GetSelectedFormat();
            if (!_editor.CanSave(format))
            {
                SetActivity($"Cannot export {_editor.GetType().Name} content as {GetSelectedFormatLabel()}.");
                RequestUiRefresh();
                return;
            }

            using var stream = new MemoryStream();
            if (selectionOnly)
            {
                _editor.SaveSelection(stream, format);
            }
            else
            {
                _editor.Save(stream, format);
            }

            _payloadPreviewTextBox.Text = ReadUtf8(stream);
            SetActivity($"Exported {(selectionOnly ? "selection" : "document")} as {GetSelectedFormatLabel()}.");
            RequestUiRefresh();
        });
    }

    private void LoadPayload(bool selectionOnly)
    {
        PerformUiRefreshBatch(() =>
        {
            var format = GetSelectedFormat();
            if (!_editor.CanLoad(format))
            {
                SetActivity($"Cannot load payload using {GetSelectedFormatLabel()}.");
                RequestUiRefresh();
                return;
            }

            var payload = _payloadPreviewTextBox.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(payload))
            {
                SetActivity("Payload preview is empty.");
                RequestUiRefresh();
                return;
            }

            try
            {
                using var stream = CreateUtf8Stream(payload);
                if (selectionOnly)
                {
                    _editor.LoadSelection(stream, format);
                }
                else
                {
                    _editor.Load(stream, format);
                }

                _editor.SetFocusedFromInput(true);
                SetActivity($"Loaded {(selectionOnly ? "selection" : "document")} from {GetSelectedFormatLabel()} payload.");
            }
            catch (Exception ex)
            {
                SetActivity($"Load failed: {ex.Message}");
            }

            RequestUiRefresh();
        });
    }

    private void RequestUiRefresh()
    {
        if (_uiRefreshBatchDepth > 0)
        {
            _uiRefreshPending = true;
            return;
        }

        RefreshUiState();
    }

    private void QueueEditorUiRefresh()
    {
        if (_hasQueuedEditorUiRefresh)
        {
            return;
        }

        _hasQueuedEditorUiRefresh = true;
        Dispatcher.EnqueueDeferred(() =>
        {
            _hasQueuedEditorUiRefresh = false;
            RefreshEditorUiState();
        });
    }

    private void RefreshEditorUiState()
    {
        if (_uiRefreshBatchDepth > 0)
        {
            _uiRefreshPending = true;
            return;
        }

        var stats = DocumentStats.FromDocument(_editor.Document);
        UpdateEditorCommandStates();
        UpdateStatusLabels(stats);
    }

    private void PerformUiRefreshBatch(Action action)
    {
        _uiRefreshBatchDepth++;
        try
        {
            action();
        }
        finally
        {
            _uiRefreshBatchDepth--;
            if (_uiRefreshBatchDepth == 0 && _uiRefreshPending)
            {
                _uiRefreshPending = false;
                RefreshUiState();
            }
        }
    }

    private void RefreshUiState()
    {
        var stats = DocumentStats.FromDocument(_editor.Document);
        UpdateSelectionOpacityLabel();
        UpdateHeroSummary(stats);
        UpdatePayloadMeta();
        UpdateCommandStates();
        UpdateStatusLabels(stats);
        UpdatePresetHints();
    }

    private void UpdateSelectionOpacityLabel()
    {
        _selectionOpacityLabel.Content = $"Selection opacity: {_selectionOpacitySlider.Value:0.00}";
    }

    private void UpdateHeroSummary(DocumentStats stats)
    {
        _heroSummaryLabel.Text =
            $"Preset: {_lastPresetName} | blocks {stats.BlockCount} | paragraphs {stats.ParagraphCount} | lists {stats.ListCount} | tables {stats.TableCount} | hosted UI {stats.HostedUiCount}";
    }

    private void UpdatePayloadMeta()
    {
        var format = GetSelectedFormat();
        var payloadLength = (_payloadPreviewTextBox.Text ?? string.Empty).Length;
        _payloadMetaLabel.Text =
            $"Format: {GetSelectedFormatLabel()} ({format}) | length {payloadLength} chars | can load: {_editor.CanLoad(format)} | can save: {_editor.CanSave(format)}";
    }

    private void UpdateCommandStates()
    {
        if (_undoButton != null)
        {
            _undoButton.IsEnabled = _editor.IsUndoEnabled && _editor.CanUndo;
        }

        if (_redoButton != null)
        {
            _redoButton.IsEnabled = _editor.IsUndoEnabled && _editor.CanRedo;
        }

        foreach (var pair in _commandButtons)
        {
            pair.Value.IsEnabled = pair.Key.CanExecute(null, _editor);
        }

        var selectedFormat = GetSelectedFormat();
        var hasPayload = !string.IsNullOrWhiteSpace(_payloadPreviewTextBox.Text);

        if (_exportDocumentButton != null)
        {
            _exportDocumentButton.IsEnabled = _editor.CanSave(selectedFormat);
        }

        if (_exportSelectionButton != null)
        {
            _exportSelectionButton.IsEnabled = _editor.SelectionLength > 0 && _editor.CanSave(selectedFormat);
        }

        if (_loadDocumentButton != null)
        {
            _loadDocumentButton.IsEnabled = hasPayload && _editor.CanLoad(selectedFormat);
        }

        if (_loadSelectionButton != null)
        {
            _loadSelectionButton.IsEnabled = hasPayload && _editor.CanLoad(selectedFormat);
        }
    }

    private void UpdateEditorCommandStates()
    {
        if (_undoButton != null)
        {
            _undoButton.IsEnabled = _editor.IsUndoEnabled && _editor.CanUndo;
        }

        if (_redoButton != null)
        {
            _redoButton.IsEnabled = _editor.IsUndoEnabled && _editor.CanRedo;
        }

        if (_exportSelectionButton != null)
        {
            _exportSelectionButton.IsEnabled = _editor.SelectionLength > 0 && _editor.CanSave(GetSelectedFormat());
        }
    }

    private void UpdateStatusLabels(DocumentStats stats)
    {
        var scrollMetrics = _editor.GetScrollMetricsSnapshot();
        _documentStatusLabel.Text =
            $"Document: {stats.CharacterCount} chars | paragraphs {stats.ParagraphCount} | hyperlinks {stats.HyperlinkCount} | inline UI {stats.InlineUiCount} | block UI {stats.BlockUiCount}";
        _selectionStatusLabel.Text =
            $"Selection: start {_editor.SelectionStart} | length {_editor.SelectionLength} | caret {_editor.CaretIndex} | text changes {_textChangedCount} | selection changes {_selectionChangedCount}";
        _viewportStatusLabel.Text =
            $"Viewport: x {scrollMetrics.HorizontalOffset:0.##}/{scrollMetrics.ExtentWidth:0.##} | y {scrollMetrics.VerticalOffset:0.##}/{scrollMetrics.ExtentHeight:0.##} | view {scrollMetrics.ViewportWidth:0.##} x {scrollMetrics.ViewportHeight:0.##}";
        _activityStatusLabel.Text = $"Activity: {_lastActivity} | hyperlink: {_lastNavigateUri}";
        _spellCheckStatusLabel.Text = _editor.IsSpellCheckEnabled
            ? "SpellCheck API is enabled. This demo exposes the WPF-facing surface, but no live spelling engine is attached in this parity slice."
            : "SpellCheck API is disabled. Enable it to exercise the public surface; spelling results remain empty because no engine is attached.";
    }

    private void UpdatePresetHints()
    {
        _toolbarHintLabel.Content = _lastPresetName switch
        {
            "Embedded UI" => "Hosted children active: compact inline and block buttons are shown in non-competing flow positions.",
            "Longform" => "Use Page Up / Page Down or the mouse wheel to exercise viewport movement.",
            "Structure" => "Try bulleted and numbered list toggles, then insert or merge table cells.",
            _ => "Ctrl+B / Ctrl+I / Ctrl+U / PageUp / Ctrl+Backspace"
        };

        _canvasFooterLabel.Text = _lastPresetName switch
        {
            "Welcome" => "The welcome document mixes inline formatting, hyperlink content, bullets, and a status table.",
            "Structure" => "The structured document is tuned for list, table, and outline-style editing commands.",
            "Embedded UI" => "Embedded UI demonstrates compact inline and block-hosted controls without crowding the current placeholder-based flow layout.",
            "Longform" => "Longform gives the editor enough content to demonstrate page navigation, scroll bars, and selection persistence.",
            _ => "Build from scratch: type, paste, or load payloads to create your own document state."
        };
    }

    private void SetActivity(string activity)
    {
        _lastActivity = activity.Trim();
    }

    private string GetSelectedFormat()
    {
        var index = _formatComboBox.SelectedIndex;
        if (index < 0 || index >= FormatOptions.Length)
        {
            return FormatOptions[0].Format;
        }

        return FormatOptions[index].Format;
    }

    private string GetSelectedFormatLabel()
    {
        var index = _formatComboBox.SelectedIndex;
        if (index < 0 || index >= FormatOptions.Length)
        {
            return FormatOptions[0].Label;
        }

        return FormatOptions[index].Label;
    }

    private static MemoryStream CreateUtf8Stream(string payload)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(payload));
    }

    private static string ReadUtf8(MemoryStream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private T RequireElement<T>(string name)
        where T : class
    {
        return this.FindName(name) as T
               ?? throw new InvalidOperationException($"Expected element '{name}' of type {typeof(T).Name}.");
    }

    private static FlowDocument CreateWelcomeDocument()
    {
        var document = new FlowDocument();

        var title = new Paragraph();
        var titleBold = new Bold();
        titleBold.Inlines.Add(new Run("RichTextBox Studio"));
        title.Inlines.Add(titleBold);
        title.Inlines.Add(new Run(" gives the Controls Catalog a real editing surface."));
        document.Blocks.Add(title);

        var body = new Paragraph();
        body.Inlines.Add(new Run("Use "));
        var bold = new Bold();
        bold.Inlines.Add(new Run("Bold"));
        body.Inlines.Add(bold);
        body.Inlines.Add(new Run(", "));
        var italic = new Italic();
        italic.Inlines.Add(new Run("Italic"));
        body.Inlines.Add(italic);
        body.Inlines.Add(new Run(", and "));
        var underline = new Underline();
        underline.Inlines.Add(new Run("Underline"));
        body.Inlines.Add(underline);
        body.Inlines.Add(new Run(" with the toolbar, or follow the "));
        var hyperlink = new Hyperlink { NavigateUri = "https://inkkslinger.dev/richtext" };
        hyperlink.Inlines.Add(new Run("documentation link"));
        body.Inlines.Add(hyperlink);
        body.Inlines.Add(new Run(" to inspect navigation events."));
        document.Blocks.Add(body);

        var list = new InkkSlinger.List();
        list.Items.Add(CreateListItem("Toggle formatting on a live selection."));
        list.Items.Add(CreateListItem("Round-trip the full document through Flow XML, XAML, XamlPackage, RTF, or plain text."));
        list.Items.Add(CreateListItem("Swap into embedded UI mode to click hosted buttons inside the document."));
        document.Blocks.Add(list);

        var table = CreateStatusTable(
            ("Mode", "Interactive"),
            ("Selection", "Live metrics"),
            ("Clipboard", "Flow XML, XAML, RTF, Text"));
        document.Blocks.Add(table);

        return document;
    }

    private static FlowDocument CreateStructuredDocument()
    {
        var document = new FlowDocument();

        var heading = new Paragraph();
        var headingBold = new Bold();
        headingBold.Inlines.Add(new Run("Sprint outline"));
        heading.Inlines.Add(headingBold);
        document.Blocks.Add(heading);

        var intro = new Paragraph();
        intro.Inlines.Add(new Run("This preset is tuned for list depth, numbered structure, and table operations."));
        document.Blocks.Add(intro);

        var orderedList = new InkkSlinger.List { IsOrdered = true };
        orderedList.Items.Add(CreateListItem("Review editor commands."));
        orderedList.Items.Add(CreateListItem("Promote and demote list levels."));
        orderedList.Items.Add(CreateListItem("Insert a new table or merge neighboring cells."));
        document.Blocks.Add(orderedList);

        var table = CreateStatusTable(
            ("Track", "Formatting"),
            ("Owner", "Controls Catalog"),
            ("Priority", "High"),
            ("Validation", "Focused regression tests"));
        document.Blocks.Add(table);

        return document;
    }

    private FlowDocument CreateEmbeddedUiDocument()
    {
        var document = new FlowDocument();

        var intro = new Paragraph();
        intro.Inlines.Add(new Run("Hosted UI proves that inline and block containers participate in layout and input."));
        document.Blocks.Add(intro);

        var inlineParagraph = new Paragraph();
        inlineParagraph.Inlines.Add(new Run("Inline tool: compact hosted button "));
        inlineParagraph.Inlines.Add(new InlineUIContainer
        {
            Child = CreateEmbeddedButton(
                "Inline",
                "Inline hosted button clicked.",
                width: 72f,
                height: 18f,
                fontSize: 11f,
                padding: new Thickness(8f, 2f, 8f, 2f))
        });
        document.Blocks.Add(inlineParagraph);

        var blockLabel = new Paragraph();
        blockLabel.Inlines.Add(new Run("Block host: standalone action button below."));
        document.Blocks.Add(blockLabel);

        var blockButton = CreateEmbeddedButton(
            "Block",
            "Block hosted button clicked.",
            width: 84f,
            height: 20f,
            fontSize: 11f,
            padding: new Thickness(10f, 3f, 10f, 3f));
        document.Blocks.Add(new BlockUIContainer { Child = blockButton });

        return document;
    }

    private static FlowDocument CreateLongformDocument()
    {
        var document = new FlowDocument();

        for (var index = 1; index <= 9; index++)
        {
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run($"Paragraph {index}. "));
            paragraph.Inlines.Add(new Run("This longer sample gives Page Up, Page Down, selection persistence, and scroll bar settings enough surface area to be visible immediately in the catalog. "));
            paragraph.Inlines.Add(new Run("Use the payload lab after editing to compare how the same content round-trips across the supported formats."));
            document.Blocks.Add(paragraph);
        }

        var footerTable = CreateStatusTable(
            ("Page keys", "Enabled"),
            ("Scroll offsets", "Visible in sidebar"),
            ("Selection export", "Available"));
        document.Blocks.Add(footerTable);

        return document;
    }

    private static FlowDocument CreateBlankDocument()
    {
        var document = new FlowDocument();
        document.Blocks.Add(new Paragraph());
        return document;
    }

    private Button CreateEmbeddedButton(
        string content,
        string activity,
        float width,
        float height,
        float fontSize,
        Thickness padding)
    {
        var button = new Button
        {
            Content = content,
            Style = new Style(typeof(Button)),
            Template = null,
            Width = width,
            Height = height,
            FontSize = fontSize,
            Padding = padding,
            BorderBrush = ResolveThemeColor("OrangePrimaryBrush", new Color(0xFF, 0x8C, 0x00)),
            Foreground = ResolveThemeColor("OrangePrimaryBrush", new Color(0xFF, 0x8C, 0x00))
        };

        var minimumWidth = UiTextRenderer.MeasureWidth(button, content, button.FontSize) + button.Padding.Horizontal + (button.BorderThickness * 2f);
        var minimumHeight = UiTextRenderer.GetLineHeight(button, button.FontSize) + button.Padding.Vertical + (button.BorderThickness * 2f);
        button.Width = MathF.Max(button.Width, minimumWidth);
        button.Height = MathF.Max(button.Height, minimumHeight);

        button.Click += (_, _) =>
        {
            SetActivity(activity);
            RequestUiRefresh();
        };

        return button;
    }

    private static ListItem CreateListItem(string text)
    {
        var item = new ListItem();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(text));
        item.Blocks.Add(paragraph);
        return item;
    }

    private static Table CreateStatusTable(params (string Left, string Right)[] rows)
    {
        var table = new Table();
        var rowGroup = new TableRowGroup();
        foreach (var (left, right) in rows)
        {
            var row = new TableRow();
            row.Cells.Add(CreateTableCell(left));
            row.Cells.Add(CreateTableCell(right));
            rowGroup.Rows.Add(row);
        }

        table.RowGroups.Add(rowGroup);
        return table;
    }

    private static TableCell CreateTableCell(string text)
    {
        var cell = new TableCell();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(text));
        cell.Blocks.Add(paragraph);
        return cell;
    }

    private static Color ResolveThemeColor(string key, Color fallback)
    {
        if (UiApplication.Current.Resources.TryGetValue(key, out var resource))
        {
            if (resource is Color color)
            {
                return color;
            }

            if (resource is Brush brush)
            {
                return brush.ToColor();
            }
        }

        return fallback;
    }

    private readonly record struct FormatOption(string Label, string Format);

    private readonly record struct DocumentStats(
        int BlockCount,
        int ParagraphCount,
        int ListCount,
        int TableCount,
        int HyperlinkCount,
        int InlineUiCount,
        int BlockUiCount,
        int HostedUiCount,
        int CharacterCount)
    {
        public static DocumentStats FromDocument(FlowDocument document)
        {
            var accumulator = new Accumulator();
            foreach (var block in document.Blocks)
            {
                accumulator.BlockCount++;
                AccumulateBlock(block, accumulator);
            }

            accumulator.CharacterCount = DocumentEditing.GetText(document).Length;
            return accumulator.ToStats();
        }

        private static void AccumulateBlock(Block block, Accumulator accumulator)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    accumulator.ParagraphCount++;
                    foreach (var inline in paragraph.Inlines)
                    {
                        AccumulateInline(inline, accumulator);
                    }
                    break;
                case Section section:
                    foreach (var nested in section.Blocks)
                    {
                        accumulator.BlockCount++;
                        AccumulateBlock(nested, accumulator);
                    }
                    break;
                case InkkSlinger.List list:
                    accumulator.ListCount++;
                    foreach (var item in list.Items)
                    {
                        foreach (var nested in item.Blocks)
                        {
                            accumulator.BlockCount++;
                            AccumulateBlock(nested, accumulator);
                        }
                    }
                    break;
                case Table table:
                    accumulator.TableCount++;
                    foreach (var group in table.RowGroups)
                    {
                        foreach (var row in group.Rows)
                        {
                            foreach (var cell in row.Cells)
                            {
                                foreach (var nested in cell.Blocks)
                                {
                                    accumulator.BlockCount++;
                                    AccumulateBlock(nested, accumulator);
                                }
                            }
                        }
                    }
                    break;
                case BlockUIContainer blockUiContainer:
                    accumulator.BlockUiCount++;
                    if (blockUiContainer.Child != null)
                    {
                        accumulator.HostedUiCount++;
                    }
                    break;
            }
        }

        private static void AccumulateInline(Inline inline, Accumulator accumulator)
        {
            switch (inline)
            {
                case Hyperlink hyperlink:
                    accumulator.HyperlinkCount++;
                    foreach (var nested in hyperlink.Inlines)
                    {
                        AccumulateInline(nested, accumulator);
                    }
                    break;
                case Span span:
                    foreach (var nested in span.Inlines)
                    {
                        AccumulateInline(nested, accumulator);
                    }
                    break;
                case InlineUIContainer inlineUiContainer:
                    accumulator.InlineUiCount++;
                    if (inlineUiContainer.Child != null)
                    {
                        accumulator.HostedUiCount++;
                    }
                    break;
            }
        }

        private sealed class Accumulator
        {
            public int BlockCount;
            public int ParagraphCount;
            public int ListCount;
            public int TableCount;
            public int HyperlinkCount;
            public int InlineUiCount;
            public int BlockUiCount;
            public int HostedUiCount;
            public int CharacterCount;

            public DocumentStats ToStats()
            {
                return new DocumentStats(
                    BlockCount,
                    ParagraphCount,
                    ListCount,
                    TableCount,
                    HyperlinkCount,
                    InlineUiCount,
                    BlockUiCount,
                    HostedUiCount,
                    CharacterCount);
            }
        }
    }
}




