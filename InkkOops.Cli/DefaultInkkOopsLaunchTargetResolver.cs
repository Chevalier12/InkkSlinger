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
        return new InkkOopsLaunchTarget(
            Path.Combine(repoRoot, "InkkSlinger.csproj"),
            repoRoot);
    }
}
