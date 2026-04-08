using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsWaitFramesCommand : IInkkOopsCommand
{
    public InkkOopsWaitFramesCommand(int frameCount)
    {
        if (frameCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        }

        FrameCount = frameCount;
    }

    public int FrameCount { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return $"WaitFrames({FrameCount})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.WaitFramesAsync(FrameCount, cancellationToken);
    }
}
