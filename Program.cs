using System;
using System.Collections.Generic;
using InkkSlinger;

static int[] ParseActionDiagnosticsIndexes(string? text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return [];
    }

    var parts = text.Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var values = new List<int>(parts.Length);
    for (var i = 0; i < parts.Length; i++)
    {
        if (int.TryParse(parts[i], out var value) && value >= 0)
        {
            values.Add(value);
        }
    }

    return [.. values];
}

static string[] AppendScriptAssemblyPath(string[] existing, string assemblyPath)
{
    if (string.IsNullOrWhiteSpace(assemblyPath))
    {
        return existing;
    }

    return [.. existing, assemblyPath];
}

static InkkOopsRuntimeOptions ParseInkkOopsOptions(string[] args)
{
    var options = new InkkOopsRuntimeOptions();
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--inkkoops-script" when i + 1 < args.Length:
                options = new InkkOopsRuntimeOptions
                {
                    StartupScriptName = args[++i],
                    AdditionalScriptAssemblyPaths = options.AdditionalScriptAssemblyPaths,
                    ActionDiagnosticsIndexes = options.ActionDiagnosticsIndexes,
                    NamedPipeName = options.NamedPipeName,
                    ArtifactRoot = options.ArtifactRoot,
                    RecordUserSession = options.RecordUserSession,
                    RecordingRoot = options.RecordingRoot,
                    StartupRecordingPath = options.StartupRecordingPath,
                    DisableRetainedRenderList = options.DisableRetainedRenderList,
                    DisableDirtyRegionRendering = options.DisableDirtyRegionRendering
                };
                break;
            case "--inkkoops-script-assembly" when i + 1 < args.Length:
                options = new InkkOopsRuntimeOptions
                {
                    StartupScriptName = options.StartupScriptName,
                    AdditionalScriptAssemblyPaths = AppendScriptAssemblyPath(options.AdditionalScriptAssemblyPaths, args[++i]),
                    ActionDiagnosticsIndexes = options.ActionDiagnosticsIndexes,
                    NamedPipeName = options.NamedPipeName,
                    ArtifactRoot = options.ArtifactRoot,
                    RecordUserSession = options.RecordUserSession,
                    RecordingRoot = options.RecordingRoot,
                    StartupRecordingPath = options.StartupRecordingPath,
                    DisableRetainedRenderList = options.DisableRetainedRenderList,
                    DisableDirtyRegionRendering = options.DisableDirtyRegionRendering
                };
                break;
            case "--inkkoops-pipe" when i + 1 < args.Length:
                options = new InkkOopsRuntimeOptions
                {
                    StartupScriptName = options.StartupScriptName,
                    AdditionalScriptAssemblyPaths = options.AdditionalScriptAssemblyPaths,
                    ActionDiagnosticsIndexes = options.ActionDiagnosticsIndexes,
                    NamedPipeName = args[++i],
                    ArtifactRoot = options.ArtifactRoot,
                    RecordUserSession = options.RecordUserSession,
                    RecordingRoot = options.RecordingRoot,
                    StartupRecordingPath = options.StartupRecordingPath,
                    DisableRetainedRenderList = options.DisableRetainedRenderList,
                    DisableDirtyRegionRendering = options.DisableDirtyRegionRendering
                };
                break;
            case "--inkkoops-artifacts" when i + 1 < args.Length:
                options = new InkkOopsRuntimeOptions
                {
                    StartupScriptName = options.StartupScriptName,
                    AdditionalScriptAssemblyPaths = options.AdditionalScriptAssemblyPaths,
                    ActionDiagnosticsIndexes = options.ActionDiagnosticsIndexes,
                    NamedPipeName = options.NamedPipeName,
                    ArtifactRoot = args[++i],
                    RecordUserSession = options.RecordUserSession,
                    RecordingRoot = options.RecordingRoot,
                    StartupRecordingPath = options.StartupRecordingPath,
                    DisableRetainedRenderList = options.DisableRetainedRenderList,
                    DisableDirtyRegionRendering = options.DisableDirtyRegionRendering
                };
                break;
            case "--inkkoops-action-diagnostics" when i + 1 < args.Length:
                options = new InkkOopsRuntimeOptions
                {
                    StartupScriptName = options.StartupScriptName,
                    AdditionalScriptAssemblyPaths = options.AdditionalScriptAssemblyPaths,
                    ActionDiagnosticsIndexes = ParseActionDiagnosticsIndexes(args[++i]),
                    NamedPipeName = options.NamedPipeName,
                    ArtifactRoot = options.ArtifactRoot,
                    RecordUserSession = options.RecordUserSession,
                    RecordingRoot = options.RecordingRoot,
                    StartupRecordingPath = options.StartupRecordingPath,
                    DisableRetainedRenderList = options.DisableRetainedRenderList,
                    DisableDirtyRegionRendering = options.DisableDirtyRegionRendering
                };
                break;
            case "--inkkoops-record":
                options = new InkkOopsRuntimeOptions
                {
                    StartupScriptName = options.StartupScriptName,
                    AdditionalScriptAssemblyPaths = options.AdditionalScriptAssemblyPaths,
                    ActionDiagnosticsIndexes = options.ActionDiagnosticsIndexes,
                    NamedPipeName = options.NamedPipeName,
                    ArtifactRoot = options.ArtifactRoot,
                    RecordUserSession = true,
                    RecordingRoot = options.RecordingRoot,
                    StartupRecordingPath = options.StartupRecordingPath,
                    DisableRetainedRenderList = options.DisableRetainedRenderList,
                    DisableDirtyRegionRendering = options.DisableDirtyRegionRendering
                };
                break;
            case "--inkkoops-record-root" when i + 1 < args.Length:
                options = new InkkOopsRuntimeOptions
                {
                    StartupScriptName = options.StartupScriptName,
                    AdditionalScriptAssemblyPaths = options.AdditionalScriptAssemblyPaths,
                    ActionDiagnosticsIndexes = options.ActionDiagnosticsIndexes,
                    NamedPipeName = options.NamedPipeName,
                    ArtifactRoot = options.ArtifactRoot,
                    RecordUserSession = options.RecordUserSession,
                    RecordingRoot = args[++i],
                    StartupRecordingPath = options.StartupRecordingPath,
                    DisableRetainedRenderList = options.DisableRetainedRenderList,
                    DisableDirtyRegionRendering = options.DisableDirtyRegionRendering
                };
                break;
            case "--inkkoops-recording" when i + 1 < args.Length:
                options = new InkkOopsRuntimeOptions
                {
                    StartupScriptName = options.StartupScriptName,
                    AdditionalScriptAssemblyPaths = options.AdditionalScriptAssemblyPaths,
                    ActionDiagnosticsIndexes = options.ActionDiagnosticsIndexes,
                    NamedPipeName = options.NamedPipeName,
                    ArtifactRoot = options.ArtifactRoot,
                    RecordUserSession = options.RecordUserSession,
                    RecordingRoot = options.RecordingRoot,
                    StartupRecordingPath = args[++i],
                    DisableRetainedRenderList = options.DisableRetainedRenderList,
                    DisableDirtyRegionRendering = options.DisableDirtyRegionRendering
                };
                break;
            case "--inkkoops-disable-retained":
                options = new InkkOopsRuntimeOptions
                {
                    StartupScriptName = options.StartupScriptName,
                    AdditionalScriptAssemblyPaths = options.AdditionalScriptAssemblyPaths,
                    ActionDiagnosticsIndexes = options.ActionDiagnosticsIndexes,
                    NamedPipeName = options.NamedPipeName,
                    ArtifactRoot = options.ArtifactRoot,
                    RecordUserSession = options.RecordUserSession,
                    RecordingRoot = options.RecordingRoot,
                    StartupRecordingPath = options.StartupRecordingPath,
                    DisableRetainedRenderList = true,
                    DisableDirtyRegionRendering = options.DisableDirtyRegionRendering
                };
                break;
            case "--inkkoops-disable-dirty":
                options = new InkkOopsRuntimeOptions
                {
                    StartupScriptName = options.StartupScriptName,
                    AdditionalScriptAssemblyPaths = options.AdditionalScriptAssemblyPaths,
                    ActionDiagnosticsIndexes = options.ActionDiagnosticsIndexes,
                    NamedPipeName = options.NamedPipeName,
                    ArtifactRoot = options.ArtifactRoot,
                    RecordUserSession = options.RecordUserSession,
                    RecordingRoot = options.RecordingRoot,
                    StartupRecordingPath = options.StartupRecordingPath,
                    DisableRetainedRenderList = options.DisableRetainedRenderList,
                    DisableDirtyRegionRendering = true
                };
                break;
        }
    }

    return options;
}

using var game = new Game1(ParseInkkOopsOptions(args));
game.Run();
