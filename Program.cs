using InkkSlinger;

var isWindowDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--window-demo", global::System.StringComparison.OrdinalIgnoreCase));
var isMainMenuDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--main-menu", global::System.StringComparison.OrdinalIgnoreCase));
var isPaintShellDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--paint-shell", global::System.StringComparison.OrdinalIgnoreCase));
var isCommandingDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--commanding-demo", global::System.StringComparison.OrdinalIgnoreCase));
var isThreeScrollViewersDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--three-scroll-viewers", global::System.StringComparison.OrdinalIgnoreCase));
var isSimpleScrollViewerDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--simple-scroll-viewer", global::System.StringComparison.OrdinalIgnoreCase));
var isSimpleStackPanelDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--simple-stack-panel", global::System.StringComparison.OrdinalIgnoreCase));
var isScrollViewerTextBoxDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--scrollviewer-textbox-demo", global::System.StringComparison.OrdinalIgnoreCase));
var isItemsPresenterDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--items-presenter-demo", global::System.StringComparison.OrdinalIgnoreCase));
var isListBoxDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--listbox-demo", global::System.StringComparison.OrdinalIgnoreCase));
var isTwoScrollViewersDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--two-scroll-viewers", global::System.StringComparison.OrdinalIgnoreCase));
var isVirtualizedStackPanelDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--virtualized-stack-panel-demo", global::System.StringComparison.OrdinalIgnoreCase));
if (!isWindowDemo && !isMainMenuDemo && !isPaintShellDemo && !isCommandingDemo && !isThreeScrollViewersDemo && !isTwoScrollViewersDemo && !isSimpleScrollViewerDemo && !isSimpleStackPanelDemo && !isScrollViewerTextBoxDemo && !isListBoxDemo && !isItemsPresenterDemo && !isVirtualizedStackPanelDemo)
{
    isThreeScrollViewersDemo = true;
}
using var game = new InkkSlinger.Game1(
    isWindowDemo,
    isPaintShellDemo,
    isCommandingDemo,
    isThreeScrollViewersDemo,
    isTwoScrollViewersDemo,
    isSimpleScrollViewerDemo,
    isSimpleStackPanelDemo,
    isScrollViewerTextBoxDemo,
    isListBoxDemo,
    isItemsPresenterDemo,
    isVirtualizedStackPanelDemo);
game.Run();
