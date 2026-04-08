using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace InkkSlinger;

public static class InkkOopsScriptAssemblyLoader
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, Assembly> LoadedAssemblies = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<Assembly> LoadAssemblies(IEnumerable<string>? assemblyPaths)
    {
        if (assemblyPaths == null)
        {
            return [];
        }

        var assemblies = new List<Assembly>();
        foreach (var assemblyPath in assemblyPaths)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                continue;
            }

            assemblies.Add(LoadAssembly(assemblyPath));
        }

        return assemblies;
    }

    public static Assembly LoadAssembly(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        var fullPath = Path.GetFullPath(assemblyPath);
        lock (Sync)
        {
            if (LoadedAssemblies.TryGetValue(fullPath, out var existing))
            {
                return existing;
            }

            var alreadyLoaded = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(assembly =>
                    !string.IsNullOrWhiteSpace(assembly.Location) &&
                    string.Equals(Path.GetFullPath(assembly.Location), fullPath, StringComparison.OrdinalIgnoreCase));
            if (alreadyLoaded != null)
            {
                LoadedAssemblies[fullPath] = alreadyLoaded;
                return alreadyLoaded;
            }

            var context = new ScriptAssemblyLoadContext(fullPath);
            var loadedAssembly = context.LoadFromAssemblyPath(fullPath);
            LoadedAssemblies[fullPath] = loadedAssembly;
            return loadedAssembly;
        }
    }

    private sealed class ScriptAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public ScriptAssemblyLoadContext(string assemblyPath)
            : base($"InkkOopsScript:{Path.GetFileNameWithoutExtension(assemblyPath)}", isCollectible: false)
        {
            _resolver = new AssemblyDependencyResolver(assemblyPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var defaultAssembly = AssemblyLoadContext.Default.Assemblies
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName.Name, StringComparison.Ordinal));
            if (defaultAssembly != null)
            {
                return defaultAssembly;
            }

            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return string.IsNullOrWhiteSpace(assemblyPath)
                ? null
                : LoadFromAssemblyPath(assemblyPath);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return string.IsNullOrWhiteSpace(libraryPath)
                ? IntPtr.Zero
                : LoadUnmanagedDllFromPath(libraryPath);
        }
    }
}