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
                var semanticBefore = await CaptureSemanticSnapshotAsync(session, command, i, currentDescription, cancellationToken).ConfigureAwait(false);
                session.Artifacts.LogCommand(i, currentDescription);
                await command.ExecuteAsync(session, cancellationToken).ConfigureAwait(false);
                await LogSemanticStateAsync(session, command, i, currentDescription, semanticBefore, cancellationToken).ConfigureAwait(false);
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
                failedCommandIndex: currentIndex >= 0 ? currentIndex : TryGetLastLoggedCommandIndex(session.Artifacts.GetCommandLogPath()),
                failedCommandDescription: currentDescription,
                failureCategory: failureCategory,
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

    private static bool ShouldLogSemanticEntry(IInkkOopsCommand command)
    {
        return command is InkkOopsHoverTargetCommand or
               InkkOopsMovePointerCommand or
               InkkOopsClickTargetCommand or
               InkkOopsPointerDownCommand or
               InkkOopsPointerUpCommand or
               InkkOopsWheelCommand or
               InkkOopsDragTargetCommand or
               InkkOopsInvokeTargetCommand or
               InkkOopsScrollByCommand or
               InkkOopsScrollToCommand or
               InkkOopsScrollIntoViewCommand;
    }

    private static async Task<InkkOopsSemanticSnapshot> CaptureSemanticSnapshotAsync(
        InkkOopsSession session,
        IInkkOopsCommand command,
        int index,
        string commandDescription,
        CancellationToken cancellationToken)
    {
        return await session.QueryOnUiThreadAsync(
            () => InkkOopsSemanticLogFormatter.Capture(
                command,
                session.UiRoot,
                session.Host.SemanticLogContributors,
                index,
                commandDescription),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task LogSemanticStateAsync(
        InkkOopsSession session,
        IInkkOopsCommand command,
        int index,
        string commandDescription,
        InkkOopsSemanticSnapshot semanticBefore,
        CancellationToken cancellationToken)
    {
        if (!ShouldLogSemanticEntry(command))
        {
            return;
        }

        var semanticAfter = await CaptureSemanticSnapshotAsync(session, command, index, commandDescription, cancellationToken).ConfigureAwait(false);
        var entry = InkkOopsSemanticLogFormatter.Format(
            command,
            index,
            commandDescription,
            semanticBefore,
            semanticAfter,
            session.Host.SemanticLogContributors);

        if (entry is { } semanticEntry)
        {
            session.Artifacts.LogSemanticEntry(semanticEntry.Subject, semanticEntry.Details);
        }
    }

}
