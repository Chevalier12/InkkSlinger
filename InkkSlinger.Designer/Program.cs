using InkkSlinger;

namespace InkkSlinger.Designer;

internal static class Program
{
    private static void Main(string[] args)
    {
        InkkSlingerUI.Initialize(
            static () => new DesignerShellView(),
            new InkkSlingerOptions
            {
                WindowTitle = "InkkSlinger Designer",
                FpsEnabled = true,
                InkkOopsRuntimeOptions = App.ParseInkkOopsOptions(args)
            });
    }
}