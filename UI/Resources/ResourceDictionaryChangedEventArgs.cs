namespace InkkSlinger;

public enum ResourceDictionaryChangeAction
{
    Add,
    Update,
    Remove,
    Clear,
    MergeChanged
}

public sealed class ResourceDictionaryChangedEventArgs : System.EventArgs
{
    public ResourceDictionaryChangedEventArgs(ResourceDictionaryChangeAction action, object? key)
    {
        Action = action;
        Key = key;
    }

    public ResourceDictionaryChangeAction Action { get; }

    public object? Key { get; }
}
