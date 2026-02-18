using System;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public partial class RichTextDiagnosticsLabView : UserControl
{
    private SpriteFont? _currentFont;

    public RichTextDiagnosticsLabView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "RichTextDiagnosticsLabView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        if (Editor != null)
        {
            Editor.HyperlinkNavigate += OnHyperlinkNavigate;
            SeedDocument();
            RefreshSnapshot();
        }
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        _currentFont = font;
        ApplyFontRecursive(this, font);
    }

    private void OnSeedDocumentClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        SeedDocument();
        AppendLog("Seeded lab document.");
    }

    private void OnSelectAllClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ExecuteCommand(EditingCommands.SelectAll, "SelectAll");
    }

    private void OnIndentClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ExecuteCommand(EditingCommands.IncreaseListLevel, "IncreaseListLevel");
    }

    private void OnOutdentClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ExecuteCommand(EditingCommands.DecreaseListLevel, "DecreaseListLevel");
    }

    private void OnInsertTableClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ExecuteCommand(EditingCommands.InsertTable, "InsertTable");
    }

    private void OnSplitCellClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ExecuteCommand(EditingCommands.SplitCell, "SplitCell");
    }

    private void OnMergeCellsClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ExecuteCommand(EditingCommands.MergeCells, "MergeCells");
    }

    private void OnToggleBoldClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ExecuteCommand(EditingCommands.ToggleBold, "ToggleBold");
    }

    private void OnCopyClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ExecuteCommand(EditingCommands.Copy, "Copy");
    }

    private void OnPasteClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ExecuteCommand(EditingCommands.Paste, "Paste");
    }

    private void OnUndoClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (Editor == null)
        {
            return;
        }

        var handled = Editor.HandleKeyDownFromInput(Keys.Z, ModifierKeys.Control);
        AppendLog($"Undo => {handled}");
        RefreshSnapshot();
    }

    private void OnRedoClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (Editor == null)
        {
            return;
        }

        var handled = Editor.HandleKeyDownFromInput(Keys.Y, ModifierKeys.Control);
        AppendLog($"Redo => {handled}");
        RefreshSnapshot();
    }

    private void OnStressEditClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (Editor == null)
        {
            return;
        }

        const int iterations = 200;
        var inserted = 0;
        for (var i = 0; i < iterations; i++)
        {
            if (Editor.HandleTextInputFromInput('x'))
            {
                inserted++;
            }
        }

        AppendLog($"StressEdit x{iterations} inserted={inserted}");
        RefreshSnapshot();
    }

    private void OnRefreshSnapshotClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        RefreshSnapshot();
        AppendLog("Snapshot refreshed.");
    }

    private void OnReadOnlyChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (Editor == null)
        {
            return;
        }

        Editor.IsReadOnly = ReadOnlyCheck?.IsChecked == true;
        AppendLog($"ReadOnly => {Editor.IsReadOnly}");
    }

    private void OnHyperlinkNavigate(object? sender, HyperlinkNavigateRoutedEventArgs args)
    {
        _ = sender;
        AppendLog($"HyperlinkNavigate => {args.NavigateUri}");
    }

    private void ExecuteCommand(RoutedCommand command, string label)
    {
        if (Editor == null)
        {
            return;
        }

        var canExecute = CommandManager.CanExecute(command, null, Editor);
        CommandManager.Execute(command, null, Editor);
        AppendLog($"{label} (canExecute={canExecute})");
        RefreshSnapshot();
    }

    private void RefreshSnapshot()
    {
        if (Editor == null || SnapshotLabel == null)
        {
            return;
        }

        var snapshot = Editor.GetPerformanceSnapshot();
        var summary = new StringBuilder();
        summary.Append($"Layout cache: hit={snapshot.LayoutCacheHitCount} miss={snapshot.LayoutCacheMissCount}");
        summary.Append($" | layout p95/p99={snapshot.P95LayoutBuildMilliseconds:0.000}/{snapshot.P99LayoutBuildMilliseconds:0.000}ms");
        summary.AppendLine();
        summary.Append($"Render avg/max={snapshot.AverageRenderMilliseconds:0.000}/{snapshot.MaxRenderMilliseconds:0.000}ms");
        summary.Append($" | selection avg={snapshot.AverageSelectionGeometryMilliseconds:0.000}ms");
        summary.AppendLine();
        summary.Append($"Clipboard serde: s={snapshot.ClipboardSerializeSampleCount} d={snapshot.ClipboardDeserializeSampleCount}");
        summary.Append($" | edit avg/max={snapshot.AverageEditMilliseconds:0.000}/{snapshot.MaxEditMilliseconds:0.000}ms");
        summary.AppendLine();
        summary.Append($"Undo depth/op={snapshot.UndoDepth}/{snapshot.UndoOperationCount}");
        summary.Append($" | Redo depth/op={snapshot.RedoDepth}/{snapshot.RedoOperationCount}");
        summary.AppendLine();
        summary.Append($"Caret={Editor.CaretIndex} SelStart={Editor.SelectionStart} SelLen={Editor.SelectionLength}");
        SnapshotLabel.Text = summary.ToString();
    }

    private void SeedDocument()
    {
        if (Editor == null)
        {
            return;
        }

        var document = new FlowDocument();

        var intro = new Paragraph();
        intro.Inlines.Add(new Run("Use "));
        var bold = new Bold();
        bold.Inlines.Add(new Run("Ctrl+Enter"));
        intro.Inlines.Add(bold);
        intro.Inlines.Add(new Run(" or click to activate "));
        var hyperlink = new Hyperlink { NavigateUri = "https://example.com/inkkslinger-richtext-lab" };
        hyperlink.Inlines.Add(new Run("this hyperlink"));
        intro.Inlines.Add(hyperlink);
        intro.Inlines.Add(new Run("."));
        document.Blocks.Add(intro);

        var list = new InkkSlinger.List { IsOrdered = true };
        list.Items.Add(CreateListItem("List item 1"));
        list.Items.Add(CreateListItem("List item 2"));
        document.Blocks.Add(list);

        var table = new Table();
        var rowGroup = new TableRowGroup();
        var row = new TableRow();
        row.Cells.Add(CreateTableCell("R1C1"));
        row.Cells.Add(CreateTableCell("R1C2"));
        rowGroup.Rows.Add(row);
        table.RowGroups.Add(rowGroup);
        document.Blocks.Add(table);

        Editor.Document = document;
        Editor.SetFocusedFromInput(true);
        Editor.ResetPerformanceSnapshot();
    }

    private static ListItem CreateListItem(string text)
    {
        var item = new ListItem();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(text));
        item.Blocks.Add(paragraph);
        return item;
    }

    private static TableCell CreateTableCell(string text)
    {
        var cell = new TableCell();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(text));
        cell.Blocks.Add(paragraph);
        return cell;
    }

    private void AppendLog(string message)
    {
        if (LogList == null)
        {
            return;
        }

        var line = $"{DateTime.Now:HH:mm:ss} {message}";
        var label = new Label { Text = line };
        if (_currentFont != null)
        {
            label.Font = _currentFont;
        }

        LogList.Items.Insert(0, label);
        while (LogList.Items.Count > 140)
        {
            LogList.Items.RemoveAt(LogList.Items.Count - 1);
        }
    }

    private static void ApplyFontRecursive(UIElement? element, SpriteFont font)
    {
        if (element == null)
        {
            return;
        }

        if (element is TextBlock textBlock)
        {
            textBlock.Font = font;
        }

        if (element is Label label)
        {
            label.Font = font;
        }

        if (element is RichTextBox richTextBox)
        {
            richTextBox.Font = font;
        }

        if (element is Button button)
        {
            button.Font = font;
        }

        if (element is CheckBox checkBox)
        {
            checkBox.Font = font;
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }
}
