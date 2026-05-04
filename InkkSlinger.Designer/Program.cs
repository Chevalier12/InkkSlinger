using System;
using InkkSlinger;

namespace InkkSlinger.Designer;

internal static class Program
{
    private static void Main(string[] args)
    {
        var useWorkspaceStartup = Array.Exists(
            args,
            static arg => string.Equals(arg, "--designer-start-workspace", StringComparison.Ordinal));
        var inkkOopsOptions = App.ParseInkkOopsOptions(args);
        InkkSlingerUI.Initialize(
            () => CreateStartupView(useWorkspaceStartup, inkkOopsOptions),
            new InkkSlingerOptions
            {
                WindowTitle = "InkkSlinger Designer",
                FpsEnabled = true,
                IsBorderless = false,
                InkkOopsRuntimeOptions = inkkOopsOptions
            });
    }

    private static UIElement CreateStartupView(bool useWorkspaceStartup, InkkOopsRuntimeOptions inkkOopsOptions)
    {
        var hostView = new DesignerHostView();
        if (!useWorkspaceStartup)
        {
            return hostView;
        }

        if (!string.IsNullOrWhiteSpace(inkkOopsOptions.LaunchProjectPath) &&
            System.IO.Directory.Exists(inkkOopsOptions.LaunchProjectPath))
        {
            hostView.ViewModel.OpenProjectCommand.Execute(inkkOopsOptions.LaunchProjectPath);
        }

        return hostView;
    }
}
