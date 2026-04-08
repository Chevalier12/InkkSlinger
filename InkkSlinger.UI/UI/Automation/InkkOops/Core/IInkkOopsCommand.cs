using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public interface IInkkOopsCommand
{
    InkkOopsExecutionMode ExecutionMode { get; }

    string Describe();

    Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default);
}
