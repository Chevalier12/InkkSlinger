using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed class InkkOopsKeyDownCommand : IInkkOopsCommand
{
    public InkkOopsKeyDownCommand(Keys key)
    {
        Key = key;
    }

    public Keys Key { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"KeyDown({Key})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.KeyDownAsync(Key, cancellationToken);
    }
}
