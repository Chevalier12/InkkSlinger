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
        if (!useWorkspaceStartup)
        {
            return new DesignerHostView();
        }

        var projectSession = TryOpenStartupProjectSession(inkkOopsOptions.LaunchProjectPath);
        return new DesignerShellView(projectSession: projectSession);
    }

    private static DesignerProjectSession? TryOpenStartupProjectSession(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !System.IO.Directory.Exists(projectPath))
        {
            return null;
        }

        return DesignerProjectSession.Open(projectPath, new PhysicalDesignerProjectFileStore());
    }
}
