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
                var command = script.Commands[i];
                currentDescription = command.Describe();
                session.BeginActionCommand(i);

                try
                {
                    var plannedDisplayedFps = await session.QueryOnUiThreadAsync(() => session.Host.GetDisplayedFps(), cancellationToken).ConfigureAwait(false);
                    foreach (var entry in InkkOopsActionLogFormatter.CreatePlannedEntries(command, i, currentDescription, plannedDisplayedFps))
                    {
                        session.Artifacts.LogActionEntry(entry.Subject, entry.Details);
                    }

                    await command.ExecuteAsync(session, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    session.EndActionCommand();
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
            var failureCategory = ex is InkkOopsCommandException commandException
                ? commandException.Category
                : InkkOopsFailureCategory.None;
            return new InkkOopsRunResult(
                InkkOopsRunStatus.Failed,
                script.Name,
                session.Artifacts.DirectoryPath,
                script.Commands.Count,
                failedCommandIndex: currentIndex >= 0 ? currentIndex : session.Artifacts.GetLastLoggedActionIndex(),
                failedCommandDescription: currentDescription,
                failureCategory: failureCategory,
                failureMessage: ex.ToString(),
                duration: Stopwatch.GetElapsedTime(started));
        }
    }

}
