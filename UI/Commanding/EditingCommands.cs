namespace InkkSlinger;

public static class EditingCommands
{
    public static readonly RoutedCommand Backspace = new(nameof(Backspace), typeof(EditingCommands));
    public static readonly RoutedCommand Delete = new(nameof(Delete), typeof(EditingCommands));
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

    public static readonly RoutedCommand ToggleBold = new(nameof(ToggleBold), typeof(EditingCommands));
    public static readonly RoutedCommand ToggleItalic = new(nameof(ToggleItalic), typeof(EditingCommands));
    public static readonly RoutedCommand ToggleUnderline = new(nameof(ToggleUnderline), typeof(EditingCommands));

    public static readonly RoutedCommand IncreaseListLevel = new(nameof(IncreaseListLevel), typeof(EditingCommands));
    public static readonly RoutedCommand DecreaseListLevel = new(nameof(DecreaseListLevel), typeof(EditingCommands));
    public static readonly RoutedCommand InsertTable = new(nameof(InsertTable), typeof(EditingCommands));
    public static readonly RoutedCommand SplitCell = new(nameof(SplitCell), typeof(EditingCommands));
    public static readonly RoutedCommand MergeCells = new(nameof(MergeCells), typeof(EditingCommands));
}
