using CommunityToolkit.Mvvm.ComponentModel;

namespace InkkSlinger;

public partial class MainMenuViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "TextBox Performance Demo";

    [ObservableProperty]
    private string description = "Large-document editing and scrolling showcase for TextBox optimization work.";

    [ObservableProperty]
    private string status = "Ready";

    [ObservableProperty]
    private string documentStatsText = "Chars: 0 | Lines: 0";

    [ObservableProperty]
    private string viewportStatsText = "Offset X/Y: 0 / 0";

    [ObservableProperty]
    private string wrappingText = "Wrapping: Wrap";

    [ObservableProperty]
    private string inputTimingText = "Input(ms): Last 0 | Avg 0 | Max 0";

    [ObservableProperty]
    private string renderTimingText = "Render(ms): Last 0 | Avg 0 | Max 0";

    [ObservableProperty]
    private string viewportTimingText = "Viewport(ms): Last 0 | Avg 0 | Max 0 | Hit 0 | Miss 0";

    [ObservableProperty]
    private string caretTimingText = "Caret(ms): Last 0 | Avg 0 | Max 0";

    [ObservableProperty]
    private string frameLoopTimingText = "FrameLoop(ms): Update Last 0 | Avg 0 | Max 0; Draw Last 0 | Avg 0 | Max 0";

    [ObservableProperty]
    private string hitchLogText = "Hitches: 0";
}
