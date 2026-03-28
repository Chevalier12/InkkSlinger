using System;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsResizeWindowCommand : IInkkOopsCommand
{
    public InkkOopsResizeWindowCommand(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return $"ResizeWindow({Width}, {Height})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.ResizeWindowAsync(Width, Height, cancellationToken);
    }
}
