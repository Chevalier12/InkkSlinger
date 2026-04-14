using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed class InkkOopsKeyUpCommand : IInkkOopsCommand
{
    public InkkOopsKeyUpCommand(Keys key)
    {
        Key = key;
    }

    public Keys Key { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"KeyUp({Key})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.KeyUpAsync(Key, cancellationToken);
    }
}
