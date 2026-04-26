using System;
using System.Collections.Generic;
using System.Reflection;

namespace InkkSlinger;

public sealed class InkkOopsHostConfiguration
{
    public const string BuiltInDefaultNamedPipeName = "InkkOops";

    public required IInkkOopsScriptCatalog ScriptCatalog { get; init; }

    public required IInkkOopsArtifactNamingPolicy ArtifactNamingPolicy { get; init; }

    public string DefaultNamedPipeName { get; init; } = string.Empty;

    public string DefaultArtifactRoot { get; init; } = string.Empty;

    public string DefaultRecordingRoot { get; init; } = string.Empty;

    public static InkkOopsHostConfiguration CreateDefault(Assembly scriptAssembly, IEnumerable<string>? additionalScriptAssemblyPaths = null)
    {
        ArgumentNullException.ThrowIfNull(scriptAssembly);

        var namingPolicy = new DefaultInkkOopsArtifactNamingPolicy();
        var assemblies = CreateDefaultAssemblies(scriptAssembly, additionalScriptAssemblyPaths);
        return new InkkOopsHostConfiguration
        {
            ScriptCatalog = new ReflectionInkkOopsScriptCatalog(assemblies),
            ArtifactNamingPolicy = namingPolicy,
            DefaultNamedPipeName = BuiltInDefaultNamedPipeName,
            DefaultArtifactRoot = "artifacts/inkkoops",
            DefaultRecordingRoot = "artifacts/inkkoops-recordings"
        };
    }

    private static IReadOnlyList<Assembly> CreateDefaultAssemblies(Assembly scriptAssembly, IEnumerable<string>? additionalScriptAssemblyPaths)
    {
        var assemblies = new List<Assembly>();
        var seenAssemblyNames = new HashSet<string>(StringComparer.Ordinal);

        AddAssembly(typeof(InkkOopsHostConfiguration).Assembly);
        AddAssembly(scriptAssembly);

        foreach (var assembly in InkkOopsScriptAssemblyLoader.LoadAssemblies(additionalScriptAssemblyPaths))
        {
            AddAssembly(assembly);
        }

        return assemblies;

        void AddAssembly(Assembly assembly)
        {
            var assemblyName = assembly.FullName ?? assembly.GetName().Name ?? string.Empty;
            if (seenAssemblyNames.Add(assemblyName))
            {
                assemblies.Add(assembly);
            }
        }
    }

}
