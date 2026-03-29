namespace InkkSlinger.Cli;

internal interface IInkkOopsLaunchTargetResolver
{
    InkkOopsLaunchTarget Resolve(IReadOnlyDictionary<string, string> options);
}
