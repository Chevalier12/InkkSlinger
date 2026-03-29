namespace InkkSlinger;

public interface IInkkOopsReplayPostamblePolicy
{
    void Apply(InkkOopsScriptBuilder builder, string recordingPath, IInkkOopsArtifactNamingPolicy namingPolicy);
}
