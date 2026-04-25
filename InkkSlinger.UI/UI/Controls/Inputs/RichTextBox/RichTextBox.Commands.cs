using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public partial class RichTextBox
{
    private void RegisterEditingCommandBindings()
    {
        CommandBindings.Add(new CommandBinding(EditingCommands.Backspace, (_, _) => ExecuteBackspace(), (_, args) => args.CanExecute = CanBackspace()));
        CommandBindings.Add(new CommandBinding(EditingCommands.Delete, (_, _) => ExecuteDelete(), (_, args) => args.CanExecute = CanDelete()));
        CommandBindings.Add(new CommandBinding(EditingCommands.DeletePreviousWord, (_, _) => ExecuteDeletePreviousWord(), (_, args) => args.CanExecute = CanBackspace()));
        CommandBindings.Add(new CommandBinding(EditingCommands.DeleteNextWord, (_, _) => ExecuteDeleteNextWord(), (_, args) => args.CanExecute = CanDelete()));
        CommandBindings.Add(new CommandBinding(EditingCommands.EnterParagraphBreak, (_, _) => ExecuteEnterParagraphBreak(), (_, args) => args.CanExecute = CanMutateText()));
        CommandBindings.Add(new CommandBinding(EditingCommands.EnterLineBreak, (_, _) => ExecuteEnterLineBreak(), (_, args) => args.CanExecute = CanMutateText()));
        CommandBindings.Add(new CommandBinding(EditingCommands.TabForward, (_, _) => ExecuteTabForward(), (_, args) => args.CanExecute = CanMutateText()));
        CommandBindings.Add(new CommandBinding(EditingCommands.TabBackward, (_, _) => ExecuteTabBackward(), (_, args) => args.CanExecute = CanTabBackward()));

        CommandBindings.Add(new CommandBinding(EditingCommands.Copy, (_, _) => ExecuteCopy(), (_, args) => args.CanExecute = SelectionLength > 0));
        CommandBindings.Add(new CommandBinding(EditingCommands.Cut, (_, _) => ExecuteCut(), (_, args) => args.CanExecute = !IsReadOnly && SelectionLength > 0));
        CommandBindings.Add(new CommandBinding(EditingCommands.Paste, (_, _) => ExecutePaste(), (_, args) => args.CanExecute = !IsReadOnly && CanPasteFromClipboard()));

        CommandBindings.Add(new CommandBinding(EditingCommands.SelectAll, (_, _) => ExecuteSelectAllCore(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveLeftByCharacter, (_, _) => ExecuteMoveLeftByCharacter(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveRightByCharacter, (_, _) => ExecuteMoveRightByCharacter(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveLeftByWord, (_, _) => ExecuteMoveLeftByWord(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveRightByWord, (_, _) => ExecuteMoveRightByWord(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectLeftByCharacter, (_, _) => ExecuteSelectLeftByCharacter(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectRightByCharacter, (_, _) => ExecuteSelectRightByCharacter(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectLeftByWord, (_, _) => ExecuteSelectLeftByWord(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectRightByWord, (_, _) => ExecuteSelectRightByWord(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveUpByLine, (_, _) => ExecuteMoveUpByLine(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveDownByLine, (_, _) => ExecuteMoveDownByLine(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveUpByPage, (_, _) => ExecuteMoveUpByPage(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveDownByPage, (_, _) => ExecuteMoveDownByPage(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveUpByParagraph, (_, _) => ExecuteMoveUpByParagraph(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveDownByParagraph, (_, _) => ExecuteMoveDownByParagraph(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveToLineStart, (_, _) => ExecuteMoveToLineStart(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveToLineEnd, (_, _) => ExecuteMoveToLineEnd(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveToParagraphStart, (_, _) => ExecuteMoveToParagraphStart(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveToParagraphEnd, (_, _) => ExecuteMoveToParagraphEnd(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveToDocumentStart, (_, _) => ExecuteMoveToDocumentStart(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.MoveToDocumentEnd, (_, _) => ExecuteMoveToDocumentEnd(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectUpByLine, (_, _) => ExecuteSelectUpByLine(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectDownByLine, (_, _) => ExecuteSelectDownByLine(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectUpByPage, (_, _) => ExecuteSelectUpByPage(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectDownByPage, (_, _) => ExecuteSelectDownByPage(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectUpByParagraph, (_, _) => ExecuteSelectUpByParagraph(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectDownByParagraph, (_, _) => ExecuteSelectDownByParagraph(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectToLineStart, (_, _) => ExecuteSelectToLineStart(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectToLineEnd, (_, _) => ExecuteSelectToLineEnd(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectToParagraphStart, (_, _) => ExecuteSelectToParagraphStart(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectToParagraphEnd, (_, _) => ExecuteSelectToParagraphEnd(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectToDocumentStart, (_, _) => ExecuteSelectToDocumentStart(), (_, args) => args.CanExecute = true));
        CommandBindings.Add(new CommandBinding(EditingCommands.SelectToDocumentEnd, (_, _) => ExecuteSelectToDocumentEnd(), (_, args) => args.CanExecute = true));

        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleBold, (_, _) => ExecuteToggleBold(), (_, args) => args.CanExecute = !IsReadOnly));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleItalic, (_, _) => ExecuteToggleItalic(), (_, args) => args.CanExecute = !IsReadOnly));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleUnderline, (_, _) => ExecuteToggleUnderline(), (_, args) => args.CanExecute = !IsReadOnly));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleBullets, (_, _) => ExecuteToggleBullets(), (_, args) => args.CanExecute = CanExecuteListStyleToggle()));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleNumbering, (_, _) => ExecuteToggleNumbering(), (_, args) => args.CanExecute = CanExecuteListStyleToggle()));

        CommandBindings.Add(new CommandBinding(EditingCommands.IncreaseListLevel, (_, _) => ExecuteIncreaseListLevel(), (_, args) => args.CanExecute = CanExecuteListLevelChange(increase: true)));
        CommandBindings.Add(new CommandBinding(EditingCommands.DecreaseListLevel, (_, _) => ExecuteDecreaseListLevel(), (_, args) => args.CanExecute = CanExecuteListLevelChange(increase: false)));
        CommandBindings.Add(new CommandBinding(EditingCommands.InsertTable, (_, _) => ExecuteInsertTable(), (_, args) => args.CanExecute = !IsReadOnly));
        CommandBindings.Add(
            new CommandBinding(
                EditingCommands.SplitCell,
                (_, _) => ExecuteSplitCell(),
                (_, args) =>
                {
                    args.CanExecute = !IsReadOnly && TryGetActiveTableCell(Document, _caretIndex, out TableCellSelectionInfo _);
                }));
        CommandBindings.Add(new CommandBinding(EditingCommands.MergeCells, (_, _) => ExecuteMergeCells(), (_, args) => args.CanExecute = !IsReadOnly && CanMergeActiveCell()));
    }

    private void RegisterEditingInputBindings()
    {
        AddEditingKeyBinding(Keys.Back, ModifierKeys.None, EditingCommands.Backspace);
        AddEditingKeyBinding(Keys.Delete, ModifierKeys.None, EditingCommands.Delete);
        AddEditingKeyBinding(Keys.Back, ModifierKeys.Control, EditingCommands.DeletePreviousWord);
        AddEditingKeyBinding(Keys.Delete, ModifierKeys.Control, EditingCommands.DeleteNextWord);
        AddEditingKeyBinding(Keys.Enter, ModifierKeys.None, EditingCommands.EnterParagraphBreak);
        AddEditingKeyBinding(Keys.Enter, ModifierKeys.Shift, EditingCommands.EnterLineBreak);
        AddEditingKeyBinding(Keys.Tab, ModifierKeys.None, EditingCommands.TabForward);
        AddEditingKeyBinding(Keys.Tab, ModifierKeys.Shift, EditingCommands.TabBackward);

        AddEditingKeyBinding(Keys.C, ModifierKeys.Control, EditingCommands.Copy);
        AddEditingKeyBinding(Keys.X, ModifierKeys.Control, EditingCommands.Cut);
        AddEditingKeyBinding(Keys.V, ModifierKeys.Control, EditingCommands.Paste);
        AddEditingKeyBinding(Keys.A, ModifierKeys.Control, EditingCommands.SelectAll);

        AddEditingKeyBinding(Keys.Left, ModifierKeys.None, EditingCommands.MoveLeftByCharacter);
        AddEditingKeyBinding(Keys.Right, ModifierKeys.None, EditingCommands.MoveRightByCharacter);
        AddEditingKeyBinding(Keys.Left, ModifierKeys.Control, EditingCommands.MoveLeftByWord);
        AddEditingKeyBinding(Keys.Right, ModifierKeys.Control, EditingCommands.MoveRightByWord);
        AddEditingKeyBinding(Keys.Up, ModifierKeys.None, EditingCommands.MoveUpByLine);
        AddEditingKeyBinding(Keys.Down, ModifierKeys.None, EditingCommands.MoveDownByLine);
        AddEditingKeyBinding(Keys.PageUp, ModifierKeys.None, EditingCommands.MoveUpByPage);
        AddEditingKeyBinding(Keys.PageDown, ModifierKeys.None, EditingCommands.MoveDownByPage);
        AddEditingKeyBinding(Keys.Up, ModifierKeys.Control, EditingCommands.MoveUpByParagraph);
        AddEditingKeyBinding(Keys.Down, ModifierKeys.Control, EditingCommands.MoveDownByParagraph);
        AddEditingKeyBinding(Keys.Home, ModifierKeys.None, EditingCommands.MoveToLineStart);
        AddEditingKeyBinding(Keys.End, ModifierKeys.None, EditingCommands.MoveToLineEnd);
        AddEditingKeyBinding(Keys.Home, ModifierKeys.Control, EditingCommands.MoveToDocumentStart);
        AddEditingKeyBinding(Keys.End, ModifierKeys.Control, EditingCommands.MoveToDocumentEnd);
        AddEditingKeyBinding(Keys.Left, ModifierKeys.Shift, EditingCommands.SelectLeftByCharacter);
        AddEditingKeyBinding(Keys.Right, ModifierKeys.Shift, EditingCommands.SelectRightByCharacter);
        AddEditingKeyBinding(Keys.Left, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.SelectLeftByWord);
        AddEditingKeyBinding(Keys.Right, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.SelectRightByWord);
        AddEditingKeyBinding(Keys.Up, ModifierKeys.Shift, EditingCommands.SelectUpByLine);
        AddEditingKeyBinding(Keys.Down, ModifierKeys.Shift, EditingCommands.SelectDownByLine);
        AddEditingKeyBinding(Keys.PageUp, ModifierKeys.Shift, EditingCommands.SelectUpByPage);
        AddEditingKeyBinding(Keys.PageDown, ModifierKeys.Shift, EditingCommands.SelectDownByPage);
        AddEditingKeyBinding(Keys.Up, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.SelectUpByParagraph);
        AddEditingKeyBinding(Keys.Down, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.SelectDownByParagraph);
        AddEditingKeyBinding(Keys.Home, ModifierKeys.Shift, EditingCommands.SelectToLineStart);
        AddEditingKeyBinding(Keys.End, ModifierKeys.Shift, EditingCommands.SelectToLineEnd);
        AddEditingKeyBinding(Keys.Home, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.SelectToDocumentStart);
        AddEditingKeyBinding(Keys.End, ModifierKeys.Control | ModifierKeys.Shift, EditingCommands.SelectToDocumentEnd);

        AddEditingKeyBinding(Keys.B, ModifierKeys.Control, EditingCommands.ToggleBold);
        AddEditingKeyBinding(Keys.I, ModifierKeys.Control, EditingCommands.ToggleItalic);
        AddEditingKeyBinding(Keys.U, ModifierKeys.Control, EditingCommands.ToggleUnderline);
    }

    private void AddEditingKeyBinding(Keys key, ModifierKeys modifiers, RoutedCommand command)
    {
        InputBindings.Add(
            new KeyBinding
            {
                Key = key,
                Modifiers = modifiers,
                Command = command
            });
    }

    private bool TryExecuteEditingCommandFromKey(Keys key, ModifierKeys modifiers)
    {
        if ((key == Keys.Enter && (modifiers & ModifierKeys.Control) == 0 && !AcceptsReturn) ||
            (key == Keys.Tab && !AcceptsTab))
        {
            return false;
        }

        if (!HasEditingKeyBinding(key, modifiers))
        {
            return false;
        }

        return InputGestureService.Execute(key, modifiers, this, this);
    }

    private bool HasEditingKeyBinding(Keys key, ModifierKeys modifiers)
    {
        for (var i = 0; i < InputBindings.Count; i++)
        {
            if (InputBindings[i] is KeyBinding binding && binding.Matches(key, modifiers))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanMutateText()
    {
        return !IsReadOnly;
    }

    private bool CanBackspace()
    {
        return !IsReadOnly && (SelectionLength > 0 || _caretIndex > 0);
    }

    private bool CanDelete()
    {
        return !IsReadOnly && (SelectionLength > 0 || _caretIndex < GetText().Length);
    }
}