using System;
using System.Collections.Generic;
using System.IO;

namespace InkkSlinger.Cli;

internal sealed class DefaultInkkOopsLaunchTargetResolver : IInkkOopsLaunchTargetResolver
{
    public InkkOopsLaunchTarget Resolve(IReadOnlyDictionary<string, string> options)
    {
        if (options.TryGetValue("project", out var projectPath) && !string.IsNullOrWhiteSpace(projectPath))
        {
            var fullProjectPath = Path.GetFullPath(projectPath);
            return new InkkOopsLaunchTarget(
                fullProjectPath,
                Path.GetDirectoryName(fullProjectPath) ?? Environment.CurrentDirectory);
        }

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var defaultProjectPath = Path.Combine(repoRoot, "InkkSlinger.DemoApp", "InkkSlinger.DemoApp.csproj");
        return new InkkOopsLaunchTarget(
            defaultProjectPath,
            Path.GetDirectoryName(defaultProjectPath) ?? repoRoot);
    }
}
