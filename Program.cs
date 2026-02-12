var isWindowDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--window-demo", global::System.StringComparison.OrdinalIgnoreCase));
var isMainMenuDemo = global::System.Array.Exists(args,
    arg => string.Equals(arg, "--main-menu", global::System.StringComparison.OrdinalIgnoreCase));
var isPaintShellDemo = !isWindowDemo && !isMainMenuDemo;
using var game = new InkkSlinger.Game1(isWindowDemo, isPaintShellDemo);
game.Run();
