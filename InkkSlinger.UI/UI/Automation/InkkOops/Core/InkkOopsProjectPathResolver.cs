using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace InkkSlinger;

public static class InkkOopsProjectPathResolver
{
    public static string ResolveCurrentEntryProjectPath()
    {
        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var projectFiles = directory.GetFiles("*.csproj", SearchOption.TopDirectoryOnly);
            if (projectFiles.Length == 1)
            {
                return projectFiles[0].FullName;
            }

            if (!string.IsNullOrWhiteSpace(entryAssemblyName))
            {
                for (var i = 0; i < projectFiles.Length; i++)
                {
                    if (string.Equals(
                        Path.GetFileNameWithoutExtension(projectFiles[i].Name),
                        entryAssemblyName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return projectFiles[i].FullName;
                    }
                }
            }

            directory = directory.Parent;
        }

        return string.Empty;
    }
}