using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsTextInputCommand : IInkkOopsCommand
{
    public InkkOopsTextInputCommand(char character)
    {
        Character = character;
    }

    public char Character { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"TextInput({Character})";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        return session.TextInputAsync(Character, cancellationToken);
    }
}
