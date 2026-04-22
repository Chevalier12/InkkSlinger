using System;
using System.Collections.Generic;
using System.Reflection;

namespace InkkSlinger;

public sealed class InkkOopsHostConfiguration
{
    public required IInkkOopsScriptCatalog ScriptCatalog { get; init; }

    public required IInkkOopsArtifactNamingPolicy ArtifactNamingPolicy { get; init; }

    public required IInkkOopsDiagnosticsSerializer DiagnosticsSerializer { get; init; }

    public required IInkkOopsDiagnosticsFilterPolicy DiagnosticsFilterPolicy { get; init; }

    public required IReadOnlyList<IInkkOopsDiagnosticsContributor> DiagnosticsContributors { get; init; }

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
            DiagnosticsSerializer = new DefaultInkkOopsDiagnosticsSerializer(),
            DiagnosticsFilterPolicy = new DefaultInkkOopsDiagnosticsFilterPolicy(),
            DiagnosticsContributors = CreateDefaultDiagnosticsContributors(assemblies),
            DefaultNamedPipeName = "InkkOops",
            DefaultArtifactRoot = "artifacts/inkkoops",
            DefaultRecordingRoot = "artifacts/inkkoops-recordings"
        };
    }

    private static IReadOnlyList<IInkkOopsDiagnosticsContributor> CreateDefaultDiagnosticsContributors(IReadOnlyList<Assembly> assemblies)
    {
        _ = assemblies;

        var contributors = new List<IInkkOopsDiagnosticsContributor>
        {
            // Rebuilt from zero for the completion dropdown scroll FPS investigation.
            new InkkOopsGenericElementDiagnosticsContributor(),
            new InkkOopsFrameworkElementDiagnosticsContributor(),
            new InkkOopsControlDiagnosticsContributor(),
            new InkkOopsUserControlDiagnosticsContributor(),
            new InkkOopsScrollViewerDiagnosticsContributor(),
            new InkkOopsVirtualizingStackPanelDiagnosticsContributor()
        };

        contributors.Sort(static (left, right) =>
        {
            var orderComparison = left.Order.CompareTo(right.Order);
            return orderComparison != 0
                ? orderComparison
                : StringComparer.Ordinal.Compare(left.GetType().FullName, right.GetType().FullName);
        });
        return contributors;
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
