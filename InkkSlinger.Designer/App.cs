using System;
using System.Collections.Generic;
using InkkSlinger;

namespace InkkSlinger.Designer;

internal static class App
{
    internal static InkkOopsRuntimeOptions ParseInkkOopsOptions(string[] args)
    {
        var options = new InkkOopsRuntimeOptions
        {
            LaunchProjectPath = InkkOopsProjectPathResolver.ResolveCurrentEntryProjectPath()
        };
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--inkkoops-script" when i + 1 < args.Length:
                    options = CopyOptions(options, startupScriptName: args[++i]);
                    break;
                case "--inkkoops-script-assembly" when i + 1 < args.Length:
                    options = CopyOptions(
                        options,
                        additionalScriptAssemblyPaths: AppendScriptAssemblyPath(options.AdditionalScriptAssemblyPaths, args[++i]));
                    break;
                case "--inkkoops-pipe" when i + 1 < args.Length:
                    options = CopyOptions(options, namedPipeName: args[++i]);
                    break;
                case "--inkkoops-artifacts" when i + 1 < args.Length:
                    options = CopyOptions(options, artifactRoot: args[++i]);
                    break;
                case "--inkkoops-action-diagnostics" when i + 1 < args.Length:
                    options = CopyOptions(options, actionDiagnosticsIndexes: ParseActionDiagnosticsIndexes(args[++i]));
                    break;
                case "--inkkoops-record":
                    options = CopyOptions(options, recordUserSession: true);
                    break;
                case "--inkkoops-project" when i + 1 < args.Length:
                    options = CopyOptions(options, launchProjectPath: args[++i]);
                    break;
                case "--inkkoops-record-root" when i + 1 < args.Length:
                    options = CopyOptions(options, recordingRoot: args[++i]);
                    break;
                case "--inkkoops-recording" when i + 1 < args.Length:
                    options = CopyOptions(options, startupRecordingPath: args[++i]);
                    break;
                case "--inkkoops-disable-retained":
                    options = CopyOptions(options, disableRetainedRenderList: true);
                    break;
                case "--inkkoops-disable-dirty":
                    options = CopyOptions(options, disableDirtyRegionRendering: true);
                    break;
            }
        }

        return options;
    }

    private static int[] ParseActionDiagnosticsIndexes(string? text)
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

    private static string[] AppendScriptAssemblyPath(string[] existing, string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return existing;
        }

        return [.. existing, assemblyPath];
    }

    private static InkkOopsRuntimeOptions CopyOptions(
        InkkOopsRuntimeOptions options,
        string? startupScriptName = null,
        string[]? additionalScriptAssemblyPaths = null,
        int[]? actionDiagnosticsIndexes = null,
        string? namedPipeName = null,
        string? artifactRoot = null,
        bool? recordUserSession = null,
        string? launchProjectPath = null,
        string? recordingRoot = null,
        string? startupRecordingPath = null,
        bool? disableRetainedRenderList = null,
        bool? disableDirtyRegionRendering = null)
    {
        return new InkkOopsRuntimeOptions
        {
            StartupScriptName = startupScriptName ?? options.StartupScriptName,
            AdditionalScriptAssemblyPaths = additionalScriptAssemblyPaths ?? options.AdditionalScriptAssemblyPaths,
            ActionDiagnosticsIndexes = actionDiagnosticsIndexes ?? options.ActionDiagnosticsIndexes,
            NamedPipeName = namedPipeName ?? options.NamedPipeName,
            ArtifactRoot = artifactRoot ?? options.ArtifactRoot,
            RecordUserSession = recordUserSession ?? options.RecordUserSession,
            LaunchProjectPath = launchProjectPath ?? options.LaunchProjectPath,
            RecordingRoot = recordingRoot ?? options.RecordingRoot,
            StartupRecordingPath = startupRecordingPath ?? options.StartupRecordingPath,
            DisableRetainedRenderList = disableRetainedRenderList ?? options.DisableRetainedRenderList,
            DisableDirtyRegionRendering = disableDirtyRegionRendering ?? options.DisableDirtyRegionRendering
        };
    }
}
