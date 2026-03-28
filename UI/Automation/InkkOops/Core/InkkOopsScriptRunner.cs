using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsScriptRunner
{
    public async Task<InkkOopsRunResult> RunAsync(
        InkkOopsScript script,
        InkkOopsSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(session);
        var started = Stopwatch.GetTimestamp();
        var currentIndex = -1;
        var currentDescription = string.Empty;

        try
        {
            for (var i = 0; i < script.Commands.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentIndex = i;
                currentDescription = script.Commands[i].Describe();
                session.BeginCommand(i, currentDescription, script.Commands[i].ExecutionMode);
                session.Artifacts.LogCommand(i, currentDescription);
                try
                {
                    await script.Commands[i].ExecuteAsync(session, cancellationToken).ConfigureAwait(false);
                    session.CompleteCommand();
                }
                catch
                {
                    throw;
                }
            }

            return new InkkOopsRunResult(
                InkkOopsRunStatus.Completed,
                script.Name,
                session.Artifacts.DirectoryPath,
                script.Commands.Count,
                duration: Stopwatch.GetElapsedTime(started));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            session.FailCommand(ex);
            return new InkkOopsRunResult(
                InkkOopsRunStatus.Failed,
                script.Name,
                session.Artifacts.DirectoryPath,
                script.Commands.Count,
                failedCommandIndex: currentIndex >= 0 ? currentIndex : TryGetLastLoggedCommandIndex(session.Artifacts.GetPath("commands.log")),
                failedCommandDescription: currentDescription,
                failureMessage: ex.ToString(),
                duration: Stopwatch.GetElapsedTime(started));
        }
    }

    private static int? TryGetLastLoggedCommandIndex(string logPath)
    {
        if (!System.IO.File.Exists(logPath))
        {
            return null;
        }

        var lines = System.IO.File.ReadAllLines(logPath);
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i];
            var marker = "command[";
            var start = line.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                continue;
            }

            start += marker.Length;
            var end = line.IndexOf(']', start);
            if (end > start &&
                int.TryParse(line.AsSpan(start, end - start), out var index))
            {
                return index;
            }
        }

        return null;
    }
}
