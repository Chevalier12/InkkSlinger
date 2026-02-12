var isWindowDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--window-demo", global::System.StringComparison.OrdinalIgnoreCase));
var isMainMenuDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--main-menu", global::System.StringComparison.OrdinalIgnoreCase));
var isPaintShellDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--paint-shell", global::System.StringComparison.OrdinalIgnoreCase));
var isCommandingDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--commanding-demo", global::System.StringComparison.OrdinalIgnoreCase));
isCommandingDemo = isCommandingDemo || (!isWindowDemo && !isMainMenuDemo && !isPaintShellDemo);
using var game = new InkkSlinger.Game1(isWindowDemo, isPaintShellDemo, isCommandingDemo);
game.Run();
