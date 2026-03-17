namespace InkkSlinger;

public static class EditingCommands
{
    public static readonly RoutedCommand Backspace = new(nameof(Backspace), typeof(EditingCommands));
    public static readonly RoutedCommand Delete = new(nameof(Delete), typeof(EditingCommands));
    public static readonly RoutedCommand DeletePreviousWord = new(nameof(DeletePreviousWord), typeof(EditingCommands));
    public static readonly RoutedCommand DeleteNextWord = new(nameof(DeleteNextWord), typeof(EditingCommands));
    public static readonly RoutedCommand EnterParagraphBreak = new(nameof(EnterParagraphBreak), typeof(EditingCommands));
    public static readonly RoutedCommand EnterLineBreak = new(nameof(EnterLineBreak), typeof(EditingCommands));
    public static readonly RoutedCommand TabForward = new(nameof(TabForward), typeof(EditingCommands));
    public static readonly RoutedCommand TabBackward = new(nameof(TabBackward), typeof(EditingCommands));

    public static readonly RoutedCommand Copy = new(nameof(Copy), typeof(EditingCommands));
    public static readonly RoutedCommand Cut = new(nameof(Cut), typeof(EditingCommands));
    public static readonly RoutedCommand Paste = new(nameof(Paste), typeof(EditingCommands));

    public static readonly RoutedCommand SelectAll = new(nameof(SelectAll), typeof(EditingCommands));
    public static readonly RoutedCommand MoveLeftByCharacter = new(nameof(MoveLeftByCharacter), typeof(EditingCommands));
    public static readonly RoutedCommand MoveRightByCharacter = new(nameof(MoveRightByCharacter), typeof(EditingCommands));
    public static readonly RoutedCommand MoveLeftByWord = new(nameof(MoveLeftByWord), typeof(EditingCommands));
    public static readonly RoutedCommand MoveRightByWord = new(nameof(MoveRightByWord), typeof(EditingCommands));
    public static readonly RoutedCommand SelectLeftByCharacter = new(nameof(SelectLeftByCharacter), typeof(EditingCommands));
    public static readonly RoutedCommand SelectRightByCharacter = new(nameof(SelectRightByCharacter), typeof(EditingCommands));
    public static readonly RoutedCommand SelectLeftByWord = new(nameof(SelectLeftByWord), typeof(EditingCommands));
    public static readonly RoutedCommand SelectRightByWord = new(nameof(SelectRightByWord), typeof(EditingCommands));
    public static readonly RoutedCommand MoveUpByLine = new(nameof(MoveUpByLine), typeof(EditingCommands));
    public static readonly RoutedCommand MoveDownByLine = new(nameof(MoveDownByLine), typeof(EditingCommands));
    public static readonly RoutedCommand MoveUpByPage = new(nameof(MoveUpByPage), typeof(EditingCommands));
    public static readonly RoutedCommand MoveDownByPage = new(nameof(MoveDownByPage), typeof(EditingCommands));
    public static readonly RoutedCommand MoveUpByParagraph = new(nameof(MoveUpByParagraph), typeof(EditingCommands));
    public static readonly RoutedCommand MoveDownByParagraph = new(nameof(MoveDownByParagraph), typeof(EditingCommands));
    public static readonly RoutedCommand MoveToLineStart = new(nameof(MoveToLineStart), typeof(EditingCommands));
    public static readonly RoutedCommand MoveToLineEnd = new(nameof(MoveToLineEnd), typeof(EditingCommands));
    public static readonly RoutedCommand MoveToParagraphStart = new(nameof(MoveToParagraphStart), typeof(EditingCommands));
    public static readonly RoutedCommand MoveToParagraphEnd = new(nameof(MoveToParagraphEnd), typeof(EditingCommands));
    public static readonly RoutedCommand MoveToDocumentStart = new(nameof(MoveToDocumentStart), typeof(EditingCommands));
    public static readonly RoutedCommand MoveToDocumentEnd = new(nameof(MoveToDocumentEnd), typeof(EditingCommands));
    public static readonly RoutedCommand SelectUpByLine = new(nameof(SelectUpByLine), typeof(EditingCommands));
    public static readonly RoutedCommand SelectDownByLine = new(nameof(SelectDownByLine), typeof(EditingCommands));
    public static readonly RoutedCommand SelectUpByPage = new(nameof(SelectUpByPage), typeof(EditingCommands));
    public static readonly RoutedCommand SelectDownByPage = new(nameof(SelectDownByPage), typeof(EditingCommands));
    public static readonly RoutedCommand SelectUpByParagraph = new(nameof(SelectUpByParagraph), typeof(EditingCommands));
    public static readonly RoutedCommand SelectDownByParagraph = new(nameof(SelectDownByParagraph), typeof(EditingCommands));
    public static readonly RoutedCommand SelectToLineStart = new(nameof(SelectToLineStart), typeof(EditingCommands));
    public static readonly RoutedCommand SelectToLineEnd = new(nameof(SelectToLineEnd), typeof(EditingCommands));
    public static readonly RoutedCommand SelectToParagraphStart = new(nameof(SelectToParagraphStart), typeof(EditingCommands));
    public static readonly RoutedCommand SelectToParagraphEnd = new(nameof(SelectToParagraphEnd), typeof(EditingCommands));
    public static readonly RoutedCommand SelectToDocumentStart = new(nameof(SelectToDocumentStart), typeof(EditingCommands));
    public static readonly RoutedCommand SelectToDocumentEnd = new(nameof(SelectToDocumentEnd), typeof(EditingCommands));

    public static readonly RoutedCommand ToggleBold = new(nameof(ToggleBold), typeof(EditingCommands));
    public static readonly RoutedCommand ToggleItalic = new(nameof(ToggleItalic), typeof(EditingCommands));
    public static readonly RoutedCommand ToggleUnderline = new(nameof(ToggleUnderline), typeof(EditingCommands));
    public static readonly RoutedCommand ToggleBullets = new(nameof(ToggleBullets), typeof(EditingCommands));
    public static readonly RoutedCommand ToggleNumbering = new(nameof(ToggleNumbering), typeof(EditingCommands));

    public static readonly RoutedCommand IncreaseListLevel = new(nameof(IncreaseListLevel), typeof(EditingCommands));
    public static readonly RoutedCommand DecreaseListLevel = new(nameof(DecreaseListLevel), typeof(EditingCommands));
    public static readonly RoutedCommand InsertTable = new(nameof(InsertTable), typeof(EditingCommands));
    public static readonly RoutedCommand SplitCell = new(nameof(SplitCell), typeof(EditingCommands));
    public static readonly RoutedCommand MergeCells = new(nameof(MergeCells), typeof(EditingCommands));
}
